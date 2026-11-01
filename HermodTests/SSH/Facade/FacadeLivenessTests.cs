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

using System.Diagnostics;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The <see cref="SshClient"/> façade's two ways of not waiting for ever: a <c>ConnectTimeout</c> for a
    /// server that takes the connection and says nothing, and keepalive probes for one that goes silent
    /// later - as OpenSSH's <c>ConnectTimeout</c> and <c>ServerAliveInterval</c>/<c>ServerAliveCountMax</c>.
    /// </summary>
    [TestFixture]
    [Category("Loopback")]
    public class FacadeLivenessTests
    {

        #region (private) helpers

        private static readonly IIPAddress Loopback = IPv4Address.Localhost;

        private static async Task WithServerAsync(ISshUserAuthenticator                     Authenticator,
                                                  Func<UInt16, Func<Byte[], Boolean>, Task> Test,
                                                  CancellationToken                         CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();

            var server  = new SshServer(new SshServerOptions {
                              HostKeys       = [ hostKey ],
                              Authenticator  = Authenticator,
                              ExecHandler    = async (ctx, ct) => { await ctx.WriteAsync($"ran: {ctx.Command}\n", ct); return 0; }
                          });

            try
            {
                await server.StartAsync(new IPSocket(Loopback, IPPort.Auto), CancellationToken);
                await Test((UInt16) server.LocalEndPoint.Port.ToInt32(), blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob));
            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        /// <summary>
        /// A server of our own making, one connection deep: it shakes hands and logs the client in, then
        /// either answers global requests (counting the keepalives) or - <paramref name="GoSilent"/> -
        /// reads nothing more at all, which is what a dead server looks like from the client.
        /// </summary>
        private sealed class HandMadeServer : IAsyncDisposable
        {

            private readonly SshTcpListener           listener;
            private readonly CancellationTokenSource  cts = new ();
            private readonly Task                     serving;
            private          Int32                    keepAlives;

            public ISshHostKey  HostKey     { get; } = SshHostKey.GenerateEd25519();
            public ISshHostKey  ClientKey   { get; } = SshHostKey.GenerateEd25519();
            public Int32        KeepAlives  => Volatile.Read(ref keepAlives);

            public UInt16 Port
                => (UInt16) listener.LocalEndPoint.Port.ToInt32();

            public HandMadeServer(Boolean GoSilent)
            {
                listener = SshTcpListener.Start(new IPSocket(Loopback, IPPort.Auto));
                serving  = ServeAsync(GoSilent);
            }

            private async Task ServeAsync(Boolean GoSilent)
            {
                try
                {

                    var pipe      = await listener.AcceptAsync(cts.Token);
                    var transport = await SshTransport.ServerHandshakeAsync(pipe, HostKey, CancellationToken: cts.Token);

                    await UserAuthentication.ServerAuthenticateAsync(transport, SshUserAuthenticator.ForAuthorizedKeys(ClientKey.PublicKeyBlob), CancellationToken: cts.Token);

                    if (GoSilent)
                    {
                        await Task.Delay(Timeout.Infinite, cts.Token);
                        return;
                    }

                    await using var mux = new SshChannelMultiplexer(transport) {
                                              GlobalRequestHandler = info => {
                                                  if (info.Name == SshClient.KeepAliveRequestName)
                                                      Interlocked.Increment(ref keepAlives);
                                                  return ValueTask.FromResult(false);   // as sshd: it does not know the request
                                              }
                                          };

                    mux.Start();
                    await Task.Delay(Timeout.Infinite, cts.Token);

                }
                catch (OperationCanceledException)
                { }
            }

            public async ValueTask DisposeAsync()
            {
                await cts.CancelAsync();
                try { await serving; } catch { }
                listener.Dispose();
                cts.Dispose();
            }

        }

        #endregion


        #region ConnectTimeout_ASilentServer_IsATimeout

        /// <summary>
        /// A server that takes the TCP connection and never sends its version: without a limit the client
        /// waits for as long as the socket lives.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task ConnectTimeout_ASilentServer_IsATimeout(CancellationToken CancellationToken)
        {

            using var listener = SshTcpListener.Start(new IPSocket(Loopback, IPPort.Auto));
            var accepting      = listener.AcceptAsync(CancellationToken);
            var watch          = Stopwatch.StartNew();

            var connecting     = SshClient.ConnectAsync("127.0.0.1", (UInt16) listener.LocalEndPoint.Port.ToInt32(), new SshClientOptions {
                                     Username        = "achim",
                                     VerifyHostKey   = SshHostKeyVerification.AcceptAnyUnsafe,
                                     ConnectTimeout  = TimeSpan.FromMilliseconds(500)
                                 }, CancellationToken).AsTask();

            var accepted       = await accepting;

            Assert.That(async () => await connecting, Throws.TypeOf<TimeoutException>().With.Message.Contains("within 0.5 s"));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));

            // The client let the socket go: the silent server reads the end of the stream. Its version
            // line came first - the client speaks first, then waits.
            while (true)
            {
                var read = await accepted.Input.ReadAsync(CancellationToken);
                accepted.Input.AdvanceTo(read.Buffer.End);
                if (read.IsCompleted)
                    break;
            }

        }

        #endregion

        #region ConnectTimeout_TheCallersCancellation_StaysACancellation

        [Test]
        [CancelAfter(20000)]
        public async Task ConnectTimeout_TheCallersCancellation_StaysACancellation(CancellationToken CancellationToken)
        {

            using var listener = SshTcpListener.Start(new IPSocket(Loopback, IPPort.Auto));
            _                  = listener.AcceptAsync(CancellationToken);

            using var caller   = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            caller.CancelAfter(TimeSpan.FromMilliseconds(300));

            Assert.That(async () => await SshClient.ConnectAsync("127.0.0.1", (UInt16) listener.LocalEndPoint.Port.ToInt32(), new SshClientOptions {
                                              Username        = "achim",
                                              VerifyHostKey   = SshHostKeyVerification.AcceptAnyUnsafe,
                                              ConnectTimeout  = TimeSpan.FromSeconds(30)
                                          }, caller.Token),
                        Throws.InstanceOf<OperationCanceledException>());

        }

        #endregion

        #region ConnectTimeout_DoesNotCountTheTimeAPersonTakes

        /// <summary>
        /// A keyboard-interactive prompt answered after the connect timeout has long passed: the timeout
        /// bounds the connection and the key exchange, never a person typing.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task ConnectTimeout_DoesNotCountTheTimeAPersonTakes(CancellationToken CancellationToken)
        {

            var policy = new SshAuthenticationPolicy()
                             .WithKeyboardInteractive(_ => new OneQuestion());

            await WithServerAsync(policy, async (port, verify) => {

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username             = "achim",
                    VerifyHostKey        = verify,
                    ConnectTimeout       = TimeSpan.FromMilliseconds(500),
                    KeyboardInteractive  = async (challenge, ct) => {
                                               await Task.Delay(TimeSpan.FromSeconds(2), ct);
                                               return [ "424242" ];
                                           }
                }, CancellationToken);

                var result = await client.ExecuteAsync("whoami", CancellationToken);
                Assert.That(result.StandardOutput, Is.EqualTo("ran: whoami\n"));

            }, CancellationToken);

        }

        private sealed class OneQuestion : ISshKeyboardInteractiveFactor
        {
            public String                    Name         => "";
            public String                    Instruction  => "";
            public IReadOnlyList<SshPrompt>  Prompts      => [ new SshPrompt("Code: ", Echo: true) ];

            public ValueTask<Boolean> ValidateAsync(IReadOnlyList<String> Responses, CancellationToken CancellationToken = default)
                => ValueTask.FromResult(Responses.Count == 1 && Responses[0] == "424242");
        }

        #endregion


        #region KeepAlive_IsSentEveryInterval_AndARefusalIsAnAnswer

        [Test]
        [CancelAfter(20000)]
        public async Task KeepAlive_IsSentEveryInterval_AndARefusalIsAnAnswer(CancellationToken CancellationToken)
        {

            await using var server = new HandMadeServer(GoSilent: false);

            await using (var client = await SshClient.ConnectAsync("127.0.0.1", server.Port, new SshClientOptions {
                                                         Username           = "achim",
                                                         VerifyHostKey      = blob => blob.AsSpan().SequenceEqual(server.HostKey.PublicKeyBlob),
                                                         Credentials        = [ server.ClientKey ],
                                                         KeepAliveInterval  = TimeSpan.FromMilliseconds(100),
                                                         KeepAliveCountMax  = 2
                                                     }, CancellationToken))
            {

                // Every probe is refused, as sshd refuses it - and far more than two have gone by.
                while (server.KeepAlives < 6)
                    await Task.Delay(50, CancellationToken);

                // Refused, but answered: the connection is still there.
                Assert.That(await client.Multiplexer.SendGlobalRequestAsync("still-there@hermod", true, [], CancellationToken), Is.False);

            }

        }

        #endregion

        #region KeepAlive_WithoutTheOption_NothingIsSent

        [Test]
        [CancelAfter(20000)]
        public async Task KeepAlive_WithoutTheOption_NothingIsSent(CancellationToken CancellationToken)
        {

            await using var server = new HandMadeServer(GoSilent: false);

            await using (var client = await SshClient.ConnectAsync("127.0.0.1", server.Port, new SshClientOptions {
                                                         Username       = "achim",
                                                         VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(server.HostKey.PublicKeyBlob),
                                                         Credentials    = [ server.ClientKey ]
                                                     }, CancellationToken))
            {

                await Task.Delay(500, CancellationToken);

                // A request of our own, answered, so everything sent before it has been read.
                await client.Multiplexer.SendGlobalRequestAsync("still-there@hermod", true, [], CancellationToken);
                Assert.That(server.KeepAlives, Is.Zero);

            }

        }

        #endregion

        #region KeepAlive_ASilentServer_IsGivenUp

        /// <summary>
        /// The server logs us in and then reads nothing more, as a server that died or a network that
        /// dropped the connection: a command waiting for its channel fails with the reason, not for ever.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task KeepAlive_ASilentServer_IsGivenUp(CancellationToken CancellationToken)
        {

            await using var server = new HandMadeServer(GoSilent: true);

            var client = await SshClient.ConnectAsync("127.0.0.1", server.Port, new SshClientOptions {
                             Username           = "achim",
                             VerifyHostKey      = blob => blob.AsSpan().SequenceEqual(server.HostKey.PublicKeyBlob),
                             Credentials        = [ server.ClientKey ],
                             KeepAliveInterval  = TimeSpan.FromMilliseconds(200),
                             KeepAliveCountMax  = 2
                         }, CancellationToken);

            var watch = Stopwatch.StartNew();

            Assert.That(async () => await client.ExecuteAsync("whoami", CancellationToken),
                        Throws.TypeOf<TimeoutException>().With.Message.Contains("2 keepalive probe(s)"));

            Assert.Multiple(() => {

                Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));

                // Given up for good: what comes after learns the same.
                Assert.That(async () => await client.ExecuteAsync("whoami", CancellationToken),
                            Throws.TypeOf<TimeoutException>());

            });

            // And disposing what is already closed neither hangs nor throws.
            Assert.That(async () => await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), CancellationToken), Throws.Nothing);

        }

        #endregion

    }

}
