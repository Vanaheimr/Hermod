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

using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The keys a DKIM signer signs with: RSA (RFC 6376) and Ed25519 (RFC 8463) -
    /// made, read from their PEM, and as the DNS record publishes them.
    /// </summary>
    /// <remarks>
    /// Both are kept as PKCS#8 PEM ("PRIVATE KEY"). The record of an RSA key
    /// carries its SubjectPublicKeyInfo, that of an Ed25519 key the 32 octets of
    /// the public key itself (RFC 8463 §4.2).
    /// </remarks>
    public static class DkimKeyMaterial
    {

        #region NewRsaPem(Bits = 2048) / NewEd25519Pem()

        /// <summary>
        /// A new RSA key, PKCS#8 PEM.
        /// </summary>
        public static String NewRsaPem(Int32 Bits = 2048)
        {
            using var rsa = RSA.Create(Bits);
            return rsa.ExportPkcs8PrivateKeyPem();
        }

        /// <summary>
        /// A new Ed25519 key, PKCS#8 PEM.
        /// </summary>
        public static String NewEd25519Pem()
        {

            var generator = new Ed25519KeyPairGenerator();
            generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));

            var pair = generator.GenerateKeyPair();

            return PemEncoding.WriteString("PRIVATE KEY", PrivateKeyInfoFactory.CreatePrivateKeyInfo(pair.Private).GetEncoded());

        }

        #endregion

        #region Ed25519Of(PrivateKeyPem) / IsEd25519(PrivateKeyPem)

        /// <summary>
        /// The Ed25519 key of a PEM, or null for one that is no Ed25519 key - an RSA key.
        /// </summary>
        public static Ed25519PrivateKeyParameters? Ed25519Of(String PrivateKeyPem)
        {

            try
            {

                var fields = PemEncoding.Find(PrivateKeyPem);

                if (PrivateKeyPem[fields.Label] != "PRIVATE KEY")
                    return null;

                return PrivateKeyFactory.CreateKey(Convert.FromBase64String(PrivateKeyPem[fields.Base64Data])) as Ed25519PrivateKeyParameters;

            }
            catch (Exception e) when (e is ArgumentException or FormatException or IOException or InvalidCastException or SecurityUtilityException)
            {
                return null;
            }

        }

        /// <summary>
        /// Whether the PEM is an Ed25519 key.
        /// </summary>
        public static Boolean IsEd25519(String PrivateKeyPem)
            => Ed25519Of(PrivateKeyPem) is not null;

        #endregion

        #region RecordOf(PrivateKeyPem)

        /// <summary>
        /// What the DNS record of the key says: its k= - "rsa" or "ed25519" - and its p=.
        /// </summary>
        /// <exception cref="ArgumentException">The PEM is no key this reads.</exception>
        public static (String KeyType, String PublicKey) RecordOf(String PrivateKeyPem)
        {

            if (Ed25519Of(PrivateKeyPem) is { } ed25519)
                return ("ed25519", Convert.ToBase64String(ed25519.GeneratePublicKey().GetEncoded()));

            using var rsa = RSA.Create();

            try
            {
                rsa.ImportFromPem(PrivateKeyPem);
            }
            catch (CryptographicException e)
            {
                throw new ArgumentException($"No DKIM key: {e.Message}", nameof(PrivateKeyPem));
            }

            return ("rsa", Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));

        }

        #endregion

    }

}
