// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;

namespace Microsoft.AspNetCore.Components.Infrastructure;

internal class RazorComponentEndpointDataSourceFactory
{
    private readonly RazorComponentEndpointFactory _factory;

    public RazorComponentEndpointDataSourceFactory(RazorComponentEndpointFactory factory)
    {
        _factory = factory;
    }

    public RazorComponentEndpointDataSource<TRootComponent> CreateDataSource<TRootComponent>()
        where TRootComponent : IRazorComponentApplication<TRootComponent>
    {
        var metadata = TRootComponent.GetPageMetadata();
        return new RazorComponentEndpointDataSource<TRootComponent>(metadata, _factory);
    }
}
