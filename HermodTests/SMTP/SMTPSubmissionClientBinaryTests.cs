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
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// A server that takes DATA and BDAT, and keeps what arrived as octets: the commands,
    /// the DATA lines (dot-stuffing undone), and the BDAT chunks.
    /// </summary>
    internal sealed class BinaryHop : IDisposable
    {

        private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);
        private readonly String[]                 extensions;

        public readonly ConcurrentQueue<String>   Commands   = new();
        public readonly ConcurrentQueue<Byte[]>   DataLines  = new();
        public readonly MemoryStream              Bdat       = new();
        public volatile Boolean                   UsedData;
        public volatile Boolean                   UsedBdat;

        public IPPort Port
            => IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

        public BinaryHop(params String[] Extensions)
        {
            extensions = Extensions;
            listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        /// <summary>
        /// The lines that arrived, by DATA or by BDAT.
        /// </summary>
        public IEnumerable<Byte[]> Lines()
        {

            if (!UsedBdat)
                return DataLines.ToArray();

            var octets = Bdat.ToArray();
            var lines  = new List<Byte[]>();
            var start  = 0;

            for (var i = 0; i + 1 < octets.Length; i++)
                if (octets[i] == '\r' && octets[i + 1] == '\n')
                {
                    lines.Add(octets[start..i]);
                    start = i + 2;
                    i++;
                }

            if (start < octets.Length)
                lines.Add(octets[start..]);

            return lines;

        }

        /// <summary>
        /// Whether binary data arrived: a line of more than 998 octets, or a NUL.
        /// </summary>
        public Boolean BinaryArrived
            => Lines().Any(line => line.Length > 998 || line.Contains((Byte) 0));

        private async Task AcceptAsync()
        {
            try
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => ServeAsync(client));
                }
            }
            catch
            {
                // the listener was stopped
            }
        }

        private async Task ServeAsync(TcpClient Client)
        {
            try
            {

                using var client = Client;
                var stream = client.GetStream();

                async Task<Byte[]?> ReadLine()
                {
                    var line  = new List<Byte>();
                    var octet = new Byte[1];
                    while (true)
                    {
                        if (await stream.ReadAsync(octet).AsTask().WaitAsync(TimeSpan.FromSeconds(10)) == 0)
                            return null;
                        if (octet[0] == '\n')
                            return [.. line.Take(line.Count > 0 && line[^1] == '\r' ? line.Count - 1 : line.Count)];
                        line.Add(octet[0]);
                    }
                }

                Task Write(String Text)
                    => stream.WriteAsync(Encoding.ASCII.GetBytes(Text + "\r\n")).AsTask();

                await Write("220 binary.hop ESMTP");

                while (true)
                {

                    var octets = await ReadLine();
                    if (octets is null)
                        return;

                    var line = Encoding.Latin1.GetString(octets);
                    Commands.Enqueue(line);

                    var verb = line.Split(' ')[0].ToUpperInvariant();

                    if (verb == "EHLO")
                    {
                        String[] lines = [ "binary.hop", .. extensions ];
                        await Write(String.Join("\r\n", lines.Select((text, i) => (i == lines.Length - 1 ? "250 " : "250-") + text)));
                    }

                    else if (verb == "DATA")
                    {
                        UsedData = true;
                        await Write("354 go ahead");
                        while (await ReadLine() is { } dataLine)
                        {
                            if (dataLine is [ (Byte) '.' ])
                                break;
                            DataLines.Enqueue(dataLine is [ (Byte) '.', .. ] ? dataLine[1..] : dataLine);
                        }
                        await Write("250 2.0.0 queued");
                    }

                    else if (verb == "BDAT")
                    {
                        UsedBdat  = true;
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var chunk = new Byte[Int32.Parse(parts[1])];
                        await stream.ReadExactlyAsync(chunk);
                        Bdat.Write(chunk);
                        await Write(parts.Length > 2 ? "250 2.0.0 queued" : $"250 2.0.0 {chunk.Length} octets");
                    }

                    else if (verb == "QUIT")
                    {
                        await Write("221 2.0.0 bye");
                        return;
                    }

                    else
                        await Write("250 2.0.0 ok");

                }

            }
            catch
            {
                // the client hung up
            }
        }

        public void Dispose()
            => listener.Stop();

    }


    /// <summary>
    /// RFC 3030 §3: binary data - a line of more than 998 octets, a NUL (RFC 2045 §2.7 to §2.9) -
    /// goes as BODY=BINARYMIME, over BDAT, and to a server that does not offer it "the
    /// sender-SMTP MUST NOT, under any circumstances, send binary data"; RFC 6152 §3: 8BITMIME
    /// "DOES NOT provide a means of transferring an object with the BINARY content-transfer-encoding".
    /// The submission client does not convert such a message: to a server without BINARYMIME
    /// and CHUNKING it is a permanent failure, decided before MAIL.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientBinaryTests
    {

        internal const String LongLine = "1200 x";

        private static String BodyOf(String Body)
            => Body == LongLine ? new String('x', 1200) : Body;

        private static async Task<SMTPSendResult> Send(BinaryHop Hop, String Body, String ContentTransferEncoding = "binary")
        {

            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        Hop.Port,
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      TLSUsage.NoTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              TimeSpan.FromSeconds(3));

            var result = await client.SendWithResult(new EMailEnvelop(EMail.Parse([
                                                         "From: app@client.example",
                                                         "To: you@binary.hop",
                                                         "Subject: binary data",
                                                         "MIME-Version: 1.0",
                                                         "Content-Type: application/octet-stream",
                                                         "Content-Transfer-Encoding: " + ContentTransferEncoding,
                                                         "",
                                                         BodyOf(Body)
                                                     ])),
                                                     NumberOfRetries: 0);

            await Task.Delay(100);

            return result;

        }


        [TestCase(LongLine)]
        [TestCase("before\0after")]
        public async Task Binary_data_goes_as_BDAT_with_BODY_BINARYMIME(String Body)
        {

            using var hop = new BinaryHop("8BITMIME", "CHUNKING", "BINARYMIME", "SIZE 10485760");

            var result = await Send(hop, Body);

            Assert.Multiple(() => {
                Assert.That(result.Status,                                       Is.EqualTo(MailSentStatus.ok));
                Assert.That(hop.Commands.Single(command => command.StartsWith("MAIL")), Does.Contain(" BODY=BINARYMIME").And.Not.Contain("8BITMIME"));
                Assert.That(hop.UsedBdat && !hop.UsedData,                       Is.True, "as BDAT");
                Assert.That(hop.Lines().Any(line => line.SequenceEqual(Encoding.UTF8.GetBytes(BodyOf(Body)))), Is.True, "the binary data arrived as it is");
            });

        }


        [TestCase(LongLine,       new[] { "8BITMIME" },                         TestName = "A line of 1200 octets is not sent to a server with 8BITMIME alone")]
        [TestCase(LongLine,       new[] { "8BITMIME", "CHUNKING" },             TestName = "A line of 1200 octets is not sent to a server with CHUNKING and without BINARYMIME")]
        [TestCase(LongLine,       new[] { "8BITMIME", "BINARYMIME" },           TestName = "A line of 1200 octets is not sent to a server with BINARYMIME and without CHUNKING")]
        [TestCase("before\0after", new[] { "8BITMIME" },                        TestName = "A NUL is not sent to a server with 8BITMIME alone")]
        [TestCase("before\0after", new[] { "8BITMIME", "CHUNKING" },            TestName = "A NUL is not sent to a server with CHUNKING and without BINARYMIME")]
        public async Task Binary_data_is_not_sent_to_a_server_without_BINARYMIME_and_CHUNKING(String Body, String[] Extensions)
        {

            using var hop = new BinaryHop(Extensions);

            var result = await Send(hop, Body);

            Assert.Multiple(() => {
                Assert.That(result.Status,  Is.EqualTo(MailSentStatus.BinaryNotSupported));
                Assert.That(hop.Commands,   Has.None.StartsWith("MAIL"), "decided before MAIL");
                Assert.That(hop.BinaryArrived, Is.False);
            });

        }


        [Test]
        public async Task A_line_of_998_octets_is_no_binary_data()
        {

            using var hop = new BinaryHop("8BITMIME", "SIZE 10485760");

            var line   = new String('y', 998);
            var result = await Send(hop, line, "7bit");

            Assert.Multiple(() => {
                Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
                Assert.That(hop.UsedData,  Is.True, "as DATA");
                Assert.That(hop.Lines().Any(arrived => arrived.SequenceEqual(Encoding.ASCII.GetBytes(line))), Is.True);
            });

        }


        [Test]
        public async Task Eight_bit_text_still_goes_as_BODY_8BITMIME()
        {

            using var hop = new BinaryHop("8BITMIME", "CHUNKING", "BINARYMIME", "SIZE 10485760");

            var result = await Send(hop, "Grüße aus Köln", "8bit");

            Assert.Multiple(() => {
                Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
                Assert.That(hop.Commands.Single(command => command.StartsWith("MAIL")), Does.Contain(" BODY=8BITMIME"));
            });

        }

    }

}
