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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// What the server takes from a client and what it turns away: environment variables only as
    /// <see cref="SshServerOptions.AcceptEnv"/> names them, as sshd's <c>AcceptEnv</c>; channel types it
    /// does not serve refused - and both refusals audited.
    /// </summary>
    [TestFixture]
    public class ServerEnvironmentAndChannelTests
    {

        #region Env_IsRefused_WithoutAcceptEnv

        /// <summary>
        /// Without <see cref="SshServerOptions.AcceptEnv"/> no variable reaches the shell, as with sshd,
        /// and each refusal is audited with the variable's name.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Env_IsRefused_WithoutAcceptEnv(CancellationToken CancellationToken)
        {

            var (environment, audit) = await ShellEnvironmentAsync([], new () { ["LANG"] = "de_DE.UTF-8", ["LD_PRELOAD"] = "/tmp/evil.so" }, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(environment, Is.Empty, "no variable reached the shell");
                Assert.That(Refused(audit), Is.EquivalentTo(new[] { "LANG", "LD_PRELOAD" }));
            });

        }

        #endregion

        #region Env_IsAccepted_AsAcceptEnvNamesIt

        /// <summary>
        /// <c>*</c> stands for any run of characters and <c>?</c> for one, and names are case-sensitive:
        /// what the patterns name gets through, nothing else does.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Env_IsAccepted_AsAcceptEnvNamesIt(CancellationToken CancellationToken)
        {

            var (environment, audit) = await ShellEnvironmentAsync([ "LANG", "LC_*", "TZ?" ],
                                                                   new () {
                                                                       ["LANG"]     = "de_DE.UTF-8",
                                                                       ["LC_ALL"]   = "C.UTF-8",
                                                                       ["TZ1"]      = "Europe/Berlin",
                                                                       ["TZ12"]     = "too long for TZ?",
                                                                       ["lang"]     = "another case",
                                                                       ["PATH"]     = "/tmp/evil"
                                                                   },
                                                                   CancellationToken);

            Assert.Multiple(() => {
                Assert.That(environment.Keys,  Is.EquivalentTo(new[] { "LANG", "LC_ALL", "TZ1" }));
                Assert.That(environment["LANG"], Is.EqualTo("de_DE.UTF-8"));
                Assert.That(Refused(audit),    Is.EquivalentTo(new[] { "TZ12", "lang", "PATH" }));
            });

        }

        #endregion

        #region MatchesPattern_AsSshdDoes

        [TestCase("LANG",      "LANG",    true)]
        [TestCase("LANG",      "LAN",     false)]
        [TestCase("LANGUAGE",  "LANG",    false)]
        [TestCase("LC_ALL",    "LC_*",    true)]
        [TestCase("LC_",       "LC_*",    true)]
        [TestCase("XLC_ALL",   "LC_*",    false)]
        [TestCase("anything",  "*",       true)]
        [TestCase("",          "*",       true)]
        [TestCase("TZ1",       "TZ?",     true)]
        [TestCase("TZ",        "TZ?",     false)]
        [TestCase("A_B_C",     "*_*_*",   true)]
        [TestCase("A_B",       "*_*_*",   false)]
        [TestCase("lang",      "LANG",    false)]
        public void MatchesPattern_AsSshdDoes(String Name, String Pattern, Boolean Matches)
            => Assert.That(SshSessionChannel.MatchesPattern(Name, Pattern), Is.EqualTo(Matches));

        #endregion

        #region AChannelTypeNobodyServes_IsRefusedAndAudited

        /// <summary>
        /// A client opens channels the server never serves - the agent's <c>agent-connect</c> and
        /// <c>auth-agent@openssh.com</c>, <c>x11</c>, and a made-up one: each is refused, and each refusal
        /// is audited with the channel type. Before, they were turned away without a trace.
        /// </summary>
        [TestCase("agent-connect")]
        [TestCase("auth-agent@openssh.com")]
        [TestCase("x11")]
        [TestCase("hermod-test@example.org")]
        [CancelAfter(20000)]
        public async Task AChannelTypeNobodyServes_IsRefusedAndAudited(String ChannelType, CancellationToken CancellationToken)
        {

            var (server, port, userKey, audit) = await StartAsync([], null, CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, userKey, CancellationToken);

                await Assert.ThatAsync(async () => await client.Multiplexer.OpenChannelAsync(ChannelType, CancellationToken: CancellationToken),
                                       Throws.InstanceOf<SshChannelOpenException>());

                Assert.That(audit.Events.OfType<PolicyDeniedEvent>().Where(e => e.PolicyType == "channel").Select(e => e.Target),
                            Is.EqualTo(new[] { ChannelType }));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion


        #region (private) helpers

        private static async Task<(IReadOnlyDictionary<String, String> Environment, CollectingAuditSink Audit)>
            ShellEnvironmentAsync(IReadOnlyList<String> AcceptEnv, Dictionary<String, String> Sent, CancellationToken CancellationToken)
        {

            var seen = new TaskCompletionSource<IReadOnlyDictionary<String, String>>(TaskCreationOptions.RunContinuationsAsynchronously);

            var (server, port, userKey, audit) = await StartAsync(AcceptEnv,
                                                                  (context, ct) => { seen.TrySetResult(context.Environment); return ValueTask.FromResult(0); },
                                                                  CancellationToken);

            try
            {
                await using var client = await ConnectAsync(port, userKey, CancellationToken);
                await using var shell  = await client.OpenShellAsync(null, Sent, CancellationToken);
                return (await seen.Task.WaitAsync(CancellationToken), audit);
            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        private static IEnumerable<String> Refused(CollectingAuditSink Audit)
            => Audit.Events.OfType<PolicyDeniedEvent>().Where(e => e.PolicyType == "env").Select(e => e.Target);

        private static async Task<(SshServer Server, UInt16 Port, ISshHostKey UserKey, CollectingAuditSink Audit)>
            StartAsync(IReadOnlyList<String> AcceptEnv, SshShellHandler? ShellHandler, CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();
            var audit   = new CollectingAuditSink();

            var server  = new SshServer(new SshServerOptions {
                              HostKeys       = [ SshHostKey.GenerateEd25519() ],
                              Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                              AcceptEnv      = AcceptEnv,
                              ShellHandler   = ShellHandler ?? ((context, ct) => ValueTask.FromResult(0)),
                              AuditSink      = audit
                          });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return (server, (UInt16) server.LocalEndPoint.Port.ToInt32(), userKey, audit);

        }

        private static ValueTask<SshClient> ConnectAsync(UInt16 Port, ISshHostKey UserKey, CancellationToken CancellationToken)
            => SshClient.ConnectAsync("127.0.0.1", Port, new SshClientOptions {
                   Username       = "achim",
                   VerifyHostKey  = SshHostKeyVerification.AcceptAnyUnsafe,
                   Credentials    = [ UserKey ]
               }, CancellationToken);

        #endregion

    }

}
