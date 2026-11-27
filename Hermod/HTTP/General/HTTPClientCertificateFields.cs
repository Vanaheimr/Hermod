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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using System.Diagnostics.CodeAnalysis;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;


    /// <summary>
    /// The Client-Cert and Client-Cert-Chain fields (RFC 9440): how a
    /// TLS-terminating reverse proxy tells the server behind it which
    /// certificate the client authenticated with, and the chain it was
    /// validated through.
    ///
    ///    Client-Cert       = sf-binary             ; the DER of the end-entity certificate
    ///    Client-Cert-Chain = sf-list of sf-binary  ; in TLS order, without the end-entity certificate
    ///
    /// Client-Cert is a singleton (Section 2.2). Client-Cert-Chain may be split
    /// over several field lines (Section 2.3), MUST NOT appear without
    /// Client-Cert, and may leave out the trust anchor.
    ///
    /// Both fields are a claim of whoever wrote them, and anyone can write
    /// them: "backend servers MUST only accept the Client-Cert and
    /// Client-Cert-Chain header fields from a trusted TTRP" (Section 4). Nothing
    /// in the stack reads them on its own; they say nothing about the connection
    /// they arrived on, which is what <see cref="HTTPRequest.ClientCertificate"/>
    /// is about. The proxy's side of the bargain is to remove or overwrite any
    /// that came from the client (Section 2.4), which is what
    /// <see cref="HTTPRequest.Builder.ForwardClientCertificate"/> does.
    ///
    /// A value is taken only as RFC 9440 defines it: a byte sequence, whose
    /// octets are one DER-encoded X.509 certificate and nothing else. Parameters
    /// are ignored, none being defined. Anything else is no certificate at all,
    /// rather than the part of it that could be read.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9440.html"/>
    public static class HTTPClientCertificateFields
    {

        #region Data

        /// <summary>
        /// The name of the field holding the client's certificate.
        /// </summary>
        public const String ClientCert       = "Client-Cert";

        /// <summary>
        /// The name of the field holding the chain the client's certificate was
        /// validated through.
        /// </summary>
        public const String ClientCertChain  = "Client-Cert-Chain";

        #endregion


        #region FieldValue     (Certificate)

        /// <summary>
        /// The Client-Cert field value of the given certificate: its DER, as a
        /// byte sequence.
        /// </summary>
        /// <param name="Certificate">The client's end-entity certificate.</param>
        public static String FieldValue(X509Certificate2 Certificate)

            => ByteSequenceOf(Certificate).ToString();

        #endregion

        #region ChainFieldValue(Chain)

        /// <summary>
        /// The Client-Cert-Chain field value of the given certificates, in the
        /// order given: the TLS order, the issuer of the end-entity certificate
        /// first (RFC 9440, Section 2.3).
        /// </summary>
        /// <param name="Chain">The chain, without the end-entity certificate.</param>
        public static String ChainFieldValue(IEnumerable<X509Certificate2> Chain)

            => StructuredFields.Serialize(Chain.Select(ByteSequenceOf));

        #endregion


        #region TryParse       (FieldValue, out Certificate)

        /// <summary>
        /// Read a Client-Cert field value. False when it is not a single byte
        /// sequence, or its octets are not exactly one DER-encoded certificate.
        /// </summary>
        /// <param name="FieldValue">The field value.</param>
        /// <param name="Certificate">The certificate.</param>
        public static Boolean TryParse(String?                                     FieldValue,
                                       [NotNullWhen(true)] out X509Certificate2?   Certificate)
        {

            Certificate = null;

            if (FieldValue is null ||
                !StructuredFields.TryParseItem(FieldValue, out var item) ||
                item.Value.Type != SFType.ByteSequence)
            {
                return false;
            }

            Certificate = FromDER(item.Value.AsByteSequence);

            return Certificate is not null;

        }

        #endregion

        #region TryParseChain  (FieldValue, out Chain)

        /// <summary>
        /// Read a Client-Cert-Chain field value, its field lines joined with ", ".
        /// False when it is not a list, or any member of it is not a byte sequence
        /// holding exactly one DER-encoded certificate: a chain with a link
        /// missing is not a shorter chain.
        /// </summary>
        /// <param name="FieldValue">The field value.</param>
        /// <param name="Chain">The certificates, in the order of the field.</param>
        public static Boolean TryParseChain(String?                                                  FieldValue,
                                            [NotNullWhen(true)] out IReadOnlyList<X509Certificate2>?  Chain)
        {

            Chain = null;

            if (FieldValue is null ||
                !StructuredFields.TryParseList(FieldValue, out var members))
            {
                return false;
            }

            var certificates = new List<X509Certificate2>(members.Count);

            foreach (var member in members)
            {

                var certificate = member is SFItem item && item.Value.Type == SFType.ByteSequence
                                      ? FromDER(item.Value.AsByteSequence)
                                      : null;

                if (certificate is null)
                {
                    foreach (var read in certificates)
                        read.Dispose();
                    return false;
                }

                certificates.Add(certificate);

            }

            Chain = certificates;
            return true;

        }

        #endregion


        #region (private static) ByteSequenceOf(Certificate)

        private static SFItem ByteSequenceOf(X509Certificate2 Certificate)

            => new (SFBareItem.ByteSequence(Certificate.RawData));

        #endregion

        #region (private static) FromDER(DER)

        /// <summary>
        /// The certificate whose DER the given octets are, and null when they are
        /// anything else. The loader also takes PEM, and what it reads need not
        /// span every octet; comparing what it read with what it was given rules
        /// both out.
        /// </summary>
        private static X509Certificate2? FromDER(Byte[] DER)
        {

            X509Certificate2 certificate;

            try
            {
                certificate = X509CertificateLoader.LoadCertificate(DER);
            }
            catch (CryptographicException)
            {
                return null;
            }

            if (certificate.RawData.AsSpan().SequenceEqual(DER))
                return certificate;

            certificate.Dispose();
            return null;

        }

        #endregion

    }

}
