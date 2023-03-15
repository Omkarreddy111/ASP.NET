// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Diagnostics.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Internal;
using Microsoft.Extensions.Metrics;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Fakes;

namespace Microsoft.AspNetCore.Hosting;

public class HostingEventSourceTests
{
    [Fact]
    public void MatchesNameAndGuid()
    {
        // Arrange & Act
        var eventSource = new HostingEventSource();

        // Assert
        Assert.Equal("Microsoft.AspNetCore.Hosting", eventSource.Name);
        Assert.Equal(Guid.Parse("9ded64a4-414c-5251-dcf7-1e4e20c15e70", CultureInfo.InvariantCulture), eventSource.Guid);
    }

    [Fact]
    public void HostStart()
    {
        // Arrange
        var expectedEventId = 1;
        var eventListener = new TestEventListener(expectedEventId);
        var hostingEventSource = GetHostingEventSource();
        eventListener.EnableEvents(hostingEventSource, EventLevel.Informational);

        // Act
        hostingEventSource.HostStart();

        // Assert
        var eventData = eventListener.EventData;
        Assert.NotNull(eventData);
        Assert.Equal(expectedEventId, eventData.EventId);
        Assert.Equal("HostStart", eventData.EventName);
        Assert.Equal(EventLevel.Informational, eventData.Level);
        Assert.Same(hostingEventSource, eventData.EventSource);
        Assert.Null(eventData.Message);
        Assert.Empty(eventData.Payload);
    }

    [Fact]
    public void HostStop()
    {
        // Arrange
        var expectedEventId = 2;
        var eventListener = new TestEventListener(expectedEventId);
        var hostingEventSource = GetHostingEventSource();
        eventListener.EnableEvents(hostingEventSource, EventLevel.Informational);

        // Act
        hostingEventSource.HostStop();

        // Assert
        var eventData = eventListener.EventData;
        Assert.NotNull(eventData);
        Assert.Equal(expectedEventId, eventData.EventId);
        Assert.Equal("HostStop", eventData.EventName);
        Assert.Equal(EventLevel.Informational, eventData.Level);
        Assert.Same(hostingEventSource, eventData.EventSource);
        Assert.Null(eventData.Message);
        Assert.Empty(eventData.Payload);
    }

    public static TheoryData<DefaultHttpContext, string[]> RequestStartData
    {
        get
        {
            var variations = new TheoryData<DefaultHttpContext, string[]>();

            var context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = "/Home/Index";
            variations.Add(
                context,
                new string[]
                {
                    "GET",
                    "/Home/Index"
                });

            context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Path = "/";
            variations.Add(
                context,
                new string[]
                {
                    "POST",
                    "/"
                });

            return variations;
        }
    }

