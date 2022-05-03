// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage.MessageInterception;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(MessageInterceptor))]
    [InterceptMethod(Methods.WorkspaceSymbolName)]
    [ContentType(RazorLSPConstants.HtmlLSPContentTypeName)]
    [ContentType(RazorLSPConstants.CssLSPContentTypeName)]
    [ContentType(RazorLSPConstants.TypeScriptLSPContentTypeName)]
    internal class RazorHtmlWorkspaceSymbolInterceptor : MessageInterceptor
    {
        public override Task<InterceptionResult> ApplyChangesAsync(JToken message, string containedLanguageName, CancellationToken cancellationToken)
        {
            return Task.FromResult(InterceptionResult.NoChange);
        }
    }
}
