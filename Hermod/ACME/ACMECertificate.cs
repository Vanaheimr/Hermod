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

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// The kind of key a certificate is ordered for.
    /// </summary>
    public enum ACMEKeyType
    {

        /// <summary>ECDSA on P-256: small, fast, and what every TLS client of the last ten years takes.</summary>
        ECDSA_P256,

        /// <summary>ECDSA on P-384.</summary>
        ECDSA_P384,

        /// <summary>RSA with 2048 bits, for clients that know nothing else.</summary>
        RSA_2048,

        /// <summary>RSA with 4096 bits.</summary>
        RSA_4096

    }


    /// <summary>
    /// A certificate that came from an ACME server, with its private key: as
    /// the PEM texts that go into certbot's fullchain.pem and privkey.pem, and
    /// as the chain a Hermod TLS server presents.
    /// </summary>
    public sealed class ACMECertificate
    {

        #region Properties

        /// <summary>
        /// The certificate and its intermediates, in PEM, as the server sent them.
        /// </summary>
        public String                        FullChainPem     { get; }

        /// <summary>
        /// The private key, in PKCS#8 PEM.
        /// </summary>
        public String                        PrivateKeyPem    { get; }

        /// <summary>
        /// The certificate with its key, and its intermediates.
        /// </summary>
        public ServerCertificateChain        Chain            { get; }

        /// <summary>
        /// The certificate itself.
        /// </summary>
        public X509Certificate2              Certificate
            => Chain.Certificate;

        /// <summary>
        /// From when it holds.
        /// </summary>
        public DateTimeOffset                NotBefore        { get; }

        /// <summary>
        /// Until when it holds.
        /// </summary>
        public DateTimeOffset                NotAfter         { get; }

        /// <summary>
        /// The names and addresses it is for, from its subject alternative names.
        /// </summary>
        public IReadOnlyList<ACMEIdentifier> Identifiers      { get; }

        #endregion

        #region Constructor(s)

        private ACMECertificate(String                  FullChainPem,
                                String                  PrivateKeyPem,
                                ServerCertificateChain  Chain)
        {

            this.FullChainPem   = FullChainPem;
            this.PrivateKeyPem  = PrivateKeyPem;
            this.Chain          = Chain;
            this.NotBefore      = new DateTimeOffset(Chain.Certificate.NotBefore.ToUniversalTime());
            this.NotAfter       = new DateTimeOffset(Chain.Certificate.NotAfter. ToUniversalTime());
            this.Identifiers    = IdentifiersOf(Chain.Certificate);

        }

        #endregion


        #region (static) FromPem(FullChainPem, PrivateKeyPem, Source = null)

        /// <summary>
        /// A certificate out of the two PEM texts.
        /// </summary>
        /// <param name="FullChainPem">The certificate and its intermediates.</param>
        /// <param name="PrivateKeyPem">Its private key.</param>
        /// <param name="Source">Where they came from, for the message of an exception.</param>
        public static ACMECertificate FromPem(String   FullChainPem,
                                              String   PrivateKeyPem,
                                              String?  Source   = null)

            => new (FullChainPem,
                    PrivateKeyPem,
                    ServerCertificateChain.FromPem(FullChainPem, PrivateKeyPem, Source));

        #endregion

        #region IsFor(Identifiers)

        /// <summary>
        /// Whether it is for exactly these names and addresses - no fewer, and
        /// no more either: a name dropped from a server's configuration is to be
        /// dropped from its certificate as well.
        /// </summary>
        public Boolean IsFor(IEnumerable<ACMEIdentifier> Identifiers)

            => this.Identifiers.ToHashSet().SetEquals(Identifiers);

        #endregion

        #region (static) IdentifiersOf(Certificate)

        /// <summary>
        /// The names and addresses a certificate is for, from its subject
        /// alternative names.
        /// </summary>
        public static IReadOnlyList<ACMEIdentifier> IdentifiersOf(X509Certificate2 Certificate)
        {

            var identifiers = new List<ACMEIdentifier>();

            foreach (var extension in Certificate.Extensions)
            {
                if (extension.Oid?.Value == "2.5.29.17")
                {

                    var subjectAlternativeNames = new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical);

                    identifiers.AddRange(subjectAlternativeNames.EnumerateDnsNames().   Select(name    => new ACMEIdentifier("dns", name.ToLowerInvariant())));
                    identifiers.AddRange(subjectAlternativeNames.EnumerateIPAddresses().Select(address => new ACMEIdentifier("ip",  address.ToString())));

                }
            }

            return identifiers.Distinct().ToArray();

        }

        #endregion

        #region ToString()

        /// <inheritdoc/>
        public override String ToString()

            => $"{String.Join(", ", Identifiers)}, valid until {NotAfter:yyyy-MM-dd HH:mm:ss} UTC, issued by {Certificate.Issuer}";

        #endregion

    }

}
