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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Alternative services (RFC 7838): the ALTSVC frame and the <c>Alt-Svc</c>
    /// field value it carries. With ORIGIN already in place this completes the set
    /// of extension frames that are still alive in the wild.
    ///
    /// An alternative is not a redirect — it names another route to the *same*
    /// origin, so the authority in requests does not change. The classic use is a
    /// server pointing at its HTTP/3 endpoint over an HTTP/2 connection, which is
    /// also why the client here records alternatives without acting on them: acting
    /// would mean speaking a protocol this stack does not implement yet.
    /// </summary>
    [TestFixture]
    public class AlternativeServiceTests
    {

        #region (helpers)

        private static Task<(List<(String, String)>, Byte[]?)> Handle(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                   ([(":status", "200")], Encoding.UTF8.GetBytes("ok")));

        #endregion


        #region FieldValue_Parses()

        [Test]
        public void FieldValue_Parses()
        {

            var single = HTTPAlternativeService.Parse("h3=\":443\"");

            Assert.Multiple(() =>
            {
                Assert.That(single,               Has.Count.EqualTo(1));
                Assert.That(single[0].ProtocolId, Is.EqualTo("h3"));
                Assert.That(single[0].Host,       Is.Empty,                                  "an empty host means the same host");
                Assert.That(single[0].Port,       Is.EqualTo(443));
                Assert.That(single[0].MaxAge,     Is.EqualTo(HTTPAlternativeService.DefaultMaxAge), "ma defaults to 24 h");
                Assert.That(single[0].Persist,    Is.False);
            });

            // Several alternatives, in the sender's order of preference, with
            // parameters attached to each.
            var many = HTTPAlternativeService.Parse(
                           "h3=\"alt.example:8443\"; ma=3600; persist=1, h2=\":443\"; ma=7200");

            Assert.Multiple(() =>
            {
                Assert.That(many,                Has.Count.EqualTo(2));
                Assert.That(many[0].ProtocolId,  Is.EqualTo("h3"));
                Assert.That(many[0].Host,        Is.EqualTo("alt.example"));
                Assert.That(many[0].Port,        Is.EqualTo(8443));
                Assert.That(many[0].MaxAge,      Is.EqualTo(TimeSpan.FromHours(1)));
                Assert.That(many[0].Persist,     Is.True);
                Assert.That(many[1].ProtocolId,  Is.EqualTo("h2"));
                Assert.That(many[1].MaxAge,      Is.EqualTo(TimeSpan.FromHours(2)));
                Assert.That(many[1].Persist,     Is.False);
            });

        }

        #endregion

        #region FieldValue_HandlesTheAwkwardCases()

        [Test]
        public void FieldValue_HandlesTheAwkwardCases()
        {

            // "clear" is an instruction to forget every alternative, which is the
            // opposite of "no alternatives were listed" — so it is reported
            // separately rather than as an empty list.
            var cleared = HTTPAlternativeService.Parse("clear", out var isClear);

            // A percent-encoded ALPN name (the field is a token, ALPN names are not).
            var encoded = HTTPAlternativeService.Parse("%68%33=\":443\"");

            // An IPv6 literal keeps its brackets; the port is still the last colon.
            var ipv6    = HTTPAlternativeService.Parse("h3=\"[2001:db8::1]:443\"");

            // Garbage entries are skipped, not fatal: the good one beside them
            // survives, because this is advisory routing information.
            var mixed   = HTTPAlternativeService.Parse("nonsense, h2=\":8443\", also-nonsense=");

            Assert.Multiple(() =>
            {
                Assert.That(isClear,            Is.True);
                Assert.That(cleared,            Is.Empty);

                Assert.That(encoded,            Has.Count.EqualTo(1));
                Assert.That(encoded[0].ProtocolId, Is.EqualTo("h3"),              "percent-decoded");

                Assert.That(ipv6,               Has.Count.EqualTo(1));
                Assert.That(ipv6[0].Host,       Is.EqualTo("[2001:db8::1]"));
                Assert.That(ipv6[0].Port,       Is.EqualTo(443));

                Assert.That(mixed,              Has.Count.EqualTo(1),             "only the well-formed entry");
                Assert.That(mixed[0].ProtocolId, Is.EqualTo("h2"));
            });

        }

        #endregion

        #region FieldValue_RoundTrips()

        [Test]
        public void FieldValue_RoundTrips()
        {

            var original = new HTTPAlternativeService("h3", "alt.example", 8443, TimeSpan.FromHours(1), true);
            var reparsed = HTTPAlternativeService.Parse(original.ToFieldValue());

            Assert.Multiple(() =>
            {
                Assert.That(original.ToFieldValue(), Is.EqualTo("h3=\"alt.example:8443\"; ma=3600; persist=1"));
                Assert.That(reparsed,                Has.Count.EqualTo(1));
                Assert.That(reparsed[0],             Is.EqualTo(original));

                // The default max-age is omitted rather than spelled out.
                Assert.That(new HTTPAlternativeService("h3", "", 443, HTTPAlternativeService.DefaultMaxAge, false).ToFieldValue(),
                            Is.EqualTo("h3=\":443\""));
            });

        }

        #endregion

        #region Frame_RoundTrips()

        [Test]
        public void Frame_RoundTrips()
        {

            var frame = HTTP2Frame.CreateAltSvc("https://example.com", "h3=\":443\"; ma=3600");

            Assert.That(HTTP2Frame.TryParseAltSvc(frame.Payload!, out var origin, out var value), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Type,     Is.EqualTo(HTTP2FrameType.ALTSVC));
                Assert.That(frame.StreamId, Is.EqualTo(0u));
                Assert.That(origin,         Is.EqualTo("https://example.com"));
                Assert.That(value,          Is.EqualTo("h3=\":443\"; ma=3600"));

                // The request-stream form carries no origin.
                var onStream = HTTP2Frame.CreateAltSvc("", "h3=\":443\"", StreamId: 3);
                Assert.That(HTTP2Frame.TryParseAltSvc(onStream.Payload!, out var noOrigin, out _), Is.True);
                Assert.That(onStream.StreamId, Is.EqualTo(3u));
                Assert.That(noOrigin,          Is.Empty);

                // Truncated payloads tell us nothing and must not half-parse.
                Assert.That(HTTP2Frame.TryParseAltSvc([],           out _, out _), Is.False, "no length prefix");
                Assert.That(HTTP2Frame.TryParseAltSvc([0x00],       out _, out _), Is.False, "half a length prefix");
                Assert.That(HTTP2Frame.TryParseAltSvc([0x00, 0x40], out _, out _), Is.False, "origin length past the end");
            });

        }

        #endregion


        #region Server_AnnouncesAlternatives_ClientRecordsThem()

        [Test]
        public async Task Server_AnnouncesAlternatives_ClientRecordsThem()
        {

            await using var srv = await TestH2Server.StartAsync(
                                      Handle,
                                      AlternativeServices: [("https://example.com", "h3=\":443\"; ma=3600; persist=1")]);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            // ALTSVC follows the server's SETTINGS, so it lands a beat after the
            // handshake completes — unsolicited, exactly as RFC 7838 intends.
            Assert.That(await H2.EventuallyAsync(() => conn.AlternativeServices.Count > 0), Is.True, "ALTSVC arrived");

            var announced = conn.AlternativeServices["https://example.com"];

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(announced,               Has.Count.EqualTo(1));
                Assert.That(announced[0].ProtocolId, Is.EqualTo("h3"));
                Assert.That(announced[0].Port,       Is.EqualTo(443));
                Assert.That(announced[0].MaxAge,     Is.EqualTo(TimeSpan.FromHours(1)));
                Assert.That(announced[0].Persist,    Is.True);
            });

        }

        #endregion

        #region Client_IgnoresInvalidFrames()

        // RFC 7838 §4: stream 0 requires an origin, any other stream forbids one.
        // Neither mismatch is a protocol error — the frame is simply ignored, since
        // there is no error code defined for a bad ALTSVC.
        [Test]
        public async Task Client_IgnoresInvalidFrames()
        {

            await using var mock = MockH2Server.Start(0, async (index, ssl, frame, encoder) =>
            {

                if (frame.Type == HTTP2FrameType.SETTINGS && frame.IsAck)
                {
                    // Invalid: stream 0 with no origin.
                    await MockH2Server.WriteFrameAsync(ssl, HTTP2Frame.CreateAltSvc("", "h3=\":443\""));

                    // Invalid: a request stream carrying an origin.
                    await MockH2Server.WriteFrameAsync(ssl, HTTP2Frame.CreateAltSvc("https://example.com", "h3=\":443\"", StreamId: 1));

                    // Valid, so the test can tell "ignored the bad ones" from
                    // "ignored everything".
                    await MockH2Server.WriteFrameAsync(ssl, HTTP2Frame.CreateAltSvc("https://good.example", "h2=\":8443\""));
                }

            });

            var conn = await HTTP2Client.ConnectAsync("localhost", mock.Port, H2.AcceptAnyServerCert);

            Assert.That(await H2.EventuallyAsync(() => conn.AlternativeServices.Count > 0), Is.True);

            var recorded = conn.AlternativeServices;

            Assert.Multiple(() =>
            {
                Assert.That(recorded,                          Has.Count.EqualTo(1), "only the valid frame counted");
                Assert.That(recorded.ContainsKey("https://good.example"), Is.True);
                Assert.That(recorded.ContainsKey("https://example.com"),  Is.False, "origin on a request stream -> ignored");
            });

        }

        #endregion

    }

}
