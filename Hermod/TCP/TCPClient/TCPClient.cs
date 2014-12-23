﻿/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Net;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services
{

    public static class Ext
    {

        public static void Poll(this Socket socket, SelectMode mode, CancellationToken cancellationToken)
        {

            if (!cancellationToken.CanBeCanceled)
                return;

            if (socket != null)
            {
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                } while (!socket.Poll(1000, mode));
            }

            else
                cancellationToken.ThrowIfCancellationRequested();

        }

        public static Socket CreateAndConnectTCPSocket(IIPAddress IP_Address, IPPort Port)
        {

            Socket _TCPSocket = null;

            if (IP_Address is IPv4Address)
                _TCPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            else if (IP_Address is IPv6Address)
                _TCPSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

            _TCPSocket.Connect(IPAddress.Parse(IP_Address.ToString()), Port.ToUInt16());

            return _TCPSocket;

        }

    }

    public class TCPClient
    {

        #region Data

        private           A[]                    _CachedIPv4Addresses;
        private           AAAA[]                 _CachedIPv6Addresses;
        private           List<IPSocket>         OrderedDNS;
        private           IEnumerator<IPSocket>  OrderedDNSEnumerator;

        #endregion

        #region Properties

        #region RemoteHost

        private String _RemoteHost;

        public String RemoteHost
        {
            get
            {
                return _RemoteHost;
            }
        }

        #endregion

        #region ServiceName

        private String _ServiceName;

        public String ServiceName
        {
            get
            {
                return _ServiceName;
            }
        }

        #endregion

        #region RemotePort

        private IPPort _RemotePort;

        public IPPort RemotePort
        {
            get
            {
                return _RemotePort;
            }
        }

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

        #region PreferIPv6

        public Boolean _PreferIPv6;

        public Boolean PreferIPv6
        {

            get
            {
                return _PreferIPv6;
            }

            set
            {
                _PreferIPv6 = value;
                // Change DNSClient!
            }

        }

        #endregion

        #region ConnectionTimeout

        public TimeSpan _ConnectionTimeout;

        public TimeSpan ConnectionTimeout
        {

            get
            {
                return _ConnectionTimeout;
            }

            set
            {
                _ConnectionTimeout = value;
                // Change DNSClient!
            }

        }

        #endregion

        #region DNSClient

        private readonly DNSClient _DNSClient;

        /// <summary>
        /// The default server name.
        /// </summary>
        public virtual DNSClient DNSClient
        {
            get
            {
                return _DNSClient;
            }
        }

        #endregion

        #region CancellationToken

        public CancellationToken CancellationToken { get; private set; }

        #endregion


        #region TCPSocket

        private Socket _TCPSocket;

        public Socket TCPSocket
        {
            get
            {
                return _TCPSocket;
            }
        }

        #endregion

        #region TCPStream

        private NetworkStream _TCPStream;

        public NetworkStream TCPStream
        {
            get
            {
                return _TCPStream;
            }
        }

        #endregion

        #endregion

        #region Events

        #region Connected

        public delegate void CSConnectedDelegate(Object Sender, String DNSName, IPSocket IPSocket);

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
        /// <param name="AutoConnect">Connect to the TCP service automatically on startup. Default is false.</param>
        public TCPClient(String     DNSName            = "",
                         String     ServiceName        = "",
                         Boolean    UseIPv4            = true,
                         Boolean    UseIPv6            = false,
                         Boolean    PreferIPv6         = false,
                         TimeSpan?  ConnectionTimeout  = null,
                         DNSClient  DNSClient          = null,
                         Boolean    AutoConnect        = false)
        {

            this._RemoteHost         = DNSName;
            this._ServiceName        = ServiceName;
            this._UseIPv4            = UseIPv4;
            this._UseIPv6            = UseIPv6;
            this._PreferIPv6         = PreferIPv6;

            this._ConnectionTimeout  = (ConnectionTimeout.HasValue)
                                           ? ConnectionTimeout.Value
                                           : TimeSpan.FromSeconds(60);

            this._DNSClient          = (DNSClient != null)
                                           ? DNSClient
                                           : new DNSClient(SearchForIPv4DNSServers: _UseIPv4,
                                                           SearchForIPv6DNSServers: _UseIPv6);

            if (AutoConnect)
                Connect();

        }

        #endregion

        #region TCPClient(RemoteHost, RemotePort, ...)

        /// <summary>
        /// 
        /// </summary>
        /// <param name="RemoteHost"></param>
        /// <param name="RemotePort"></param>
        /// <param name="CancellationToken"></param>
        /// <param name="UseIPv4">Wether to use IPv4 as networking protocol.</param>
        /// <param name="UseIPv6">Wether to use IPv6 as networking protocol.</param>
        /// <param name="PreferIPv6">Prefer IPv6 (instead of IPv4) as networking protocol.</param>
        /// <param name="ConnectionTimeout">The timeout connecting to the remote service.</param>
        /// <param name="DNSClient">An optional DNS client used to resolve DNS names.</param>
        /// <param name="AutoConnect">Connect to the TCP service automatically on startup. Default is false.</param>
        public TCPClient(String              RemoteHost,
                         IPPort              RemotePort,
                         CancellationToken   CancellationToken,
                         Boolean             UseIPv4            = true,
                         Boolean             UseIPv6            = false,
                         Boolean             PreferIPv6         = false,
                         TimeSpan?           ConnectionTimeout  = null,
                         DNSClient           DNSClient          = null,
                         Boolean             AutoConnect        = false)

        {

            this._RemoteHost         = RemoteHost;
            this._RemotePort         = RemotePort;
            this.CancellationToken   = CancellationToken;
            this._UseIPv4            = UseIPv4;
            this._UseIPv6            = UseIPv6;
            this._PreferIPv6         = PreferIPv6;

            this._ConnectionTimeout  = (ConnectionTimeout.HasValue)
                                           ? ConnectionTimeout.Value
                                           : TimeSpan.FromSeconds(60);

            this._DNSClient          = (DNSClient == null)
                                           ? new DNSClient(SearchForIPv6DNSServers: true)
                                           : DNSClient;

            if (AutoConnect)
                Connect();

        }

        #endregion

        #endregion


        #region (private) QueryDNS()

        private void QueryDNS()
        {

            var IPv4 = _DNSClient.Query<A>(RemoteHost);
            if (IPv4.Any())
                _CachedIPv4Addresses = IPv4.ToArray();

            var IPv6 = _DNSClient.Query<AAAA>(RemoteHost);
            if (IPv6.Any())
                _CachedIPv6Addresses = IPv6.ToArray();

            OrderedDNS = (IPv4.Select(ARecord    => new IPSocket(ARecord.   IPv4Address, this.RemotePort)).Concat(
                          IPv6.Select(AAAARecord => new IPSocket(AAAARecord.IPv6Address, this.RemotePort)))).
                          ToList();

            OrderedDNSEnumerator = OrderedDNS.GetEnumerator();

        }

        #endregion

        #region (private) Reconnect()

        private Boolean Reconnect(IPSocket IPSocket)
        {

            #region Close previous TCP stream and sockets...

            try
            {

                if (_TCPStream != null)
                    _TCPStream.Close();

            }
            catch (Exception)
            { }

            try
            {

                if (_TCPSocket != null)
                    _TCPSocket.Close();

            }
            catch (Exception)
            { }

            #endregion

            try
            {

                _TCPSocket = Ext.CreateAndConnectTCPSocket(IPSocket.IPAddress, IPSocket.Port);
                _TCPStream = new NetworkStream(_TCPSocket, true);

            }
            catch (Exception e)
            {
                _TCPStream  = null;
                _TCPSocket  = null;
                return false;
            }

            var ConnectedLocal = Connected;
            if (ConnectedLocal != null)
                ConnectedLocal(this, RemoteHost, IPSocket);

            return true;

        }

        #endregion


        #region Connect()

        public TCPConnectResult Connect()
        {

            // if already connected => return!

            if (RemoteHost == null &&
                RemoteHost == String.Empty)
                return TCPConnectResult.InvalidDomainName;

            QueryDNS();

            if (OrderedDNS.Count == 0)
                return TCPConnectResult.NoIPAddressFound;

            // Get next IP socket in ordered list...
            while (OrderedDNSEnumerator.MoveNext())
            {

                if (Reconnect(OrderedDNSEnumerator.Current))
                    return TCPConnectResult.Ok;

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