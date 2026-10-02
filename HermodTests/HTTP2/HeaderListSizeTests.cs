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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// <c>SETTINGS_MAX_HEADER_LIST_SIZE</c> in the outbound direction (RFC 9113,
    /// Section 6.5.2) — refusing to send a header list the peer has already said it
    /// will not accept.
    ///
    /// The server has always done this. The client did not: it tracked the peer's
    /// limit, enforced it on inbound responses, and then sent whatever it was asked
    /// to. The result was a round trip spent on headers that came back as a stream
    /// reset, with nothing at the call site to say why.
    ///
    /// Both roles now measure the same way, through
    /// <see cref="HTTP2HeaderList.UncompressedSize"/> — the limit is stated against
    /// the *uncompressed* list precisely because the compressed size depends on
    /// whichever connection's dynamic table it travels on.
    ///
    /// Neither role stated its own limit, and each held the other to its own
    /// default, 8 KiB, until the other stated one: the tests here ran into that
    /// default, never into anything on the wire. RFC 9113 makes the initial value
    /// unlimited. Both roles now state their limit in their connection preface,
    /// 32 KiB, and hold the other to the limit it stated, and to none before that.
    /// What a role does with a header list over its own limit is in
    /// ServerHeaderListSizeTests and ClientHeaderListSizeTests.
    /// </summary>
    [TestFixture]
    public class HeaderListSizeTests
    {

        #region Data / handlers

        /// <summary>
        /// What both roles advertise by default (<c>HTTP2Settings</c>).
        /// </summary>
        private const Int32 AdvertisedLimit = 32 * 1024;

        private static Task<(List<(String, String)>, Byte[]?)> Ok(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>((
                   [(":status", "200"), ("content-type", "text/plain")], "ok"u8.ToArray()));

        /// <summary>
        /// Answers with a header list far past what the client will accept.
        /// </summary>
        private static Task<(List<(String, String)>, Byte[]?)> Oversized(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>((
                   [(":status", "200"), ("x-huge", new String('r', 40000))], "ok"u8.ToArray()));

        private static async Task OkStreaming(IHTTP2RequestStream req, IHTTP2ResponseStream resp, CancellationToken ct)
        {
            while (await req.ReadAsync(ct) is not null) { }
            await resp.WriteHeadersAsync([(":status", "200")], ct);
            await resp.CompleteAsync(null, ct);
        }

        #endregion

        #region UncompressedSize_UsesTheRFCsAccounting()

        [Test]
        public void UncompressedSize_UsesTheRFCsAccounting()
        {

            Assert.Multiple(() =>
            {

                Assert.That(HTTP2HeaderList.UncompressedSize([]), Is.EqualTo(0));

                // Name + value + a flat 32 bytes of assumed per-field overhead.
                Assert.That(HTTP2HeaderList.UncompressedSize([("ab", "cde")]), Is.EqualTo(2 + 3 + 32));

                // The 32 is per field, not per list — which is the part that would go
                // wrong first if the formula were ever written out twice.
                Assert.That(HTTP2HeaderList.UncompressedSize([("a", ""), ("b", ""), ("c", "")]),
                            Is.EqualTo(3 * 33));

                Assert.That(HTTP2HeaderList.UncompressedSize([("x", new String('y', 1000))]),
                            Is.EqualTo(1 + 1000 + 32));

            });

        }

        #endregion

        #region Client_RefusesToSendMoreThanThePeerAllows()

        [Test]
        public async Task Client_RefusesToSendMoreThanThePeerAllows()
        {

            await using var srv = await TestH2Server.StartAsync(Ok);

            var conn      = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var authority = $"localhost:{srv.Port}";

            var first = await conn.StartRequestAsync(HTTPMethod.GET, URIScheme.https, authority, "/");
            await first.Response;

            // Comfortably under the advertised 32 KiB: unaffected.
            var modest = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, authority, "/",
                             [("x-context", new String('c', 16000))]);

            Assert.That(async () => await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, authority, "/",
                            [("x-huge", new String('h', 40000))]),
                        Throws.InstanceOf<InvalidOperationException>()
                              .With.Message.Contains("MAX_HEADER_LIST_SIZE"),
                        "refused locally rather than sent and reset");

            // The connection is untouched, and — because the check runs before the
            // stream is allocated — the refused request burned no stream ID.
            var next = await conn.StartRequestAsync(HTTPMethod.GET, URIScheme.https, authority, "/");
            var body = await next.Response;

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(modest.Status,               Is.EqualTo(200));
                Assert.That(body.Status,                 Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(body.Body), Is.EqualTo("ok"));
                Assert.That(next.StreamId,               Is.EqualTo(first.StreamId + 4),
                            "two streams were used between them (the modest one and this one), not three");
            });

        }

        #endregion

        #region Client_RefusesTrailersLargerThanThePeerAllows()

        // The second outbound header path. The stream exists by this point, so the
        // failure is stream-level — the same shape as an invalid trailer field.
        [Test]
        public async Task Client_RefusesTrailersLargerThanThePeerAllows()
        {

            await using var srv = await TestH2Server.StartAsync(Ok, StreamingHandler: OkStreaming);

            var conn   = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var stream = await conn.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, $"localhost:{srv.Port}", "/");

            await stream.WriteAsync("body"u8.ToArray());

            Assert.That(async () => await stream.CompleteRequestAsync([("x-huge", new String('h', 40000))]),
                        Throws.InstanceOf<HTTP2StreamException>()
                              .With.Message.Contains("MAX_HEADER_LIST_SIZE"));

            // Modest trailers on a fresh stream still go out.
            var ok = await conn.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, $"localhost:{srv.Port}", "/");
            await ok.CompleteRequestAsync([("x-small", "1")]);
            var head = await ok.GetResponseAsync();

            await conn.CloseAsync();

            Assert.That(head.Status, Is.EqualTo(200));

        }

        #endregion

        #region Server_RefusesToSendMoreThanTheClientAllows()

        // The half that already existed, kept honest across the move of the
        // accounting into Core. The handler's oversized response never reaches the
        // wire; the connection's catch-all turns it into a 500, which is by
        // construction small enough to send.
        [Test]
        public async Task Server_RefusesToSendMoreThanTheClientAllows()
        {

            await using var srv = await TestH2Server.StartAsync(Oversized);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var response = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{srv.Port}", "/");

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.Status, Is.EqualTo(500));
                Assert.That(response.HeaderValue("x-huge"), Is.Null, "and none of it leaked out");
            });

        }

        #endregion

        #region Server_HoldsAClientToTheLimitItStated(Stated)

        // The other half of the server's test above, without a Hermod client to
        // state a limit for it: one stated in a SETTINGS frame of the test's own
        // is held to, and a client that states none is held to none — not to the
        // server's own limit, which the server used to assume for it.
        [TestCase(null)]
        [TestCase(1000u)]
        public async Task Server_HoldsAClientToTheLimitItStated(UInt32? Stated)
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(Oversized);

            if (Stated is { } limit)
            {
                await peer.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_HEADER_LIST_SIZE, limit))).WaitAsync(PipedH2ServerConnection.StepTimeout);
                await peer.PingAsync();
            }

            await peer.RequestAsync(1, "/");

            var response = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(response?.Status,                                   Is.EqualTo(Stated is null ? "200" : "500"), "status of the response");
                Assert.That(response?.Headers.Any(field => field.Name == "x-huge"), Is.EqualTo(Stated is null),            "whether the large field went out");
            });

        }

        #endregion

        #region Client_HoldsAServerToTheLimitItStated(Stated)

        // And the client's: a request past a limit the server stated is refused
        // before it is sent; a server that states none is held to none.
        [TestCase(null)]
        [TestCase(1000u)]
        public async Task Client_HoldsAServerToTheLimitItStated(UInt32? Stated)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection = await transport.ConnectAsync(new HTTP2ClientOptions());

            if (Stated is { } limit)
                await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_HEADER_LIST_SIZE, limit)));

            var response = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/", [("x-huge", new String('h', 40000))]);

            if (Stated is null)
            {

                var request = await transport.NextHeadersAsync();

                await transport.RespondAsync(request.StreamId, "ok");

                Assert.That((await response.WaitAsync(HoldingH2Transport.StepTimeout)).Status, Is.EqualTo(200));

            }
            else
                Assert.That(async () => await response.WaitAsync(HoldingH2Transport.StepTimeout),
                            Throws.InstanceOf<InvalidOperationException>()
                                  .With.Message.Contains("MAX_HEADER_LIST_SIZE"));

            await connection.CloseAsync();

        }

        #endregion

        #region Client_HoldsAServerToTheLimitItStated_Trailers(Stated)

        // The same for trailers, the client's second outbound header path.
        [TestCase(null)]
        [TestCase(1000u)]
        public async Task Client_HoldsAServerToTheLimitItStated_Trailers(UInt32? Stated)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection = await transport.ConnectAsync(new HTTP2ClientOptions());

            if (Stated is { } limit)
                await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_HEADER_LIST_SIZE, limit)));

            var upload = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await transport.NextHeadersAsync();

            var ending = upload.CompleteRequestAsync([("x-huge", new String('h', 40000))]);

            if (Stated is null)
            {

                await ending.WaitAsync(HoldingH2Transport.StepTimeout);

                var trailers = await transport.NextHeadersAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(trailers.StreamId,   Is.EqualTo(upload.StreamId),  "the stream of the trailers");
                    Assert.That(trailers.EndStream,  Is.True,                      "the trailers end the request");
                });

            }
            else
                Assert.That(async () => await ending.WaitAsync(HoldingH2Transport.StepTimeout),
                            Throws.InstanceOf<HTTP2StreamException>()
                                  .With.Message.Contains("MAX_HEADER_LIST_SIZE"));

            await connection.CloseAsync();

        }

        #endregion

        #region BothRolesAdvertiseTheSameLimit()

        // The tests above rely on the limit each end states in its connection
        // preface; if that changes, they should say so rather than quietly start
        // measuring something else. Read off the wire: a limit kept in
        // HTTP2Settings and never sent held only the end that kept it.
        [Test]
        public async Task BothRolesAdvertiseTheSameLimit()
        {

            await using var server     = await PipedH2ServerConnection.StartAsync(Ok);

            var serverSettings         = server.FramesOn(0).First(frame => frame.Frame.Type == HTTP2FrameType.SETTINGS && !frame.Frame.IsAck).Frame;

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var client                 = await transport.ConnectAsync(new HTTP2ClientOptions());
            var response               = client.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/");
            var sent                   = new List<HTTP2Frame>();
            var request                = await transport.NextHeadersAsync(sent);

            await transport.RespondAsync(request.StreamId, "ok");
            await response.WaitAsync(HoldingH2Transport.StepTimeout);
            await client.CloseAsync();

            var clientSettings         = sent.First(frame => frame.Type == HTTP2FrameType.SETTINGS && !frame.IsAck);

            Assert.Multiple(() =>
            {
                Assert.That(RawHeaderBlock.SettingOf(serverSettings, HTTP2SettingsParameter.MAX_HEADER_LIST_SIZE),
                            Is.EqualTo((UInt32) AdvertisedLimit), "the server's connection preface");
                Assert.That(RawHeaderBlock.SettingOf(clientSettings, HTTP2SettingsParameter.MAX_HEADER_LIST_SIZE),
                            Is.EqualTo((UInt32) AdvertisedLimit), "the client's connection preface");
            });

        }

        #endregion

    }

}
