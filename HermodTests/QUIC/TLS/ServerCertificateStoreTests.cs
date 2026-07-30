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
using System.Security.Cryptography.X509Certificates;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

/// <summary>
/// The stored test certificate (<see cref="ServerCertificate.LoadOrCreatePkcs12"/>) and the two
/// identities a pinning client checks. Both exist for clients that skip the Web PKI: a browser pins
/// the public key on its command line (SPKI pin) and WebTransport pins the whole certificate
/// (<c>serverCertificateHashes</c>) — neither survives a key that changes on every server start.
/// </summary>
[TestFixture]
public class ServerCertificateStoreTests
{

    private string _path = "";

    [SetUp]
    public void CreateTempPath()
        => _path = Path.Combine(Path.GetTempPath(), $"hermod-test-{Guid.NewGuid():N}.pfx");

    [TearDown]
    public void RemoveTempFile()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Test]
    public void LoadOrCreate_KeepsTheKeyAcrossReloads()
    {
        using ServerCertificate first = ServerCertificate.LoadOrCreatePkcs12(_path, out bool created1);
        Assert.That(created1, Is.True);
        Assert.That(File.Exists(_path), Is.True);

        using ServerCertificate second = ServerCertificate.LoadOrCreatePkcs12(_path, out bool created2);
        Assert.That(created2, Is.False);

        Assert.Multiple(() =>
        {
            // Both pins have to be stable — that is the entire point of the file.
            Assert.That(second.SubjectPublicKeyInfoPin, Is.EqualTo(first.SubjectPublicKeyInfoPin));
            Assert.That(second.CertificateHashSha256, Is.EqualTo(first.CertificateHashSha256));
            Assert.That(second.Certificate.Thumbprint, Is.EqualTo(first.Certificate.Thumbprint));
            Assert.That(second.SignatureScheme, Is.EqualTo(SignatureScheme.EcdsaSecp256r1Sha256));
        });

        // The private key came along, otherwise the reloaded certificate could not sign a handshake.
        byte[] signature = second.SignCertificateVerify([1, 2, 3]);
        Assert.That(signature, Is.Not.Empty);
    }

    [Test]
    public void LoadOrCreate_ReplacesAnExpiredCertificate()
    {
        // A short-lived certificate is the norm here (WebTransport rejects anything living longer than
        // 14 days), so expiry is a case that actually happens rather than a theoretical one.
        using (ServerCertificate expired = ServerCertificate.CreateSelfSigned("localhost", TimeSpan.FromHours(1)))
            File.WriteAllBytes(_path, expired.Certificate.Export(X509ContentType.Pkcs12));
        // Backdating is one day, so a one-hour lifetime is already over.
        using ServerCertificate replaced = ServerCertificate.LoadOrCreatePkcs12(_path, out bool created);

        Assert.That(created, Is.True, "an expired certificate must not be served");
        Assert.That(replaced.Certificate.NotAfter.ToUniversalTime(), Is.GreaterThan(DateTime.UtcNow));
    }

    [Test]
    public void Validity_IsTheTotalLifetime_StartingOneDayInThePast()
    {
        using ServerCertificate shortLived = ServerCertificate.CreateSelfSigned("localhost", TimeSpan.FromDays(13));

        TimeSpan lifetime = shortLived.Certificate.NotAfter - shortLived.Certificate.NotBefore;
        Assert.That(lifetime, Is.EqualTo(TimeSpan.FromDays(13)).Within(TimeSpan.FromSeconds(2)),
                    "13 days total — WebTransport's serverCertificateHashes refuses more than 14.");
        Assert.That(shortLived.Certificate.NotBefore.ToUniversalTime(), Is.LessThan(DateTime.UtcNow),
                    "backdated, so a skewed clock on the peer does not reject it");
    }

    [Test]
    public void Pins_MatchAnIndependentComputation()
    {
        using ServerCertificate certificate = ServerCertificate.CreateSelfSigned("localhost");

        Assert.Multiple(() =>
        {
            Assert.That(certificate.CertificateHashSha256,
                        Is.EqualTo(Convert.ToHexString(SHA256.HashData(certificate.Certificate.RawData)).ToLowerInvariant()),
                        "certificate hash = SHA-256 over the DER certificate, lowercase hex");
            Assert.That(certificate.SubjectPublicKeyInfoPin,
                        Is.EqualTo(Convert.ToBase64String(
                            SHA256.HashData(certificate.Certificate.PublicKey.ExportSubjectPublicKeyInfo()))),
                        "SPKI pin = base64 of SHA-256 over the SubjectPublicKeyInfo");
            // The two identities cover different bytes and must not be confused with one another.
            Assert.That(certificate.CertificateHashSha256,
                        Is.Not.EqualTo(Convert.ToHexString(
                            SHA256.HashData(certificate.Certificate.PublicKey.ExportSubjectPublicKeyInfo())).ToLowerInvariant()));
        });
    }

    [Test]
    public void LoadOrCreate_RefusesACertificateWithoutAnEcdsaKey()
    {
        // Ed25519 keys stay in BouncyCastle and never reach the X509Certificate2, so a PKCS#12 built
        // from one carries no usable private key — that has to fail loudly, not silently half-work.
        using (ServerCertificate ed25519 = ServerCertificate.CreateSelfSignedEd25519("localhost"))
            File.WriteAllBytes(_path, ed25519.Certificate.Export(X509ContentType.Pkcs12));

        Assert.Throws<NotSupportedException>(
            () => ServerCertificate.LoadOrCreatePkcs12(_path, out _));
    }

}
