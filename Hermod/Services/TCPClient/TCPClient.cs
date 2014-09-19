/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Services.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Services.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services
{

    public class TCPClient
    {

        #region Data

        private           A[]        _CachedIPv4Addresses;
        private           AAAA[]     _CachedIPv6Addresses;

        private           DNSClient         _DNSClient;
        private           TcpClient         _TcpClient;
        private           NetworkStream     _TcpStream;

        #endregion

        #region Properties

        #region DNSName

        public String DNSName { get; set; }

        #endregion

        #region ServiceName

        public String ServiceName { get; set; }

        #endregion

        #region UseIPv4

        public Boolean _UseIPv4;

        public Boolean UseIPv4
        {

            get
            {
                return _UseIPv4;
            }

            set
            {
                _UseIPv4 = value;
                // Change DNSClient!
            }

        }

        #endregion

        #region UseIPv6

        public Boolean _UseIPv6;

        public Boolean UseIPv6
        {

            get
            {
                return _UseIPv6;
            }

            set
            {
                _UseIPv6 = value;
                // Change DNSClient!
            }

        }

        #endregion

        #region ConnectionTimeout

        public TimeSpan ConnectionTimeout { get; set; }

        #endregion

        #endregion

        #region Events

        #region Connected

        public delegate void CSConnectedDelegate(Object Sender, String EVSEOperatorName);

        public event CSConnectedDelegate Connected;

        #endregion

        #endregion

        #region Constructor(s)

        #region TCPClient(DNSName = null, ServiceName = "", ConnectionTimeout = null, DNSClient = null, AutoConnect = false)

        /// <summary>
        /// Create a new TCPClient connecting to a remote service using DNS SRV records.
        /// </summary>
        /// <param name="DNSName">The optional DNS name of the remote service to connect to.</param>
        /// <param name="ServiceName">The optional DNS SRV service name of the remote service to connect to.</param>
        /// <param name="UseIPv4">Wether to use IPv4 as networking protocol.</param>
        /// <param name="UseIPv6">Wether to use IPv6 as networking protocol.</param>
        /// <param name="PreferIPv6">Prefer IPv6 (instead of IPv4) as networking protocol.</param>
        /// <param name="ConnectionTimeout">The timeout connecting to the remote service.</param>
        /// <param name="DNSClient">An optional DNS client used to resolve DNS names.</param>
        /// <param name="AutoConnect">Connect to the EVSE operator backend automatically on startup. Default is false.</param>
        public TCPClient(String     DNSName            = "",
                         String     ServiceName        = "",
                         Boolean    UseIPv4            = true,
                         Boolean    UseIPv6            = false,
                         Boolean    PreferIPv6         = false,
                         TimeSpan?  ConnectionTimeout  = null,
                         DNSClient  DNSClient          = null,
                         Boolean    AutoConnect        = false)
        {

            this.DNSName            = DNSName;
            this.ServiceName        = ServiceName;
            this._UseIPv4           = UseIPv4;
            this._UseIPv6           = UseIPv6;

            this.ConnectionTimeout  = (ConnectionTimeout.HasValue)
                                          ? ConnectionTimeout.Value
                                          : TimeSpan.FromSeconds(60);

            this._DNSClient         = (DNSClient != null)
                                          ? DNSClient
                                          : new DNSClient(SearchForIPv4Servers: _UseIPv4,
                                                          SearchForIPv6Servers: _UseIPv6);

            if (AutoConnect)
                Connect();

        }

        #endregion

        #endregion


        #region (private) Query DNS

        private void QueryDNS()
        {

            var IPv4 = _DNSClient.Query<A>(DNSName);
            if (IPv4.Any())
                _CachedIPv4Addresses = IPv4.ToArray();

            var IPv6 = _DNSClient.Query<AAAA>(DNSName);
            if (IPv6.Any())
                _CachedIPv6Addresses = IPv6.ToArray();

        }

        #endregion

        #region (private) Reconnect()

        private void Reconnect()
        {

            try
            {

                if (_TcpStream != null)
                    _TcpStream.Close();

                if (_TcpClient != null)
                    _TcpClient.Close();

            }
            catch (Exception)
            { }

            try
            {

                _TcpClient  = new TcpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(_CachedIPv4Addresses.First().ToString()),
                                            80));
                _TcpStream  = _TcpClient.GetStream();

            }
            catch (Exception)
            {
                _TcpClient  = null;
                _TcpStream  = null;
                // DO SOME LOGGING OR SO!
            }

        }

        #endregion


        #region Connect()

        public TCPConnectResult Connect()
        {

            if (DNSName == null &&
                DNSName == String.Empty)
                return TCPConnectResult.NoDNSGiven;

            QueryDNS();

            if (_CachedIPv4Addresses == null &&
                _CachedIPv6Addresses == null)
                return TCPConnectResult.NoIPAddressFound;

            if (_CachedIPv4Addresses.Any())
            {
                Reconnect();
            }

            return TCPConnectResult.UnknownError;

        }

        #endregion

        #region Disconnect()

        public TCPDisconnectResult Disconnect()
        {
            return TCPDisconnectResult.Ok;
        }

        #endregion


    }

}
