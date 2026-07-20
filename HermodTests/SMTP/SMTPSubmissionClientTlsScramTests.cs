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
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;
using org.GraphDefined.Vanaheimr.Hermod.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// A full, live end-to-end exercise of the submission client's security path against an in-process
    /// fake server: STARTTLS upgrade to a self-signed TLS session, followed by a real SCRAM-SHA-256
    /// (RFC 7677) authentication — the client computes and sends its proof, the server verifies it against
    /// credentials from Hermod's own <c>ScramCredentialGenerator</c> and returns a server signature the
    /// client verifies — and only then a message is sent.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientTlsScramTests
    {

        private const String User     = "app";
        private const String Password = "correct horse battery staple";


        #region A STARTTLS + SCRAM-SHA-256 fake server

        private sealed class TlsScramServer : IDisposable
        {

            private readonly TcpListener       listener;
            private readonly X509Certificate2  certificate;
            private readonly ScramCredentials  credentials;

            public Boolean Authenticated { get; private set; }
            public Boolean MessageStored { get; private set; }

            public Int32 Port => ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            public TlsScramServer(String password)
            {
                certificate  = SelfSignedCertificate();
                credentials  = ScramCredentialGenerator.Generate(password);
                listener     = new TcpListener(System.Net.IPAddress.Loopback, 0);
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

                    await writer.WriteLineAsync("220 fake.test ESMTP");

                    var inData = false;
                    String? line;

                    while ((line = await reader.ReadLineAsync()) is not null)
                    {

                        if (inData)
                        {
                            if (line == ".") { await writer.WriteLineAsync("250 2.0.0 Ok: queued"); MessageStored = true; inData = false; }
                            continue;
                        }

                        var upper = line.ToUpperInvariant();

                        if (upper.StartsWith("EHLO") || upper.StartsWith("HELO"))
                        {
                            await writer.WriteLineAsync("250-fake.test");
                            await writer.WriteLineAsync("250-8BITMIME");
                            if (!tls)
                                await writer.WriteLineAsync("250-STARTTLS");
                            else
                                await writer.WriteLineAsync("250-AUTH SCRAM-SHA-256");
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

                        else if (upper.StartsWith("AUTH SCRAM-SHA-256"))
                        {
                            var initial = line.Length > "AUTH SCRAM-SHA-256 ".Length ? line["AUTH SCRAM-SHA-256 ".Length..] : "";
                            Authenticated = await HandleScramAsync(initial, reader, writer);
                            if (!Authenticated) { client.Close(); return; }
                        }

                        else if (upper.StartsWith("MAIL FROM")) await writer.WriteLineAsync("250 2.1.0 Ok");
                        else if (upper.StartsWith("RCPT TO"))    await writer.WriteLineAsync("250 2.1.5 Ok");
                        else if (upper == "DATA")                { await writer.WriteLineAsync("354 End data"); inData = true; }
                        else if (upper == "QUIT")                { await writer.WriteLineAsync("221 2.0.0 Bye"); client.Close(); return; }
                        else                                     await writer.WriteLineAsync("250 Ok");

                    }
                }
                catch { /* client tore down the connection */ }
            }

            // The server half of SCRAM-SHA-256 (RFC 5802): challenge, verify the client proof against the
            // stored credentials, return a server signature.
            private async Task<Boolean> HandleScramAsync(String initialBase64, StreamReader reader, StreamWriter writer)
            {

                static String  B64(Byte[] b) => Convert.ToBase64String(b);
                static Byte[]   Un(String s)  => Convert.FromBase64String(s);
                static String   Val(String msg, Char key) => msg.Split(',').First(p => p.StartsWith(key + "=")).Split('=', 2)[1];

                var clientFirst  = Encoding.UTF8.GetString(Un(initialBase64));           // "n,,n=app,r=cnonce"
                var bare         = clientFirst[clientFirst.IndexOf("n=")..];             // "n=app,r=cnonce"
                var clientNonce  = Val(bare, 'r');
                var combined     = clientNonce + B64(RandomNumberGenerator.GetBytes(18));
                var serverFirst  = $"r={combined},s={credentials.SaltBase64},i={credentials.Iterations}";

                await writer.WriteLineAsync($"334 {B64(Encoding.UTF8.GetBytes(serverFirst))}");

                var clientFinal      = Encoding.UTF8.GetString(Un((await reader.ReadLineAsync()) ?? ""));  // "c=biws,r=combined,p=proof"
                var proof            = Un(Val(clientFinal, 'p'));
                var clientFinalBare  = clientFinal[..clientFinal.IndexOf(",p=")];        // "c=biws,r=combined"
                var authMessage      = Encoding.UTF8.GetBytes($"{bare},{serverFirst},{clientFinalBare}");

                // Verify: SHA256(proof XOR HMAC(StoredKey, authMessage)) must equal the stored StoredKey.
                var storedKey        = Un(credentials.StoredKeyBase64);
                var clientSignature  = HMACSHA256.HashData(storedKey, authMessage);
                var recovered        = proof.Zip(clientSignature, (a, b) => (Byte) (a ^ b)).ToArray();

                if (B64(SHA256.HashData(recovered)) != credentials.StoredKeyBase64)
                {
                    await writer.WriteLineAsync("535 5.7.8 Authentication credentials invalid");
                    return false;
                }

                // Server signature, then final success. (The client verifies v= before acking.)
                var serverSignature = HMACSHA256.HashData(Un(credentials.ServerKeyBase64), authMessage);
                await writer.WriteLineAsync($"334 {B64(Encoding.UTF8.GetBytes("v=" + B64(serverSignature)))}");
                await reader.ReadLineAsync();   // client's empty ack
                await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                return true;

            }

            private static X509Certificate2 SelfSignedCertificate()
            {
                using var rsa = RSA.Create(2048);
                var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var san = new SubjectAlternativeNameBuilder();
                san.AddDnsName("localhost");
                san.AddIpAddress(System.Net.IPAddress.Loopback);
                req.CertificateExtensions.Add(san.Build());
                var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
                return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
            }

            public void Dispose()
            {
                try { listener.Stop(); } catch { }
                certificate.Dispose();
            }

        }

        #endregion


        private static SMTPSubmissionClient ClientFor(TlsScramServer server, String password)
            => new (DomainName.Parse("127.0.0.1"),
                    IPPort.Parse((UInt16) server.Port),
                    Login:                       User,
                    Password:                    password,
                    UseTLS:                      TLSUsage.STARTTLS,
                    RemoteCertificateValidator:  (s, c, ch, cl, e) => TLSValidationResult.Success(),   // accept the self-signed cert
                    ConnectionTimeout:           TimeSpan.FromSeconds(5),
                    CommandTimeout:              TimeSpan.FromSeconds(5));

        private static EMailEnvelop Message()
            => new (EMail.Parse([
                        "From: app@example.com",
                        "To: you@example.org",
                        "Subject: TLS+SCRAM live test",
                        "Content-Type: text/plain; charset=utf-8",
                        "",
                        "Sent over STARTTLS with SCRAM-SHA-256."
                    ]));


        [Test]
        public async Task Full_STARTTLS_and_SCRAM_SHA_256_send_succeeds()
        {

            using var server = new TlsScramServer(Password);
            using var client = ClientFor(server, Password);

            var result = await client.Send(Message(), NumberOfRetries: 0);

            Assert.That(result,               Is.EqualTo(MailSentStatus.ok), "the authenticated TLS send must succeed");
            Assert.That(server.Authenticated, Is.True, "the server must have accepted the SCRAM proof");
            Assert.That(server.MessageStored, Is.True, "the message must have been delivered after auth");

        }

        [Test]
        public async Task SCRAM_with_a_wrong_password_fails_authentication()
        {

            using var server = new TlsScramServer(Password);
            using var client = ClientFor(server, "the-wrong-password");

            var result = await client.Send(Message(), NumberOfRetries: 0);

            Assert.That(result,               Is.EqualTo(MailSentStatus.InvalidLogin), "a wrong password must fail auth cleanly");
            Assert.That(server.Authenticated, Is.False);
            Assert.That(server.MessageStored, Is.False, "no message may be delivered without authentication");

        }

    }

}
