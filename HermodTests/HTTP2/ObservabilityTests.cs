/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The observability seam: structured events via <see cref="HTTP2EventSource"/>
    /// and spans via <see cref="HTTP2Diagnostics"/>, both BCL, so the stack can be
    /// observed without it taking a logging dependency.
    ///
    /// Worth testing rather than assuming, for two reasons. Emitting nothing is a
    /// perfectly quiet failure mode — a seam nobody exercises is indistinguishable
    /// from a seam that does not work. And the "costs nothing unobserved" claim is
    /// the reason it is acceptable in a hot path, so that too should be checked
    /// rather than asserted in a comment.
    /// </summary>
    [TestFixture]
    public class ObservabilityTests
    {

        #region (helpers)

        private static Task<(List<(String, String)>, Byte[]?)> Handle(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                   ([(":status", "200"), ("content-type", "text/plain")], Encoding.UTF8.GetBytes("ok")));

        /// <summary>Captures everything the stack's EventSource emits while alive.</summary>
        private sealed class Capture : EventListener
        {

            public readonly List<(String Name, List<Object?> Payload)> Events = [];

            protected override void OnEventSourceCreated(EventSource Source)
            {
                if (Source.Name == "Vanaheimr-Hermod-HTTP2")
                    EnableEvents(Source, EventLevel.Verbose);
            }

            protected override void OnEventWritten(EventWrittenEventArgs Event)
            {
                if (Event.EventName == "EventCounters")
                    return;

                lock (Events)
                    Events.Add((Event.EventName ?? "?", [.. Event.Payload ?? []]));
            }

            public Boolean Saw(String Name)
            {
                lock (Events)
                    return Events.Any(e => e.Name == Name);
            }

        }

        #endregion

        #region Events_AreEmittedForAConnectionAndRequest()

        [Test]
        public async Task Events_AreEmittedForAConnectionAndRequest()
        {

            using var capture = new Capture();

            await using var srv = await TestH2Server.StartAsync(Handle);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{srv.Port}", "/hello");
            await conn.CloseAsync();

            Assert.That(resp.Status, Is.EqualTo(200));

            Assert.That(await H2.EventuallyAsync(() => capture.Saw("RequestHandled")), Is.True,
                        "the request was reported");

            Assert.Multiple(() =>
            {
                Assert.That(capture.Saw("ServerListening"),       Is.True, "listener start");
                Assert.That(capture.Saw("ConnectionAccepted"),    Is.True, "accept");
                Assert.That(capture.Saw("ConnectionEstablished"), Is.True, "ALPN + TLS parameters");
            });

            // The negotiated security parameters are the ones otherwise invisible
            // after the handshake, so check they actually carry values.
            List<Object?> established;
            lock (capture.Events)
                established = capture.Events.First(e => e.Name == "ConnectionEstablished").Payload;

            Assert.Multiple(() =>
            {
                Assert.That(established[1]?.ToString(), Is.EqualTo("h2"),        "ALPN");
                Assert.That(established[2]?.ToString(), Does.StartWith("Tls"),   "TLS version");
                Assert.That(established[3]?.ToString(), Does.StartWith("TLS_"),  "cipher suite");
            });

            List<Object?> handled;
            lock (capture.Events)
                handled = capture.Events.First(e => e.Name == "RequestHandled").Payload;

            Assert.Multiple(() =>
            {
                Assert.That(handled[1]?.ToString(), Is.EqualTo(HTTPMethod.GET.ToString()));
                Assert.That(handled[2]?.ToString(), Is.EqualTo("/hello"));
                Assert.That(handled[3],             Is.EqualTo(200));
            });

        }

        #endregion

        #region Spans_NestRequestInsideConnection()

        // A request span whose parent is the connection span is what makes "this
        // slow request shared a connection with 40 others" visible in a trace.
        [Test]
        public async Task Spans_NestRequestInsideConnection()
        {

            var started = new List<Activity>();

            using var listener = new ActivityListener {
                ShouldListenTo     = source => source.Name == HTTP2Diagnostics.ActivitySourceName,
                Sample             = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted    = activity => { lock (started) started.Add(activity); }
            };

            ActivitySource.AddActivityListener(listener);

            await using var srv = await TestH2Server.StartAsync(Handle);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{srv.Port}", "/traced");
            await conn.CloseAsync();

            Assert.That(resp.Status, Is.EqualTo(200));

            Activity[] all;
            lock (started)
                all = [.. started];

            var connection = all.FirstOrDefault(a => a.OperationName.StartsWith("HTTP/2 connection server"));
            var request    = all.FirstOrDefault(a => a.OperationName == HTTPMethod.GET.ToString() && a.Kind == ActivityKind.Server);
            var clientSide = all.FirstOrDefault(a => a.OperationName == HTTPMethod.GET.ToString() && a.Kind == ActivityKind.Client);

            Assert.Multiple(() =>
            {
                Assert.That(connection, Is.Not.Null, "a connection span");
                Assert.That(request,    Is.Not.Null, "a server request span");
                Assert.That(clientSide, Is.Not.Null, "a client request span");

                Assert.That(request?.Parent,                        Is.EqualTo(connection), "request nests in the connection");
                Assert.That(request?.GetTagItem("url.path"),        Is.EqualTo("/traced"));
                Assert.That(request?.GetTagItem("http.request.method"), Is.EqualTo(HTTPMethod.GET));
                Assert.That(request?.GetTagItem("network.protocol.version"), Is.EqualTo("2"));
                Assert.That(clientSide?.GetTagItem("http.response.status_code"), Is.EqualTo(200));
            });

        }

        #endregion

        #region Unobserved_CostsNothing()

        // The claim that justifies putting this in a hot path: with no listener,
        // StartActivity returns null and the EventSource is disabled, so no span
        // object and no event payload is ever allocated.
        [Test]
        public void Unobserved_CostsNothing()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTP2Diagnostics.StartRequest(HTTPMethod.GET, URIScheme.https, "example.com", "/", 1, "server"),
                            Is.Null, "no listener -> no Activity allocated");

                Assert.That(HTTP2Diagnostics.StartConnection("127.0.0.1:1", "server"),
                            Is.Null);

                Assert.That(HTTP2EventSource.Log.IsEnabled(), Is.False,
                            "and the EventSource reports itself disabled, so payloads are never built");
            });
        }

        #endregion

        #region AbuseDefences_AreReportable()

        // The gap this seam was really meant to close: the hardening counters used
        // to detect their conditions and then tell nobody but stdout. Here the
        // event is checked directly, since provoking a real Rapid Reset storm in a
        // unit test would be slow and flaky.
        [Test]
        public void AbuseDefences_AreReportable()
        {

            using var capture = new Capture();

            HTTP2EventSource.Log.AbuseDetected("rapid-reset", "peer reset 51% of opened streams");

            Assert.That(capture.Saw("AbuseDetected"), Is.True);

            List<Object?> payload;
            lock (capture.Events)
                payload = capture.Events.First(e => e.Name == "AbuseDetected").Payload;

            Assert.Multiple(() =>
            {
                Assert.That(payload[0]?.ToString(), Is.EqualTo("rapid-reset"));
                Assert.That(payload[1]?.ToString(), Does.Contain("51%"));
            });

        }

        #endregion

    }

}
