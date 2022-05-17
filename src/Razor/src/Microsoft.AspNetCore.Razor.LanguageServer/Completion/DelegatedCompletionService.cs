// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class DelegatedCompletionService
    {
        private readonly ILogger _logger;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ClientNotifierServiceBase _languageServer;

        private static readonly IReadOnlyList<string> s_razorTriggerCharacters = new[] { "@" };
        private static readonly IReadOnlyList<string> s_cSharpTriggerCharacters = new[] { " ", "(", "=", "#", ".", "<", "[", "{", "\"", "/", ":", "~" };
        private static readonly IReadOnlyList<string> s_htmlTriggerCharacters = new[] { ":", "@", "#", ".", "!", "*", ",", "(", "[", "-", "<", "&", "\\", "/", "'", "\"", "=", ":", " ", "`" };

        public static readonly IReadOnlyList<string> AllTriggerCharacters = new HashSet<string>(
            s_cSharpTriggerCharacters
                .Concat(s_htmlTriggerCharacters)
                .Concat(s_razorTriggerCharacters))
            .ToArray();

        private static readonly IReadOnlyCollection<string> s_designTimeHelpers = new string[]
        {
            "__builder",
            "__o",
            "__RazorDirectiveTokenHelpers__",
            "__tagHelperExecutionContext",
            "__tagHelperRunner",
            "__typeHelper",
            "_Imports",
            "BuildRenderTree"
        };

        private static readonly IReadOnlyCollection<CompletionItem> s_designTimeHelpersCompletionItems = GenerateCompletionItems(s_designTimeHelpers);

        public DelegatedCompletionService(
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
        {
            _documentMappingService = documentMappingService;
            _languageServer = languageServer;
            _logger = loggerFactory.CreateLogger<RazorCompletionEndpoint>();
        }

        public async Task<DelegatedCompletionResult?> GetCompletionListAsync(CompletionParams request, DocumentSnapshot documentSnapshot, int documentVersion, CancellationToken cancellationToken)
        {
            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            if (!request.Position.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex))
            {
                return null;
            }

            var projection = GetProjection(request.Position, codeDocument, sourceText);

            var completionContext = (VSInternalCompletionContext?)request.Context;
            completionContext = completionContext == null ? null : RewriteContext(completionContext, projection.LanguageKind);
            var provisionalCompletion = TryGetProvisionalCompletionInfo(request, projection, codeDocument, sourceText);
            TextEdit? provisionalTextEdit = null;
            if (provisionalCompletion != null)
            {
                provisionalTextEdit = provisionalCompletion.ProvisionalTextEdit;
                projection = provisionalCompletion.ProvisionalProjection;
            }

            var delegatedParams = new DelegatedCompletionParams(
                projection.Position,
                completionContext,
                provisionalTextEdit,
                projection.LanguageKind,
                request.TextDocument,
                documentVersion);
            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorCompletionEndpointName, delegatedParams).ConfigureAwait(false);
            var delegatedResponse = await delegatedRequest.Returning<VSInternalCompletionList?>(cancellationToken).ConfigureAwait(false);
            var syntaxTree = codeDocument.GetSyntaxTree();
            var location = new SourceSpan(hostDocumentIndex, 0);
            var change = new SourceChange(location, string.Empty);
            var owner = syntaxTree.Root.LocateOwner(change);

            if (delegatedResponse != null)
            {
                if (projection.LanguageKind == RazorLanguageKind.CSharp)
                {
                    delegatedResponse = PostProcessCSharpCompletionList(owner, sourceText, delegatedResponse);
                }

                delegatedResponse = TranslateTextEdits(request.Position, projection.Position, delegatedResponse);

                if (delegatedResponse.ItemDefaults?.EditRange != null)
                {
                    delegatedResponse.ItemDefaults.EditRange = TranslateRange(request.Position, projection.Position, delegatedResponse.ItemDefaults.EditRange);
                }
            }

            var delegationResult = new DelegatedCompletionResult(delegatedResponse, delegatedParams);
            return delegationResult;
        }

        private CompletionProjection GetProjection(Position requestPosition, RazorCodeDocument codeDocument, SourceText sourceText)
        {
            if (!requestPosition.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex))
            {
                throw new InvalidOperationException("Should not be able to ask for a projection at an invalid location");
            }

            var languageKind = _documentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex);
            var projectedPosition = requestPosition;
            if (languageKind == RazorLanguageKind.CSharp)
            {
                if (_documentMappingService.TryMapToVSProjectedDocumentPosition(codeDocument, hostDocumentIndex, out var mappedPosition, out var projectedIndex))
                {
                    // For C# locations, we attempt to return the corresponding position
                    // within the projected document
                    projectedPosition = mappedPosition;
                }
                else
                {
                    // It no longer makes sense to think of this location as C#, since it doesn't
                    // correspond to any position in the projected document. This should not happen
                    // since there should be source mappings for all the C# spans.
                    languageKind = RazorLanguageKind.Razor;
                }
            }

            return new CompletionProjection(languageKind, projectedPosition);
        }

        private VSInternalCompletionList PostProcessCSharpCompletionList(
            SyntaxNode owner,
            SourceText sourceText,
            VSInternalCompletionList completionList)
        {
            //var formattingOptions = _formattingOptionsProvider.GetOptions(documentSnapshot);

            //if (IsSimpleImplicitExpression(request, documentSnapshot, wordExtent))
            //{
            //    completionList = RemovePreselection(completionList);

            //    // -1 is to account for the transition so base indentation is "|@if" instead of "@|if"
            //    var baseIndentation = Math.Max(GetBaseIndentation(wordExtent, formattingOptions) - 1, 0);
            //    completionList = IncludeCSharpSnippets(baseIndentation, completionList, formattingOptions);
            //}
            ////if all completion items are properties then completion is requested inside initializer syntax and we don't need to add snippets
            //else if (IsWordOnEmptyLine(wordExtent, documentSnapshot) && !IsForPropertyInitializer(completionList))
            //{
            //    var baseIndentation = GetBaseIndentation(wordExtent, formattingOptions);
            //    completionList = IncludeCSharpSnippets(baseIndentation, completionList, formattingOptions);
            //}

            completionList = RemoveDesignTimeItems(owner, sourceText, completionList);
            return completionList;
        }

        // We should remove Razor design time helpers from C#'s completion list. If the current identifier being targeted does not start with a double
        // underscore, we trim out all items starting with "__" from the completion list. If the current identifier does start with a double underscore
        // (e.g. "__ab[||]"), we only trim out common design time helpers from the completion list.
        private static VSInternalCompletionList RemoveDesignTimeItems(
            SyntaxNode owner,
            SourceText sourceText,
            VSInternalCompletionList completionList)
        {
            var filteredItems = completionList.Items.Except(s_designTimeHelpersCompletionItems, CompletionItemComparer.Instance).ToArray();

            // If the current identifier starts with "__", only trim out common design time helpers from the list.
            // In all other cases, trim out both common design time helpers and all completion items starting with "__".
            if (ShouldRemoveAllDesignTimeItems(owner, sourceText))
            {
                filteredItems = filteredItems.Where(item => item.Label != null && !item.Label.StartsWith("__", StringComparison.Ordinal)).ToArray();
            }

            completionList.Items = filteredItems;
            return completionList;

            static bool ShouldRemoveAllDesignTimeItems(SyntaxNode owner, SourceText sourceText)
            {
                if (owner.Span.Length < 2)
                {
                    return true;
                }

                if (sourceText[owner.Span.Start] == '_' && sourceText[owner.Span.Start + 1] == '_')
                {
                    return false;
                }

                return true;
            }
        }

        // Internal for testing
        internal static bool TriggerAppliesToProjection(CompletionContext context, RazorLanguageKind languageKind)
        {
            if (languageKind == RazorLanguageKind.Razor)
            {
                // We don't handle any type of triggers in Razor pieces of the document
                return false;
            }

            if (context.TriggerKind != CompletionTriggerKind.TriggerCharacter)
            {
                // Not a trigger character completion, allow it.
                return true;
            }

            if (!AllTriggerCharacters.Contains(context.TriggerCharacter))
            {
                // This is an auto-invoked completion from the VS LSP platform. Completions are automatically invoked upon typing identifiers
                // and are represented as CompletionTriggerKind.TriggerCharacter and have a trigger character that we have not registered for.
                return true;
            }

            if (IsApplicableTriggerCharacter(context.TriggerCharacter, languageKind))
            {
                // Trigger character is associated with the langauge at the current cursor position
                return true;
            }

            // We were triggered but the trigger character doesn't make sense for the current cursor position. Bail.
            return false;
        }

        private static bool IsApplicableTriggerCharacter(string? triggerCharacter, RazorLanguageKind languageKind)
        {
            if (s_razorTriggerCharacters.Contains(triggerCharacter))
            {
                // Razor trigger characters always transition into either C# or HTML, always note as "applicable".
                return true;
            }
            else if (languageKind == RazorLanguageKind.CSharp)
            {
                return s_cSharpTriggerCharacters.Contains(triggerCharacter);
            }
            else if (languageKind == RazorLanguageKind.Html)
            {
                return s_htmlTriggerCharacters.Contains(triggerCharacter);
            }

            // Unknown trigger character.
            return false;
        }

        internal static VSInternalCompletionContext RewriteContext(VSInternalCompletionContext context, RazorLanguageKind languageKind)
        {
            if (context.TriggerKind != CompletionTriggerKind.TriggerCharacter)
            {
                // Non-triggered based completion, the existing context is valid.
                return context;
            }

            if (languageKind == RazorLanguageKind.CSharp && s_cSharpTriggerCharacters.Contains(context.TriggerCharacter))
            {
                // C# trigger character for C# content
                return context;
            }

            if (languageKind == RazorLanguageKind.Html && s_htmlTriggerCharacters.Contains(context.TriggerCharacter))
            {
                // HTML trigger character for HTML content
                return context;
            }

            // Trigger character not associated with the current langauge. Transform the context into an invoked context.
            var rewrittenContext = new VSInternalCompletionContext()
            {
                TriggerKind = CompletionTriggerKind.Invoked,
            };

            var invokeKind = (context as VSInternalCompletionContext)?.InvokeKind;
            if (invokeKind.HasValue)
            {
                rewrittenContext.InvokeKind = invokeKind.Value;
            }

            if (languageKind == RazorLanguageKind.CSharp && s_razorTriggerCharacters.Contains(context.TriggerCharacter))
            {
                // The C# language server will not return any completions for the '@' character unless we
                // send the completion request explicitly.
                rewrittenContext.InvokeKind = VSInternalCompletionInvokeKind.Explicit;
            }

            return rewrittenContext;
        }

        // The TextEdit positions returned to us from the C#/HTML language servers are positions correlating to the virtual document.
        // We need to translate these positions to apply to the Razor document instead. Performance is a big concern here, so we want to
        // make the logic as simple as possible, i.e. no asynchronous calls.
        // The current logic takes the approach of assuming the original request's position (Razor doc) correlates directly to the positions
        // returned by the C#/HTML language servers. We use this assumption (+ math) to map from the virtual (projected) doc positions ->
        // Razor doc positions.
        internal static VSInternalCompletionList TranslateTextEdits(
            Position hostDocumentPosition,
            Position projectedPosition,
            VSInternalCompletionList completionList)
        {
            var newItems = completionList.Items.Select(item => TranslateTextEdits(hostDocumentPosition, projectedPosition, item)).ToArray();
            completionList.Items = newItems;

            return completionList;

            static CompletionItem TranslateTextEdits(Position hostDocumentPosition, Position projectedPosition, CompletionItem item)
            {
                if (item.TextEdit != null)
                {
                    var translatedRange = TranslateRange(hostDocumentPosition, projectedPosition, item.TextEdit.Range);
                    item.TextEdit = new TextEdit
                    {
                        NewText = item.TextEdit.NewText,
                        Range = translatedRange,
                    };
                }
                else if (item.AdditionalTextEdits?.Any() == true)
                {
                    // Additional text edits should typically only be provided at resolve time. We don't support them in the normal completion flow.
                    item.AdditionalTextEdits = null;
                }

                return item;
            }
        }

        internal static Range TranslateRange(Position hostDocumentPosition, Position projectedPosition, Range textEditRange)
        {
            var offset = projectedPosition.Character - hostDocumentPosition.Character;

            var editStartPosition = textEditRange.Start;
            var translatedStartPosition = TranslatePosition(offset, hostDocumentPosition, editStartPosition);
            var editEndPosition = textEditRange.End;
            var translatedEndPosition = TranslatePosition(offset, hostDocumentPosition, editEndPosition);
            var translatedRange = new Range()
            {
                Start = translatedStartPosition,
                End = translatedEndPosition,
            };

            return translatedRange;

            static Position TranslatePosition(int offset, Position hostDocumentPosition, Position editPosition)
            {
                var translatedCharacter = editPosition.Character - offset;

                // Note: If this completion handler ever expands to deal with multi-line TextEdits, this logic will likely need to change since
                // it assumes we're only dealing with single-line TextEdits.
                var translatedPosition = new Position(hostDocumentPosition.Line, translatedCharacter);
                return translatedPosition;
            }
        }

        private static IReadOnlyCollection<CompletionItem> GenerateCompletionItems(IReadOnlyCollection<string> completionItems)
            => completionItems.Select(item => new CompletionItem { Label = item }).ToArray();

        private class CompletionItemComparer : IEqualityComparer<CompletionItem>
        {
            public static CompletionItemComparer Instance = new();

            public bool Equals(CompletionItem x, CompletionItem y)
            {
                if (x is null && y is null)
                {
                    return true;
                }
                else if (x is null || y is null)
                {
                    return false;
                }

                return x.Label.Equals(y.Label, StringComparison.Ordinal);
            }

            public int GetHashCode(CompletionItem obj) => obj?.Label?.GetHashCode() ?? 0;
        }

        private record class CompletionProjection(RazorLanguageKind LanguageKind, Position Position);

        private record class ProvisionalCompletionInfo(
            TextEdit ProvisionalTextEdit,
            CompletionProjection ProvisionalProjection);

        private ProvisionalCompletionInfo? TryGetProvisionalCompletionInfo(
            CompletionParams request,
            CompletionProjection projection,
            RazorCodeDocument codeDocument,
            SourceText sourceText)
        {
            if (projection.LanguageKind != RazorLanguageKind.Html ||
                request.Context is null ||
                request.Context.TriggerKind != CompletionTriggerKind.TriggerCharacter ||
                request.Context.TriggerCharacter != ".")
            {
                _logger.LogInformation("Invalid provisional completion context.");
                return null;
            }

            if (projection.Position.Character == 0)
            {
                // We're at the start of line. Can't have provisional completions here.
                _logger.LogInformation("Start of line, invalid completion location.");
                return null;
            }

            var previousCharacterPosition = new Position(projection.Position.Line, projection.Position.Character - 1);
            var previousCharacterProjection = GetProjection(previousCharacterPosition, codeDocument, sourceText);
            if (previousCharacterProjection.LanguageKind != RazorLanguageKind.CSharp)
            {
                _logger.LogInformation($"Failed to find previous char projection in {previousCharacterProjection.LanguageKind:G}");
                return null;
            }

            // Edit the CSharp projected document to contain a '.'. This allows C# completion to provide valid
            // completion items for moments when a user has typed a '.' that's typically interpreted as Html.
            var addProvisionalDot = new TextEdit()
            {
                Range = new Range()
                {
                    Start = previousCharacterProjection.Position,
                    End = previousCharacterProjection.Position,
                },
                NewText = ".",
            };
            var provisionalProjection = new CompletionProjection(
                RazorLanguageKind.CSharp,
                new Position(
                    previousCharacterProjection.Position.Line,
                    previousCharacterProjection.Position.Character + 1));
            return new ProvisionalCompletionInfo(addProvisionalDot, provisionalProjection);
        }
    }
}
