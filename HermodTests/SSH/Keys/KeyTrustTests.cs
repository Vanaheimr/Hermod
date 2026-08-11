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
using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M4 key trust: authorized_keys options and validity windows, known_hosts lookup (incl. hashed and
    /// markers), and the host-key policy chain (pins, known_hosts, TOFU).
    /// </summary>
    [TestFixture]
    public class KeyTrustTests
    {

        private sealed class FixedTimeProvider(DateTimeOffset Now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }


        #region AuthorizedKeys_ParsesOptionsAndValidityWindows

        [Test]
        public void AuthorizedKeys_ParsesOptionsAndValidityWindows()
        {

            var key   = SshHostKey.GenerateEd25519();
            var line  = SshPublicKey.FromHostKey(key, "alice@host").ToAuthorizedKeyLine();

            var text  =
                "# a comment\n" +
                "cert-authority,principals=\"admin,ops\" " + line + "\n" +
                "not-before=\"20260101\",expiry-time=\"20270101Z\" " + line + "\n";

            var entries = AuthorizedKeysFile.Parse(text);

            Assert.Multiple(() => {
                Assert.That(entries,                    Has.Count.EqualTo(2));
                Assert.That(entries[0].IsCertAuthority, Is.True);
                Assert.That(entries[0].Principals,      Is.EqualTo(new[] { "admin", "ops" }));
                Assert.That(entries[1].NotBefore,       Is.EqualTo(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
                Assert.That(entries[1].NotAfter,        Is.EqualTo(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            });

        }

        #endregion

        #region AuthorizedKey_ValidityWindow

        [Test]
        public void AuthorizedKey_ValidityWindow()
        {

            var key   = SshPublicKey.FromHostKey(SshHostKey.GenerateEd25519());
            var entry = new AuthorizedKey(key)
            {
                NotBefore = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                NotAfter  = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)
            };

            Assert.Multiple(() => {
                Assert.That(entry.IsValidAt(new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero)), Is.False, "before window");
                Assert.That(entry.IsValidAt(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)), Is.True,  "inside window");
                Assert.That(entry.IsValidAt(new DateTimeOffset(2026, 7,  1, 0, 0, 0, TimeSpan.Zero)), Is.False, "at NotAfter (exclusive)");
            });

        }

        #endregion

        #region KnownHosts_PlainWildcardPortAndHashed

        [Test]
        public void KnownHosts_PlainWildcardPortAndHashed()
        {

            var key      = SshHostKey.GenerateEd25519();
            var keyLine  = SshPublicKey.FromHostKey(key).ToAuthorizedKeyLine();

            // A hashed entry for "example.org": |1|<b64 salt>|<b64 HMAC-SHA1(salt, name)>.
            var salt     = RandomNumberGenerator.GetBytes(20);
            var hash     = HMACSHA1.HashData(salt, Encoding.UTF8.GetBytes("example.org"));
            var hashed   = $"|1|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hash)}";

            var text =
                "*.example.com "          + keyLine + "\n" +
                "[gateway]:2222 "         + keyLine + "\n" +
                "@revoked badhost.net "   + keyLine + "\n" +
                hashed + " "              + keyLine + "\n";

            var known = KnownHostsFile.Parse(text);

            Assert.Multiple(() => {
                Assert.That(known.Lookup("host.example.com", IPPort.Parse(22)), Is.Not.Empty, "wildcard match");
                Assert.That(known.Lookup("other.net",        IPPort.Parse(22)), Is.Empty,     "no match");
                Assert.That(known.Lookup("gateway",          IPPort.Parse(2222)), Is.Not.Empty, "[host]:port match");
                Assert.That(known.Lookup("gateway",          IPPort.Parse(22)),   Is.Empty,     "wrong port");
                Assert.That(known.Lookup("example.org",      IPPort.Parse(22)), Is.Not.Empty, "hashed match");
                Assert.That(known.Lookup("badhost.net",      IPPort.Parse(22))[0].Marker, Is.EqualTo(KnownHostMarker.Revoked));
            });

        }

        #endregion

        #region HostKeyPolicy_Pin

        [Test]
        public void HostKeyPolicy_Pin()
        {

            var hostKey  = SshHostKey.GenerateEd25519();
            var blob     = hostKey.PublicKeyBlob;
            var other    = SshHostKey.GenerateEd25519().PublicKeyBlob;

            var byFingerprint = HostKeyPolicy.Pin(SshFingerprint.Sha256(blob));
            var byKeyLine     = HostKeyPolicy.Pin(SshPublicKey.FromHostKey(hostKey).ToAuthorizedKeyLine());

            Assert.Multiple(() => {
                Assert.That(byFingerprint.Verify("srv", IPPort.Parse(22), blob),  Is.True);
                Assert.That(byFingerprint.Verify("srv", IPPort.Parse(22), other), Is.False, "unpinned key rejected (strict)");
                Assert.That(byKeyLine.Verify    ("srv", IPPort.Parse(22), blob),  Is.True);
            });

        }

        #endregion

        #region HostKeyPolicy_KnownHosts_AcceptRejectChangedAndRevoked

        [Test]
        public void HostKeyPolicy_KnownHosts_AcceptRejectChangedAndRevoked()
        {

            var good     = SshHostKey.GenerateEd25519();
            var changed  = SshHostKey.GenerateEd25519();
            var goodLine = SshPublicKey.FromHostKey(good).ToAuthorizedKeyLine();

            var known    = KnownHostsFile.Parse("srv.example.org " + goodLine + "\n");
            var policy   = HostKeyPolicy.Pin("SHA256:definitely-not-this").OrKnownHosts(known);

            var tofuReached = false;
            var withTofu = HostKeyPolicy.Pin("SHA256:nope")
                                        .OrKnownHosts(known)
                                        .OrInteractiveTofu(_ => { tofuReached = true; return true; });

            Assert.Multiple(() => {
                Assert.That(policy.Verify("srv.example.org", IPPort.Parse(22), good.PublicKeyBlob),    Is.True,  "known key accepted");
                Assert.That(policy.Verify("srv.example.org", IPPort.Parse(22), changed.PublicKeyBlob), Is.False, "changed host key rejected");
                Assert.That(policy.Verify("unknown.host",    IPPort.Parse(22), good.PublicKeyBlob),    Is.False, "unknown host, no TOFU ⇒ reject");
                // For an unknown host the chain falls through to TOFU.
                Assert.That(withTofu.Verify("unknown.host",  IPPort.Parse(22), good.PublicKeyBlob),    Is.True);
                Assert.That(tofuReached, Is.True);
            });

        }

        #endregion

        #region Auth_ExpiredAuthorizedKey_IsRejected

        [Test]
        [CancelAfter(15000)]
        public async Task Auth_ExpiredAuthorizedKey_IsRejected(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            // An authorized key whose validity window ended in 2020.
            var expired = new AuthorizedKey(SshPublicKey.FromHostKey(userKey))
            {
                NotAfter = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
            };

            var now           = new FixedTimeProvider(new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero));
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys([ expired ], now);

            var serverRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                return await UserAuthentication.ClientPublicKeyAuthenticateAsync(transport, "achim", userKey, CancellationToken: CancellationToken);
            }, CancellationToken);

            Assert.That(await clientRun, Is.False, "an expired authorized key must be rejected");

            clientPipe.Output.Complete();
            Assert.That(async () => await serverRun, Throws.InstanceOf<SshWireException>());

        }

        #endregion

        #region HostKeyPolicy_WiredToClientHandshake

        [Test]
        [CancelAfter(15000)]
        public async Task HostKeyPolicy_WiredToClientHandshake(CancellationToken CancellationToken)
        {

            var hostKey     = Ed25519KeyPair.Generate();
            var goodPolicy  = HostKeyPolicy.Pin(SshFingerprint.Sha256(hostKey.PublicKeyBlob));
            var wrongPolicy = HostKeyPolicy.Pin("SHA256:this-is-not-the-server-key");

            // Correct pin: the handshake completes.
            {
                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: goodPolicy.ForHost("localhost", IPPort.Parse(22)), CancellationToken: CancellationToken);
                using var server = await serverTask;
                Assert.That(client.SessionId, Is.EqualTo(server.SessionId));
            }

            // Wrong pin: the client rejects the host key and the handshake fails.
            {
                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                _ = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                Assert.That(async () => await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: wrongPolicy.ForHost("localhost", IPPort.Parse(22)), CancellationToken: CancellationToken),
                            Throws.InstanceOf<SshWireException>());
            }

        }

        #endregion

    }

}
