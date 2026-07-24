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

using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Shared helpers for the in-process HTTP/2 integration tests: a free-port
    /// allocator, a self-signed certificate factory (server or client EKU with
    /// localhost SANs), a .NET <see cref="HttpClient"/> pinned to HTTP/2 that
    /// trusts our self-signed cert, and a probe that waits until a loopback port
    /// is accepting. These mirror the boilerplate that each stand-alone harness
    /// used to carry, so the ported fixtures stay thin.
    /// </summary>
    internal static class H2
    {

        /// <summary>Accept any server certificate (self-signed test certs).</summary>
        public static readonly RemoteCertificateValidationCallback AcceptAnyServerCert = (_, _, _, _) => true;

        /// <summary>
        /// Grab a currently-free ephemeral loopback TCP port. There is a small
        /// TOCTOU window before the server rebinds it, which is acceptable for a
        /// local, sequential test run.
        /// </summary>
        public static Int32 FreePort()
        {
            var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint) l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        /// <summary>
        /// A self-signed certificate for <paramref name="CN"/> with localhost /
        /// loopback SANs and either the serverAuth (default) or clientAuth EKU.
        /// </summary>
        public static X509Certificate2 MakeCert(String CN = "localhost", Boolean ClientAuth = false)
        {

            using var rsa = RSA.Create(2048);

            var req = new CertificateRequest($"CN={CN}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            san.AddIpAddress(System.Net.IPAddress.Loopback);
            san.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
            req.CertificateExtensions.Add(san.Build());

            // clientAuth (1.3.6.1.5.5.7.3.2) or serverAuth (.3.1) EKU.
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                [new Oid(ClientAuth ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1")], false));

            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.UserKeySet);

        }

        /// <summary>
        /// A .NET <see cref="HttpClient"/> forced to HTTP/2 (exact version) that
        /// trusts our self-signed test certificate, optionally presenting a client
        /// certificate for mTLS.
        /// </summary>
        public static HttpClient MakeHttpClient(X509Certificate2? ClientCert = null)
        {

            var handler = new SocketsHttpHandler {
                SslOptions = new SslClientAuthenticationOptions {
                    RemoteCertificateValidationCallback = (_, _, _, _) => true
                }
            };

            if (ClientCert is not null)
                handler.SslOptions.ClientCertificates = [ClientCert];

            return new HttpClient(handler) {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy  = HttpVersionPolicy.RequestVersionExact,
                Timeout               = TimeSpan.FromSeconds(8)
            };

        }

        /// <summary>
        /// Poll a raw TCP connect to the loopback port until it is accepting (or
        /// time out) — a deterministic replacement for the fixed start-up delays
        /// the stand-alone harnesses used.
        /// </summary>
        public static async Task WaitUntilListeningAsync(Int32 Port, CancellationToken CancellationToken = default)
        {

            for (var i = 0; i < 100; i++)
            {
                try
                {
                    using var c = new TcpClient();
                    await c.ConnectAsync(System.Net.IPAddress.Loopback, Port, CancellationToken);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    await Task.Delay(50, CancellationToken);
                }
            }

            throw new TimeoutException($"Nothing started listening on 127.0.0.1:{Port}");

        }

        /// <summary>
        /// Poll <paramref name="condition"/> up to a timeout — used to wait for a
        /// pool to warm up / heal without a fixed sleep.
        /// </summary>
        public static async Task<Boolean> EventuallyAsync(Func<Boolean> condition, Int32 timeoutMs = 3000)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(25);
            }
            return condition();
        }

    }


    /// <summary>
    /// An <see cref="HTTP2Server"/> running on an ephemeral loopback port,
    /// started and waited-for on construction and gracefully torn down on
    /// <see cref="DisposeAsync"/>. Use with <c>await using</c> inside a test.
    /// </summary>
    internal sealed class TestH2Server : IAsyncDisposable
    {

        private readonly HTTP2Server server;
        private readonly Task        runTask;
        private          Int32       stopped;

        /// <summary>The ephemeral loopback port the server is listening on.</summary>
        public Int32 Port { get; }

        /// <summary>The server's accept loop task — completes once the listener stops.</summary>
        public Task Running => runTask;

        private TestH2Server(HTTP2Server Server, Int32 Port, Task RunTask)
        {
            this.server  = Server;
            this.Port    = Port;
            this.runTask = RunTask;
        }

        /// <summary>Gracefully stop (GOAWAY + listener teardown). Idempotent.</summary>
        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref stopped, 1) == 0)
            {
                try { await server.StopAsync(); } catch { }
            }
        }

        /// <summary>
        /// Start an HTTP2Server on a free loopback port (TLS with a fresh
        /// self-signed cert unless <paramref name="Cleartext"/>), and wait until
        /// it is accepting connections.
        /// </summary>
        public static async Task<TestH2Server> StartAsync(
            HTTP2RequestHandler    Handler,
            X509Certificate2?      Certificate               = null,
            HTTP2ConnectHandler?   ConnectHandler            = null,
            HTTP2StreamingHandler? StreamingHandler          = null,
            Boolean                RequireClientCertificate  = false,
            RemoteCertificateValidationCallback? ValidateClientCertificate = null,
            HTTP2Timeouts?         Timeouts                  = null,
            Boolean                Cleartext                 = false,
            Int64                  MaxRequestBodySize        = HTTP2Server.DefaultMaxRequestBodySize,
            Func<TlsCipherSuite, Boolean>? IsBlocklistedCipherSuite = null,
            Func<String, Boolean>? IsAuthorityServed = null,
            IEnumerable<String>?   OriginSet         = null)
        {

            var port   = H2.FreePort();
            var cert   = Cleartext ? null : (Certificate ?? H2.MakeCert());

            var server = new HTTP2Server(System.Net.IPAddress.Loopback, port, cert, Handler,
                                         ConnectHandler, RequireClientCertificate, ValidateClientCertificate,
                                         Timeouts, StreamingHandler, Cleartext, MaxRequestBodySize,
                                         IsBlocklistedCipherSuite, IsAuthorityServed, OriginSet);

            var runTask = server.RunAsync();
            await H2.WaitUntilListeningAsync(port);

            return new TestH2Server(server, port, runTask);

        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            try { await runTask; } catch { }
        }

    }


    /// <summary>
    /// A raw, frame-level HTTP/2 client over <see cref="SslStream"/> for the tests
    /// that need byte-exact control of the wire (flow-control accounting, timeout
    /// hardening, MUST-level framing details). Mirrors the hand-rolled clients the
    /// stand-alone harnesses carried.
    /// </summary>
    internal static class H2Raw
    {

        /// <summary>The RFC 9113 client connection preface.</summary>
        public static readonly Byte[] Preface = Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

        /// <summary>
        /// TCP-connect to a loopback port and complete the TLS handshake (with
        /// ALPN <c>h2</c> unless disabled). Nothing HTTP/2 is sent yet.
        /// </summary>
        public static async Task<SslStream> ConnectTlsAsync(Int32 Port, String Host = "localhost", Boolean Alpn = true, SslProtocols? Protocols = null, CancellationToken CancellationToken = default)
        {

            var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, Port, CancellationToken);

            var ssl  = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            var opts = new SslClientAuthenticationOptions { TargetHost = Host };
            if (Alpn)
                opts.ApplicationProtocols = [SslApplicationProtocol.Http2];

            // Pin the TLS version when a test needs a specific one (the RFC 9113
            // §9.2.2 cipher-suite rule only applies to TLS 1.2).
            if (Protocols.HasValue)
                opts.EnabledSslProtocols = Protocols.Value;

            await ssl.AuthenticateAsClientAsync(opts, CancellationToken);
            return ssl;

        }

        /// <summary>Read exactly <paramref name="buf"/>.Length bytes; false on EOF.</summary>
        public static async Task<Boolean> ReadExactAsync(Stream Stream, Byte[] buf, CancellationToken CancellationToken)
        {
            var off = 0;
            while (off < buf.Length)
            {
                var n = await Stream.ReadAsync(buf.AsMemory(off, buf.Length - off), CancellationToken);
                if (n == 0) return false;
                off += n;
            }
            return true;
        }

        /// <summary>Read one whole frame (9-byte header + payload); null on EOF.</summary>
        public static async Task<HTTP2Frame?> ReadFrameAsync(Stream Stream, CancellationToken CancellationToken)
        {
            var header = new Byte[9];
            if (!await ReadExactAsync(Stream, header, CancellationToken))
                return null;
            var f = HTTP2Frame.ParseHeader(header);
            if (f.Length > 0)
            {
                f.Payload = new Byte[f.Length];
                if (!await ReadExactAsync(Stream, f.Payload, CancellationToken))
                    return null;
            }
            return f;
        }

        /// <summary>
        /// Send the preface + our SETTINGS, drain the server's SETTINGS and its
        /// initial connection-window bump, and ACK the server SETTINGS. Returns the
        /// startup connection-window increment (0 if none was seen).
        /// </summary>
        public static async Task<Int64> HandshakeAsync(SslStream Ssl, CancellationToken CancellationToken)
        {

            await Ssl.WriteAsync(Preface, CancellationToken);
            await Ssl.WriteAsync(HTTP2Frame.CreateSettings().Serialize(), CancellationToken);
            await Ssl.FlushAsync(CancellationToken);

            Int64 startupConnectionBump = 0;
            var   sawSettings           = false;

            while (startupConnectionBump == 0 || !sawSettings)
            {
                var f = await ReadFrameAsync(Ssl, CancellationToken);
                if (f is null) break;

                if (f.Type == HTTP2FrameType.WINDOW_UPDATE && f.StreamId == 0)
                    startupConnectionBump = BinaryPrimitives.ReadUInt32BigEndian(f.Payload) & 0x7FFFFFFFu;

                if (f.Type == HTTP2FrameType.SETTINGS && !f.IsAck)
                {
                    sawSettings = true;
                    await Ssl.WriteAsync(HTTP2Frame.CreateSettingsAck().Serialize(), CancellationToken);
                    await Ssl.FlushAsync(CancellationToken);
                }
            }

            return startupConnectionBump;

        }

    }


    /// <summary>
    /// A raw, misbehaving HTTP/2 <em>mock server</em> for the client-robustness /
    /// connection-pool tests. Accepts any number of TLS+<c>h2</c> connections;
    /// each one does the preface handshake (advertising the given
    /// MAX_CONCURRENT_STREAMS), then hands every client frame to
    /// <c>onFrame(connectionIndex, ssl, frame, encoder)</c>, so the mock can react
    /// per-connection (refuse a stream, GOAWAY, go silent, ...). Disposed at end of
    /// test.
    /// </summary>
    internal sealed class MockH2Server : IAsyncDisposable
    {

        private readonly TcpListener             listener;
        private readonly X509Certificate2        cert = H2.MakeCert();
        private readonly CancellationTokenSource cts  = new();
        private readonly Int32                   mcs;
        private readonly Func<Int32, SslStream, HTTP2Frame, HPACKEncoder, Task> onFrame;
        private          Int32                   connIndex = -1;

        /// <summary>The ephemeral loopback port the mock is listening on.</summary>
        public Int32 Port => ((IPEndPoint) listener.LocalEndpoint).Port;

        private MockH2Server(Int32 Mcs, Func<Int32, SslStream, HTTP2Frame, HPACKEncoder, Task> OnFrame)
        {
            mcs      = Mcs;
            onFrame  = OnFrame;
            listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            _ = AcceptLoopAsync();
        }

        /// <summary>Start a mock advertising <paramref name="mcs"/> (0 = don't send MCS).</summary>
        public static MockH2Server Start(Int32 mcs, Func<Int32, SslStream, HTTP2Frame, HPACKEncoder, Task> onFrame)
            => new(mcs, onFrame);

        private async Task AcceptLoopAsync()
        {
            while (!cts.IsCancellationRequested)
            {
                TcpClient tcp;
                try { tcp = await listener.AcceptTcpClientAsync(cts.Token); }
                catch { break; }
                var idx = Interlocked.Increment(ref connIndex);
                _ = HandleConnectionAsync(idx, tcp);
            }
        }

        private async Task HandleConnectionAsync(Int32 idx, TcpClient tcp)
        {
            try
            {
                using (tcp)
                {
                    var ssl = new SslStream(tcp.GetStream(), false);
                    await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate    = cert,
                        ApplicationProtocols  = [new SslApplicationProtocol("h2")]
                    });

                    var magic = new Byte[H2Raw.Preface.Length];
                    if (!await H2Raw.ReadExactAsync(ssl, magic, cts.Token)) return;

                    await WriteFrameAsync(ssl, mcs > 0
                        ? HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS, (UInt32) mcs))
                        : HTTP2Frame.CreateSettings());

                    var enc = new HPACKEncoder();
                    while (!cts.IsCancellationRequested)
                    {
                        var f = await H2Raw.ReadFrameAsync(ssl, cts.Token);
                        if (f is null) break;
                        if (f.Type == HTTP2FrameType.SETTINGS && !f.IsAck)
                            await WriteFrameAsync(ssl, HTTP2Frame.CreateSettingsAck());
                        await onFrame(idx, ssl, f, enc);
                    }
                }
            }
            catch { /* connection ended / cancelled */ }
        }

        /// <summary>Write one frame to the mock's connection stream.</summary>
        public static async Task WriteFrameAsync(SslStream ssl, HTTP2Frame f)
        {
            await ssl.WriteAsync(f.Serialize());
            await ssl.FlushAsync();
        }

        /// <summary>Send a 200 "ok" response on a stream.</summary>
        public static async Task Respond200Async(SslStream ssl, HPACKEncoder enc, UInt32 streamId)
        {
            await WriteFrameAsync(ssl, HTTP2Frame.CreateHeaders(streamId,
                enc.EncodeHeaderBlock([(":status", "200"), ("content-length", "2")]), EndStream: false, EndHeaders: true));
            await WriteFrameAsync(ssl, HTTP2Frame.CreateData(streamId, "ok"u8.ToArray(), EndStream: true));
        }

        public ValueTask DisposeAsync()
        {
            cts.Cancel();
            try { listener.Stop(); } catch { }
            return ValueTask.CompletedTask;
        }

    }

}
