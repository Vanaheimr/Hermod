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

    public class CryptoSignature
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of the object.
        /// </summary>
        public const String JSONLDContext = "https://open.charging.cloud/contexts/crypto/signatures";

        #endregion

        #region Properties

        public String                 PublicKey        { get; }

        public String                 Signature        { get; }

        public I18NString?            Description      { get; }

        /// <summary>
        /// The type of the crypto keys.
        /// </summary>
        public CryptoSignatureType    SignatureType    { get; }

        /// <summary>
        /// The encoding of the signature and keys.
        /// </summary>
        public DataEncoding           Encoding         { get; }

        public CryptoSignatureStatus  Status           { get; }

        #endregion

        #region Constructor(s)

        public CryptoSignature(String                  PublicKey,
                               String                  Signature,
                               I18NString?             Description     = null,
                               CryptoSignatureType?    SignatureType   = null,
                               DataEncoding?           Encoding        = null,
                               CryptoSignatureStatus?  Status          = null)
        {

            this.PublicKey      = PublicKey;
            this.Signature      = Signature;
            this.Description    = Description;
            this.SignatureType  = SignatureType ?? CryptoSignatureType.ECDSA;
            this.Encoding       = Encoding      ?? DataEncoding.BASE64;
            this.Status         = Status        ?? CryptoSignatureStatus.Unverified;

        }

        #endregion


        #region ToJSON(this Embedded = false, MaxPublicKeyLength = null, CustomCryptoSignatureSerializer = null)

        /// <summary>
        /// Return a JSON representation of the given crypto signature.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        /// <param name="MaxPublicKeyLength">An optional max public key length, when you want to shorten this string.</param>
        /// <param name="CustomCryptoSignatureSerializer">A delegate to serialize custom crypto signature JSON elements.</param>
        public JObject ToJSON(Boolean                                            Embedded                          = false,
                              UInt16?                                            MaxPublicKeyLength                = null,
                              CustomJObjectSerializerDelegate<CryptoSignature>?  CustomCryptoSignatureSerializer   = null)
        {

            var status  = Status.ToString();

            var json    = JSONObject.Create(

                              !Embedded
                                  ? new JProperty("@context",      SignatureType.ToString())
                                  : null,

                                    new JProperty("publicKey",     MaxPublicKeyLength.HasValue
                                                                       ? PublicKey.SubstringMax(MaxPublicKeyLength.Value)
                                                                       : PublicKey),

                                    new JProperty("signature",     Signature),

                                                                   // When it is the default JSON-LD context, we remove it...
                                    new JProperty("status",        status.StartsWith(CryptoSignatureStatus.JSONLDContext)
                                                                       ? status[(CryptoSignatureStatus.JSONLDContext.Length + 1)..]
                                                                       : status),

                              Description is not null && Description.IsNeitherNullNorEmpty()
                                  ? new JProperty("description",   Description.  ToJSON())
                                  : null,

                              Encoding.IsNotNullOrEmpty
                                  ? new JProperty("encoding",      Encoding.     ToString())
                                  : null

                          );

            return CustomCryptoSignatureSerializer is not null
                       ? CustomCryptoSignatureSerializer(this, json)
                       : json;

        }

        #endregion


        #region Clone()

        /// <summary>
        /// Clone this crypto signature.
        /// </summary>
        public CryptoSignature Clone()

            => new (PublicKey,
                    Signature,
                    Description,
                    SignatureType,
                    Encoding);

        #endregion

    }

}
