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


#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public class CryptoKeyInfo
    {

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
        public CryptoKeyEncoding        CryptoKeyEncoding    { get; }

        /// <summary>
        /// The priority of this key among all they keys of a key usage.
        /// </summary>
        public UInt32                   Priority             { get; }


        public CryptoKeyInfo(String                       PublicKeyText,
                             String                       PrivateKeyText,
                             IEnumerable<String>          CertificatesText,
                             IEnumerable<CryptoKeyUsage>  KeyUsages,
                             UInt32?                      Priority) //ToDo: Perhaps "Priority per key usage"?
        {

            this.PublicKeyText     = PublicKeyText;
            this.PrivateKeyText    = PrivateKeyText;
            this.CertificatesText  = CertificatesText ?? Array.Empty<String>();
            this.KeyUsages         = KeyUsages.Any()
                                         ? new HashSet<CryptoKeyUsage>(KeyUsages)
                                         : new HashSet<CryptoKeyUsage>();
            this.Priority          = Priority ?? 0;

        }

        public CryptoKeyInfo(String              PublicKeyText,
                             String              PrivateKeyText,
                             CryptoKeyUsage      KeyUsage,
                             CryptoKeyType?      CryptoKeyType       = null,
                             CryptoKeyEncoding?  CryptoKeyEncoding   = null)
        {

            this.PublicKeyText      = PublicKeyText;
            this.PrivateKeyText     = PrivateKeyText;
            this.CertificatesText   = CertificatesText  ?? Array.Empty<String>();
            this.KeyUsages          = new HashSet<CryptoKeyUsage>() { KeyUsage };
            this.CryptoKeyType      = CryptoKeyType     ?? Hermod.CryptoKeyType.SecP521r1;
            this.CryptoKeyEncoding  = CryptoKeyEncoding ?? Hermod.CryptoKeyEncoding.BASE64;
            this.Priority           = 0;

        }

    }

}
