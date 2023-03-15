// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Metrics;

namespace Microsoft.AspNetCore.Hosting;

internal sealed class HostingMetrics : IDisposable
{
    // TODO: Temporary name to test differences between metrics and event source counters in tooling.
    public const string MeterName = "Microsoft.AspNetCore.Hosting.Temp";

    private static readonly ConcurrentDictionary<int, object> _statusCodeCache = new ConcurrentDictionary<int, object>();

    private readonly Meter _meter;
    private readonly Counter<long> _totalRequestsCounter;
    private readonly UpDownCounter<long> _currentRequestsCounter;
    private readonly Counter<long> _failedRequestsCounter;
    private readonly Histogram<double> _requestDuration;

    public HostingMetrics(IMeterFactory metricsFactory)
    {
        _meter = metricsFactory.CreateMeter(MeterName);

        _totalRequestsCounter = _meter.CreateCounter<long>(
            "total-requests",
            description: "Total Requests");

        _currentRequestsCounter = _meter.CreateUpDownCounter<long>(
            "current-requests",
            description: "Current Requests");

        _failedRequestsCounter = _meter.CreateCounter<long>(
            "failed-requests",
            description: "Failed Requests");

        _requestDuration = _meter.CreateHistogram<double>(
            "request-duration",
            description: "Request duration");
    }

    public void RequestStart()
    {
        _totalRequestsCounter.Add(1);
        _currentRequestsCounter.Add(1);
    }

    public void RequestEnd(string protocol, string scheme, string method, HostString host, string? routePattern, int statusCode, long startTimestamp, long currentTimestamp)
    {
        var duration = new TimeSpan((long)(HostingApplicationDiagnostics.TimestampToTicks * (currentTimestamp - startTimestamp)));

        var tags = new TagList();
        tags.Add("protocol-version", ResolveProtocol(protocol));
        tags.Add("scheme", scheme);
        tags.Add("method", method);
        tags.Add("status-code", GetBoxedStatusCode(statusCode));
        if (host.HasValue)
        {
            tags.Add("host", host.Host);
            if (host.Port is not null && host.Port != 80 && host.Port != 443)
            {
                tags.Add("port", host.Port);
            }
        }
        if (routePattern != null)
        {
            tags.Add("route", routePattern);
        }

        _requestDuration.Record(duration.TotalMilliseconds, tags);

        static string ResolveProtocol(string protocol)
        {
            return protocol switch
            {
                "HTTP/0.9" => "0.9",
                "HTTP/1.0" => "1.0",
                "HTTP/1.1" => "1.1",
                "HTTP/2" => "2.0",
                "HTTP/3" => "3.0",
                _ => protocol,
            };
        }
    }

    public void RequestStop()
    {
        _currentRequestsCounter.Add(-1);
    }

    public void RequestFailed(int statusCode)
    {
        _failedRequestsCounter.Add(1, new KeyValuePair<string, object?>("status-code", GetBoxedStatusCode(statusCode)));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    public bool IsEnabled() => _totalRequestsCounter.Enabled || _currentRequestsCounter.Enabled || _failedRequestsCounter.Enabled;

    // Tag accepts object value. Maintain a cache of boxed status codes to avoid allocating for every request.
    private static object GetBoxedStatusCode(int statusCode)
    {
        object? boxedStatusCode;

        // Status code should always be inside this range. Limit cache size with unexpected values.
        if (statusCode >= 100 && statusCode <= 599)
        {
            if (!_statusCodeCache.TryGetValue(statusCode, out boxedStatusCode))
            {
                _statusCodeCache[statusCode] = boxedStatusCode = statusCode;
            }
        }
        else
        {
            boxedStatusCode = statusCode;
        }

        return boxedStatusCode;
    }
}
