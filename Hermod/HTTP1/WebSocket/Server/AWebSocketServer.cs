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

using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Authentication;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// An HTTP WebSocket server: a WebSocket endpoint with a TCP listener of its own.
    /// </summary>
    /// <remarks>
    /// <b>The protocol is the endpoint's.</b> Everything <see cref="AWebSocketEndpoint"/> does, this
    /// does alike; what this adds is where the connections come from: a port of its own, with TLS,
    /// connection limits, maintenance and a warden, as every TCP server of this library has them.
    ///
    /// <b>The listener is owned, not inherited.</b> A WebSocket used to be a TCP server, and so every
    /// WebSocket put on an HTTP path built one - listeners, a DNS client, a timer, a warden, and with
    /// port 0 a socket bound and listening that nobody served. Now the TCP server is a part of this
    /// server, and an endpoint has none. Its settings and events are this server's as they always were,
    /// and every callback - a certificate selector, a validator, a connection identification builder,
    /// a started, stopped or rejected event - is given this server as its sender, as before.
    /// <see cref="TCPListener"/> is the listener itself, for its own TCP-level events.
    /// </remarks>
    public abstract class AWebSocketServer : AWebSocketEndpoint,
                                             ITCPServer
    {

        #region Data

        private readonly Listener                                                  listener;
        private          ServerCertificateChainSelectorDelegate?                   serverCertificateChainSelector;
        private          RemoteTLSClientCertificateValidationHandler<ITCPServer>?  clientCertificateValidator;

        /// <summary>
        /// The HTTP/3 server beside the listener, the certificates it shows and
        /// what ends its WebSockets: while this server takes WebSockets over
        /// HTTP/3 (AcceptHTTP3) and is running.
        /// </summary>
        private          Http3Server?                                               http3Server;
        private          QUICCertificates?                                          http3Certificates;
        private          CancellationTokenSource?                                   http3Cancellation;
        private          Boolean                                                    acceptHTTP3;

        /// <summary>
        /// Whether the listener was started, as far as this server knows:
        /// IsRunning says so only once the listener is accepting.
        /// </summary>
        private volatile Boolean                                                    listenerStarted;

        /// <summary>
        /// The HTTP/3 servers beside the first: a listener on "localhost" is
        /// one on both loopback addresses, and QUIC has a socket for each.
        /// </summary>
        private          List<Http3Server>                                          http3MoreServers   = [];
        private readonly Lock                                                       http3Lock          = new();

        /// <summary>
        /// Done once the HTTP/3 servers stopped last have let go of their UDP
        /// ports: a stop asked for while another is under way waits for that
        /// one, so that nobody takes the port to be free before it is.
        /// </summary>
        private          Task                                                       http3Stopped       = Task.CompletedTask;
        private          Int32                                                     disposed;

        /// <summary>
        /// The servers this one's listener hands WebSockets to, by their
        /// subprotocol - see <see cref="RouteBySubprotocol"/> - and the one
        /// whose listener hands WebSockets to this one, where another does.
        /// </summary>
        private          ImmutableArray<AWebSocketServer>                          routedServers      = [];
        private          AWebSocketServer?                                          routedBy;
        private readonly Lock                                                       routingLock        = new();

        #endregion

        #region Properties

        /// <summary>
        /// The TCP listener of this server: for its TCP-level events, e.g. every connection it accepts
        /// or closes, before and after the WebSocket protocol.
        /// </summary>
        public ATCPServer                                               TCPListener
            => listener;

        /// <summary>
        /// The IP address to listen on.
        /// </summary>
        public IPAddress                                               IPAddress
            => listener.IPAddress;

        /// <summary>
        /// The TCP port to listen on.
        /// </summary>
        public IPPort                                                   TCPPort
            => listener.TCPPort;

        /// <summary>
        /// The TCP port to listen on.
        /// </summary>
        public IPPort                                                   IPPort
            => listener.IPSocket.Port;

        /// <summary>
        /// The IP socket to listen on.
        /// </summary>
        public IPSocket                                                 IPSocket
            => listener.IPSocket;

        /// <summary>
        /// Whether the TCP listener is currently running.
        /// </summary>
        public Boolean                                                  IsRunning
            => listener.IsRunning;

        /// <summary>
        /// The receive timeout of its connections.
        /// </summary>
        public TimeSpan                                                 ReceiveTimeout
            => listener.ReceiveTimeout;

        /// <summary>
        /// The send timeout of its connections.
        /// </summary>
        public TimeSpan                                                 SendTimeout
            => listener.SendTimeout;

        /// <summary>
        /// Selects the TLS server certificate.
        /// </summary>
        public ServerCertificateSelectorDelegate?                       ServerCertificateSelector    { get; }

        /// <summary>
        /// Selects the TLS server certificate chain.
        /// </summary>
        public ServerCertificateChainSelectorDelegate?                  ServerCertificateChainSelector
        {

            get
            {
                return serverCertificateChainSelector;
            }

            set
            {

                serverCertificateChainSelector = value;

                listener.ServerCertificateChainSelector = value is not null
                                                              ? (tcpServer, tcpClient) => value(this, tcpClient)
                                                              : null;

                // A server that was running without TLS, and so without
                // HTTP/3, has what HTTP/3 needs now.
                if (value is not null && acceptHTTP3 && (listenerStarted || IsRunning))
                    StartHTTP3();

            }

        }

        /// <summary>
        /// Validates the TLS client certificates.
        /// </summary>
        public RemoteTLSClientCertificateValidationHandler<ITCPServer>? ClientCertificateValidator
        {

            get
            {
                return clientCertificateValidator;
            }

            set
            {

                clientCertificateValidator = value;

                listener.ClientCertificateValidator = value is not null
                                                          ? (sender, certificate, certificateChain, tlsServer, policyErrors) => value(sender, certificate, certificateChain, this, policyErrors)
                                                          : null;

            }

        }

        /// <summary>
        /// Whether this server shows its key raw, without its certificate
        /// (RFC 7250): never, to a client that takes raw public keys, or to
        /// no other. Over TCP and over QUIC alike. Set before the server starts.
        /// </summary>
        public RawPublicKeyMode                                         RawPublicKeys
        {
            get => listener.RawPublicKeys;
            set => listener.RawPublicKeys = value;
        }

        /// <summary>
        /// Whether a client's key may come raw (RFC 7250): never, where the
        /// client offers it, or in no other way. Only with a
        /// <see cref="ClientKeyValidator"/>, and over TCP. Set before the
        /// server starts.
        /// </summary>
        public RawPublicKeyMode                                         ClientRawPublicKeys
        {
            get => listener.ClientRawPublicKeys;
            set => listener.ClientRawPublicKeys = value;
        }

        /// <summary>
        /// Asked whether the raw public key of a client is believed: a key
        /// without a certificate, to be judged by its key pin.
        /// </summary>
        public RemoteTLSClientKeyValidationHandler<ITCPServer>?         ClientKeyValidator
        {

            get => clientKeyValidator;

            set
            {

                clientKeyValidator = value;

                // As for the certificate validator: this server is the sender, not its listener.
                listener.ClientKeyValidator = value is not null
                                                  ? (sender, clientKey, tlsServer) => value(sender, clientKey, this)
                                                  : null;

            }

        }

        private RemoteTLSClientKeyValidationHandler<ITCPServer>? clientKeyValidator;

        /// <summary>
        /// Selects the local TLS certificate.
        /// </summary>
        public LocalCertificateSelectionHandler?                        LocalCertificateSelector
            => listener.LocalCertificateSelector;

        /// <summary>
        /// The allowed TLS protocols.
        /// </summary>
        public SslProtocols?                                            AllowedTLSProtocols
            => listener.AllowedTLSProtocols;

        /// <summary>
        /// The TLS application protocols (ALPN) offered.
        /// </summary>
        public IEnumerable<SslApplicationProtocol>                      TLSApplicationProtocols
            => listener.TLSApplicationProtocols;

        /// <summary>
        /// Whether this server takes WebSockets over HTTP/2 as well (RFC 8441),
        /// on the port it takes them over HTTP/1.1 on. Off unless switched on.
        /// </summary>
        /// <remarks>
        /// With TLS the server then offers "h2" and "http/1.1" for ALPN, and a
        /// client that chooses "h2" is spoken to in HTTP/2; a client that
        /// offers nothing, or "http/1.1", as before. Without TLS a connection
        /// that starts with the connection preface of HTTP/2 is one (prior
        /// knowledge, RFC 9113, Section 3.3). An extended CONNECT with
        /// <c>:protocol = websocket</c> is then an upgrade request to this
        /// server, validated, authenticated and answered as every other: see
        /// <see cref="WebSocketOverHTTP2"/>. One WebSocket at a time per
        /// HTTP/2 connection: a connection is known by its remote socket here.
        /// Anything else asked over HTTP/2 is refused.
        /// </remarks>
        public Boolean                                                  AcceptHTTP2                   { get; set; }

        /// <summary>
        /// Whether this server takes WebSockets over HTTP/3 as well (RFC 9220):
        /// over QUIC, on the UDP port of the number of its TCP port. Off unless
        /// switched on; a server that is running takes the change at once.
        /// </summary>
        /// <remarks>
        /// HTTP/3 is HTTP over TLS and nothing else, so only a server that has
        /// a certificate takes it; the certificate is asked for at every new
        /// connection, as over TCP at every handshake, so a certificate that
        /// was replaced is shown to the next client without a restart; it is
        /// shown with a key of ECDSA P-256, ECDSA P-384 or RSA. A server
        /// without TLS, with another key, or whose UDP port is taken, runs as
        /// it would without this: <see cref="HTTP3Port"/> says so, and
        /// <see cref="WhyNoHTTP3"/> why, which is logged as well. It listens
        /// where its listener listens: a server on "localhost" on both
        /// loopback addresses. An extended CONNECT with <c>:protocol = websocket</c> is
        /// then an upgrade request to this server, validated, authenticated
        /// and answered as every other: see <see cref="WebSocketOverHTTP2"/>,
        /// whose translation is the same over HTTP/3. One WebSocket at a time
        /// per HTTP/3 connection, as over HTTP/2; anything else asked over
        /// HTTP/3 is refused.
        ///
        /// A client certificate is asked for, and one that is shown is judged
        /// by the <see cref="ClientCertificateValidator"/> this server has
        /// then - without one, by the roots of the operating system. A client
        /// that has none shows none. Where it is not required, a client whose
        /// certificate is refused is a client without one - over TCP its
        /// handshake fails.
        ///
        /// While it takes them, the server says so with every upgrade it
        /// accepts over HTTP/1.1 and HTTP/2: <c>Alt-Svc: h3=":port"</c>
        /// (RFC 7838), which is what a client that leaves the version of HTTP
        /// to the server goes by.
        /// </remarks>
        public Boolean                                                  AcceptHTTP3
        {

            get
            {
                return acceptHTTP3;
            }

            set
            {

                acceptHTTP3 = value;

                if (!listenerStarted && !IsRunning)
                    return;

                if (value)
                    StartHTTP3();
                else
                    _ = StopHTTP3();

            }

        }

        /// <summary>
        /// The UDP port this server takes WebSockets over HTTP/3 on, or null
        /// while it takes none: see <see cref="AcceptHTTP3"/>.
        /// </summary>
        public IPPort?                                                  HTTP3Port
            => http3Server is { } server
                   ? server.Port
                   : null;

        /// <summary>
        /// Why this server takes no WebSockets over HTTP/3 although it was to
        /// (<see cref="AcceptHTTP3"/>), or null: while it takes them, and while
        /// it was not asked to or is not running.
        /// </summary>
        public String?                                                  WhyNoHTTP3                    { get; private set; }

        /// <summary>
        /// Whether clients must present a TLS certificate.
        /// </summary>
        public Boolean                                                  ClientCertificateRequired
            => listener.ClientCertificateRequired;

        /// <summary>
        /// Whether the revocation of client certificates is checked.
        /// </summary>
        public Boolean                                                  CheckCertificateRevocation
            => listener.CheckCertificateRevocation;

        /// <summary>
        /// The connection timeout.
        /// </summary>
        public TimeSpan                                                 ConnectionTimeout
        {
            get => listener.ConnectionTimeout;
            set => listener.ConnectionTimeout = value;
        }

        /// <summary>
        /// The maximum number of concurrent client connections.
        /// </summary>
        public UInt32                                                   MaxClientConnections
            => listener.MaxClientConnections;

        /// <summary>
        /// The number of currently connected clients.
        /// </summary>
        public UInt32                                                   NumberOfConnectedClients
            => listener.NumberOfConnectedClients;

        /// <summary>
        /// The sockets of the currently connected clients.
        /// </summary>
        public IEnumerable<IPSocket>                                    ClientSockets
            => listener.ClientSockets;

        /// <summary>
        /// The currently connected clients.
        /// </summary>
        public IEnumerable<TCPConnection>                               ClientConnections
            => listener.ClientConnections;

        /// <summary>
        /// The DNS client of the TCP listener.
        /// </summary>
        public IDNSClient                                               DNSClient
            => listener.DNSClient;

        /// <summary>
        /// Whether the maintenance tasks are disabled.
        /// </summary>
        public Boolean                                                  DisableMaintenanceTasks
        {
            get => listener.DisableMaintenanceTasks;
            set => listener.DisableMaintenanceTasks = value;
        }

        /// <summary>
        /// The initial delay of the maintenance tasks.
        /// </summary>
        public TimeSpan                                                 MaintenanceInitialDelay
            => listener.MaintenanceInitialDelay;

        /// <summary>
        /// The interval of the maintenance tasks.
        /// </summary>
        public TimeSpan                                                 MaintenanceEvery
            => listener.MaintenanceEvery;

        /// <summary>
        /// Whether the warden tasks are disabled.
        /// </summary>
        public Boolean                                                  DisableWardenTasks
        {
            get => listener.DisableWardenTasks;
            set => listener.DisableWardenTasks = value;
        }

        /// <summary>
        /// The initial delay of the warden.
        /// </summary>
        public TimeSpan                                                 WardenInitialDelay
            => listener.WardenInitialDelay;

        /// <summary>
        /// The interval of the warden.
        /// </summary>
        public TimeSpan                                                 WardenCheckEvery
            => listener.WardenCheckEvery;

        /// <summary>
        /// The warden of the TCP listener.
        /// </summary>
        public Warden.Warden                                            Warden
            => listener.Warden;

        /// <summary>
        /// The connections of the TCP listener, with their handlers.
        /// </summary>
        protected ConcurrentDictionary<TCPConnection, Task>             activeClients
            => listener.ActiveClients;

        /// <summary>
        /// The servers the listener of this one hands WebSockets to, by their
        /// subprotocol: see <see cref="RouteBySubprotocol"/>.
        /// </summary>
        public IReadOnlyList<AWebSocketServer>                          RoutedServers
            => routedServers;

        /// <summary>
        /// The server whose listener hands WebSockets to this one, by their
        /// subprotocol - or null, where this one is reached by its own.
        /// </summary>
        public AWebSocketServer?                                        RoutedBy
            => routedBy;

        #endregion

        #region Events

        /// <summary>
        /// An event sent whenever the HTTP WebSocket server started.
        /// </summary>
        public event OnServerStartedDelegate?      OnServerStarted;

        /// <summary>
        /// An event sent whenever the TCP listener started.
        /// </summary>
        public event TCPServerStartedDelegate?     OnTCPServerStarted;

        /// <summary>
        /// An event sent whenever a TCP connection failed.
        /// </summary>
        public event TCPConnectionFailedDelegate?  OnTCPConnectionFailed;

        /// <summary>
        /// An event sent whenever the TCP listener stopped.
        /// </summary>
        public event TCPServerStoppedDelegate?     OnTCPServerStopped;

        /// <summary>
        /// An event sent whenever the HTTP WebSocket server stopped.
        /// </summary>
        public event OnServerStoppedDelegate?      OnServerStopped;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP WebSocket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: ATCPServer default.</param>
        /// <param name="TCPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServerName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP WebSocket service.</param>
        /// <param name="AutoStart">Whether to start the HTTP WebSocket server automatically.</param>
        public AWebSocketServer(IPAddress?                                               IPAddress                    = null,
                                IPPort?                                                   TCPPort                      = null,
                                String?                                                   HTTPServerName               = null,
                                I18NString?                                               Description                  = null,

                                Boolean?                                                  RequireAuthentication        = true,
                                IEnumerable<String>?                                      SecWebSocketProtocols        = null,
                                SubprotocolSelectorDelegate?                              SubprotocolSelector          = null,
                                Boolean                                                   DisableWebSocketPings        = false,
                                TimeSpan?                                                 WebSocketPingEvery           = null,
                                TimeSpan?                                                 SlowNetworkSimulationDelay   = null,

                                UInt32?                                                   BufferSize                   = null,
                                TimeSpan?                                                 ReceiveTimeout               = null,
                                TimeSpan?                                                 SendTimeout                  = null,
                                TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                                ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                                RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                                LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                                SslProtocols?                                             AllowedTLSProtocols          = null,
                                Boolean?                                                  ClientCertificateRequired    = null,
                                Boolean?                                                  CheckCertificateRevocation   = null,

                                ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                                UInt32?                                                   MaxClientConnections         = null,
                                IDNSClient?                                               DNSClient                    = null,

                                Boolean?                                                  DisableMaintenanceTasks      = false,
                                TimeSpan?                                                 MaintenanceInitialDelay      = null,
                                TimeSpan?                                                 MaintenanceEvery             = null,

                                Boolean?                                                  DisableWardenTasks           = false,
                                TimeSpan?                                                 WardenInitialDelay           = null,
                                TimeSpan?                                                 WardenCheckEvery             = null,

                                ILoggerFactory?                                           LoggerFactory                = null,
                                Boolean?                                                  AutoStart                    = false)

            : base(HTTPServerName,
                   Description,
                   RequireAuthentication,
                   SecWebSocketProtocols,
                   SubprotocolSelector,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,
                   ConnectionIdBuilder,
                   LoggerFactory)

        {

            this.ServerCertificateSelector  = ServerCertificateSelector;

            // Every callback of the listener is given this server, not the listener, as its sender.
            this.listener                   = new Listener(
                                                  this,
                                                  IPAddress,
                                                  TCPPort,
                                                  Description,
                                                  ReceiveTimeout,
                                                  SendTimeout,
                                                  LoggingHandler,

                                                  ServerCertificateSelector is not null
                                                      ? (tcpServer, tcpClient) => ServerCertificateSelector(this, tcpClient)
                                                      : null,
                                                  LocalCertificateSelector,
                                                  AllowedTLSProtocols,
                                                  ClientCertificateRequired,
                                                  CheckCertificateRevocation,

                                                  (sender, timestamp, localSocket, remoteSocket) => this.ConnectionIdBuilder(this, timestamp, localSocket, remoteSocket),
                                                  MaxClientConnections,
                                                  DNSClient,

                                                  DisableMaintenanceTasks,
                                                  MaintenanceInitialDelay,
                                                  MaintenanceEvery,

                                                  DisableWardenTasks,
                                                  WardenInitialDelay,
                                                  WardenCheckEvery,

                                                  LoggerFactory
                                              );

            this.ClientCertificateValidator = ClientCertificateValidator;

            #region The events of the listener, as this server's

            listener.OnTCPServerStarted         += async (tcpServer, timestamp, eventTrackingId, message) => {

                // However the listener was started: HTTP/3 beside it, where it is to be taken.
                listenerStarted = true;

                if (acceptHTTP3)
                    StartHTTP3();

                await LogEvent(
                          OnTCPServerStarted,
                          loggingDelegate => loggingDelegate.Invoke(
                                                 this,
                                                 timestamp,
                                                 eventTrackingId,
                                                 message
                                             )
                      );

                await LogEvent(
                          OnServerStarted,
                          loggingDelegate => loggingDelegate.Invoke(
                                                 timestamp,
                                                 this,
                                                 eventTrackingId,
                                                 CancellationToken.None
                                             )
                      );

            };

            listener.OnNewTCPConnectionRejected += (tcpServer, timestamp, eventTrackingId, remoteSocket, connectionId, reason) =>

                SendNewTCPConnectionRejected(
                    this,
                    timestamp,
                    eventTrackingId,
                    remoteSocket,
                    connectionId,
                    reason
                );

            listener.OnTCPConnectionFailed      += (tcpServer, timestamp, eventTrackingId, remoteSocket, connectionId, exception) =>

                LogEvent(
                    OnTCPConnectionFailed,
                    loggingDelegate => loggingDelegate.Invoke(
                                           this,
                                           timestamp,
                                           eventTrackingId,
                                           remoteSocket,
                                           connectionId,
                                           exception
                                       )
                );

            listener.OnTCPServerStopped         += async (tcpServer, timestamp, eventTrackingId, message) => {

                await LogEvent(
                          OnTCPServerStopped,
                          loggingDelegate => loggingDelegate.Invoke(
                                                 this,
                                                 timestamp,
                                                 eventTrackingId,
                                                 message
                                             )
                      );

                await LogEvent(
                          OnServerStopped,
                          loggingDelegate => loggingDelegate.Invoke(
                                                 timestamp,
                                                 this,
                                                 eventTrackingId,
                                                 message,
                                                 CancellationToken.None
                                             )
                      );

            };

            #endregion

            if (AutoStart ?? false)
                Start().GetAwaiter().GetResult();

        }

        #endregion


        #region Start(EventTrackingId = null)

        /// <summary>
        /// Start listening.
        /// </summary>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        public async Task Start(EventTracking_Id? EventTrackingId = null)
        {

            await listener.Start(EventTrackingId).ConfigureAwait(false);

            listenerStarted = true;

            if (AcceptHTTP3)
                StartHTTP3();

        }

        #endregion

        #region Stop (EventTrackingId = null, Message = null)

        /// <summary>
        /// Stop listening and close all connections, without telling their clients;
        /// Shutdown() tells them first.
        /// </summary>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="Message">An optional message to include in the stopped events.</param>
        public async Task Stop(EventTracking_Id?  EventTrackingId   = null,
                               String?            Message           = null)
        {

            listenerStarted = false;

            await StopHTTP3().ConfigureAwait(false);

            await listener.Stop(EventTrackingId, Message).ConfigureAwait(false);

        }

        #endregion

        #region RestartHTTP3Async(CancellationToken = default)

        /// <summary>
        /// Stop taking WebSockets over HTTP/3 and start again.
        /// </summary>
        /// <remarks>
        /// Not needed for a certificate that was replaced: QUIC asks the
        /// selector of this server for every connection, as TCP asks at every
        /// handshake, and the next client over HTTP/3 is shown the new one
        /// (QUICCertificates). A restart drops the connections over HTTP/3 as
        /// well, which then are shown it at once.
        ///
        /// The new QUIC listener binds the UDP port only after the old one has
        /// let go of it. Switching <see cref="AcceptHTTP3"/> off and on does
        /// not wait for that, as a property cannot, and the start right after
        /// it may find the port still taken.
        ///
        /// The connections over HTTP/3 are dropped, and their clients connect
        /// again; the connections over TCP are not touched. Nothing is started
        /// where the server is not running or is not to take HTTP/3.
        /// </remarks>
        /// <param name="CancellationToken">Gives up waiting for the old listener to let go of its port: HTTP/3 is not started again then.</param>
        /// <returns>Whether the server takes WebSockets over HTTP/3 afterwards; where it does not, <see cref="WhyNoHTTP3"/> says why.</returns>
        public async Task<Boolean> RestartHTTP3Async(CancellationToken CancellationToken = default)
        {

            await StopHTTP3().WaitAsync(CancellationToken).ConfigureAwait(false);

            if (listenerStarted || IsRunning)
                StartHTTP3();

            return http3Server is not null;

        }

        #endregion

        #region (protected override) StopAcceptingAsync()

        /// <summary>
        /// Stop the listener, after Shutdown() said goodbye to the open connections.
        /// </summary>
        protected override Task StopAcceptingAsync()

            => Stop();

        #endregion

        #region (protected override) AlternativeServicesAnnounced

        /// <summary>
        /// HTTP/3 on the UDP port of this server, while it takes WebSockets
        /// over it: <c>h3=":port"</c>, for a day. A server reached through
        /// the listener of another announces what that one announces: its
        /// WebSockets came in on that one's ports.
        /// </summary>
        protected override AlternativeServices? AlternativeServicesAnnounced

            => routedBy is { } listening
                   ? listening.AlternativeServicesAnnounced
                   : http3Server is { } server
                         ? AlternativeServices.Of(
                               new HTTPAlternativeService(
                                   "h3",
                                   "",
                                   server.Port,
                                   HTTPAlternativeService.DefaultMaxAge,
                                   false
                               )
                           )
                         : null;

        #endregion


        #region RouteBySubprotocol(params Servers)

        /// <summary>
        /// Hand the WebSockets the listener of this server takes to the given
        /// servers as well, each by the subprotocol it speaks: one port for
        /// several servers, each with its own handshake, authentication,
        /// connections and messages.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Which server a WebSocket goes to is decided by the subprotocols the
        /// client offers, in its order: the first it offers that this server
        /// or one of the given speaks (see <see cref="AWebSocketEndpoint.SecWebSocketProtocols"/>)
        /// chooses, this server first where both speak it - as an SSH server
        /// routed by subprotocol chooses. A WebSocket that offers nothing any
        /// of them speaks stays with this server, which turns it away as it
        /// always did.
        /// </para>
        /// <para>
        /// Decided before the handshake, on the request alone: the server
        /// chosen validates the upgrade, authenticates the client, keeps the
        /// connection and is told its messages and its close, as if it had
        /// accepted it itself. What is decided before the request - the TCP
        /// connection, TLS and the client certificate, HTTP/2 and HTTP/3 - is
        /// this server's, whose listener it is: the certificate a client shows
        /// has to be one this server's validator believes, for every
        /// subprotocol it routes.
        /// </para>
        /// <para>
        /// A server routed to is not started: it has no listener of its own to
        /// start, and is given its WebSockets by this one alone.
        /// </para>
        /// </remarks>
        /// <param name="Servers">The servers to hand WebSockets to as well.</param>
        public void RouteBySubprotocol(params AWebSocketServer[] Servers)
        {

            lock (routingLock)
            {

                if (routedBy is not null)
                    throw new InvalidOperationException("A WebSocket server handed WebSockets by another's listener has no listener to hand them on with.");

                foreach (var server in Servers)
                {

                    if (ReferenceEquals(server, this) || routedServers.Contains(server))
                        continue;

                    if (server.routedBy is AWebSocketServer other && !ReferenceEquals(other, this))
                        throw new ArgumentException("A WebSocket server is reached through one listener, and this one is reached through another's already.", nameof(Servers));

                    if (server.routedServers.Length > 0)
                        throw new ArgumentException("A WebSocket server that hands its WebSockets to others is not handed WebSockets itself.", nameof(Servers));

                    server.routedBy  = this;
                    routedServers    = routedServers.Add(server);

                }
            }

        }

        #endregion

        #region StopRouting(Server)

        /// <summary>
        /// Hand no more WebSockets to the given server. Those it was handed
        /// already stay its own until they close.
        /// </summary>
        /// <param name="Server">A server WebSockets were handed to.</param>
        public Boolean StopRouting(AWebSocketServer Server)
        {

            lock (routingLock)
            {

                if (!routedServers.Contains(Server))
                    return false;

                routedServers    = routedServers.Remove(Server);
                Server.routedBy  = null;

                return true;

            }

        }

        #endregion

        #region (private) ServerFor(UpgradeRequest)

        /// <summary>
        /// The server the WebSocket that asks with the given upgrade request -
        /// as it is written over HTTP/1.1 - goes to: see <see cref="RouteBySubprotocol"/>.
        /// </summary>
        private AWebSocketServer ServerFor(Byte[] UpgradeRequest)
        {

            var routed = routedServers;

            if (routed.IsEmpty ||
                !HTTPRequest.TryParse(UpgradeRequest, out var request))
                return this;

            foreach (var offered in request.SecWebSocketProtocol)
            {

                if (SecWebSocketProtocols.Contains(offered))
                    return this;

                foreach (var server in routed)
                    if (server.SecWebSocketProtocols.Contains(offered))
                        return server;

            }

            return this;

        }

        #endregion

        #region (private) ReadTheUpgradeRequest(Stream, CancellationToken)

        /// <summary>
        /// What a client sent on a TCP connection up to and with the empty
        /// line that ends the header of its request - all of it, where it
        /// stopped before, sent more than a handshake may be, or did not
        /// finish within the time a handshake has.
        /// </summary>
        /// <remarks>
        /// Read only where WebSockets are routed by their subprotocol, which
        /// is said in the request: everything read is handed on in front of
        /// the stream, and the server it goes to reads it as if it had been
        /// the first to, with its own limits.
        /// </remarks>
        private async Task<Byte[]> ReadTheUpgradeRequest(Stream             Stream,
                                                         CancellationToken  CancellationToken)
        {

            var read    = new MemoryStream();
            var buffer  = new Byte[4096];

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            if (HandshakeTimeout > TimeSpan.Zero)
                deadline.CancelAfter(HandshakeTimeout);

            try
            {

                while (true)
                {

                    var count = await Stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false);

                    if (count == 0)
                        break;

                    read.Write(buffer, 0, count);

                    if (EndsTheHeader(read) ||
                        MaxHandshakeRequestSize > 0 && (UInt64) read.Length > MaxHandshakeRequestSize)
                        break;

                }

            }
            catch (OperationCanceledException)
            { }
            catch (IOException)
            { }

            return read.ToArray();

            static Boolean EndsTheHeader(MemoryStream Read)
            {

                var bytes = Read.GetBuffer().AsSpan(0, (Int32) Read.Length);

                return bytes.IndexOf("\r\n\r\n"u8) >= 0;

            }

        }

        #endregion

        #region (private) StartHTTP3()

        /// <summary>
        /// Take WebSockets over HTTP/3 beside the listener: see AcceptHTTP3.
        /// Whatever keeps this from happening is logged, and the server runs
        /// without.
        /// </summary>
        private void StartHTTP3()
        {
            lock (http3Lock)
            {
                StartHTTP3Locked();
            }
        }

        private void StartHTTP3Locked()
        {

            // Switched off since it was to be started: the listener tells of its
            // start in its own time, and a server that was told meanwhile not to
            // take HTTP/3 takes none.
            if (http3Server is not null || !acceptHTTP3)
                return;

            WhyNoHTTP3 = null;

            // A server has TLS by either of the two: a certificate, or a
            // certificate with the chain that leads from it towards its root.
            if (ServerCertificateSelector is null && serverCertificateChainSelector is null)
            {
                WhyNoHTTP3 = "The server has no TLS, and HTTP/3 is HTTP over TLS and nothing else.";
                Logger.LogDebug("The WebSocket server on {IPSocket} has no TLS, and so takes no WebSockets over HTTP/3.", IPSocket);
                return;
            }

            // Asked of the selector for every connection, as TCP asks at every
            // handshake - and once here: a server that has nothing to show
            // over QUIC takes no HTTP/3 at all.
            var certificates = new QUICCertificates(

                                   nobody => {

                                       // The chain where the server has one, as its listener does: a
                                       // client that knows the root alone needs the intermediates
                                       // over QUIC as it needs them over TCP.
                                       if (serverCertificateChainSelector is { } chainSelector)
                                       {

                                           var chain = chainSelector(this, nobody)
                                                           ?? throw new InvalidOperationException("The server has no certificate chain to show.");

                                           return (chain.Certificate, chain.Intermediates);

                                       }

                                       return (ServerCertificateSelector!(this, nobody), null);

                                   },

                                   RawPublicKeys,

                                   e => Logger.LogWarning(e, "The WebSocket server on {IPSocket} cannot say which certificate to show over HTTP/3, and shows the one it showed before.", IPSocket)

                               );

            try
            {
                certificates.ForTheNextConnection();
            }
            catch (Exception e)
            {
                WhyNoHTTP3 = $"The certificate of the server cannot be shown over QUIC: {e.Message}";
                Logger.LogWarning(e, "The WebSocket server on {IPSocket} takes no WebSockets over HTTP/3: its certificate cannot be shown over QUIC.", IPSocket);
                certificates.Dispose();
                return;
            }

            // A client certificate is asked for, and judged by the validator this
            // server has when it is shown: without one, by the roots of the
            // operating system.
            //
            // As over TCP, where "required" asks for a certificate and leaves
            // a client that shows none to the validator: a server of OCPP lets
            // a station in with a password and no certificate on the port it
            // lets another in with its certificate alone. So the handshake of
            // QUIC insists only where there is no validator to ask, and what
            // a validator says of a client without a certificate is asked
            // where its WebSocket is: HandleHTTP3Connect.
            var clientCertificate  = new ClientCertificateOptions {
                                         Mode        = ClientCertificateRequired && clientCertificateValidator is null
                                                           ? ClientCertificateMode.Require
                                                           : ClientCertificateMode.Request,
                                         Validation  = new CertificateValidationOptions {
                                                           VerifyHostname  = false,
                                                           Judge           = (clientCert, chain, policyErrors) =>
                                                                                 clientCertificateValidator is { } validator
                                                                                     ? validator(this, clientCert, chain, this, policyErrors).IsValid
                                                                                     : policyErrors == SslPolicyErrors.None
                                                       }
                                     };

            var cancellation       = new CancellationTokenSource();
            var port               = IPSocket.Port;

            // Where the listener listens: "localhost" is both loopback addresses, and
            // any address of both families is one socket of both (Http3Server).
            System.Net.IPAddress[] localAddresses = IPAddress.IsIPv4 && IPAddress.IsIPv6 && IPAddress.IsLocalhost
                                                        ? [ System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback ]
                                                        : [ IPAddress.ToDotNet() ];

            Http3Server ServerOn(System.Net.IPAddress localAddress)

                                => new Http3Server(
                                         port,
                                         (remoteEndPoint, validatedRetry) => {

                                             // One WebSocket at a time: see AcceptHTTP3.
                                             var webSockets = new StrongBox<Int32>(0);

                                             Http3ServerConnection? connection = null;

                                             connection = new Http3ServerConnection(

                                                              certificates.ForTheNextConnection(),

                                                              request => new Http3Response {
                                                                             Status   = 400,
                                                                             Headers  = [ new HeaderField("content-type", "text/plain; charset=utf-8") ],
                                                                             Body     = "This is a WebSocket server: over HTTP/3 a WebSocket is opened by an extended CONNECT with ':protocol = websocket' (RFC 9220).".ToUTF8Bytes()
                                                                         },

                                                              validatedRetry:     validatedRetry,
                                                              clientCertificate:  clientCertificate

                                                          ) {

                                                              ConnectHandlerAsync = (request, cancellationToken) =>
                                                                                        HandleHTTP3Connect(
                                                                                            connection!,
                                                                                            IPSocket,
                                                                                            IPSocket.FromIPEndPoint(remoteEndPoint),
                                                                                            webSockets,
                                                                                            request,
                                                                                            cancellation.Token,
                                                                                            cancellationToken
                                                                                        )

                                                          };

                                             return connection;

                                         },
                                         localAddress
                                     );

            var        started  = new List<Http3Server>();
            Exception? failure  = null;

            foreach (var localAddress in localAddresses)
            {

                var server = ServerOn(localAddress);

                try
                {
                    server.Start();
                    started.Add(server);
                }
                catch (Exception e)
                {
                    // One family the machine does not have, or a port that is taken there.
                    failure = e;
                    Logger.LogDebug(e, "The WebSocket server on {IPSocket} does not listen for HTTP/3 on {Address}, UDP port {Port}.", IPSocket, localAddress, port);
                    _ = server.DisposeAsync();
                }

            }

            if (started.Count == 0)
            {
                WhyNoHTTP3 = $"The UDP port {port} cannot be listened on: {failure?.Message}";
                Logger.LogWarning(failure, "The WebSocket server on {IPSocket} takes no WebSockets over HTTP/3: its UDP port {Port} cannot be listened on.", IPSocket, port);
                cancellation.Dispose();
                certificates.Dispose();
                return;
            }

            http3Certificates  = certificates;
            http3Cancellation  = cancellation;
            http3MoreServers   = started[1..];
            http3Server        = started[0];

        }

        #endregion

        #region (private) StopHTTP3()

        /// <summary>
        /// Stop taking WebSockets over HTTP/3: done once the UDP ports are let
        /// go of - also where another stop let go of them, and is still at it.
        /// </summary>
        private Task StopHTTP3()
        {

            Http3Server?              server;
            CancellationTokenSource?  cancellation;
            QUICCertificates?         certificates;
            List<Http3Server>         more;
            TaskCompletionSource      stopped;

            lock (http3Lock)
            {

                server             = http3Server;
                cancellation       = http3Cancellation;
                certificates       = http3Certificates;
                more               = http3MoreServers;
                http3Server        = null;
                http3Cancellation  = null;
                http3Certificates  = null;
                http3MoreServers   = [];
                WhyNoHTTP3         = null;

                if (server is null)
                    return http3Stopped;

                stopped            = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                http3Stopped       = stopped.Task;

            }

            return LetGoOfHTTP3(server, more, cancellation, certificates, stopped);

        }

        /// <summary>
        /// Stop the HTTP/3 servers taken from this one, outside of the lock:
        /// what their cancellation ends runs at once, on this thread.
        /// </summary>
        private async Task LetGoOfHTTP3(Http3Server               server,
                                        List<Http3Server>         more,
                                        CancellationTokenSource?  cancellation,
                                        QUICCertificates?         certificates,
                                        TaskCompletionSource      stopped)
        {

            try
            {
                await StopHTTP3Servers(server, more, cancellation, certificates).ConfigureAwait(false);
            }
            finally
            {
                stopped.TrySetResult();
            }

        }

        private async Task StopHTTP3Servers(Http3Server               server,
                                            List<Http3Server>         more,
                                            CancellationTokenSource?  cancellation,
                                            QUICCertificates?         certificates)
        {

            try
            {
                cancellation?.Cancel();
            }
            catch
            { }

            foreach (var one in (Http3Server[]) [ server, .. more ])
            {
                try
                {
                    await one.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.LogDebug(e, "Stopping the HTTP/3 server of the WebSocket server on {IPSocket} failed.", IPSocket);
                }
            }

            cancellation?.Dispose();
            certificates?.Dispose();

        }

        #endregion

        #region (private) HandleHTTP3Connect(Connection, LocalSocket, RemoteSocket, WebSockets, Request, ServerToken, RequestToken)

        /// <summary>
        /// Answer an extended CONNECT over HTTP/3: a WebSocket where it asks
        /// for one, by the handshake every WebSocket of this server has.
        /// </summary>
        private async Task<Http3ConnectResult> HandleHTTP3Connect(Http3ServerConnection  Connection,
                                                                  IPSocket               LocalSocket,
                                                                  IPSocket               RemoteSocket,
                                                                  StrongBox<Int32>       WebSockets,
                                                                  Http3Request           Request,
                                                                  CancellationToken      ServerToken,
                                                                  CancellationToken      RequestToken)
        {

            // RFC 9220 with RFC 8441, Section 4: a protocol the server does not know.
            if (Request.Protocol != WebSocketOverHTTP2.Protocol)
                return new Http3ConnectResult { Status = 501 };

            List<(String Name, String Value)> connectHeaders = [
                                                                   (":authority", Request.Authority),
                                                                   (":path",      Request.Path),
                                                                   .. Request.AdditionalHeaders.Select(field => (field.Name, field.Value))
                                                               ];

            if (!WebSocketOverHTTP2.TryUpgradeRequestOf(connectHeaders, out var requestBytes, out var error))
            {
                Logger.LogDebug("Refusing the WebSocket over HTTP/3 of {RemoteSocket}: {Error}", RemoteSocket, error);
                return new Http3ConnectResult { Status = 400 };
            }

            // A client certificate that was required: one that was shown and
            // not believed opens nothing, as a handshake over TCP would have
            // failed with it; and whether a client that showed none may come
            // is the validator's to say, as it is over TCP.
            if (ClientCertificateRequired && Connection.ClientAuthentication.Certificate is null)
            {

                var believed = Connection.ClientAuthentication.FailureReason is null &&
                               clientCertificateValidator is { } validator &&
                               validator(this, null, null, this, SslPolicyErrors.RemoteCertificateNotAvailable).IsValid;

                if (!believed)
                {
                    Logger.LogDebug("Refusing the WebSocket over HTTP/3 of {RemoteSocket}: {Reason}",
                                    RemoteSocket,
                                    Connection.ClientAuthentication.FailureReason ?? "it showed no client certificate, and one is required.");
                    return new Http3ConnectResult { Status = 403 };
                }

            }

            if (Interlocked.CompareExchange(ref WebSockets.Value, 1, 0) != 0)
                return new Http3ConnectResult { Status = 503 };

            // What a one-time password bound to this connection is verified
            // with: asked here, where the connection is being worked on, and
            // before anything is awaited. QUIC's TLS exports it; without it a
            // bound password is refused (AuthenticateAsync).
            Byte[]? totpExporterMaterial = null;

            try
            {
                totpExporterMaterial = Connection.ExportKeyingMaterial(
                                           TOTPGenerator.TLSExporterLabel,
                                           ReadOnlySpan<Byte>.Empty,
                                           TOTPGenerator.TLSExporterLength
                                       );
            }
            catch (Exception e)
            {
                Logger.LogDebug(e, "No TLS exporter material of the HTTP/3 connection of {RemoteSocket}: a one-time password bound to it will be refused.", RemoteSocket);
            }

            var bridge = new WebSocketOverHTTP2ServerStream();

            // The server routed to by its subprotocol, where there is one.
            var server = ServerFor(requestBytes);

            // The handshake and the frame loop every WebSocket of this
            // server has, on the request the CONNECT stands for.
            _ = server.RunConnectionAsync(
                    new WebSocketServerConnection(

                        WebSocketServer:              server,
                        NetworkStream:                new PrefixedStream(requestBytes, bridge),
                        LocalSocket:                  LocalSocket,
                        RemoteSocket:                 RemoteSocket,
                        ClientCertificate:            Connection.ClientAuthentication.Certificate,

                        HTTPRequest:                  null,
                        HTTPResponse:                 null,

                        MaxTextMessageSizeIn:         server.MaxTextMessageSizeIn,
                        MaxTextMessageSizeOut:        server.MaxTextMessageSizeOut,
                        MaxTextFragmentLengthIn:      server.MaxTextFragmentLengthIn,
                        MaxTextFragmentLengthOut:     server.MaxTextFragmentLengthOut,

                        MaxBinaryMessageSizeIn:       server.MaxBinaryMessageSizeIn,
                        MaxBinaryMessageSizeOut:      server.MaxBinaryMessageSizeOut,
                        MaxBinaryFragmentLengthIn:    server.MaxBinaryFragmentLengthIn,
                        MaxBinaryFragmentLengthOut:   server.MaxBinaryFragmentLengthOut,

                        SlowNetworkSimulationDelay:   server.SlowNetworkSimulationDelay

                    ) {
                        HTTPVersion              = HTTPVersion.HTTP_3_0,
                        TOTPTLSExporterMaterial  = totpExporterMaterial,
                        ClientKey                = Connection.ClientAuthentication.Key
                    },
                    ServerToken
                ).ContinueWith(task => {
                    _ = task.Exception;
                    bridge.Dispose();
                    Interlocked.Exchange(ref WebSockets.Value, 0);
                }, TaskScheduler.Default);

            try
            {

                var (status, answerHeaders) = await bridge.Answer.WaitAsync(RequestToken).ConfigureAwait(false);

                List<HeaderField> answerFields = [.. WebSocketOverHTTP2.FieldsOverHTTP2(answerHeaders).
                                                         Select(field => new HeaderField(field.Name, field.Value))];

                // RFC 9220 with RFC 8441, Section 5: accepted with a 2xx, where the Upgrade is with a 101.
                if (status == 101)
                    return new Http3ConnectResult {
                               Status    = 200,
                               Headers   = answerFields,
                               OnTunnel  = tunnel => {
                                               // Until the server closes the bridge, or stops: then the
                                               // tunnel ends as a TCP connection is closed (RFC 9220, Section 3).
                                               _ = bridge.RunAsync(new WebSocketOverHTTP3Tunnel(tunnel), ServerToken).
                                                          ContinueWith(task => {
                                                              _ = task.Exception;
                                                              try
                                                              {
                                                                  tunnel.Complete();
                                                              }
                                                              catch
                                                              { }
                                                          }, TaskScheduler.Default);
                                           }
                           };

                bridge.Dispose();

                return new Http3ConnectResult {
                           Status   = status,
                           Headers  = answerFields
                       };

            }
            catch (Exception e)
            {
                // Closed without an answer, or called off.
                Logger.LogDebug(e, "The WebSocket over HTTP/3 of {RemoteSocket} was not answered.", RemoteSocket);
                bridge.Dispose();
                return new Http3ConnectResult { Status = 503 };
            }

        }

        #endregion

        #region (private) HandleConnection(Connection, CancellationToken)

        /// <summary>
        /// Speak WebSocket on a TCP/TLS connection the listener accepted.
        /// </summary>
        private async Task HandleConnection(TCPConnection      Connection,
                                            CancellationToken  CancellationToken)
        {

            if (AcceptHTTP2)
            {

                // Over TLS, whichever TLS stack runs it: what ALPN came to.
                if (Connection.TLSStream is Stream tlsStream)
                {
                    if (Connection.TLSSession?.ApplicationProtocol == SslApplicationProtocol.Http2.ToString())
                    {
                        await HandleHTTP2Connection(Connection, tlsStream, CancellationToken).ConfigureAwait(false);
                        return;
                    }
                }

                else if (await StartsWithTheHTTP2Preface(Connection, CancellationToken).ConfigureAwait(false))
                {
                    await HandleHTTP2Connection(Connection, Connection.NetworkStream, CancellationToken).ConfigureAwait(false);
                    return;
                }

            }

            // Where WebSockets are routed by their subprotocol, the request
            // that says it is read first, and handed on in front of the stream
            // to the server it chooses.
            if (!routedServers.IsEmpty)
            {

                var stream          = Connection.TLSStream ?? Connection.NetworkStream;
                var upgradeRequest  = await ReadTheUpgradeRequest(stream, CancellationToken).ConfigureAwait(false);
                var server          = ServerFor(upgradeRequest);

                await server.RunConnectionAsync(
                          new WebSocketServerConnection(

                              WebSocketServer:              server,
                              TcpClient:                    Connection.TCPClient,
                              TLSStream:                    new PrefixedStream(upgradeRequest, stream),
                              ClientCertificate:            Connection.ClientCertificate,

                              HTTPRequest:                  null,
                              HTTPResponse:                 null,

                              MaxTextMessageSizeIn:         server.MaxTextMessageSizeIn,
                              MaxTextMessageSizeOut:        server.MaxTextMessageSizeOut,
                              MaxTextFragmentLengthIn:      server.MaxTextFragmentLengthIn,
                              MaxTextFragmentLengthOut:     server.MaxTextFragmentLengthOut,

                              MaxBinaryMessageSizeIn:       server.MaxBinaryMessageSizeIn,
                              MaxBinaryMessageSizeOut:      server.MaxBinaryMessageSizeOut,
                              MaxBinaryFragmentLengthIn:    server.MaxBinaryFragmentLengthIn,
                              MaxBinaryFragmentLengthOut:   server.MaxBinaryFragmentLengthOut,

                              SlowNetworkSimulationDelay:   server.SlowNetworkSimulationDelay

                          ) {
                              TCPConnection = Connection,
                              ClientKey     = Connection.ClientKey
                          },
                          CancellationToken
                      ).ConfigureAwait(false);

                return;

            }

            await RunConnectionAsync(
                   new WebSocketServerConnection(

                       WebSocketServer:              this,
                       TcpClient:                    Connection.TCPClient,
                       TLSStream:                    Connection.TLSStream,
                       ClientCertificate:            Connection.ClientCertificate,

                       HTTPRequest:                  null,
                       HTTPResponse:                 null,

                       MaxTextMessageSizeIn:         MaxTextMessageSizeIn,
                       MaxTextMessageSizeOut:        MaxTextMessageSizeOut,
                       MaxTextFragmentLengthIn:      MaxTextFragmentLengthIn,
                       MaxTextFragmentLengthOut:     MaxTextFragmentLengthOut,

                       MaxBinaryMessageSizeIn:       MaxBinaryMessageSizeIn,
                       MaxBinaryMessageSizeOut:      MaxBinaryMessageSizeOut,
                       MaxBinaryFragmentLengthIn:    MaxBinaryFragmentLengthIn,
                       MaxBinaryFragmentLengthOut:   MaxBinaryFragmentLengthOut,

                       SlowNetworkSimulationDelay:   SlowNetworkSimulationDelay

                   ) {
                       TCPConnection = Connection,
                       ClientKey     = Connection.ClientKey
                   },
                   CancellationToken
               ).ConfigureAwait(false);

        }

        #endregion

        #region (private) StartsWithTheHTTP2Preface(Connection, CancellationToken)

        /// <summary>
        /// Whether what a client sent first on a connection without TLS is the
        /// beginning of the connection preface of HTTP/2, "PRI * HTTP/2.0"
        /// (RFC 9113, Section 3.4), and not of a request of HTTP/1.1. Looked
        /// at without taking it off the socket.
        /// </summary>
        private static async Task<Boolean> StartsWithTheHTTP2Preface(TCPConnection      Connection,
                                                                     CancellationToken  CancellationToken)
        {

            var preface  = "PRI "u8.ToArray();
            var seen     = new Byte[preface.Length];

            // A client that says nothing is the handshake's to wait for and to
            // give up on, as it always was.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {

                while (true)
                {

                    var read = await Connection.TCPClient.Client.ReceiveAsync(seen, SocketFlags.Peek, deadline.Token).ConfigureAwait(false);

                    if (read == 0)
                        return false;

                    for (var i = 0; i < read; i++)
                        if (seen[i] != preface[i])
                            return false;

                    if (read == preface.Length)
                        return true;

                    await Task.Delay(5, deadline.Token).ConfigureAwait(false);

                }

            }
            catch
            {
                return false;
            }

        }

        #endregion

        #region (private) HandleHTTP2Connection(Connection, Transport, CancellationToken)

        /// <summary>
        /// Speak HTTP/2 on a connection the listener accepted: an extended
        /// CONNECT for a WebSocket (RFC 8441) is an upgrade request to this
        /// server, and nothing else is served.
        /// </summary>
        private async Task HandleHTTP2Connection(TCPConnection      Connection,
                                                 Stream             Transport,
                                                 CancellationToken  CancellationToken)
        {

            // One WebSocket at a time: see AcceptHTTP2.
            var webSockets = 0;

            var http2Connection = new HTTP2Connection(

                                      Transport,

                                      RequestHandler:     (streamId, requestHeaders, requestBody, cancellationToken) =>
                                                              Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>((
                                                                  [ (":status",      "400"),
                                                                    ("content-type", "text/plain; charset=utf-8") ],
                                                                  "This is a WebSocket server: over HTTP/2 a WebSocket is opened by an extended CONNECT with ':protocol = websocket' (RFC 8441).".ToUTF8Bytes()
                                                              )),

                                      ConnectHandler:     async (streamId, connectHeaders, cancellationToken) => {

                                          var protocol = connectHeaders.FirstOrDefault(field => field.Name == ":protocol").Value;

                                          // A CONNECT to somewhere else: this is no proxy.
                                          if (protocol is null)
                                              return new HTTP2ConnectResult { StatusCode = 405 };

                                          // RFC 8441, Section 4: a protocol the server does not know.
                                          if (protocol != WebSocketOverHTTP2.Protocol)
                                              return new HTTP2ConnectResult { StatusCode = 501 };

                                          if (!WebSocketOverHTTP2.TryUpgradeRequestOf(connectHeaders, out var requestBytes, out var error))
                                          {
                                              Logger.LogDebug("Refusing the WebSocket over HTTP/2 of {RemoteSocket}: {Error}", Connection.RemoteSocket, error);
                                              return new HTTP2ConnectResult { StatusCode = 400 };
                                          }

                                          if (Interlocked.CompareExchange(ref webSockets, 1, 0) != 0)
                                              return new HTTP2ConnectResult { StatusCode = 503 };

                                          var bridge = new WebSocketOverHTTP2ServerStream();

                                          // The server routed to by its subprotocol, where there is one.
                                          var server = ServerFor(requestBytes);

                                          // The handshake and the frame loop every WebSocket of this
                                          // server has, on the request the CONNECT stands for.
                                          _ = server.RunConnectionAsync(
                                                  new WebSocketServerConnection(

                                                      WebSocketServer:              server,
                                                      NetworkStream:                new PrefixedStream(requestBytes, bridge),
                                                      LocalSocket:                  Connection.LocalSocket,
                                                      RemoteSocket:                 Connection.RemoteSocket,
                                                      ClientCertificate:            Connection.ClientCertificate,

                                                      HTTPRequest:                  null,
                                                      HTTPResponse:                 null,

                                                      MaxTextMessageSizeIn:         server.MaxTextMessageSizeIn,
                                                      MaxTextMessageSizeOut:        server.MaxTextMessageSizeOut,
                                                      MaxTextFragmentLengthIn:      server.MaxTextFragmentLengthIn,
                                                      MaxTextFragmentLengthOut:     server.MaxTextFragmentLengthOut,

                                                      MaxBinaryMessageSizeIn:       server.MaxBinaryMessageSizeIn,
                                                      MaxBinaryMessageSizeOut:      server.MaxBinaryMessageSizeOut,
                                                      MaxBinaryFragmentLengthIn:    server.MaxBinaryFragmentLengthIn,
                                                      MaxBinaryFragmentLengthOut:   server.MaxBinaryFragmentLengthOut,

                                                      SlowNetworkSimulationDelay:   server.SlowNetworkSimulationDelay

                                                  ) {
                                                      HTTPVersion = HTTPVersion.HTTP_2_0,
                                                      ClientKey   = Connection.ClientKey
                                                  },
                                                  CancellationToken
                                              ).ContinueWith(task => {
                                                  _ = task.Exception;
                                                  bridge.Dispose();
                                                  Interlocked.Exchange(ref webSockets, 0);
                                              }, TaskScheduler.Default);

                                          try
                                          {

                                              var (status, answerHeaders) = await bridge.Answer.WaitAsync(cancellationToken).ConfigureAwait(false);

                                              // RFC 8441, Section 5: accepted with a 2xx, where the Upgrade is with a 101.
                                              if (status == 101)
                                                  return new HTTP2ConnectResult {
                                                             StatusCode    = 200,
                                                             ExtraHeaders  = WebSocketOverHTTP2.FieldsOverHTTP2(answerHeaders),
                                                             RunAsync      = bridge.RunAsync
                                                         };

                                              bridge.Dispose();

                                              return new HTTP2ConnectResult {
                                                         StatusCode    = status,
                                                         ExtraHeaders  = WebSocketOverHTTP2.FieldsOverHTTP2(answerHeaders)
                                                     };

                                          }
                                          catch (Exception e)
                                          {
                                              // Closed without an answer, or called off.
                                              Logger.LogDebug(e, "The WebSocket over HTTP/2 of {RemoteSocket} was not answered.", Connection.RemoteSocket);
                                              bridge.Dispose();
                                              return new HTTP2ConnectResult { StatusCode = 503 };
                                          }

                                      },

                                      CancellationToken:  CancellationToken,
                                      ClientCertificate:  Connection.ClientCertificate

                                  );

            try
            {
                await http2Connection.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                // The socket is the listener's connection, and done with once
                // the HTTP/2 connection on it is.
                try
                {
                    Connection.TCPClient.Close();
                }
                catch
                { }
            }

        }

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Tell every client goodbye, stop listening, and dispose of the listener.
        /// </summary>
        public override async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref disposed, 1) == 1)
                return;

            await base.DisposeAsync();
            await listener.DisposeAsync();

            GC.SuppressFinalize(this);

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => listener.ToString();

        #endregion


        #region (private class) Listener

        /// <summary>
        /// The TCP listener of a WebSocket server: it accepts the connections, and the server speaks WebSocket on them.
        /// </summary>
        private sealed class Listener : ATCPServer
        {

            private readonly AWebSocketServer webSocketServer;

            /// <summary>
            /// Its connections, with their handlers.
            /// </summary>
            internal ConcurrentDictionary<TCPConnection, Task> ActiveClients
                => activeClients;

            public Listener(AWebSocketServer                    WebSocketServer,
                            IPAddress?                         IPAddress,
                            IPPort?                             TCPPort,
                            I18NString?                         Description,
                            TimeSpan?                           ReceiveTimeout,
                            TimeSpan?                           SendTimeout,
                            TCPEchoLoggingDelegate?             LoggingHandler,

                            ServerCertificateSelectorDelegate?  ServerCertificateSelector,
                            LocalCertificateSelectionHandler?   LocalCertificateSelector,
                            SslProtocols?                       AllowedTLSProtocols,
                            Boolean?                            ClientCertificateRequired,
                            Boolean?                            CheckCertificateRevocation,

                            ConnectionIdBuilder                 ConnectionIdBuilder,
                            UInt32?                             MaxClientConnections,
                            IDNSClient?                         DNSClient,

                            Boolean?                            DisableMaintenanceTasks,
                            TimeSpan?                           MaintenanceInitialDelay,
                            TimeSpan?                           MaintenanceEvery,

                            Boolean?                            DisableWardenTasks,
                            TimeSpan?                           WardenInitialDelay,
                            TimeSpan?                           WardenCheckEvery,

                            ILoggerFactory?                     LoggerFactory)

                : base(IPAddress,
                       TCPPort,
                       Description,
                       ReceiveTimeout,
                       SendTimeout,
                       LoggingHandler,

                       ServerCertificateSelector,
                       null,
                       LocalCertificateSelector,
                       AllowedTLSProtocols,
                       ClientCertificateRequired,
                       CheckCertificateRevocation,

                       ConnectionIdBuilder,
                       MaxClientConnections,
                       DNSClient,

                       DisableMaintenanceTasks,
                       MaintenanceInitialDelay,
                       MaintenanceEvery,

                       DisableWardenTasks,
                       WardenInitialDelay,
                       WardenCheckEvery,

                       LoggerFactory,
                       AutoStart: false)

            {
                this.webSocketServer = WebSocketServer;
            }

            /// <summary>
            /// Asked by the server, with the server as the TCP server: its handlers of OnValidateTCPConnection decide.
            /// </summary>
            public override Task<ConnectionFilterResponse> ValidateConnection(DateTimeOffset     Timestamp,
                                                                             ITCPServer         Server,
                                                                             TCPConnection      Connection,
                                                                             EventTracking_Id   EventTrackingId,
                                                                             CancellationToken  CancellationToken)

                => webSocketServer.ValidateConnection(Timestamp,
                                                      webSocketServer,
                                                      Connection,
                                                      EventTrackingId,
                                                      CancellationToken);

            protected override Task HandleConnection(TCPConnection      Connection,
                                                     CancellationToken  CancellationToken)

                => webSocketServer.HandleConnection(Connection, CancellationToken);

            /// <summary>
            /// "h2" before "http/1.1" where the server takes WebSockets over
            /// HTTP/2, and nothing otherwise, as before.
            /// </summary>
            public override IEnumerable<SslApplicationProtocol> TLSApplicationProtocols
                => webSocketServer.AcceptHTTP2
                       ? [ SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 ]
                       : [];

        }

        #endregion

    }

}
