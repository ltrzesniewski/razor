﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.ServiceHub.Framework;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal sealed class RemoteTagHelperProviderServiceFactory : RazorServiceFactoryBase<IRemoteTagHelperProviderService>
    {
        // WARNING: We must always have a parameterless constructor in order to be properly handled by ServiceHub.
        public RemoteTagHelperProviderServiceFactory() : base(RazorServiceDescriptors.TagHelperProviderServiceDescriptors)
        {
        }

        protected override IRemoteTagHelperProviderService CreateService(IServiceBroker serviceBroker)
                => new RemoteTagHelperProviderService(serviceBroker);
    }
}
