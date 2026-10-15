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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The certificate an SMTP server presents is asked for at every handshake
    /// (SMTPServerConfig.ServerCertificateSelector): a certificate renewed while
    /// the server runs is presented from the next connection on, the name the
    /// client asked for picks it, and while there is none, STARTTLS is not
    /// offered. And a certificate as certbot writes it - fullchain.pem and
    /// privkey.pem, RSA or ECDSA - is read as it is.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        #region (private static) Certificates

        /// <summary>
        /// A self-signed server certificate with its key, usable by SslStream on
        /// every platform (through PKCS#12, or the key stays ephemeral and
        /// SChannel refuses it).
        /// </summary>
        private static X509Certificate2 SelfSigned(String Name)
        {

            using var rsa         = RSA.Create(2048);
            var request           = new CertificateRequest($"CN={Name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

            return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);

        }

        /// <summary>
        /// A self-signed certificate and its key as PEM text, as certbot writes
        /// them: the key PKCS#8 ("PRIVATE KEY"), RSA or ECDSA - certbot's
        /// default since 2.0.
        /// </summary>
        private static (String CertificatePem, String KeyPem) SelfSignedPem(String   Name,
                                                                           Boolean  Ecdsa)
        {

            if (Ecdsa)
            {
                using var ecdsa       = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var request           = new CertificateRequest($"CN={Name}", ecdsa, HashAlgorithmName.SHA256);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
                return (certificate.ExportCertificatePem(), ecdsa.ExportPkcs8PrivateKeyPem());
            }

            using var rsa             = RSA.Create(2048);
            var rsaRequest            = new CertificateRequest($"CN={Name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var rsaCertificate  = rsaRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
            return (rsaCertificate.ExportCertificatePem(), rsa.ExportPkcs8PrivateKeyPem());

        }

        private static UInt16 UnusedPort()
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = (UInt16) ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<X509Certificate2?> StartTlsAsync(Server  Server,
                                                                  String  TargetHost = "mx.hermod.test")
        {

            using var wire = await Server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("STARTTLS"),            Is.EqualTo(220));

            var certificate = await wire.StartTlsAsync(TargetHost);

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250), "and the session goes on inside TLS");

            return certificate;

        }

        #endregion


        [Test]
        public async Task A_certificate_renewed_while_the_server_runs_is_presented_from_the_next_handshake()
        {

            using var first   = SelfSigned("first.hermod.test");
            using var second  = SelfSigned("second.hermod.test");

            var current       = new ServerCertificateChain(first);

            await using var server = new Server(Configure: config => config with { ServerCertificateSelector = _ => current });

            Assert.That((await StartTlsAsync(server))?.Subject, Is.EqualTo("CN=first.hermod.test"));

            current = new ServerCertificateChain(second);

            Assert.That((await StartTlsAsync(server))?.Subject, Is.EqualTo("CN=second.hermod.test"), "no restart, no new server");

        }

        [Test]
        public async Task The_name_the_client_asks_for_picks_the_certificate()
        {

            using var one    = SelfSigned("mx.one.test");
            using var two    = SelfSigned("mx.two.test");

            var asked        = new List<String?>();

            await using var server = new Server(Configure: config => config with {
                                                                    ServerCertificateSelector = name => {
                                                                        lock (asked)
                                                                            asked.Add(name);
                                                                        return new ServerCertificateChain(name == "mx.two.test" ? two : one);
                                                                    }
                                                                });

            Assert.That((await StartTlsAsync(server, "mx.two.test"))?.Subject, Is.EqualTo("CN=mx.two.test"));
            Assert.That((await StartTlsAsync(server, "mx.one.test"))?.Subject, Is.EqualTo("CN=mx.one.test"));

            Assert.That(asked, Does.Contain("mx.two.test").And.Contain("mx.one.test"), "with the name from the TLS handshake (SNI)");

        }

        [Test]
        public async Task Without_a_certificate_STARTTLS_is_not_offered_until_one_is_there()
        {

            using var later  = SelfSigned("later.hermod.test");

            ServerCertificateChain? current = null;

            await using var server = new Server(Configure: config => config with { ServerCertificateSelector = _ => current });

            using (var wire = await server.ConnectAsync())
            {

                var ehlo = await wire.CommandLinesAsync("EHLO client.example");

                Assert.That(ehlo.Code,                          Is.EqualTo(250));
                Assert.That(ehlo.Lines,                         Has.None.EqualTo("STARTTLS"));
                Assert.That(await wire.CommandAsync("STARTTLS"), Is.EqualTo(454), "4.7.0 TLS not available");
                Assert.That(await wire.CommandAsync("NOOP"),     Is.EqualTo(250), "and the session goes on in clear");

            }

            current = new ServerCertificateChain(later);

            using (var wire = await server.ConnectAsync())
            {
                Assert.That((await wire.CommandLinesAsync("EHLO client.example")).Lines, Has.Some.EqualTo("STARTTLS"), "offered once there is one");
            }

            Assert.That((await StartTlsAsync(server))?.Subject, Is.EqualTo("CN=later.hermod.test"));

        }

        [Test]
        public async Task Implicit_TLS_presents_what_the_selector_answers()
        {

            using var certificate  = SelfSigned("smtps.hermod.test");
            var implicitTlsPort    = UnusedPort();

            await using var server = new Server(Configure: config => config with {
                                                                    EnableImplicitTls          = true,
                                                                    ImplicitTlsPort            = implicitTlsPort,
                                                                    ServerCertificateSelector  = _ => new ServerCertificateChain(certificate)
                                                                });

            using var wire         = new Wire(new TcpClient("127.0.0.1", implicitTlsPort));

            Assert.That((await wire.StartTlsAsync())?.Subject,          Is.EqualTo("CN=smtps.hermod.test"), "TLS from the first byte");
            Assert.That((await wire.ReplyAsync()).Code,                 Is.EqualTo(220),                    "and the greeting inside it");
            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

        }

        [TestCase(false, TestName = "A_certificate_as_certbot_writes_it_is_read_RSA")]
        [TestCase(true,  TestName = "A_certificate_as_certbot_writes_it_is_read_ECDSA")]
        public async Task A_certificate_as_certbot_writes_it_is_read(Boolean Ecdsa)
        {

            var (certificatePem, keyPem) = SelfSignedPem("pem.hermod.test", Ecdsa);

            await using var server = new Server(Configure: config => {

                                         var fullchain  = Path.Combine(config.MailStoragePath, "fullchain.pem");
                                         var privkey    = Path.Combine(config.MailStoragePath, "privkey.pem");

                                         File.WriteAllText(fullchain, certificatePem);
                                         File.WriteAllText(privkey,   keyPem);

                                         return config with {
                                                    CertificatePath     = fullchain,
                                                    CertificateKeyPath  = privkey
                                                };

                                     });

            Assert.That((await StartTlsAsync(server))?.Subject, Is.EqualTo("CN=pem.hermod.test"));

        }

    }

}
