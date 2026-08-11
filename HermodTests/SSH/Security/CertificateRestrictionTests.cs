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
    /// End-to-end enforcement of OpenSSH certificate critical options.
    ///
    /// <para>
    /// A CA issues a restricted certificate to grant <i>less</i> than a normal login — one fixed
    /// command, or access only from one subnet. The M9 security review found both options accepted and
    /// then ignored, which handed the holder everything the CA had tried to withhold. These tests pin
    /// the enforcement: the forced command actually replaces what the client asks for, and a
    /// certificate used from outside its subnet does not get a session.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Security")]
    public class CertificateRestrictionTests
    {

        #region (private) issue a restricted user certificate

        private static (CertifiedKey Credential, SshCertificateAuthorityTrust Trust) IssueRestricted(
            params (String Name, String Value)[] CriticalOptions)
        {

            var caKey   = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var builder = new OpenSshCertificateBuilder {
                Serial       = 7,
                Type         = SshCertType.User,
                KeyId        = "restricted",
                Principals   = [ "achim" ],
                ValidAfter   = DateTimeOffset.UtcNow.AddDays(-1),
                ValidBefore  = DateTimeOffset.UtcNow.AddDays(1)
            };

            foreach (var ext in OpenSshCertificateBuilder.DefaultUserExtensions)
                builder.Extensions.Add(ext);

            foreach (var (name, value) in CriticalOptions)
            {
                var abw = new System.Buffers.ArrayBufferWriter<Byte>();
                var w   = new SshPacketWriter(abw);
                w.WriteString(value);                                   // the option data is itself an SSH string
                builder.CriticalOptions.Add(new KeyValuePair<String, Byte[]>(name, abw.WrittenSpan.ToArray()));
            }

            var certificate = builder.Sign(userKey.PublicKeyBlob, caKey);

            return (new CertifiedKey(userKey, certificate),
                    new SshCertificateAuthorityTrust().TrustCA(caKey));

        }

        private static async Task<(SshServer Server, UInt16 Port, ISshHostKey HostKey)> StartServerAsync(
            SshCertificateAuthorityTrust  Trust,
            CancellationToken             CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys       = [ hostKey ],
                Authenticator  = new SshAuthenticationPolicy().WithCertificateAuthority(Trust),
                ExecHandler    = async (ctx, ct) => {
                                     await ctx.WriteAsync($"ran:{ctx.Command}|original:{ctx.OriginalCommand ?? "-"}\n", ct);
                                     return 0;
                                 }
            });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
            return (server, (UInt16) server.LocalEndPoint.Port.ToInt32(), hostKey);

        }

        private static ValueTask<SshClient> ConnectAsync(UInt16 Port, ISshHostKey HostKey, CertifiedKey Credential, CancellationToken CancellationToken)
            => SshClient.ConnectAsync("127.0.0.1", Port, new SshClientOptions {
                   Username      = "achim",
                   VerifyHostKey = blob => blob.AsSpan().SequenceEqual(HostKey.PublicKeyBlob),
                   Credentials   = [ Credential ]
               }, CancellationToken);

        #endregion


        #region ForceCommand_ReplacesWhateverTheClientAsksFor

        /// <summary>
        /// The whole point of <c>force-command</c>: the CA's command runs, not the client's, and the
        /// client's request survives only as information (OpenSSH's <c>SSH_ORIGINAL_COMMAND</c>).
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task ForceCommand_ReplacesWhateverTheClientAsksFor(CancellationToken CancellationToken)
        {

            var (credential, trust) = IssueRestricted(("force-command", "/usr/bin/backup-only"));
            var (server, port, hostKey) = await StartServerAsync(trust, CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, hostKey, credential, CancellationToken);

                var result = await client.ExecuteAsync("cat /etc/shadow", CancellationToken);

                Assert.Multiple(() => {

                    Assert.That(result.StandardOutput, Does.Contain("ran:/usr/bin/backup-only"),
                                "the CA's forced command must be what runs");

                    Assert.That(result.StandardOutput, Does.Not.Contain("ran:cat /etc/shadow"),
                                "the client's command must not run");

                    Assert.That(result.StandardOutput, Does.Contain("original:cat /etc/shadow"),
                                "the client's request must still be visible to the handler");

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region UnrestrictedCertificate_RunsTheClientsCommand

        /// <summary>Enforcement must not disturb an ordinary, unrestricted certificate.</summary>
        [Test]
        [CancelAfter(25000)]
        public async Task UnrestrictedCertificate_RunsTheClientsCommand(CancellationToken CancellationToken)
        {

            var (credential, trust) = IssueRestricted();
            var (server, port, hostKey) = await StartServerAsync(trust, CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, hostKey, credential, CancellationToken);
                var result = await client.ExecuteAsync("uname -a", CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(result.StandardOutput, Does.Contain("ran:uname -a"));
                    Assert.That(result.StandardOutput, Does.Contain("original:-"), "nothing was forced, so there is no original");
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region SourceAddress_OutsideTheAllowedRange_GetsNoSession

        /// <summary>
        /// The certificate is restricted to a subnet the loopback client is not in, so it must not get a
        /// usable session — the credential is only valid from where the CA said.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task SourceAddress_OutsideTheAllowedRange_GetsNoSession(CancellationToken CancellationToken)
        {

            var (credential, trust) = IssueRestricted(("source-address", "10.0.0.0/8"));
            var (server, port, hostKey) = await StartServerAsync(trust, CancellationToken);

            try
            {

                Assert.CatchAsync(async () => {
                    await using var client = await ConnectAsync(port, hostKey, credential, CancellationToken);
                    await client.ExecuteAsync("whoami", CancellationToken);
                }, "a certificate restricted to 10.0.0.0/8 must not work from 127.0.0.1");

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region SourceAddress_InsideTheAllowedRange_Works

        [Test]
        [CancelAfter(25000)]
        public async Task SourceAddress_InsideTheAllowedRange_Works(CancellationToken CancellationToken)
        {

            var (credential, trust) = IssueRestricted(("source-address", "127.0.0.0/8"));
            var (server, port, hostKey) = await StartServerAsync(trust, CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, hostKey, credential, CancellationToken);
                var result = await client.ExecuteAsync("whoami", CancellationToken);

                Assert.That(result.StandardOutput, Does.Contain("ran:whoami"));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Restrictions_AreParsedFromTheCertificate

        /// <summary>Unit-level: the option values decode, and an unevaluable peer address is refused.</summary>
        [Test]
        public void Restrictions_AreParsedFromTheCertificate()
        {

            var (credential, _) = IssueRestricted(("force-command",  "/usr/bin/backup-only"),
                                                  ("source-address", "10.0.0.0/8,192.168.1.0/24"));

            var restrictions = SshCertificateRestrictions.FromCertificate(credential.Certificate);

            Assert.Multiple(() => {

                Assert.That(restrictions.ForcedCommand,   Is.EqualTo("/usr/bin/backup-only"));
                Assert.That(restrictions.SourceAddresses, Has.Count.EqualTo(2));
                Assert.That(restrictions.IsRestricted,    Is.True);

                Assert.That(restrictions.AllowsSource(System.Net.IPAddress.Parse("10.1.2.3")),      Is.True);
                Assert.That(restrictions.AllowsSource(System.Net.IPAddress.Parse("192.168.1.9")),   Is.True);
                Assert.That(restrictions.AllowsSource(System.Net.IPAddress.Parse("127.0.0.1")),     Is.False);

                Assert.That(restrictions.AllowsSource(null), Is.False,
                            "a restriction that cannot be evaluated must not count as satisfied");

                Assert.That(restrictions.EffectiveCommand("anything"), Is.EqualTo("/usr/bin/backup-only"));

            });

        }

        #endregion

        #region Validator_AcceptsOnlyWhatTheCallerEnforces

        /// <summary>
        /// The validator stays fail-closed for callers that do not apply restrictions: the same
        /// certificate validates only when the caller declares it enforces the option.
        /// </summary>
        [Test]
        public void Validator_AcceptsOnlyWhatTheCallerEnforces()
        {

            var (credential, trust) = IssueRestricted(("force-command", "/usr/bin/backup-only"));
            var now = DateTimeOffset.UtcNow;

            Assert.Multiple(() => {

                Assert.That(SshCertificateValidator.Validate(credential.Certificate, SshCertType.User, "achim", trust, now).IsValid,
                            Is.False, "a caller that does not enforce the option must not accept the certificate");

                Assert.That(SshCertificateValidator.Validate(credential.Certificate, SshCertType.User, "achim", trust, now,
                                                             SshSessionRestrictions.EnforcedCriticalOptions).IsValid,
                            Is.True, "declaring enforcement makes it acceptable");

            });

        }

        #endregion

    }

}
