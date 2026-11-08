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

using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.Argus;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;
using org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.UDP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Resolving
{

    /// <summary>
    /// Every client of Hermod finds the address of a host the same way: an IP address is itself, "localhost"
    /// the loopback, and a host name what the DNS client it was given says - never the resolver of the
    /// operating system.
    /// </summary>
    /// <remarks>
    /// Each client is sent to a name below .invalid, which no resolver of an operating system answers
    /// (RFC 6761 §6.4): it reaches its server only through the stand-in DNS client, which points it at the
    /// loopback. Without a DNS client, the name is refused.
    /// </remarks>
    [TestFixture]
    public class NameResolutionTests
    {

        #region (private) Helpers

        /// <summary>
        /// A stand-in DNS client that names the loopback for every host.
        /// </summary>
        private static FakeDNSClient Loopback()
        {
            var dnsClient = new FakeDNSClient();
            dnsClient.IPv4Addresses.Add(IPv4Address.Localhost);
            return dnsClient;
        }

        /// <summary>
        /// The names a stand-in DNS client was asked for, without the trailing dot.
        /// </summary>
        private static IEnumerable<String> AskedFor(FakeDNSClient DNSClient)
            => DNSClient.QueriedNames.Select(name => name.FullName.TrimEnd('.')).Distinct();

        /// <summary>
        /// A TCP listener on the loopback that takes one connection, reads what comes first, and answers it.
        /// </summary>
        private sealed class OneConnection : IDisposable
        {

            private readonly TcpListener listener = new (System.Net.IPAddress.Loopback, 0);

            public UInt16        Port      { get; }
            public Task<String>  Received  { get; }

            public OneConnection(String Answer = "")
            {

                listener.Start();
                Port      = (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

                Received  = Task.Run(async () => {

                                using var connection = await listener.AcceptTcpClientAsync();
                                using var stream     = connection.GetStream();

                                if (Answer.Length > 0 && !Answer.StartsWith("HTTP/"))
                                    await stream.WriteAsync(Encoding.ASCII.GetBytes(Answer));

                                var buffer = new Byte[4096];
                                using var reading = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                var read = 0;
                                try { read = await stream.ReadAsync(buffer, reading.Token); } catch (OperationCanceledException) { }

                                if (Answer.StartsWith("HTTP/"))
                                    await stream.WriteAsync(Encoding.ASCII.GetBytes(Answer));

                                return Encoding.ASCII.GetString(buffer, 0, read);

                            });

            }

            public async Task<Boolean> WasConnectedToAsync()
                => await Task.WhenAny(Received, Task.Delay(TimeSpan.FromSeconds(10))) == Received;

            public void Dispose()
                => listener.Stop();

        }

        #endregion


        #region NameResolution itself

        [Test]
        public async Task AnIPAddressIsItself()
        {

            Assert.That((await NameResolution.ResolveAsync("127.0.0.1", null)).Select(address => address.ToString()), Is.EqualTo(new[] { "127.0.0.1" }));
            Assert.That((await NameResolution.ResolveAsync("[::1]",     null)).Select(address => address.ToString()), Is.EqualTo(new[] { "::1" }));
            Assert.That((await NameResolution.ResolveAsync("localhost", null)).Select(address => address.ToString()), Is.EqualTo(new[] { "127.0.0.1", "::1" }));

        }

        [TestCase("fe80::1%14")]
        [TestCase("[fe80::1%14]")]
        [TestCase("[fe80::1%2514]", Description = "as a URL has it")]
        public async Task ALinkLocalAddressKeepsItsZone(String Host)
        {

            var address = (await NameResolution.ResolveAsync(Host, null)).Single();

            Assert.That(address.IsIPv6LinkLocal, Is.True);
            Assert.That(address.ScopeId,         Is.EqualTo(14));

        }

        [Test]
        public async Task AHostNameWithoutADNSClientIsRefused()
        {

            var refused = await Assert.ThrowsAsync<NameResolutionException>(async () => await NameResolution.ResolveAsync("csms.example.org", null));

            Assert.That(refused!.Host,    Is.EqualTo("csms.example.org"));
            Assert.That(refused.Message,  Does.Contain("no DNS client"));

        }

        [Test]
        public async Task AHostNameIsAskedOfTheDNSClient()
        {

            var dnsClient = new FakeDNSClient();

            dnsClient.IPv4Addresses.Add(IPv4Address.Parse("192.0.2.7"));
            dnsClient.IPv6Addresses.Add(IPv6Address.Parse("2001:db8::7"));

            var addresses = await NameResolution.ResolveAsync("csms.example.org", dnsClient);

            Assert.That(addresses.Select(address => address.ToString()), Is.EqualTo(new[] { "2001:db8::7", "192.0.2.7" }), "Not the DNS client's addresses, IPv6 first.");
            Assert.That(AskedFor(dnsClient),                              Is.EqualTo(new[] { "csms.example.org" }));

        }

        [Test]
        public async Task ANameWithoutAnAddressSaysSo()
        {

            var refused = await Assert.ThrowsAsync<NameResolutionException>(async () => await NameResolution.ResolveAsync("csms.example.org", new FakeDNSClient()));

            Assert.That(refused!.Message, Does.Contain("no address"));

        }

        [Test]
        public async Task SSHsRefusalIsOneOfNameResolution()
        {

            var refused = await Assert.ThrowsAsync<SshNameResolutionException>(async () => await SshTcp.ResolveAsync("csms.example.org", null));

            Assert.That(refused, Is.InstanceOf<NameResolutionException>());

        }

        [Test]
        public async Task ATCPClientIsConnectedThroughTheDNSClient()
        {

            using var server    = new OneConnection();
            using var client    = new TcpClient();
            var       dnsClient = Loopback();

            await NameResolution.ConnectAsync(client, "csms.invalid", server.Port, dnsClient);

            Assert.That(client.Connected,                  Is.True);
            Assert.That(AskedFor(dnsClient),               Is.EqualTo(new[] { "csms.invalid" }));

            using var unresolved = new TcpClient();
            await Assert.ThrowsAsync<NameResolutionException>(async () => await NameResolution.ConnectAsync(unresolved, "csms.invalid", server.Port, null));

        }

        #endregion

        #region HTTP/2

        [Test]
        [CancelAfter(20000)]
        public async Task AnHTTP2ClientDialsAHostNameThroughItsDNSClient(CancellationToken CancellationToken)
        {

            await using var server = await TestH2Server.StartAsync(
                                               (stream, headers, body, ct) => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                                                                                  ([ (":status", "200") ], Encoding.UTF8.GetBytes("here"))
                                                                              ),
                                               Cleartext: true
                                           );

            var dnsClient   = Loopback();
            var connection  = await HTTP2Client.ConnectAsync("h2.invalid", server.Port, Options: new HTTP2ClientOptions { DNSClient = dnsClient }, Cleartext: true, CancellationToken: CancellationToken);

            try
            {
                var response = await connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, $"h2.invalid:{server.Port}", "/");
                Assert.That(Encoding.UTF8.GetString(response.Body), Is.EqualTo("here"));
            }
            finally
            {
                await connection.CloseAsync();
            }

            Assert.That(AskedFor(dnsClient), Is.EqualTo(new[] { "h2.invalid" }));

            await Assert.ThrowsAsync<NameResolutionException>(async () =>
                await HTTP2Client.ConnectAsync("h2.invalid", server.Port, Cleartext: true, CancellationToken: CancellationToken));

        }

        #endregion

        #region HTTP/3

        [Test]
        [CancelAfter(20000)]
        public async Task AnHTTP3ClientDialsAHostNameThroughItsDNSClient(CancellationToken CancellationToken)
        {

            using var certificate = ServerCertificate.CreateSelfSigned("h3.invalid");
            var validation        = new CertificateValidationOptions { CustomTrustRoots = [ certificate.Certificate ] };

            await using var server = new Http3Server(certificate, request => new Http3Response { Status = 200, Body = Encoding.UTF8.GetBytes("here") }, port: 0);
            server.Start();

            var dnsClient = Loopback();

            await using (var client = new Http3Client("h3.invalid", server.Port, validation, dnsClient: dnsClient))
            {
                await client.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken);
                Assert.That((await client.GetAsync("/", CancellationToken)).BodyText, Is.EqualTo("here"));
            }

            Assert.That(AskedFor(dnsClient), Is.EqualTo(new[] { "h3.invalid" }));

            await using var unresolved = new Http3Client("h3.invalid", server.Port, validation);
            await Assert.ThrowsAsync<NameResolutionException>(async () => await unresolved.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken));

        }

        #endregion

        #region SunSpec Modbus

        [Test]
        public async Task AModbusClientDialsAHostNameThroughItsDNSClient()
        {

            using var server    = new OneConnection();
            var       dnsClient = Loopback();

            await using (var client = await ModbusTCPClient.ConnectAsync("meter.invalid", server.Port, dnsClient, TimeSpan.FromSeconds(5), CancellationToken.None))
            {
                Assert.That(client.IsTLS, Is.False);
            }

            Assert.That(await server.WasConnectedToAsync(), Is.True);
            Assert.That(AskedFor(dnsClient),                Is.EqualTo(new[] { "meter.invalid" }));

            await Assert.ThrowsAsync<NameResolutionException>(async () =>
                await ModbusTCPClient.ConnectAsync("meter.invalid", server.Port, null, TimeSpan.FromSeconds(5), CancellationToken.None));

        }

        #endregion

        #region UDP

        [Test]
        public async Task AUDPSenderSendsToAHostNameThroughItsDNSClient()
        {

            using var receiver  = new UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            var       port      = IPPort.Parse((UInt16) ((System.Net.IPEndPoint) receiver.Client.LocalEndPoint!).Port);
            var       dnsClient = Loopback();

            using var sender    = await UDPSender.ResolveAsync("syslog.invalid", port, dnsClient);

            sender.Send(Encoding.ASCII.GetBytes("here"));

            var received = await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(Encoding.ASCII.GetString(received.Buffer), Is.EqualTo("here"));
            Assert.That(AskedFor(dnsClient),                        Is.EqualTo(new[] { "syslog.invalid" }));

            Assert.Throws<ArgumentException>(() => new UDPSender("syslog.invalid", port), "A host name was resolved some other way.");
            await Assert.ThrowsAsync<NameResolutionException>(async () => await UDPSender.ResolveAsync("syslog.invalid", port, null));

        }

        #endregion

        #region SMTP

        [Test]
        public async Task PublicAddressesAreTheDNSClients()
        {

            var dnsClient = new FakeDNSClient();
            dnsClient.IPv4Addresses.Add(IPv4Address.Parse("192.0.2.25"));

            Assert.That((await PublicAddresses.ResolveAsync("mx.invalid",    dnsClient)).Select(address => address.ToString()), Is.EqualTo(new[] { "192.0.2.25" }));
            Assert.That((await PublicAddresses.ResolveAsync("[192.0.2.26]",  null)).     Select(address => address.ToString()), Is.EqualTo(new[] { "192.0.2.26" }));

            await Assert.ThrowsAsync<NameResolutionException>(async () => await PublicAddresses.ResolveAsync("mx.invalid", null));

        }

        [Test]
        [CancelAfter(20000)]
        public async Task AnSMTPRelayDialsItsMXThroughItsDNSClient(CancellationToken CancellationToken)
        {

            // No MX record: the domain is its own MX host (RFC 5321 §5.1), and only the DNS client knows it.
            using var server    = new OneConnection("554 not here\r\n");
            var       dnsClient = Loopback();

            var relay  = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname     = "relay.hermod.test",
                                                    ConnectTimeoutMs  = 3_000,
                                                    ReadTimeoutMs     = 3_000,
                                                    MxPort            = server.Port
                                                },
                                                null,
                                                dnsClient,
                                                new QuietSMTPLogger());

            await relay.SendAsync("mx.invalid", "sender@client.example", [ "you@mx.invalid" ], "Subject: here\r\n\r\nhello\r\n");

            Assert.That(await server.WasConnectedToAsync(), Is.True, "The MX host was not reached through the DNS client.");
            Assert.That(AskedFor(dnsClient),                Does.Contain("mx.invalid"));

        }

        private sealed class QuietSMTPLogger : org.GraphDefined.Vanaheimr.Hermod.SMTP.ILogger
        {
            public void Log(org.GraphDefined.Vanaheimr.Hermod.SMTP.LogLevel level, String message) { }
        }

        [Test]
        public async Task AnMTASTSPolicyHostIsReachedThroughTheDNSClient()
        {

            using var server    = new OneConnection("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
            var       dnsClient = Loopback();

            using (var http = new HttpClient(MtaStsResolver.CreateDefaultHandler(dnsClient)))
            {
                Assert.That(await http.GetStringAsync($"http://mta-sts.mx.invalid:{server.Port}/"), Is.EqualTo("ok"));
            }

            Assert.That(AskedFor(dnsClient), Is.EqualTo(new[] { "mta-sts.mx.invalid" }));

            using var handler = (SocketsHttpHandler) MtaStsResolver.CreateDefaultHandler(dnsClient);
            Assert.That(handler.UseProxy,         Is.False, "A proxy's name would be the system's to look up.");
            Assert.That(handler.ConnectCallback,  Is.Not.Null);

            using var unresolved = new HttpClient(MtaStsResolver.CreateDefaultHandler(null));
            var refused = await Assert.ThrowsAsync<HttpRequestException>(async () => await unresolved.GetStringAsync($"http://mta-sts.mx.invalid:{server.Port}/"));
            Assert.That(refused!.InnerException, Is.InstanceOf<NameResolutionException>());

        }

        #endregion

        #region TLS warden

        [Test]
        [CancelAfter(20000)]
        public async Task TheTLSWardenChecksAHostNameThroughItsDNSClient(CancellationToken CancellationToken)
        {

            using var smtp      = new OneConnection("554 not here\r\n");
            using var https     = new OneConnection();
            var       dnsClient = Loopback();

            await TLS_Warden.Check(new Check($"smtp://mail.invalid:{smtp.Port}"),   dnsClient, CancellationToken);
            await TLS_Warden.Check(new Check($"https://web.invalid:{https.Port}/"), dnsClient, CancellationToken);

            Assert.That(await smtp. WasConnectedToAsync(), Is.True, "The SMTP server was not reached through the DNS client.");
            Assert.That(await https.WasConnectedToAsync(), Is.True, "The HTTPS server was not reached through the DNS client.");
            Assert.That(AskedFor(dnsClient),                Is.EquivalentTo(new[] { "mail.invalid", "web.invalid" }));

            var unresolved = await TLS_Warden.Check(new Check($"smtp://mail.invalid:{smtp.Port}"), null, CancellationToken);
            Assert.That(unresolved.ErrorMessages, Has.Some.Contains("no DNS client"));

        }

        #endregion

        #region HTTPS monitor

        [Test]
        [CancelAfter(20000)]
        public async Task TheHTTPSMonitorReachesAHostNameThroughItsDNSClient(CancellationToken CancellationToken)
        {

            var logDirectory = Path.Combine(Path.GetTempPath(), "hermod-argus-" + Guid.NewGuid().ToString("N"));

            try
            {

                using var server    = new OneConnection("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
                var       dnsClient = Loopback();

                var (status, error) = await MeasureOnce(URL.Parse($"http://monitor.invalid:{server.Port}/"), logDirectory, dnsClient, CancellationToken);

                Assert.That(await server.WasConnectedToAsync(), Is.True, "The monitored server was not reached through the DNS client.");
                Assert.That(await server.Received,              Does.Contain("Host: monitor.invalid"));
                Assert.That(status,                Is.EqualTo(200), error);
                Assert.That(AskedFor(dnsClient),   Is.EqualTo(new[] { "monitor.invalid" }));

                var (_, unresolved) = await MeasureOnce(URL.Parse($"http://monitor.invalid:{server.Port}/"), logDirectory, null, CancellationToken);
                Assert.That(unresolved, Does.Contain("no DNS client"));

            }
            finally
            {
                if (Directory.Exists(logDirectory))
                    Directory.Delete(logDirectory, recursive: true);
            }

        }

        private static async Task<(Int32 Status, String Error)> MeasureOnce(URL                URL,
                                                                            String             LogDirectory,
                                                                            IDNSClient?        DNSClient,
                                                                            CancellationToken  CancellationToken)
        {

            using var done    = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            var       result  = (0, "");

            var monitor = new HTTPSMonitor(
                              "test",
                              URL,
                              new MeasurementStore(LogDirectory),
                              OnMeasurement: (measurement, name, url, httpStatusCode, errorMessage, diagnostics) => {
                                                 result = (measurement.StatusCode, measurement.Error ?? "");
                                                 done.Cancel();
                                                 return Task.CompletedTask;
                                             },
                              DNSClient: DNSClient
                          );

            await monitor.RunAsync(TimeSpan.FromHours(1), done.Token);

            return result;

        }

        #endregion

    }

}
