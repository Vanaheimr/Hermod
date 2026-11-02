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
    /// A TCP connection that ends before the answers on it are in.
    /// </summary>
    /// <remarks>
    /// RFC 7766 §6.2.4: "DNS clients SHOULD retry unanswered queries if the
    /// connection closes before receiving all outstanding responses."
    /// </remarks>
    [TestFixture]
    public class DNSTCPTeardown_Tests
    {

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);


        #region A_Query_Whose_Answer_Was_Cut_Off_Is_Asked_Again_On_A_New_Connection()

        [Test]
        public async Task A_Query_Whose_Answer_Was_Cut_Off_Is_Asked_Again_On_A_New_Connection()
        {

            // The first connection gets the length of the answer and half of its
            // octets, and then an orderly close: the client reads a 0 in the middle
            // of the message. The second one answers.
            await using var server = new ScriptedDNSTCPServer(async (connection, stream, query) => {

                var answer = ScriptedDNSTCPServer.Answer(query);

                if (connection > 1)
                {
                    await ScriptedDNSTCPServer.WriteFrame(stream, answer);
                    return;
                }

                await stream.WriteAsync(new[] { (Byte) (answer.Length >> 8), (Byte) answer.Length });
                await stream.WriteAsync(answer.AsMemory(0, answer.Length / 2));
                await stream.FlushAsync();
                stream.Close();

            });

            await using var client = new DNSTCPClient(IPv4Address.Localhost,
                                                      Port:          server.Port,
                                                      QueryTimeout:  Timeout);

            var answer = await client.Query<A>(DomainName.Parse("cut.example."), Timeout: Timeout);

            Assert.Multiple(() => {
                Assert.That(answer.FilteredAnswers.Select(a => a.IPv4Address.ToString()), Is.EqualTo(new[] { "192.0.2.1" }),
                            "the query was asked again, and answered");
                Assert.That(server.Connections, Is.EqualTo(2));
            });

        }

        #endregion

    }

}
