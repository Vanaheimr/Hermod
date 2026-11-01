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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// RSA host certificates with SHA-2, as OpenSSH has spoken them since 8.8: the certificate's key type
    /// stays <c>ssh-rsa-cert-v01@openssh.com</c>, but the algorithm names the hash -
    /// <c>rsa-sha2-512-cert-v01@openssh.com</c> signs as rsa-sha2-512. Before, an RSA certificate was
    /// offered only under its key type, which means SHA-1 and which OpenSSH refuses, and signed with
    /// SHA-512 whatever was negotiated.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    public class RsaHostCertificateTests
    {

        #region (private) Certify(Subject)

        private static readonly ISshHostKey CA = SshHostKey.GenerateEd25519();

        private static CertifiedKey Certify(ISshHostKey Subject)
            => new (Subject,
                    new OpenSshCertificateBuilder {
                        Serial       = 1,
                        Type         = SshCertType.Host,
                        KeyId        = "host",
                        Principals   = [ "host.example.org" ],
                        ValidAfter   = TimeProvider.System.GetUtcNow().AddHours(-1),
                        ValidBefore  = TimeProvider.System.GetUtcNow().AddHours(1)
                    }.Sign(Subject.PublicKeyBlob, CA));

        #endregion

        #region (private) HandshakeAsync(HostKeys, ClientAlgorithms, CancellationToken)

        private static async Task<(SshTransport Client, SshTransport Server)> HandshakeAsync(IReadOnlyList<ISshHostKey>  HostKeys,
                                                                                             String[]?                   ClientAlgorithms,
                                                                                             CancellationToken           CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, HostKeyAlgorithms: ClientAlgorithms, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, HostKeys, CancellationToken: CancellationToken);

            var client = await clientTask;
            var server = await serverTask;

            if (client.Algorithms.ExtensionInfo)
                Assert.That(client.TryHandleExtInfo(await client.ReceivePacketAsync(CancellationToken)), Is.True);

            return (client, server);

        }

        #endregion

        #region (private) WrongHashKey

        /// <summary>
        /// A server key that agrees to one hash and signs with the other.
        /// </summary>
        private sealed class WrongHashKey(ISshHostKey Rsa) : ISshHostKey
        {

            public IReadOnlyList<String> AlgorithmNames => [ SshAlgorithmNames.HostKey.RsaSha2_512 ];

            public Byte[] PublicKeyBlob => Rsa.PublicKeyBlob;

            public Byte[] Sign(String AlgorithmName, ReadOnlySpan<Byte> Data)
                => Rsa.Sign(SshAlgorithmNames.HostKey.RsaSha2_256, Data);

        }

        #endregion


        #region AnRsaCertificate_OffersBothSha2Algorithms

        [Test]
        public void AnRsaCertificate_OffersBothSha2Algorithms()
        {

            var certified = Certify(SshHostKey.GenerateRsa(2048));

            Assert.Multiple(() => {
                Assert.That(certified.Certificate.CertAlgorithm, Is.EqualTo(SshAlgorithmNames.HostKey.SshRsaCert), "the key type stays");
                Assert.That(certified.AlgorithmNames,            Is.EqualTo(new[] { SshAlgorithmNames.HostKey.RsaSha2_512Cert,
                                                                                    SshAlgorithmNames.HostKey.RsaSha2_256Cert }));
            });

        }

        #endregion

        #region ACertificate_SignsWithTheHashItsAlgorithmNames

        [TestCase(SshAlgorithmNames.HostKey.RsaSha2_512Cert, SshAlgorithmNames.HostKey.RsaSha2_512)]
        [TestCase(SshAlgorithmNames.HostKey.RsaSha2_256Cert, SshAlgorithmNames.HostKey.RsaSha2_256)]
        public void ACertificate_SignsWithTheHashItsAlgorithmNames(String Algorithm, String SignatureAlgorithm)
        {

            var certified = Certify(SshHostKey.GenerateRsa(2048));
            var data      = RandomNumberGenerator.GetBytes(32);
            var signature = certified.Sign(Algorithm, data);

            Assert.Multiple(() => {
                Assert.That(new SshPacketReader(signature).ReadString(),                           Is.EqualTo(SignatureAlgorithm));
                Assert.That(SshSignature.Verify(certified.PublicKeyBlob, data, signature, Algorithm), Is.True);
            });

        }

        #endregion

        #region AnRsaHostCertificate_IsNegotiatedWithSha2

        [TestCase(SshAlgorithmNames.HostKey.RsaSha2_512Cert)]
        [TestCase(SshAlgorithmNames.HostKey.RsaSha2_256Cert)]
        [CancelAfter(15000)]
        public async Task AnRsaHostCertificate_IsNegotiatedWithSha2(String Algorithm, CancellationToken CancellationToken)
        {

            var plain     = SshHostKey.GenerateRsa(2048);
            var certified = Certify(plain);

            var (client, server) = await HandshakeAsync([ plain, certified ], [ Algorithm ], CancellationToken);

            using var _c = client;
            using var _s = server;

            Assert.Multiple(() => {
                Assert.That(client.Algorithms.HostKey, Is.EqualTo(Algorithm));
                Assert.That(client.ServerHostKey,      Is.EqualTo(certified.PublicKeyBlob));
            });

        }

        #endregion

        #region OurClient_AsksForAnRsaHostCertificateByDefault

        /// <summary>
        /// A server whose only host key is an RSA certificate: our client's default list has to name it.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task OurClient_AsksForAnRsaHostCertificateByDefault(CancellationToken CancellationToken)
        {

            var certified = Certify(SshHostKey.GenerateRsa(2048));

            var (client, server) = await HandshakeAsync([ certified ], null, CancellationToken);

            using var _c = client;
            using var _s = server;

            Assert.That(client.Algorithms.HostKey, Is.EqualTo(SshAlgorithmNames.HostKey.RsaSha2_512Cert));

        }

        #endregion

        #region Verify_BindsTheSignatureToTheAlgorithm

        [Test]
        public void Verify_BindsTheSignatureToTheAlgorithm()
        {

            var rsa       = SshHostKey.GenerateRsa(2048);
            var certified = Certify(rsa);
            var ed25519   = SshHostKey.GenerateEd25519();
            var data      = RandomNumberGenerator.GetBytes(32);
            var sha256    = rsa.Sign(SshAlgorithmNames.HostKey.RsaSha2_256, data);

            Assert.Multiple(() => {

                Assert.That(SshSignature.Verify(rsa.PublicKeyBlob,       data, sha256, SshAlgorithmNames.HostKey.RsaSha2_256),     Is.True);
                Assert.That(SshSignature.Verify(rsa.PublicKeyBlob,       data, sha256, SshAlgorithmNames.HostKey.RsaSha2_512),     Is.False, "the other hash");
                Assert.That(SshSignature.Verify(certified.PublicKeyBlob, data, sha256, SshAlgorithmNames.HostKey.RsaSha2_256Cert), Is.True);
                Assert.That(SshSignature.Verify(certified.PublicKeyBlob, data, sha256, SshAlgorithmNames.HostKey.RsaSha2_256),     Is.False, "a certificate where a plain key was agreed");
                Assert.That(SshSignature.Verify(rsa.PublicKeyBlob,       data, sha256, SshAlgorithmNames.HostKey.RsaSha2_256Cert), Is.False, "a plain key where a certificate was agreed");

                var signedByEd25519 = ed25519.Sign(SshAlgorithmNames.HostKey.Ed25519, data);
                Assert.That(SshSignature.Verify(ed25519.PublicKeyBlob,   data, signedByEd25519, SshAlgorithmNames.HostKey.RsaSha2_512), Is.False, "another kind of key");

            });

        }

        #endregion

        #region OurClient_RefusesAServerThatSignsWithAnotherHash

        /// <summary>
        /// The server agrees to rsa-sha2-512 and signs with rsa-sha2-256: a valid signature by the right
        /// key, but not the one agreed - OpenSSH's client refuses it, and so does ours now.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task OurClient_RefusesAServerThatSignsWithAnotherHash(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken).AsTask();
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, new WrongHashKey(SshHostKey.GenerateRsa(2048)), CancellationToken: CancellationToken).AsTask();

            var exception = await Assert.ThrowsAsync<SshWireException>(async () => await clientTask);
            Assert.That(exception!.Message, Does.Contain($"invalid for the negotiated '{SshAlgorithmNames.HostKey.RsaSha2_512}'"));

            // The server waits for a NEWKEYS that will not come; end its connection.
            await clientPipe.Output.CompleteAsync();
            await Assert.CatchAsync(async () => await serverTask);

        }

        #endregion

        #region TheAnnouncement_NamesACertifiedKeyByItsPlainKey_Once

        /// <summary>
        /// hostkeys-00 carries keys, not certificates, as sshd's does: a certified key is announced as the
        /// key it certifies, once, also when its plain key is configured beside it - and a proof for it
        /// is signed, where before nothing matched it and the whole proof was refused.
        /// </summary>
        [Test]
        public void TheAnnouncement_NamesACertifiedKeyByItsPlainKey_Once()
        {

            var rsa           = SshHostKey.GenerateRsa(2048);
            var ed25519       = SshHostKey.GenerateEd25519();
            var certifiedOnly = Certify(ed25519);

            ISshHostKey[] hostKeys = [ rsa, Certify(rsa), certifiedOnly ];

            var announced = SshHostKeyRotation.PlainKeys(hostKeys).Select(key => key.PublicKeyBlob).ToArray();

            Assert.That(announced, Is.EqualTo(new[] { rsa.PublicKeyBlob, ed25519.PublicKeyBlob }));

            var sessionId = RandomNumberGenerator.GetBytes(32);
            var proofs    = SshHostKeyRotation.SignProofs(announced, hostKeys, sessionId);

            Assert.That(proofs, Is.Not.Null);
            Assert.That(SshHostKeyRotation.VerifyProofs(announced, proofs!, sessionId), Has.Count.EqualTo(2));

        }

        #endregion

    }

}
