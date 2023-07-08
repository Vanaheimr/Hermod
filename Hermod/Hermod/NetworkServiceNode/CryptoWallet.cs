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

using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Concurrent;

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public class CryptoWallet
    {

        private ConcurrentDictionary<CryptoKeyUsage_Id, List<CryptoKeyInfo>> cryptoKeys = new();

        public UInt32  Priority          { get; }


        public CryptoWallet(IEnumerable<CryptoKeyInfo>? CryptoKeys = null)
        {

            this.cryptoKeys = new ConcurrentDictionary<CryptoKeyUsage_Id, List<CryptoKeyInfo>>();

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



        public Boolean AddCryptoKey(CryptoKeyUsage_Id  CryptoKeyUsageId,
                                    CryptoKeyInfo      CryptoKeyInfo)
        {

            if (cryptoKeys.TryGetValue(CryptoKeyUsageId, out var cryptoKeyInfo)) {
                cryptoKeyInfo.Add(CryptoKeyInfo);
                return true;
            }

            return false;

        }


        public IEnumerable<CryptoKeyInfo> GetKeysFor(CryptoKeyUsage_Id CryptoKeyUsageId)
            => cryptoKeys[CryptoKeyUsageId];


    }

}
