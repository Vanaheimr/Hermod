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

using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The <c>hostkeys-00@openssh.com</c> / <c>hostkeys-prove-00@openssh.com</c> host-key rotation
    /// extension: wire codec, the signed proof preimage, and — most importantly — that a client only
    /// ever trusts a key whose private half was actually proven.
    /// </summary>
    [TestFixture]
    public class HostKeyRotationTests
    {

        private static Byte[] SessionId => Convert.FromHexString("0badc0ffee1234567890abcdefabcdef");


        #region KeyList_RoundTrips

        [Test]
        public void KeyList_RoundTrips()
        {

            var keys  = new[] { SshHostKey.GenerateEd25519(), SshHostKey.GenerateEcdsa(SshAlgorithmNames.HostKey.EcdsaNistP256) };
            var blobs = keys.Select(k => k.PublicKeyBlob).ToArray();

            var decoded = SshHostKeyRotation.DecodeKeyList(SshHostKeyRotation.EncodeKeyList(blobs));

            Assert.Multiple(() => {
                Assert.That(decoded,    Has.Count.EqualTo(2));
                Assert.That(decoded[0], Is.EqualTo(blobs[0]));
                Assert.That(decoded[1], Is.EqualTo(blobs[1]));
            });

        }

        #endregion

        #region ProofData_MatchesTheSpecifiedPreimage

        /// <summary>The signature is computed over string("hostkeys-prove-00@openssh.com") || string(session-id) || string(hostkey).</summary>
        [Test]
        public void ProofData_MatchesTheSpecifiedPreimage()
        {

            var key    = SshHostKey.GenerateEd25519();
            var actual = SshHostKeyRotation.ProofData(SessionId, key.PublicKeyBlob);

            // SshPacketReader is a ref struct, so read everything out before asserting.
            var reader    = new SshPacketReader(actual);
            var name      = reader.ReadString();
            var session   = reader.ReadBinaryString();
            var hostKey   = reader.ReadBinaryString();
            var trailing  = reader.HasMoreData;

            Assert.Multiple(() => {
                Assert.That(name,     Is.EqualTo("hostkeys-prove-00@openssh.com"));
                Assert.That(session,  Is.EqualTo(SessionId));
                Assert.That(hostKey,  Is.EqualTo(key.PublicKeyBlob));
                Assert.That(trailing, Is.False, "nothing may trail the preimage");
            });

        }

        #endregion

        #region SignAndVerify_RoundTrips_AcrossKeyTypes

        [TestCase("ssh-ed25519")]
        [TestCase("ecdsa-sha2-nistp256")]
        [TestCase("ssh-rsa")]
        public void SignAndVerify_RoundTrips_AcrossKeyTypes(String KeyType)
        {

            var hostKeys = new[] { SshKeyGenerator.Generate(KeyType) };
            var blobs    = hostKeys.Select(k => k.PublicKeyBlob).ToArray();

            var proofs   = SshHostKeyRotation.SignProofs(blobs, hostKeys, SessionId);
            Assert.That(proofs, Is.Not.Null);

            var proven   = SshHostKeyRotation.VerifyProofs(blobs, proofs!, SessionId);
            Assert.That(proven, Has.Count.EqualTo(1));
            Assert.That(proven[0], Is.EqualTo(blobs[0]));

        }

        #endregion

        #region SignProofs_RefusesKeysWeDoNotHold

        /// <summary>A server must never sign a proof for a blob it was merely handed — that would let a peer harvest signatures.</summary>
        [Test]
        public void SignProofs_RefusesKeysWeDoNotHold()
        {

            var ours      = new[] { SshHostKey.GenerateEd25519() };
            var strangers = SshHostKey.GenerateEd25519().PublicKeyBlob;

            Assert.That(SshHostKeyRotation.SignProofs([ strangers ], ours, SessionId), Is.Null,
                        "signing must be refused for a key we do not hold");

            Assert.That(SshHostKeyRotation.SignProofs([ ours[0].PublicKeyBlob, strangers ], ours, SessionId), Is.Null,
                        "one foreign key must invalidate the whole batch");

        }

        #endregion

        #region VerifyProofs_RejectsForgedOrMismatchedReplies

        [Test]
        public void VerifyProofs_RejectsForgedOrMismatchedReplies()
        {

            var real     = SshHostKey.GenerateEd25519();
            var attacker = SshHostKey.GenerateEd25519();

            var announced = new[] { real.PublicKeyBlob, attacker.PublicKeyBlob };

            // The attacker's key is advertised, but only the real key can be signed for.
            var honest = SshHostKeyRotation.SignProofs([ real.PublicKeyBlob ], [ real ], SessionId)!;

            Assert.Multiple(() => {

                // Too few signatures for the number of keys challenged.
                Assert.That(SshHostKeyRotation.VerifyProofs(announced, honest, SessionId), Is.Empty,
                            "signature count must match the number of challenged keys");

                // A signature made over a DIFFERENT session id must not verify (cross-session replay).
                var otherSession = RandomNumberGenerator.GetBytes(16);
                var replayed     = SshHostKeyRotation.SignProofs([ real.PublicKeyBlob ], [ real ], otherSession)!;
                Assert.That(SshHostKeyRotation.VerifyProofs([ real.PublicKeyBlob ], replayed, SessionId), Is.Empty,
                            "a proof from another session must not verify");

                // A signature by the attacker's key offered as proof for the real key must not verify.
                var forged = SshHostKeyRotation.SignProofs([ attacker.PublicKeyBlob ], [ attacker ], SessionId)!;
                Assert.That(SshHostKeyRotation.VerifyProofs([ real.PublicKeyBlob ], forged, SessionId), Is.Empty,
                            "a proof signed by a different key must not verify");

                // Garbage payload.
                Assert.That(SshHostKeyRotation.VerifyProofs([ real.PublicKeyBlob ], [ 0x00, 0x01, 0x02 ], SessionId), Is.Empty);

            });

        }

        #endregion

        #region VerifyProofs_AllOrNothing

        /// <summary>One bad signature invalidates the entire update — no partial trust.</summary>
        [Test]
        public void VerifyProofs_AllOrNothing()
        {

            var good     = SshHostKey.GenerateEd25519();
            var attacker = SshHostKey.GenerateEd25519();

            // Proof list: a valid signature for 'good', then a signature by 'attacker' over its own key
            // presented as the proof for a key the client challenged.
            var goodProof     = SshHostKeyRotation.SignProofs([ good.PublicKeyBlob     ], [ good     ], SessionId)!;
            var attackerProof = SshHostKeyRotation.SignProofs([ attacker.PublicKeyBlob ], [ attacker ], SessionId)!;

            var mixed = goodProof.Concat(attackerProof).ToArray();

            var proven = SshHostKeyRotation.VerifyProofs(
                             [ good.PublicKeyBlob, SshHostKey.GenerateEd25519().PublicKeyBlob ],
                             mixed,
                             SessionId);

            Assert.That(proven, Is.Empty, "a single unverifiable key must void the whole advertisement");

        }

        #endregion


        #region Advertisement_MustContainTheSessionsOwnHostKey

        /// <summary>
        /// A server that does not advertise the key it authenticated with is not describing the key set
        /// the client already trusts, so the whole update is refused (OpenSSH enforces the same rule) —
        /// otherwise a server could walk a client off its known-good key onto a chosen one.
        /// </summary>
        [Test]
        public void Advertisement_MustContainTheSessionsOwnHostKey()
        {

            var current = SshHostKey.GenerateEd25519().PublicKeyBlob;
            var other   = SshHostKey.GenerateEd25519().PublicKeyBlob;

            Assert.Multiple(() => {
                Assert.That(SshHostKeyRotation.Advertises([ current, other ], current), Is.True);
                Assert.That(SshHostKeyRotation.Advertises([ other ],          current), Is.False,
                            "an advertisement omitting the session's host key must not be accepted");
                Assert.That(SshHostKeyRotation.Advertises([ current ],        null),    Is.False);
                Assert.That(SshHostKeyRotation.Advertises([],                 current), Is.False);
            });

        }

        #endregion

        #region HostKeys_AreAdvertisedAndProven_EndToEnd

        /// <summary>
        /// End-to-end over the real multiplexer: a server holding two host keys advertises both after
        /// auth, the client challenges them and receives valid proofs — the rotation path a client would
        /// use to learn a new key before the old one is retired.
        /// </summary>
        [Test]
        [Category("Loopback")]
        [CancelAfter(25000)]
        public async Task HostKeys_AreAdvertisedAndProven_EndToEnd(CancellationToken CancellationToken)
        {

            var currentKey = SshHostKey.GenerateEd25519();                                    // used for the handshake
            var rotatedIn  = SshHostKey.GenerateEcdsa(SshAlgorithmNames.HostKey.EcdsaNistP256); // the incoming key
            var userKey    = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys      = [ currentKey, rotatedIn ],
                Authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ExecHandler   = async (ctx, ct) => { await ctx.WriteAsync("ok\n", ct); return 0; }
            });

            var received = new TaskCompletionSource<SshHostKeyUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = (UInt16) server.LocalEndPoint.Port.ToInt32();

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username         = "achim",
                    VerifyHostKey    = blob => blob.AsSpan().SequenceEqual(currentKey.PublicKeyBlob),
                    Credentials      = [ userKey ],
                    HostKeysReceived = (update, ct) => { received.TrySetResult(update); return ValueTask.CompletedTask; }
                }, CancellationToken);

                var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);

                Assert.Multiple(() => {

                    Assert.That(update.CurrentKeyAdvertised, Is.True,
                                "the session's own host key must be among the advertised keys");

                    Assert.That(update.AnnouncedKeys, Has.Count.EqualTo(2));

                    Assert.That(update.ProvenKeys,    Has.Count.EqualTo(2),
                                "both advertised keys must prove possession of their private half");

                    Assert.That(update.ProvenKeys.Any(b => b.AsSpan().SequenceEqual(rotatedIn.PublicKeyBlob)), Is.True,
                                "the rotated-in key must be learnable this way");

                });

                // The connection stays fully usable while all of that happens on the side.
                var result = await client.ExecuteAsync("status", CancellationToken);
                Assert.That(result.StandardOutput, Is.EqualTo("ok\n"));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

    }

}
