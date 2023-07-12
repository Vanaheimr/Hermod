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
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A network service node.
    /// </summary>
    public class NetworkServiceNode : INetworkServiceNode
    {

        #region Data

        private readonly CryptoWallet cryptoWallet = new();

        #endregion

        #region Properties

        /// <summary>
        /// The unique identification of this network service node.
        /// </summary>
        public NetworkServiceNode_Id       Id                { get; }

        /// <summary>
        /// The multi-language name of this network service node.
        /// </summary>
        public I18NString                  Name              { get; }

        /// <summary>
        /// The multi-language description of this network service node.
        /// </summary>
        public I18NString                  Description       { get; }



        public IEnumerable<CryptoKeyInfo>  Identities
            => cryptoWallet.GetKeysForUsage(CryptoKeyUsage.Identity);

        public IEnumerable<CryptoKeyInfo>  IdentityGroups
            => cryptoWallet.GetKeysForUsage(CryptoKeyUsage.IdentityGroup);



        /// <summary>
        /// The optional default HTTP API.
        /// </summary>
        public HTTPAPI?                    DefaultHTTPAPI    { get; }


        /// <summary>
        /// The DNS client used by the network service node.
        /// </summary>
        public DNSClient                   DNSClient         { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new network service node.
        /// </summary>
        /// <param name="Id">An unique identification of this network service node.</param>
        /// <param name="Name">A multi-language name of this network service node.</param>
        /// <param name="Description">A multi-language description of this network service node.</param>
        /// 
        /// <param name="DefaultHTTPAPI">An optional default HTTP API.</param>
        /// 
        /// <param name="DNSClient">The DNS client used by the network service node.</param>
        public NetworkServiceNode(NetworkServiceNode_Id?       Id               = null,
                                  I18NString?                  Name             = null,
                                  I18NString?                  Description      = null,

                                  IEnumerable<CryptoKeyInfo>?  Identities       = null,
                                  IEnumerable<CryptoKeyInfo>?  IdentityGroups   = null,

                                  HTTPAPI?                     DefaultHTTPAPI   = null,

                                  DNSClient?                   DNSClient        = null)
        {

            #region Initial checks

            if (Id.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Id), "The given unique network service node identification must not be null or empty!");

            #endregion

            this.Id               = Id          ?? NetworkServiceNode_Id.NewRandom();
            this.Name             = Name        ?? I18NString.Empty;
            this.Description      = Description ?? I18NString.Empty;

            if (Identities is not null)
                foreach (var identity in Identities.Where(cryptoKey => cryptoKey.KeyUsages.Contains(CryptoKeyUsage.Identity)))
                    AddCryptoKey(CryptoKeyUsage.Identity,
                                 identity);

            if (IdentityGroups is not null)
                foreach (var identityGroup in IdentityGroups.Where(cryptoKey => cryptoKey.KeyUsages.Contains(CryptoKeyUsage.IdentityGroup)))
                    AddCryptoKey(CryptoKeyUsage.IdentityGroup,
                                 identityGroup);

            this.DefaultHTTPAPI   = DefaultHTTPAPI;

            this.DNSClient        = DNSClient   ?? new DNSClient();

            unchecked
            {

                hashCode = this.Id.         GetHashCode() * 5 ^
                           this.Name.       GetHashCode() * 3 ^
                           this.Description.GetHashCode();

            }

            if (this.DefaultHTTPAPI is not null)
                AddHTTPAPI("default",
                           this.DefaultHTTPAPI);

        }

        #endregion


        #region Crypto Wallet

        public Boolean AddCryptoKey(CryptoKeyUsage  CryptoKeyUsageId,
                                    CryptoKeyInfo   CryptoKeyInfo)

            => cryptoWallet.Add(CryptoKeyUsageId,
                                         CryptoKeyInfo);

        #endregion

        #region HTTP APIs

        #region Data

        private readonly ConcurrentDictionary<String, HTTPAPI> httpAPIs = new();

        /// <summary>
        /// An enumeration of all HTTP APIs.
        /// </summary>
        public IEnumerable<HTTPAPI> HTTPAPIs
            => httpAPIs.Values;

        #endregion

        public Boolean AddHTTPAPI(String   HTTPAPIId,
                                  HTTPAPI  HTTPAPI)
        {

            return httpAPIs.TryAdd(HTTPAPIId, HTTPAPI);

        }

        public HTTPAPI? GetHTTPAPI(String HTTPAPIId)
        {

            return httpAPIs.TryGet(HTTPAPIId);

        }

        #endregion


        //ToDo: Add HTTP WebSocket Servers
        //ToDo: Add Trackers
        //ToDo: Add Overlay Networks

        //ToDo: Add ADataStores!?!


        #region Clone

        /// <summary>
        /// Clone this network service node.
        /// </summary>
        public NetworkServiceNode Clone

            => new (Id.Clone);

        #endregion


        #region Operator overloading

        #region Operator == (NetworkServiceNode1, NetworkServiceNode2)

        /// <summary>
        /// Compares two network service nodes for equality.
        /// </summary>
        /// <param name="NetworkServiceNode1">A network service node.</param>
        /// <param name="NetworkServiceNode2">Another network service node.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (NetworkServiceNode NetworkServiceNode1,
                                           NetworkServiceNode NetworkServiceNode2)
        {

            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(NetworkServiceNode1, NetworkServiceNode2))
                return true;

            // If one is null, but not both, return false.
            if (NetworkServiceNode1 is null || NetworkServiceNode2 is null)
                return false;

            return NetworkServiceNode1.Equals(NetworkServiceNode2);

        }

        #endregion

        #region Operator != (NetworkServiceNode1, NetworkServiceNode2)

        /// <summary>
        /// Compares two network service nodes for inequality.
        /// </summary>
        /// <param name="NetworkServiceNode1">A network service node.</param>
        /// <param name="NetworkServiceNode2">Another network service node.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (NetworkServiceNode NetworkServiceNode1,
                                           NetworkServiceNode NetworkServiceNode2)

            => !(NetworkServiceNode1 == NetworkServiceNode2);

        #endregion

        #region Operator <  (NetworkServiceNode1, NetworkServiceNode2)

        /// <summary>
        /// Compares two network service nodes.
        /// </summary>
        /// <param name="NetworkServiceNode1">A network service node.</param>
        /// <param name="NetworkServiceNode2">Another network service node.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (NetworkServiceNode NetworkServiceNode1,
                                          NetworkServiceNode NetworkServiceNode2)
        {

            if (NetworkServiceNode1 is null)
                throw new ArgumentNullException(nameof(NetworkServiceNode1), "The given network service node 1 must not be null!");

            return NetworkServiceNode1.CompareTo(NetworkServiceNode2) < 0;

        }

        #endregion

        #region Operator <= (NetworkServiceNode1, NetworkServiceNode2)

        /// <summary>
        /// Compares two network service nodes.
        /// </summary>
        /// <param name="NetworkServiceNode1">A network service node.</param>
        /// <param name="NetworkServiceNode2">Another network service node.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (NetworkServiceNode NetworkServiceNode1,
                                           NetworkServiceNode NetworkServiceNode2)

            => !(NetworkServiceNode1 > NetworkServiceNode2);

        #endregion

        #region Operator >  (NetworkServiceNode1, NetworkServiceNode2)

        /// <summary>
        /// Compares two network service nodes.
        /// </summary>
        /// <param name="NetworkServiceNode1">A network service node.</param>
        /// <param name="NetworkServiceNode2">Another network service node.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (NetworkServiceNode NetworkServiceNode1,
                                          NetworkServiceNode NetworkServiceNode2)
        {

            if (NetworkServiceNode1 is null)
                throw new ArgumentNullException(nameof(NetworkServiceNode1), "The given network service node 1 must not be null!");

            return NetworkServiceNode1.CompareTo(NetworkServiceNode2) > 0;

        }

        #endregion

        #region Operator >= (NetworkServiceNode1, NetworkServiceNode2)

        /// <summary>
        /// Compares two network service nodes.
        /// </summary>
        /// <param name="NetworkServiceNode1">A network service node.</param>
        /// <param name="NetworkServiceNode2">Another network service node.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (NetworkServiceNode NetworkServiceNode1,
                                           NetworkServiceNode NetworkServiceNode2)

            => !(NetworkServiceNode1 < NetworkServiceNode2);

        #endregion

        #endregion

        #region IComparable<NetworkServiceNode> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two network service nodes.
        /// </summary>
        /// <param name="Object">A network service node to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is NetworkServiceNode networkServiceNode
                   ? CompareTo(networkServiceNode)
                   : throw new ArgumentException("The given object is not a network service node!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(NetworkServiceNode)

        /// <summary>
        /// Compares two network service nodes.
        /// </summary>
        /// <param name="NetworkServiceNode">A network service node to compare with.</param>
        public Int32 CompareTo(NetworkServiceNode NetworkServiceNode)
        {

            if (NetworkServiceNode is null)
                throw new ArgumentNullException(nameof(NetworkServiceNode), "The given network service node must not be null!");

            var c = Id.         CompareTo(NetworkServiceNode.Id);

            //if (c == 0)
            //    c = Name.       CompareTo(NetworkServiceNode.Name);

            //if (c == 0)
            //    c = Description.CompareTo(NetworkServiceNode.Description);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<NetworkServiceNode> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two network service nodes for equality.
        /// </summary>
        /// <param name="Object">A network service node to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is NetworkServiceNode networkServiceNode &&
                   Equals(networkServiceNode);

        #endregion

        #region Equals(NetworkServiceNode)

        /// <summary>
        /// Compares two network service nodes for equality.
        /// </summary>
        /// <param name="NetworkServiceNode">A network service node to compare with.</param>
        public Boolean Equals(NetworkServiceNode NetworkServiceNode)

            => NetworkServiceNode is not null &&

               Id.         Equals(NetworkServiceNode.Id)   &&
               Name.       Equals(NetworkServiceNode.Name) &&
               Description.Equals(NetworkServiceNode.Description);

        #endregion

        #endregion

        #region GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Id.ToString();

        #endregion

    }

}
