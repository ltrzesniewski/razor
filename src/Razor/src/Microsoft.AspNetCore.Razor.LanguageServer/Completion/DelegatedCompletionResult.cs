// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class DelegatedCompletionResult
    {
        public DelegatedCompletionResult(
            VSInternalCompletionList? completionList,
            DelegatedCompletionParams delegationParams)
        {
            CompletionList = completionList;
            DelegationParams = delegationParams;
        }

        public VSInternalCompletionList? CompletionList { get; }

        public DelegatedCompletionParams DelegationParams { get; }
    }
}
