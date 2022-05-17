// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal static class VSInternalCompletionItemExtensions
    {
        private const string ResultIdKey = "_resultId";

        public static void SetResultId(
            this CompletionItem completionItem,
            long resultId)
        {
            if (completionItem is null)
            {
                throw new ArgumentNullException(nameof(completionItem));
            }

            var data = new JObject()
            {
                [ResultIdKey] = resultId,
            };
            var mergedData = CompletionListMerger.MergeData(data, completionItem.Data);
            completionItem.Data = mergedData;
        }

        public static bool TryGetCompletionListResultId(this CompletionItem completion, [NotNullWhen(true)] out int? resultId)
        {
            if (completion is null)
            {
                throw new ArgumentNullException(nameof(completion));
            }

            if (!CompletionListMerger.TrySplit(completion.Data, out var splitData))
            {
                resultId = default;
                return false;
            }

            for (var i = 0; i < splitData.Count; i++)
            {
                var data = splitData[i];
                if (data.ContainsKey(ResultIdKey))
                {
                    resultId = data[ResultIdKey]?.ToObject<int>();
                    return resultId is not null;
                }
            }

            resultId = default;
            return false;
        }

        public static void UseCommitCharactersFrom(
            this VSInternalCompletionItem completionItem,
            RazorCompletionItem razorCompletionItem,
            VSInternalClientCapabilities clientCapabilities)
        {
            if (razorCompletionItem.CommitCharacters == null || razorCompletionItem.CommitCharacters.Count == 0)
            {
                return;
            }

            var supportsVSExtensions = clientCapabilities?.SupportsVisualStudioExtensions ?? false;
            if (supportsVSExtensions)
            {
                var vsCommitCharacters = razorCompletionItem
                    .CommitCharacters
                    .Select(c => new VSInternalCommitCharacter() { Character = c.Character, Insert = c.Insert })
                    .ToArray();
                completionItem.VsCommitCharacters = vsCommitCharacters;
            }
            else
            {
                completionItem.CommitCharacters = razorCompletionItem
                    .CommitCharacters
                    .Select(c => c.Character)
                    .ToArray();
            }
        }
    }
}
