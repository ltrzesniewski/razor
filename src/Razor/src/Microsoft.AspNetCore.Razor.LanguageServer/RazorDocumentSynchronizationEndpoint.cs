// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorDocumentSynchronizationEndpoint : ITextDocumentSyncHandler
    {
        private readonly ILogger _logger;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorProjectService _projectService;

        public static int? LastDocumentVersion = null;
        public static int? CurrentRunningVersion = null;
        public static int? LastDocumentPreUpdateLength = null;
        public static int? LastDocumentPostUpdateLength = null;
        public static int? LastRunOnIteration = null;

        public RazorDocumentSynchronizationEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            DocumentResolver documentResolver!!,
            RazorProjectService projectService!!,
            ILoggerFactory loggerFactory!!)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _projectService = projectService;
            _logger = loggerFactory.CreateLogger<RazorDocumentSynchronizationEndpoint>();
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Incremental;

        public async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            if (CurrentRunningVersion != null)
            {
                Debugger.Launch();
            }

            LastRunOnIteration = JsonRpcRequestScheduler.IterationCount;
            CurrentRunningVersion = notification.TextDocument.Version!.Value;
            LastDocumentVersion = CurrentRunningVersion.Value;

            var uri = notification.TextDocument.Uri.GetAbsoluteOrUNCPath();
            var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(uri, out var documentSnapshot);

                return documentSnapshot;
            }, CancellationToken.None).ConfigureAwait(false);

            if (document is null)
            {
                throw new InvalidOperationException(RazorLS.Resources.FormatDocument_Not_Found(uri));
            }

            var sourceText = await document.GetTextAsync();
            sourceText = ApplyContentChanges(notification.ContentChanges, sourceText);

            LastDocumentPostUpdateLength = null;
            LastDocumentPreUpdateLength = sourceText.Lines.Count;

            if (notification.TextDocument.Version is null)
            {
                throw new InvalidOperationException(RazorLS.Resources.Version_Should_Not_Be_Null);
            }

            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectService.UpdateDocument(document.FilePath, sourceText, notification.TextDocument.Version.Value),
                CancellationToken.None).ConfigureAwait(false);

            LastDocumentPostUpdateLength = sourceText.Lines.Count;

            CurrentRunningVersion = null;

            return Unit.Value;
        }

        public async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            var sourceText = SourceText.From(notification.TextDocument.Text);

            if (notification.TextDocument.Version is null)
            {
                throw new InvalidOperationException(RazorLS.Resources.Version_Should_Not_Be_Null);
            }

            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectService.OpenDocument(notification.TextDocument.Uri.GetAbsoluteOrUNCPath(), sourceText, notification.TextDocument.Version.Value),
                CancellationToken.None).ConfigureAwait(false);

            return Unit.Value;
        }

        public async Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectService.CloseDocument(notification.TextDocument.Uri.GetAbsoluteOrUNCPath()),
                CancellationToken.None).ConfigureAwait(false);

            return Unit.Value;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token)
        {
            _logger.LogInformation($"Saved Document {notification.TextDocument.Uri.GetAbsoluteOrUNCPath()}");

            return Unit.Task;
        }

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector,
                SyncKind = Change
            };
        }

        TextDocumentOpenRegistrationOptions IRegistration<TextDocumentOpenRegistrationOptions, SynchronizationCapability>.GetRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentOpenRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector
            };
        }

        TextDocumentCloseRegistrationOptions IRegistration<TextDocumentCloseRegistrationOptions, SynchronizationCapability>.GetRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentCloseRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector
            };
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions, SynchronizationCapability>.GetRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector,
                IncludeText = true
            };
        }

        // Internal for testing
        internal SourceText ApplyContentChanges(IEnumerable<TextDocumentContentChangeEvent> contentChanges, SourceText sourceText)
        {
            foreach (var change in contentChanges)
            {
                if (change.Range is null)
                {
                    throw new ArgumentNullException(nameof(change.Range), "Range of change should not be null.");
                }

                var linePosition = new LinePosition(change.Range.Start.Line, change.Range.Start.Character);
                var position = sourceText.Lines.GetPosition(linePosition);
                var textSpan = new TextSpan(position, change.RangeLength);
                var textChange = new TextChange(textSpan, change.Text);

                _logger.LogTrace("Applying " + textChange);

                // If there happens to be multiple text changes we generate a new source text for each one. Due to the
                // differences in VSCode and Roslyn's representation we can't pass in all changes simultaneously because
                // ordering may differ.
                sourceText = sourceText.WithChanges(textChange);
            }

            return sourceText;
        }

        public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            return new TextDocumentAttributes(uri, "razor");
        }
    }
}
