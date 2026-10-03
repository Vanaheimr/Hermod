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
    /// The submission port (RFC 6409) delivers only for an authenticated client — by
    /// BDAT (RFC 3030) just as by DATA.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [Test]
        public async Task BDAT_on_the_submission_port_needs_AUTH_and_still_consumes_its_chunk()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync(server.SubmissionPort);

            // A local recipient needs no relay permission, so RCPT is accepted; the
            // authentication check comes with the message.
            await OpenTransaction(wire);

            // The chunk is two valid commands: refused, it must still be read as data (RFC 3030 §2).
            await wire.SendAsync(Bdat("NOOP\r\nNOOP\r\n", Last: true));

            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new[] { 530 }));
            Assert.That(server.Storage.Messages,                        Is.Empty, "nothing is delivered without AUTH");
            Assert.That(await wire.CommandAsync("NOOP"),               Is.EqualTo(250), "the session is still in step");

        }


        [Test]
        public async Task DATA_on_the_submission_port_needs_AUTH()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync(server.SubmissionPort);

            await OpenTransaction(wire);

            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(530));
            Assert.That(server.Storage.Messages,         Is.Empty);

        }

    }

}
