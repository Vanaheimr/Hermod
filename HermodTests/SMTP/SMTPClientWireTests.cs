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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    /// End-to-end wire tests for the submission SMTPClient against a scriptable in-process fake server,
    /// including "mean" servers that abruptly drop or stall the TCP connection (à la the WebSocket
    /// Autobahn suite). The point is that these are detected cleanly and FAST — never after a 60s wait.
    /// </summary>
    [TestFixture]
    public class SMTPClientWireTests
    {

        #region A scriptable fake SMTP server

        private sealed class FakeSmtpServer : IDisposable
        {

            public enum Mode { Normal, DropAfterGreeting, DropAfterMailFrom, DropDuringData, HangAfterGreeting }

            private readonly TcpListener   listener;
            private readonly Mode          mode;

            public readonly List<String>  Commands  = [];   // received SMTP commands
            public readonly List<String>  DataLines = [];   // received DATA body lines (verbatim, incl. dot-stuffing)

            public Int32 Port => ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            public FakeSmtpServer(Mode Mode)
            {
                mode      = Mode;
                listener  = new TcpListener(System.Net.IPAddress.Loopback, 0);
                listener.Start();
                _ = Task.Run(HandleAsync);
            }

            private async Task HandleAsync()
            {
                try
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

                    await writer.WriteLineAsync("220 fake.test ESMTP");

                    if (mode == Mode.DropAfterGreeting) { client.Close(); return; }
                    if (mode == Mode.HangAfterGreeting) { await Task.Delay(TimeSpan.FromSeconds(30)); return; }

                    var inData = false;
                    String? line;

                    while ((line = await reader.ReadLineAsync()) is not null)
                    {

                        if (inData)
                        {
                            if (line == ".") { await writer.WriteLineAsync("250 2.0.0 Ok: queued"); inData = false; continue; }
                            DataLines.Add(line);
                            if (mode == Mode.DropDuringData) { client.Close(); return; }
                            continue;
                        }

                        Commands.Add(line);
                        var upper = line.ToUpperInvariant();

                        if (upper.StartsWith("EHLO") || upper.StartsWith("HELO"))
                        {
                            await writer.WriteLineAsync("250-fake.test");
                            await writer.WriteLineAsync("250-SIZE 10485760");
                            await writer.WriteLineAsync("250-8BITMIME");
                            await writer.WriteLineAsync("250-SMTPUTF8");
                            await writer.WriteLineAsync("250-DSN");
                            await writer.WriteLineAsync("250 ENHANCEDSTATUSCODES");
                        }
                        else if (upper.StartsWith("MAIL FROM"))
                        {
                            if (mode == Mode.DropAfterMailFrom) { client.Close(); return; }
                            await writer.WriteLineAsync("250 2.1.0 Ok");
                        }
                        else if (upper.StartsWith("RCPT TO")) await writer.WriteLineAsync("250 2.1.5 Ok");
                        else if (upper == "DATA")             { await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>"); inData = true; }
                        else if (upper == "QUIT")             { await writer.WriteLineAsync("221 2.0.0 Bye"); client.Close(); return; }
                        else                                  await writer.WriteLineAsync("250 Ok");

                    }
                }
                catch { /* the client tore down the connection — expected in the mean cases */ }
            }

            public void Dispose()
            {
                try { listener.Stop(); } catch { }
            }

        }

        #endregion


        private static SMTPClient ClientFor(FakeSmtpServer server)
            => new (DomainName.Parse("127.0.0.1"),
                    IPPort.Parse((UInt16) server.Port),
                    UseTLS:             TLSUsage.NoTLS,
                    ConnectionTimeout:  TimeSpan.FromSeconds(3),
                    CommandTimeout:     TimeSpan.FromSeconds(2));

        private static EMailEnvelop Message(String body)
            => new (EMail.Parse([
                        "From: me@example.com",
                        "To: you@example.org",
                        "Subject: Wire test",
                        "Content-Type: text/plain; charset=utf-8",
                        "",
                        .. body.Split("\n")
                    ]));


        [Test]
        public async Task Normal_send_completes_and_dot_stuffs_the_body()
        {

            using var server = new FakeSmtpServer(FakeSmtpServer.Mode.Normal);
            using var client = ClientFor(server);

            // A body containing a bare "." line: if it is NOT dot-stuffed, DATA terminates early and
            // the text after it is parsed as commands (a broken send).
            var result = await client.Send(Message("before the dot\n.\nafter the dot"));

            Assert.That(result, Is.EqualTo(MailSentStatus.ok));

            // MAIL FROM has the correct RFC 5321 syntax (no space after the colon).
            Assert.That(server.Commands, Has.Some.StartsWith("MAIL FROM:<me@example.com>"));
            Assert.That(server.Commands, Has.Some.StartsWith("RCPT TO:<you@example.org>"));

            // The bare "." was stuffed to ".." and the text after it survived (no early DATA end).
            Assert.That(server.DataLines, Does.Contain(".."));
            Assert.That(server.DataLines, Does.Contain("after the dot"));

        }

        [Test]
        public void Server_dropping_after_greeting_is_detected_fast_and_clean()
            => AssertFastClean(FakeSmtpServer.Mode.DropAfterGreeting, MailSentStatus.ConnectionClosed);

        [Test]
        public void Server_dropping_after_MAIL_FROM_is_detected_fast_and_clean()
            => AssertFastClean(FakeSmtpServer.Mode.DropAfterMailFrom, MailSentStatus.ConnectionClosed);

        [Test]
        public void Server_dropping_during_DATA_is_detected_fast_and_clean()
            => AssertFastClean(FakeSmtpServer.Mode.DropDuringData, MailSentStatus.ConnectionClosed);

        [Test]
        public void Server_that_hangs_times_out_fast_and_clean()
            => AssertFastClean(FakeSmtpServer.Mode.HangAfterGreeting, MailSentStatus.Timeout);


        private static void AssertFastClean(FakeSmtpServer.Mode mode, MailSentStatus expected)
        {

            using var server = new FakeSmtpServer(mode);
            using var client = ClientFor(server);

            var sw     = Stopwatch.StartNew();
            var result = client.Send(Message("hello world")).GetAwaiter().GetResult();
            sw.Stop();

            Assert.That(result, Is.EqualTo(expected), $"mean server ({mode}) must be classified as {expected}");
            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
                        $"detection must be fast (was {sw.Elapsed.TotalSeconds:F1}s) — never a 60s hang");

        }

    }

}
