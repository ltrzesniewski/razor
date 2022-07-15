// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class CodeDocumentReferenceHolder : DocumentProcessedListener
    {
        private Dictionary<string, RazorCodeDocument> _codeDocumentCache;
        private ProjectSnapshotManager _projectManager;

        public CodeDocumentReferenceHolder()
        {
            _codeDocumentCache = new(FilePathComparer.Instance);
        }

        public override void DocumentProcessed(RazorCodeDocument codeDocument, DocumentSnapshot documentSnapshot)
        {
            _codeDocumentCache[documentSnapshot.FilePath] = codeDocument;
        }

        public override void Initialize(ProjectSnapshotManager projectManager)
        {
            _projectManager = projectManager;
            _projectManager.Changed += ProjectManager_Changed;
        }

        private void ProjectManager_Changed(object sender, ProjectChangeEventArgs args)
        {
            switch (args.Kind)
            {
                case ProjectChangeKind.ProjectAdded:
                case ProjectChangeKind.ProjectChanged:
                case ProjectChangeKind.ProjectRemoved:
                    var project = _projectManager.GetLoadedProject(args.ProjectFilePath);
                    foreach (var documentFilePath in project.DocumentFilePaths)
                    {
                        _codeDocumentCache.Remove(documentFilePath);
                    }

                    break;
                case ProjectChangeKind.DocumentAdded:
                case ProjectChangeKind.DocumentChanged:
                case ProjectChangeKind.DocumentRemoved:
                    _codeDocumentCache.Remove(args.DocumentFilePath);
                    break;
            }
        }
    }
}
