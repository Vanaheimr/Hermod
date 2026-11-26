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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 3030 §3: the relay sends binary data - a line of more than 998 octets, a NUL (RFC
    /// 2045 §2.7 to §2.9) - as BODY=BINARYMIME, over BDAT, to a next hop that offers BINARYMIME
    /// and CHUNKING, and to no other: "the sender-SMTP MUST NOT, under any circumstances, send
    /// binary data", and "may treat this as a permanent error". "BINARYMIME cannot be used
    /// with the DATA command".
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region (private) Binary data to a next hop

        private static String BinaryMessage(String Body)

            => "From: app@client.example\r\n" +
               "To: you@next.example\r\n" +
               "Subject: binary data\r\n" +
               "MIME-Version: 1.0\r\n" +
               "Content-Type: application/octet-stream\r\n" +
               "Content-Transfer-Encoding: binary\r\n" +
               "\r\n" +
               Body + "\r\n";

        private static async Task<SendResult> SendBinary(BinaryHop Hop, String Message, Byte[]? Octets = null)
        {

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname     = "relay.hermod.test",
                                                    SmartHost         = "127.0.0.1",
                                                    SmartHostPort     = Hop.Port,
                                                    ConnectTimeoutMs  = 3_000,
                                                    ReadTimeoutMs     = 5_000,
                                                    WriteTimeoutMs    = 3_000
                                                },
                                                null,
                                                new NoDNS(),
                                                new QuietLogger());

            var result = await client.SendAsync("next.example",
                                                "sender@client.example",
                                                [ "you@next.example" ],
                                                Message,
                                                ct:             cts.Token,
                                                messageOctets:  Octets);

            await Task.Delay(100);
            return result;

        }

        #endregion


        [TestCase(1200, false, TestName = "The relay sends a line of 1200 octets as BDAT with BODY=BINARYMIME")]
        [TestCase(10,   true,  TestName = "The relay sends a NUL as BDAT with BODY=BINARYMIME")]
        public async Task The_relay_sends_binary_data_as_BDAT_with_BODY_BINARYMIME(Int32 Length, Boolean Nul)
        {

            using var hop = new BinaryHop("8BITMIME", "CHUNKING", "BINARYMIME", "SIZE 10485760");

            var body   = new String('x', Length) + (Nul ? "\0x" : "");
            var result = await SendBinary(hop, BinaryMessage(body));

            Assert.Multiple(() => {
                Assert.That(result.Status,                                         Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(hop.Commands.Single(command => command.StartsWith("MAIL")), Does.Contain(" BODY=BINARYMIME"));
                Assert.That(hop.UsedBdat && !hop.UsedData,                         Is.True, "as BDAT, not DATA");
                Assert.That(hop.Commands.Single(command => command.StartsWith("BDAT")), Does.EndWith(" LAST"));
                Assert.That(hop.Lines().Any(line => line.SequenceEqual(Encoding.UTF8.GetBytes(body))), Is.True, "the binary data arrived as it is");
                Assert.That(Encoding.ASCII.GetString(hop.Bdat.ToArray()),          Does.StartWith("From: app@client.example\r\n").And.EndWith(body + "\r\n"));
            });

        }


        [Test]
        public async Task The_relay_sends_the_octets_it_received_as_they_are()
        {

            using var hop = new BinaryHop("8BITMIME", "CHUNKING", "BINARYMIME", "SIZE 10485760");

            // An 8-bit body that is no UTF-8, with a NUL in it, and a line beginning with a dot:
            // BDAT sends it octet for octet, no dot-stuffing.
            var octets = Encoding.Latin1.GetBytes(BinaryMessage("Grüße\0aus Köln\r\n.punkt"));
            var result = await SendBinary(hop, Encoding.Latin1.GetString(octets), octets);

            Assert.Multiple(() => {
                Assert.That(result.Status,       Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(hop.Bdat.ToArray(),  Is.EqualTo(octets));
            });

        }


        [TestCase("8BITMIME",            TestName = "The relay sends no binary data to a next hop with 8BITMIME alone")]
        [TestCase("8BITMIME CHUNKING",   TestName = "The relay sends no binary data to a next hop with CHUNKING and without BINARYMIME")]
        [TestCase("8BITMIME BINARYMIME", TestName = "The relay sends no binary data to a next hop with BINARYMIME and without CHUNKING")]
        public async Task The_relay_sends_no_binary_data_to_a_next_hop_without_BINARYMIME_and_CHUNKING(String Extensions)
        {

            foreach (var body in new[] { new String('x', 1200), "before\0after" })
            {

                using var hop = new BinaryHop(Extensions.Split(' '));

                var result = await SendBinary(hop, BinaryMessage(body));

                Assert.Multiple(() => {
                    Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail), "a permanent failure");
                    Assert.That(result.ResponseText,     Does.Contain("5.6.3"));
                    Assert.That(hop.Commands,        Has.None.StartsWith("MAIL"), "decided before MAIL");
                    Assert.That(hop.BinaryArrived,   Is.False);
                });

            }

        }


        [Test]
        public async Task A_line_of_998_octets_is_relayed_as_DATA()
        {

            using var hop = new BinaryHop("8BITMIME", "CHUNKING", "BINARYMIME", "SIZE 10485760");

            var line   = new String('y', 998);
            var result = await SendBinary(hop, $"Subject: a long line\r\n\r\n{line}\r\n");

            Assert.Multiple(() => {
                Assert.That(result.Status,  Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(hop.UsedData,   Is.True, "as DATA");
                Assert.That(hop.Commands.Single(command => command.StartsWith("MAIL")), Does.Not.Contain("BODY="));
                Assert.That(hop.Lines().Any(arrived => arrived.SequenceEqual(Encoding.ASCII.GetBytes(line))), Is.True);
            });

        }


        #region MessageOctets.IsBinary, MessageOctets.Bdat

        [TestCase("",                                   false, TestName = "No octets are no binary data")]
        [TestCase("998",                                false, TestName = "A line of 998 octets is no binary data")]
        [TestCase("999",                                true,  TestName = "A line of 999 octets is binary data")]
        [TestCase("998\r\n998\r\n998",                  false, TestName = "Lines of 998 octets each, CR LF between them, are no binary data")]
        [TestCase("998\n998\r998",                      false, TestName = "A bare LF and a bare CR end a line too")]
        [TestCase("a\0b",                               true,  TestName = "A NUL is binary data")]
        [TestCase("Grüße",                    false, TestName = "8-bit text is no binary data")]
        public void What_binary_data_is(String Text, Boolean Binary)
        {

            // "998" and "999" stand for a line of that many octets.
            var expanded = Text.Replace("998", new String('a', 998)).
                                Replace("999", new String('a', 999));

            Assert.Multiple(() => {
                Assert.That(MessageOctets.IsBinary(Encoding.Latin1.GetBytes(expanded)),                     Is.EqualTo(Binary), "as octets");
                Assert.That(MessageOctets.IsBinary(expanded.Split([ "\r\n", "\r", "\n" ], StringSplitOptions.None)), Is.EqualTo(Binary), "as lines");
            });

        }


        [Test]
        public void A_line_of_UTF_8_is_measured_in_octets()
        {

            // 500 characters of two octets each: 1000 octets on the wire.
            Assert.Multiple(() => {
                Assert.That(MessageOctets.IsBinary([ new String('ü', 500) ]), Is.True);
                Assert.That(MessageOctets.IsBinary([ new String('ü', 499) ]), Is.False);
            });

        }


        [TestCase("a\r\nb",     "a\r\nb\r\n", TestName = "BDAT ends the last line with CR LF")]
        [TestCase("a\r\nb\r\n", "a\r\nb\r\n", TestName = "BDAT adds no CR LF to a last line that has one")]
        [TestCase(".a\r\n",     ".a\r\n",     TestName = "BDAT stuffs no dot")]
        public void The_octets_of_BDAT(String Octets, String Sent)

            => Assert.That(MessageOctets.Bdat(Encoding.ASCII.GetBytes(Octets)), Is.EqualTo(Encoding.ASCII.GetBytes(Sent)));

        #endregion

    }

}
