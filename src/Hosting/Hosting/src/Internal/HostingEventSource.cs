// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Hosting;

internal sealed class HostingEventSource : EventSource
{
    public static readonly HostingEventSource Log = new HostingEventSource();

    // Used for testing
    private readonly Meter? _meter;

    private MeterListener? _listener;
    private IncrementingPollingCounter? _requestsPerSecondCounter;
    private PollingCounter? _totalRequestsCounter;
    private PollingCounter? _failedRequestsCounter;
    private PollingCounter? _currentRequestsCounter;

    private long _totalRequests;
    private long _currentRequests;
    private long _failedRequests;

    internal HostingEventSource()
        : base("Microsoft.AspNetCore.Hosting", EventSourceSettings.EtwManifestEventFormat)
    {
    }

    // Used for testing
    internal HostingEventSource(string eventSourceName, Meter? meter)
        : base(eventSourceName, EventSourceSettings.EtwManifestEventFormat)
    {
        _meter = meter;
    }

    // NOTE
    // - The 'Start' and 'Stop' suffixes on the following event names have special meaning in EventSource. They
    //   enable creating 'activities'.
    //   For more information, take a look at the following blog post:
    //   https://blogs.msdn.microsoft.com/vancem/2015/09/14/exploring-eventsource-activity-correlation-and-causation-features/
    // - A stop event's event id must be next one after its start event.

    [Event(1, Level = EventLevel.Informational)]
    public void HostStart()
    {
        WriteEvent(1);
    }

    [Event(2, Level = EventLevel.Informational)]
    public void HostStop()
    {
        WriteEvent(2);
    }

    [Event(3, Level = EventLevel.Informational)]
    public void RequestStart(string method, string path)
    {
        WriteEvent(3, method, path);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [Event(4, Level = EventLevel.Informational)]
    public void RequestStop()
    {
        WriteEvent(4);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [Event(5, Level = EventLevel.Error)]
    public void UnhandledException()
    {
        WriteEvent(5);
    }

    [Event(6, Level = EventLevel.Informational)]
    public void ServerReady()
    {
        WriteEvent(6);
    }

    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        if (command.Command == EventCommand.Enable)
        {
            // This is the convention for initializing counters in the RuntimeEventSource (lazily on the first enable command).
            // They aren't disabled afterwards...

            if (_listener == null)
            {
                StartListener();
            }

            _requestsPerSecondCounter ??= new IncrementingPollingCounter("requests-per-second", this, () => Volatile.Read(ref _totalRequests))
            {
                DisplayName = "Request Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _totalRequestsCounter ??= new PollingCounter("total-requests", this, () => Volatile.Read(ref _totalRequests))
            {
                DisplayName = "Total Requests",
            };

            _currentRequestsCounter ??= new PollingCounter("current-requests", this, () => Volatile.Read(ref _currentRequests))
            {
                DisplayName = "Current Requests"
            };

            _failedRequestsCounter ??= new PollingCounter("failed-requests", this, () => Volatile.Read(ref _failedRequests))
            {
                DisplayName = "Failed Requests"
            };
        }
    }

    [NonEvent]
    private void InstrumentPublished(Instrument instrument, MeterListener meterListener)
    {
        if (_meter != null && instrument.Meter != _meter)
        {
            return;
        }
        if (instrument.Meter.Name != "Microsoft.AspNetCore.Hosting")
        {
            return;
        }
        switch (instrument.Name)
        {
            case "total-requests":
            case "current-requests":
            case "failed-requests":
                meterListener.EnableMeasurementEvents(instrument, this);
                break;
        }
    }

    [NonEvent]
    private void StartListener()
    {
        _listener = new MeterListener();
        // InstrumentPublished must be a method annotated with [NonEvent] to prevent event source from breaking.
        _listener.InstrumentPublished = InstrumentPublished;
        _listener.SetMeasurementEventCallback<long>(static (instrument, measurement, tags, state) =>
        {
            var eventSource = (HostingEventSource)state!;
            switch (instrument.Name)
            {
                case "total-requests":
                    eventSource._totalRequests += measurement;
                    break;
                case "current-requests":
                    eventSource._currentRequests += measurement;
                    break;
                case "failed-requests":
                    eventSource._failedRequests += measurement;
                    break;
            }
        });
        _listener.Start();
    }
}
