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

using System.Net.Security;
using System.Security.Authentication;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// RFC 9113, Section 9.2.2: HTTP/2 over TLS 1.2 must not use a cipher suite
    /// listed in Appendix A, and an endpoint may answer one with a connection
    /// error of type INADEQUATE_SECURITY.
    ///
    /// The decision logic is tested exhaustively by class, on cipher-suite *names*
    /// — that is the level the rule is actually defined at, and it keeps the tests
    /// independent of which suites a given .NET version happens to have enum
    /// members for. The wire behavior is then covered by a TLS 1.2 handshake
    /// against a live server, which proves the check runs on that path and does
    /// not false-positive on a modern suite.
    ///
    /// A live *negative* test (server actually negotiating a blocklisted suite) is
    /// not portable: forcing a specific suite needs CipherSuitesPolicy, which
    /// throws PlatformNotSupportedException on Windows.
    /// </summary>
    [TestFixture]
    public class TLSCipherSuiteTests
    {

        #region NoForwardSecrecy_IsBlocklisted()

        // Appendix A blocks every suite whose key exchange is not ephemeral:
        // static RSA, static (EC)DH, plain PSK, and the anonymous variants — even
        // when the cipher itself is a perfectly good AEAD.
        [Test]
        public void NoForwardSecrecy_IsBlocklisted()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_RSA_WITH_AES_128_GCM_SHA256"),      Is.False, "static RSA");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_RSA_WITH_AES_256_GCM_SHA384"),      Is.False, "static RSA");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_PSK_WITH_AES_128_GCM_SHA256"),      Is.False, "plain PSK");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_RSA_PSK_WITH_AES_128_GCM_SHA256"),  Is.False, "RSA-PSK");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDH_RSA_WITH_AES_128_GCM_SHA256"), Is.False, "static ECDH, not ECDHE");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_DH_DSS_WITH_AES_128_GCM_SHA256"),   Is.False, "static DH, not DHE");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_DH_anon_WITH_AES_128_GCM_SHA256"),  Is.False, "anonymous");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_NULL_WITH_NULL_NULL"),              Is.False, "the null suite");
            });
        }

        #endregion

        #region NoAEAD_IsBlocklisted()

        // The other half of Appendix A: ephemeral key exchange, but a non-AEAD
        // cipher — CBC-plus-MAC, RC4, 3DES, NULL.
        [Test]
        public void NoAEAD_IsBlocklisted()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA"),      Is.False, "CBC");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384"),   Is.False, "CBC");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_DHE_RSA_WITH_AES_128_CBC_SHA256"),     Is.False, "CBC");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_ECDSA_WITH_3DES_EDE_CBC_SHA"),   Is.False, "3DES");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_RSA_WITH_RC4_128_SHA"),          Is.False, "RC4");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_ECDSA_WITH_NULL_SHA"),           Is.False, "no encryption at all");
            });
        }

        #endregion

        #region EphemeralAEAD_IsPermitted()

        // The complement of Appendix A: ephemeral key exchange *and* an AEAD
        // cipher. These are the only TLS 1.2 suites HTTP/2 may run over.
        [Test]
        public void EphemeralAEAD_IsPermitted()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256"),          Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384"),        Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_DHE_RSA_WITH_AES_128_GCM_SHA256"),            Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256"),    Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_ECDSA_WITH_AES_128_CCM"),               Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8"),             Is.True, "truncated tag is still AEAD");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_DHE_PSK_WITH_AES_128_GCM_SHA256"),            Is.True, "PSK is fine once it is ephemeral");
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_ECDHE_PSK_WITH_CHACHA20_POLY1305_SHA256"),    Is.True);
            });
        }

        #endregion

        #region TLS13Suites_ArePermitted()

        // TLS 1.3 suites name only the cipher (no "_WITH_"): the key exchange is
        // always ephemeral and the cipher is always AEAD, so none of them can be
        // on the list.
        [Test]
        public void TLS13Suites_ArePermitted()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_AES_128_GCM_SHA256"),        Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_AES_256_GCM_SHA384"),        Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_CHACHA20_POLY1305_SHA256"),  Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_AES_128_CCM_SHA256"),        Is.True);
                Assert.That(HTTP2CipherSuites.IsPermitted("TLS_AES_128_CCM_8_SHA256"),      Is.True);
            });
        }

        #endregion

        #region UnnamedSuite_IsPermitted()

        // Appendix A is a closed list: a suite registered after RFC 9113 — one this
        // runtime has no enum name for — is by definition not on it. Refusing it
        // would be stricter than the RFC, so the unknown is let through.
        [Test]
        public void UnnamedSuite_IsPermitted()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTP2CipherSuites.IsPermitted(null),                              Is.True, "no name -> not on the list");
                Assert.That(HTTP2CipherSuites.IsPermitted(""),                                Is.True);
                Assert.That(HTTP2CipherSuites.IsBlocklisted((TlsCipherSuite) 0xFAFA),         Is.False, "unassigned code point");
            });
        }

        #endregion

        #region EnumOverload_AgreesWithNames()

        // The TlsCipherSuite overload is what the server and client actually call;
        // check it lines up with the name-level rule for suites every .NET version
        // knows.
        [Test]
        public void EnumOverload_AgreesWithNames()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTP2CipherSuites.IsBlocklisted(TlsCipherSuite.TLS_AES_128_GCM_SHA256),                 Is.False, "TLS 1.3");
                Assert.That(HTTP2CipherSuites.IsBlocklisted(TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256),  Is.False, "ECDHE + AEAD");
                Assert.That(HTTP2CipherSuites.IsBlocklisted(TlsCipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256),        Is.True,  "no forward secrecy");
                Assert.That(HTTP2CipherSuites.IsBlocklisted(TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA),     Is.True,  "no AEAD");
                Assert.That(HTTP2CipherSuites.IsBlocklisted(TlsCipherSuite.TLS_NULL_WITH_NULL_NULL),                Is.True,  "the null suite");
            });
        }

        #endregion


        #region TLS12Handshake_WithModernSuite_IsAccepted()

        // The wire side: a client that offers *only* TLS 1.2 lands on the
        // enforcement path (TLS 1.3 skips it), negotiates ECDHE + AES-GCM, and must
        // be served normally — a SETTINGS preface, not GOAWAY/INADEQUATE_SECURITY.
        [Test]
        public async Task TLS12Handshake_WithModernSuite_IsAccepted()
        {

            await using var srv = await TestH2Server.StartAsync(
                                      (s, h, b, ct) => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                                          ([(":status", "200")], Encoding.UTF8.GetBytes("ok"))));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await using var ssl = await H2Raw.ConnectTlsAsync(srv.Port,
                                                              Protocols:         SslProtocols.Tls12,
                                                              CancellationToken: cts.Token);

            Assert.Multiple(() =>
            {
                Assert.That(ssl.SslProtocol,                                      Is.EqualTo(SslProtocols.Tls12), "TLS 1.2 was actually negotiated");
                Assert.That(HTTP2CipherSuites.IsBlocklisted(ssl.NegotiatedCipherSuite), Is.False,                 $"{ssl.NegotiatedCipherSuite} is HTTP/2-eligible");
            });

            await ssl.WriteAsync(H2Raw.Preface, cts.Token);
            await ssl.WriteAsync(HTTP2Frame.CreateSettings().Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            var first = await H2Raw.ReadFrameAsync(ssl, cts.Token);

            Assert.That(first,      Is.Not.Null,                          "server sent its preface");
            Assert.That(first!.Type, Is.EqualTo(HTTP2FrameType.SETTINGS), "SETTINGS, not a GOAWAY");

        }

        #endregion

        #region TLS12Handshake_Rejected_SendsGoAwayInadequateSecurity()

        // The rejection path itself, exercised by pretending every suite is
        // blocklisted: with enforcement on, a TLS 1.2 peer must receive the server
        // preface followed by GOAWAY(INADEQUATE_SECURITY) — and nothing else.
        [Test]
        public async Task TLS12Handshake_Rejected_SendsGoAwayInadequateSecurity()
        {

            await using var srv = await TestH2Server.StartAsync(
                                      (s, h, b, ct) => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                                          ([(":status", "200")], null)),
                                      IsBlocklistedCipherSuite: _ => true);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await using var ssl = await H2Raw.ConnectTlsAsync(srv.Port,
                                                              Protocols:         SslProtocols.Tls12,
                                                              CancellationToken: cts.Token);

            await ssl.WriteAsync(H2Raw.Preface, cts.Token);
            await ssl.WriteAsync(HTTP2Frame.CreateSettings().Serialize(), cts.Token);
            await ssl.FlushAsync(cts.Token);

            var settings = await H2Raw.ReadFrameAsync(ssl, cts.Token);
            var goAway   = await H2Raw.ReadFrameAsync(ssl, cts.Token);

            Assert.Multiple(() =>
            {
                Assert.That(settings?.Type, Is.EqualTo(HTTP2FrameType.SETTINGS), "server preface still comes first (§3.4)");
                Assert.That(goAway?.Type,   Is.EqualTo(HTTP2FrameType.GOAWAY),   "then the connection error");
            });

            Assert.That(goAway!.Payload!.Length, Is.GreaterThanOrEqualTo(8));

            var lastStreamId = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(goAway.Payload.AsSpan(0)) & 0x7FFFFFFFu;
            var errorCode    = (HTTP2ErrorCode) System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(goAway.Payload.AsSpan(4));

            Assert.Multiple(() =>
            {
                Assert.That(errorCode,    Is.EqualTo(HTTP2ErrorCode.INADEQUATE_SECURITY));
                Assert.That(lastStreamId, Is.EqualTo(0u), "no stream was ever processed");
            });

        }

        #endregion

    }

}
