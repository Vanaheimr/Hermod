/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text;
using System.Net.Sockets;
using System.Collections.Concurrent;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A HTTP web socket server.
    /// </summary>
    public class WebSocketServer
    {

        #region Data

        private readonly ConcurrentDictionary<IPSocket, WebSocketConnection>  webSocketConnections;

        private          Thread?                                              listenerThread;

        private readonly CancellationTokenSource                              cancellationTokenSource;

        private volatile Boolean                                              isRunning                   = false;

        private const    String                                               LogfileName                 = "HTTPWebSocketServer.log";

        public  readonly TimeSpan                                             DefaultWebSocketPingEvery   = TimeSpan.FromSeconds(30);

        #endregion

        #region Properties

        /// <summary>
        /// Return an enumeration of all currently connected HTTP web socket connections.
        /// </summary>
        public IEnumerable<WebSocketConnection>  WebSocketConnections
            => webSocketConnections.Values.ToArray();

        /// <summary>
        /// The HTTP service name.
        /// </summary>
        public String                            HTTPServiceName               { get; }


        public ServerThreadNameCreatorDelegate?  ServerThreadNameCreator       { get; }

        public ThreadPriority                    ServerThreadPriority          { get; }

        public Boolean                           ServerThreadIsBackground      { get; }

        /// <summary>
        /// The IP address to listen on.
        /// </summary>
        public IIPAddress                        IPAddress
            => IPSocket.IPAddress;

        /// <summary>
        /// The TCP port to listen on.
        /// </summary>
        public IPPort                            IPPort
            => IPSocket.Port;

        /// <summary>
        /// The IP socket to listen on.
        /// </summary>
        public IPSocket                          IPSocket                      { get; }

        /// <summary>
        /// Whether the web socket TCP listener is currently running.
        /// </summary>
        public Boolean                           IsRunning
            => isRunning;

        /// <summary>
        /// Disable web socket pings.
        /// </summary>
        public Boolean                           DisableWebSocketPings         { get; set; }

        /// <summary>
        /// The web socket ping interval.
        /// </summary>
        public TimeSpan                          WebSocketPingEvery            { get; set; }

        /// <summary>
        /// An additional delay between sending each byte to the networking stack.
        /// This is intended for debugging other web socket stacks.
        /// </summary>
        public TimeSpan?                         SlowNetworkSimulationDelay    { get; set; }


        /// <summary>
        /// An optional DNS client to use.
        /// </summary>
        public DNSClient?                        DNSClient                     { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event sent whenever the HTTP web socket server started.
        /// </summary>
        public event OnServerStartedDelegate?                     OnServerStarted;

        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        public event OnNewTCPConnectionDelegate?                  OnNewTCPConnection;

        /// <summary>
        /// An event sent whenever a HTTP request was received.
        /// </summary>
        public event HTTPRequestLogDelegate?                      OnHTTPRequest;

        /// <summary>
        /// An event sent whenever the HTTP headers of a new web socket connection
        /// need to be validated or filtered by an upper layer application logic.
        /// </summary>
        public event OnOnValidateWebSocketConnectionDelegate?     OnValidateWebSocketConnection;

        /// <summary>
        /// An event sent whenever the HTTP connection switched successfully to web socket.
        /// </summary>
        public event OnNewWebSocketConnectionDelegate?            OnNewWebSocketConnection;

        /// <summary>
        /// An event sent whenever a reponse to a HTTP request was sent.
        /// </summary>
        public event HTTPResponseLogDelegate?                     OnHTTPResponse;


        /// <summary>
        /// An event sent whenever a web socket frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?                    OnWebSocketFrameReceived;

        /// <summary>
        /// An event sent whenever the response to a web socket frame was sent.
        /// </summary>
        public event OnWebSocketResponseFrameDelegate?            OnWebSocketFrameResponse;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketFrameDelegate?                    OnWebSocketFrameSent;


        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketTextMessageRequestDelegate?       OnTextMessageRequest;

        /// <summary>
        /// An event sent whenever the response to a text message was sent.
        /// </summary>
        public event OnWebSocketTextMessageResponseDelegate?      OnTextMessageResponse;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketTextMessageRequestDelegate?       OnTextMessageSent;


        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketBinaryMessageRequestDelegate?     OnBinaryMessageRequest;

        /// <summary>
        /// An event sent whenever the response to a binary message was sent.
        /// </summary>
        public event OnWebSocketBinaryMessageResponseDelegate?    OnBinaryMessageResponse;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketBinaryMessageRequestDelegate?     OnBinaryMessageSent;


        /// <summary>
        /// An event sent whenever a web socket ping frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?                    OnPingMessageReceived;

        /// <summary>
        /// An event sent whenever a web socket ping frame was sent.
        /// </summary>
        public event OnWebSocketFrameDelegate?                    OnPingMessageSent;

        /// <summary>
        /// An event sent whenever a web socket pong frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?                    OnPongMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket close frame was received.
        /// </summary>
        public event OnCloseMessageDelegate?                      OnCloseMessageReceived;


        /// <summary>
        /// An event sent whenever a TCP connection was closed.
        /// </summary>
        public event OnTCPConnectionClosedDelegate?               OnTCPConnectionClosed;

        #endregion

        #region Constructor(s)

        #region WebSocketServer(IPAddress = null, TCPPort = null, HTTPServiceName = null, ..., Autostart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="TCPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="Autostart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketServer(IIPAddress?                       IPAddress                    = null,
                               IPPort?                           TCPPort                      = null,
                               String?                           HTTPServiceName              = null,
                               ServerThreadNameCreatorDelegate?  ServerThreadNameCreator      = null,
                               ThreadPriority?                   ServerThreadPriority         = null,
                               Boolean?                          ServerThreadIsBackground     = null,

                               Boolean                           DisableWebSocketPings        = false,
                               TimeSpan?                         WebSocketPingEvery           = null,
                               TimeSpan?                         SlowNetworkSimulationDelay   = null,

                               DNSClient?                        DNSClient                    = null,
                               Boolean                           Autostart                    = false)

            : this(new IPSocket(IPAddress ?? IPv4Address.Any,   // 0.0.0.0  IPv4+IPv6 sockets seem to fail on Win11!
                                TCPPort   ?? IPPort.HTTP),
                   HTTPServiceName,
                   ServerThreadNameCreator,
                   ServerThreadPriority,
                   ServerThreadIsBackground,

                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   DNSClient,
                   Autostart)

        { }

        #endregion

        #region WebSocketServer(IPSocket, HTTPServiceName = null, ..., Autostart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen on.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="Autostart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketServer(IPSocket                          IPSocket,
                               String?                           HTTPServiceName              = null,
                               ServerThreadNameCreatorDelegate?  ServerThreadNameCreator      = null,
                               ThreadPriority?                   ServerThreadPriority         = null,
                               Boolean?                          ServerThreadIsBackground     = null,

                               Boolean                           DisableWebSocketPings        = false,
                               TimeSpan?                         WebSocketPingEvery           = null,
                               TimeSpan?                         SlowNetworkSimulationDelay   = null,

                               DNSClient?                        DNSClient                    = null,
                               Boolean                           Autostart                    = false)
        {

            this.IPSocket                    = IPSocket;
            this.HTTPServiceName             = HTTPServiceName          ?? "GraphDefined HTTP Web Socket Server v2.0";
            this.ServerThreadNameCreator     = ServerThreadNameCreator;
            this.ServerThreadPriority        = ServerThreadPriority     ?? ThreadPriority.Normal;
            this.ServerThreadIsBackground    = ServerThreadIsBackground ?? false;
            this.DisableWebSocketPings       = DisableWebSocketPings;
            this.WebSocketPingEvery          = WebSocketPingEvery       ?? DefaultWebSocketPingEvery;
            this.SlowNetworkSimulationDelay  = SlowNetworkSimulationDelay;
            this.DNSClient                   = DNSClient;

            this.webSocketConnections        = new ConcurrentDictionary<IPSocket, WebSocketConnection>();
            this.cancellationTokenSource     = new CancellationTokenSource();

            if (Autostart)
                Start();

        }

        #endregion

        #endregion


        public Boolean SendFrame(WebSocketConnection  Connection,
                                 WebSocketFrame       Frame,
                                 EventTracking_Id?    EventTrackingId   = null)
        {

            var success = Connection.SendWebSocketFrame(Frame);

            if (success)
            {

                var eventTrackingId = EventTrackingId ?? EventTracking_Id.New;

                #region Send OnWebSocketFrameSent event

                try
                {

                    var OnWebSocketFrameSentLocal = OnWebSocketFrameSent;
                    if (OnWebSocketFrameSentLocal is not null)
                    {

                        var responseTask = OnWebSocketFrameSentLocal(Timestamp.Now,
                                                                     this,
                                                                     Connection,
                                                                     Frame,
                                                                     eventTrackingId);

                        responseTask.Wait(TimeSpan.FromSeconds(10));

                    }

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnWebSocketFrameSent));
                }

                #endregion

                #region Send OnTextMessageSent    event

                if (Frame.Opcode == WebSocketFrame.Opcodes.Text)
                {

                    try
                    {

                        var OnTextMessageSentLocal = OnTextMessageSent;
                        if (OnTextMessageSentLocal is not null)
                        {

                            var responseTask = OnTextMessageSentLocal(Timestamp.Now,
                                                                      this,
                                                                      Connection,
                                                                      Frame.Payload.ToUTF8String(),
                                                                      eventTrackingId);

                            responseTask.Wait(TimeSpan.FromSeconds(10));

                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessageSent));
                    }

                }

                #endregion

                #region Send OnBinaryMessageSent  event

                if (Frame.Opcode == WebSocketFrame.Opcodes.Binary)
                {

                    try
                    {

                        var OnBinaryMessageSentLocal = OnBinaryMessageSent;
                        if (OnBinaryMessageSentLocal is not null)
                        {

                            var responseTask = OnBinaryMessageSentLocal(Timestamp.Now,
                                                                        this,
                                                                        Connection,
                                                                        Frame.Payload,
                                                                        eventTrackingId);

                            responseTask.Wait(TimeSpan.FromSeconds(10));

                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnBinaryMessageSent));
                    }

                }

                #endregion

            }

            return success;

        }


        #region RemoveConnection(Connection)

        /// <summary>
        /// Remove the given web socket connection.
        /// </summary>
        /// <param name="Connection">A HTTP web socket connection.</param>
        public Boolean RemoveConnection(WebSocketConnection Connection)
        {

            DebugX.Log(nameof(WebSocketServer), " Removing HTTP web socket connection with " + Connection.RemoteSocket);

            return webSocketConnections.Remove(Connection.RemoteSocket, out _);

        }

        #endregion



        #region Start()

        /// <summary>
        /// Start the HTTP web socket listener thread.
        /// </summary>
        public void Start()
        {

            listenerThread = new Thread(() => {

                Thread.CurrentThread.Name          = ServerThreadNameCreator?.Invoke(IPSocket) ?? "HTTP Web Socket Server :" + IPSocket.Port;
                Thread.CurrentThread.Priority      = ServerThreadPriority;
                Thread.CurrentThread.IsBackground  = ServerThreadIsBackground;

                var token                          = cancellationTokenSource.Token;
                var tcpListener                    = new TcpListener(IPSocket.ToIPEndPoint());

                tcpListener.Start();

                isRunning = true;

                #region Send OnServerStarted event

                try
                {

                    // DebugX.Log("Web socket server has started on " + server.IPSocket);

                    var OnServerStartedLocal = OnServerStarted;
                    if (OnServerStartedLocal is not null)
                    {

                        var responseTask = OnServerStartedLocal(Timestamp.Now,
                                                                this,
                                                                EventTracking_Id.New);

                        responseTask.Wait(TimeSpan.FromSeconds(10));

                    }

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnNewTCPConnection));
                }

                #endregion


                while (!token.IsCancellationRequested)
                {

                    #region Accept new TCP connection

                    // Wait for a new/pending client connection
                    while (!token.IsCancellationRequested && !tcpListener.Pending())
                        Thread.Sleep(5);

                    if (token.IsCancellationRequested)
                        break;

                    var newTCPConnection = tcpListener.AcceptTcpClient();

                    #endregion

                    Task.Factory.StartNew(async context =>
                    {

                        try
                        {

                            if (context is WebSocketConnection webSocketConnection)
                            {

                                #region Data

                                Boolean       IsStillHTTP        = true;
                                String?       httpMethod         = null;
                                Byte[]?       bytes              = null;
                                Byte[]        bytesLeftOver      = Array.Empty<Byte>();
                                HTTPResponse? httpResponse       = null;

                                var cts2                         = CancellationTokenSource.CreateLinkedTokenSource(token);
                                var token2                       = cts2.Token;
                                var lastWebSocketPingTimestamp   = Timestamp.Now;

                                #endregion

                                #region Config web socket connection

                                if (webSocketConnection.TCPStream is not null) {
                                    webSocketConnection.TCPStream.ReadTimeout   = 20000; // msec
                                    webSocketConnection.TCPStream.WriteTimeout  = 3000;  // msec
                                }

                                if (!webSocketConnections.TryAdd(webSocketConnection.RemoteSocket,
                                                                 webSocketConnection))
                                {

                                    webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                    webSocketConnections.TryAdd(webSocketConnection.RemoteSocket,
                                                                webSocketConnection);

                                }

                                #endregion

                                #region Send OnNewTCPConnection event

                                try
                                {

                                    var OnNewTCPConnectionLocal = OnNewTCPConnection;
                                    if (OnNewTCPConnectionLocal is not null)
                                    {

                                        var responseTask = OnNewTCPConnectionLocal(Timestamp.Now,
                                                                                   this,
                                                                                   webSocketConnection,
                                                                                   EventTracking_Id.New,
                                                                                   token2);

                                        responseTask.Wait(TimeSpan.FromSeconds(10));

                                    }

                                }
                                catch (Exception e)
                                {
                                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnNewTCPConnection));
                                }

                                #endregion

                                while (!token2.IsCancellationRequested && webSocketConnection.TCPStream is not null)
                                {

                                    #region Main loop waiting for data... and sending a regular web socket ping

                                    if (bytes is null)
                                    {

                                        while (webSocketConnection.TCPStream is not null &&
                                               webSocketConnection.TCPStream.DataAvailable == false)
                                        {

                                            #region Send a regular web socket "ping"

                                            if (!DisableWebSocketPings &&
                                                Timestamp.Now > lastWebSocketPingTimestamp + WebSocketPingEvery)
                                            {

                                                var eventTrackingId  = EventTracking_Id.New;

                                                var payload          = Guid.NewGuid().ToString();

                                                var frame            = new WebSocketFrame(
                                                                           WebSocketFrame.Fin.Final,
                                                                           WebSocketFrame.MaskStatus.Off,
                                                                           new Byte[] { 0x00, 0x00, 0x00, 0x00 },
                                                                           WebSocketFrame.Opcodes.Ping,
                                                                           payload.ToUTF8Bytes(),
                                                                           WebSocketFrame.Rsv.Off,
                                                                           WebSocketFrame.Rsv.Off,
                                                                           WebSocketFrame.Rsv.Off
                                                                       );


                                                SendFrame(webSocketConnection,
                                                          frame,
                                                          eventTrackingId);

                                                #region OnPingMessageSent

                                                try
                                                {

                                                    OnPingMessageSent?.Invoke(Timestamp.Now,
                                                                              this,
                                                                              webSocketConnection,
                                                                              frame,
                                                                              eventTrackingId);

                                                }
                                                catch (Exception e)
                                                {
                                                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessageRequest));
                                                }

                                                #endregion


                                                lastWebSocketPingTimestamp = Timestamp.Now;

                                            }

                                            #endregion

                                            Thread.Sleep(5);

                                        };

                                        if (webSocketConnection.TCPStream is null)
                                            break;

                                        bytes = new Byte[bytesLeftOver.Length + webSocketConnection.TcpClient.Available];

                                        if (bytesLeftOver.Length > 0)
                                            Array.Copy(bytesLeftOver, 0, bytes, 0, bytesLeftOver.Length);

                                        webSocketConnection.TCPStream.Read(bytes,
                                                                           bytesLeftOver.Length,
                                                                           bytes.Length - bytesLeftOver.Length);

                                        httpMethod = IsStillHTTP
                                            ? Encoding.UTF8.GetString(bytes, 0, 4)
                                            : "";

                                    }

                                    #endregion


                                    #region A web socket handshake...

                                    if (httpMethod == "GET ")
                                    {

                                        #region Invalid HTTP request...

                                        // GET /webServices/ocpp/CP3211 HTTP/1.1
                                        // Host:                    some.server.com:33033
                                        // Upgrade:                 websocket
                                        // Connection:              Upgrade
                                        // Sec-WebSocket-Key:       x3JJHMbDL1EzLkh9GBhXDw==
                                        // Sec-WebSocket-Protocol:  ocpp1.6, ocpp1.5
                                        // Sec-WebSocket-Version:   13
                                        if (!HTTPRequest.TryParse(bytes, out var httpRequest))
                                            httpResponse  = new HTTPResponse.Builder(HTTPStatusCode.BadRequest) {
                                                                Server      = HTTPServiceName,
                                                                Date        = Timestamp.Now,
                                                                Connection  = "close"
                                                            }.AsImmutable;

                                        #endregion

                                        else
                                        {

                                            webSocketConnection.Request = httpRequest;

                                            #region OnHTTPRequest

                                            var OnHTTPRequestLocal = OnHTTPRequest;
                                            if (OnHTTPRequestLocal is not null)
                                            {

                                                await OnHTTPRequestLocal(Timestamp.Now,
                                                                         this,
                                                                         httpRequest);

                                            }

                                            #endregion

                                            #region OnValidateWebSocketConnection

                                            var OnValidateWebSocketConnectionLocal = OnValidateWebSocketConnection;
                                            if (OnValidateWebSocketConnectionLocal is not null)
                                            {

                                                httpResponse  = await OnValidateWebSocketConnectionLocal(Timestamp.Now,
                                                                                                         this,
                                                                                                         webSocketConnection,
                                                                                                         EventTracking_Id.New,
                                                                                                         token2);

                                            }

                                            #endregion

                                            #region Validate HTTP web socket request

                                            // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                                            // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                                            // 3. Compute SHA-1 and Base64 hash of the new value
                                            // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
#pragma warning disable SCS0006 // Weak hashing function.
                                            var swk             = webSocketConnection.Request.SecWebSocketKey;
                                            var swka            = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                                            var swkaSha1        = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                                            var swkaSha1Base64  = Convert.ToBase64String(swkaSha1);
#pragma warning restore SCS0006 // Weak hashing function.

                                            // HTTP/1.1 101 Switching Protocols
                                            // Connection:              Upgrade
                                            // Upgrade:                 websocket
                                            // Sec-WebSocket-Accept:    s3pPLMBiTxaQ9kYGzzhZRbK+xOo=
                                            // Sec-WebSocket-Protocol:  ocpp1.6
                                            // Sec-WebSocket-Version:   13
                                            httpResponse ??= new HTTPResponse.Builder(HTTPStatusCode.SwitchingProtocols) {
                                                                 Server                = HTTPServiceName,
                                                                 Connection            = "Upgrade",
                                                                 Upgrade               = "websocket",
                                                                 SecWebSocketAccept    = swkaSha1Base64,
                                                                 SecWebSocketProtocol  = "ocpp1.6",
                                                                 SecWebSocketVersion   = "13"
                                                             }.AsImmutable;

                                            #endregion

                                        }

                                        #region Send HTTP response

                                        var responseBytes = (httpResponse.EntirePDU + "\r\n\r\n").ToUTF8Bytes();

                                        lock (webSocketConnection.TCPStream)
                                        {
                                            webSocketConnection.TCPStream.Write(responseBytes,
                                                                                0,
                                                                                responseBytes.Length);
                                        }

                                        #region OnHTTPResponse

                                        var OnHTTPResponseLocal = OnHTTPResponse;
                                        if (OnHTTPResponseLocal is not null)
                                        {

                                            await OnHTTPResponseLocal(Timestamp.Now,
                                                                      this,
                                                                      httpRequest,
                                                                      httpResponse);

                                        }

                                        #endregion

                                        #endregion

                                        if (httpResponse.Connection == "close")
                                        {
                                            webSocketConnection.TCPStream.Close();
                                            webSocketConnection.TCPStream = null;
                                        }
                                        else
                                        {

                                            #region Send OnNewWebSocketConnection event

                                            try
                                            {

                                                var OnNewWebSocketConnectionLocal = OnNewWebSocketConnection;
                                                if (OnNewWebSocketConnectionLocal is not null)
                                                {

                                                    var responseTask = OnNewWebSocketConnectionLocal(Timestamp.Now,
                                                                                                     this,
                                                                                                     webSocketConnection,
                                                                                                     EventTracking_Id.New,
                                                                                                     token2);

                                                    responseTask.Wait(TimeSpan.FromSeconds(10));

                                                }

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnNewWebSocketConnection));
                                            }

                                            #endregion

                                            IsStillHTTP  = false;
                                            bytes        = null;

                                        }

                                    }

                                    #endregion

                                    #region ...or a web socket frame...

                                    else if (IsStillHTTP == false)
                                    {

                                        if (WebSocketFrame.TryParse(bytes,
                                                                    out var frame,
                                                                    out var frameLength,
                                                                    out var errorResponse))
                                        {

                                            if (frame is not null)
                                            {

                                                EventTracking_Id eventTrackingId  = EventTracking_Id.New;
                                                WebSocketFrame?  responseFrame    = null;

                                                #region OnWebSocketFrameReceived

                                                try
                                                {

                                                    OnWebSocketFrameReceived?.Invoke(Timestamp.Now,
                                                                                     this,
                                                                                     webSocketConnection,
                                                                                     frame,
                                                                                     eventTrackingId);

                                                }
                                                catch (Exception e)
                                                {
                                                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnWebSocketFrameReceived));
                                                }

                                                #endregion

                                                switch (frame.Opcode)
                                                {

                                                    #region Text   message

                                                    case WebSocketFrame.Opcodes.Text:

                                                        #region OnTextMessageRequest

                                                        try
                                                        {

                                                            OnTextMessageRequest?.Invoke(Timestamp.Now,
                                                                                         this,
                                                                                         webSocketConnection,
                                                                                         frame.Payload.ToUTF8String(),
                                                                                         eventTrackingId);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessageRequest));
                                                        }

                                                        #endregion

                                                        #region ProcessTextMessage

                                                        WebSocketTextMessageResponse? textMessageResponse = null;

                                                        try
                                                        {

                                                            textMessageResponse = await ProcessTextMessage(Timestamp.Now,
                                                                                                           webSocketConnection,
                                                                                                           frame.Payload.ToUTF8String(),
                                                                                                           eventTrackingId,
                                                                                                           token2);

                                                            // Incoming higher protocol level respones will not produce another response!
                                                            if (textMessageResponse          is not null &&
                                                                textMessageResponse.ResponseMessage is not null &&
                                                                textMessageResponse.ResponseMessage != "")
                                                            {

                                                                responseFrame = new WebSocketFrame(
                                                                                    WebSocketFrame.Fin.Final,
                                                                                    WebSocketFrame.MaskStatus.Off,
                                                                                    Array.Empty<Byte>(),
                                                                                    WebSocketFrame.Opcodes.Text,
                                                                                    textMessageResponse.ResponseMessage.ToUTF8Bytes(),
                                                                                    WebSocketFrame.Rsv.Off,
                                                                                    WebSocketFrame.Rsv.Off,
                                                                                    WebSocketFrame.Rsv.Off
                                                                                );

                                                            }

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(ProcessTextMessage));
                                                        }

                                                        #endregion

                                                        #region OnTextMessageResponse

                                                        try
                                                        {

                                                            if (responseFrame is not null && textMessageResponse is not null)
                                                                OnTextMessageResponse?.Invoke(Timestamp.Now,
                                                                                              this,
                                                                                              webSocketConnection,
                                                                                              textMessageResponse.EventTrackingId,
                                                                                              textMessageResponse.RequestTimestamp,
                                                                                              textMessageResponse.RequestMessage,
                                                                                              textMessageResponse.ResponseTimestamp,
                                                                                              textMessageResponse.ResponseMessage);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessageResponse));
                                                        }

                                                        #endregion

                                                        break;

                                                    #endregion

                                                    #region Binary message

                                                    case WebSocketFrame.Opcodes.Binary:

                                                        #region OnBinaryMessageRequest

                                                        try
                                                        {

                                                            OnBinaryMessageRequest?.Invoke(Timestamp.Now,
                                                                                           this,
                                                                                           webSocketConnection,
                                                                                           frame.Payload,
                                                                                           eventTrackingId);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnBinaryMessageRequest));
                                                        }

                                                        #endregion

                                                        #region ProcessBinaryMessage

                                                        WebSocketBinaryMessageResponse? binaryMessageResponse = null;

                                                        try
                                                        {

                                                            binaryMessageResponse = await ProcessBinaryMessage(Timestamp.Now,
                                                                                                               webSocketConnection,
                                                                                                               frame.Payload,
                                                                                                               eventTrackingId,
                                                                                                               token2);

                                                            // Incoming higher protocol level respones will not produce another response!
                                                            if (binaryMessageResponse                 is not null &&
                                                                binaryMessageResponse.ResponseMessage is not null &&
                                                                binaryMessageResponse.ResponseMessage.Length > 0)
                                                            {

                                                                responseFrame = new WebSocketFrame(
                                                                                    WebSocketFrame.Fin.Final,
                                                                                    WebSocketFrame.MaskStatus.Off,
                                                                                    Array.Empty<Byte>(),
                                                                                    WebSocketFrame.Opcodes.Text,
                                                                                    binaryMessageResponse.ResponseMessage,
                                                                                    WebSocketFrame.Rsv.Off,
                                                                                    WebSocketFrame.Rsv.Off,
                                                                                    WebSocketFrame.Rsv.Off
                                                                                );

                                                            }

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(ProcessBinaryMessage));
                                                        }

                                                        #endregion

                                                        #region OnBinaryMessageResponse

                                                        try
                                                        {

                                                            if (responseFrame is not null && binaryMessageResponse is not null)
                                                                OnBinaryMessageResponse?.Invoke(Timestamp.Now,
                                                                                                this,
                                                                                                webSocketConnection,
                                                                                                binaryMessageResponse.EventTrackingId,
                                                                                                binaryMessageResponse.RequestTimestamp,
                                                                                                binaryMessageResponse.RequestMessage,
                                                                                                binaryMessageResponse.ResponseTimestamp,
                                                                                                binaryMessageResponse.ResponseMessage);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnBinaryMessageResponse));
                                                        }

                                                        #endregion

                                                        break;

                                                    #endregion

                                                    #region Ping   message

                                                    case WebSocketFrame.Opcodes.Ping:

                                                        #region OnPingMessageReceived

                                                        try
                                                        {

                                                            OnPingMessageReceived?.Invoke(Timestamp.Now,
                                                                                          this,
                                                                                          webSocketConnection,
                                                                                          frame,
                                                                                          eventTrackingId);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPingMessageReceived));
                                                        }

                                                        #endregion

                                                        responseFrame = new WebSocketFrame(
                                                                            WebSocketFrame.Fin.Final,
                                                                            WebSocketFrame.MaskStatus.Off,
                                                                            new Byte[] { 0x00, 0x00, 0x00, 0x00 },
                                                                            WebSocketFrame.Opcodes.Pong,
                                                                            frame.Payload,
                                                                            WebSocketFrame.Rsv.Off,
                                                                            WebSocketFrame.Rsv.Off,
                                                                            WebSocketFrame.Rsv.Off
                                                                        );

                                                        break;

                                                    #endregion

                                                    #region Pong   message

                                                    case WebSocketFrame.Opcodes.Pong:

                                                        #region OnPongMessageReceived

                                                        try
                                                        {

                                                            OnPongMessageReceived?.Invoke(Timestamp.Now,
                                                                                          this,
                                                                                          webSocketConnection,
                                                                                          frame,
                                                                                          eventTrackingId);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPongMessageReceived));
                                                        }

                                                        #endregion

                                                        break;

                                                    #endregion

                                                    #region Close  message

                                                    case WebSocketFrame.Opcodes.Close:

                                                        webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                                        #region OnCloseMessage

                                                        try
                                                        {

                                                            OnCloseMessageReceived?.Invoke(Timestamp.Now,
                                                                                           this,
                                                                                           webSocketConnection,
                                                                                           frame,
                                                                                           eventTrackingId);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnCloseMessageReceived));
                                                        }

                                                        #endregion

                                                        webSocketConnection.TCPStream.Close();
                                                        webSocketConnection.TCPStream = null;

                                                        break;

                                                    #endregion

                                                }

                                                #region Send immediate response frame... when available

                                                if (responseFrame is not null && webSocketConnection.TCPStream is not null)
                                                {

                                                    SendFrame(webSocketConnection,
                                                              responseFrame,
                                                              eventTrackingId);

                                                    #region OnWebSocketFrameResponse

                                                    try
                                                    {

                                                        if (responseFrame is not null)
                                                            OnWebSocketFrameResponse?.Invoke(Timestamp.Now,
                                                                                             this,
                                                                                             webSocketConnection,
                                                                                             frame,
                                                                                             responseFrame,
                                                                                             eventTrackingId);

                                                    }
                                                    catch (Exception e)
                                                    {
                                                        DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnWebSocketFrameResponse));
                                                    }

                                                    #endregion

                                                }

                                                #endregion

                                                //if (frame.Opcode.IsControl())
                                                //{
                                                //    if (frame.FIN    == WebSocketFrame.Fin.More)
                                                //        Console.WriteLine(">>> A control frame is fragmented!");
                                                //}

                                                if ((UInt64) bytes.Length == frameLength)
                                                    bytes = null;

                                                else
                                                {
                                                    // The buffer might contain additional web socket frames...
                                                    var newBytes = new Byte[(UInt64) bytes.Length - frameLength];
                                                    Array.Copy(bytes, (Int32) frameLength, newBytes, 0, newBytes.Length);
                                                    bytes = newBytes;
                                                }

                                                bytesLeftOver = Array.Empty<Byte>();

                                            }

                                        }
                                        else
                                        {
                                            bytesLeftOver = bytes;
                                            DebugX.Log("Could not parse the given web socket frame of " + bytesLeftOver.Length + " byte(s): " + errorResponse);
                                            bytes = null;
                                        }

                                    }

                                    #endregion

                                    #region ...some other crap!

                                    // Can e.g. be web crawlers etc.pp

                                    else
                                    {

                                        // DebugX.Log(nameof(WebSocketServer) + ": Closing invalid TCP connection!");

                                        webSocketConnection.TCPStream.Close();
                                        webSocketConnection.TCPStream = null;

                                    }

                                    #endregion

                                }

                                webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                #region OnTCPConnectionClosed

                                try
                                {

                                    OnTCPConnectionClosed?.Invoke(Timestamp.Now,
                                                                  this,
                                                                  webSocketConnection,
                                                                  "!!!",
                                                                  EventTracking_Id.New);

                                }
                                catch (Exception e)
                                {
                                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTCPConnectionClosed));
                                }

                                #endregion

                            }
                            else
                                DebugX.Log(nameof(WebSocketServer), " The given web socket connection is invalid!");

                        }
                        catch (Exception e)
                        {
                            DebugX.Log(nameof(WebSocketServer), " Exception in web socket server: " + e.Message + Environment.NewLine + e.StackTrace);
                        }

                    },
                    new WebSocketConnection(this,
                                            newTCPConnection,
                                            SlowNetworkSimulationDelay: SlowNetworkSimulationDelay));

                }

                #region Stop TCP listener

                DebugX.Log(nameof(WebSocketServer), " Stopping server on " + IPSocket.IPAddress + ":" + IPSocket.Port);

                tcpListener.Stop();

                isRunning = false;

                //ToDo: Request all client connections to finish!

                DebugX.Log(nameof(WebSocketServer), " Stopped server on " + IPSocket.IPAddress + ":" + IPSocket.Port);

                #endregion

            });

            listenerThread.Start();

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Shutdown the HTTP web socket listener thread.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        public void Shutdown(String?  Message   = null,
                             Boolean  Wait      = true)
        {

            cancellationTokenSource.Cancel();

            if (Wait) {
                while (isRunning) {
                    Thread.Sleep(10);
                }
            }

        }

        #endregion


        #region (virtual) ProcessTextMessage  (RequestTimestamp, Connection, TextMessage, ...)

        /// <summary>
        /// The default HTTP web socket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">The event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public virtual Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime             RequestTimestamp,
                                                                             WebSocketConnection  Connection,
                                                                             String               TextMessage,
                                                                             EventTracking_Id     EventTrackingId,
                                                                             CancellationToken    CancellationToken)
        {

            return Task.FromResult(
                       new WebSocketTextMessageResponse(
                           RequestTimestamp,
                           TextMessage,
                           Timestamp.Now,
                           String.Empty,
                           EventTrackingId
                       )
                   );

        }

        #endregion

        #region (virtual) ProcessBinaryMessage(RequestTimestamp, Connection, BinaryMessage, ...)

        /// <summary>
        /// The default HTTP web socket binary message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The web socket binary message.</param>
        /// <param name="EventTrackingId">The event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public virtual Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime             RequestTimestamp,
                                                                                 WebSocketConnection  Connection,
                                                                                 Byte[]               BinaryMessage,
                                                                                 EventTracking_Id     EventTrackingId,
                                                                                 CancellationToken    CancellationToken)
        {

            return Task.FromResult(
                       new WebSocketBinaryMessageResponse(
                           RequestTimestamp,
                           BinaryMessage,
                           Timestamp.Now,
                           Array.Empty<Byte>(),
                           EventTrackingId
                       )
                   );

        }

        #endregion


    }

}
