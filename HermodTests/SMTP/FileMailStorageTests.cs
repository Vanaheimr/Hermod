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
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The default store, as it writes a message: the fields of its own a message (RFC 5322
    /// §2.2, §2.1.1) behind its Return-Path (RFC 5321 §4.4), and a file for every valid message,
    /// whatever its Message-ID (RFC 5322 §3.6.4).
    /// </summary>
    [TestFixture]
    public class FileMailStorageTests
    {

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private String directory = null!;

        [SetUp]
        public void MakeDirectory()
            => directory = Directory.CreateTempSubdirectory("hermod-filestore-").FullName;

        [TearDown]
        public void RemoveDirectory()
            => Directory.Delete(directory, recursive: true);


        private async Task<String> Store(String Message, String? SpfRecord = null)
        {

            var message = EMailMessage.Parse(Message);

            if (SpfRecord is not null)
                message.Verification = new DnsVerificationResult(SPFResult.PermError, SpfRecord, DkimResult.None, null, DmarcResult.None, null, []);

            var path = await new FileMailStorage(directory, new QuietLogger()).StoreAsync(message, "sender@client.example", [ "alice@hermod.test" ]);

            return Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path));

        }

        private static String Header(String Stored)
            => Stored[..(Stored.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 2)];

        private const String Message = "Return-Path: <sender@client.example>\r\n" +
                                       "Received: from client.example\r\n" +
                                       "\tby mx.hermod.test; Wed, 25 Nov 2026 12:00:00 +0000\r\n" +
                                       "From: <sender@client.example>\r\n" +
                                       "Subject: stored\r\n" +
                                       "Message-ID: <stored@client.example>\r\n" +
                                       "\r\n" +
                                       "hello\r\n";


        #region What DNS says, in the header

        [TestCase("\r\n")]
        [TestCase("\n")]
        [TestCase("\r")]
        [TestCase("\r\n\r\n")]
        [TestCase("\u0085")]
        public async Task A_line_break_in_a_DNS_record_makes_no_field_of_its_own(String LineBreak)
        {

            var stored = await Store(Message, $"v=spf1 -all{LineBreak}X-DMARC-Result: Pass");
            var header = Header(stored);

            Assert.That(header,                                 Does.Contain("\r\nX-SPF-Record: v=spf1 -all X-DMARC-Result: Pass\r\n"));
            Assert.That(Regex.Split(header, "\r\n|\r|\n|\u0085"), Has.None.EqualTo("X-DMARC-Result: Pass"));
            Assert.That(header,                                 Does.Contain("\r\nSubject: stored\r\n"), "the message's own fields are in the header");

        }


        [Test]
        public void A_long_value_is_folded_and_unfolds_to_itself()
        {

            var value = String.Join(" ", Enumerable.Range(0, 300).Select(number => $"ip4:192.0.2.{number % 256}"));
            var field = HeaderField.Of("X-SPF-Record", value);

            Assert.That(field.Split("\r\n").Where(line => line.Length > 0), Has.All.Matches<String>(line => line is not null && Encoding.UTF8.GetByteCount(line) <= 998));
            Assert.That(field.Split("\r\n").Skip(1).Where(line => line.Length > 0), Has.All.StartWith(" "), "folded before white space");
            Assert.That(field.Replace("\r\n", ""), Is.EqualTo("X-SPF-Record: " + value + ""));

        }


        [Test]
        public void A_value_of_the_usual_kind_goes_as_it_is()

            => Assert.That(HeaderField.Of("X-SPF-Record", "v=spf1 ip4:192.0.2.0/24\t-all"),
                           Is.EqualTo("X-SPF-Record: v=spf1 ip4:192.0.2.0/24\t-all\r\n"));

        #endregion

        #region The Return-Path first

        [Test]
        public async Task A_stored_message_begins_with_its_Return_Path()
        {

            var stored = await Store(Message);

            Assert.That(stored, Does.StartWith("Return-Path: <sender@client.example>\r\nX-Envelope-From: sender@client.example\r\n"));
            Assert.That(stored, Does.EndWith(Message[Message.IndexOf("Received:", StringComparison.Ordinal)..]), "the message behind the store's fields as it came");

        }


        [Test]
        public async Task A_folded_Return_Path_stays_whole()
        {

            var stored = await Store("Return-Path:\r\n <sender@client.example>\r\nSubject: folded\r\n\r\nhello\r\n");

            Assert.That(stored, Does.StartWith("Return-Path:\r\n <sender@client.example>\r\nX-Envelope-From: "));

        }


        [Test]
        public async Task A_message_without_a_Return_Path_begins_with_the_stores_fields()

            => Assert.That(await Store("Subject: none\r\n\r\nhello\r\n"), Does.StartWith("X-Envelope-From: sender@client.example\r\n"));

        #endregion

        #region A file for every message

        [TestCase(300)]
        [TestCase(900)]
        public async Task A_message_with_a_long_Message_ID_is_stored(Int32 Length)
        {

            var stored = await Store($"Subject: long\r\nMessage-ID: <{new String('m', Length)}@client.example>\r\n\r\nhello\r\n");

            Assert.That(stored,                              Does.Contain("\r\nSubject: long\r\n"));
            Assert.That(Directory.GetFiles(directory).Single(), Has.Length.LessThan(directory.Length + 1 + 255));

        }


        [Test]
        public void Long_Message_IDs_that_begin_alike_get_names_of_their_own()
        {

            var one    = FileMailStorage.FileNameOf(new String('m', 300) + "1@client.example");
            var other  = FileMailStorage.FileNameOf(new String('m', 300) + "2@client.example");

            Assert.That(one,                              Is.Not.EqualTo(other));
            Assert.That(Encoding.UTF8.GetByteCount(one),  Is.LessThanOrEqualTo(FileMailStorage.MaxMessageIdOctetsInFileName));

        }


        [Test]
        public void A_long_Message_ID_in_UTF8_is_cut_by_its_octets()
        {

            var name = FileMailStorage.FileNameOf(new String('ü', 200) + "😀😀@client.example");

            Assert.That(Encoding.UTF8.GetByteCount(name), Is.LessThanOrEqualTo(FileMailStorage.MaxMessageIdOctetsInFileName));
            Assert.That(name,                             Does.Not.Contain("\uFFFD"));

        }


        [Test]
        public void A_Message_ID_of_the_usual_kind_names_the_file_as_it_is()

            => Assert.That(FileMailStorage.FileNameOf("0123456789abcdef@client.example"), Is.EqualTo("0123456789abcdef@client.example"));

        #endregion

    }

}
