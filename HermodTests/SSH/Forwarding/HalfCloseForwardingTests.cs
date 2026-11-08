/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using System.Net;
using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// A half-close crosses every forward, whichever side makes it: the bytes on their way in the other
    /// direction still arrive. Through <c>-L</c> it crosses the client's listener and the server's
    /// <c>direct-tcpip</c> dial; through <c>-R</c>, the server's listener and the client's dial.
    /// </summary>
    [TestFixture]
    public class HalfCloseForwardingTests
    {

        #region Data

        private const String Question  = "a question that is only complete at its end";
        private const String Greeting  = "the target speaks first and then stops speaking";
        private const String LateWords = "words sent after the target stopped speaking";

        #endregion


        #region TheTargetsAnswer_ArrivesAfterTheEndOfTheQuestion

        /// <summary>
        /// The connecting side sends, then shuts down its sending side; the target reads to the end and
        /// only then answers. The answer arrives only if the end of input crossed the forward as an end of
        /// input, and the connection outlived it.
        /// </summary>
        [TestCase("-L")]
        [TestCase("-R")]
        [CancelAfter(25000)]
        public async Task TheTargetsAnswer_ArrivesAfterTheEndOfTheQuestion(String Direction, CancellationToken CancellationToken)
        {

            await using var target = new Target(async connection => {
                var stream   = connection.GetStream();
                var question = await ReadToEndAsync(stream, CancellationToken);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(new String(question.Reverse().ToArray())), CancellationToken);
            }, CancellationToken);

            await ThroughAForwardAsync(Direction, target.Port, async port => {

                using var local = new TcpClient();
                await local.ConnectAsync(IPAddress.Loopback, port, CancellationToken);

                var stream = local.GetStream();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(Question), CancellationToken);
                local.Client.Shutdown(SocketShutdown.Send);

                Assert.That(await ReadToEndAsync(stream, CancellationToken), Is.EqualTo(new String(Question.Reverse().ToArray())),
                            "the target's answer to the whole question");

            }, CancellationToken);

        }

        #endregion

        #region WhatIsSentAfterTheTargetStoppedSpeaking_StillArrives

        /// <summary>
        /// The other way round: the target speaks, shuts down its sending side, and goes on reading. The
        /// connecting side reads to that end, and only then sends; the target still receives it.
        /// </summary>
        [TestCase("-L")]
        [TestCase("-R")]
        [CancelAfter(25000)]
        public async Task WhatIsSentAfterTheTargetStoppedSpeaking_StillArrives(String Direction, CancellationToken CancellationToken)
        {

            // Whatever each connection sent the target; the forward's is the one the late words reached.
            var received   = new ConcurrentQueue<String>();
            var lateWords  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var target = new Target(async connection => {
                var stream = connection.GetStream();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(Greeting), CancellationToken);
                connection.Client.Shutdown(SocketShutdown.Send);
                var text = await ReadToEndAsync(stream, CancellationToken);
                received.Enqueue(text);
                if (text == LateWords)
                    lateWords.TrySetResult();
            }, CancellationToken);

            await ThroughAForwardAsync(Direction, target.Port, async port => {

                using var local = new TcpClient();
                await local.ConnectAsync(IPAddress.Loopback, port, CancellationToken);

                var stream = local.GetStream();

                Assert.That(await ReadToEndAsync(stream, CancellationToken), Is.EqualTo(Greeting), "everything the target said");

                await stream.WriteAsync(Encoding.UTF8.GetBytes(LateWords), CancellationToken);
                local.Client.Shutdown(SocketShutdown.Send);

                try
                {
                    await lateWords.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
                }
                catch (TimeoutException)
                {
                    Assert.Fail($"What was sent after the target's end did not reach it; it received {received.Count} time(s): " +
                                String.Join(", ", received.Select(text => $"'{text}'")));
                }

            }, CancellationToken);

        }

        #endregion


        #region (private) helpers

        #region (private class) Target

        /// <summary>
        /// The target of a forward, on a loopback port of its own, serving every connection made to it
        /// until disposed - not the first alone. The forward's connection need not be the first: the port
        /// was free a moment ago, and a test of another process on this machine may keep a port it was
        /// handed back as one where nobody listens. ChargyCore's AnUnreachableEndpointIsPassedOver polls
        /// such a port for seconds, and Windows hands ports out in ascending order: once the counter has
        /// come round, the port is given to this listener, and the poll reaches it. A target that took
        /// only the first connection read that stranger's request to its end, and the forward's waited
        /// in the backlog until CancelAfter.
        /// </summary>
        private sealed class Target : IAsyncDisposable
        {

            private readonly TcpListener                 listener     = new (IPAddress.Loopback, 0);
            private readonly ConcurrentQueue<TcpClient>  connections  = new ();
            private readonly ConcurrentQueue<Task>       serving      = new ();
            private readonly Task                        accepting;

            /// <summary>
            /// The port the target listens on.
            /// </summary>
            public UInt16 Port { get; }

            /// <summary>
            /// Listen, and serve each connection with <paramref name="Serve"/>, which closes it when done.
            /// </summary>
            public Target(Func<TcpClient, Task> Serve, CancellationToken CancellationToken)
            {

                listener.Start();
                Port = (UInt16) ((IPEndPoint) listener.LocalEndpoint).Port;

                accepting = Task.Run(async () => {
                    try
                    {
                        while (true)
                        {
                            var connection = await listener.AcceptTcpClientAsync(CancellationToken);
                            connections.Enqueue(connection);
                            serving.Enqueue(Task.Run(async () => {
                                using (connection)
                                    await Serve(connection);
                            }));
                        }
                    }
                    catch
                    {
                        // Stopped, or the test is over.
                    }
                });

            }

            /// <summary>
            /// Stop listening, and end the connections still served - a stranger's may wait for ever.
            /// </summary>
            public async ValueTask DisposeAsync()
            {

                listener.Stop();
                await accepting;

                foreach (var connection in connections)
                    connection.Dispose();

                foreach (var served in serving)
                {
                    try   { await served; }
                    catch { }
                }

            }

        }

        #endregion

        /// <summary>
        /// Our client and our server, with a forward to <paramref name="TargetPort"/> in the given
        /// direction; <paramref name="Exercise"/> gets the port to connect to.
        /// </summary>
        private static async Task ThroughAForwardAsync(String Direction, UInt16 TargetPort, Func<Int32, Task> Exercise, CancellationToken CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys         = [ hostKey ],
                Authenticator    = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ForwardingPolicy = ForwardingPolicy.LoopbackOnly
            });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

                await using var client = await SshClient.ConnectAsync("127.0.0.1", (UInt16) server.LocalEndPoint.Port.ToInt32(), new SshClientOptions {
                    Username      = "achim",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ userKey ]
                }, CancellationToken);

                if (Direction == "-L")
                {
                    await using var forward = client.StartLocalForward(new IPSocket(IPv4Address.Localhost, IPPort.Auto), "127.0.0.1", TargetPort);
                    await Exercise(forward.LocalEndPoint.Port.ToInt32());
                }
                else
                {
                    await using var forward = await client.RequestRemoteForwardAsync("127.0.0.1", 0, "127.0.0.1", TargetPort, CancellationToken);
                    await Exercise(forward.BoundPort);
                }

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        private static async Task<String> ReadToEndAsync(Stream Stream, CancellationToken CancellationToken)
        {
            using var buffer = new MemoryStream();
            await Stream.CopyToAsync(buffer, CancellationToken);
            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        #endregion

    }

}
