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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 5321 §4.1.1.10: "The sender MUST NOT intentionally close the transmission channel
    /// until it sends a QUIT command" - after a failed delivery as after one that succeeded.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        public static IEnumerable<TestCaseData> FailedDeliveries()
        {

            TestCaseData Case(String Name, Func<String, String?> Reply, SendStatus Status, String Greeting = "220 next.hop ESMTP")
                => new TestCaseData(Reply, Greeting, Status).SetName($"QUIT after {Name}");

            yield return Case("a refused greeting",   _    => null,                                                                       SendStatus.PermFail, Greeting: "554 5.3.2 No service");
            yield return Case("a refused EHLO",       line => line.StartsWith("EHLO") ? "421 4.3.2 Shutting down"       : null,         SendStatus.TempFail);
            yield return Case("a refused MAIL",       line => line.StartsWith("MAIL") ? "550 5.7.1 Sender rejected"     : null,         SendStatus.PermFail);
            yield return Case("every RCPT refused",   line => line.StartsWith("RCPT") ? "550 5.1.1 No such user"        : null,         SendStatus.PermFail);
            yield return Case("a refused DATA",       line => line.StartsWith("DATA") ? "554 5.5.1 No valid recipients" : null,         SendStatus.PermFail);
            yield return Case("a refused message",    line => line == "."             ? "554 5.7.1 Spam"                : null,         SendStatus.PermFail);
            yield return Case("a delivery",           _    => null,                                                                       SendStatus.Success);

        }


        [TestCaseSource(nameof(FailedDeliveries))]
        public async Task Every_session_ends_with_QUIT_and_a_closed_connection(Func<String, String?> Reply, String Greeting, SendStatus Status)
        {

            using var nextHop = new NextHop(Reply, Greeting);

            var result = await Send(nextHop);

            Assert.That(result.Status,                    Is.EqualTo(Status), result.ResponseText);
            Assert.That(nextHop.Commands.LastOrDefault(), Is.EqualTo("QUIT"), String.Join(" | ", nextHop.Commands));
            Assert.That(nextHop.ClosedByClient,           Is.True, "the client closes the connection after QUIT");

        }

    }

}
