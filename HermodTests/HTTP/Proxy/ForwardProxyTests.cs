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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Proxy
{

    /// <summary>
    /// A WebSocket through a forward proxy: asked for with CONNECT, told who is
    /// connecting in Proxy-Authorization - HTTP Basic, a one-time password, and a
    /// one-time password bound to the TLS to the proxy -, and SSH over WebSocket
    /// end to end through the tunnel. The TLS to the server runs inside it.
    /// </summary>
    /// <remarks>
    /// No certificate store is touched: the certificates live in memory and are
    /// disposed with the test.
    /// </remarks>
    [TestFixture]
    public class ForwardProxyTests
    {

        #region Data

        private static readonly TimeSpan  Patience      = TimeSpan.FromSeconds(10);

        private const           String    Login         = "CP001";

        private static readonly TOTPConfig Unbound      = new ("proxy-shared-secret-0123456789");
        private static readonly TOTPConfig Bound        = new ("proxy-shared-secret-0123456789", TLSChannelBinding: true);

        private HTTPServer?            httpServer;
        private SshWebSocketEndpoint?  endpoint;

        private readonly ISshHostKey   hostKey      = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey   userKey      = SshHostKey.GenerateEd25519();

        #endregion

        #region Setting up / tearing down

        [TearDown]
        public async Task Shutdown()
        {

            if (endpoint is not null)
                await endpoint.DisposeAsync();

            if (httpServer is not null)
                await httpServer.DisposeAsync();

            endpoint    = null;
            httpServer  = null;

        }

        #endregion

        #region (private) Helpers

        // An HTTP server with the SSH over WebSocket endpoint on /ssh, and an SSH
        // server behind it that says back every command it runs.
        private async Task<(URL URL, SshServer Server)> AnSshEndpoint()
        {

            var server  = new SshServer(new SshServerOptions {
                              HostKeys       = [ hostKey ],
                              Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                              ExecHandler    = async (ctx, ct) => {
                                                   await ctx.WriteAsync($"ran: {ctx.Command}\n", ct);
                                                   return 0;
                                               }
                          });

            httpServer  = await HTTPServer.StartNew();
            endpoint    = new SshWebSocketEndpoint((connection, ct) => server.ServeAsync(connection.Pipe, CancellationToken: ct),
                                                   RequireAuthentication: false);

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/ssh"),
                HTTPDelegate: WebSocketUpgrade.For(endpoint)
            );

            return (URL.Parse($"ws://127.0.0.1:{httpServer.TCPPort}/ssh"), server);

        }

        private SSHClientOptions AnSshClient()

            => new () {
                   Username       = User_Id.Parse("operator"),
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ userKey ]
               };

        // A self-signed certificate for the given key, for a name nobody connects to.
        private static X509Certificate2 Carrier(ECDsa Key)
        {

            var request = new CertificateRequest("CN=proxy.invalid", Key, HashAlgorithmName.SHA256);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false));

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

            return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.UserKeySet);

        }

        // Whether a Proxy-Authorization is a one-time password of this moment under
        // the login - bound to the TLS to the proxy, where Material is given.
        private static Boolean IsTheTOTP(String? ProxyAuthorization, TOTPConfig Config, Byte[]? Material)
        {

            if (ProxyAuthorization is null)
                return false;

            var (previous, current, next, _, _) = TOTPGenerator.GenerateTOTPs(
                                                      Config.SharedSecret,
                                                      Config.ValidityTime,
                                                      Config.Length,
                                                      Config.Alphabet,
                                                      null,
                                                      Material,
                                                      Config.HashAlgorithm
                                                  );

            return new[] { previous, current, next }.Any(totp => HTTPTOTPAuthentication.Create(Login, totp).HTTPText == ProxyAuthorization);

        }

        private static WebSocketClient AWebSocketClient(URL URL, HTTPProxy Proxy)

            => new (URL, ConnectTimeout: TimeSpan.FromSeconds(5)) {
                   Proxy            = Proxy,
                   ReconnectPolicy  = null
               };

        #endregion


        #region SSH_ThroughAProxy

        /// <summary>
        /// SSH over WebSocket through a proxy that wants to be told nothing: the proxy is
        /// asked for a tunnel to the endpoint, and a command runs end to end.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task SSH_ThroughAProxy(CancellationToken CancellationToken)
        {

            var (url, server) = await AnSshEndpoint();
            await using var _ = server;

            await using var proxy      = new TestForwardProxy();
            using var       webSocket  = new SshWebSocketClient(url, Proxy: new HTTPProxy(proxy.URL));
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result = await client.Execute("whoami", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,             Is.EqualTo("ran: whoami\n"));
                Assert.That(proxy.Targets.ToArray(),           Is.EqualTo(new[] { $"127.0.0.1:{httpServer!.TCPPort}" }));
                Assert.That(proxy.ProxyAuthorizations.Single(), Is.Null);
            });

        }

        #endregion

        #region Basic_IsSentToTheProxy_And407WithoutIt

        /// <summary>
        /// A proxy that wants HTTP Basic: without it the answer is its 407, with what it
        /// takes, and with it the WebSocket is opened and SSH runs through the tunnel.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Basic_IsSentToTheProxy_And407WithoutIt(CancellationToken CancellationToken)
        {

            var basic = HTTPBasicAuthentication.Create("station", "proxy-password");

            var (url, server) = await AnSshEndpoint();
            await using var _ = server;

            await using var proxy = new TestForwardProxy(Authorize: (proxyAuthorization, material) => proxyAuthorization == basic.HTTPText);

            await using (var refused = AWebSocketClient(url, new HTTPProxy(proxy.URL)))
            {

                var (connection, response) = await refused.Connect(CancellationToken: CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(connection,               Is.Null);
                    Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.ProxyAuthenticationRequired));
                    Assert.That(response.HTTPBodyAsUTF8String, Does.Contain("Basic realm=\\\"test\\\""));
                });

            }

            using var       webSocket  = new SshWebSocketClient(url, Proxy: new HTTPProxy(proxy.URL) { Authentication = basic });
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result = await client.Execute("whoami", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,                Is.EqualTo("ran: whoami\n"));
                Assert.That(proxy.ProxyAuthorizations.ToArray(),  Is.EqualTo(new String?[] { null, basic.HTTPText }));
            });

        }

        #endregion

        #region SSHWebSocketClient_SaysThe407

        /// <summary>
        /// The SSH over WebSocket client tells a refusal of the proxy as the 407 it was.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task SSHWebSocketClient_SaysThe407(CancellationToken CancellationToken)
        {

            var (url, server) = await AnSshEndpoint();
            await using var _ = server;

            await using var proxy      = new TestForwardProxy(Authorize: (proxyAuthorization, material) => false);
            using var       webSocket  = new SshWebSocketClient(url, Proxy: new HTTPProxy(proxy.URL));

            var refused = await Assert.ThrowsAsync<SshWebSocketException>(async () => await webSocket.OpenAsync(CancellationToken));

            Assert.Multiple(() => {
                Assert.That(refused.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.ProxyAuthenticationRequired));
                Assert.That(refused.FromProxy,       Is.True);
                Assert.That(refused.InnerException,  Is.InstanceOf<HTTPProxyException>());
                Assert.That(refused.Message,         Does.StartWith($"The proxy {proxy.URL}"));
            });

        }

        #endregion

        #region TOTP_IsSentToTheProxy

        /// <summary>
        /// A one-time password goes in Proxy-Authorization under its login, made anew for
        /// the connection, and the proxy lets it through.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TOTP_IsSentToTheProxy(CancellationToken CancellationToken)
        {

            var (url, server) = await AnSshEndpoint();
            await using var _ = server;

            await using var proxy      = new TestForwardProxy(Authorize: (proxyAuthorization, material) => IsTheTOTP(proxyAuthorization, Unbound, null));
            using var       webSocket  = new SshWebSocketClient(url, Proxy: new HTTPProxy(proxy.URL) { TOTPConfig = Unbound, TOTPLogin = Login });
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result = await client.Execute("whoami", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,              Is.EqualTo("ran: whoami\n"));
                Assert.That(proxy.ProxyAuthorizations.Single(), Does.StartWith("TOTP "));
            });

        }

        #endregion

        #region BoundTOTP_IsBoundToTheTLSToTheProxy

        /// <summary>
        /// A one-time password bound to the TLS to an "https" proxy whose key is pinned:
        /// the proxy that checks it with its own side of that connection lets it through,
        /// and one that checks it as an unbound password does not - read off this
        /// connection, it is no password on any other.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task BoundTOTP_IsBoundToTheTLSToTheProxy(CancellationToken CancellationToken)
        {

            using var key          = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate  = Carrier(key);

            var (url, server) = await AnSshEndpoint();
            await using var _ = server;

            HTTPProxy BoundTo(TestForwardProxy Proxy)

                => new (Proxy.URL) {
                       TOTPConfig     = Bound,
                       TOTPLogin      = Login,
                       ServerKeyPins  = KeyPinPolicy.PinnedTo(KeyPin.FromKey(key))
                   };

            await using (var unboundChecker = new TestForwardProxy(certificate, (proxyAuthorization, material) => IsTheTOTP(proxyAuthorization, Unbound, null)))
            {

                using var refused = new SshWebSocketClient(url, Proxy: BoundTo(unboundChecker));

                var refusal = await Assert.ThrowsAsync<SshWebSocketException>(async () => await refused.OpenAsync(CancellationToken));

                Assert.That(refusal.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.ProxyAuthenticationRequired));
                Assert.That(refusal.FromProxy,       Is.True);

            }

            await using var proxy      = new TestForwardProxy(certificate, (proxyAuthorization, material) => material is not null && IsTheTOTP(proxyAuthorization, Bound, material));
            using var       webSocket  = new SshWebSocketClient(url, Proxy: BoundTo(proxy));
            await using var client     = await SSHClient.Connect(webSocket, AnSshClient(), CancellationToken);

            var result = await client.Execute("whoami", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,        Is.EqualTo("ran: whoami\n"));
                Assert.That(webSocket.ProxyTLSSession,    Is.Not.Null);
                Assert.That(webSocket.ProxyTLSSession!.CanExportKeyingMaterial, Is.True);
            });

        }

        #endregion

        #region ABinding_ThatCannotBeMade_IsNotSentAtAll

        /// <summary>
        /// A one-time password that is to be bound and cannot be - a proxy without TLS, or
        /// one whose key is not pinned, where SslStream exports nothing to bind it with -
        /// is not sent weaker than configured: nothing is opened at all.
        /// </summary>
        [Test]
        public async Task ABinding_ThatCannotBeMade_IsNotSentAtAll()
        {

            await using var proxy = new TestForwardProxy();

            foreach (var configured in new[] {
                                           new HTTPProxy(URL.Parse("http://127.0.0.1:3128"))  { TOTPConfig = Bound,   TOTPLogin = Login },
                                           new HTTPProxy(URL.Parse("https://127.0.0.1:3128")) { TOTPConfig = Bound,   TOTPLogin = Login },
                                           new HTTPProxy(URL.Parse("http://127.0.0.1:3128"))  { TOTPConfig = Unbound },
                                           new HTTPProxy(URL.Parse("http://127.0.0.1:3128"))  { TOTPConfig = Unbound, TOTPLogin = Login, Authentication = HTTPBasicAuthentication.Create("a", "b") }
                                       })
            {

                await using var client = AWebSocketClient(URL.Parse("ws://127.0.0.1:1/"), configured);

                Assert.That(configured.WhyItCannotBeUsed(), Is.Not.Null);
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.Connect());

            }

            Assert.That(proxy.Targets, Is.Empty);

        }

        #endregion

        #region TheTLSToTheServer_RunsInsideTheTunnel

        /// <summary>
        /// A "wss" server through the proxy: the TLS to the server runs end to end inside
        /// the tunnel and is checked against the server's key pin, and the proxy is asked
        /// for the server's authority and sees nothing more of it.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheTLSToTheServer_RunsInsideTheTunnel(CancellationToken CancellationToken)
        {

            using var key          = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate  = Carrier(key);

            await using var server = new WebSocketMirrorServer(HTTPPort:                   IPPort.Zero,
                                                               RequireAuthentication:      false,
                                                               ServerCertificateSelector:  (tcpServer, tcpClient) => certificate,
                                                               AutoStart:                  true);

            await using var proxy  = new TestForwardProxy();

            await using var client = new WebSocketClient(
                                         URL.Parse($"wss://127.0.0.1:{server.IPPort}"),
                                         RemoteCertificateValidator:  KeyPinValidator.ForServerCertificates<IWebSocketClient>(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key))),
                                         ConnectTimeout:              TimeSpan.FromSeconds(5)
                                     ) {
                                         Proxy            = new HTTPProxy(proxy.URL),
                                         ReconnectPolicy  = null
                                     };

            var (connection, response) = await client.Connect(CancellationToken: CancellationToken).WaitAsync(Patience, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(connection,               Is.Not.Null);
                Assert.That(client.TLSSession,        Is.Not.Null);
                Assert.That(proxy.Targets.ToArray(),  Is.EqualTo(new[] { $"127.0.0.1:{server.IPPort}" }));
            });

        }

        #endregion

        #region TheProxyCannotReachTheServer_502

        /// <summary>
        /// The proxy answers 502 where it cannot reach the server, and that is what the
        /// client is told.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheProxyCannotReachTheServer_502(CancellationToken CancellationToken)
        {

            await using var proxy   = new TestForwardProxy();

            // A port nothing listens on: bound and let go again.
            var listener            = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var closedPort          = ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();

            await using var client  = AWebSocketClient(URL.Parse($"ws://127.0.0.1:{closedPort}/"), new HTTPProxy(proxy.URL));

            var (connection, response) = await client.Connect(CancellationToken: CancellationToken).WaitAsync(Patience, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(connection,               Is.Null);
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.BadGateway));
            });

        }

        #endregion

        #region HTTP3_IsNotTunnelled

        /// <summary>
        /// A WebSocket that is to be opened over HTTP/3 alone is not opened through a
        /// proxy, which tunnels TCP and not QUIC.
        /// </summary>
        [Test]
        public async Task HTTP3_IsNotTunnelled()
        {

            await using var client = AWebSocketClient(URL.Parse("wss://127.0.0.1:1/"), new HTTPProxy(URL.Parse("http://127.0.0.1:3128")));

            client.WebSocketOver = WebSocketHTTPVersion.HTTP3;

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.Connect());

        }

        #endregion

    }

}
