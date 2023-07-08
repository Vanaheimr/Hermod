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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public class CryptoWallet
    {

        #region Data

        private readonly ConcurrentDictionary<CryptoKeyUsage_Id, List<CryptoKeyInfo>> cryptoKeys = new();

        #endregion

        #region Properties

        public UInt32  Priority    { get; }

        #endregion

        #region Constructor(s)

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

        #endregion


        #region Add(CryptoKeyUsageId, CryptoKeyInfo)

        public Boolean Add(CryptoKeyUsage_Id  CryptoKeyUsageId,
                           CryptoKeyInfo      CryptoKeyInfo)
        {

            if (cryptoKeys.TryGetValue(CryptoKeyUsageId, out var cryptoKeyInfo)) {
                cryptoKeyInfo.Add(CryptoKeyInfo);
                return true;
            }

            return false;

        }

        #endregion


        #region GetKeys           (KeyFilter)

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


        #region GetKeysForUsageId (CryptoKeyUsageId)

        public IEnumerable<CryptoKeyInfo> GetKeysForUsageId(CryptoKeyUsage_Id CryptoKeyUsageId)
            => cryptoKeys[CryptoKeyUsageId];

        #endregion

        #region GetKeysForUsageIds(CryptoKeyUsageIds)

        public IEnumerable<CryptoKeyInfo> GetKeysForUsageIds(params CryptoKeyUsage_Id[] CryptoKeyUsageIds)
            => GetKeysForUsageIds(CryptoKeyUsageIds);

        #endregion

        #region GetKeysForUsageIds(CryptoKeyUsageIds)

        public IEnumerable<CryptoKeyInfo> GetKeysForUsageIds(IEnumerable<CryptoKeyUsage_Id> CryptoKeyUsageIds)
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



    }

}
