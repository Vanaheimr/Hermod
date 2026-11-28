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

using System.Buffers;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// A subsystem that is a console for people rather than a protocol for programs - an admin console,
    /// <c>ssh -t -s station admin@...</c>: it is given the terminal the client asked for before it
    /// (<see cref="SshSubsystemContext.Terminal"/>), hears of every resize and signal while it runs, and
    /// may be granted by what a CA wrote into the certificate that signed in
    /// (<see cref="SshSessionInfo.Certificate"/>).
    /// </summary>
    [TestFixture]
    public class SubsystemTerminalTests
    {

        #region (private) Fixture

        private const String Console    = "console@test.open.charging.cloud";
        private const String Admin      = "admin@test.open.charging.cloud";
        private const String AdminGrant = "admin-console@test.open.charging.cloud";

        private static readonly SshPty Xterm = new ("xterm-256color", new SshWindowSize(80, 24), new Dictionary<Byte, UInt32>());

        /// <summary>
        /// Takes the one key, and confines the session as told.
        /// </summary>
        private sealed class Confining(Byte[] Key, SshSessionRestrictions Restrictions) : ISshUserAuthenticator
        {

            public IReadOnlyList<String> OfferedMethods { get; } = [ "publickey" ];

            public ValueTask<Boolean> AuthorizePublicKeyAsync(SshPublicKeyAuthRequest Request, CancellationToken CancellationToken = default)
                => ValueTask.FromResult(Request.PublicKeyBlob.AsSpan().SequenceEqual(Key));

            public ValueTask<SshSessionRestrictions> GetRestrictionsAsync(SshPublicKeyAuthRequest Request, CancellationToken CancellationToken = default)
                => ValueTask.FromResult(Restrictions);

        }

        /// <summary>
        /// A console: says what terminal it was given and who signed in, writes a line for every resize
        /// and signal as it comes, and ends when the client types "q".
        /// </summary>
        private static async ValueTask ServeConsoleAsync(SshSubsystemContext Context, CancellationToken CancellationToken)
        {

            // The events come on the task reading the channel's requests: queued, so that only this one
            // writes to the channel.
            var lines = System.Threading.Channels.Channel.CreateUnbounded<String>();

            Context.Terminal.WindowChanged += size => lines.Writer.TryWrite($"resized {size.Columns}x{size.Rows}, now {Context.Terminal.Size?.Columns}x{Context.Terminal.Size?.Rows}");
            Context.Terminal.Signalled     += name => lines.Writer.TryWrite($"signal {name}");

            var terminal = Context.Pty is SshPty pty
                               ? $"{pty.Term} {pty.Size.Columns}x{pty.Size.Rows}"
                               : "no terminal";

            await WriteLineAsync(Context, $"{terminal} for {Context.Session.Certificate?.KeyId ?? "no certificate"}", CancellationToken);

            _ = Task.Run(async () => {

                    var one = new Byte[1];

                    try
                    {
                        while (await Context.Channel.Input.ReadAsync(one, CancellationToken) > 0 && one[0] != (Byte) 'q')
                        { }
                    }
                    catch
                    { }

                    lines.Writer.TryComplete();

                }, CancellationToken.None);

            await foreach (var line in lines.Reader.ReadAllAsync(CancellationToken))
                await WriteLineAsync(Context, line, CancellationToken);

        }

        private static ValueTask WriteLineAsync(SshSubsystemContext Context, String Line, CancellationToken CancellationToken)
            => Context.Channel.SendDataAsync(Encoding.UTF8.GetBytes(Line + "\r\n"), CancellationToken);

        private static Byte[] EncodeString(String Text)
        {
            var abw = new ArrayBufferWriter<Byte>();
            new SshPacketWriter(abw).WriteString(Text);
            return abw.WrittenSpan.ToArray();
        }

        private static async Task<(SshServer Server, IPPort Port, ISshHostKey HostKey)> StartAsync(ISshUserAuthenticator  Authenticator,
                                                                                                      SshSubsystemFactory    Subsystems,
                                                                                                      CancellationToken      CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();

            var server  = new SshServer(new SshServerOptions {
                              HostKeys       = [ hostKey ],
                              Authenticator  = Authenticator,
                              ExecHandler    = async (ctx, ct) => { await ctx.WriteAsync("ran\n", ct); return 0; },
                              Subsystems     = Subsystems
                          });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return (server, server.LocalEndPoint.Port, hostKey);

        }

        private static ValueTask<SSHClient> ConnectAsync(IPPort Port, ISshHostKey HostKey, ISshHostKey Credential, CancellationToken CancellationToken)

            => SSHClient.Connect("127.0.0.1", Port, new SSHClientOptions {
                   Username       = User_Id.Parse("operator"),
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(HostKey.PublicKeyBlob),
                   Credentials    = [ Credential ]
               }, CancellationToken);

        private static readonly SshSubsystemFactory ServesTheConsole =

            (session, name, ct) => ValueTask.FromResult<SshSubsystemHandler?>(name == Console ? ServeConsoleAsync : null);

        #endregion


        #region Subsystem_WithATerminal_SeesItsPty_EveryResize_AndASignal

        /// <summary>
        /// <c>ssh -t -s</c>: the terminal asked for before the subsystem is the subsystem's, and a resize
        /// or a signal while it runs reaches it as it comes - not when it has ended.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Subsystem_WithATerminal_SeesItsPty_EveryResize_AndASignal(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();

            var (server, port, hostKey) = await StartAsync(new Confining(userKey.PublicKeyBlob, SshSessionRestrictions.None), ServesTheConsole, CancellationToken);
            await using var _      = server;
            await using var client = await ConnectAsync(port, hostKey, userKey, CancellationToken);

            var channel  = await client.OpenSubsystem(Console, Xterm, CancellationToken);
            using var reader = new StreamReader(channel.Input);

            var greeting = await reader.ReadLineAsync(CancellationToken);

            await channel.SendRequestAsync("window-change", false, new SshWindowSize(120, 40).ToWindowChangePayload(), CancellationToken);
            var resized  = await reader.ReadLineAsync(CancellationToken);

            await channel.SendRequestAsync("signal", false, EncodeString("INT"), CancellationToken);
            var signal   = await reader.ReadLineAsync(CancellationToken);

            await channel.SendDataAsync("q"u8.ToArray(), CancellationToken);
            var rest     = await reader.ReadToEndAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(greeting, Is.EqualTo("xterm-256color 80x24 for no certificate"), "the subsystem was not given the terminal, or a plain key was taken for a certificate");
                Assert.That(resized,  Is.EqualTo("resized 120x40, now 120x40"),             "a window-change did not reach the subsystem while it ran");
                Assert.That(signal,   Is.EqualTo("signal INT"),                             "a signal did not reach the subsystem while it ran");
                Assert.That(rest,     Is.Empty,                                             "the channel was not closed once the subsystem returned");
            });

        }

        #endregion

        #region Subsystem_WithoutATerminal_HasNone

        /// <summary>
        /// <c>ssh -s</c>, or SFTP: no terminal asked for, none given.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Subsystem_WithoutATerminal_HasNone(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();

            var (server, port, hostKey) = await StartAsync(new Confining(userKey.PublicKeyBlob, SshSessionRestrictions.None), ServesTheConsole, CancellationToken);
            await using var _      = server;
            await using var client = await ConnectAsync(port, hostKey, userKey, CancellationToken);

            var channel  = await client.OpenSubsystem(Console, CancellationToken);
            using var reader = new StreamReader(channel.Input);

            var greeting = await reader.ReadLineAsync(CancellationToken);
            await channel.SendDataAsync("q"u8.ToArray(), CancellationToken);

            Assert.That(greeting, Is.EqualTo("no terminal for no certificate"));

        }

        #endregion

        #region Subsystem_ForACredentialWithNoPty_IsServed_WithoutTheTerminal

        /// <summary>
        /// A credential with <c>no-pty</c> is refused the terminal, not the subsystem: the console runs,
        /// and knows it has no terminal. (<c>ssh -t</c>, told to have one, gives up at the refusal; a
        /// client that asks only where it can carries on - this one does.)
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Subsystem_ForACredentialWithNoPty_IsServed_WithoutTheTerminal(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();

            var (server, port, hostKey) = await StartAsync(new Confining(userKey.PublicKeyBlob, new SshSessionRestrictions(AllowPty: false)), ServesTheConsole, CancellationToken);
            await using var _      = server;
            await using var client = await ConnectAsync(port, hostKey, userKey, CancellationToken);

            var channel  = await client.OpenSubsystem(Console, Xterm, CancellationToken);
            using var reader = new StreamReader(channel.Input);

            var greeting = await reader.ReadLineAsync(CancellationToken);
            await channel.SendDataAsync("q"u8.ToArray(), CancellationToken);

            Assert.That(greeting, Is.EqualTo("no terminal for no certificate"), "a no-pty credential was given a terminal");

        }

        #endregion

        #region Subsystem_WhileItRuns_ARequestItCannotServe_IsRefused

        /// <summary>
        /// The channel's requests are read while the subsystem runs: a second request - an exec on the
        /// subsystem's channel - is refused at once, rather than left waiting for an answer until the
        /// subsystem ends.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Subsystem_WhileItRuns_ARequestItCannotServe_IsRefused(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();

            var (server, port, hostKey) = await StartAsync(new Confining(userKey.PublicKeyBlob, SshSessionRestrictions.None), ServesTheConsole, CancellationToken);
            await using var _      = server;
            await using var client = await ConnectAsync(port, hostKey, userKey, CancellationToken);

            var channel  = await client.OpenSubsystem(Console, Xterm, CancellationToken);
            using var reader = new StreamReader(channel.Input);

            await reader.ReadLineAsync(CancellationToken);

            var exec     = await channel.SendRequestAsync("exec", true, EncodeString("ls"), CancellationToken);
            var ptyAgain = await channel.SendRequestAsync("pty-req", true, Xterm.ToPayload(), CancellationToken);

            await channel.SendDataAsync("q"u8.ToArray(), CancellationToken);
            var rest     = await reader.ReadToEndAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(exec,     Is.False, "an exec on a subsystem's channel was not refused");
                Assert.That(ptyAgain, Is.False, "a second terminal was granted while the subsystem ran");
                Assert.That(rest,     Is.Empty, "the subsystem saw the refused requests");
            });

        }

        #endregion

        #region AdminConsole_IsGranted_ByWhatTheCaWroteIntoTheCertificate

        /// <summary>
        /// The admin console of a charging station: the CA grants it with an extension of its own in the
        /// operator's certificate, and the factory reads the certificate off the session - its key id
        /// says which operator it is. A certificate of the same CA without the extension is refused.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AdminConsole_IsGranted_ByWhatTheCaWroteIntoTheCertificate(CancellationToken CancellationToken)
        {

            var caKey  = SshHostKey.GenerateEd25519();
            var trust  = new SshCertificateAuthorityTrust().TrustCA(caKey);

            CertifiedKey Issue(String KeyId, params String[] Extensions)
            {

                var userKey = SshHostKey.GenerateEd25519();

                var builder = new OpenSshCertificateBuilder {
                    Serial       = 17,
                    Type         = SshCertType.User,
                    KeyId        = KeyId,
                    Principals   = [ "operator" ],
                    ValidAfter   = DateTimeOffset.UtcNow.AddDays(-1),
                    ValidBefore  = DateTimeOffset.UtcNow.AddDays(1)
                };

                foreach (var extension in OpenSshCertificateBuilder.DefaultUserExtensions.Select(ext => ext.Key).Concat(Extensions))
                    builder.Extensions.Add(new KeyValuePair<String, Byte[]>(extension, []));

                return new CertifiedKey(userKey, builder.Sign(userKey.PublicKeyBlob, caKey));

            }

            var granted    = Issue("operator-17", AdminGrant);
            var notGranted = Issue("operator-18");

            var (server, port, hostKey) = await StartAsync(
                                              new SshAuthenticationPolicy().WithCertificateAuthority(trust),
                                              (session, name, ct) => ValueTask.FromResult<SshSubsystemHandler?>(
                                                                         name == Admin &&
                                                                         session.Certificate is SshCertificate certificate &&
                                                                         certificate.Extensions.Any(extension => extension.Key == AdminGrant)
                                                                             ? ServeConsoleAsync
                                                                             : null),
                                              CancellationToken);

            await using var _ = server;

            String? greeting;

            await using (var client = await ConnectAsync(port, hostKey, granted, CancellationToken))
            {

                var channel = await client.OpenSubsystem(Admin, Xterm, CancellationToken);
                using var reader = new StreamReader(channel.Input);

                greeting = await reader.ReadLineAsync(CancellationToken);
                await channel.SendDataAsync("q"u8.ToArray(), CancellationToken);

            }

            await using var refusedClient = await ConnectAsync(port, hostKey, notGranted, CancellationToken);

            Assert.That(greeting, Is.EqualTo("xterm-256color 80x24 for operator-17"), "the factory did not see the certificate that signed in");

            await Assert.ThrowsAsync<SshSubsystemRefusedException>(async () => await refusedClient.OpenSubsystem(Admin, Xterm, CancellationToken),
                                                                   "a certificate without the grant was given the admin console");

        }

        #endregion

    }

}
