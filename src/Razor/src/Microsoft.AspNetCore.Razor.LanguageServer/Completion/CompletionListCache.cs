// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal sealed class CompletionListCache
    {
        // Internal for testing
        internal static readonly int MaxCacheSize = 3;

        private readonly object _accessLock;
        private readonly List<CompletionCacheEntry> _resultIdToCompletionList;
        private long _nextResultId;

        public CompletionListCache()
        {
            _accessLock = new object();
            _resultIdToCompletionList = new List<CompletionCacheEntry>();
        }

        public long Set(IReadOnlyList<RazorCompletionItem> razorCompletionList, DelegatedCompletionResult? delegatedCompletionResult)
        {
            if (razorCompletionList is null)
            {
                throw new ArgumentNullException(nameof(razorCompletionList));
            }

            lock (_accessLock)
            {
                // If cache exceeds maximum size, remove the oldest list in the cache
                if (_resultIdToCompletionList.Count >= MaxCacheSize)
                {
                    _resultIdToCompletionList.RemoveAt(0);
                }

                var resultId = _nextResultId++;

                var cacheEntry = new CompletionCacheEntry(resultId, razorCompletionList, delegatedCompletionResult);
                _resultIdToCompletionList.Add(cacheEntry);

                // Return generated resultId so completion list can later be retrieved from cache
                return resultId;
            }
        }

        public bool TryGet(
            long resultId,
            [NotNullWhen(true)] out IReadOnlyList<RazorCompletionItem>? razorCompletionList,
            out DelegatedCompletionResult? delegatedCompletionResult)
        {
            lock (_accessLock)
            {
                // Search back -> front because the items in the back are the most recently added which are most frequently accessed.
                for (var i = _resultIdToCompletionList.Count - 1; i >= 0; i--)
                {
                    var cacheEntry = _resultIdToCompletionList[i];
                    if (cacheEntry.ResultId == resultId)
                    {
                        razorCompletionList = cacheEntry.RazorCompletionList;
                        delegatedCompletionResult = cacheEntry.DelegatedCompletionResult;
                        return true;
                    }
                }

                // Completion lists associated with the given resultId were not found
                razorCompletionList = null;
                delegatedCompletionResult = null;
                return false;
            }
        }

        private record CompletionCacheEntry(long ResultId, IReadOnlyList<RazorCompletionItem> RazorCompletionList, DelegatedCompletionResult? DelegatedCompletionResult);
    }
}
