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
    /// What a TLS server sends a client: its own certificate and the
    /// intermediate certificates that lead from it towards a root the client
    /// may already trust.
    /// </summary>
    /// <remarks>
    /// <b>Both halves come from one place on purpose.</b> A certificate that is
    /// renewed while the server runs brings its own chain along - a certificate
    /// authority does change its intermediates, Let's Encrypt went from R3 to
    /// R10 and R11 - and a server that took the leaf from one source and the
    /// chain from another would sooner or later send a pair that does not
    /// belong together. So the selector hands over both or neither.
    ///
    /// <b>Why the chain has to be sent at all.</b> A client is expected to know
    /// root certificates, not intermediates. TLS therefore has the server send
    /// everything in between (RFC 8446, section 4.4.2). A server that sends
    /// only its own certificate works against browsers - they cache
    /// intermediates and fetch what they lack through the issuer's AIA URL -
    /// and fails against everything stricter, which is most non-browser
    /// clients.
    ///
    /// The root itself is not sent. It is either already trusted, in which case
    /// sending it is a waste, or it is not, in which case sending it changes
    /// nothing.
    /// </remarks>
    public sealed class ServerCertificateChain
    {

        #region Data

        private readonly String  cacheKey;

        #endregion

        #region Properties

        /// <summary>
        /// The certificate of this server, with its private key.
        /// </summary>
        public X509Certificate2            Certificate      { get; }

        /// <summary>
        /// The intermediate certificates between it and a root, in order,
        /// without the leaf and without the root. May be empty.
        /// </summary>
        public X509Certificate2Collection  Intermediates    { get; }

        /// <summary>
        /// Whether anything beyond the certificate itself is sent.
        /// </summary>
        public Boolean                     HasIntermediates
            => Intermediates.Count > 0;

        /// <summary>
        /// What tells two chains apart - the thumbprints of everything in them.
        /// Whoever caches a TLS context keys it by this and not by the
        /// thumbprint of the certificate alone, or a context built before the
        /// intermediates were known would go on being used after they are.
        /// </summary>
        public String                      CacheKey
            => cacheKey;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A server certificate and the intermediates that lead to it.
        /// </summary>
        /// <param name="Certificate">The certificate of this server, with its private key.</param>
        /// <param name="Intermediates">
        /// The intermediates, in order from the issuer of the certificate
        /// towards the root. The leaf is dropped if it turns up here again, and
        /// so is a self-signed certificate: a root sent along is bytes on the
        /// wire that change nothing.
        /// </param>
        public ServerCertificateChain(X509Certificate2                Certificate,
                                      IEnumerable<X509Certificate2>?  Intermediates   = null)
        {

            this.Certificate    = Certificate ?? throw new ArgumentNullException(nameof(Certificate));
            this.Intermediates  = [];

            if (Intermediates is not null)
            {
                foreach (var intermediate in Intermediates)
                {

                    if (intermediate is null ||
                        intermediate.Thumbprint == Certificate.Thumbprint ||
                        IsSelfSigned(intermediate))
                    {
                        continue;
                    }

                    this.Intermediates.Add(intermediate);

                }
            }

            this.cacheKey = this.Intermediates.Count == 0
                                ? Certificate.Thumbprint
                                : String.Concat(
                                      Certificate.Thumbprint,
                                      ":",
                                      String.Join(",", this.Intermediates.
                                                           OfType<X509Certificate2>().
                                                           Select(certificate => certificate.Thumbprint))
                                  );

        }

        #endregion


        #region (private static) IsSelfSigned(Certificate)

        /// <summary>
        /// Whether this certificate is its own issuer, i.e. a root.
        /// </summary>
        private static Boolean IsSelfSigned(X509Certificate2 Certificate)

            => Certificate.SubjectName.RawData.AsSpan().
                   SequenceEqual(Certificate.IssuerName.RawData);

        #endregion

        #region (override) ToString()

        public override String ToString()

            => HasIntermediates
                   ? $"{Certificate.Subject} via {String.Join(", ", Intermediates.OfType<X509Certificate2>().Select(certificate => certificate.Subject))}"
                   : $"{Certificate.Subject} (no intermediates)";

        #endregion

    }

}
