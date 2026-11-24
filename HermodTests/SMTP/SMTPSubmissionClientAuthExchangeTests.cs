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

using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The submission client's AUTH exchange (RFC 4954 §4): no initial response that would make
    /// the AUTH command longer than a command line may be, and a challenge that is no base64
    /// cancelled with "*".
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientAuthExchangeTests
    {

        /// <summary>
        /// A server behind STARTTLS that offers the given mechanisms and answers every line of an
        /// AUTH exchange as told; it records every line it reads.
        /// </summary>
        private sealed class TlsAuthServer : IDisposable
        {

            private readonly TcpListener            listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly X509Certificate2       certificate = SelfSignedCertificate();
            private readonly String[]               mechanisms;
            private readonly Func<String, String>   auth;

            public readonly ConcurrentQueue<String> Lines = new();
            public volatile Boolean                 MessageStored;

            public IPPort Port => IPPort.Parse(((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

            /// <param name="Mechanisms">The mechanisms of the AUTH keyword.</param>
            /// <param name="Auth">The reply to the AUTH command and to each line of its exchange; a 334 keeps the exchange open.</param>
            public TlsAuthServer(String[] Mechanisms, Func<String, String> Auth)
            {
                mechanisms = Mechanisms;
                auth       = Auth;
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
                    var       inAuth  = false;
                    var       inData  = false;

                    await writer.WriteLineAsync("220 fake.test ESMTP");

                    String? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                    {

                        if (inData)
                        {
                            if (line == ".") { await writer.WriteLineAsync("250 2.0.0 Ok: queued"); MessageStored = true; inData = false; }
                            continue;
                        }

                        Lines.Enqueue(line);

                        var upper = line.ToUpperInvariant();

                        if (inAuth || upper.StartsWith("AUTH"))
                        {
                            var reply = auth(line);
                            inAuth = reply.StartsWith("334");
                            await writer.WriteLineAsync(reply);
                        }

                        else if (upper.StartsWith("EHLO"))
                        {
                            await writer.WriteLineAsync("250-fake.test");
                            await writer.WriteLineAsync("250-8BITMIME");
                            await writer.WriteLineAsync(tls ? $"250-AUTH {String.Join(' ', mechanisms)}" : "250-STARTTLS");
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
                catch { /* the client tore down the connection */ }
            }

            private static X509Certificate2 SelfSignedCertificate()
            {
                using var rsa = RSA.Create(2048);
                var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var san     = new SubjectAlternativeNameBuilder();
                san.AddDnsName("localhost");
                san.AddIpAddress(System.Net.IPAddress.Loopback);
                request.CertificateExtensions.Add(san.Build());
                var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
                return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
            }

            public void Dispose()
            {
                try { listener.Stop(); } catch { }
                certificate.Dispose();
            }

        }


        private static SMTPSubmissionClient ClientFor(TlsAuthServer Server, String Password)
            => new (DomainName.Parse("127.0.0.1"),
                    Server.Port,
                    Login:                       "app",
                    Password:                    Password,
                    UseTLS:                      TLSUsage.STARTTLS,
                    RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                    ConnectionTimeout:           TimeSpan.FromSeconds(5),
                    CommandTimeout:              TimeSpan.FromSeconds(5));

        private static EMailEnvelop Message()
            => new (EMail.Parse([
                        "From: app@example.com",
                        "To: you@example.org",
                        "Subject: AUTH exchange",
                        "Content-Type: text/plain; charset=utf-8",
                        "",
                        "hello"
                    ]));

        private static String Base64(String Text)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(Text));

        /// <summary>
        /// PLAIN: an empty challenge to AUTH without an initial response, 235 to any response.
        /// </summary>
        private static String Plain(String Line)
            => Line.Equals("AUTH PLAIN", StringComparison.OrdinalIgnoreCase) ? "334 " : "235 2.7.0 Authentication successful";


        #region The initial response and the line length (RFC 4954 §4, RFC 5321 §4.5.3.1.4)

        [Test]
        public async Task A_long_password_goes_after_the_challenge()
        {

            var password = new String('p', 400);

            using var server = new TlsAuthServer([ "PLAIN" ], Plain);
            using var client = ClientFor(server, password);

            var result = await client.Send(Message(), NumberOfRetries: 0);
            var lines  = server.Lines.ToList();
            var auth   = lines.FindIndex(line => line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase));

            Assert.Multiple(() => {
                Assert.That(result,                                                     Is.EqualTo(MailSentStatus.ok));
                Assert.That(Encoding.UTF8.GetByteCount(lines[auth]) + 2,                     Is.LessThanOrEqualTo(512), "the AUTH command fits a command line; the response after the 334 need not");
                Assert.That(lines[auth],                                                Is.EqualTo("AUTH PLAIN"));
                Assert.That(lines[auth + 1],                                            Is.EqualTo(Base64("\0app\0" + password)));
            });

        }


        [Test]
        public async Task A_short_password_goes_as_the_initial_response()
        {

            using var server = new TlsAuthServer([ "PLAIN" ], Plain);
            using var client = ClientFor(server, "secret");

            var result = await client.Send(Message(), NumberOfRetries: 0);

            Assert.Multiple(() => {
                Assert.That(result,          Is.EqualTo(MailSentStatus.ok));
                Assert.That(server.Lines,    Does.Contain("AUTH PLAIN " + Base64("\0app\0secret")));
            });

        }

        #endregion


        #region A challenge that is no base64 (RFC 4954 §4)

        public static IEnumerable<TestCaseData> ChallengesThatAreNoBase64()
        {

            var serverFirst = Base64("r=abc,s=c2FsdA==,i=4096");

            TestCaseData Case(String Mechanism, String Name, String Challenge)
                => new TestCaseData(Mechanism, Challenge).SetName($"{Mechanism}: a challenge that is no base64 is cancelled: {Name}");

            yield return Case("SCRAM-SHA-256", "not the alphabet",  "334 %%% not base64 %%%");
            yield return Case("SCRAM-SHA-256", "a space inside it", "334 " + serverFirst.Insert(8, " "));
            yield return Case("CRAM-MD5",      "not the alphabet",  "334 %%% not base64 %%%");
            yield return Case("CRAM-MD5",      "a space inside it", "334 " + Base64("<1896.697170952@postoffice.example>").Insert(8, " "));

        }


        [TestCaseSource(nameof(ChallengesThatAreNoBase64))]
        public async Task A_challenge_that_is_no_base64_is_cancelled(String Mechanism, String Challenge)
        {

            String Auth(String Line)
                => Line == "*"                                           ? "501 5.7.0 Authentication cancelled"
                 : Line.StartsWith("AUTH", StringComparison.Ordinal)     ? Challenge
                 :                                                         "535 5.7.8 Authentication credentials invalid";

            using var server = new TlsAuthServer([ Mechanism ], Auth);
            using var client = ClientFor(server, "secret");

            var result = await client.Send(Message(), NumberOfRetries: 0);
            var lines  = server.Lines.ToList();
            var auth   = lines.FindIndex(line => line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase));

            Assert.Multiple(() => {
                Assert.That(auth,                                           Is.Not.Negative);
                Assert.That(lines.ElementAtOrDefault(auth + 1),             Is.EqualTo("*"), "the answer to the challenge");
                Assert.That(lines,                                          Has.None.StartsWith("MAIL FROM"));
                Assert.That(result,                                         Is.EqualTo(MailSentStatus.InvalidLogin));
                Assert.That(server.MessageStored,                           Is.False);
            });

        }

        #endregion

    }

}
