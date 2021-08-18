﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.


using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Test.Common
{
    internal class TestProjectSnapshotManager : DefaultProjectSnapshotManager
    {
        private TestProjectSnapshotManager(ProjectSnapshotManagerDispatcher dispatcher, Workspace workspace, SolutionCloseTracker solutionCloseTracker)
            : base(dispatcher, new DefaultErrorReporter(), Enumerable.Empty<ProjectSnapshotChangeTrigger>(), workspace, solutionCloseTracker)
        {
        }

        public static TestProjectSnapshotManager Create(ProjectSnapshotManagerDispatcher dispatcher)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            var services = TestServices.Create(
                workspaceServices: new[]
                {
                    new DefaultProjectSnapshotProjectEngineFactory(new FallbackProjectEngineFactory(), ProjectEngineFactories.Factories)
                },
                razorLanguageServices: Enumerable.Empty<ILanguageService>());
            var workspace = TestWorkspace.Create(services);
            var solutionCloseTracker = new TestSolutionCloseTracker();
            var testProjectManager = new TestProjectSnapshotManager(dispatcher, workspace, solutionCloseTracker);

            return testProjectManager;
        }

        public bool AllowNotifyListeners { get; set; }

        protected override void NotifyListeners(ProjectChangeEventArgs e)
        {
            if (AllowNotifyListeners)
            {
                base.NotifyListeners(e);
            }
        }
    }
}
