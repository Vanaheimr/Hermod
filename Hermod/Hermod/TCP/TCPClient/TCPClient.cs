/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// Extension methods for sockets.
    /// </summary>
    public static class SocketExtensions
    {

        #region Poll(this Socket, Mode, CancellationToken)

        public static void Poll(this Socket        Socket,
                                SelectMode         Mode,
                                CancellationToken  CancellationToken)
        {

            if (!CancellationToken.CanBeCanceled)
                return;

            if (Socket != null)
            {
                do
                {
                    CancellationToken.ThrowIfCancellationRequested();
                } while (!Socket.Poll(1000, Mode));
            }

            else
                CancellationToken.ThrowIfCancellationRequested();

        }

        #endregion

    }

    /// <summary>
    /// A TCP client.
    /// </summary>
    public class TCPClient
    {

        #region Data

        private readonly List<IPSocket>  _IPSocketList;
        public const     SslProtocols    DefaultSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

        #endregion

        #region Properties

        public DomainName                  RemoteHost               { get; }

        public DNSService                  ServiceName              { get; }

        public IPPort                      RemotePort               { get; }

        public IEnumerable<IPSocket>       IPSocketList
            => _IPSocketList;

        public IPSocket                    CurrentIPSocket          { get; private set; }

        public Boolean                     UseIPv4                  { get; }

        public Boolean                     UseIPv6                  { get; }

        public Boolean                     PreferIPv6               { get; }

        public TLSUsage                    UseTLS                   { get; }

        #region ConnectionTimeout

        private TimeSpan _ConnectionTimeout;

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

        public DNSClient?                  DNSClient                { get; }

        public CancellationToken?          CancellationToken        { get; private set; }

        public Socket?                     TCPSocket                { get; private set; }

        public Stream?                     Stream                   { get; private set; }

        public NetworkStream?              TCPStream                { get; private set; }

        public SslStream?                  TLSStream                { get; private set; }

        public X509CertificateCollection?  TLSClientCertificates    { get; }

        #endregion

        #region Events

        #region Connected

        public delegate void TCPConnectedDelegate(Object Sender, DomainName DNSName, IPSocket IPSocket);

        public event TCPConnectedDelegate Connected;

        #endregion

        #region ValidateRemoteCertificate

        public delegate Boolean ValidateRemoteCertificateDelegate(TCPClient Sender, X509Certificate? Certificate, X509Chain? CertificateChain, SslPolicyErrors PolicyErrors);

        public event ValidateRemoteCertificateDelegate? ValidateServerCertificate;

        #endregion

        #endregion

        #region Constructor(s)

        #region TCPClient(IPAddress, AutoConnect = false)

        /// <summary>
        /// Create a new TCPClient connecting to a remote service using an IP address.
        /// </summary>
        /// <param name="ConnectionTimeout">The timeout connecting to the remote service.</param>
        /// <param name="AutoConnect">Connect to the TCP service automatically on startup. Default is false.</param>
        public TCPClient(IIPAddress                         IPAddress,
                         IPPort                             RemotePort,
                         TLSUsage                           UseTLS                      = TLSUsage.STARTTLS,
                         ValidateRemoteCertificateDelegate  ValidateServerCertificate   = null,
                         TimeSpan?                          ConnectionTimeout           = null,
                         Boolean                            AutoConnect                 = false)
        {

            this.RemotePort                 = RemotePort;
            this.UseTLS                     = UseTLS;
            this.ValidateServerCertificate  = ValidateServerCertificate;
            this._ConnectionTimeout         = ConnectionTimeout ?? TimeSpan.FromSeconds(60);
            this.DNSClient                  = DNSClient         ?? new DNSClient(SearchForIPv4DNSServers: this.UseIPv4,
                                                                                 SearchForIPv6DNSServers: this.UseIPv6);

            this._IPSocketList               = [ new IPSocket(IPAddress, RemotePort) ];

            if (AutoConnect)
                Connect();

        }

        #endregion

        #region TCPClient(DNSName = null, ServiceName = "", ConnectionTimeout = null, DNSClient = null, AutoConnect = false)

        /// <summary>
        /// Create a new TCPClient connecting to a remote service using DNS SRV records.
        /// </summary>
        /// <param name="RemoteHost">The optional DNS name of the remote service to connect to.</param>
        /// <param name="ServiceName">The optional DNS SRV service name of the remote service to connect to.</param>
        /// <param name="UseIPv4">Whether to use IPv4 as networking protocol.</param>
        /// <param name="UseIPv6">Whether to use IPv6 as networking protocol.</param>
        /// <param name="PreferIPv6">Prefer IPv6 (instead of IPv4) as networking protocol.</param>
        /// <param name="ConnectionTimeout">The timeout connecting to the remote service.</param>
        /// <param name="DNSClient">An optional DNS client used to resolve DNS names.</param>
        /// <param name="AutoConnect">Connect to the TCP service automatically on startup. Default is false.</param>
        public TCPClient(DomainName                          RemoteHost,
                         DNSService                          ServiceName,
                         Boolean                             UseIPv4                     = true,
                         Boolean                             UseIPv6                     = false,
                         Boolean                             PreferIPv6                  = false,
                         TLSUsage                            UseTLS                      = TLSUsage.STARTTLS,
                         ValidateRemoteCertificateDelegate?  ValidateServerCertificate   = null,
                         TimeSpan?                           ConnectionTimeout           = null,
                         DNSClient?                          DNSClient                   = null,
                         Boolean                             AutoConnect                 = false)
        {

            this.RemoteHost                 = RemoteHost;
            this.ServiceName                = ServiceName;
            this.UseIPv4                    = UseIPv4;
            this.UseIPv6                    = UseIPv6;
            this.PreferIPv6                 = PreferIPv6;
            this.UseTLS                     = UseTLS;
            this.ValidateServerCertificate  = ValidateServerCertificate;
            this._ConnectionTimeout         = ConnectionTimeout ?? TimeSpan.FromSeconds(60);
            this.DNSClient                  = DNSClient         ?? new DNSClient(
                                                                       SearchForIPv4DNSServers: this.UseIPv4,
                                                                       SearchForIPv6DNSServers: this.UseIPv6
                                                                   );

            this._IPSocketList              = [];

            if (IPv4Address.TryParse(this.RemoteHost, out IPv4Address ipv4address))
                _IPSocketList.Add(new IPSocket(ipv4address, RemotePort));

            if (IPv6Address.TryParse(this.RemoteHost, out IPv6Address ipv6address))
                _IPSocketList.Add(new IPSocket(ipv6address, RemotePort));

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
        /// <param name="UseIPv4">Whether to use IPv4 as networking protocol.</param>
        /// <param name="UseIPv6">Whether to use IPv6 as networking protocol.</param>
        /// <param name="PreferIPv6">Prefer IPv6 (instead of IPv4) as networking protocol.</param>
        /// <param name="UseTLS">Whether Transport Layer Security should be used or not.</param>
        /// <param name="ValidateServerCertificate">A callback for validating the remote server certificate.</param>
        /// <param name="ConnectionTimeout">The timeout connecting to the remote service.</param>
        /// <param name="DNSClient">An optional DNS client used to resolve DNS names.</param>
        /// <param name="AutoConnect">Connect to the TCP service automatically on startup. Default is false.</param>
        /// <param name="CancellationToken"></param>
        public TCPClient(DomainName                          RemoteHost,
                         IPPort                              RemotePort,
                         Boolean                             UseIPv4                     = true,
                         Boolean                             UseIPv6                     = false,
                         Boolean                             PreferIPv6                  = false,
                         TLSUsage                            UseTLS                      = TLSUsage.STARTTLS,
                         ValidateRemoteCertificateDelegate?  ValidateServerCertificate   = null,
                         TimeSpan?                           ConnectionTimeout           = null,
                         DNSClient?                          DNSClient                   = null,
                         Boolean                             AutoConnect                 = false,
                         CancellationToken?                  CancellationToken           = null)
        {

            this.RemoteHost                 = RemoteHost;
            this.RemotePort                 = RemotePort;
            this.CancellationToken          = CancellationToken != null ? CancellationToken : new CancellationToken();
            this.UseIPv4                    = UseIPv4;
            this.UseIPv6                    = UseIPv6;
            this.PreferIPv6                 = PreferIPv6;
            this.UseTLS                     = UseTLS;
            this.ValidateServerCertificate  = ValidateServerCertificate ?? ((TCPClient, Certificate, CertificateChain, PolicyErrors) => false);
            this._ConnectionTimeout         = ConnectionTimeout         ?? TimeSpan.FromSeconds(60);
            this.DNSClient                  = DNSClient                 ?? new DNSClient(SearchForIPv4DNSServers: this.UseIPv4,
                                                                                         SearchForIPv6DNSServers: this.UseIPv6);

            this._IPSocketList              = [];

            if (IPv4Address.TryParse(RemoteHost, out IPv4Address ipv4address))
                _IPSocketList.Add(new IPSocket(ipv4address, RemotePort));

            if (IPv6Address.TryParse(RemoteHost, out IPv6Address ipv6address))
                _IPSocketList.Add(new IPSocket(ipv6address, RemotePort));

            if (AutoConnect)
                Connect();

        }

        #endregion

        #endregion


        #region (private) QueryDNS()

        private async Task QueryDNS()
        {
            if (DNSClient is not null)
            {

                foreach (var socket in (await DNSClient.Query<A>   (RemoteHost)).FilteredAnswers.SafeSelect(ARecord => new IPSocket(ARecord.IPv4Address, RemotePort)))
                    _IPSocketList.Add(socket);

                foreach (var socket in (await DNSClient.Query<AAAA>(RemoteHost)).FilteredAnswers.SafeSelect(ARecord => new IPSocket(ARecord.IPv6Address, RemotePort)))
                    _IPSocketList.Add(socket);

            }
        }

        #endregion

        #region (private) CreateAndConnectTCPSocket(_IPAddress, Port)

        private Socket CreateAndConnectTCPSocket(IIPAddress ipAddress, IPPort Port)
        {

            Socket tcpSocket;

            if      (ipAddress is IPv4Address)
                tcpSocket = new Socket(AddressFamily.InterNetwork,   SocketType.Stream, ProtocolType.Tcp);

            else if (ipAddress is IPv6Address)
                tcpSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

            else
                throw new Exception("IP address '" + ipAddress.ToString() + "' is invalid!");

            tcpSocket.Connect(System.Net.IPAddress.Parse(ipAddress.ToString()), Port.ToUInt16());

            return tcpSocket;

        }

        #endregion

        #region Connect()

        public TCPConnectResult Connect()
        {

            // if already connected => return!

            // If an ip address was given, this list will be pre-populated!
            var UseDNS  = _IPSocketList.Count == 0;
            var retry   = 0;

            do
            {

                if (UseDNS)
                    QueryDNS().Wait();

                if (_IPSocketList.Count == 0)
                    return TCPConnectResult.NoIPAddressFound;

                foreach (var ipSocket in _IPSocketList)
                {

                    CurrentIPSocket = ipSocket;

                    #region Close previous streams and sockets...

                    try
                    {

                        if (TCPStream is not null)
                        {
                            TCPStream.Close();
                            TCPStream = null;
                        }

                    }
                    catch
                    { }

                    try
                    {

                        if (TLSStream is not null)
                        {
                            TLSStream.Close();
                            TLSStream = null;
                        }

                    }
                    catch
                    { }

                    Stream = null;

                    try
                    {

                        if (TCPSocket is not null)
                        {
                            TCPSocket.Close();
                            TCPSocket = null;
                        }

                    }
                    catch
                    { }

                    #endregion

                    try
                    {

                        TCPSocket  = CreateAndConnectTCPSocket(CurrentIPSocket.IPAddress, CurrentIPSocket.Port);
                        TCPStream = new NetworkStream(TCPSocket, true);// {
                        //    ReadTimeout = 5000
                        //};
                        Stream     = TCPStream;

                        if (UseTLS == TLSUsage.TLSSocket)
                            EnableTLS();

                        Connected?.Invoke(this,
                                          RemoteHost,
                                          CurrentIPSocket);

                        return TCPConnectResult.Ok;

                    }
                    catch (Exception e)
                    {

                        DebugX.LogT("TCP client reconnect failed!" + Environment.NewLine +
                                    e.Message);

                        Stream     = null;
                        TCPStream  = null;
                        TLSStream  = null;
                        TCPSocket  = null;

                    }

                }

                retry++;

                if (UseDNS)
                    _IPSocketList.Clear();

                Thread.Sleep(5000);

            } while (retry < 2);

            return TCPConnectResult.UnknownError;

        }

        #endregion


        #region (protected) EnableTLS()

        protected void EnableTLS()
        {

            if (Stream is null)
                throw new Exception("Cannot enable TLS on a null stream!");

            try
            {
                TLSStream  = new SslStream(Stream, false, ValidateRemoteCertificate);
                TLSStream.AuthenticateAsClient(RemoteHost.FullName, TLSClientCertificates, DefaultSslProtocols, true);
                Stream     = TLSStream;
            }
            catch (Exception e)
            {
                DebugX.LogT("EnableTLS() failed!" + Environment.NewLine +
                            e.Message);
            }

        }

        #endregion

        #region (private) ValidateRemoteCertificate(Sender, Certificate, Chain, Errors)
        private Boolean ValidateRemoteCertificate(Object            Sender,
                                                  X509Certificate?  Certificate,
                                                  X509Chain?        Chain,
                                                  SslPolicyErrors   Errors)
        {

            var ValidateRemoteCertificateLocal = ValidateServerCertificate;
            if (ValidateRemoteCertificateLocal is not null)
                return ValidateRemoteCertificateLocal(this, Certificate, Chain, Errors);

            return false;

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
