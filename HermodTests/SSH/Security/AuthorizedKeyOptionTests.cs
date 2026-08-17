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
    /// <c>authorized_keys</c> options are enforced, not merely parsed.
    ///
    /// <para>
    /// The M9 security review found <c>command=</c> parsed into <c>AuthorizedKey.ForcedCommand</c> and
    /// never read, while the class documentation additionally claimed support for <c>from=</c>,
    /// <c>restrict</c> and <c>no-*</c> that the parser did not even recognise. An administrator writing
    /// <c>from="10.0.0.0/8",command="/usr/local/bin/report"</c> got a line that was accepted in full and
    /// restricted nothing.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Security")]
    public class AuthorizedKeyOptionTests
    {

        #region (private) helpers

        private static AuthorizedKey Parse(String Options, ISshHostKey Key)
        {
            var line = $"{Options} {SshPublicKey.FromHostKey(Key, "device").ToAuthorizedKeyLine()}";
            Assert.That(AuthorizedKeysFile.TryParseLine(line, out var entry), Is.True, $"should parse: {line}");
            return entry!;
        }

        private static async Task<(SshServer Server, UInt16 Port, ISshHostKey HostKey)> StartServerAsync(
            AuthorizedKey      Entry,
            CancellationToken  CancellationToken,
            ForwardingPolicy?  Policy = null)
        {

            var hostKey = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys         = [ hostKey ],
                Authenticator    = SshUserAuthenticator.ForAuthorizedKeys([ Entry ]),
                ForwardingPolicy = Policy ?? ForwardingPolicy.None,
                ExecHandler      = async (ctx, ct) => {
                                       await ctx.WriteAsync($"ran:{ctx.Command}|original:{ctx.OriginalCommand ?? "-"}\n", ct);
                                       return 0;
                                   }
            });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
            return (server, (UInt16) server.LocalEndPoint.Port.ToInt32(), hostKey);

        }

        #endregion


        #region Command_IsEnforced_EndToEnd

        [Test]
        [CancelAfter(25000)]
        public async Task Command_IsEnforced_EndToEnd(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();
            var entry   = Parse("command=\"/usr/local/bin/report\"", userKey);

            var (server, port, hostKey) = await StartServerAsync(entry, CancellationToken);

            try
            {

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username      = "device",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ userKey ]
                }, CancellationToken);

                var result = await client.ExecuteAsync("rm -rf /", CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(result.StandardOutput, Does.Contain("ran:/usr/local/bin/report"), "the administrator's command must run");
                    Assert.That(result.StandardOutput, Does.Not.Contain("ran:rm -rf /"),          "the client's command must not");
                    Assert.That(result.StandardOutput, Does.Contain("original:rm -rf /"));
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region From_IsEnforced_EndToEnd

        /// <summary>
        /// A key restricted to a subnet the loopback client is not in must not get a session.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task From_IsEnforced_EndToEnd(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();
            var entry   = Parse("from=\"10.0.0.0/8\"", userKey);

            var (server, port, hostKey) = await StartServerAsync(entry, CancellationToken);

            try
            {
                Assert.CatchAsync(async () => {
                    await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                        Username      = "device",
                        VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                        Credentials   = [ userKey ]
                    }, CancellationToken);
                    await client.ExecuteAsync("whoami", CancellationToken);
                });
            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region NoPortForwarding_IsEnforced_EndToEnd

        /// <summary>
        /// <c>no-port-forwarding</c> must narrow the server's own policy: even where the forwarding ACL
        /// would permit the target, this key may not open the tunnel.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task NoPortForwarding_IsEnforced_EndToEnd(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();
            var entry   = Parse("no-port-forwarding", userKey);

            var (server, port, hostKey) = await StartServerAsync(entry, CancellationToken, ForwardingPolicy.LoopbackOnly);

            try
            {

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username      = "device",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ userKey ]
                }, CancellationToken);

                Assert.CatchAsync(async () => await client.OpenTcpStreamAsync("127.0.0.1", 9, CancellationToken),
                                  "no-port-forwarding must refuse the channel even under a permissive server policy");

                // The session itself still works.
                var result = await client.ExecuteAsync("hello", CancellationToken);
                Assert.That(result.StandardOutput, Does.Contain("ran:hello"));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Options_AreParsedIntoRestrictions

        [Test]
        public void Options_AreParsedIntoRestrictions()
        {

            var key = SshHostKey.GenerateEd25519();

            var full = Parse("from=\"10.0.0.0/8,192.168.1.5\",command=\"/bin/report\",no-pty", key).Restrictions;
            Assert.Multiple(() => {
                Assert.That(full.ForcedCommand,        Is.EqualTo("/bin/report"));
                Assert.That(full.AllowPty,             Is.False);
                Assert.That(full.SourceAddresses,      Has.Count.EqualTo(2));
                Assert.That(full.AllowsSource(System.Net.IPAddress.Parse("10.1.2.3")),     Is.True);
                Assert.That(full.AllowsSource(System.Net.IPAddress.Parse("192.168.1.5")),  Is.True, "a bare address means exactly that host");
                Assert.That(full.AllowsSource(System.Net.IPAddress.Parse("192.168.1.6")),  Is.False);
                Assert.That(full.AllowsSource(System.Net.IPAddress.Parse("127.0.0.1")),    Is.False);
            });

            var restricted = Parse("restrict", key).Restrictions;
            Assert.Multiple(() => {
                Assert.That(restricted.AllowPty,             Is.False, "restrict denies everything by default");
                Assert.That(restricted.AllowPortForwarding,  Is.False);
            });

            var reEnabled = Parse("restrict,pty", key).Restrictions;
            Assert.Multiple(() => {
                Assert.That(reEnabled.AllowPty,             Is.True, "a following permit re-enables");
                Assert.That(reEnabled.AllowPortForwarding,  Is.False);
            });

            // Restricting a capability we never offer is already satisfied, so the line stays usable.
            Assert.That(AuthorizedKeysFile.TryParseLine(
                            $"no-agent-forwarding,no-x11-forwarding {SshPublicKey.FromHostKey(key).ToAuthorizedKeyLine()}",
                            out _), Is.True);

        }

        #endregion

        #region UnenforceableOptions_RejectTheLine

        /// <summary>
        /// An option exists to take access away. If it cannot be enforced, honouring the line without it
        /// would grant more than was written down — so the line is refused, as OpenSSH also does for an
        /// unknown option.
        /// </summary>
        [Test]
        [TestCase("permitopen=\"10.0.0.1:80\"",  "permitopen cannot be expressed yet")]
        [TestCase("from=\"*.example.com\"",      "a hostname pattern cannot be evaluated")]
        [TestCase("from=\"!10.0.0.0/8\"",        "a negated entry is not supported")]
        [TestCase("environment=\"X=1\"",         "unknown/unsupported option")]
        public void UnenforceableOptions_RejectTheLine(String Options, String Why)
        {

            var line = $"{Options} {SshPublicKey.FromHostKey(SshHostKey.GenerateEd25519()).ToAuthorizedKeyLine()}";

            Assert.That(AuthorizedKeysFile.TryParseLine(line, out var entry), Is.False, Why);
            Assert.That(entry, Is.Null);

        }

        #endregion

        #region Restrictions_Intersect_StricterWins

        [Test]
        public void Restrictions_Intersect_StricterWins()
        {

            var a = new SshSessionRestrictions(AllowPty: false);
            var b = new SshSessionRestrictions(AllowPortForwarding: false);

            var combined = a.And(b);

            Assert.Multiple(() => {
                Assert.That(combined.AllowPty,             Is.False);
                Assert.That(combined.AllowPortForwarding,  Is.False);
                Assert.That(SshSessionRestrictions.None.And(SshSessionRestrictions.None).IsRestricted, Is.False);
            });

        }

        #endregion

    }

}
