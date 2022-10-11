// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    [Export(typeof(FallbackCapabilitiesFilterResolver))]
    internal class DefaultFallbackCapabilitiesFilterResolver : FallbackCapabilitiesFilterResolver
    {
        public override Func<JToken, TIn, bool> Resolve<TIn>(string lspRequestMethodName)
        {
            if (lspRequestMethodName is null)
            {
                throw new ArgumentNullException(nameof(lspRequestMethodName));
            }

            switch (lspRequestMethodName)
            {
                // Standard LSP capabilities
                case Methods.TextDocumentImplementationName:
                    return CheckImplementationCapabilities;
                case Methods.TextDocumentTypeDefinitionName:
                    return CheckTypeDefinitionCapabilities;
                case Methods.TextDocumentReferencesName:
                    return CheckFindAllReferencesCapabilities;
                case Methods.TextDocumentRenameName:
                    return CheckRenameCapabilities;
                case Methods.TextDocumentSignatureHelpName:
                    return CheckSignatureHelpCapabilities;
                case Methods.TextDocumentWillSaveName:
                    return CheckWillSaveCapabilities;
                case Methods.TextDocumentWillSaveWaitUntilName:
                    return CheckWillSaveWaitUntilCapabilities;
                case Methods.TextDocumentRangeFormattingName:
                    return CheckRangeFormattingCapabilities;
                case Methods.WorkspaceSymbolName:
                    return CheckWorkspaceSymbolCapabilities;
                case Methods.TextDocumentOnTypeFormattingName:
                    return CheckOnTypeFormattingCapabilities;
                case Methods.TextDocumentFormattingName:
                    return CheckFormattingCapabilities;
                case Methods.TextDocumentHoverName:
                    return CheckHoverCapabilities;
                case Methods.TextDocumentCodeActionName:
                    return CheckCodeActionCapabilities;
                case Methods.TextDocumentCodeLensName:
                    return CheckCodeLensCapabilities;
                case Methods.TextDocumentCompletionName:
                    return CheckCompletionCapabilities;
                case Methods.TextDocumentCompletionResolveName:
                    return CheckCompletionResolveCapabilities;
                case Methods.TextDocumentDefinitionName:
                    return CheckDefinitionCapabilities;
                case Methods.TextDocumentDocumentHighlightName:
                    return CheckHighlightCapabilities;
                case "textDocument/semanticTokens":
                case Methods.TextDocumentSemanticTokensFullName:
                case Methods.TextDocumentSemanticTokensFullDeltaName:
                case Methods.TextDocumentSemanticTokensRangeName:
                    return CheckSemanticTokensCapabilities;
                case Methods.TextDocumentLinkedEditingRangeName:
                    return CheckLinkedEditingRangeCapabilities;
                case Methods.CodeActionResolveName:
                    return CheckCodeActionResolveCapabilities;
                case Methods.TextDocumentDocumentColorName:
                    return CheckDocumentColorCapabilities;

                // VS LSP Expansion capabilities
                case VSMethods.GetProjectContextsName:
                    return CheckProjectContextsCapabilities;
                case VSInternalMethods.DocumentReferencesName:
                    return CheckMSReferencesCapabilities;
                case VSInternalMethods.OnAutoInsertName:
                    return CheckOnAutoInsertCapabilities;
                case VSInternalMethods.DocumentPullDiagnosticName:
                case VSInternalMethods.WorkspacePullDiagnosticName:
                    return CheckPullDiagnosticCapabilities;
                case VSInternalMethods.TextDocumentTextPresentationName:
                    return CheckTextPresentationCapabilities;
                case VSInternalMethods.TextDocumentUriPresentationName:
                    return CheckUriPresentationCapabilities;

                default:
                    return FallbackCheckCapabilties;
            }
        }

        private static bool CheckDocumentColorCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.DocumentColorProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckSemanticTokensCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<VSServerCapabilities>();

            return serverCapabilities?.SemanticTokensOptions != null;
        }

        private static bool CheckImplementationCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.ImplementationProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckTypeDefinitionCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.TypeDefinitionProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckFindAllReferencesCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.ReferencesProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckRenameCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.RenameProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckSignatureHelpCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.SignatureHelpProvider != null;
        }

        private static bool CheckWillSaveCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.TextDocumentSync?.WillSave == true;
        }

        private static bool CheckWillSaveWaitUntilCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.TextDocumentSync?.WillSaveWaitUntil == true;
        }

        private static bool CheckRangeFormattingCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.DocumentRangeFormattingProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckWorkspaceSymbolCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.WorkspaceSymbolProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckOnTypeFormattingCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            if (serverCapabilities?.DocumentOnTypeFormattingProvider is DocumentOnTypeFormattingOptions formattingOptions
                && parameters is DocumentOnTypeFormattingParams formattingParams)
            {
                if (formattingOptions.FirstTriggerCharacter == formattingParams.Character
                    || formattingOptions.MoreTriggerCharacter.Contains(formattingParams.Character))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckFormattingCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.DocumentFormattingProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckHoverCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.HoverProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckCodeActionCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.CodeActionProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckCodeLensCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.CodeLensProvider != null;
        }

        private static bool CheckCompletionCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.CompletionProvider != null;
        }

        private static bool CheckCompletionResolveCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.CompletionProvider?.ResolveProvider == true;
        }

        private static bool CheckDefinitionCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.DefinitionProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckHighlightCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.DocumentHighlightProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;
        }

        private static bool CheckMSReferencesCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<VSInternalServerCapabilities>();

            return serverCapabilities?.MSReferencesProvider == true;
        }

        private static bool CheckProjectContextsCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<VSInternalServerCapabilities>();

            return serverCapabilities?.ProjectContextProvider == true;
        }

        private static bool CheckCodeActionResolveCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            var resolvesCodeActions = serverCapabilities?.CodeActionProvider?.Match(
                boolValue => false,
                options => options.ResolveProvider) ?? false;

            return resolvesCodeActions;
        }

        private static bool CheckOnAutoInsertCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<VSInternalServerCapabilities>();

            return serverCapabilities?.OnAutoInsertProvider != null;
        }

        private static bool CheckTextPresentationCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<VSInternalServerCapabilities>();

            return serverCapabilities?.TextPresentationProvider == true;
        }
        private static bool CheckUriPresentationCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<VSInternalServerCapabilities>();

            return serverCapabilities?.UriPresentationProvider == true;
        }

        private static bool CheckLinkedEditingRangeCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            return serverCapabilities?.LinkedEditingRangeProvider?.Match(
              boolValue => boolValue,
              options => options != null) ?? false;
        }

        private static bool CheckPullDiagnosticCapabilities<TIn>(JToken token, TIn parameters)
        {
            var serverCapabilities = token.ToObject<VSInternalServerCapabilities>();

            return serverCapabilities?.SupportsDiagnosticRequests == true;
        }

        private static bool FallbackCheckCapabilties<TIn>(JToken token, TIn parameters)
        {
            // Fallback is to assume present

            return true;
        }
    }
}
