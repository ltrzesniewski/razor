// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text.Adornments;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class RazorCompletionResolveEndpoint : IVSCompletionResolveEndpoint
    {
        private readonly ILogger _logger;
        private readonly LSPTagHelperTooltipFactory _lspTagHelperTooltipFactory;
        private readonly VSLSPTagHelperTooltipFactory _vsLspTagHelperTooltipFactory;
        private readonly CompletionListCache _completionListCache;
        private readonly ClientNotifierServiceBase _languageServer;
        private VSInternalClientCapabilities? _clientCapabilities;
        private MarkupKind _documentationKind;

        // Guid is magically generated and doesn't mean anything. O# magic.
        public Guid Id => new("011c77cc-f90e-4f2e-b32c-dafc6587ccd6");

        public RazorCompletionResolveEndpoint(
            LSPTagHelperTooltipFactory lspTagHelperTooltipFactory,
            VSLSPTagHelperTooltipFactory vsLspTagHelperTooltipFactory,
            CompletionListCache completionListCache,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
        {
            if (lspTagHelperTooltipFactory is null)
            {
                throw new ArgumentNullException(nameof(lspTagHelperTooltipFactory));
            }

            if (vsLspTagHelperTooltipFactory is null)
            {
                throw new ArgumentNullException(nameof(vsLspTagHelperTooltipFactory));
            }

            if (completionListCache is null)
            {
                throw new ArgumentNullException(nameof(completionListCache));
            }

            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _lspTagHelperTooltipFactory = lspTagHelperTooltipFactory;
            _vsLspTagHelperTooltipFactory = vsLspTagHelperTooltipFactory;
            _logger = loggerFactory.CreateLogger<RazorCompletionEndpoint>();
            _completionListCache = completionListCache;
            _languageServer = languageServer;
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            _clientCapabilities = clientCapabilities;

            var completionSupportedKinds = clientCapabilities.TextDocument?.Completion?.CompletionItem?.DocumentationFormat;
            _documentationKind = completionSupportedKinds?.Contains(MarkupKind.Markdown) == true ? MarkupKind.Markdown : MarkupKind.PlainText;

            return null;
        }

        public async Task<VSInternalCompletionItem> Handle(VSCompletionItemBridge completionItemBridge, CancellationToken cancellationToken)
        {
            VSInternalCompletionItem completionItem = completionItemBridge;

            var resolvedCompletionItem = TryResolveRazorCompletionItem(completionItem);
            resolvedCompletionItem ??= await TryResolveDelegatedCompletionItemAsync(completionItem, cancellationToken).ConfigureAwait(false);
            resolvedCompletionItem ??= completionItem;

            return resolvedCompletionItem;
        }

        private VSInternalCompletionItem? TryResolveRazorCompletionItem(VSInternalCompletionItem completionItem)
        {
            if (!completionItem.TryGetCompletionListResultId(out var resultId))
            {
                // Couldn't resolve.
                return null;
            }

            if (!_completionListCache.TryGet(resultId.Value, out var razorCompletionList, out _))
            {
                return null;
            }

            var labelQuery = completionItem.Label;
            var associatedRazorCompletion = razorCompletionList.FirstOrDefault(completion => string.Equals(labelQuery, completion.DisplayText, StringComparison.Ordinal));
            if (associatedRazorCompletion is null)
            {
                //_logger.LogError("Could not find an associated razor completion item. This should never happen since we were able to look up the cached completion list.");
                //Debug.Fail("Could not find an associated razor completion item. This should never happen since we were able to look up the cached completion list.");
                return null;
            }

            // If the client is VS, also fill in the Description property.
            var useDescriptionProperty = _clientCapabilities?.SupportsVisualStudioExtensions ?? false;

            MarkupContent? tagHelperMarkupTooltip = null;
            ClassifiedTextElement? tagHelperClassifiedTextTooltip = null;

            switch (associatedRazorCompletion.Kind)
            {
                case RazorCompletionItemKind.Directive:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetDirectiveCompletionDescription();
                        if (descriptionInfo is not null)
                        {
                            completionItem.Documentation = descriptionInfo.Description;
                        }

                        break;
                    }
                case RazorCompletionItemKind.MarkupTransition:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetMarkupTransitionCompletionDescription();
                        if (descriptionInfo is not null)
                        {
                            completionItem.Documentation = descriptionInfo.Description;
                        }

                        break;
                    }
                case RazorCompletionItemKind.DirectiveAttribute:
                case RazorCompletionItemKind.DirectiveAttributeParameter:
                case RazorCompletionItemKind.TagHelperAttribute:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetAttributeCompletionDescription();
                        if (descriptionInfo == null)
                        {
                            break;
                        }

                        if (useDescriptionProperty)
                        {
                            _vsLspTagHelperTooltipFactory.TryCreateTooltip(descriptionInfo, out tagHelperClassifiedTextTooltip);
                        }
                        else
                        {
                            _lspTagHelperTooltipFactory.TryCreateTooltip(descriptionInfo, _documentationKind, out tagHelperMarkupTooltip);
                        }

                        break;
                    }
                case RazorCompletionItemKind.TagHelperElement:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetTagHelperElementDescriptionInfo();
                        if (descriptionInfo == null)
                        {
                            break;
                        }

                        if (useDescriptionProperty)
                        {
                            _vsLspTagHelperTooltipFactory.TryCreateTooltip(descriptionInfo, out tagHelperClassifiedTextTooltip);
                        }
                        else
                        {
                            _lspTagHelperTooltipFactory.TryCreateTooltip(descriptionInfo, _documentationKind, out tagHelperMarkupTooltip);
                        }

                        break;
                    }
            }

            if (tagHelperMarkupTooltip != null)
            {
                completionItem.Documentation = tagHelperMarkupTooltip;
            }

            if (tagHelperClassifiedTextTooltip != null)
            {
                completionItem.Description = tagHelperClassifiedTextTooltip;
            }

            return completionItem;
        }

        private async Task<VSInternalCompletionItem?> TryResolveDelegatedCompletionItemAsync(VSInternalCompletionItem completionItem, CancellationToken cancellationToken)
        {
            if (!completionItem.TryGetCompletionListResultId(out var resultId))
            {
                // Couldn't resolve.
                return null;
            }

            if (!_completionListCache.TryGet(resultId.Value, out _, out var delegatedCompletionResult))
            {
                return null;
            }

            if (delegatedCompletionResult is null)
            {
                return null;
            }

            var delegatedCompletionList = delegatedCompletionResult.CompletionList;

            if (delegatedCompletionList is null)
            {
                return null;
            }

            var labelQuery = completionItem.Label;
            var associatedDelegatedCompletion = delegatedCompletionList.Items.FirstOrDefault(completion => string.Equals(labelQuery, completion.Label, StringComparison.Ordinal));
            if (associatedDelegatedCompletion is null)
            {
                return null;
            }

            var originalCompletionParams = delegatedCompletionResult.DelegationParams;

            completionItem.Data = associatedDelegatedCompletion.Data ?? delegatedCompletionResult?.CompletionList?.Data;
            var delegatedParams = new DelegatedCompletionItemResolveParams(
                completionItem,
                originalCompletionParams.Kind,
                originalCompletionParams.HostDocument.Uri);
            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorCompletionResolveEndpointName, delegatedParams).ConfigureAwait(false);
            var resolvedCompletionItem = await delegatedRequest.Returning<VSInternalCompletionItem?>(cancellationToken).ConfigureAwait(false);
            return resolvedCompletionItem;
        }
    }
}
