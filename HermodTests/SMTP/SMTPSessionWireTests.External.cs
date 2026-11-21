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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// AUTH EXTERNAL (RFC 4422 Appendix A) by the client certificate of the TLS session: the
    /// server asks for one in the handshake, offers EXTERNAL once it has one, and authenticates
    /// the account whose pinned thumbprint it is. The finding S-27 of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        #region Setup

        /// <summary>
        /// A client certificate with its private key, as SslStream can send it.
        /// </summary>
        private static X509Certificate2 ClientCertificate(String CommonName)
        {

            using var rsa  = RSA.Create(2048);
            var request    = new CertificateRequest($"CN={CommonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.2") ], critical: false));

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

            return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);

        }

        /// <summary>
        /// A server with a certificate, and carol's account, opened by the given thumbprint.
        /// </summary>
        private static Server ServerWithCertificateUser(String Thumbprint)

            => new (Configure: config => {

                        // username:password_sha256:scram_salt:scram_stored_key:scram_server_key:iterations:cert_thumbprints
                        File.WriteAllText(Path.Combine(config.MailStoragePath, "users.txt"), $"carol::::::{Thumbprint}\n");

                        return WithServerCertificate(config);

                    });

        /// <summary>
        /// STARTTLS with the given client certificate, or none, and the EHLO after it.
        /// </summary>
        private static async Task<List<String>> StartTlsWith(Wire wire, X509Certificate2? Certificate)
        {

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("STARTTLS"),            Is.EqualTo(220));

            await wire.StartTlsAsync(ClientCertificate: Certificate);

            var (code, lines) = await wire.CommandLinesAsync("EHLO client.example");
            Assert.That(code, Is.EqualTo(250));

            return lines;

        }

        private static Boolean OffersExternal(List<String> EhloLines)

            => EhloLines.Any(line => line.Contains("AUTH ", StringComparison.OrdinalIgnoreCase) &&
                                     line.Split(' ').Contains("EXTERNAL", StringComparer.OrdinalIgnoreCase));

        #endregion


        [Test(Description = "RFC 4422 Appendix A: EXTERNAL authenticates by \"some external means\" - the client certificate of the TLS session, which a client sends only when the server asks for one (RFC 8446 §4.3.2, §4.4.2): the server asks, offers EXTERNAL, and authenticates the account the certificate is pinned to")]
        public async Task A_listed_client_certificate_authenticates_with_EXTERNAL()
        {

            using var certificate  = ClientCertificate("carol");
            await using var server = ServerWithCertificateUser(certificate.Thumbprint);
            using var wire         = await server.ConnectAsync();

            Assert.That(OffersExternal(await StartTlsWith(wire, certificate)), Is.True, "EXTERNAL is offered once there is a client certificate");
            Assert.That(await wire.CommandAsync("AUTH EXTERNAL ="),            Is.EqualTo(235));

        }


        [Test(Description = "RFC 4422 Appendix A: EXTERNAL with no response sent in the AUTH command - the empty response after the 334")]
        public async Task EXTERNAL_with_the_empty_response_after_the_challenge_authenticates()
        {

            using var certificate  = ClientCertificate("carol");
            await using var server = ServerWithCertificateUser(certificate.Thumbprint);
            using var wire         = await server.ConnectAsync();

            await StartTlsWith(wire, certificate);

            Assert.That(await wire.CommandAsync("AUTH EXTERNAL"), Is.EqualTo(334));
            Assert.That(await wire.CommandAsync("="),             Is.EqualTo(235));

        }


        [Test(Description = "A certificate no account is pinned to opens none - offered EXTERNAL, refused")]
        public async Task An_unlisted_client_certificate_is_refused()
        {

            using var listed       = ClientCertificate("carol");
            using var unlisted     = ClientCertificate("carol");
            await using var server = ServerWithCertificateUser(listed.Thumbprint);
            using var wire         = await server.ConnectAsync();

            await StartTlsWith(wire, unlisted);

            Assert.That(await wire.CommandAsync("AUTH EXTERNAL ="), Is.EqualTo(535));

        }


        [Test(Description = "The certificate is asked for, not required: a client without one completes the handshake, is not offered EXTERNAL, and is refused it")]
        public async Task A_client_without_a_certificate_completes_the_handshake_without_EXTERNAL()
        {

            using var certificate  = ClientCertificate("carol");
            await using var server = ServerWithCertificateUser(certificate.Thumbprint);
            using var wire         = await server.ConnectAsync();

            Assert.That(OffersExternal(await StartTlsWith(wire, null)), Is.False);
            Assert.That(await wire.CommandAsync("AUTH EXTERNAL ="),     Is.EqualTo(535));
            Assert.That(await wire.CommandAsync("NOOP"),                Is.EqualTo(250));

        }

    }

}
