﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Razor.Editor;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(IRazorDocumentOptionsService))]
    internal sealed class RazorDocumentOptionsService : IRazorDocumentOptionsService
    {
        private readonly RazorLSPClientOptionsMonitor _optionsMonitor;

        [ImportingConstructor]
        public RazorDocumentOptionsService(RazorLSPClientOptionsMonitor optionsMonitor)
        {
            if (optionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            _optionsMonitor = optionsMonitor;
        }

        public Task<IRazorDocumentOptions> GetOptionsForDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            // TO-DO: We should switch to a per-document implementation once Razor starts supporting .editorconfig.
            var editorSettings = _optionsMonitor.EditorSettings;
            return Task.FromResult<IRazorDocumentOptions>(new RazorDocumentOptions(document, editorSettings));
        }

        private sealed class RazorDocumentOptions : IRazorDocumentOptions
        {
            private readonly EditorSettings _editorSettings;
            private readonly OptionKey _useTabsOptionKey;
            private readonly OptionKey _tabSizeOptionKey;
            private readonly OptionKey _indentationSizeOptionKey;

            public RazorDocumentOptions(Document document, EditorSettings editorSettings)
            {
                _editorSettings = editorSettings;

                _useTabsOptionKey = new OptionKey(FormattingOptions.UseTabs, document.Project.Language);
                _tabSizeOptionKey = new OptionKey(FormattingOptions.TabSize, document.Project.Language);
                _indentationSizeOptionKey = new OptionKey(FormattingOptions.IndentationSize, document.Project.Language);
            }

            public bool TryGetDocumentOption(OptionKey option, out object value)
            {
                if (option == _useTabsOptionKey)
                {
                    value = _editorSettings.IndentWithTabs;
                    return true;
                }
                else if (option == _tabSizeOptionKey || option == _indentationSizeOptionKey)
                {
                    value = _editorSettings.IndentSize;
                    return true;
                }
                else
                {
                    value = null;
                    return false;
                }
            }
        }
    }
}