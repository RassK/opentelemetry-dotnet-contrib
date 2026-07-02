// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Reflection;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class EventSourceTests
{
    private static readonly IReadOnlyDictionary<string, int> ExpectedEventIds =
        new Dictionary<string, int>
        {
            [nameof(OpAmpClientEventSource.TransportCloseException)] = 2,
            [nameof(OpAmpClientEventSource.FrameProcessingException)] = 5,
            [nameof(OpAmpClientEventSource.HeartbeatServiceTickException)] = 502,
            [nameof(OpAmpClientEventSource.HeartbeatServiceTimerUpdateException)] = 503,
            [nameof(OpAmpClientEventSource.SendIdentificationMessageException)] = 1_100,
            [nameof(OpAmpClientEventSource.SendHeartbeatMessageException)] = 1_101,
            [nameof(OpAmpClientEventSource.SendAgentDisconnectMessageException)] = 1_102,
            [nameof(OpAmpClientEventSource.SendEffectiveConfigMessageException)] = 1_103,
            [nameof(OpAmpClientEventSource.SendCustomCapabilitiesMessageException)] = 1_104,
            [nameof(OpAmpClientEventSource.SendCustomMessageMessageException)] = 1_105,
            [nameof(OpAmpClientEventSource.SendRemoteConfigStatusMessageException)] = 1_106,
            [nameof(OpAmpClientEventSource.SendFullStateReportMessageException)] = 1_107,
        };

    [Fact]
    public void EventSourceTests_OpAmpClientEventSource()
        => EventSourceTestHelper.ValidateEventSourceIds<OpAmpClientEventSource>();

    [Fact]
    public void EventSourceTests_OpAmpClientEventSource_ValidateNonEventExceptions()
    {
        // Arrange
        var exception = new InvalidOperationException("Unit test exception");

        var nonEventMethods = typeof(OpAmpClientEventSource)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m =>
                m.GetCustomAttribute<NonEventAttribute>() != null &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(Exception))
            .ToList();

        // Ensure every NonEvent(Exception) has an expected EventId mapping.
        Assert.Equal(
                ExpectedEventIds.Keys.OrderBy(x => x),
                nonEventMethods.Select(m => m.Name).OrderBy(x => x));

        using var listener = new OpAmpTestEventListener(OpAmpClientEventSource.Log);

        foreach (var method in nonEventMethods)
        {
            listener.Clear();

            // Act
            method.Invoke(OpAmpClientEventSource.Log, [exception]);

            // Assert
            var evt = Assert.Single(listener.Events);

            Assert.Equal(
                ExpectedEventIds[method.Name],
                evt.EventId);

            var payload = Assert.Single(evt.Payload!);

            Assert.Contains(
                exception.Message,
                payload?.ToString() ?? string.Empty);
        }
    }

    internal sealed class OpAmpTestEventListener : EventListener
    {
        public OpAmpTestEventListener(EventSource source)
        {
            this.EnableEvents(source, EventLevel.LogAlways);
        }

        public List<EventWrittenEventArgs> Events { get; } = [];

        public void Clear() => this.Events.Clear();

        protected override void OnEventWritten(EventWrittenEventArgs eventData) => this.Events.Add(eventData);
    }
}
