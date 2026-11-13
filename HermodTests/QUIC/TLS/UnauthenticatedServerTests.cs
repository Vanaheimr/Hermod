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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

/// <summary>
/// A TLS 1.3 client believes a server that authenticated, and no other: by the time the server's
/// Finished arrives, a Certificate and a CertificateVerify were validated, or the handshake is the
/// resumption of one in which they were (RFC 8446 §4.4). The Finished itself has to verify.
/// <para>
/// Whoever answers in a server's place can do the key exchange, and so it can compute a Finished
/// that verifies. What it cannot do is sign with the server's key. A client that goes on without
/// the signature has checked no certificate and no key pin at all.
/// </para>
/// </summary>
[TestFixture]
public class UnauthenticatedServerTests
{
    private static readonly byte[] Tp = [0x0f, 0x00];

    private static IEnumerable<CertificateValidationOptions> Validations(ServerCertificate certificate)
    {
        yield return new CertificateValidationOptions { CustomTrustRoots = [certificate.Certificate] };
        yield return CertificateValidationOptions.PinnedTo(KeyPinPolicy.PinnedTo(certificate.KeyPin));
        yield return CertificateValidationOptions.Insecure;
    }

    [Test]
    public void AServerThatSendsNoCertificateAndNoCertificateVerify_IsNotBelieved()
    {
        using var certificate = ServerCertificate.CreateSelfSigned("localhost");

        foreach (CertificateValidationOptions validation in Validations(certificate))
        {
            using var client = new TlsClientHandshake("localhost", Tp, certificateValidation: validation);
            using var server = new TlsServerHandshake(certificate, Tp);

            client.Start();
            Assert.That(client.TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] clientHello), Is.True);
            server.ProvideCrypto(level, clientHello);

            using var transcript = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            transcript.AppendData(clientHello);

            var e = Assert.Throws<TlsHandshakeException>(() =>
            {
                while (server.TryGetOutgoingCrypto(out EncryptionLevel serverLevel, out byte[] message))
                {
                    switch ((HandshakeType)message[0])
                    {
                        case HandshakeType.ServerHello:
                        case HandshakeType.EncryptedExtensions:
                            transcript.AppendData(message);
                            client.ProvideCrypto(serverLevel, message);
                            break;

                        case HandshakeType.Finished:
                            // The Finished of a server that left Certificate and CertificateVerify
                            // out: over ClientHello, ServerHello and EncryptedExtensions alone.
                            Assert.That(server.NegotiatedCipherSuite, Is.EqualTo(CipherSuite.Aes128GcmSha256));
                            byte[] verifyData = new KeySchedule(CipherSuite.Aes128GcmSha256).FinishedVerifyData(
                                server.HandshakeSecrets!.ServerHandshakeTrafficSecret, transcript.GetCurrentHash());
                            client.ProvideCrypto(serverLevel, Finished.BuildMessage(verifyData));
                            break;

                        // Certificate and CertificateVerify: left out.
                    }
                }
            });

            Assert.Multiple(() =>
            {
                Assert.That(e!.Alert, Is.EqualTo(TlsAlert.UnexpectedMessage));
                Assert.That(client.ServerFinishedValid, Is.True, "The Finished of this test does not verify: it shows nothing.");
                Assert.That(client.ServerCertificateValid, Is.False);
                Assert.That(client.IsComplete, Is.False, "A server that did not authenticate was believed.");
                Assert.That(client.ApplicationSecrets, Is.Null);
                Assert.That(client.TryGetOutgoingCrypto(out _, out _), Is.False, "The client went on to its own Finished.");
            });
        }
    }

    [Test]
    public void AServerWhoseFinishedDoesNotVerify_IsNotBelieved()
    {
        using var certificate = ServerCertificate.CreateSelfSigned("localhost");

        foreach (CertificateValidationOptions validation in Validations(certificate))
        {
            using var client = new TlsClientHandshake("localhost", Tp, certificateValidation: validation);
            using var server = new TlsServerHandshake(certificate, Tp);

            client.Start();
            Assert.That(client.TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] clientHello), Is.True);
            server.ProvideCrypto(level, clientHello);

            var e = Assert.Throws<TlsHandshakeException>(() =>
            {
                while (server.TryGetOutgoingCrypto(out EncryptionLevel serverLevel, out byte[] message))
                {
                    if ((HandshakeType)message[0] == HandshakeType.Finished)
                        message[^1] ^= 0x01;
                    client.ProvideCrypto(serverLevel, message);
                }
            });

            Assert.Multiple(() =>
            {
                Assert.That(e!.Alert, Is.EqualTo(TlsAlert.DecryptError));
                Assert.That(client.ServerCertificateValid, Is.True);
                Assert.That(client.IsComplete, Is.False, "A Finished that does not verify was taken.");
                Assert.That(client.ApplicationSecrets, Is.Null);
            });
        }
    }
}
