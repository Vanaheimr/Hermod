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
    /// MT-PRIORITY on the server (RFC 6710 §4.1): one parameter, as the grammar writes it.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [TestCase("MT-PRIORITY=+3")]
        [TestCase("MT-PRIORITY=-0")]
        [TestCase("MT-PRIORITY=05")]
        [TestCase("MT-PRIORITY=3 MT-PRIORITY=3")]
        [TestCase("MT-PRIORITY=3 mt-priority=-3")]
        [Test(Description = "RFC 6710 §4.1: \"If any of the associated <esmtp-value>s [...] are not syntactically valid, or if there is more than one MT-PRIORITY parameter in a particular MAIL FROM command, the server MUST return an error\"")]
        public async Task MT_PRIORITY_outside_the_grammar_or_given_twice_is_501(String Parameters)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),                               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"MAIL FROM:<sender@client.example> {Parameters}"),  Is.EqualTo(501));

            // Nothing was opened: a correct MAIL follows.
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example> MT-PRIORITY=-3"),  Is.EqualTo(250));

        }

    }

}
