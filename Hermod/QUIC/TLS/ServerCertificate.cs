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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

/// <summary>
/// The server certificate incl. private key for the TLS 1.3 handshake. Creates a self-signed test
/// certificate on demand (for tests / <c>curl -k</c>) — ECDSA P-256 or Ed25519 as desired —
/// and signs the CertificateVerify message (RFC 8446 §4.4.3).
/// </summary>
public sealed class ServerCertificate : IDisposable
{
    private readonly ICertificateSigner _signer;

    /// <summary>
    /// The certificate (the chain here: exactly this one, self-signed).
    /// </summary>
    public X509Certificate2 Certificate { get; }

    /// <summary>
    /// DER encoding of the certificate (for the TLS Certificate message).
    /// </summary>
    public byte[] Der => Certificate.RawData;

    /// <summary>
    /// Signature scheme in use (ecdsa_secp256r1_sha256 or ed25519).
    /// </summary>
    public SignatureScheme SignatureScheme => _signer.Scheme;

    /// <summary>
    /// The SPKI pin: base64 of the SHA-256 hash over the DER-encoded SubjectPublicKeyInfo. This is
    /// the identity a pinning client checks instead of the chain, so it is what Chrome's
    /// <c>--ignore-certificate-errors-spki-list</c> expects — and why the key has to survive a
    /// restart (see <see cref="LoadOrCreatePkcs12"/>).
    /// </summary>
    public string SubjectPublicKeyInfoPin
        => Convert.ToBase64String(SHA256.HashData(Certificate.PublicKey.ExportSubjectPublicKeyInfo()));

    /// <summary>
    /// SHA-256 over the DER certificate, lowercase hex. Not the same identity as
    /// <see cref="SubjectPublicKeyInfoPin"/>: this one covers the whole certificate, which is what
    /// WebTransport's <c>serverCertificateHashes</c> compares against. That option only accepts an
    /// ECDSA P-256 certificate valid for at most 14 days — short life stands in for revocation — so a
    /// certificate built with the default <paramref name="validity"/> of
    /// <see cref="CreateSelfSigned"/> is deliberately out of range for it.
    /// </summary>
    public string CertificateHashSha256
        => Convert.ToHexString(SHA256.HashData(Der)).ToLowerInvariant();

    private ServerCertificate(X509Certificate2 certificate, ICertificateSigner signer)
    {
        Certificate = certificate;
        _signer = signer;
    }

