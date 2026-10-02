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

using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Interactive shell sessions on the server façade: a conversation for as long as the handler runs,
    /// with the terminal it was asked for, what is typed at it, its new sizes and signals as they come,
    /// and an orderly end - by the client, by the handler, or by the server stopping.
    /// </summary>
    [TestFixture]
    public class ShellSessionTests
    {

        #region (private) Data

        private static readonly SshPty Xterm = new ("xterm", new SshWindowSize(100, 30), new Dictionary<Byte, UInt32> { [SshPty.VERASE] = 127 });

        #endregion

        #region (private) Serve(Options, Key) / Connect(Server, HostKey, UserKey)

        private sealed record Running(SshServer Server, ISshHostKey HostKey, ISshHostKey UserKey, CollectingAuditSink Audit) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => Server.DisposeAsync();
        }

        /// <summary>
        /// A server on a free loopback port, with the given options on top of a host key, a user key and
        /// an audit sink.
        /// </summary>
        private static async Task<Running> Serve(Func<SshServerOptions, SshServerOptions> Options,
                                                 CancellationToken                        CancellationToken,
                                                 String?                                  AuthorizedKeyOptions = null)
        {

            var hostKey   = SshHostKey.GenerateEd25519();
            var userKey   = SshHostKey.GenerateEd25519();
            var audit     = new CollectingAuditSink();
            var line      = (AuthorizedKeyOptions is not null ? AuthorizedKeyOptions + " " : "") + SshPublicKey.FromHostKey(userKey, "tester").ToAuthorizedKeyLine();

            var server    = new SshServer(Options(new SshServerOptions {
                                HostKeys       = [ hostKey ],
                                Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(AuthorizedKeysFile.Parse(line)),
                                AuditSink      = audit
                            }));

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return new Running(server, hostKey, userKey, audit);

        }

        private static ValueTask<SshClient> Connect(Running Running, CancellationToken CancellationToken)

            => SshClient.ConnectAsync("127.0.0.1",
                                      (UInt16) Running.Server.LocalEndPoint.Port.ToInt32(),
                                      new SshClientOptions {
                                          Username       = "achim",
                                          VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(Running.HostKey.PublicKeyBlob),
                                          Credentials    = [ Running.UserKey ]
                                      },
                                      CancellationToken);

        /// <summary>
        /// Read what the shell writes until it contains the given text.
        /// </summary>
        private static async Task<String> ReadUntil(SshClientShell Shell, String Text, CancellationToken CancellationToken)
        {

            var received = new StringBuilder();
            var buffer   = new Byte[4096];

            while (!received.ToString().Contains(Text, StringComparison.Ordinal))
            {
                var read = await Shell.Output.ReadAsync(buffer, CancellationToken);
                if (read == 0)
                    break;
                received.Append(Encoding.UTF8.GetString(buffer, 0, read));
            }

            return received.ToString();

        }

        /// <summary>
        /// Read what the shell writes until it ends.
        /// </summary>
        private static async Task<String> ReadToEnd(SshClientShell Shell, CancellationToken CancellationToken)
        {
            using var reader = new StreamReader(Shell.Output, Encoding.UTF8);
            return await reader.ReadToEndAsync(CancellationToken);
        }

        #endregion


        #region AShellIsAConversationWithTheTerminalItWasAskedFor

        /// <summary>
        /// The handler gets the terminal that was asked for, the environment, who signed in with which key
        /// and from where - and what is typed, as it is typed; what it writes reaches the client, and its
        /// return value is the exit status.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task AShellIsAConversationWithTheTerminalItWasAskedFor(CancellationToken CancellationToken)
        {

            SshShellContext? seen = null;

            await using var running = await Serve(options => options with {
                                          ShellHandler = async (context, ct) => {
                                              seen = context;
                                              var buffer = new Byte[64];
                                              var typed  = new StringBuilder();
                                              while (!typed.ToString().EndsWith('\r'))
                                              {
                                                  var read = await context.Input.ReadAsync(buffer, ct);
                                                  if (read == 0) break;
                                                  typed.Append(Encoding.UTF8.GetString(buffer, 0, read));
                                              }
                                              await context.WriteAsync($"you typed: {typed.ToString().TrimEnd('\r')}\r\n", ct);
                                              return 7;
                                          }
                                      }, CancellationToken);

            await using var client  = await Connect(running, CancellationToken);
            await using var shell   = await client.OpenShellAsync(Xterm, new Dictionary<String, String> { ["LANG"] = "de_DE.UTF-8" }, CancellationToken);

            await shell.WriteAsync("hello\r", CancellationToken);

            var output = await ReadToEnd(shell, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(shell.HasPty,                         Is.True);
                Assert.That(output,                               Is.EqualTo("you typed: hello\r\n"));
                Assert.That(shell.ExitStatus.Result,              Is.EqualTo(7));
                Assert.That(seen!.Pty!.Term,                      Is.EqualTo("xterm"));
                Assert.That(seen.Size,                            Is.EqualTo(new SshWindowSize(100, 30)));
                Assert.That(seen.Pty.Modes[SshPty.VERASE],        Is.EqualTo(127));
                Assert.That(seen.Environment["LANG"],             Is.EqualTo("de_DE.UTF-8"));
                Assert.That(seen.Session.Username,                Is.EqualTo("achim"));
                Assert.That(seen.Session.Method,                  Is.EqualTo("publickey"));
                Assert.That(seen.Session.PublicKeyType,           Is.EqualTo("ssh-ed25519"));
                Assert.That(seen.Session.PublicKeyFingerprint,    Is.EqualTo(SshFingerprint.Sha256(running.UserKey.PublicKeyBlob)));
                Assert.That(seen.Session.Peer?.IPAddress.ToString(), Is.EqualTo("127.0.0.1"));
                Assert.That(seen.Session.ClientIdentification?.SoftwareVersion, Does.StartWith("HermodSSH"));
            });

        }

        #endregion

        #region ANewSizeAndASignalReachTheShellWhileItRuns

        /// <summary>
        /// A resized terminal and a signal reach the handler while it runs - the requests are not left
        /// waiting until it ends, which is what a shell run like an exec did.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task ANewSizeAndASignalReachTheShellWhileItRuns(CancellationToken CancellationToken)
        {

            await using var running = await Serve(options => options with {
                                          ShellHandler = async (context, ct) => {
                                              var resized   = new TaskCompletionSource<SshWindowSize>(TaskCreationOptions.RunContinuationsAsynchronously);
                                              var signalled = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);
                                              context.WindowChanged += size => resized.TrySetResult(size);
                                              context.Signalled     += name => signalled.TrySetResult(name);
                                              await context.WriteAsync("ready\r\n", ct);
                                              var size = await resized.Task.WaitAsync(ct);
                                              await context.WriteAsync($"now {size.Columns}x{size.Rows}, and Size says {context.Size?.Columns}\r\n", ct);
                                              var name = await signalled.Task.WaitAsync(ct);
                                              await context.WriteAsync($"signal {name}\r\n", ct);
                                              return 0;
                                          }
                                      }, CancellationToken);

            await using var client  = await Connect(running, CancellationToken);
            await using var shell   = await client.OpenShellAsync(Xterm, CancellationToken: CancellationToken);

            await ReadUntil(shell, "ready", CancellationToken);

            await shell.ResizeAsync(new SshWindowSize(132, 43), CancellationToken);
            await shell.SignalAsync("INT", CancellationToken);

            var output = await ReadToEnd(shell, CancellationToken);

            Assert.That(output, Is.EqualTo("now 132x43, and Size says 132\r\nsignal INT\r\n"));
            Assert.That(await shell.ExitStatus, Is.EqualTo(0));

        }

        #endregion

        #region AClosedWindowEndsTheShell

        /// <summary>
        /// A client that closes its channel - a PuTTY window closed - ends the shell: its token fires, and
        /// it is told that it was the client, not the server stopping.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task AClosedWindowEndsTheShell(CancellationToken CancellationToken)
        {

            var ended = new TaskCompletionSource<(Boolean Closed, Boolean Stopping)>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var running = await Serve(options => options with {
                                          ShellHandler = async (context, ct) => {
                                              await context.WriteAsync("ready\r\n", ct);
                                              try
                                              {
                                                  await Task.Delay(Timeout.Infinite, ct);
                                              }
                                              catch (OperationCanceledException)
                                              {
                                                  ended.TrySetResult((context.Closed.IsCancellationRequested, context.Stopping.IsCancellationRequested));
                                              }
                                              return 0;
                                          }
                                      }, CancellationToken);

            await using var client  = await Connect(running, CancellationToken);
            var shell               = await client.OpenShellAsync(Xterm, CancellationToken: CancellationToken);

            await ReadUntil(shell, "ready", CancellationToken);
            await shell.CloseAsync(CancellationToken);

            var (closed, stopping) = await ended.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

            Assert.Multiple(() => {
                Assert.That(closed,    Is.True,  "the handler was not told the channel closed");
                Assert.That(stopping,  Is.False, "the handler was told the server is stopping");
            });

        }

        #endregion

        #region AStoppingServerLetsTheShellSayGoodbye

        /// <summary>
        /// A server that stops tells the shell through its token while the channel is still open: the
        /// shell's goodbye and its exit status reach the client, and then the connection ends with a
        /// DISCONNECT that says why.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AStoppingServerLetsTheShellSayGoodbye(CancellationToken CancellationToken)
        {

            var running = await Serve(options => options with {
                              ShellHandler = async (context, ct) => {
                                  await context.WriteAsync("ready\r\n", CancellationToken.None);
                                  try
                                  {
                                      await Task.Delay(Timeout.Infinite, ct);
                                  }
                                  catch (OperationCanceledException) when (context.Stopping.IsCancellationRequested)
                                  {
                                      await context.WriteAsync("The server is shutting down.\r\n", CancellationToken.None);
                                  }
                                  return 3;
                              }
                          }, CancellationToken);

            await using var client  = await Connect(running, CancellationToken);
            await using var shell   = await client.OpenShellAsync(Xterm, CancellationToken: CancellationToken);

            await ReadUntil(shell, "ready", CancellationToken);

            var stopped = running.Server.DisposeAsync().AsTask();

            var output  = await ReadToEnd(shell, CancellationToken);

            await stopped.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);

            Assert.Multiple(() => {
                Assert.That(output,                          Is.EqualTo("The server is shutting down.\r\n"));
                Assert.That(shell.ExitStatus.Result,         Is.EqualTo(3));
                Assert.That(running.Server.ConnectionCount,  Is.Zero);
                Assert.That(running.Audit.Events.OfType<DisconnectedEvent>().Select(e => e.Description),
                            Does.Contain("the server is shutting down"));
            });

        }

        #endregion

        #region OnlyTheShellIsServed

        /// <summary>
        /// A server with a shell handler and nothing else serves the shell and nothing else: exec, SFTP and
        /// a tunnel are refused, and the refusals are recorded.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task OnlyTheShellIsServed(CancellationToken CancellationToken)
        {

            await using var running = await Serve(options => options with {
                                          ShellHandler = (context, ct) => ValueTask.FromResult(0)
                                      }, CancellationToken);

            await using var client = await Connect(running, CancellationToken);

            var exec     = await client.Multiplexer.OpenChannelAsync("session", CancellationToken: CancellationToken);
            var execOk   = await exec.SendRequestAsync("exec", true, SshSessionChannel.EncodeString("cat /etc/passwd"), CancellationToken);

            var sftp     = await client.Multiplexer.OpenChannelAsync("session", CancellationToken: CancellationToken);
            var sftpOk   = await sftp.SendRequestAsync("subsystem", true, SshSessionChannel.EncodeString("sftp"), CancellationToken);

            Assert.Multiple(() => {
                Assert.That(execOk,  Is.False, "exec was served");
                Assert.That(sftpOk,  Is.False, "sftp was served");
            });

            Assert.That(async () => await client.OpenTcpStreamAsync("127.0.0.1", 22, CancellationToken),
                        Throws.TypeOf<SshChannelOpenException>(), "a tunnel was opened");

            var denied = running.Audit.Events.OfType<PolicyDeniedEvent>().Select(e => e.PolicyType).ToArray();

            Assert.That(denied, Is.SupersetOf(new[] { "exec", "subsystem", "direct-tcpip" }));

        }

        #endregion

        #region ACommandOnlyCredentialGetsNoShell

        /// <summary>
        /// A key confined to one command (<c>command="..."</c>) is not given the shell: with no exec
        /// handler to run its command, its shell request is refused rather than widened.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task ACommandOnlyCredentialGetsNoShell(CancellationToken CancellationToken)
        {

            var shellRan = false;

            await using var running = await Serve(options => options with {
                                          ShellHandler = (context, ct) => { shellRan = true; return ValueTask.FromResult(0); }
                                      },
                                      CancellationToken,
                                      AuthorizedKeyOptions: "command=\"uptime\"");

            await using var client = await Connect(running, CancellationToken);

            Assert.That(async () => await client.OpenShellAsync(Xterm, CancellationToken: CancellationToken),
                        Throws.TypeOf<InvalidOperationException>());

            Assert.That(shellRan, Is.False);

        }

        #endregion

        #region OneSessionPerConnectionWhereThatIsTheLimit

        /// <summary>
        /// Sessions beyond the limit are refused when they are opened.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task OneSessionPerConnectionWhereThatIsTheLimit(CancellationToken CancellationToken)
        {

            await using var running = await Serve(options => options with {
                                          ShellHandler  = async (context, ct) => { await Task.Delay(Timeout.Infinite, ct); return 0; },
                                          Limits        = new SshServerLimits { MaxSessions = 1 }
                                      }, CancellationToken);

            await using var client  = await Connect(running, CancellationToken);
            await using var first   = await client.OpenShellAsync(Xterm, CancellationToken: CancellationToken);

            Assert.That(async () => await client.OpenShellAsync(Xterm, CancellationToken: CancellationToken),
                        Throws.TypeOf<SshChannelOpenException>());

            Assert.That(running.Audit.Events.OfType<LimitExceededEvent>().Select(e => e.Limit), Does.Contain("sessions"));

        }

        #endregion

        #region SomebodyWhoSaysNothingIsClosedAfterTheGraceTime

        /// <summary>
        /// A connection that never says anything is closed once the login grace time is over, rather than
        /// holding its slot for ever.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task SomebodyWhoSaysNothingIsClosedAfterTheGraceTime(CancellationToken CancellationToken)
        {

            await using var running = await Serve(options => options with {
                                          ShellHandler  = (context, ct) => ValueTask.FromResult(0),
                                          Limits        = new SshServerLimits { LoginGraceTime = TimeSpan.FromSeconds(1) }
                                      }, CancellationToken);

            using var silent = new TcpClient();
            await silent.ConnectAsync("127.0.0.1", running.Server.LocalEndPoint.Port.ToInt32(), CancellationToken);

            var stream  = silent.GetStream();
            var buffer  = new Byte[1024];

            // The server's version line, and then nothing until it closes.
            var started = DateTime.UtcNow;
            while (await stream.ReadAsync(buffer, CancellationToken) > 0) { }

            Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(10)));
            Assert.That(running.Audit.Events.OfType<LimitExceededEvent>().Select(e => e.Limit), Does.Contain("login grace time"));

        }

        #endregion

        #region OneAddressCannotTakeEverySlot

        /// <summary>
        /// One address gets as many connections that have not authenticated as the limit says, and the
        /// next is closed at once - while one that authenticates frees its slot.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task OneAddressCannotTakeEverySlot(CancellationToken CancellationToken)
        {

            await using var running = await Serve(options => options with {
                                          ShellHandler  = (context, ct) => ValueTask.FromResult(0),
                                          Limits        = new SshServerLimits { MaxUnauthenticatedPerAddress = 1 }
                                      }, CancellationToken);

            var port = running.Server.LocalEndPoint.Port.ToInt32();

            using var first = new TcpClient();
            await first.ConnectAsync("127.0.0.1", port, CancellationToken);

            // Its version line, so that it is surely accepted and counted.
            var buffer = new Byte[256];
            await first.GetStream().ReadAsync(buffer, CancellationToken);

            using var second = new TcpClient();
            await second.ConnectAsync("127.0.0.1", port, CancellationToken);

            Assert.That(await second.GetStream().ReadAsync(buffer, CancellationToken), Is.Zero, "the second was served");

            first.Close();

            // Once the first has gone, a client gets in again.
            await Task.Delay(500, CancellationToken);
            await using var client = await Connect(running, CancellationToken);

        }

        #endregion

        #region AClientThatLeavesIsNoProtocolError

        /// <summary>
        /// A client that disconnects is recorded as having gone, and not as a protocol error - which every
        /// logout used to be.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task AClientThatLeavesIsNoProtocolError(CancellationToken CancellationToken)
        {

            await using var running = await Serve(options => options with {
                                          ShellHandler = (context, ct) => ValueTask.FromResult(0)
                                      }, CancellationToken);

            var client = await Connect(running, CancellationToken);
            await using (var shell = await client.OpenShellAsync(Xterm, CancellationToken: CancellationToken))
                await shell.ExitStatus;

            await client.DisposeAsync();

            var until = DateTime.UtcNow.AddSeconds(5);
            while (running.Audit.Events.OfType<ConnectionClosedEvent>().Any() == false && DateTime.UtcNow < until)
                await Task.Delay(50, CancellationToken);

            var disconnected = running.Audit.Events.OfType<DisconnectedEvent>().ToArray();

            Assert.Multiple(() => {
                Assert.That(disconnected,                  Is.Not.Empty);
                Assert.That(disconnected.Select(e => e.Code), Has.None.EqualTo((UInt32) DisconnectReason.ProtocolError));
                Assert.That(running.Audit.Events.OfType<SessionOpenedEvent>().Count(),  Is.EqualTo(1));
                Assert.That(running.Audit.Events.OfType<SessionClosedEvent>().Count(),  Is.EqualTo(1));
                Assert.That(running.Audit.Events.Select(e => e.ConnectionId).Distinct(), Has.Exactly(1).Items, "every event of one connection carries its id");
            });

        }

        #endregion

        #region ThePseudoTerminalIsReadAsItIsSent

        /// <summary>
        /// A pty-req as a client writes it is read back as it was: the terminal, its size and its modes -
        /// up to the end marker, and no further.
        /// </summary>
        [Test]
        public void ThePseudoTerminalIsReadAsItIsSent()
        {

            var pty  = new SshPty("xterm-256color", new SshWindowSize(80, 24, 640, 480),
                                  new Dictionary<Byte, UInt32> { [SshPty.VINTR] = 3, [SshPty.VERASE] = 127, [128] = 38400 });

            var read = SshPty.Parse(pty.ToPayload());

            Assert.Multiple(() => {
                Assert.That(read.Term,   Is.EqualTo("xterm-256color"));
                Assert.That(read.Size,   Is.EqualTo(new SshWindowSize(80, 24, 640, 480)));
                Assert.That(read.Modes,  Is.EquivalentTo(pty.Modes));
            });

            // An opcode without a defined argument ends what can be read.
            Assert.That(SshPty.ParseModes([ 3, 0, 0, 0, 127, 200, 1, 2, 3, 4, 5 ]), Is.EquivalentTo(new Dictionary<Byte, UInt32> { [3] = 127 }));

        }

        #endregion

    }

}
