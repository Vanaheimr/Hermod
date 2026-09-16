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

using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.PKI
{

    /// <summary>
    /// A kind of key a device will generate for itself, and what it signs a
    /// certificate request with.
    /// </summary>
    /// <remarks>
    /// <b>Everything goes through Bouncy Castle, including the kinds .NET could
    /// do by itself.</b> One path rather than two: the day a key is an Ed448 or
    /// an ML-DSA one, the .NET half of a split would have to hand over to the
    /// other half anyway, and a store that generates one way and reads back
    /// another is a store with two sets of bugs in it.
    ///
    /// <b>Generating a key and presenting it are different questions.</b> Every
    /// kind here can be generated, written down and made into a signing
    /// request. Whether the platform underneath can then hold the answer up to
    /// somebody during a TLS handshake is something else entirely, and it is
    /// not decided here or by a list written from memory - it is found out by
    /// trying, on the machine this is running on. See
    /// <see cref="CanBePresented"/>.
    /// </remarks>
    /// <param name="Id">How it is written in a configuration and in an API.</param>
    /// <param name="Name">How it is written on a page.</param>
    /// <param name="SignatureAlgorithm">What Bouncy Castle calls the signature it makes.</param>
    /// <param name="Remark">What somebody choosing it should know.</param>
    public sealed record KeyAlgorithm(String  Id,
                                      String  Name,
                                      String  SignatureAlgorithm,
                                      String  Remark)
    {

        #region Data

        /// <summary>
        /// What can be generated, in the order somebody should consider them:
        /// what everything handles, then what a stricter operator may ask for,
        /// then what is being prepared for.
        /// </summary>
        public static readonly IReadOnlyList<KeyAlgorithm> All = [

            new ("ecdsa-p256",         "ECDSA P-256 (secp256r1)",   "SHA256withECDSA",
                 "What a device with a small processor handles best, and what every one of them understands."),

            new ("ecdsa-p384",         "ECDSA P-384 (secp384r1)",   "SHA384withECDSA",
                 "A larger curve, still understood everywhere that understands P-256."),

            new ("ecdsa-p521",         "ECDSA P-521 (secp521r1)",   "SHA512withECDSA",
                 "The largest of the NIST curves. Widely supported in certificates and rather less widely in TLS itself."),

            new ("rsa-2048",           "RSA 2048",                  "SHA256withRSA",
                 "The smallest RSA key still generally accepted, and what an older device may be the only thing able to verify."),

            new ("rsa-3072",           "RSA 3072",                  "SHA256withRSA",
                 "What some certificate authorities still issue and nothing else."),

            new ("rsa-4096",           "RSA 4096",                  "SHA256withRSA",
                 "The same, larger and slower; a small device notices the difference at every handshake."),

            new ("ed25519",            "Ed25519",                   "Ed25519",
                 "Small, fast and modern. TLS 1.3 allows it (RFC 8422), but many platforms will not yet present it."),

            new ("ed448",              "Ed448",                     "Ed448",
                 "Ed25519's larger relative, and supported in fewer places again."),

            new ("ml-dsa-44",          "ML-DSA-44 (FIPS 204)",      "ML-DSA-44",
                 "Post-quantum, NIST level 1. Certificates and requests work today; TLS is still being standardised around it."),

            new ("ml-dsa-65",          "ML-DSA-65 (FIPS 204)",      "ML-DSA-65",
                 "Post-quantum, NIST level 3 - the parameter set most people mean by ML-DSA."),

            new ("ml-dsa-87",          "ML-DSA-87 (FIPS 204)",      "ML-DSA-87",
                 "Post-quantum, NIST level 5."),

            new ("slh-dsa-sha2-128s",  "SLH-DSA-SHA2-128s (FIPS 205)", "SLH-DSA-SHA2-128S",
                 "Post-quantum on hash functions alone, so it rests on the fewest assumptions - and signs slowly, with large signatures."),

            new ("slh-dsa-sha2-192s",  "SLH-DSA-SHA2-192s (FIPS 205)", "SLH-DSA-SHA2-192S",
                 "The same, at NIST level 3.")

        ];

        /// <summary>
        /// What a key is generated as when nobody says otherwise.
        /// </summary>
        public const String DefaultId = "ecdsa-p256";

        /// <summary>
        /// What was found out about presenting each kind, by trying it. Keyed
        /// by identification, because the answer belongs to the algorithm and
        /// to this machine, not to one certificate.
        /// </summary>
        private static readonly ConcurrentDictionary<String, Boolean> presentable = [];

        /// <summary>
        /// How long the question is given before the answer is no.
        /// </summary>
        public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

        #endregion

        #region Properties

        /// <summary>
        /// Whether this is a key the platform underneath is known to be able to
        /// present, or null while nobody has tried.
        /// </summary>
        public Boolean? KnownToBePresentable
            => presentable.TryGetValue(Id, out var known) ? known : null;

        /// <summary>
        /// Whether a key of this kind can encipher, which decides whether
        /// "keyEncipherment" belongs in a certificate request made with it.
        /// </summary>
        /// <remarks>
        /// Only RSA can. An elliptic curve key agrees rather than enciphers,
        /// and RFC 8410 section 5 says plainly that an Ed25519 or Ed448 key is
        /// for signing - as is every post-quantum signature scheme here. A
        /// request that claims otherwise is a request a strict certificate
        /// authority is entitled to refuse.
        /// </remarks>
        public Boolean CanEncipher
            => Id.StartsWith("rsa-");

        #endregion


        #region (static) Find(Id)

        /// <summary>
        /// The algorithm written that way, or null.
        /// </summary>
        public static KeyAlgorithm? Find(String? Id)
        {

            var id = Id?.Trim().ToLowerInvariant();

            return id is null or ""
                       ? null
                       : All.FirstOrDefault(algorithm => algorithm.Id == id);

        }

        #endregion

        #region Generate()

        /// <summary>
        /// A new key pair of this kind.
        /// </summary>
        public AsymmetricCipherKeyPair Generate()

            => Id switch {

                   "ecdsa-p256"         => PKIFactory.GenerateECCKeyPair    ("secp256r1"),
                   "ecdsa-p384"         => PKIFactory.GenerateECCKeyPair    ("secp384r1"),
                   "ecdsa-p521"         => PKIFactory.GenerateECCKeyPair    ("secp521r1"),

                   "rsa-2048"           => PKIFactory.GenerateRSAKeyPair    (2048),
                   "rsa-3072"           => PKIFactory.GenerateRSAKeyPair    (3072),
                   "rsa-4096"           => PKIFactory.GenerateRSAKeyPair    (4096),

                   "ed25519"            => PKIFactory.GenerateEd25519KeyPair(),
                   "ed448"              => PKIFactory.GenerateEd448KeyPair  (),

                   "ml-dsa-44"          => PKIFactory.GenerateMLDSAKeyPair  ("ml_dsa_44"),
                   "ml-dsa-65"          => PKIFactory.GenerateMLDSAKeyPair  ("ml_dsa_65"),
                   "ml-dsa-87"          => PKIFactory.GenerateMLDSAKeyPair  ("ml_dsa_87"),

                   "slh-dsa-sha2-128s"  => PKIFactory.GenerateSLHDSAKeyPair ("slh_dsa_sha2_128s"),
                   "slh-dsa-sha2-192s"  => PKIFactory.GenerateSLHDSAKeyPair ("slh_dsa_sha2_192s"),

                   _                    => throw new NotSupportedException($"'{Id}' is not a key this factory generates!")

               };

        #endregion

        #region (static) CanBePresented(Algorithm, Certificate)

        /// <summary>
        /// Whether this machine can hold a certificate of this kind up to
        /// somebody during a TLS handshake.
        /// </summary>
        /// <remarks>
        /// <b>Asked by doing it.</b> A TLS server and a TLS client on the
        /// loopback address, once, and whether they got through each other.
        /// Nothing else answers this honestly: it depends on the operating
        /// system's TLS stack, on the runtime, and on the year - Ed25519
        /// certificates are refused by one platform and served by the next, and
        /// a table written into this file would be wrong on somebody's machine
        /// from the day it was written.
        ///
        /// Kept per algorithm, because that is what the answer is about, and
        /// asked once: a handshake against oneself costs a few milliseconds,
        /// and doing it for every connection would spend them on every
        /// connection.
        ///
        /// Anything that goes wrong is a no. The question is not why it failed,
        /// it is whether the other side would get in.
        /// </remarks>
        public static Boolean CanBePresented(String            Algorithm,
                                             X509Certificate2  Certificate)

            => presentable.GetOrAdd(
                   Algorithm,
                   _ => {
                       try
                       {
                           return Handshake(Certificate).GetAwaiter().GetResult();
                       }
                       catch
                       {
                           return false;
                       }
                   }
               );

        /// <summary>
        /// One TLS handshake against oneself.
        /// </summary>
        private static async Task<Boolean> Handshake(X509Certificate2 Certificate)
        {

            // A deadline, because this runs while something is starting: a TLS
            // stack that neither completes nor refuses would otherwise hold up
            // a whole device over a question about an algorithm.
            using var deadline = new CancellationTokenSource(ProbeTimeout);

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);

            listener.Start();

            try
            {

                var port = ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

                var serving = Task.Run(async () => {
                                  using var accepted = await listener.AcceptTcpClientAsync(deadline.Token);
                                  using var tls      = new SslStream(accepted.GetStream(), false);
                                  await tls.AuthenticateAsServerAsync(
                                            new SslServerAuthenticationOptions {
                                                ServerCertificate = Certificate
                                            },
                                            deadline.Token
                                        );
                              }, deadline.Token);

                using var client = new TcpClient();
                await client.ConnectAsync(System.Net.IPAddress.Loopback, port, deadline.Token);

                // Accepts anything: what is being asked is whether the server
                // can present the certificate at all, not whether it is
                // trusted here.
                using var tls = new SslStream(client.GetStream(), false, (sender, certificate, chain, errors) => true);

                await tls.AuthenticateAsClientAsync(
                          new SslClientAuthenticationOptions {
                              TargetHost = "localhost"
                          },
                          deadline.Token
                      );

                await serving;

                return true;

            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// This algorithm, as a web interface reads it.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("id",      Id),
                           new JProperty("name",    Name),
                           new JProperty("remark",  Remark)
                       );

            // Only when somebody has found out. "Not tried yet" is a third
            // answer and the page should be able to tell it from "no".
            if (KnownToBePresentable.HasValue)
                json.Add("presentable", KnownToBePresentable.Value);

            return json;

        }

        #endregion

        #region (override) ToString()

        public override String ToString()
            => Name;

        #endregion

    }

}
