// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Diagnostics
{
    public class RazorPullDiagnosticsEndpointTest : LanguageServerTestBase
    {
        private readonly RazorDocumentMappingService _mappingService;

        public RazorPullDiagnosticsEndpointTest(ITestOutputHelper testOutput)
            : base(testOutput)
        {
            _mappingService = new DefaultRazorDocumentMappingService(
                TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);
        }

        [Fact]
        public async Task Handle_WithSingleServerOff_ReturnsNullAsync()
        {
            // Arrange
            var uri = new Uri("C:/path/to/document.cshtml");
            var endpoint = GetEndpoint(useSingleServer: false);
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
            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(client => client.SendRequestAsync<IDelegatedParams, IEnumerable<VSInternalDiagnosticReport>?>(
                    RazorLanguageServerCustomMessageTargets.RazorPullDiagnosticEndpointName, It.IsAny<DelegatedDiagnosticParams>(), DisposalToken))
                .ReturnsAsync(cSharpDiagnostics);
            var endpoint = GetEndpoint(useSingleServer: true, languageServer.Object);
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

        private RazorPullDiagnosticsEndpoint GetEndpoint(bool useSingleServer, ClientNotifierServiceBase? languageServer = null)
        {
            var languageServerFeatureOptions = new TestLanguageServerFeatureOptions(singleServerSupport: useSingleServer);
            languageServer ??= new Mock<ClientNotifierServiceBase>(MockBehavior.Strict).Object;
            var endpoint = new RazorPullDiagnosticsEndpoint(languageServerFeatureOptions, _mappingService, languageServer, Logger);

            return endpoint;
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
