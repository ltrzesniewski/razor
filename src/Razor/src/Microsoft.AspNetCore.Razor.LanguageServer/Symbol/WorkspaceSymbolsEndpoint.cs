// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Symbol
{
    internal class WorkspaceSymbolsEndpoint : IWorkspaceSymbolsHandler
    {
        public WorkspaceSymbolRegistrationOptions GetRegistrationOptions(WorkspaceSymbolCapability capability, ClientCapabilities clientCapabilities)
            => new();

        public Task<Container<SymbolInformation>?> Handle(WorkspaceSymbolParams request, CancellationToken cancellationToken)
            => Task.FromResult<Container<SymbolInformation>?>(null);
    }
}
