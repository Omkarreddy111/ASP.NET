// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Metrics;

namespace Microsoft.AspNetCore.Hosting.Fakes;

public class TestMeterFactory : IMeterFactory
{
    public List<Meter> Meters { get; } = new List<Meter>();

    public Meter CreateMeter(string name)
    {
        var meter = new Meter(name);
        Meters.Add(meter);
        return meter;
    }

    public Meter CreateMeter(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version);
        Meters.Add(meter);
        return meter;
    }
}
