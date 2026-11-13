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

using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The key a TLS peer proved to hold in a handshake, and what it came in:
    /// an X.509 certificate, or nothing at all - a raw public key (RFC 7250).
    /// </summary>
    /// <remarks>
    /// This is what every TLS stack of Hermod hands to the layers above it,
    /// whatever the peer presented: its own TLS 1.3 under QUIC, and the stacks
    /// under TCP. A key that came raw has no certificate, no subject, no
    /// validity period and no chain: it is its SubjectPublicKeyInfo, and its
    /// key pin is what identifies it.
    /// </remarks>
    public sealed class TLSPeerKey : IEquatable<TLSPeerKey>
    {

        #region Data

        private readonly Byte[] subjectPublicKeyInfo;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the key came in a certificate or raw.
        /// </summary>
        public TLSCertificateType    CertificateType    { get; }

        /// <summary>
        /// The DER encoded SubjectPublicKeyInfo of the key.
        /// </summary>
        public ReadOnlyMemory<Byte>  SubjectPublicKeyInfo
            => subjectPublicKeyInfo;

        /// <summary>
        /// The key pin of the key: SHA-256 over its SubjectPublicKeyInfo.
        /// </summary>
        public KeyPin                KeyPin             { get; }

        /// <summary>
        /// The certificate the key came in, or null for a raw public key.
        /// It is not owned by this object and may have been disposed since.
        /// </summary>
        public X509Certificate2?     Certificate        { get; }

        /// <summary>
        /// Whether the key came without a certificate (RFC 7250).
        /// </summary>
        public Boolean               IsRawPublicKey
            => CertificateType == TLSCertificateType.RawPublicKey;

        #endregion

        #region Constructor(s)

        private TLSPeerKey(TLSCertificateType  CertificateType,
                           Byte[]              SubjectPublicKeyInfo,
                           X509Certificate2?   Certificate)
        {

            this.CertificateType       = CertificateType;
            this.subjectPublicKeyInfo  = SubjectPublicKeyInfo;
            this.KeyPin                = KeyPin.FromSubjectPublicKeyInfo(SubjectPublicKeyInfo);
            this.Certificate           = Certificate;

        }

        #endregion


        #region (static) Raw            (SubjectPublicKeyInfo)

        /// <summary>
        /// A key that came raw: the DER encoded SubjectPublicKeyInfo of the
        /// Certificate message, as it was sent.
        /// </summary>
        /// <param name="SubjectPublicKeyInfo">The DER encoded SubjectPublicKeyInfo of the key.</param>
        public static TLSPeerKey Raw(ReadOnlySpan<Byte> SubjectPublicKeyInfo)

            => new (TLSCertificateType.RawPublicKey,
                    SubjectPublicKeyInfo.ToArray(),
                    null);

        #endregion

        #region (static) FromCertificate(Certificate)

        /// <summary>
        /// The key of the given certificate.
        /// </summary>
        /// <param name="Certificate">The certificate a peer presented.</param>
        public static TLSPeerKey FromCertificate(X509Certificate2 Certificate)

            => new (TLSCertificateType.X509,
                    Certificate.PublicKey.ExportSubjectPublicKeyInfo(),
                    Certificate);

        #endregion


        #region Operator overloading

        public static Boolean operator == (TLSPeerKey? PeerKey1, TLSPeerKey? PeerKey2)

            => PeerKey1 is null
                   ? PeerKey2 is null
                   : PeerKey1.Equals(PeerKey2);

        public static Boolean operator != (TLSPeerKey? PeerKey1, TLSPeerKey? PeerKey2)

            => !(PeerKey1 == PeerKey2);

        #endregion

        #region IEquatable<TLSPeerKey> Members

        /// <summary>
        /// Two peer keys are the same when they are the same key and came in
        /// the same type. The certificate around a key is not compared.
        /// </summary>
        public override Boolean Equals(Object? Object)

            => Object is TLSPeerKey peerKey &&
               Equals(peerKey);

        /// <summary>
        /// Two peer keys are the same when they are the same key and came in
        /// the same type. The certificate around a key is not compared.
        /// </summary>
        public Boolean Equals(TLSPeerKey? PeerKey)

            => PeerKey is not null &&
               CertificateType == PeerKey.CertificateType &&
               subjectPublicKeyInfo.AsSpan().SequenceEqual(PeerKey.subjectPublicKeyInfo);

        #endregion

        #region (override) GetHashCode()

        public override Int32 GetHashCode()

            => HashCode.Combine(CertificateType, KeyPin);

        #endregion

        #region (override) ToString()

        public override String ToString()

            => $"{KeyPin} ({CertificateType})";

        #endregion

    }

}
