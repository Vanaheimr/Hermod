/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Collections;
using System.Collections.Concurrent;
using System.Security.Cryptography;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Math;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public class CryptoWallet : IEnumerable<CryptoKeyInfo>
    {

        #region Data

        private readonly ConcurrentDictionary<CryptoKeyUsage, List<CryptoKeyInfo>> cryptoKeys = new();

        #endregion

        #region Properties

        public UInt32          Priority     { get; }

        public X9ECParameters  SecP256r1    { get; } = SecNamedCurves.GetByName("secp256r1");
        public X9ECParameters  SecP384r1    { get; } = SecNamedCurves.GetByName("secp384r1");
        public X9ECParameters  SecP521r1    { get; } = SecNamedCurves.GetByName("secp521r1");

        #endregion

        #region Constructor(s)

        public CryptoWallet(IEnumerable<CryptoKeyInfo>? CryptoKeys = null)
        {

            this.cryptoKeys = new ConcurrentDictionary<CryptoKeyUsage, List<CryptoKeyInfo>>();

            if (CryptoKeys is not null)
            {
                foreach (var cryptoKey in CryptoKeys)
                {
                    foreach (var keyUsage in cryptoKey.KeyUsages)
                    {
                        cryptoKeys.AddOrUpdate(keyUsage,
                                               key         => new List<CryptoKeyInfo>(new[] { cryptoKey }),
                                              (key, list)  => list.AddAndReturnList(cryptoKey));
                    }
                }
            }

        }

        #endregion


        #region Clone()

        /// <summary>
        /// Clone this crypto wallet.
        /// </summary>
        public CryptoWallet Clone()

            => new (cryptoKeys.Values.SelectMany(cryptoKeyInfoList => cryptoKeyInfoList).
                                      Select    (cryptoKeyInfo     => cryptoKeyInfo.
                                      Clone     ()));

        #endregion


        #region Add   (CryptoKeyInfo)

        public Boolean Add(CryptoKeyInfo CryptoKeyInfo)
        {

            foreach (var keyUsage in CryptoKeyInfo.KeyUsages)
            {

                if (cryptoKeys.TryGetValue(keyUsage, out var cryptoKeyInfos))
                    cryptoKeyInfos.Add(CryptoKeyInfo);

                else
                    cryptoKeys.TryAdd(keyUsage,
                                      new List<CryptoKeyInfo> {
                                          CryptoKeyInfo
                                      });

            }

            return true;

        }

        #endregion

        #region Add   (CryptoKeyInfos)

        public Boolean Add(IEnumerable<CryptoKeyInfo> CryptoKeyInfos)
        {

            foreach (var cryptoKeyInfo in CryptoKeyInfos)
                Add(cryptoKeyInfo);

            return true;

        }

        #endregion

        #region Remove(CryptoKeyInfo)

        public Boolean Remove(CryptoKeyInfo CryptoKeyInfo)
        {

            foreach (var keyUsage in CryptoKeyInfo.KeyUsages)
            {
                if (cryptoKeys.TryGetValue(keyUsage, out var cryptoKeyInfos))
                {
                    foreach (var cryptoKey in cryptoKeyInfos.ToArray())
                    {
                        if (cryptoKey.PublicKey == CryptoKeyInfo.PublicKey)
                            cryptoKeyInfos.Remove(cryptoKey);
                    }
                }
            }

            return true;

        }

        #endregion


        #region GetKeys         (KeyFilter)

        public IEnumerable<CryptoKeyInfo> GetKeys(Func<CryptoKeyInfo, Boolean> KeyFilter)
        {

            var cryptoKeyUsageIdSet = new HashSet<CryptoKeyInfo>();

            foreach (var kvp in cryptoKeys)
            {
                foreach (var cryptoKeyInfo in kvp.Value)
                {
                    if (KeyFilter(cryptoKeyInfo))
                        cryptoKeyUsageIdSet.Add(cryptoKeyInfo);
                }
            }

            return cryptoKeyUsageIdSet;

        }

        #endregion


        #region GetKeysForUsage (CryptoKeyUsageId)

        public IEnumerable<CryptoKeyInfo> GetKeysForUsage(CryptoKeyUsage CryptoKeyUsageId)
            => cryptoKeys[CryptoKeyUsageId];

        #endregion

        #region GetKeysForUsages(CryptoKeyUsageIds)

        public IEnumerable<CryptoKeyInfo> GetKeysForUsages(params CryptoKeyUsage[] CryptoKeyUsageIds)
            => GetKeysForUsages(CryptoKeyUsageIds);

        #endregion

        #region GetKeysForUsages(CryptoKeyUsageIds)

        public IEnumerable<CryptoKeyInfo> GetKeysForUsages(IEnumerable<CryptoKeyUsage> CryptoKeyUsageIds)
        {

            var cryptoKeyUsageIdSet = new HashSet<CryptoKeyInfo>();

            foreach (var cryptoKeyUsageId in CryptoKeyUsageIds)
            {
                foreach (var keyUsage in cryptoKeys[cryptoKeyUsageId])
                {
                    cryptoKeyUsageIdSet.Add(keyUsage);
                }
            }

            return cryptoKeyUsageIdSet;

        }

        #endregion


        #region GetEnumerator()

        public IEnumerator<CryptoKeyInfo> GetEnumerator()

            => cryptoKeys.Values.SelectMany(cryptoKeyInfo => cryptoKeyInfo).ToList().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()

            => cryptoKeys.Values.SelectMany(cryptoKeyInfo => cryptoKeyInfo).ToList().GetEnumerator();

        #endregion


        #region SignKey(CryptoKeyInfo, params KeyPairs)

        public CryptoKeyInfo SignKey(CryptoKeyInfo           CryptoKeyInfo,
                                     params CryptoKeyInfo[]  KeyPairs)
        {

            #region Data

            var cc = new Newtonsoft.Json.Converters.IsoDateTimeConverter {
                         DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffZ"
                     };

            var json        = CryptoKeyInfo.ToJSON();
            var text        = json.ToString(Newtonsoft.Json.Formatting.None, cc);
            var json2       = JObject.Parse(text);
            json2.Remove("signatures");
            var plainText   = json2.ToString(Newtonsoft.Json.Formatting.None, cc);
            var blockSize   = 0;

            Byte[]? sha256Hash  = null;
            Byte[]? sha512Hash  = null;
            Byte[]? shaHash     = null;

            if (json["signatures"] is not JArray signaturesJSON)
            {
                signaturesJSON = new JArray();
                json.Add("signatures", signaturesJSON);
            }

            var signatures = new List<CryptoSignature>();

            #endregion

            foreach (var keyPair in KeyPairs)
            {

                if (keyPair is null)
                    continue;

                ECPrivateKeyParameters? privateKey  = null;
                ECPublicKeyParameters?  publicKey   = null;

                if      (keyPair.KeyType == CryptoKeyType.SecP256r1)
                {

                    privateKey   = new ECPrivateKeyParameters(
                                       new BigInteger(
                                           keyPair.PrivateKey.FromBASE64()
                                       ),
                                       new ECDomainParameters(
                                           SecP256r1.Curve,
                                           SecP256r1.G,
                                           SecP256r1.N,
                                           SecP256r1.H,
                                           SecP256r1.GetSeed()
                                       )
                                   );

                    publicKey    = new ECPublicKeyParameters(
                                       "ECDSA",
                                       SecP256r1.Curve.DecodePoint(
                                           keyPair.PublicKey.FromBASE64()
                                       ),
                                       new ECDomainParameters(
                                           SecP256r1.Curve,
                                           SecP256r1.G,
                                           SecP256r1.N,
                                           SecP256r1.H,
                                           SecP256r1.GetSeed()
                                       )
                                   );

                    sha256Hash ??= SHA256.HashData(plainText.ToUTF8Bytes());
                    shaHash      = sha256Hash;
                    blockSize    = sha256Hash.Length;

                }

                else if (keyPair.KeyType == CryptoKeyType.SecP384r1)
                {

                    privateKey   = new ECPrivateKeyParameters(
                                       new BigInteger(
                                           keyPair.PrivateKey.FromBASE64()
                                       ),
                                       new ECDomainParameters(
                                           SecP384r1.Curve,
                                           SecP384r1.G,
                                           SecP384r1.N,
                                           SecP384r1.H,
                                           SecP384r1.GetSeed()
                                       )
                                   );

                    publicKey    = new ECPublicKeyParameters(
                                       "ECDSA",
                                       SecP384r1.Curve.DecodePoint(
                                           keyPair.PublicKey.FromBASE64()
                                       ),
                                       new ECDomainParameters(
                                           SecP384r1.Curve,
                                           SecP384r1.G,
                                           SecP384r1.N,
                                           SecP384r1.H,
                                           SecP384r1.GetSeed()
                                       )
                                   );

                    sha512Hash ??= SHA512.HashData(plainText.ToUTF8Bytes());
                    shaHash      = sha512Hash;
                    blockSize    = sha512Hash.Length;

                }

                else if (keyPair.KeyType == CryptoKeyType.SecP521r1)
                {

                    privateKey   = new ECPrivateKeyParameters(
                                       new BigInteger(
                                           keyPair.PrivateKey.FromBASE64()
                                       ),
                                       new ECDomainParameters(
                                           SecP521r1.Curve,
                                           SecP521r1.G,
                                           SecP521r1.N,
                                           SecP521r1.H,
                                           SecP521r1.GetSeed()
                                       )
                                   );

                    publicKey    = new ECPublicKeyParameters(
                                       "ECDSA",
                                       SecP521r1.Curve.DecodePoint(
                                           keyPair.PublicKey.FromBASE64()
                                       ),
                                       new ECDomainParameters(
                                           SecP521r1.Curve,
                                           SecP521r1.G,
                                           SecP521r1.N,
                                           SecP521r1.H,
                                           SecP521r1.GetSeed()
                                       )
                                   );

                    sha512Hash ??= SHA512.HashData(plainText.ToUTF8Bytes());
                    shaHash      = sha512Hash;
                    blockSize    = sha512Hash.Length;

                }

                if (privateKey is null || publicKey is null)
                    continue;

                var signatureJSON   = new JObject();
                signaturesJSON.Add(signatureJSON);

                //var publicKeyBytes  = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey).PublicKeyData.GetBytes();
                signatureJSON.Add(new JProperty("publicKey", keyPair.PublicKey)); // Convert.ToBase64String(publicKeyBytes)));

                var signer = SignerUtilities.GetSigner("NONEwithECDSA");
                signer.Init(true, privateKey);
                signer.BlockUpdate(shaHash, 0, blockSize);
                var signature        = signer.GenerateSignature();
                var signatureBASE64  = Convert.ToBase64String(signature);
                signatureJSON.Add(new JProperty("signature", signatureBASE64));

                signatures.Add(new CryptoSignature(
                                   keyPair.PublicKey,
                                   signatureBASE64,
                                   Status: CryptoSignatureStatus.Verified
                               ));

            }

            var newKey = new CryptoKeyInfo(
                             CryptoKeyInfo.PublicKey,
                             CryptoKeyInfo.PrivateKey,
                             signatures,
                             CryptoKeyInfo.KeyUsages,
                             CryptoKeyInfo.NotBefore,
                             CryptoKeyInfo.NotAfter,
                             CryptoKeyInfo.KeyType,
                             CryptoKeyInfo.KeyEncoding,
                             CryptoKeyInfo.Priority
                         );

            Remove(newKey);
            Add   (newKey);

            return newKey;

        }

        #endregion


    }

}
