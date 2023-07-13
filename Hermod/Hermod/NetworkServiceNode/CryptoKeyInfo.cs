/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

    public class CryptoKeyInfo
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of the object.
        /// </summary>
        public const String JSONLDContext = "https://open.charging.cloud/contexts/crypto/keyInfo";

        #endregion

        #region Properties

        public String                   PublicKeyText        { get; }

        public String                   PrivateKeyText       { get; }

        public IEnumerable<String>      CertificatesText     { get; }

        /// <summary>
        /// Crypto key usages.
        /// Best for security is to use an individual key per key usage!
        /// </summary>
        public HashSet<CryptoKeyUsage>  KeyUsages            { get; }

        /// <summary>
        /// The type of the crypto keys.
        /// </summary>
        public CryptoKeyType            CryptoKeyType        { get; }

        /// <summary>
        /// The encoding of the crypto keys.
        /// </summary>
        public DataEncoding        CryptoKeyEncoding    { get; }

        /// <summary>
        /// The priority of this key among all they keys of a key usage.
        /// </summary>
        public UInt32                   Priority             { get; }

        #endregion

        #region Constructor(s)

        public CryptoKeyInfo(String                       PublicKeyText,
                             String                       PrivateKeyText,
                             IEnumerable<String>          CertificatesText,
                             IEnumerable<CryptoKeyUsage>  KeyUsages,
                             CryptoKeyType?               CryptoKeyType,
                             DataEncoding?           CryptoKeyEncoding,
                             UInt32?                      Priority) //ToDo: Perhaps "Priority per key usage"?
        {

            this.PublicKeyText      = PublicKeyText;
            this.PrivateKeyText     = PrivateKeyText;
            this.CertificatesText   = CertificatesText ?? Array.Empty<String>();
            this.KeyUsages          = KeyUsages.Any()
                                          ? new HashSet<CryptoKeyUsage>(KeyUsages)
                                          : new HashSet<CryptoKeyUsage>();
            this.CryptoKeyType      = CryptoKeyType     ?? Hermod.CryptoKeyType.SecP521r1;
            this.CryptoKeyEncoding  = CryptoKeyEncoding ?? Hermod.DataEncoding.BASE64;
            this.Priority           = Priority ?? 0;

        }

        public CryptoKeyInfo(String              PublicKeyText,
                             String              PrivateKeyText,
                             CryptoKeyUsage      KeyUsage,
                             CryptoKeyType?      CryptoKeyType       = null,
                             DataEncoding?  CryptoKeyEncoding   = null)
        {

            this.PublicKeyText      = PublicKeyText;
            this.PrivateKeyText     = PrivateKeyText;
            this.CertificatesText   = CertificatesText  ?? Array.Empty<String>();
            this.KeyUsages          = new HashSet<CryptoKeyUsage>() { KeyUsage };
            this.CryptoKeyType      = CryptoKeyType     ?? Hermod.CryptoKeyType.SecP521r1;
            this.CryptoKeyEncoding  = CryptoKeyEncoding ?? Hermod.DataEncoding.BASE64;
            this.Priority           = 0;

        }

        #endregion


        #region ToJSON(this Embedded = false, CustomCryptoKeyInfoSerializer = null)

        /// <summary>
        /// Return a JSON representation of the given crypto key information.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        /// <param name="CustomCryptoKeyInfoSerializer">A delegate to serialize custom crypto key information JSON elements.</param>
        public JObject ToJSON(Boolean                                          Embedded                        = false,
                              CustomJObjectSerializerDelegate<CryptoKeyInfo>?  CustomCryptoKeyInfoSerializer   = null)
        {

            var json = JSONObject.Create(

                           !Embedded
                               ? new JProperty("@context",       CryptoKeyType.ToString())
                               : null,

                           PublicKeyText    is not null && PublicKeyText.   IsNotNullOrEmpty()
                               ? new JProperty("publicKey",      PublicKeyText)
                               : null,

                           PrivateKeyText   is not null && PrivateKeyText.  IsNotNullOrEmpty()
                               ? new JProperty("privateKey",     PrivateKeyText)
                               : null,

                           CertificatesText.Any()
                               ? new JProperty("certificates",   new JArray(CertificatesText))
                               : null,

                           KeyUsages.Any()
                               ? new JProperty("keyUsages",      new JArray(KeyUsages.Select(keyUsage => keyUsage.ToString())))
                               : null,

                           CryptoKeyEncoding.IsNotNullOrEmpty
                               ? new JProperty("keyEncoding",    CryptoKeyEncoding.ToString())
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

            => new (PublicKeyText,
                    PrivateKeyText,
                    CertificatesText,
                    KeyUsages,
                    CryptoKeyType,
                    CryptoKeyEncoding,
                    Priority);

        #endregion

    }

}
