/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Proxy
{

    /// <summary>
    /// A forward proxy for the tests: it takes CONNECT and nothing else, over TCP or -
    /// given a certificate - over TLS 1.3 by the TLS stack that exports keying material,
    /// asks its authorizer about Proxy-Authorization with that material, and then
    /// passes bytes on both ways, unread.
    /// </summary>
    internal sealed class TestForwardProxy : IAsyncDisposable
    {

        #region Data

        private readonly TcpListener                         listener;
        private readonly CancellationTokenSource             stopping  = new ();
        private readonly X509Certificate2?                   certificate;
        private readonly Func<String?, Byte[]?, Boolean>     authorize;
        private readonly Task                                accepting;
        private readonly ConcurrentDictionary<Task, Byte>    tunnels   = new ();

        #endregion

        #region Properties

        /// <summary>
        /// The authority of every CONNECT, as it was asked for.
        /// </summary>
        public ConcurrentQueue<String>   Targets               { get; } = new ();

        /// <summary>
        /// The Proxy-Authorization of every CONNECT, or null where it had none.
        /// </summary>
        public ConcurrentQueue<String?>  ProxyAuthorizations   { get; } = new ();

        /// <summary>
        /// The port the proxy listens on.
        /// </summary>
        public IPPort                    Port                  { get; }

        /// <summary>
        /// The proxy's URL: "https" with a certificate, "http" without.
        /// </summary>
        public URL                       URL
            => URL.Parse($"{(certificate is not null ? "https" : "http")}://127.0.0.1:{Port}");

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A forward proxy on a port of its own of 127.0.0.1.
        /// </summary>
        /// <param name="Certificate">The proxy's certificate, for TLS to the proxy; none for a plain TCP connection.</param>
        /// <param name="Authorize">Says whether a CONNECT with the given Proxy-Authorization, and keying material of the TLS to the proxy, is let through; all are by default.</param>
        public TestForwardProxy(X509Certificate2?                 Certificate  = null,
                                Func<String?, Byte[]?, Boolean>?  Authorize    = null)
        {

            this.certificate  = Certificate;
            this.authorize    = Authorize ?? ((_, _) => true);

            listener          = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            Port              = IPPort.Parse(((IPEndPoint) listener.LocalEndpoint).Port);
            accepting         = AcceptAsync();

        }

        #endregion


        #region (private) AcceptAsync()

        private async Task AcceptAsync()
        {
            try
            {
                while (!stopping.IsCancellationRequested)
                {

                    var client  = await listener.AcceptTcpClientAsync(stopping.Token);
                    var tunnel  = Task.Run(() => ServeAsync(client));

                    tunnels.TryAdd(tunnel, 0);
                    _ = tunnel.ContinueWith(done => tunnels.TryRemove(done, out _), TaskScheduler.Default);

                }
            }
            catch (OperationCanceledException)
            { }
            catch (ObjectDisposedException)
            { }
            catch (SocketException)
            { }
        }

        #endregion

        #region (private) ServeAsync(Client)

        private async Task ServeAsync(TcpClient Client)
        {

            using var client   = Client;
            var       stream   = (Stream) client.GetStream();
            Byte[]?   material = null;

            try
            {

                if (certificate is not null)
                {

                    var session = await BouncyCastleTLS.AuthenticateAsServerAsync(
                                            stream,
                                            new RawPublicKeyTLSServerParameters {
                                                ServerCertificate     = certificate,
                                                ApplicationProtocols  = [ "http/1.1" ]
                                            },
                                            LeaveInnerStreamOpen:  false,
                                            CancellationToken:     stopping.Token
                                        );

                    stream = session.Stream;

                    if (session.CanExportKeyingMaterial)
                        material = session.ExportKeyingMaterial(TOTPGenerator.TLSExporterLabel, [], TOTPGenerator.TLSExporterLength);

                }

                var header              = await ReadHeaderAsync(stream);
                var lines               = header.Split("\r\n");
                var requestLine         = lines[0].Split(' ');
                var proxyAuthorization  = lines.Skip(1).
                                                Select (line  => line.Split(':', 2)).
                                                Where  (field => field.Length == 2 && field[0].Trim().Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)).
                                                Select (field => field[1].Trim()).
                                                FirstOrDefault();

                if (requestLine.Length != 3 || requestLine[0] != "CONNECT")
                {
                    await WriteAsync(stream, "HTTP/1.1 405 Method Not Allowed\r\nAllow: CONNECT\r\nContent-Length: 0\r\n\r\n");
                    return;
                }

                Targets.            Enqueue(requestLine[1]);
                ProxyAuthorizations.Enqueue(proxyAuthorization);

                if (!authorize(proxyAuthorization, material))
                {
                    await WriteAsync(stream, "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"test\"\r\nProxy-Authenticate: TOTP\r\nContent-Length: 0\r\n\r\n");
                    return;
                }

                var separator  = requestLine[1].LastIndexOf(':');
                var host       = requestLine[1][..separator].Trim('[', ']');
                var port       = Int32.Parse(requestLine[1][(separator + 1)..]);

                using var upstream = new TcpClient();

                try
                {
                    await upstream.ConnectAsync(host, port, stopping.Token);
                }
                catch (SocketException)
                {
                    await WriteAsync(stream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n");
                    return;
                }

                await WriteAsync(stream, "HTTP/1.1 200 Connection established\r\n\r\n");

                var server = upstream.GetStream();

                await Task.WhenAny(
                          stream.CopyToAsync(server, stopping.Token),
                          server.CopyToAsync(stream, stopping.Token)
                      );

            }
            catch (Exception)
            {
                // A tunnel ends however its ends end.
            }
            finally
            {
                try { await stream.DisposeAsync(); } catch { }
            }

        }

        #endregion

        #region (private static) ReadHeaderAsync(Stream) / WriteAsync(Stream, Text)

        private static async Task<String> ReadHeaderAsync(Stream Stream)
        {

            var bytes = new List<Byte>();
            var one   = new Byte[1];

            while (bytes.Count < 16 * 1024)
            {

                if (await Stream.ReadAsync(one) == 0)
                    throw new IOException("The client closed the connection before its request was complete.");

                bytes.Add(one[0]);

                if (bytes.Count >= 4 && bytes[^4] == '\r' && bytes[^3] == '\n' && bytes[^2] == '\r' && bytes[^1] == '\n')
                    return Encoding.ASCII.GetString([.. bytes.Take(bytes.Count - 4)]);

            }

            throw new IOException("The request header is too long.");

        }

        private static async Task WriteAsync(Stream Stream, String Text)
        {
            await Stream.WriteAsync(Encoding.ASCII.GetBytes(Text));
            await Stream.FlushAsync();
        }

        #endregion


        #region DisposeAsync()

        public async ValueTask DisposeAsync()
        {

            stopping.Cancel();
            listener.Stop();

            try { await accepting; } catch { }

            try
            {
                await Task.WhenAll(tunnels.Keys).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            { }

            stopping.Dispose();

        }

        #endregion

    }

}
