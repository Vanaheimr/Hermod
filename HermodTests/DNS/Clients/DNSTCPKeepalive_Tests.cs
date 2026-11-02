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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// RFC 7828's TIMEOUT belongs to the connection it arrived on.
    /// </summary>
    /// <remarks>
    /// §3.1: TIMEOUT is "an idle timeout value for the TCP connection". A TIMEOUT
    /// of 0 closes that connection once its outstanding answers are in (§3.2.2),
    /// and the next one is a connection like any other: RFC 7766 §5, "If the
    /// resolver already has an open TCP connection to the server, it SHOULD
    /// reuse this connection."
    /// </remarks>
    [TestFixture]
    public class DNSTCPKeepalive_Tests
    {

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

        private static String[] AddressesOf(DNSInfo<A> Answer)
            => [.. Answer.FilteredAnswers.Select(a => a.IPv4Address.ToString())];


        #region A_Timeout_Zero_Does_Not_Outlive_Its_Connection()

        [Test]
        public async Task A_Timeout_Zero_Does_Not_Outlive_Its_Connection()
        {

            // The first connection answers with a TIMEOUT of 0, every later one
            // without the option.
            await using var server = new ScriptedDNSTCPServer((connection, stream, query) =>
                                         ScriptedDNSTCPServer.WriteFrame(stream,
                                                                         ScriptedDNSTCPServer.Answer(query, connection == 1 ? (UInt16) 0 : null)));

            await using var client = new DNSTCPClient(IPv4Address.Localhost,
                                                      Port:          server.Port,
                                                      QueryTimeout:  Timeout);

            var first   = await client.Query<A>(DomainName.Parse("one.example."),   Timeout: Timeout);
            var second  = await client.Query<A>(DomainName.Parse("two.example."),   Timeout: Timeout);
            var third   = await client.Query<A>(DomainName.Parse("three.example."), Timeout: Timeout);

            Assert.Multiple(() => {

                foreach (var answer in new[] { first, second, third })
                    Assert.That(AddressesOf(answer), Is.EqualTo(new[] { "192.0.2.1" }));

                Assert.That(server.Connections,           Is.EqualTo(2),
                            "the first connection closed for its TIMEOUT 0, and the second, which never sent one, was reused");

                Assert.That(client.ServerKeepaliveTimeout, Is.Null,
                            "nothing was advertised on the connection in use");

            });

        }

        #endregion

        #region A_Late_Answer_On_A_Drained_Connection_Does_Not_Close_The_Next_One()

        [Test]
        public async Task A_Late_Answer_On_A_Drained_Connection_Does_Not_Close_The_Next_One()
        {

            // The first connection answers the first query with a TIMEOUT of 0 and
            // holds the answer to the second until the third query has been asked:
            // the third waits for that answer, closes the connection and opens the
            // next. The late answer is read on the old connection afterwards.
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var asked   = 0;

            await using var server = new ScriptedDNSTCPServer(async (connection, stream, query) => {

                if (connection == 1 && Interlocked.Increment(ref asked) == 2)
                    await release.Task;

                await ScriptedDNSTCPServer.WriteFrame(stream,
                                                      ScriptedDNSTCPServer.Answer(query, connection == 1 && Volatile.Read(ref asked) == 1 ? (UInt16) 0 : null));

            });

            await using var client = new DNSTCPClient(IPv4Address.Localhost,
                                                      Port:          server.Port,
                                                      QueryTimeout:  Timeout);

            var first   = client.Query<A>(DomainName.Parse("one.example."),   Timeout: Timeout);
            var second  = client.Query<A>(DomainName.Parse("two.example."),   Timeout: Timeout);

            await first;

            var third   = client.Query<A>(DomainName.Parse("three.example."), Timeout: Timeout);

            await Task.Delay(TimeSpan.FromMilliseconds(250));
            release.SetResult();

            var answers = await Task.WhenAll(second, third);
            var fourth  = await client.Query<A>(DomainName.Parse("four.example."), Timeout: Timeout);

            Assert.Multiple(() => {

                Assert.That(AddressesOf(answers[0]), Is.EqualTo(new[] { "192.0.2.1" }), "the second query, answered on the first connection");
                Assert.That(AddressesOf(answers[1]), Is.EqualTo(new[] { "192.0.2.1" }), "the third query, asked while the first connection drained");
                Assert.That(AddressesOf(fourth),     Is.EqualTo(new[] { "192.0.2.1" }), "the fourth query");

                Assert.That(server.Connections,      Is.EqualTo(2),
                            "the third and the fourth query share the second connection");

            });

        }

        #endregion

    }

}
