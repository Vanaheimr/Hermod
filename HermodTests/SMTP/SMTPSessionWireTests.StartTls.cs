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
    /// After STARTTLS the server discards what it knows from the client that it did not
    /// learn from the TLS negotiation itself (RFC 3207 §4.2): the cleartext part of the
    /// session may have been written by someone on the path.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// A self-signed server certificate in the mail storage directory, which makes the
        /// server offer STARTTLS.
        /// </summary>
        private static SMTPServerConfig WithServerCertificate(SMTPServerConfig config)
        {

            var pfx = Path.Combine(config.MailStoragePath, "server.pfx");

            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=mx.hermod.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
                File.WriteAllBytes(pfx, certificate.Export(X509ContentType.Pfx, "test"));
            }

            return config with {
                       CertificatePath      = pfx,
                       CertificatePassword  = "test"
                   };

        }


        [Test]
        public async Task STARTTLS_discards_an_AUTH_completed_in_cleartext()
        {

            await using var server = ServerWithSubmissionUser(WithServerCertificate);
            using var wire         = await server.ConnectAsync(server.SubmissionPort);

            // SCRAM-SHA-256 is offered in cleartext, so the AUTH can come before the upgrade.
            Assert.That(await wire.CommandAsync("EHLO client.example"),             Is.EqualTo(250));
            await AuthenticateWithScram(wire);

            Assert.That(await wire.CommandAsync("STARTTLS"),                        Is.EqualTo(220));
            await wire.StartTlsAsync();
            Assert.That(await wire.CommandAsync("EHLO client.example"),             Is.EqualTo(250));

            // Nothing that needs the AUTH works any more: not relaying ...
            Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),   Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<carol@remote.example>"),  Is.EqualTo(550), "a relay RCPT needs a new AUTH");

            // ... and not a message on the submission port.
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                            Is.EqualTo(530), "DATA needs a new AUTH");
            Assert.That(await wire.CommandAsync("RSET"),                            Is.EqualTo(250));

            // The client authenticates again, now inside TLS - not 503 "Already authenticated".
            await AuthenticateWithScram(wire);

            Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),   Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<carol@remote.example>"),  Is.EqualTo(250), "the AUTH inside TLS counts");
            Assert.That(await wire.CommandAsync("RSET"),                            Is.EqualTo(250));

            Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),   Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                            Is.EqualTo(354));

            await wire.SendAsync("Subject: after STARTTLS\r\n\r\nbody\r\n.\r\n");

            Assert.That((await wire.ReplyAsync()).Code,                             Is.EqualTo(250));
            Assert.That(server.Storage.Envelopes.Single().To,                       Is.EqualTo(new[] { "bob@hermod.test" }));

        }

    }

}
