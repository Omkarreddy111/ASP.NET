// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Metrics;

namespace Microsoft.AspNetCore.Hosting;

internal sealed class HostingMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _totalRequestsCounter;
    private readonly UpDownCounter<long> _currentRequestsCounter;
    private readonly Counter<long> _failedRequestsCounter;

    public HostingMetrics(IMeterFactory metricsFactory)
    {
        _meter = metricsFactory.CreateMeter("Microsoft.AspNetCore.Hosting");

        _totalRequestsCounter = _meter.CreateCounter<long>(
            "total-requests",
            description: "Total Requests");

        _currentRequestsCounter = _meter.CreateUpDownCounter<long>(
            "current-requests",
            description: "Current Requests");

        _failedRequestsCounter = _meter.CreateCounter<long>(
            "failed-requests",
            description: "Failed Requests");
    }

    public void RequestStart()
    {
        _totalRequestsCounter.Add(1);
        _currentRequestsCounter.Add(1);
    }

    public void RequestStop()
    {
        _currentRequestsCounter.Add(-1);
    }

    public void RequestFailed()
    {
        _failedRequestsCounter.Add(1);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    public bool IsEnabled() => _totalRequestsCounter.Enabled || _currentRequestsCounter.Enabled || _failedRequestsCounter.Enabled;
}
