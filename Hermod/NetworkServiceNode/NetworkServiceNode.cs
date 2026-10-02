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

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A network service node.
    /// </summary>
    public class NetworkServiceNode : INetworkServiceNode,
                                      IDisposable,
                                      IAsyncDisposable
    {

        #region Data

        private readonly CryptoWallet cryptoWallet = new();

        /// <summary>
        /// Whether this node made its HTTP server itself, and so is the one to
        /// dispose of it. One that was handed in belongs to whoever handed it
        /// in, and may be serving others as well.
        /// </summary>
        private readonly Boolean      ownsHTTPServer;

        /// <summary>
        /// Whether this node made the DNS client of its HTTP server itself, and
        /// so is the one to dispose of it: the server was lent it, and leaves
        /// it alone. One that was handed in belongs to whoever handed it in.
        /// </summary>
        private readonly Boolean      ownsDNSClient;

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



        public HTTPServer             HTTPServer        { get; }

        /// <summary>
        /// The optional default HTTP APIX.
        /// </summary>
        public HTTPExtAPI                 DefaultHTTPAPI   { get; }


        /// <summary>
        /// The DNS client used by the network service node.
        /// </summary>
        public IDNSClient                  DNSClient
            => HTTPServer.DNSClient;

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

                                  HTTPServer?                  HTTPServer       = null,
                                  HTTPExtAPI?                  DefaultHTTPAPI   = null,

                                  IDNSClient?                  DNSClient        = null,
                                  ILoggerFactory?              LoggerFactory    = null)
        {

            this.Id              = Id             ?? NetworkServiceNode_Id.NewRandom();
            this.Name            = Name           ?? I18NString.Empty;
            this.Description     = Description    ?? I18NString.Empty;

            if (Identities is not null)
                foreach (var identity in Identities.Where(cryptoKey => cryptoKey.KeyUsages.Contains(CryptoKeyUsage.Identity)))
                    AddCryptoKey(identity);

            if (IdentityGroups is not null)
                foreach (var identityGroup in IdentityGroups.Where(cryptoKey => cryptoKey.KeyUsages.Contains(CryptoKeyUsage.IdentityGroup)))
                    AddCryptoKey(identityGroup);

            // The DNS client of a node is that of its HTTP server. One made beside
            // a server that was handed in served nobody, and its cache cleaned up
            // on a timer for the rest of the process; so one is made only for a
            // server of this node's own making.
            this.ownsHTTPServer  = HTTPServer is null;
            this.ownsDNSClient   = HTTPServer is null && DNSClient is null;

            this.HTTPServer      = HTTPServer     ?? new HTTPServer(
                                                         TCPPort:         IPPort.Parse(1234),
                                                         HTTPServerName:  this.Id.ToString(),
                                                         //Description:     this.Description,
                                                         DNSClient:       DNSClient ?? new DNSClient(
                                                                                           LoggerFactory: LoggerFactory
                                                                                       ),
                                                         LoggerFactory:   LoggerFactory
                                                     );

            this.DefaultHTTPAPI  = DefaultHTTPAPI ?? new HTTPExtAPI(
                                                         this.HTTPServer,
                                                         Description:     this.Description,
                                                         LoggerFactory:   LoggerFactory
                                                     );

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

        public Boolean AddCryptoKey(CryptoKeyInfo CryptoKeyInfo)

            => cryptoWallet.Add(CryptoKeyInfo);

        #endregion

        #region HTTP APIs

        #region Data

        private readonly ConcurrentDictionary<String, HTTPExtAPI> httpAPIs = [];

        /// <summary>
        /// An enumeration of all HTTP APIs.
        /// </summary>
        public IEnumerable<HTTPExtAPI> HTTPAPIs
            => httpAPIs.Values;

        #endregion

        public Boolean AddHTTPAPI(String       HTTPAPIId,
                                  HTTPExtAPI  HTTPAPI)
        {

            return httpAPIs.TryAdd(HTTPAPIId, HTTPAPI);

        }

        public HTTPExtAPI? GetHTTPAPI(String HTTPAPIId)
        {

            return httpAPIs.TryGet(HTTPAPIId);

        }

        #endregion


        //ToDo: Add HTTP WebSocket Servers
        //ToDo: Add Trackers
        //ToDo: Add Overlay Networks

        //ToDo: Add ADataStores!?!


        #region Clone()

        /// <summary>
        /// Clone this network service node.
        /// </summary>
        public NetworkServiceNode Clone()

            => new (
                   Id.Clone()
               );

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

        #region (override) GetHashCode()

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


        #region Dispose/Async()

        /// <summary>
        /// Dispose of the HTTP server and its DNS client, where this node made
        /// them. Their maintenance, Warden and DNS cache timers used to run for
        /// as long as the process did. What was handed in is left to whoever
        /// handed it in.
        /// </summary>
        public virtual async ValueTask DisposeAsync()
        {

            if (ownsHTTPServer)
                await HTTPServer.DisposeAsync().ConfigureAwait(false);

            if (ownsDNSClient)
                await DNSClient.DisposeAsync().ConfigureAwait(false);

            GC.SuppressFinalize(this);

        }

        /// <summary>
        /// Dispose of the HTTP server and its DNS client, where this node made
        /// them, and leave what was handed in alone.
        /// </summary>
        public virtual void Dispose()
        {

            if (ownsHTTPServer)
                HTTPServer.Dispose();

            if (ownsDNSClient)
                DNSClient.Dispose();

            GC.SuppressFinalize(this);

        }

        #endregion

    }

}
