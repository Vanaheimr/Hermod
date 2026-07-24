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
    /// Which origins a connection is authoritative for — the two halves of the
    /// same question.
    ///
    /// A client that already holds a connection to us may reuse it for any origin
    /// our certificate covers (RFC 9113, Section 9.1.1), so the <c>:authority</c> a
    /// request names is not necessarily the name it dialed. The server therefore
    /// checks it, and declines with 421 (RFC 9110, Section 15.5.20) when it is not
    /// authoritative. The ORIGIN frame (RFC 8336) is the other direction: the
    /// server stating the set up front instead of leaving the client to infer it.
    /// </summary>
    [TestFixture]
    public class AuthoritativeOriginTests
    {

        #region (helpers)

        private static Task<(List<(String, String)>, Byte[]?)> Handle(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                   ([(":status", "200"), ("content-type", "text/plain")], Encoding.UTF8.GetBytes("served")));

        #endregion


        #region HostOf_StripsPortUserinfoAndBrackets()

        [Test]
        public void HostOf_StripsPortUserinfoAndBrackets()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTPAuthority.HostOf("example.com"),          Is.EqualTo("example.com"));
                Assert.That(HTTPAuthority.HostOf("example.com:8443"),     Is.EqualTo("example.com"));
                Assert.That(HTTPAuthority.HostOf("user@example.com:443"), Is.EqualTo("example.com"), "userinfo dropped");
                Assert.That(HTTPAuthority.HostOf("[::1]:8443"),           Is.EqualTo("::1"),         "IPv6 literal unwrapped");
                Assert.That(HTTPAuthority.HostOf("[::1]"),                Is.EqualTo("::1"));
                Assert.That(HTTPAuthority.HostOf("127.0.0.1:8443"),       Is.EqualTo("127.0.0.1"));
            });
        }

        #endregion

        #region MatchesName_FollowsRFC6125Wildcards()

        [Test]
        public void MatchesName_FollowsRFC6125Wildcards()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTPAuthority.MatchesName("example.com",     "example.com"),   Is.True);
                Assert.That(HTTPAuthority.MatchesName("EXAMPLE.com",     "example.COM"),   Is.True,  "host names are case-insensitive");
                Assert.That(HTTPAuthority.MatchesName("a.example.com",   "*.example.com"), Is.True,  "one label");
                Assert.That(HTTPAuthority.MatchesName("a.b.example.com", "*.example.com"), Is.False, "a wildcard covers exactly one label");
                Assert.That(HTTPAuthority.MatchesName("example.com",     "*.example.com"), Is.False, "and never the bare domain");
                Assert.That(HTTPAuthority.MatchesName("evil.com",        "example.com"),   Is.False);
            });
        }

        #endregion

        #region ServedByOrigins_MatchesAuthorityIncludingPort()

        [Test]
        public void ServedByOrigins_MatchesAuthorityIncludingPort()
        {

            var served = HTTPAuthority.ServedByOrigins([
                             "https://example.com",          // default port
                             "https://other.example:8443"
                         ]);

            Assert.Multiple(() =>
            {
                Assert.That(served("example.com"),         Is.True);
                Assert.That(served("example.com:443"),     Is.True,  "the default port may be spelled out");
                Assert.That(served("other.example:8443"),  Is.True);
                Assert.That(served("other.example"),       Is.False, "an explicit port is part of the origin");
                Assert.That(served("other.example:9000"),  Is.False);
                Assert.That(served("evil.example"),        Is.False);
            });

        }

        #endregion


        #region ForeignAuthority_Gets421()

        // The default predicate comes from the certificate, which covers localhost
        // and the loopback addresses — so a request naming any other origin is
        // misdirected, while the one we actually dialed is served as usual.
        [Test]
        public async Task ForeignAuthority_Gets421()
        {

            await using var srv  = await TestH2Server.StartAsync(Handle);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var ours     = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}",      "/");
            var foreign  = await conn.SendRequestAsync("GET", "https", $"evil.example:{srv.Port}",   "/");
            var loopback = await conn.SendRequestAsync("GET", "https", $"127.0.0.1:{srv.Port}",      "/");

            Assert.Multiple(() =>
            {
                Assert.That(ours.Status,                            Is.EqualTo(200), "the origin we dialed");
                Assert.That(Encoding.UTF8.GetString(ours.Body),     Is.EqualTo("served"));
                Assert.That(loopback.Status,                        Is.EqualTo(200), "covered by an iPAddress SAN");
                Assert.That(foreign.Status,                         Is.EqualTo(421), "not covered by the certificate");
            });

            // A stream-level answer: the connection keeps working afterwards.
            var after = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");
            Assert.That(after.Status, Is.EqualTo(200), "421 does not poison the connection");

        }

        #endregion

        #region ForeignAuthority_Gets421_OnTheStreamingPath()

        // A registered streaming handler is dispatched at HEADERS-complete, on a
        // different code path from the buffered one — the check has to be on both,
        // or registering a streaming handler would quietly disable it.
        [Test]
        public async Task ForeignAuthority_Gets421_OnTheStreamingPath()
        {

            var handlerRan = 0;

            await using var srv = await TestH2Server.StartAsync(
                                      Handle,
                                      StreamingHandler: async (req, resp, ct) => {
                                          Interlocked.Increment(ref handlerRan);
                                          await resp.WriteHeadersAsync([(":status", "200")], ct);
                                          await resp.CompleteAsync(null, ct);
                                      });

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var foreign = await conn.SendRequestAsync("GET", "https", "evil.example", "/");
            var ours    = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");

            Assert.Multiple(() =>
            {
                Assert.That(foreign.Status, Is.EqualTo(421));
                Assert.That(ours.   Status, Is.EqualTo(200));
                Assert.That(handlerRan,     Is.EqualTo(1), "the handler never saw the misdirected request");
            });

        }

        #endregion

        #region AuthorityCheck_CanBeOpenedUp()

        // The predicate is the whole policy: a server that means to answer for
        // anything says so, and then does.
        [Test]
        public async Task AuthorityCheck_CanBeOpenedUp()
        {

            await using var srv  = await TestH2Server.StartAsync(Handle, IsAuthorityServed: _ => true);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var foreign = await conn.SendRequestAsync("GET", "https", "anything.example", "/");

            Assert.That(foreign.Status, Is.EqualTo(200));

        }

        #endregion

        #region CleartextH2c_HasNoAuthorityToCheckAgainst()

        // No certificate, no origin set, no basis for a judgement — h2c serves
        // whatever authority it is asked for, rather than guessing.
        [Test]
        public async Task CleartextH2c_HasNoAuthorityToCheckAgainst()
        {

            await using var srv  = await TestH2Server.StartAsync(Handle, Cleartext: true);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, Cleartext: true);

            var foreign = await conn.SendRequestAsync("GET", "http", "anything.example", "/");

            Assert.Multiple(() =>
            {
                Assert.That(foreign.Status,    Is.EqualTo(200));
                Assert.That(conn.OriginSet,    Is.Null, "and nothing was announced");
            });

        }

        #endregion


        #region OriginFrame_IsAnnouncedAndReceived()

        // RFC 8336: the server announces its Origin Set right after the preface,
        // and it also becomes the yardstick for the 421 check — a server that says
        // what it serves must not then answer for something else.
        [Test]
        public async Task OriginFrame_IsAnnouncedAndReceived()
        {

            var origins = new[] { "https://alpha.example", "https://beta.example:8443" };

            await using var srv  = await TestH2Server.StartAsync(Handle, OriginSet: origins);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            // ORIGIN follows the server's SETTINGS on the wire, and the handshake
            // only waits for the SETTINGS — so it lands a beat later, unsolicited,
            // exactly as RFC 8336 intends. Wait for it rather than racing it.
            Assert.That(await H2.EventuallyAsync(() => conn.OriginSet is not null), Is.True, "ORIGIN frame arrived");
            Assert.That(conn.OriginSet, Is.EquivalentTo(origins), "announced set arrived intact");

            var alpha   = await conn.SendRequestAsync("GET", "https", "alpha.example",       "/");
            var beta    = await conn.SendRequestAsync("GET", "https", "beta.example:8443",   "/");
            var dialed  = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");

            Assert.Multiple(() =>
            {
                Assert.That(alpha. Status, Is.EqualTo(200), "announced origin");
                Assert.That(beta.  Status, Is.EqualTo(200), "announced origin, non-default port");
                Assert.That(dialed.Status, Is.EqualTo(421), "the announcement outranks the certificate");
            });

        }

        #endregion

        #region OriginFrame_IsIgnoredOverCleartext()

        // RFC 8336, Section 2.4: an unauthenticated peer's claim about which
        // origins it speaks for is worth nothing, so the client does not record it.
        [Test]
        public async Task OriginFrame_IsIgnoredOverCleartext()
        {

            await using var srv  = await TestH2Server.StartAsync(Handle, Cleartext: true,
                                                                 OriginSet: ["http://alpha.example"]);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, Cleartext: true);

            var alpha = await conn.SendRequestAsync("GET", "http", "alpha.example", "/");

            Assert.Multiple(() =>
            {
                Assert.That(conn.OriginSet, Is.Null,          "not recorded over an unauthenticated transport");
                Assert.That(alpha.Status,   Is.EqualTo(200),  "the server still applies its own set");
            });

        }

        #endregion

        #region OriginFrame_RoundTripsThroughTheCodec()

        [Test]
        public void OriginFrame_RoundTripsThroughTheCodec()
        {

            var origins = new[] { "https://a.example", "https://b.example:8443", "https://c.example" };
            var frame   = HTTP2Frame.CreateOrigin(origins);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Type,     Is.EqualTo(HTTP2FrameType.ORIGIN));
                Assert.That(frame.StreamId, Is.EqualTo(0u),                     "connection-level (§2.1)");
                Assert.That(HTTP2Frame.ParseOrigins(frame.Payload!), Is.EqualTo(origins));
            });

            // A truncated entry ends the enumeration instead of throwing: the frame
            // is advisory, and there is no error code defined for a malformed one.
            var truncated = frame.Payload![..(frame.Payload.Length - 3)];
            Assert.That(HTTP2Frame.ParseOrigins(truncated), Is.EqualTo(origins[..2]));

            Assert.That(HTTP2Frame.ParseOrigins([]), Is.Empty, "an empty payload yields nothing");

        }

        #endregion

    }

}
