// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Diagnostics
{
    public class RazorPullDiagnosticsEndpointTest : SingleServerDelegatingEndpointTestBase
    {
        public RazorPullDiagnosticsEndpointTest(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Fact]
        public async Task Handle_WithSingleServerOff_ReturnsNullAsync()
        {
            // Arrange
            var uri = new Uri("C:/path/to/document.cshtml");
            var request = new VSInternalDocumentDiagnosticsParamsBridge
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = uri,
                },
            };

            var codeDocument = CreateCodeDocumentWithCSharpProjection(
                " @{ void Foo< @* Comment! *@ TValue>() {} }  ",
                "    void Foo<  TValue>() {} ",
                new[] {
                    // "Foo< "
                    new SourceMapping(
                        new SourceSpan(3, 11),
                        new SourceSpan(3, 11)),

                    // " TValue>"
                    new SourceMapping(
                        new SourceSpan(28, 14),
                        new SourceSpan(15, 13)),
                });
            await CreateLanguageServerAsync(codeDocument, uri.ToString(), singleServerSupport: false);

            var endpoint = new RazorPullDiagnosticsEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, Logger);
            var documentContext = CreateDocumentContext(uri, codeDocument);
            var requestContext = CreateRazorRequestContext(documentContext);

            // Act
            var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_CSharpDiagnostics_Remapped()
        {
            // Arrange
            var uri = new Uri("C:/path/to/document.cshtml");
            var cSharpDiagnostics = new VSInternalDiagnosticReport[]
            {
                new VSInternalDiagnosticReport{
                    Diagnostics = new Diagnostic[]{
                        new Diagnostic
                        {
                            Message = "Message",
                            Range = new Range
                            {
                                Start = new Position(0, 15),
                                End = new Position(0, 21),
                            },
                        },
                    },
                },
            };
            var codeDocument = CreateCodeDocument(" @{ void Foo< @* Comment! *@ TValue>() {} }  ");
            await CreateLanguageServerAsync(codeDocument, uri.ToString());
            var endpoint = new RazorPullDiagnosticsEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, Logger);
            var request = new VSInternalDocumentDiagnosticsParamsBridge
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = uri,
                },
            };
            var documentContext = CreateDocumentContext(uri, codeDocument);
            var requestContext = CreateRazorRequestContext(documentContext);

            // Act
            var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

            // Assert
            Assert.NotNull(result);
            var expectedRange = new Range
            {
                Start = new Position(0, 28),
                End = new Position(0, 34),
            };
            Assert.Collection(result,
                (report) =>
                {
                    Assert.Collection(report.Diagnostics, (diagnostics) =>
                    {
                        Assert.Equal("Message", diagnostics.Message);
                        Assert.Equal(expectedRange, diagnostics.Range);
                    });
                });
        }

        private static RazorCodeDocument CreateCodeDocumentWithCSharpProjection(
            string razorSource,
            string projectedCSharpSource,
            IEnumerable<SourceMapping> sourceMappings,
            IReadOnlyList<TagHelperDescriptor>? tagHelpers = null)
        {
            var codeDocument = CreateCodeDocument(razorSource, tagHelpers);
            var csharpDocument = RazorCSharpDocument.Create(
                    projectedCSharpSource,
                    RazorCodeGenerationOptions.CreateDefault(),
                    Enumerable.Empty<RazorDiagnostic>(),
                    sourceMappings,
                    Enumerable.Empty<LinePragma>());
            codeDocument.SetCSharpDocument(csharpDocument);
            return codeDocument;
        }
    }
}