    [Theory]
    [MemberData(nameof(RequestStartData))]
    public void RequestStart(DefaultHttpContext httpContext, string[] expected)
    {
        // Arrange
        var expectedEventId = 3;
        var eventListener = new TestEventListener(expectedEventId);
        var hostingEventSource = GetHostingEventSource();
        eventListener.EnableEvents(hostingEventSource, EventLevel.Informational);

        // Act
        hostingEventSource.RequestStart(httpContext.Request.Method, httpContext.Request.Path);

        // Assert
        var eventData = eventListener.EventData;
        Assert.NotNull(eventData);
        Assert.Equal(expectedEventId, eventData.EventId);
        Assert.Equal("RequestStart", eventData.EventName);
        Assert.Equal(EventLevel.Informational, eventData.Level);
        Assert.Same(hostingEventSource, eventData.EventSource);
        Assert.Null(eventData.Message);

        var payloadList = eventData.Payload;
        Assert.Equal(expected.Length, payloadList.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], payloadList[i]);
        }
    }

    [Fact]
    public void RequestStop()
    {
        // Arrange
        var expectedEventId = 4;
        var eventListener = new TestEventListener(expectedEventId);
        var hostingEventSource = GetHostingEventSource();
        eventListener.EnableEvents(hostingEventSource, EventLevel.Informational);

        // Act
        hostingEventSource.RequestStop();

        // Assert
        var eventData = eventListener.EventData;
        Assert.Equal(expectedEventId, eventData.EventId);
        Assert.Equal("RequestStop", eventData.EventName);
        Assert.Equal(EventLevel.Informational, eventData.Level);
        Assert.Same(hostingEventSource, eventData.EventSource);
        Assert.Null(eventData.Message);
        Assert.Empty(eventData.Payload);
    }

    [Fact]
    public void UnhandledException()
    {
        // Arrange
        var expectedEventId = 5;
        var eventListener = new TestEventListener(expectedEventId);
        var hostingEventSource = GetHostingEventSource();
        eventListener.EnableEvents(hostingEventSource, EventLevel.Informational);

        // Act
        hostingEventSource.UnhandledException();

        // Assert
        var eventData = eventListener.EventData;
        Assert.Equal(expectedEventId, eventData.EventId);
        Assert.Equal("UnhandledException", eventData.EventName);
        Assert.Equal(EventLevel.Error, eventData.Level);
        Assert.Same(hostingEventSource, eventData.EventSource);
        Assert.Null(eventData.Message);
        Assert.Empty(eventData.Payload);
    }

    [Fact]
    public async Task VerifyEventSourceCountersFireWithCorrectValues()
    {
        // Arrange
        var eventListener = new TestCounterListener(new[]
        {
            "requests-per-second",
            "total-requests",
            "current-requests",
            "failed-requests"
        });

        // Simulate metrics from two hosts
        var meterFactory1 = new TestMeterFactory();
        var hostingMetrics1 = new HostingMetrics(meterFactory1);

        var meterFactory2 = new TestMeterFactory();
        var hostingMetrics2 = new HostingMetrics(meterFactory2);

        var hostingEventSource = GetHostingEventSource(meterFactory1.Meters.Concat(meterFactory2.Meters).ToArray());

        var timeout = !Debugger.IsAttached ? TimeSpan.FromSeconds(30) : Timeout.InfiniteTimeSpan;
        using CancellationTokenSource timeoutTokenSource = new CancellationTokenSource(timeout);

        var rpsValues = eventListener.GetCounterValues("requests-per-second", timeoutTokenSource.Token).GetAsyncEnumerator();
        var totalRequestValues = eventListener.GetCounterValues("total-requests", timeoutTokenSource.Token).GetAsyncEnumerator();
        var currentRequestValues = eventListener.GetCounterValues("current-requests", timeoutTokenSource.Token).GetAsyncEnumerator();
        var failedRequestValues = eventListener.GetCounterValues("failed-requests", timeoutTokenSource.Token).GetAsyncEnumerator();

        eventListener.EnableEvents(hostingEventSource, EventLevel.Informational, EventKeywords.None,
            new Dictionary<string, string>
            {
                { "EventCounterIntervalSec", "1" }
            });

        // Act & Assert
        hostingMetrics1.RequestStart();
        hostingMetrics2.RequestStart();

        Assert.Equal(2, await totalRequestValues.FirstOrDefault(v => v == 2));
        Assert.Equal(2, await rpsValues.FirstOrDefault(v => v == 2));
        Assert.Equal(2, await currentRequestValues.FirstOrDefault(v => v == 2));
        Assert.Equal(0, await failedRequestValues.FirstOrDefault(v => v == 0));

        hostingMetrics1.RequestStop();
        hostingMetrics2.RequestStop();

        Assert.Equal(2, await totalRequestValues.FirstOrDefault(v => v == 2));
        Assert.Equal(0, await rpsValues.FirstOrDefault(v => v == 0));
        Assert.Equal(0, await currentRequestValues.FirstOrDefault(v => v == 0));
        Assert.Equal(0, await failedRequestValues.FirstOrDefault(v => v == 0));

        hostingMetrics1.RequestStart();
        hostingMetrics2.RequestStart();

        Assert.Equal(4, await totalRequestValues.FirstOrDefault(v => v == 4));
        Assert.Equal(2, await rpsValues.FirstOrDefault(v => v == 2));
        Assert.Equal(2, await currentRequestValues.FirstOrDefault(v => v == 2));
        Assert.Equal(0, await failedRequestValues.FirstOrDefault(v => v == 0));

        hostingMetrics1.RequestFailed(StatusCodes.Status500InternalServerError);
        hostingMetrics2.RequestFailed(StatusCodes.Status500InternalServerError);
        hostingMetrics1.RequestStop();
        hostingMetrics2.RequestStop();

        Assert.Equal(4, await totalRequestValues.FirstOrDefault(v => v == 4));
        Assert.Equal(0, await rpsValues.FirstOrDefault(v => v == 0));
        Assert.Equal(0, await currentRequestValues.FirstOrDefault(v => v == 0));
        Assert.Equal(2, await failedRequestValues.FirstOrDefault(v => v == 2));
    }

    private static HostingEventSource GetHostingEventSource(Meter[] meters = null)
    {
        return new HostingEventSource(Guid.NewGuid().ToString(), meters);
    }
}
