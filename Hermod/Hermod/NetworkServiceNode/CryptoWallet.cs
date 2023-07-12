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

using System.Collections;
using System.Collections.Concurrent;

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

        public UInt32  Priority    { get; }

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


        #region Add(CryptoKeyUsageId, CryptoKeyInfo)

        public Boolean Add(CryptoKeyUsage  CryptoKeyUsageId,
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


    }

}
