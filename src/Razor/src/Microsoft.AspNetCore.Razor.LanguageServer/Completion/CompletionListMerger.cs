// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal static class CompletionListMerger
    {
        private static readonly string Data1Key = nameof(MergedCompletionListData.Data1).ToLowerInvariant();
        private static readonly string Data2Key = nameof(MergedCompletionListData.Data2).ToLowerInvariant();

        public static object? MergeData(object? data1, object? data2)
        {
            if (data1 is null)
            {
                return data2;
            }

            if (data2 is null)
            {
                return data1;
            }

            return new MergedCompletionListData(data1, data2);
        }

        public static VSInternalCompletionList? Merge(VSInternalCompletionList? completionListA, VSInternalCompletionList? completionListB)
        {
            if (completionListA is null)
            {
                return completionListB;
            }
            else if (completionListB is null)
            {
                return completionListA;
            }

            var mergedIsIncomplete = completionListA.IsIncomplete || completionListA.IsIncomplete;
            var aHasCommitCharacters = completionListA.CommitCharacters is not null || completionListA.ItemDefaults?.CommitCharacters is not null;
            var bHasCommitCharacters = completionListB.CommitCharacters is not null || completionListB.ItemDefaults?.CommitCharacters is not null;
            if (aHasCommitCharacters && bHasCommitCharacters)
            {
                // Need to merge commit characters
                var inheritableCommitCharacterCompletionsA = GetInheritableCommitCharacterCompletions(completionListA);
                var inheritableCommitCharacterCompletionsB = GetInheritableCommitCharacterCompletions(completionListB);
                IReadOnlyList<VSInternalCompletionItem>? inheritableCommitCharacterCompletions;
                VSInternalCompletionList? completionListToRestore;

                if (inheritableCommitCharacterCompletionsA.Count >= inheritableCommitCharacterCompletionsB.Count)
                {
                    completionListToRestore = completionListA;
                    inheritableCommitCharacterCompletions = inheritableCommitCharacterCompletionsA;
                }
                else
                {
                    completionListToRestore = completionListB;
                    inheritableCommitCharacterCompletions = inheritableCommitCharacterCompletionsB;
                }

                for (var i = 0; i < inheritableCommitCharacterCompletions.Count; i++)
                {
                    if (completionListToRestore.CommitCharacters is not null)
                    {
                        inheritableCommitCharacterCompletions[i].VsCommitCharacters = completionListToRestore.CommitCharacters;
                    }
                    else if (completionListToRestore.ItemDefaults?.CommitCharacters is not null)
                    {
                        inheritableCommitCharacterCompletions[i].VsCommitCharacters = completionListToRestore.ItemDefaults?.CommitCharacters;
                    }
                }

                completionListToRestore.CommitCharacters = null;
            }

            if (completionListA.Data != completionListB.Data &&
                completionListA.Data is null || completionListB.Data is null)
            {
                // One of the completion lists have data while the other does not, we need to ensure that any non-data centric items don't get incorrect data associated

                // The candidate completion list will be one where we populate empty data for any `null` specifying data given we'll be merging
                // two completion lists together we don't want incorrect data to be inherited down
                var candidateCompletionList = completionListA.Data is null ? completionListA : completionListB;
                for (var i = 0; i < candidateCompletionList.Items.Length; i++)
                {
                    var item = candidateCompletionList.Items[i];
                    if (item.Data is null)
                    {
                        item.Data = new object();
                    }
                }
            }

            var mergedItems = completionListA.Items.Concat(completionListB.Items).ToArray();
            var mergedData = MergeData(completionListA.Data, completionListB.Data);
            var mergedCommitCharacters = completionListA.CommitCharacters ?? completionListB.CommitCharacters;
            var mergedSuggestionMode = completionListA.SuggestionMode || completionListB.SuggestionMode;
            var mergedContinueWithCharacters = completionListA.ContinueCharacters ?? completionListB.ContinueCharacters;
            var mergedItemDefaultsEditRange = completionListA.ItemDefaults?.EditRange ?? completionListB.ItemDefaults?.EditRange;

            var mergedCompletionList = new VSInternalCompletionList()
            {
                CommitCharacters = mergedCommitCharacters,
                Data = mergedData,
                IsIncomplete = mergedIsIncomplete,
                Items = mergedItems,
                SuggestionMode = mergedSuggestionMode,
                ContinueCharacters = mergedContinueWithCharacters,
                ItemDefaults = new CompletionListItemDefaults()
                {
                    EditRange = mergedItemDefaultsEditRange,
                }
            };

            return mergedCompletionList;
        }

        public static bool TrySplit(object? data, [NotNullWhen(true)] out IReadOnlyList<JObject>? splitData)
        {
            if (data is null)
            {
                splitData = null;
                return false;
            }

            var collector = new List<JObject>();
            Split(data, collector);

            if (collector.Count == 0)
            {
                splitData = null;
                return false;
            }

            splitData = collector;
            return true;
        }

        private static void Split(object data, List<JObject> collector)
        {
            if (data is not JObject jobject)
            {
                return;
            }

            if (!jobject.ContainsKey(Data1Key) ||
                !jobject.ContainsKey(Data2Key))
            {
                // Normal, non-merged data
                collector.Add(jobject);
            }
            else
            {
                // Merged data
                var mergedCompletionListData = jobject.ToObject<MergedCompletionListData>();

                if (mergedCompletionListData is null)
                {
                    Debug.Fail("Merged completion list data is null");
                    return;
                }

                Split(mergedCompletionListData.Data1, collector);
                Split(mergedCompletionListData.Data2, collector);
            }
        }

        private static IReadOnlyList<VSInternalCompletionItem> GetInheritableCommitCharacterCompletions(VSInternalCompletionList completionList)
        {
            var inheritableCompletions = new List<VSInternalCompletionItem>();

            for (var i = 0; i < completionList.Items.Length; i++)
            {
                var completionItem = completionList.Items[i] as VSInternalCompletionItem;
                if (completionItem is null ||
                    completionItem.CommitCharacters is not null ||
                    completionItem.VsCommitCharacters is not null)
                {
                    // Completion item wasn't the right type or already specifies commit characters (it wasn't optimized)
                    continue;
                }

                inheritableCompletions.Add(completionItem);
            }

            return inheritableCompletions;
        }

        private record MergedCompletionListData(object Data1, object Data2);
    }
}
