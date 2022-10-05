// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal class RazorPullDiagnosticsEndpoint : IRazorPullDiagnosticsEndpoint
{
    public bool MutatesSolutionState => false;

    public RegistrationExtensionResult GetRegistration(VSInternalClientCapabilities clientCapabilities)
    {
        return new RegistrationExtensionResult("_vs_supportsDiagnosticRequests", options: clientCapabilities.SupportsDiagnosticRequests);
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(VSInternalDocumentDiagnosticsParams request)
    {
        if (request.TextDocument is null)
        {
            throw new NotImplementedException("Why would document be null?");
        }

        return request.TextDocument;
    }

    public async Task<IEnumerable<VSInternalDiagnosticReport>?> HandleRequestAsync(VSInternalDocumentDiagnosticsParams request, RazorRequestContext context, CancellationToken cancellationToken)
    {
        var result = await GetPullDiagnosticsAsync(request, context, cancellationToken);

        return result;
    }

    private async Task<IEnumerable<VSInternalDiagnosticReport>?> GetPullDiagnosticsAsync(VSInternalDocumentDiagnosticsParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var clientNotifier = requestContext.GetRequiredService<ClientNotifierServiceBase>();
        var documentContext = requestContext.GetRequiredDocumentContext();
        var versionedTextDocumentIdentifier = new VersionedTextDocumentIdentifier
        {
            Uri = request.TextDocument!.Uri,
            Version = documentContext.Version,
        };

        var delegatedParams = new DelegatedDiagnosticParams(versionedTextDocumentIdentifier);

        var result = await clientNotifier.SendRequestAsync<DelegatedDiagnosticParams, VSInternalDiagnosticReport[]?>(
            RazorLanguageServerCustomMessageTargets.RazorPullDiagnosticEndpointName,
            delegatedParams,
            cancellationToken);

        var mappingService = requestContext.GetRequiredService<RazorDocumentMappingService>();
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);

        var mappedResults = MapDelegatedResults(result, codeDocument, mappingService);

        return mappedResults;
    }

    private IEnumerable<VSInternalDiagnosticReport> MapDelegatedResults(IEnumerable<VSInternalDiagnosticReport>? reports, RazorCodeDocument codeDocument, RazorDocumentMappingService mappingService)
    {
        if (reports is null)
        {
            return Array.Empty<VSInternalDiagnosticReport>();
        }

        foreach (var report in reports)
        {
            if (report.Diagnostics is not null)
            {
                foreach (var diagnostic in report.Diagnostics)
                {
                    if (mappingService.TryMapFromProjectedDocumentRange(codeDocument, diagnostic.Range, out var razorRange))
                    {
                        diagnostic.Range = razorRange;
                    }
                }
            }
        }

        return reports;
    }
}
