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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// A server with several host keys, as sshd with several <c>HostKey</c> lines: every key's algorithms
    /// are offered, the client's choice of algorithm picks the key, and that key stays the host key for
    /// the connection. Before, only the first key ever signed, so a client that knew the host by another
    /// of its keys - an RSA key in <c>known_hosts</c>, say - could not get it.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    public class SeveralHostKeysTests
    {

        #region (private) HandshakeAsync(HostKeys, ClientAlgorithms, CancellationToken)

        private static async Task<(SshTransport Client, SshTransport Server)> HandshakeAsync(IReadOnlyList<ISshHostKey>  HostKeys,
                                                                                             String[]                    ClientAlgorithms,
                                                                                             CancellationToken           CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, HostKeyAlgorithms: ClientAlgorithms, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, HostKeys, CancellationToken: CancellationToken);

            var client = await clientTask;
            var server = await serverTask;

            // The server's EXT_INFO follows the initial NEWKEYS; take it off the wire before any traffic.
            if (client.Algorithms.ExtensionInfo)
                Assert.That(client.TryHandleExtInfo(await client.ReceivePacketAsync(CancellationToken)), Is.True);

            return (client, server);

        }

        #endregion


        #region TheClientsAlgorithm_PicksTheKey

        [TestCase(SshAlgorithmNames.HostKey.Ed25519,        0)]
        [TestCase(SshAlgorithmNames.HostKey.EcdsaNistP256,  1)]
        [TestCase(SshAlgorithmNames.HostKey.RsaSha2_512,    2)]
        [TestCase(SshAlgorithmNames.HostKey.RsaSha2_256,    2)]
        [CancelAfter(15000)]
        public async Task TheClientsAlgorithm_PicksTheKey(String Algorithm, Int32 Expected, CancellationToken CancellationToken)
        {

            ISshHostKey[] hostKeys = [ SshHostKey.GenerateEd25519(), SshHostKey.GenerateEcdsa(SshAlgorithmNames.HostKey.EcdsaNistP256), SshHostKey.GenerateRsa(2048) ];

            var (client, server) = await HandshakeAsync(hostKeys, [ Algorithm ], CancellationToken);

            using var _c = client;
            using var _s = server;

            Assert.Multiple(() => {
                Assert.That(client.Algorithms.HostKey, Is.EqualTo(Algorithm));
                // The client verified the signature against this blob, so the right key also signed.
                Assert.That(client.ServerHostKey,      Is.EqualTo(hostKeys[Expected].PublicKeyBlob));
                Assert.That(server.ServerHostKey,      Is.EqualTo(hostKeys[Expected].PublicKeyBlob));
            });

        }

        #endregion

        #region TheClientsPreference_WinsOverTheOrderOfTheKeys

        [Test]
        [CancelAfter(15000)]
        public async Task TheClientsPreference_WinsOverTheOrderOfTheKeys(CancellationToken CancellationToken)
        {

            var ed25519 = SshHostKey.GenerateEd25519();
            var ecdsa   = SshHostKey.GenerateEcdsa(SshAlgorithmNames.HostKey.EcdsaNistP256);

            var (client, server) = await HandshakeAsync([ ed25519, ecdsa ],
                                                        [ SshAlgorithmNames.HostKey.EcdsaNistP256, SshAlgorithmNames.HostKey.Ed25519 ],
                                                        CancellationToken);

            using var _c = client;
            using var _s = server;

            Assert.That(client.ServerHostKey, Is.EqualTo(ecdsa.PublicKeyBlob), "RFC 4253 §7.1: the first algorithm on the client's list the server supports.");

        }

        #endregion

        #region TwoKeysOfOneKind_TheFirstSigns

        /// <summary>
        /// An old and a new Ed25519 key, as during a rotation: they share the algorithm, so only the order
        /// can tell them apart - the first one listed signs, the second waits for <c>hostkeys-00</c>.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task TwoKeysOfOneKind_TheFirstSigns(CancellationToken CancellationToken)
        {

            var current = SshHostKey.GenerateEd25519();
            var next    = SshHostKey.GenerateEd25519();

            var (client, server) = await HandshakeAsync([ current, next ], [ SshAlgorithmNames.HostKey.Ed25519 ], CancellationToken);

            using var _c = client;
            using var _s = server;

            Assert.That(client.ServerHostKey, Is.EqualTo(current.PublicKeyBlob));

        }

        #endregion

        #region ACertificateBesideItsPlainKey_IsChosenByAlgorithm

        /// <summary>
        /// sshd's <c>HostKey</c> plus <c>HostCertificate</c>: a client that trusts the CA asks for the
        /// certificate, one that knows the plain key from <c>known_hosts</c> still gets the plain key.
        /// </summary>
        [TestCase(SshAlgorithmNames.HostKey.Ed25519Cert, true)]
        [TestCase(SshAlgorithmNames.HostKey.Ed25519,     false)]
        [CancelAfter(15000)]
        public async Task ACertificateBesideItsPlainKey_IsChosenByAlgorithm(String Algorithm, Boolean ExpectCertificate, CancellationToken CancellationToken)
        {

            var ca        = SshHostKey.GenerateEd25519();
            var plain     = SshHostKey.GenerateEd25519();
            var certified = new CertifiedKey(plain,
                                             new OpenSshCertificateBuilder {
                                                 Serial       = 1,
                                                 Type         = SshCertType.Host,
                                                 KeyId        = "host",
                                                 Principals   = [ "host.example.org" ],
                                                 ValidAfter   = TimeProvider.System.GetUtcNow().AddHours(-1),
                                                 ValidBefore  = TimeProvider.System.GetUtcNow().AddHours(1)
                                             }.Sign(plain.PublicKeyBlob, ca));

            var (client, server) = await HandshakeAsync([ plain, certified ], [ Algorithm ], CancellationToken);

            using var _c = client;
            using var _s = server;

            Assert.That(client.ServerHostKey, Is.EqualTo(ExpectCertificate ? certified.PublicKeyBlob : plain.PublicKeyBlob));

        }

        #endregion

        #region TheFirstKexInit_OffersEveryKeysAlgorithms

        [Test]
        [CancelAfter(15000)]
        public async Task TheFirstKexInit_OffersEveryKeysAlgorithms(CancellationToken CancellationToken)
        {

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe,
                                                               [ SshHostKey.GenerateEd25519(), SshHostKey.GenerateRsa(2048), SshHostKey.GenerateEd25519() ],
                                                               CancellationToken: cts.Token).AsTask();

            // A client that only listens: the server's KEXINIT is the first packet after the versions.
            await SshVersionExchange.WriteAsync(clientPipe.Output, SshIdentificationString.Default, CancellationToken);
            await SshVersionExchange.ReadAsync (clientPipe.Input,  CancellationToken);

            var kexInit = KexInitMessage.Decode(await SshPacketFraming.ReadPacketAsync(clientPipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken));

            await cts.CancelAsync();
            try { await serverTask; } catch (OperationCanceledException) { }

            // In the order of the keys, and the second Ed25519 key adds nothing.
            Assert.That(kexInit.ServerHostKeyAlgorithms, Is.EqualTo(new[] { SshAlgorithmNames.HostKey.Ed25519,
                                                                             SshAlgorithmNames.HostKey.RsaSha2_512,
                                                                             SshAlgorithmNames.HostKey.RsaSha2_256 }));

        }

        #endregion

        #region ARekey_KeepsTheChosenKey

        /// <summary>
        /// After the first exchange the server offers only the chosen key's algorithms, so a rekey can not
        /// move the connection to another key - which OpenSSH's client, like ours, refuses. A key that
        /// is not the first one is chosen here, so a server that fell back to its first key would show.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task ARekey_KeepsTheChosenKey(CancellationToken CancellationToken)
        {

            var ed25519 = SshHostKey.GenerateEd25519();
            var rsa     = SshHostKey.GenerateRsa(2048);

            var (client, server) = await HandshakeAsync([ ed25519, rsa ],
                                                        [ SshAlgorithmNames.HostKey.RsaSha2_256 ],
                                                        CancellationToken);

            using var _c = client;
            using var _s = server;

            await Task.WhenAll(client.RekeyAsync(CancellationToken).AsTask(),
                               server.RekeyAsync(CancellationToken).AsTask());

            Assert.Multiple(() => {
                Assert.That(client.KeyExchangeCount,   Is.EqualTo(2));
                Assert.That(client.ServerHostKey,      Is.EqualTo(rsa.PublicKeyBlob));
                Assert.That(client.Algorithms.HostKey, Is.EqualTo(SshAlgorithmNames.HostKey.RsaSha2_256));
            });

        }

        #endregion

        #region NoKeys_IsRefused

        [Test]
        public void NoKeys_IsRefused()
        {

            var (_, serverPipe) = DuplexPipe.CreateConnectedPair();

            Assert.That(async () => await SshTransport.ServerHandshakeAsync(serverPipe, Array.Empty<ISshHostKey>()),
                        Throws.ArgumentException);

        }

        #endregion

    }

}
