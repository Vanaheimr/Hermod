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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A signing key presented together with its OpenSSH certificate: it advertises the certificate
    /// algorithm and blob (so the peer receives the certificate), but signs with the underlying private
    /// key. Used for both certificate-based client authentication and host certificates.
    /// </summary>
    public sealed class CertifiedKey : ISshHostKey
    {

        #region Data

        private readonly ISshHostKey  baseKey;

        #endregion

        #region Properties

        /// <summary>
        /// The certificate presented for this key.
        /// </summary>
        public SshCertificate  Certificate  { get; }

        /// <summary>
        /// The certificate algorithm (e.g. <c>ssh-ed25519-cert-v01@openssh.com</c>).
        /// </summary>
        public IReadOnlyList<String> AlgorithmNames => [ Certificate.CertAlgorithm ];

        /// <summary>
        /// The public-key blob presented to the peer — the whole certificate.
        /// </summary>
        public Byte[] PublicKeyBlob => Certificate.Blob;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Pair a base signing key with a certificate issued for its public key.
        /// </summary>
        public CertifiedKey(ISshHostKey BaseKey, SshCertificate Certificate)
        {

            if (!BaseKey.PublicKeyBlob.AsSpan().SequenceEqual(Certificate.SubjectPublicKey))
                throw new ArgumentException("The certificate was not issued for this key's public key.", nameof(Certificate));

            this.baseKey      = BaseKey;
            this.Certificate  = Certificate;

        }

        #endregion


        #region (static) Load(BaseKey, CertificateLine)

        /// <summary>
        /// Load a certified key from a base key and an <c>id_*-cert.pub</c> certificate line.
        /// </summary>
        public static CertifiedKey Load(ISshHostKey BaseKey, String CertificateLine)
            => new (BaseKey, SshCertificate.Parse(SshPublicKey.Parse(CertificateLine).Blob));

        #endregion

        #region Sign(AlgorithmName, Data)

        /// <summary>
        /// Sign with the underlying base key. The signature uses the base algorithm (e.g. ssh-ed25519),
        /// not the certificate algorithm — the certificate only carries the public key to the peer.
        /// </summary>
        public Byte[] Sign(String AlgorithmName, ReadOnlySpan<Byte> Data)
            => baseKey.Sign(baseKey.AlgorithmNames[0], Data);

        #endregion

    }

}
