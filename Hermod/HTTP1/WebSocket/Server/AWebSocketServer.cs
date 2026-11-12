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
using System.Security.Authentication;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
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
        private          Int32                                                     disposed;

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
        public IIPAddress                                               IPAddress
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
        public AWebSocketServer(IIPAddress?                                               IPAddress                    = null,
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
        public Task Start(EventTracking_Id? EventTrackingId = null)

            => listener.Start(EventTrackingId);

        #endregion

        #region Stop (EventTrackingId = null, Message = null)

        /// <summary>
        /// Stop listening and close all connections, without telling their clients;
        /// Shutdown() tells them first.
        /// </summary>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="Message">An optional message to include in the stopped events.</param>
        public Task Stop(EventTracking_Id?  EventTrackingId   = null,
                         String?            Message           = null)

            => listener.Stop(EventTrackingId, Message);

        #endregion

        #region (protected override) StopAcceptingAsync()

        /// <summary>
        /// Stop the listener, after Shutdown() said goodbye to the open connections.
        /// </summary>
        protected override Task StopAcceptingAsync()

            => Stop();

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

                if (Connection.SSLStream is SslStream tlsStream)
                {
                    if (tlsStream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2)
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

            await RunConnectionAsync(
                   new WebSocketServerConnection(

                       WebSocketServer:              this,
                       TcpClient:                    Connection.TCPClient,
                       TLSStream:                    Connection.SSLStream,
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
                       TCPConnection = Connection
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

                                          // The handshake and the frame loop every WebSocket of this
                                          // server has, on the request the CONNECT stands for.
                                          _ = RunConnectionAsync(
                                                  new WebSocketServerConnection(

                                                      WebSocketServer:              this,
                                                      NetworkStream:                new PrefixedStream(requestBytes, bridge),
                                                      LocalSocket:                  Connection.LocalSocket,
                                                      RemoteSocket:                 Connection.RemoteSocket,
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
                                                      HTTPVersion = HTTPVersion.HTTP_2_0
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
                            IIPAddress?                         IPAddress,
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
