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

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// The key of an ACME account: an ECDSA key on P-256, with which every
    /// request to the certificate authority is signed (ES256, RFC 7518).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The key is the account.</b> An ACME server knows an account by its
    /// public key, not by a name or a password: whoever has the key can order
    /// certificates for the account's names, revoke them, and deactivate it.
    /// So it is kept like a private key, and a server that runs for years keeps
    /// the same one - <see cref="LoadOrCreate"/> - instead of opening a new
    /// account at every start, which certificate authorities limit.
    /// </para>
    /// <para>
    /// <b>Why P-256 and nothing else.</b> ES256 is the algorithm every ACME
    /// server accepts; RSA keys would do as well, and only make every request
    /// larger. The account key has nothing to do with the key of the
    /// certificates ordered with it.
    /// </para>
    /// </remarks>
    public sealed class ACMEAccountKey : IDisposable
    {

        #region Data

        private const    String  P256OID  = "1.2.840.10045.3.1.7";

        private readonly ECDsa   key;

        #endregion

        #region Properties

        /// <summary>
        /// The JWS algorithm of every signature made with this key.
        /// </summary>
        public String   Algorithm
            => "ES256";

        /// <summary>
        /// The public key as a JSON Web Key (RFC 7517), as the "jwk" of a
        /// request that has no account URL yet.
        /// </summary>
        public JObject  JWK
            => new (
                   new JProperty("crv",  "P-256"),
                   new JProperty("kty",  "EC"),
                   new JProperty("x",    X),
                   new JProperty("y",    Y)
               );

        /// <summary>
        /// The JWK thumbprint of the public key (RFC 7638): the second half of
        /// every key authorization, and so of every answer to a challenge.
        /// </summary>
        public String   Thumbprint    { get; }

        private String  X             { get; }
        private String  Y             { get; }

        #endregion

        #region Constructor(s)

        private ACMEAccountKey(ECDsa Key)
        {

            this.key  = Key;

            var parameters = Key.ExportParameters(false);

            if (!IsP256(parameters.Curve))
                throw new ArgumentException("An ACME account key is an ECDSA key on P-256.", nameof(Key));

            this.X           = Base64Url.EncodeToString(parameters.Q.X!);
            this.Y           = Base64Url.EncodeToString(parameters.Q.Y!);

            // RFC 7638, section 3: the required members only, in lexicographic
            // order, without any whitespace - the one form two parties agree on
            // without having to talk about it.
            this.Thumbprint  = Base64Url.EncodeToString(
                                   SHA256.HashData(
                                       Encoding.UTF8.GetBytes($"{{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"{X}\",\"y\":\"{Y}\"}}")
                                   )
                               );

        }

        #endregion


        #region (static) Generate()

        /// <summary>
        /// A new account key.
        /// </summary>
        public static ACMEAccountKey Generate()

            => new (ECDsa.Create(ECCurve.NamedCurves.nistP256));

        #endregion

        #region (static) FromPem(Pem)

        /// <summary>
        /// An account key out of PEM: PKCS#8 or SEC 1, as <see cref="ToPem"/>
        /// writes it and as certbot keeps its own.
        /// </summary>
        public static ACMEAccountKey FromPem(String Pem)
        {

            var key = ECDsa.Create();

            try
            {
                key.ImportFromPem(Pem);
                return new ACMEAccountKey(key);
            }
            catch
            {
                key.Dispose();
                throw;
            }

        }

        #endregion

        #region (static) LoadOrCreate(Path)

        /// <summary>
        /// The account key kept in the given file - made and written there
        /// first, if there is none yet.
        /// </summary>
        /// <remarks>
        /// A file that is there and cannot be read is an error, not a reason to
        /// make a new key: overwriting it would lose the account.
        /// </remarks>
        public static ACMEAccountKey LoadOrCreate(String Path)
        {

            if (File.Exists(Path))
                return FromPem(File.ReadAllText(Path));

            var accountKey = Generate();
            ACMEFiles.WritePrivate(Path, accountKey.ToPem());
            return accountKey;

        }

        #endregion

        #region ToPem()

        /// <summary>
        /// The private key as PKCS#8 PEM.
        /// </summary>
        public String ToPem()

            => key.ExportPkcs8PrivateKeyPem();

        #endregion

        #region Sign(Data)

        /// <summary>
        /// The ES256 signature of the given octets: R and S, 32 octets each, as
        /// JWS wants them (RFC 7518, section 3.4) - not DER.
        /// </summary>
        public Byte[] Sign(Byte[] Data)

            => key.SignData(Data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        #endregion

        #region KeyAuthorization(Token)

        /// <summary>
        /// The key authorization of a challenge token (RFC 8555, section 8.1):
        /// what proves that whoever answers a challenge holds this account's key.
        /// </summary>
        public String KeyAuthorization(String Token)

            => $"{Token}.{Thumbprint}";

        #endregion


        #region (private static) IsP256(Curve)

        private static Boolean IsP256(ECCurve Curve)

            => Curve.IsNamed &&
               (Curve.Oid.Value        == P256OID     ||
                Curve.Oid.FriendlyName is "nistP256"  or "ECDSA_P256" or "secp256r1" or "prime256v1");

        #endregion

        #region Dispose()

        /// <summary>
        /// Forget the key.
        /// </summary>
        public void Dispose()
            => key.Dispose();

        #endregion

    }

}
