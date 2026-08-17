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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Regression tests for client-side host-key verification failing <b>closed</b>.
    ///
    /// <para>
    /// The M9 security review found that a null verification callback accepted any host key. The
    /// signature a server produces during key exchange proves only that it holds the private half of
    /// the key it just presented — possession, not identity — so with no verifier there is nothing
    /// binding that key to the host the client meant to reach, and any machine on the path completes
    /// the handshake with a key generated moments earlier.
    /// </para>
    ///
    /// <para>
    /// Omitting the check must therefore be impossible by accident: the transport refuses, and
    /// <c>SshClientOptions.VerifyHostKey</c> is <c>required</c> so the compiler refuses too. Skipping it
    /// has to be written down as <see cref="SshHostKeyVerification.AcceptAnyUnsafe"/>.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Security")]
    public class HostKeyVerificationTests
    {

        #region MissingVerifier_IsRefused

        /// <summary>
        /// A handshake with no verifier must fail rather than silently accept the peer's key.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task MissingVerifier_IsRefused(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = SshHostKey.GenerateEd25519();

            _ = Task.Run(async () => {
                try { using var _ = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            var error = Assert.CatchAsync<SshWireException>(async () =>
                            await SshTransport.ClientHandshakeAsync(clientPipe, CancellationToken: CancellationToken));

            Assert.That(error!.Message, Does.Contain("host-key verification").IgnoreCase,
                        "the refusal must explain that no verifier was supplied");

        }

        #endregion

        #region WrongHostKey_IsRejected

        /// <summary>
        /// A verifier that says no must actually stop the handshake.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task WrongHostKey_IsRejected(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey  = SshHostKey.GenerateEd25519();
            var expected = SshHostKey.GenerateEd25519();     // a different key — the MITM's

            _ = Task.Run(async () => {
                try { using var _ = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            Assert.CatchAsync<SshWireException>(async () =>
                await SshTransport.ClientHandshakeAsync(
                          clientPipe,
                          VerifyHostKey:     blob => blob.AsSpan().SequenceEqual(expected.PublicKeyBlob),
                          CancellationToken: CancellationToken));

        }

        #endregion

        #region CorrectHostKey_IsAccepted

        /// <summary>
        /// Failing closed must not break the legitimate case.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task CorrectHostKey_IsAccepted(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = SshHostKey.GenerateEd25519();

            _ = Task.Run(async () => {
                try { using var _ = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            using var transport = await SshTransport.ClientHandshakeAsync(
                                            clientPipe,
                                            VerifyHostKey:     blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                                            CancellationToken: CancellationToken);

            Assert.That(transport.ServerHostKey, Is.EqualTo(hostKey.PublicKeyBlob));

        }

        #endregion

        #region AcceptAnyUnsafe_IsAnExplicitOptOut

        /// <summary>
        /// The deliberate opt-out still works — the point of the change is that it must be <i>written
        /// down</i>, not reached by leaving something unset.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task AcceptAnyUnsafe_IsAnExplicitOptOut(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = SshHostKey.GenerateEd25519();

            _ = Task.Run(async () => {
                try { using var _ = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            using var transport = await SshTransport.ClientHandshakeAsync(
                                            clientPipe,
                                            VerifyHostKey:     SshHostKeyVerification.AcceptAnyUnsafe,
                                            CancellationToken: CancellationToken);

            Assert.That(transport.ServerHostKey, Is.EqualTo(hostKey.PublicKeyBlob));

        }

        #endregion

        #region HostKeyPolicy_PluggedIntoTheClient

        /// <summary>
        /// The intended production shape: a <see cref="HostKeyPolicy"/> pinned to a fingerprint, handed
        /// to the transport via <c>ForHost</c> — accepting the pinned key and rejecting any other.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task HostKeyPolicy_PluggedIntoTheClient(CancellationToken CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();
            var policy  = HostKeyPolicy.Pin(SshFingerprint.Sha256(hostKey.PublicKeyBlob));
            var verify  = policy.ForHost("device.example", IPPort.Parse(22));

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            _ = Task.Run(async () => {
                try { using var _ = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            using var transport = await SshTransport.ClientHandshakeAsync(
                                            clientPipe,
                                            VerifyHostKey:     verify,
                                            CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(transport.ServerHostKey, Is.EqualTo(hostKey.PublicKeyBlob));
                Assert.That(verify(SshHostKey.GenerateEd25519().PublicKeyBlob), Is.False,
                            "the pinned policy must reject any other key");
            });

        }

        #endregion

    }

}
