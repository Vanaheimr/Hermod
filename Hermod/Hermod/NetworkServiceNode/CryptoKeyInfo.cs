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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    #region (class) SecP256r1Keys

    public class SecP256r1Keys : CryptoKeyInfo
    {

        public SecP256r1Keys(String                        PublicKey,
                             String                        PrivateKey,
                             IEnumerable<CryptoSignature>  Signatures,
                             IEnumerable<CryptoKeyUsage>   KeyUsages,
                             DateTimeOffset?               NotBefore,
                             DateTimeOffset?               NotAfter,
                             DataEncoding?                 KeyEncoding,
                             UInt32?                       Priority) //ToDo: Perhaps "Priority per key usage"?

            : base(PublicKey,
                   PrivateKey,
                   Signatures,
                   KeyUsages,
                   NotBefore,
                   NotAfter,
                   CryptoKeyType.SecP256r1,
                   KeyEncoding,
                   Priority)

        { }

        public SecP256r1Keys(String           PublicKey,
                             String           PrivateKey,
                             CryptoKeyUsage   KeyUsage,
                             DateTimeOffset?  NotBefore     = null,
                             DateTimeOffset?  NotAfter      = null,
                             DataEncoding?    KeyEncoding   = null)

            : base(PublicKey,
                   PrivateKey,
                   KeyUsage,
                   NotBefore,
                   NotAfter,
                   CryptoKeyType.SecP256r1,
                   KeyEncoding)

        { }

    }

    #endregion

    #region (class) SecP521r1Keys

    public class SecP521r1Keys : CryptoKeyInfo
    {

        public SecP521r1Keys(String                        PublicKey,
                             String                        PrivateKey,
                             IEnumerable<CryptoSignature>  Signatures,
                             IEnumerable<CryptoKeyUsage>   KeyUsages,
                             DateTimeOffset?               NotBefore,
                             DateTimeOffset?               NotAfter,
                             DataEncoding?                 KeyEncoding,
                             UInt32?                       Priority) //ToDo: Perhaps "Priority per key usage"?

            : base(PublicKey,
                   PrivateKey,
                   Signatures,
                   KeyUsages,
                   NotBefore,
                   NotAfter,
                   CryptoKeyType.SecP521r1,
                   KeyEncoding,
                   Priority)

        { }

        public SecP521r1Keys(String           PublicKey,
                             String           PrivateKey,
                             CryptoKeyUsage   KeyUsage,
                             DateTimeOffset?  NotBefore     = null,
                             DateTimeOffset?  NotAfter      = null,
                             DataEncoding?    KeyEncoding   = null)

            : base(PublicKey,
                   PrivateKey,
                   KeyUsage,
                   NotBefore,
                   NotAfter,
                   CryptoKeyType.SecP521r1,
                   KeyEncoding)

        { }

    }

    #endregion


    public class CryptoKeyInfo
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of the object.
        /// </summary>
        public const String JSONLDContext = "https://open.charging.cloud/contexts/crypto/keyInfo";

        #endregion

        #region Properties

        public String                        PublicKey        { get; }

        public String                        PrivateKey       { get; }

        public IEnumerable<CryptoSignature>  Signatures       { get; }

        /// <summary>
        /// Crypto key usages.
        /// Best for security is to use an individual key per key usage!
        /// </summary>
        public HashSet<CryptoKeyUsage>       KeyUsages        { get; }

        public DateTimeOffset?               NotBefore        { get; }
        public DateTimeOffset?               NotAfter         { get; }

        /// <summary>
        /// The type of the crypto keys.
        /// </summary>
        public CryptoKeyType                 KeyType          { get; }

        /// <summary>
        /// The encoding of the crypto keys.
        /// </summary>
        public DataEncoding                  KeyEncoding      { get; }

        /// <summary>
        /// The priority of this key among all they keys of a key usage.
        /// </summary>
        public UInt32                        Priority         { get; }

        #endregion

        #region Constructor(s)

        public CryptoKeyInfo(String                        PublicKey,
                             String                        PrivateKey,
                             IEnumerable<CryptoSignature>  Signatures,
                             IEnumerable<CryptoKeyUsage>   KeyUsages,
                             DateTimeOffset?               NotBefore,
                             DateTimeOffset?               NotAfter,
                             CryptoKeyType?                KeyType,
                             DataEncoding?                 KeyEncoding,
                             UInt32?                       Priority) //ToDo: Perhaps "Priority per key usage"?
        {

            this.PublicKey    = PublicKey;
            this.PrivateKey   = PrivateKey;
            this.Signatures   = Signatures ?? Array.Empty<CryptoSignature>();
            this.KeyUsages    = KeyUsages.Any()
                                    ? new HashSet<CryptoKeyUsage>(KeyUsages)
                                    : new HashSet<CryptoKeyUsage>();
            this.NotBefore    = NotBefore;
            this.NotAfter     = NotAfter;
            this.KeyType      = KeyType     ?? CryptoKeyType.SecP521r1;
            this.KeyEncoding  = KeyEncoding ?? DataEncoding.BASE64;
            this.Priority     = Priority ?? 0;

        }

        public CryptoKeyInfo(String           PublicKey,
                             String           PrivateKey,
                             CryptoKeyUsage   KeyUsage,
                             DateTimeOffset?  NotBefore     = null,
                             DateTimeOffset?  NotAfter      = null,
                             CryptoKeyType?   KeyType       = null,
                             DataEncoding?    KeyEncoding   = null)
        {

            this.PublicKey    = PublicKey;
            this.PrivateKey   = PrivateKey;
            this.Signatures   = Signatures  ?? Array.Empty<CryptoSignature>();
            this.KeyUsages    = new HashSet<CryptoKeyUsage>() { KeyUsage };
            this.NotBefore    = NotBefore;
            this.NotAfter     = NotAfter;
            this.KeyType      = KeyType     ?? CryptoKeyType.SecP521r1;
            this.KeyEncoding  = KeyEncoding ?? DataEncoding.BASE64;
            this.Priority     = 0;

        }

        #endregion


        #region ToJSON(this Embedded = false, CustomCryptoKeyInfoSerializer = null)

        /// <summary>
        /// Return a JSON representation of the given crypto key information.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        /// <param name="CustomCryptoKeyInfoSerializer">A delegate to serialize custom crypto key information JSON elements.</param>
        public JObject ToJSON(Boolean                                            Embedded                          = false,
                              UInt16?                                            MaxSignaturePublicKeyLength       = null,
                              CustomJObjectSerializerDelegate<CryptoSignature>?  CustomCryptoSignatureSerializer   = null,
                              CustomJObjectSerializerDelegate<CryptoKeyInfo>?    CustomCryptoKeyInfoSerializer     = null)
        {

            var keyEncoding  = KeyEncoding.ToString();

            var json         = JSONObject.Create(

                                   !Embedded
                                       ? new JProperty("@context",       KeyType.ToString())
                                       : null,

                                   PublicKey  is not null && PublicKey. IsNotNullOrEmpty()
                                       ? new JProperty("publicKey",      PublicKey)
                                       : null,

                                   PrivateKey is not null && PrivateKey.IsNotNullOrEmpty()
                                       ? new JProperty("privateKey",     PrivateKey)
                                       : null,

                                   Signatures.Any()
                                       ? new JProperty("certificates",   new JArray(Signatures.Select(signature => signature.ToJSON(Embedded:                         true,
                                                                                                                                    MaxPublicKeyLength:               MaxSignaturePublicKeyLength,
                                                                                                                                    CustomCryptoSignatureSerializer:  CustomCryptoSignatureSerializer))))
                                       : null,

                                   KeyUsages.Any()
                                       ? new JProperty("keyUsages",      new JArray(KeyUsages. Select(keyUsage  => keyUsage. ToString())))
                                       : null,

                                   KeyEncoding.IsNotNullOrEmpty
                                       ? new JProperty("keyEncoding",    keyEncoding.StartsWith(DataEncoding.JSONLDContext)
                                                                             ? keyEncoding[(DataEncoding.JSONLDContext.Length + 1)..]
                                                                             : keyEncoding)
                                       : null

                               );

            return CustomCryptoKeyInfoSerializer is not null
                       ? CustomCryptoKeyInfoSerializer(this, json)
                       : json;

        }

        #endregion


        #region Clone()

        /// <summary>
        /// Clone this crypto key information.
        /// </summary>
        public CryptoKeyInfo Clone()

            => new (PublicKey,
                    PrivateKey,
                    Signatures,
                    KeyUsages,
                    NotBefore,
                    NotAfter,
                    KeyType,
                    KeyEncoding,
                    Priority);

        #endregion

    }

}
