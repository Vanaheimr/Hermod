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
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// What this listener promises in the ALPN handshake, and whether it keeps the
    /// promise.
    ///
    /// The rule: <c>http/1.1</c> is advertised only when an application supplied a
    /// handler for it. Advertising a protocol and then not serving it is worse than
    /// not advertising it — a client that could have spoken h2 may pick http/1.1 on
    /// the strength of the offer and then get nothing at all.
    /// </summary>
    [TestFixture]
    public class AlpnFallbackTests
    {

        #region (helpers)

        private static Task<(List<(String, String)>, Byte[]?)> Handle(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                   ([(":status", "200")], Encoding.UTF8.GetBytes("h2")));

        /// <summary>
        /// Connect and complete a TLS handshake offering exactly these ALPN protocols.
        /// </summary>
        private static async Task<SslStream> ConnectOfferingAsync(Int32 Port, params SslApplicationProtocol[] Protocols)
        {

            var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, Port);

            var ssl  = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            var opts = new SslClientAuthenticationOptions { TargetHost = "localhost" };

            if (Protocols.Length > 0)
                opts.ApplicationProtocols = [.. Protocols];

            await ssl.AuthenticateAsClientAsync(opts);
            return ssl;

        }

        #endregion

        #region WithoutAHandler_Http11IsNotAdvertised()

        // An http/1.1-only client must fail the handshake outright rather than be
        // told "yes" and then handed silence.
        [Test]
        public async Task WithoutAHandler_Http11IsNotAdvertised()
        {

            await using var srv = await TestH2Server.StartAsync(Handle);

            Assert.That(async () => await ConnectOfferingAsync(srv.Port, SslApplicationProtocol.Http11),
                        Throws.InstanceOf<AuthenticationException>(),
                        "h2-only endpoint refuses an http/1.1-only client at the handshake");

            // ...while an h2 client is unaffected.
            await using var h2 = await ConnectOfferingAsync(srv.Port, SslApplicationProtocol.Http2);
            Assert.That(h2.NegotiatedApplicationProtocol, Is.EqualTo(SslApplicationProtocol.Http2));

        }

        #endregion

        #region WithAHandler_Http11IsServed()

        [Test]
        public async Task WithAHandler_Http11IsServed()
        {

            var served = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var srv = await TestH2Server.StartAsync(
                                      Handle,
                                      HTTP11Fallback: async (stream, ct) =>
                                      {
                                          var body     = "served by the fallback"u8.ToArray();
                                          var response = Encoding.ASCII.GetBytes(
                                                             "HTTP/1.1 200 OK\r\n" +
                                                             "Content-Type: text/plain\r\n" +
                                                            $"Content-Length: {body.Length}\r\n" +
                                                             "Connection: close\r\n\r\n");

                                          await stream.WriteAsync(response, ct);
                                          await stream.WriteAsync(body, ct);
                                          await stream.FlushAsync(ct);

                                          served.TrySetResult(true);
                                      });

            await using var ssl = await ConnectOfferingAsync(srv.Port, SslApplicationProtocol.Http11);

            Assert.That(ssl.NegotiatedApplicationProtocol, Is.EqualTo(SslApplicationProtocol.Http11),
                        "http/1.1 is advertised once a handler exists");

            // A single ReadAsync is not enough. The fallback above writes the header block and the
            // body with two separate calls, so they need not share a TLS record — on a loaded
            // machine the first read returns the headers alone and the body assertion below fails
            // for no reason at all. Read until the header terminator has been seen AND the declared
            // body has followed it, which is the framing this fixture exists to check anyway.
            using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var buffer            = new Byte[512];
            var received          = new List<Byte>();
            var text              = String.Empty;
            var headerEnd         = -1;

            while (headerEnd < 0 || received.Count - headerEnd - 4 < 22)
            {

                var read = await ssl.ReadAsync(buffer, readTimeout.Token);
                if (read == 0)
                    break;   // Connection: close — nothing more is coming

                received.AddRange(buffer[..read]);
                text      = Encoding.ASCII.GetString([.. received]);
                headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            }

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.StartWith("HTTP/1.1 200 OK"));
                Assert.That(text, Does.Contain("served by the fallback"));

                // The declared length must match the body actually sent — the bug in
                // the stub this replaced was a Content-Length of 39 for 38 bytes,
                // which left a client waiting for a byte that only arrived as EOF.
                Assert.That(text, Does.Contain("Content-Length: 22"));
                Assert.That(Encoding.ASCII.GetBytes("served by the fallback").Length, Is.EqualTo(22));
            });

            Assert.That(await served.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);

        }

        #endregion

        #region NoAlpnAtAll_GoesToTheFallback()

        // Over TLS, offering no ALPN means the peer is not speaking h2 (RFC 9113
        // §3.2 requires ALPN for that), so it belongs to the fallback just as much
        // as an explicit http/1.1 does.
        [Test]
        public async Task NoAlpnAtAll_GoesToTheFallback()
        {

            var reached = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var srv = await TestH2Server.StartAsync(
                                      Handle,
                                      HTTP11Fallback: async (stream, ct) =>
                                      {
                                          reached.TrySetResult(true);
                                          await stream.WriteAsync("HTTP/1.1 204 No Content\r\n\r\n"u8.ToArray(), ct);
                                          await stream.FlushAsync(ct);
                                      });

            await using var ssl = await ConnectOfferingAsync(srv.Port);   // no ALPN offered

            Assert.Multiple(() =>
            {
                Assert.That(ssl.NegotiatedApplicationProtocol.Protocol.Length, Is.EqualTo(0), "nothing negotiated");
                Assert.That(reached.Task.Wait(TimeSpan.FromSeconds(5)),        Is.True,       "handled anyway");
            });

        }

        #endregion

        #region NoAlpnAndNoHandler_IsClosed()

        // The remaining case: nothing negotiated and nothing to fall back to. The
        // connection is dropped rather than left hanging.
        [Test]
        public async Task NoAlpnAndNoHandler_IsClosed()
        {

            await using var srv = await TestH2Server.StartAsync(Handle);
            await using var ssl = await ConnectOfferingAsync(srv.Port);   // no ALPN, no handler

            var buffer = new Byte[16];
            var read   = await ssl.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(read, Is.EqualTo(0), "server closed the connection");

        }

        #endregion

    }

}
