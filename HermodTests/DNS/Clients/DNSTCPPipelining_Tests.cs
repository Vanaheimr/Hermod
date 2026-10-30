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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// Queries pipelined on one TCP connection.
    /// </summary>
    /// <remarks>
    /// RFC 7766 §6.2.1.1: "When a DNS client sends multiple queries to a server,
    /// it SHOULD NOT wait for an outstanding reply before sending the next query."
    /// And §7: responses may "arrive in a different order than that in which the
    /// requests were sent". The server here reads both queries before it answers
    /// either, and answers them in the reverse order.
    /// </remarks>
    [TestFixture]
    public class DNSTCPPipelining_Tests
    {

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);


        /// <summary>
        /// A server that reads two framed queries on one connection, then answers
        /// the second before the first: an A record whose address is the query's
        /// position, 1 or 2.
        /// </summary>
        private sealed class TwoThenAnswer : IAsyncDisposable
        {

            private readonly TcpListener              listener  = new (System.Net.IPAddress.Loopback, 0);
            private readonly CancellationTokenSource  stop      = new();
            private readonly Task                     serving;

            public IPPort   Port                 { get; }
            public Boolean  BothBeforeAnswering  { get; private set; }

            public TwoThenAnswer(TimeSpan Window)
            {
                listener.Start();
                Port    = IPPort.Parse(((IPEndPoint) listener.LocalEndpoint).Port);
                serving = Serve(Window);
            }

            private async Task Serve(TimeSpan Window)
            {

                try
                {

                    using var connection = await listener.AcceptTcpClientAsync(stop.Token);
                    var       stream     = connection.GetStream();
                    var       queries    = new List<Byte[]>();

                    using (var window = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                    {

                        window.CancelAfter(Window);

                        try
                        {
                            while (queries.Count < 2)
                                queries.Add(await ReadFrame(stream, window.Token));
                        }
                        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
                        { }

                    }

                    BothBeforeAnswering = queries.Count == 2;

                    // A client that waits sends its second query only now.
                    if (queries.Count < 2)
                    {
                        await WriteFrame(stream, Answer(queries[0], 1), stop.Token);
                        queries.Add(await ReadFrame(stream, stop.Token));
                        await WriteFrame(stream, Answer(queries[1], 2), stop.Token);
                    }
                    else
                    {
                        await WriteFrame(stream, Answer(queries[1], 2), stop.Token);
                        await WriteFrame(stream, Answer(queries[0], 1), stop.Token);
                    }

                    await Task.Delay(System.Threading.Timeout.Infinite, stop.Token);

                }
                catch (OperationCanceledException) { }
                catch (IOException)                { }

            }

            private static async Task<Byte[]> ReadFrame(NetworkStream Stream, CancellationToken Token)
            {
                var length = new Byte[2];
                await Stream.ReadExactlyAsync(length, Token);
                var message = new Byte[(length[0] << 8) | length[1]];
                await Stream.ReadExactlyAsync(message, Token);
                return message;
            }

            private static async Task WriteFrame(NetworkStream Stream, Byte[] Message, CancellationToken Token)
            {
                await Stream.WriteAsync(new[] { (Byte) (Message.Length >> 8), (Byte) Message.Length }, Token);
                await Stream.WriteAsync(Message, Token);
                await Stream.FlushAsync(Token);
            }

            /// <summary>
            /// The query turned round, with one A record pointing back at its
            /// question name: 192.0.2.Position.
            /// </summary>
            private static Byte[] Answer(Byte[] Query, Byte Position)
            {

                // The question ends after the name, its type and its class; an
                // OPT record after it is left out of the answer.
                var end = 12;
                while (Query[end] != 0)
                    end += 1 + Query[end];
                end += 5;

                var answer = new List<Byte>(Query[..end]);
                answer[2]  = (Byte) (answer[2] | 0x80);   // QR
                answer[3]  = 0x80;                        // RA, NOERROR
                answer[6]  = 0; answer[7]  = 1;           // ANCOUNT 1
                answer[8]  = 0; answer[9]  = 0;
                answer[10] = 0; answer[11] = 0;           // ARCOUNT 0

                answer.AddRange([ 0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2C, 0x00, 0x04, 192, 0, 2, Position ]);

                return [.. answer];

            }

            public async ValueTask DisposeAsync()
            {
                stop.Cancel();
                listener.Stop();
                await serving;
                stop.Dispose();
            }

        }


        #region Two_Queries_Are_On_The_Wire_Before_Either_Is_Answered()

        [Test]
        public async Task Two_Queries_Are_On_The_Wire_Before_Either_Is_Answered()
        {

            await using var server = new TwoThenAnswer(Window: TimeSpan.FromSeconds(1));
            await using var client = new DNSTCPClient(IPv4Address.Localhost,
                                                      Port:          server.Port,
                                                      QueryTimeout:  Timeout);

            var first   = client.Query<A>(DomainName.Parse("one.example."), Timeout: Timeout);
            var second  = client.Query<A>(DomainName.Parse("two.example."), Timeout: Timeout);

            var answers = await Task.WhenAll(first, second);

            Assert.Multiple(() => {

                Assert.That(server.BothBeforeAnswering, Is.True,
                            "the second query was written while the first was still unanswered");

                // Answered in reverse, so each query has to pick out its own.
                Assert.That(answers.Select(answer => answer.FilteredAnswers.Single().IPv4Address.ToString()),
                            Is.EquivalentTo(new[] { "192.0.2.1", "192.0.2.2" }));

                Assert.That(answers[0].FilteredAnswers.Single().DomainName.FullName.TrimEnd('.'), Is.EqualTo("one.example"));
                Assert.That(answers[1].FilteredAnswers.Single().DomainName.FullName.TrimEnd('.'), Is.EqualTo("two.example"));

            });

        }

        #endregion

    }

}
