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

using System.Text;
using System.Buffers.Binary;

using Microsoft.AspNetCore.Builder;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Client-side RFC 9218 priority signalling: the client EMITS the
    /// <c>priority</c> header and a well-formed PRIORITY_UPDATE frame (the server
    /// acting on them is proven separately by the raw h2priority scenarios). Here:
    /// frame-factory encoding, exact header value at our server, a mid-flight
    /// PRIORITY_UPDATE keeping the connection healthy, and interop with .NET
    /// Kestrel. In-process.
    /// </summary>
    [TestFixture]
    public class ClientPriorityTests
    {

        #region (our server handler)

        private static Task<(List<(String Name, String Value)>, Byte[]?)> Origin(
            UInt32 sid, List<(String Name, String Value)> h, Byte[]? body, CancellationToken ct)
        {
            var path     = h.First(x => x.Name == ":path").Value;
            var priority = h.FirstOrDefault(x => x.Name == "priority").Value ?? "none";

            if (path == "/slow")
                return SlowOk(ct);

            var reply = Encoding.UTF8.GetBytes(priority);   // echo the received priority header
            return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                ([(":status", "200"), ("content-length", reply.Length.ToString())], reply));

            static async Task<(List<(String, String)>, Byte[]?)> SlowOk(CancellationToken ct)
            {
                await Task.Delay(500, ct);
                var b = Encoding.UTF8.GetBytes("slow-ok");
                return ([(":status", "200"), ("content-length", b.Length.ToString())], b);
            }
        }

        #endregion


        #region FrameFactory_PriorityUpdate()

        [Test]
        public void FrameFactory_PriorityUpdate()
        {
            var frame   = HTTP2Frame.CreatePriorityUpdate(7, "u=0, i");
            var bytes   = frame.Serialize();
            var parsed  = HTTP2Frame.ParseHeader(bytes.AsSpan(0, 9));
            var payload = bytes[9..];
            var sid     = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(0, 4)) & 0x7FFFFFFFu;
            var val     = Encoding.ASCII.GetString(payload.AsSpan(4));

            Assert.Multiple(() =>
            {
                Assert.That(parsed.Type,     Is.EqualTo(HTTP2FrameType.PRIORITY_UPDATE), "type = PRIORITY_UPDATE (0x10)");
                Assert.That(parsed.StreamId, Is.EqualTo(0u),                             "frame stream id = 0");
                Assert.That(sid,             Is.EqualTo(7u),                             "prioritized stream id = 7");
                Assert.That(val,             Is.EqualTo("u=0, i"),                       "priority field value");
                Assert.That(new HTTP2Priority(0, true).ToHeaderValue(),  Is.EqualTo("u=0, i"), "priority header encoding u=0,i");
                Assert.That(new HTTP2Priority(5, false).ToHeaderValue(), Is.EqualTo("u=5"),    "priority header encoding u=5");
            });
        }

        #endregion

        #region OurServer_PriorityEmissionAndUpdate()

        [Test]
        public async Task OurServer_PriorityEmissionAndUpdate()
        {
            await using var srv = await TestH2Server.StartAsync(Origin);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var p0 = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/echo", Priority: new HTTP2Priority(0, true));
            var p5 = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/echo", Priority: new HTTP2Priority(5, false));
            var pNone = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/echo");
            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(p0.Body),    Is.EqualTo("u=0, i"), "Priority u=0,i -> exact header at server");
                Assert.That(Encoding.UTF8.GetString(p5.Body),    Is.EqualTo("u=5"),    "Priority u=5 -> exact header at server");
                Assert.That(Encoding.UTF8.GetString(pNone.Body), Is.EqualTo("none"),   "no Priority -> no priority header");
            });

            // PRIORITY_UPDATE mid-flight: a well-formed one keeps the connection healthy.
            var handle = await conn.StartRequestAsync("GET", "https", $"localhost:{srv.Port}", "/slow");
            Assert.That(handle.StreamId % 2, Is.EqualTo(1u), "StartRequestAsync exposes an odd stream id");
            await conn.UpdatePriorityAsync(handle.StreamId, new HTTP2Priority(0, false));
            var slow = await handle.Response;
            Assert.That(slow.Status, Is.EqualTo(200), "request completes after mid-flight PRIORITY_UPDATE");

            var after = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/echo");
            Assert.That(after.Status, Is.EqualTo(200), "connection healthy after PRIORITY_UPDATE");

            await conn.CloseAsync();
        }

        #endregion

        #region Kestrel_AcceptsPrioritySignals()

        [Test]
        public async Task Kestrel_AcceptsPrioritySignals()
        {
            await using var srv = await KestrelH2Server.StartAsync(app => app.MapGet("/", () => "kestrel-ok"));
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var withPriority = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/", Priority: new HTTP2Priority(1, false));
            Assert.Multiple(() =>
            {
                Assert.That(withPriority.Status, Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(withPriority.Body), Is.EqualTo("kestrel-ok"), "Kestrel accepts a priority-hinted request");
            });

            var handle = await conn.StartRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");
            await conn.UpdatePriorityAsync(handle.StreamId, new HTTP2Priority(0, false));
            var resp = await handle.Response;
            Assert.That(resp.Status, Is.EqualTo(200), "Kestrel accepts a client PRIORITY_UPDATE");

            var follow = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");
            Assert.That(follow.Status, Is.EqualTo(200), "Kestrel connection healthy afterward");

            await conn.CloseAsync();
        }

        #endregion

    }

}