    /// <summary>
    /// Creates a fresh self-signed ECDSA P-256 certificate for <paramref name="commonName"/>.
    /// <paramref name="validity"/> is the total lifetime, starting one day in the past to absorb clock
    /// skew; pass 14 days or less to make the certificate usable with WebTransport's
    /// <c>serverCertificateHashes</c> (see <see cref="CertificateHashSha256"/>).
    /// </summary>
    public static ServerCertificate CreateSelfSigned(string commonName = "localhost",
                                                     TimeSpan? validity = null)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256);
        AddExtensions(request, commonName);

        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        X509Certificate2 cert = request.CreateSelfSigned(
            notBefore, notBefore + (validity ?? DefaultValidity));
        return new ServerCertificate(cert, new EcdsaSigner(key));
    }

    /// <summary>
    /// Lifetime of a test certificate when none is given: a year plus the day of backdating.
    /// </summary>
    public static readonly TimeSpan DefaultValidity = TimeSpan.FromDays(366);

    /// <summary>
    /// Returns the ECDSA P-256 certificate stored at <paramref name="path"/>, creating it there on
    /// first use. A fresh key on every start would be fine for <c>curl -k</c>, but not for a client
    /// that pins the public key: the pin in a browser's command line has to stay valid across
    /// restarts. Ed25519/Ed448 have no equivalent, because BouncyCastle holds those private keys and
    /// they never reach the <see cref="X509Certificate2"/> that PKCS#12 export reads.
    /// An expired stored certificate is replaced rather than served: with a short
    /// <paramref name="validity"/> (as WebTransport demands) that happens within two weeks, and a
    /// silently expired certificate looks like a protocol bug from the client side.
    /// </summary>
    public static ServerCertificate LoadOrCreatePkcs12(string path,
                                                       out bool created,
                                                       string commonName = "localhost",
                                                       TimeSpan? validity = null)
    {
        if (File.Exists(path))
        {
            ServerCertificate stored = FromPkcs12(X509CertificateLoader.LoadPkcs12FromFile(
                                                      path, password: null, X509KeyStorageFlags.Exportable));
            if (stored.Certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow)
            {
                created = false;
                return stored;
            }
            stored.Dispose();
        }

        ServerCertificate fresh = CreateSelfSigned(commonName, validity);
        File.WriteAllBytes(path, fresh.Certificate.Export(X509ContentType.Pkcs12));
        created = true;
        return fresh;
    }

    /// <summary>
    /// Rebuilds the signer from a loaded certificate. Only ECDSA P-256 is accepted: the curve
    /// decides the signature scheme, and <see cref="EcdsaSigner"/> announces exactly one.
    /// </summary>
    private static ServerCertificate FromPkcs12(X509Certificate2 certificate)
    {
        ECDsa? key = certificate.GetECDsaPrivateKey();
        if (key is null)
            throw new NotSupportedException(
                $"'{certificate.Subject}' carries no ECDSA private key. A stored certificate has to be " +
                "ECDSA P-256; Ed25519/Ed448 keys live in BouncyCastle and cannot be exported this way.");

        string? curve = key.ExportParameters(includePrivateParameters: false).Curve.Oid.Value;
        if (curve != ECCurve.NamedCurves.nistP256.Oid.Value)
        {
            key.Dispose();
            throw new NotSupportedException(
                $"Expected an ECDSA P-256 key ({ECCurve.NamedCurves.nistP256.Oid.Value}), found {curve}.");
        }

        return new ServerCertificate(certificate, new EcdsaSigner(key));
    }

    /// <summary>
    /// Creates a fresh self-signed <b>Ed25519</b> certificate (RFC 8410) for
    /// <paramref name="commonName"/>. Key and signature come from BouncyCastle
    /// (<see cref="Ed25519Signature"/>); the TBSCertificate build is handled by the BCL via an
    /// <see cref="X509SignatureGenerator"/>, so subject-alt-name etc. stay unchanged.
    /// </summary>
    public static ServerCertificate CreateSelfSignedEd25519(string commonName = "localhost")
    {
        var ed = new Ed25519Signature();
        X509Certificate2 cert = CreateSelfSignedFromGenerator(commonName, new Ed25519SignatureGenerator(ed));
        return new ServerCertificate(cert, new Ed25519CertSigner(ed));
    }

    /// <summary>
    /// Creates a fresh self-signed <b>Ed448</b> certificate (RFC 8032/8410) for
    /// <paramref name="commonName"/>. Built like <see cref="CreateSelfSignedEd25519"/>, only with the
    /// Edwards448 primitive (57-byte key, id-Ed448 1.3.101.113).
    /// </summary>
    public static ServerCertificate CreateSelfSignedEd448(string commonName = "localhost")
    {
        var ed = new Ed448Signature();
        X509Certificate2 cert = CreateSelfSignedFromGenerator(commonName, new Ed448SignatureGenerator(ed));
        return new ServerCertificate(cert, new Ed448CertSigner(ed));
    }

    /// <summary>
    /// Creates a fresh self-signed <b>ML-DSA</b> certificate (FIPS 204, post-quantum) for
    /// <paramref name="commonName"/>. Key, certificate build and signature come entirely from the
    /// .NET 10 BCL (<see cref="MLDsa"/> + <see cref="CertificateRequest"/>); the TLS handshake uses
    /// the corresponding SignatureScheme mldsa44/65/87 (draft-ietf-tls-mldsa §3, FIPS 204 context empty).
    /// </summary>
    public static ServerCertificate CreateSelfSignedMLDsa(
        string commonName = "localhost", SignatureScheme scheme = SignatureScheme.MLDsa65)
    {
        MLDsaAlgorithm algorithm = scheme switch
        {
            SignatureScheme.MLDsa44 => MLDsaAlgorithm.MLDsa44,
            SignatureScheme.MLDsa65 => MLDsaAlgorithm.MLDsa65,
            SignatureScheme.MLDsa87 => MLDsaAlgorithm.MLDsa87,
            _ => throw new ArgumentException("Expected MLDsa44, MLDsa65 or MLDsa87.", nameof(scheme)),
        };
        MLDsa key = MLDsa.GenerateKey(algorithm);
        // SYSLIB5006: the X509 PQC integration (CertificateRequest with MLDsa) is still marked
        // "experimental" in .NET 10 — the signature APIs themselves are stable; deliberately suppressed locally.
#pragma warning disable SYSLIB5006
        var request = new CertificateRequest($"CN={commonName}", key);
#pragma warning restore SYSLIB5006
        AddExtensions(request, commonName);

        X509Certificate2 cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return new ServerCertificate(cert, new MLDsaSigner(key, scheme));
    }

    /// <summary>
    /// Builds the self-signed certificate via an <see cref="X509SignatureGenerator"/> (for key types
    /// the BCL does not know natively — Ed25519/Ed448). SAN + basic constraints as with the ECDSA certificates.
    /// </summary>
    private static X509Certificate2 CreateSelfSignedFromGenerator(string commonName, X509SignatureGenerator generator)
    {
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={commonName}"), generator.PublicKey, HashAlgorithmName.SHA256);
        AddExtensions(request, commonName);

        byte[] serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F; // positive, non-empty serial number
        return request.Create(
            request.SubjectName, generator,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), serial);
    }

    private static void AddExtensions(CertificateRequest request, string commonName)
    {
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(commonName);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, false, 0, critical: true));
    }

    /// <summary>
    /// Signs the CertificateVerify content in the TLS format of the respective scheme (RFC 8446 §4.4.3).
    /// </summary>
    public byte[] SignCertificateVerify(ReadOnlySpan<byte> content) => _signer.Sign(content);

    public void Dispose()
    {
        _signer.Dispose();
        Certificate.Dispose();
    }

    // ---- Signer abstraction (ECDSA from the BCL, Ed25519 from BouncyCastle) ------------------

    private interface ICertificateSigner : IDisposable
    {
        SignatureScheme Scheme { get; }
        byte[] Sign(ReadOnlySpan<byte> content);
    }

    private sealed class EcdsaSigner(ECDsa key) : ICertificateSigner
    {
        public SignatureScheme Scheme => SignatureScheme.EcdsaSecp256r1Sha256;

        // TLS transmits ECDSA signatures as a DER-encoded r/s sequence.
        public byte[] Sign(ReadOnlySpan<byte> content)
            => key.SignData(content, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        public void Dispose() => key.Dispose();
    }

    private sealed class Ed25519CertSigner(Ed25519Signature ed) : ICertificateSigner
    {
        public SignatureScheme Scheme => SignatureScheme.Ed25519;
        public byte[] Sign(ReadOnlySpan<byte> content) => ed.Sign(content);
        public void Dispose() { }
    }

    private sealed class Ed448CertSigner(Ed448Signature ed) : ICertificateSigner
    {
        public SignatureScheme Scheme => SignatureScheme.Ed448;
        public byte[] Sign(ReadOnlySpan<byte> content) => ed.Sign(content);
        public void Dispose() { }
    }

    private sealed class MLDsaSigner(MLDsa key, SignatureScheme scheme) : ICertificateSigner
    {
        public SignatureScheme Scheme => scheme;

        // draft-ietf-tls-mldsa §4: pure ML-DSA (no pre-hash), the FIPS 204 context parameter MUST be empty.
        public byte[] Sign(ReadOnlySpan<byte> content)
        {
            byte[] signature = new byte[key.Algorithm.SignatureSizeInBytes];
            key.SignData(content, signature, context: default);
            return signature;
        }

        public void Dispose() => key.Dispose();
    }

    /// <summary>
    /// Bridge so that <see cref="CertificateRequest.Create(X500DistinguishedName, X509SignatureGenerator,
    /// DateTimeOffset, DateTimeOffset, byte[])"/> can build an Ed25519 certificate: provides the
    /// Ed25519 SubjectPublicKeyInfo, the AlgorithmIdentifier (id-Ed25519, without parameters) and the
    /// PureEdDSA signature over the TBSCertificate bytes.
    /// </summary>
    private sealed class Ed25519SignatureGenerator(Ed25519Signature ed) : X509SignatureGenerator
    {
        // AlgorithmIdentifier ::= SEQUENCE { OID 1.3.101.112 } — parameters MUST be absent (RFC 8410 §3).
        private static readonly byte[] AlgorithmId = [0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x70];

        public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm) => AlgorithmId;

        public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) => ed.Sign(data);

        protected override PublicKey BuildPublicKey()
        {
            // SubjectPublicKeyInfo ::= SEQUENCE { AlgorithmIdentifier(Ed25519), BIT STRING(publicKey) }.
            // Fixed frame for the 32-byte key — 12-byte prefix + 32-byte key = 44 bytes.
            byte[] spki = new byte[44];
            ReadOnlySpan<byte> prefix =
                [0x30, 0x2A, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x70, 0x03, 0x21, 0x00];
            prefix.CopyTo(spki);
            ed.PublicKey.CopyTo(spki.AsSpan(12));
            return PublicKey.CreateFromSubjectPublicKeyInfo(spki, out _);
        }
    }

    /// <summary>Like <see cref="Ed25519SignatureGenerator"/>, but for id-Ed448 (1.3.101.113): 57-byte key,
    /// empty signature context.</summary>
    private sealed class Ed448SignatureGenerator(Ed448Signature ed) : X509SignatureGenerator
    {
        // AlgorithmIdentifier ::= SEQUENCE { OID 1.3.101.113 } — parameters MUST be absent (RFC 8410 §3).
        private static readonly byte[] AlgorithmId = [0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x71];

        public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm) => AlgorithmId;

        public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) => ed.Sign(data);

        protected override PublicKey BuildPublicKey()
        {
            // SubjectPublicKeyInfo for Ed448 — 12-byte prefix + 57-byte key = 69 bytes.
            byte[] spki = new byte[69];
            ReadOnlySpan<byte> prefix =
                [0x30, 0x43, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x71, 0x03, 0x3A, 0x00];
            prefix.CopyTo(spki);
            ed.PublicKey.CopyTo(spki.AsSpan(12));
            return PublicKey.CreateFromSubjectPublicKeyInfo(spki, out _);
        }
    }
}
