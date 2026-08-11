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

using System.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>The kind of OpenSSH certificate.</summary>
    public enum SshCertType : UInt32
    {
        /// <summary>A user certificate (authenticates a client).</summary>
        User = 1,
        /// <summary>A host certificate (authenticates a server).</summary>
        Host = 2
    }


    /// <summary>
    /// An OpenSSH certificate (<c>*-cert-v01@openssh.com</c>, PROTOCOL.certkeys): a CA-signed binding of a
    /// public key to a serial, type, key id, principals, validity window, critical options and extensions.
    /// This type parses and serializes the wire format and exposes the CA-signed bytes; policy checks
    /// (CA trust, principals, validity, revocation) live in <see cref="SshCertificateValidator"/>.
    /// </summary>
    public sealed class SshCertificate
    {

        #region Constants

        /// <summary>The suffix that marks an SSH certificate algorithm name.</summary>
        public const String CertSuffix = "-cert-v01@openssh.com";

        #endregion

        #region Properties

        /// <summary>The certificate algorithm name (e.g. <c>ssh-ed25519-cert-v01@openssh.com</c>).</summary>
        public String                                    CertAlgorithm       { get; }

        /// <summary>The full certificate wire blob.</summary>
        public Byte[]                                    Blob                { get; }

        /// <summary>The CA-signed bytes (the whole blob except the trailing signature field).</summary>
        public Byte[]                                    SignedBytes         { get; }

        /// <summary>The embedded subject public-key blob as a plain SSH key (for signature verification).</summary>
        public Byte[]                                    SubjectPublicKey    { get; }

        /// <summary>The subject's base signature algorithm (e.g. <c>ssh-ed25519</c>).</summary>
        public String                                    SubjectAlgorithm    { get; }

        /// <summary>The certificate serial number.</summary>
        public UInt64                                    Serial              { get; }

        /// <summary>Whether this is a user or host certificate.</summary>
        public SshCertType                               Type                { get; }

        /// <summary>The free-form key identifier (shown by <c>ssh-keygen -L</c>, logged on use).</summary>
        public String                                    KeyId               { get; }

        /// <summary>The valid principals (user names or host names); empty means "valid for all".</summary>
        public IReadOnlyList<String>                     Principals          { get; }

        /// <summary>The start of the validity window.</summary>
        public DateTimeOffset                            ValidAfter          { get; }

        /// <summary>The end of the validity window.</summary>
        public DateTimeOffset                            ValidBefore         { get; }

        /// <summary>The critical options (name → data); an unknown critical option must cause rejection.</summary>
        public IReadOnlyList<KeyValuePair<String, Byte[]>>  CriticalOptions  { get; }

        /// <summary>The extensions (name → data); unknown extensions are ignored.</summary>
        public IReadOnlyList<KeyValuePair<String, Byte[]>>  Extensions       { get; }

        /// <summary>The CA's public-key blob (the signer).</summary>
        public Byte[]                                    SignatureKey        { get; }

        /// <summary>The CA's signature over <see cref="SignedBytes"/>.</summary>
        public Byte[]                                    Signature           { get; }

        /// <summary>The SHA-256 fingerprint of the CA key.</summary>
        public String CaFingerprint => SshFingerprint.Sha256(SignatureKey);

        #endregion

        #region VerifyCaSignature() / IsValidAt(Now)

        /// <summary>Verify the CA's signature over the certificate (does not check CA trust or validity).</summary>
        public Boolean VerifyCaSignature()
            => SshSignature.Verify(SignatureKey, SignedBytes, Signature);

        /// <summary>Whether the certificate's validity window contains the given instant.</summary>
        public Boolean IsValidAt(DateTimeOffset Now)
            => ValidAfter <= Now && Now < ValidBefore;

        #endregion

        #region Constructor(s)

        private SshCertificate(String certAlgorithm, Byte[] blob, Byte[] signedBytes, Byte[] subjectPublicKey, String subjectAlgorithm,
                               UInt64 serial, SshCertType type, String keyId, IReadOnlyList<String> principals,
                               DateTimeOffset validAfter, DateTimeOffset validBefore,
                               IReadOnlyList<KeyValuePair<String, Byte[]>> criticalOptions, IReadOnlyList<KeyValuePair<String, Byte[]>> extensions,
                               Byte[] signatureKey, Byte[] signature)
        {
            this.CertAlgorithm     = certAlgorithm;
            this.Blob              = blob;
            this.SignedBytes       = signedBytes;
            this.SubjectPublicKey  = subjectPublicKey;
            this.SubjectAlgorithm  = subjectAlgorithm;
            this.Serial            = serial;
            this.Type              = type;
            this.KeyId             = keyId;
            this.Principals        = principals;
            this.ValidAfter        = validAfter;
            this.ValidBefore       = validBefore;
            this.CriticalOptions   = criticalOptions;
            this.Extensions        = extensions;
            this.SignatureKey      = signatureKey;
            this.Signature         = signature;
        }

        #endregion


        #region (static) IsCertificateAlgorithm(Name)

        /// <summary>Whether a key/algorithm name denotes an OpenSSH certificate.</summary>
        public static Boolean IsCertificateAlgorithm(String Name)
            => Name.EndsWith(CertSuffix, StringComparison.Ordinal);

        #endregion

        #region (static) Parse(Blob) / TryParse

        /// <summary>Parse an OpenSSH certificate from its wire blob.</summary>
        public static SshCertificate Parse(Byte[] Blob)
        {

            var reader        = new SshPacketReader(Blob);
            var certAlgorithm = reader.ReadString();

            if (!IsCertificateAlgorithm(certAlgorithm))
                throw new SshWireException($"'{certAlgorithm}' is not an OpenSSH certificate algorithm.");

            var subjectAlgorithm = certAlgorithm[..^CertSuffix.Length];
            var nonce            = reader.ReadBinaryString();

            // The subject public key is encoded inline (type-specific); rebuild it as a plain key blob.
            var subjectPublicKey = ReadSubjectPublicKey(subjectAlgorithm, nonce, ref reader);

            var serial        = reader.ReadUInt64();
            var type          = (SshCertType) reader.ReadUInt32();
            var keyId         = reader.ReadString();
            var principals    = ReadStringSequence(reader.ReadBinaryString());
            var validAfter    = FromUnix(reader.ReadUInt64());
            var validBefore   = FromUnix(reader.ReadUInt64());
            var critical      = ReadTupleSequence(reader.ReadBinaryString());
            var extensions    = ReadTupleSequence(reader.ReadBinaryString());
            _                 = reader.ReadBinaryString();   // reserved
            var signatureKey  = reader.ReadBinaryString();

            var signedBytes   = Blob[..reader.Position];      // everything up to (not incl.) the signature
            var signature     = reader.ReadBinaryString();

            return new SshCertificate(certAlgorithm, Blob, signedBytes, subjectPublicKey, subjectAlgorithm,
                                      serial, type, keyId, principals, validAfter, validBefore,
                                      critical, extensions, signatureKey, signature);

        }

        /// <summary>Try to parse an OpenSSH certificate.</summary>
        public static Boolean TryParse(Byte[] Blob, out SshCertificate? Certificate)
        {
            try   { Certificate = Parse(Blob); return true; }
            catch { Certificate = null;        return false; }
        }

        #endregion


        #region (private) subject public key

        private static Byte[] ReadSubjectPublicKey(String SubjectAlgorithm, Byte[] Nonce, ref SshPacketReader Reader)
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteString(SubjectAlgorithm);

            switch (SubjectAlgorithm)
            {

                case SshAlgorithmNames.HostKey.Ed25519:
                    writer.WriteBinaryString(Reader.ReadBinaryString());     // pk
                    break;

                case SshAlgorithmNames.HostKey.EcdsaNistP256 or
                     SshAlgorithmNames.HostKey.EcdsaNistP384 or
                     SshAlgorithmNames.HostKey.EcdsaNistP521:
                    writer.WriteBinaryString(Reader.ReadBinaryString());     // curve id
                    writer.WriteBinaryString(Reader.ReadBinaryString());     // Q
                    break;

                case SshAlgorithmNames.HostKey.SshRsa:
                    writer.WriteMPInt(Reader.ReadMPInt());                    // e
                    writer.WriteMPInt(Reader.ReadMPInt());                    // n
                    break;

                default:
                    throw new SshWireException($"Unsupported certificate subject algorithm '{SubjectAlgorithm}'.");

            }

            return abw.WrittenSpan.ToArray();

        }

        #endregion

        #region (private) sequence readers

        // A "string" field that itself contains a sequence of SSH strings.
        private static List<String> ReadStringSequence(Byte[] Field)
        {
            var reader  = new SshPacketReader(Field);
            var result  = new List<String>();
            while (reader.Position < Field.Length)
                result.Add(reader.ReadString());
            return result;
        }

        // A "string" field containing a sequence of (string name, string data) tuples.
        private static List<KeyValuePair<String, Byte[]>> ReadTupleSequence(Byte[] Field)
        {
            var reader  = new SshPacketReader(Field);
            var result  = new List<KeyValuePair<String, Byte[]>>();
            while (reader.Position < Field.Length)
            {
                var name = reader.ReadString();
                var data = reader.ReadBinaryString();
                result.Add(new KeyValuePair<String, Byte[]>(name, data));
            }
            return result;
        }

        private static DateTimeOffset FromUnix(UInt64 Seconds)
            => Seconds >= (UInt64) DateTimeOffset.MaxValue.ToUnixTimeSeconds()
                   ? DateTimeOffset.MaxValue
                   : DateTimeOffset.FromUnixTimeSeconds((Int64) Seconds);

        #endregion

    }

}
