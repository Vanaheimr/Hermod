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

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// REQUIRETLS as the submission client gives it (RFC 8689 §4.3): "If TLS is to be required, it
    /// MUST do so by negotiating STARTTLS and REQUIRETLS and including the REQUIRETLS option on the
    /// MAIL FROM command" - not to a server that does not offer REQUIRETLS inside TLS. The finding
    /// C-19 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientRequireTlsTests
    {

        #region Setup

        /// <summary>
        /// A submission server with STARTTLS that offers REQUIRETLS inside TLS - or not.
        /// </summary>
        private sealed class TlsServer : IDisposable
        {

            private readonly TcpListener       listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly X509Certificate2  certificate;
            private readonly Boolean           requireTls;

            public readonly ConcurrentQueue<String> Commands = new();
            public volatile Boolean                 MessageStored;

            public IPPort Port
                => IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

            public TlsServer(Boolean RequireTls)
            {
                requireTls   = RequireTls;
                certificate  = SelfSigned();
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                try
                {

                    using var client  = await listener.AcceptTcpClientAsync();
                    Stream    stream  = client.GetStream();
                    var       reader  = new StreamReader(stream, Encoding.ASCII);
                    var       writer  = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
                    var       tls     = false;
                    var       inData  = false;

                    await writer.WriteLineAsync("220 submission.test ESMTP");

                    while (await reader.ReadLineAsync() is { } line)
                    {

                        if (inData)
                        {
                            if (line == ".") { await writer.WriteLineAsync("250 2.0.0 Ok: queued"); MessageStored = true; inData = false; }
                            continue;
                        }

                        Commands.Enqueue(line);

                        var upper = line.ToUpperInvariant();

                        if (upper.StartsWith("EHLO"))
                        {
                            await writer.WriteLineAsync("250-submission.test");
                            await writer.WriteLineAsync("250-8BITMIME");
                            if (!tls)
                                await writer.WriteLineAsync("250-STARTTLS");
                            else if (requireTls)
                                await writer.WriteLineAsync("250-REQUIRETLS");
                            await writer.WriteLineAsync("250 ENHANCEDSTATUSCODES");
                        }

                        else if (upper == "STARTTLS")
                        {
                            await writer.WriteLineAsync("220 2.0.0 Ready to start TLS");
                            var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                            await ssl.AuthenticateAsServerAsync(certificate, clientCertificateRequired: false, checkCertificateRevocation: false);
                            stream = ssl;
                            reader = new StreamReader(ssl, Encoding.ASCII);
                            writer = new StreamWriter(ssl, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
                            tls    = true;
                        }

                        else if (upper.StartsWith("MAIL FROM")) await writer.WriteLineAsync("250 2.1.0 Ok");
                        else if (upper.StartsWith("RCPT TO"))    await writer.WriteLineAsync("250 2.1.5 Ok");
                        else if (upper == "DATA")                { await writer.WriteLineAsync("354 End data"); inData = true; }
                        else if (upper == "QUIT")                { await writer.WriteLineAsync("221 2.0.0 Bye"); return; }
                        else                                     await writer.WriteLineAsync("250 Ok");

                    }

                }
                catch
                {
                    // the client hung up
                }
            }

            private static X509Certificate2 SelfSigned()
            {
                using var rsa  = RSA.Create(2048);
                var request    = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
                return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
            }

            public void Dispose()
            {
                try { listener.Stop(); } catch { }
                certificate.Dispose();
            }

        }

        private static SMTPSubmissionClient ClientFor(TlsServer Server)
            => new (DomainName.Parse("127.0.0.1"),
                    Server.Port,
                    UseTLS:                      TLSUsage.STARTTLS,
                    RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                    ConnectionTimeout:           TimeSpan.FromSeconds(5),
                    CommandTimeout:              TimeSpan.FromSeconds(5));

        private static EMailEnvelop RequiringTls()
            => new (EMail.Parse([
                        "From: app@example.com",
                        "To: you@example.org",
                        "Subject: for TLS only",
                        "Content-Type: text/plain; charset=utf-8",
                        "",
                        "This message requires TLS on every hop."
                    ])) { RequireTls = true };

        #endregion


        [Test(Description = "RFC 8689 §4.3: a REQUIRETLS message goes to no server that does not offer REQUIRETLS inside TLS")]
        public async Task A_REQUIRETLS_message_goes_to_no_server_without_REQUIRETLS()
        {

            using var server = new TlsServer(RequireTls: false);
            using var client = ClientFor(server);

            var result = await client.Send(RequiringTls(), NumberOfRetries: 0);
            await Task.Delay(100);

            Assert.That(result,               Is.Not.EqualTo(MailSentStatus.ok));
            Assert.That(server.Commands.Where(command => command.StartsWith("MAIL", StringComparison.OrdinalIgnoreCase)), Is.Empty, "no MAIL at all");
            Assert.That(server.MessageStored, Is.False);

        }

        [Test(Description = "RFC 8689 §4.3: to a server that offers REQUIRETLS inside TLS, the message goes with REQUIRETLS on MAIL")]
        public async Task A_REQUIRETLS_message_goes_with_REQUIRETLS_on_MAIL()
        {

            using var server = new TlsServer(RequireTls: true);
            using var client = ClientFor(server);

            var result = await client.Send(RequiringTls(), NumberOfRetries: 0);
            await Task.Delay(100);

            Assert.That(result,               Is.EqualTo(MailSentStatus.ok));
            Assert.That(server.Commands.FirstOrDefault(command => command.StartsWith("MAIL", StringComparison.OrdinalIgnoreCase)), Does.EndWith(" REQUIRETLS"));
            Assert.That(server.MessageStored, Is.True);

        }

    }

}
