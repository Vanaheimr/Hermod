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

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Hermod.Services.DNS;
using eu.Vanaheimr.Hermod.Services.TCP;

#endregion

namespace eu.Vanaheimr.Hermod.Services
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

        private String _DNSName;

        public String DNSName
        {

            get
            {
                return _DNSName;
            }

            set
            {
                if (value != null && value != String.Empty)
                    _DNSName = value;
            }

        }

        #endregion

        #endregion

        #region Events

        #region Connected

        public delegate void CSConnectedDelegate(Object Sender, String EVSEOperatorName);

        public event CSConnectedDelegate Connected;

        #endregion

        #endregion

        #region Constructor(s)

        #region TCPClient()

        public TCPClient(String     DNSName      = null,
                         DNSClient  DNSClient    = null,
                         Boolean    AutoConnect  = false)
        {

            this._DNSName    = (DNSName != null && DNSName != String.Empty)
                                  ? DNSName
                                  : String.Empty;

            this._DNSClient  = (DNSClient != null)
                                   ? DNSClient
                                   : new DNSClient(SearchForIPv4Servers: true,
                                                   SearchForIPv6Servers: false);

            if (AutoConnect)
                Connect();

        }

        #endregion

        #endregion


        #region (private) Query DNS

        private void QueryDNS()
        {

            var IPv4 = _DNSClient.Query<A>(_DNSName);
            if (IPv4.Any())
                _CachedIPv4Addresses = IPv4.ToArray();

            var IPv6 = _DNSClient.Query<AAAA>(_DNSName);
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

            if (_DNSName == null &&
                _DNSName == String.Empty)
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
