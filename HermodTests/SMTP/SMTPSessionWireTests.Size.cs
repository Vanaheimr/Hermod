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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 1870 §6.1: a declared SIZE above the server's fixed maximum is refused with 552
    /// at MAIL, before any data is sent.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [TestCase("1025",                  552, TestName = "SIZE one octet above the limit is 552 at MAIL")]
        [TestCase("99999999999999999999",  552, TestName = "SIZE beyond UInt64 is 552, not an overflow")]
        [TestCase("1024",                  250, TestName = "SIZE exactly at the limit is accepted")]
        [TestCase("0",                     250, TestName = "SIZE=0 is accepted")]
        public async Task A_declared_SIZE_is_checked_at_MAIL(String Size, Int32 Expected)
        {

            await using var server = new Server(MaxMessageSize: 1024);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),                            Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"MAIL FROM:<sender@client.example> SIZE={Size}"),  Is.EqualTo(Expected));

            // A refused MAIL opens no transaction; an accepted one does.
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"), Is.EqualTo(Expected == 250 ? 250 : 503));

        }

    }

}
