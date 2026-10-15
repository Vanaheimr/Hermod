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

using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

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


        #region (static) Load    (CertificatePath, KeyPath = null, Password = null)

        /// <summary>
        /// A chain out of files, whichever of the two usual forms they are in:
        /// PEM - what certbot and most certificate authorities hand out - or
        /// PKCS#12.
        /// </summary>
        /// <param name="CertificatePath">A PEM file with the certificate and its intermediates (certbot's fullchain.pem), or a PKCS#12 file.</param>
        /// <param name="KeyPath">For PEM: the file with the private key (certbot's privkey.pem); null when it is in the certificate file.</param>
        /// <param name="Password">For PKCS#12: its password, if it has one.</param>
        /// <remarks>
        /// Told apart by what is in the file, not by its name: a PEM file is
        /// text and starts with "-----BEGIN", whatever it is called.
        /// </remarks>
        public static ServerCertificateChain Load(String   CertificatePath,
                                                  String?  KeyPath    = null,
                                                  String?  Password   = null)

            => FromOctets(File.ReadAllBytes(CertificatePath),
                          KeyPath is not null ? File.ReadAllBytes(KeyPath) : null,
                          Password,
                          CertificatePath);

        #endregion

        #region (static) FromOctets(CertificateOctets, KeyOctets = null, Password = null, Source = null)

        /// <summary>
        /// A chain out of the content of such files: PEM when it is text that
        /// starts with "-----BEGIN", PKCS#12 otherwise.
        /// </summary>
        public static ServerCertificateChain FromOctets(Byte[]   CertificateOctets,
                                                        Byte[]?  KeyOctets   = null,
                                                        String?  Password    = null,
                                                        String?  Source      = null)

            => LooksLikePem(CertificateOctets)
                   ? FromPem(Encoding.UTF8.GetString(CertificateOctets),
                             KeyOctets is not null ? Encoding.UTF8.GetString(KeyOctets) : null,
                             Source)
                   : FromPkcs12(CertificateOctets, Password, Source);

        #endregion

        #region (static) FromPem (CertificatesPem, KeyPem = null, Source = null)

        /// <summary>
        /// A chain out of PEM text: the certificates - the server's own and its
        /// intermediates, in any order - and the private key of the server's,
        /// as RSA or ECDSA, PKCS#1, SEC 1 or PKCS#8.
        /// </summary>
        /// <param name="CertificatesPem">The CERTIFICATE blocks, as in certbot's fullchain.pem.</param>
        /// <param name="KeyPem">The private key; null when it is among the certificates' text.</param>
        /// <param name="Source">Where it came from, for the message of an exception.</param>
        /// <remarks>
        /// The server's certificate is the one the key belongs to - not simply
        /// the first, so that a file in another order does not hand a client an
        /// intermediate as the server's certificate. And a key that belongs to
        /// none of them is said in words: it is what a renewal looks like that
        /// has replaced one file but not yet the other.
        /// </remarks>
        public static ServerCertificateChain FromPem(String   CertificatesPem,
                                                     String?  KeyPem   = null,
                                                     String?  Source   = null)
        {

            var source        = Source is not null ? $"'{Source}'" : "the PEM text";
            var certificates  = new X509Certificate2Collection();

            certificates.ImportFromPem(CertificatesPem);

            if (certificates.Count == 0)
                throw new CryptographicException($"{source} holds no certificate.");

            var keyPem = KeyPem ?? CertificatesPem;

            foreach (var candidate in certificates)
            {

                X509Certificate2 withKey;

                try
                {
                    withKey = X509Certificate2.CreateFromPem(candidate.ExportCertificatePem(), keyPem);
                }
                catch (CryptographicException)
                {
                    // Not this one's key - or no key at all, which the end says.
                    continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                var leaf = Persisted(withKey);

                return new ServerCertificateChain(
                           leaf,
                           certificates.Where(certificate => certificate.Thumbprint != leaf.Thumbprint)
                       );

            }

            throw new CryptographicException(KeyPem is null && !CertificatesPem.Contains("PRIVATE KEY", StringComparison.Ordinal)
                                                 ? $"{source} holds no private key, and none was given."
                                                 : $"The private key does not belong to any certificate in {source} - " +
                                                   "half of a renewal, or a key and a certificate of different issues.");

        }

        #endregion

        #region (static) FromPkcs12(Octets, Password = null, Source = null)

        /// <summary>
        /// A chain out of PKCS#12: the certificate with the private key, and
        /// whatever other certificates came with it as its intermediates.
        /// </summary>
        public static ServerCertificateChain FromPkcs12(Byte[]   Octets,
                                                        String?  Password   = null,
                                                        String?  Source     = null)
        {

            var certificates = X509CertificateLoader.LoadPkcs12Collection(
                                   Octets,
                                   Password,
                                   X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable
                               );

            var leaf = certificates.FirstOrDefault(certificate => certificate.HasPrivateKey)
                           ?? throw new CryptographicException($"{(Source is not null ? $"'{Source}'" : "The PKCS#12 data")} holds no certificate with a private key.");

            return new ServerCertificateChain(
                       leaf,
                       certificates.Where(certificate => certificate.Thumbprint != leaf.Thumbprint)
                   );

        }

        #endregion

        #region (private static) LooksLikePem(Octets) / Persisted(Certificate)

        private static Boolean LooksLikePem(ReadOnlySpan<Byte> Octets)
        {

            var text = Octets.Length >= 3 && Octets[0] == 0xEF && Octets[1] == 0xBB && Octets[2] == 0xBF
                           ? Octets[3..]
                           : Octets;

            while (text.Length > 0 && (text[0] is (Byte) ' ' or (Byte) '\t' or (Byte) '\r' or (Byte) '\n'))
                text = text[1..];

            return text.StartsWith("-----BEGIN"u8);

        }

        /// <summary>
        /// The certificate with a key a TLS server can use everywhere.
        /// </summary>
        /// <remarks>
        /// A key read from PEM is ephemeral, and SChannel on Windows refuses to
        /// present a certificate whose key is not in a key store. Through
        /// PKCS#12 it is; elsewhere the round trip changes nothing and is left
        /// out.
        /// </remarks>
        private static X509Certificate2 Persisted(X509Certificate2 Certificate)
        {

            if (!OperatingSystem.IsWindows())
                return Certificate;

            using (Certificate)
                return X509CertificateLoader.LoadPkcs12(Certificate.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);

        }

        #endregion


        #region TryCreateContext(out Context, out Error)

        /// <summary>
        /// Build the TLS context a server needs in order to present this chain,
        /// or say why it cannot be built.
        /// </summary>
        /// <remarks>
        /// <b>This is where a certificate that cannot be served is found out
        /// about.</b> .NET does not merely hold the bytes: it builds and checks
        /// the chain of the server's own certificate while making the context,
        /// and throws when it cannot complete it - which happens when the
        /// issuing certificate authority's root is neither sent along nor known
        /// to this machine. Whoever asks a server to present such a certificate
        /// gets a TLS handshake that is reset without explanation.
        ///
        /// So this is offered as a question rather than only done in the middle
        /// of a handshake: the answer is the same, and asked in advance it can
        /// be put in front of somebody who can fix it.
        ///
        /// Note that the chain only has to be <em>buildable</em>, not trusted.
        /// A private authority whose root is in the machine's store is fine; a
        /// private authority whose root is nowhere is not.
        /// </remarks>
        public Boolean TryCreateContext([NotNullWhen(true)]  out SslStreamCertificateContext?  Context,
                                        [NotNullWhen(false)] out String?                      Error)
        {

            Context  = null;
            Error    = null;

            try
            {

                Context = SslStreamCertificateContext.Create(
                              target:                   Certificate,
                              additionalCertificates:   HasIntermediates ? Intermediates : null,
                              trust:                    null,
                              offline:                  true
                          );

                return true;

            }
            catch (Exception e)
            {

                // The exception .NET raises here is "An unknown chain building
                // error occurred", which names neither the certificate nor what
                // is missing. Said again in words somebody can act on.
                Error = $"The chain of '{Certificate.Subject}' cannot be built on this machine" +
                        $"{(HasIntermediates ? $", although {Intermediates.Count} intermediate(s) were sent with it" : " and no intermediates were sent with it")}" +
                        $": {e.Message} " +
                         "A TLS server cannot present a certificate whose chain does not lead to a root this machine has - " +
                         "either send the intermediates that lead to one, or put the root of the issuing authority into the machine's certificate store.";

                return false;

            }

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
