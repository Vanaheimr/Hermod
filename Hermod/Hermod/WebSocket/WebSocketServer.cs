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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using System.Drawing.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// The delegate for the HTTP web socket request log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="WebSocketServer">The sending WebSocket server.</param>
    /// <param name="Request">The incoming request.</param>
    public delegate Task WebSocketRequestLogHandler(DateTime         Timestamp,
                                                    WebSocketServer  WebSocketServer,
                                                    JArray           Request);

    /// <summary>
    /// The delegate for the HTTP web socket response log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="WebSocketServer">The sending WebSocket server.</param>
    /// <param name="Request">The incoming WebSocket request.</param>
    /// <param name="Response">The outgoing WebSocket response.</param>
    public delegate Task WebSocketResponseLogHandler(DateTime         Timestamp,
                                                     WebSocketServer  WebSocketServer,
                                                     JArray           Request,
                                                     JArray           Response);


    /// <summary>
    /// A HTTP web socket server.
    /// </summary>
    public class WebSocketServer
    {

        #region Data

        private readonly List<WebSocketConnection>  webSocketConnections;

        private          Thread?                    listenerThread;

        private readonly CancellationTokenSource    cancellationTokenSource;

        private volatile Boolean                    isRunning    = false;

        private const    String                     LogfileName  = "HTTPWebSocketServer.log";

        #endregion

        #region Properties

        /// <summary>
        /// Return an enumeration of all currently connected HTTP web socket connections.
        /// </summary>
        public IEnumerable<WebSocketConnection>  WebSocketConnections
        {
            get {
                lock (webSocketConnections)
                {
                    return webSocketConnections.ToArray();
                }
            }
        }

        /// <summary>
        /// The HTTP service name.
        /// </summary>
        public String                            HTTPServiceName    { get; }

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
        public IPSocket                          IPSocket           { get; }

        /// <summary>
        /// Whether the TCP listener is currently running.
        /// </summary>
        public Boolean                           IsRunning
            => isRunning;

        /// <summary>
        /// An optional DNS client to use.
        /// </summary>
        public DNSClient?                        DNSClient          { get; }

        #endregion

        #region Events

        public event OnNewTCPConnectionDelegate?                  OnNewTCPConnection;

        public event OnOnValidateWebSocketConnectionDelegate?     OnValidateWebSocketConnection;

        public event OnNewWebSocketConnectionDelegate?            OnNewWebSocketConnection;


        public event OnWebSocketMessageRequestDelegate?           OnMessageRequest;

        public event OnWebSocketMessageResponseDelegate?          OnMessageResponse;


        public event OnWebSocketTextMessageRequestDelegate?       OnTextMessageRequest;

        public event OnWebSocketTextMessageResponseDelegate?      OnTextMessageResponse;


        public event OnWebSocketBinaryMessageRequestDelegate?     OnBinaryMessageRequest;

        public event OnWebSocketBinaryMessageResponseDelegate?    OnBinaryMessageResponse;


        public event OnWebSocketMessageRequestDelegate?           OnPingMessageReceived;

        public event OnWebSocketMessageRequestDelegate?           OnPongMessageReceived;


        public event OnCloseMessageDelegate?                      OnCloseMessage;

        #endregion

        #region Constructor(s)

        #region WebSocketServer(IPAddress = null, TCPPort = null, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="TCPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketServer(IIPAddress?  IPAddress         = null,
                               IPPort?      TCPPort           = null,
                               String?      HTTPServiceName   = null,
                               DNSClient?   DNSClient         = null,
                               Boolean      AutoStart         = false)

            : this(new IPSocket(IPAddress ?? IPv4Address.Any,   // 0.0.0.0  IPv4+IPv6 sockets seem to fail on Win11!
                                TCPPort   ?? IPPort.HTTP),
                   HTTPServiceName,
                   DNSClient,
                   AutoStart)

        { }

        #endregion

        #region WebSocketServer(IPSocket, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen on.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketServer(IPSocket    IPSocket,
                               String?     HTTPServiceName   = null,
                               DNSClient?  DNSClient         = null,
                               Boolean     AutoStart         = false)
        {

            this.IPSocket                 = IPSocket;
            this.HTTPServiceName          = HTTPServiceName ?? "GraphDefined HTTP Web Socket Server v2.0";
            this.DNSClient                = DNSClient;

            this.webSocketConnections     = new List<WebSocketConnection>();
            this.cancellationTokenSource  = new CancellationTokenSource();

            if (AutoStart)
                Start();

        }

        #endregion

        #endregion


        #region Start()

        /// <summary>
        /// Start the HTTP web socket listener thread.
        /// </summary>
        public void Start()
        {

            listenerThread = new Thread(() => {

                Thread.CurrentThread.Name           = "HTTP Web Socket Server :" + IPSocket.Port;//this.ServerThreadName;
                //Thread.CurrentThread.Priority       = this.ServerThreadPriority;
                //Thread.CurrentThread.IsBackground   = this.ServerThreadIsBackground;

                var token        = cancellationTokenSource.Token;
                var tcpListener  = new TcpListener(IPSocket.ToIPEndPoint());

                tcpListener.Start();

                DebugX.Log("Web socket server has started on " + IPSocket.IPAddress + ":" + IPSocket.Port + "...");

                isRunning = true;

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

                                if (webSocketConnection.TCPStream is not null) {
                                    webSocketConnection.TCPStream.ReadTimeout   = 20000; // msec
                                    webSocketConnection.TCPStream.WriteTimeout  = 3000;  // msec
                                }

                                lock (webSocketConnections)
                                {
                                    try
                                    {
                                        webSocketConnections.Add(webSocketConnection);
                                    }
                                    catch { }
                                }

                                Boolean IsStillHTTP              = true;
                                String? httpMethod               = null;
                                Byte[]? bytes                    = null;
                                Byte[]  bytesLeftOver            = Array.Empty<Byte>();

                                var cts2                         = CancellationTokenSource.CreateLinkedTokenSource(token);
                                var token2                       = cts2.Token;
                                var lastWebSocketPingTimestamp   = Timestamp.Now;
                                var WebSocketPingEvery           = TimeSpan.FromSeconds(20);

                                HTTPResponse? httpResponse       = null;

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

                                DebugX.Log("New web socket connection from: " + webSocketConnection.RemoteSocket.IPAddress + ":" + webSocketConnection.RemoteSocket.Port + "...");

                                while (!token2.IsCancellationRequested && webSocketConnection.TCPStream is not null)
                                {

                                    #region Main loop waiting for data...

                                    if (bytes is null)
                                    {

                                        while (webSocketConnection.TCPStream is not null &&
                                               webSocketConnection.TCPStream.DataAvailable == false)
                                        {

                                            #region Send a regular web socket "ping"

                                            if (Timestamp.Now > lastWebSocketPingTimestamp + WebSocketPingEvery)
                                            {

                                                var payload = Guid.NewGuid().ToString();

                                                webSocketConnection.SendWebSocketFrame(
                                                                        new WebSocketFrame(
                                                                            WebSocketFrame.Fin.Final,
                                                                            WebSocketFrame.MaskStatus.Off,
                                                                            new Byte[] { 0x00, 0x00, 0x00, 0x00 },
                                                                            WebSocketFrame.Opcodes.Ping,
                                                                            payload.ToUTF8Bytes(),
                                                                            WebSocketFrame.Rsv.Off,
                                                                            WebSocketFrame.Rsv.Off,
                                                                            WebSocketFrame.Rsv.Off
                                                                        )
                                                                    );

                                                DebugX.Log(nameof(WebSocketServer) + ": Ping sent:     '" + payload + "'!");

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

                                        webSocketConnection.TCPStream.Read(bytes, bytesLeftOver.Length, bytes.Length);

                                        httpMethod = IsStillHTTP
                                            ? Encoding.UTF8.GetString(bytes, 0, 4)
                                            : "";

                                    }

                                    #endregion


                                    #region A web socket handshake...

                                    if (httpMethod == "GET ")
                                    {

                                        // GET /webServices/ocpp/CP3211 HTTP/1.1
                                        // Host:                    some.server.com:33033
                                        // Upgrade:                 websocket
                                        // Connection:              Upgrade
                                        // Sec-WebSocket-Key:       x3JJHMbDL1EzLkh9GBhXDw==
                                        // Sec-WebSocket-Protocol:  ocpp1.6, ocpp1.5
                                        // Sec-WebSocket-Version:   13
                                        if (!HTTPRequest.TryParse(bytes, out HTTPRequest httpRequest))
                                        {

                                            DebugX.Log("Could not parse the incoming HTTP request!");
                                            DebugX.Log(bytes.ToUTF8String());

                                            httpResponse  = new HTTPResponse.Builder(HTTPStatusCode.BadRequest) {
                                                                Server      = HTTPServiceName,
                                                                Date        = Timestamp.Now,
                                                                Connection  = "close"
                                                            }.AsImmutable;

                                        }
                                        else
                                        {

                                            webSocketConnection.Request = httpRequest;

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

                                        }

                                        var response = (httpResponse.EntirePDU + "\r\n\r\n").ToUTF8Bytes();

                                        lock (webSocketConnection.TCPStream)
                                        {
                                            webSocketConnection.TCPStream.Write(response, 0, response.Length);
                                        }

                                        if (httpRequest is not null)
                                        {

                                            File.AppendAllText(LogfileName,
                                                               String.Concat("Timestamp: ",     Timestamp.Now.ToIso8601(), Environment.NewLine,
                                                                             "HTTP request: ",       Environment.NewLine, Environment.NewLine,
                                                                             httpRequest.EntirePDU,  Environment.NewLine,
                                                                             "--------------------------------------------------------------------------------------------", Environment.NewLine));

                                            File.AppendAllText(LogfileName,
                                                               String.Concat("Timestamp: ",     Timestamp.Now.ToIso8601(), Environment.NewLine,
                                                                             "HTTP response: ",      Environment.NewLine, Environment.NewLine,
                                                                             httpResponse.EntirePDU, Environment.NewLine,
                                                                             "--------------------------------------------------------------------------------------------", Environment.NewLine));

                                        }

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
                                                                    // ToDo: Might contain multiple frames!
                                        {

                                            if (frame is not null)
                                            {

                                                var             eventTrackingId  = EventTracking_Id.New;
                                                WebSocketFrame? responseFrame    = null;

                                                #region OnMessageRequest

                                                try
                                                {

                                                    OnMessageRequest?.Invoke(Timestamp.Now,
                                                                             this,
                                                                             webSocketConnection,
                                                                             frame,
                                                                             eventTrackingId,
                                                                             token2);

                                                }
                                                catch (Exception e)
                                                {
                                                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnMessageRequest));
                                                }

                                                #endregion

                                                #region Send OnMessage event

                                                //try
                                                //{

                                                //    var OnMessageLocal = OnMessage;

                                                //    if (OnMessageLocal != null)
                                                //    {

                                                //        var responseTask = OnMessageLocal(Timestamp.Now,
                                                //                                          this,
                                                //                                          WSConnection,
                                                //                                          frame,
                                                //                                          eventTrackingId,
                                                //                                          token2);

                                                //        responseTask.Wait(TimeSpan.FromSeconds(60));

                                                //        if (responseTask.Result.Response != null)
                                                //        {
                                                //            responseFrame = responseTask.Result.Response;
                                                //        }

                                                //    }

                                                //}
                                                //catch (Exception e)
                                                //{
                                                //    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnMessage));
                                                //}

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
                                                                                         new WebSocketTextMessageRequest(eventTrackingId,
                                                                                                                         Timestamp.Now,
                                                                                                                         frame.Payload.ToUTF8String()),
                                                                                         token2);

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
                                                                                                           eventTrackingId,
                                                                                                           webSocketConnection,
                                                                                                           frame.Payload.ToUTF8String(),
                                                                                                           token2);

                                                            // Incoming higher protocol level respones will not produce another response!
                                                            if (textMessageResponse          is not null &&
                                                                textMessageResponse.Response is not null &&
                                                                textMessageResponse.Response.IsNotNullOrEmpty())
                                                            {

                                                                responseFrame = new WebSocketFrame(
                                                                                    WebSocketFrame.Fin.Final,
                                                                                    WebSocketFrame.MaskStatus.Off,
                                                                                    Array.Empty<Byte>(),
                                                                                    WebSocketFrame.Opcodes.Text,
                                                                                    textMessageResponse.Response.ToUTF8Bytes(),
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
                                                                                              textMessageResponse);

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
                                                                                           new WebSocketBinaryMessageRequest(eventTrackingId,
                                                                                                                             Timestamp.Now,
                                                                                                                             frame.Payload),
                                                                                           token2);
                                                                                           //Timestamp.Now,
                                                                                           //this,
                                                                                           //WSConnection,
                                                                                           //frame.Payload,
                                                                                           //eventTrackingId,
                                                                                           //token2);

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
                                                                                                               eventTrackingId,
                                                                                                               webSocketConnection,
                                                                                                               frame.Payload,
                                                                                                               token2);

                                                            // Incoming higher protocol level respones will not produce another response!
                                                            if (binaryMessageResponse          is not null &&
                                                                binaryMessageResponse.Response is not null &&
                                                                binaryMessageResponse.Response.Length > 0)
                                                            {

                                                                responseFrame = new WebSocketFrame(
                                                                                    WebSocketFrame.Fin.Final,
                                                                                    WebSocketFrame.MaskStatus.Off,
                                                                                    Array.Empty<Byte>(),
                                                                                    WebSocketFrame.Opcodes.Text,
                                                                                    binaryMessageResponse.Response,
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
                                                                                                binaryMessageResponse);

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
                                                                                          eventTrackingId,
                                                                                          token2);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPingMessageReceived));
                                                        }

                                                        #endregion

                                                        DebugX.Log(nameof(WebSocketServer) + ": Ping received: '" + frame.Payload.ToUTF8String() + "'");

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
                                                                                          eventTrackingId,
                                                                                          token2);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPongMessageReceived));
                                                        }

                                                        #endregion

                                                        DebugX.Log(nameof(WebSocketServer) + ": Pong received: '" + frame.Payload.ToUTF8String() + "'");

                                                        break;

                                                    #endregion

                                                    #region Close  message

                                                    case WebSocketFrame.Opcodes.Close:

                                                        #region OnCloseMessage

                                                        try
                                                        {

                                                            OnCloseMessage?.Invoke(Timestamp.Now,
                                                                                   this,
                                                                                   webSocketConnection,
                                                                                   frame,
                                                                                   eventTrackingId,
                                                                                   token2);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnCloseMessage));
                                                        }

                                                        #endregion

                                                        DebugX.Log(nameof(WebSocketServer) + ": Close received!");

                                                        webSocketConnection.TCPStream.Close();
                                                        webSocketConnection.TCPStream = null;

                                                        break;

                                                    #endregion

                                                }

                                                if (responseFrame is not null && webSocketConnection.TCPStream is not null)
                                                {

                                                    webSocketConnection.SendWebSocketFrame(responseFrame);

                                                    #region OnMessageResponse

                                                    try
                                                    {

                                                        if (responseFrame is not null)
                                                            OnMessageResponse?.Invoke(Timestamp.Now,
                                                                                      this,
                                                                                      webSocketConnection,
                                                                                      frame,
                                                                                      responseFrame,
                                                                                      eventTrackingId,
                                                                                      token2);

                                                    }
                                                    catch (Exception e)
                                                    {
                                                        DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnMessageResponse));
                                                    }

                                                    #endregion

                                                }

                                                //if (frame.Opcode.IsControl())
                                                //{

                                                //    if (frame.FIN    == WebSocketFrame.Fin.More)
                                                //        Console.WriteLine(">>> A control frame is fragmented!");

                                                //    if (frame.Payload.Length > 125)
                                                //        Console.WriteLine("A control frame has too long payload length.");

                                                //}

                                                if ((UInt64) bytes.Length == frameLength)
                                                    bytes = null;

                                                else
                                                {
                                                    var newBytes = new Byte[(UInt64) bytes.Length - frameLength];
                                                    Array.Copy(bytes, 0, newBytes, 0, newBytes.Length);
                                                    bytes = newBytes;
                                                }

                                                bytesLeftOver = Array.Empty<Byte>();

                                            }

                                        }
                                        else
                                        {
                                            bytesLeftOver = bytes;
                                            DebugX.Log("Could not parse the given web socket frame: " + errorResponse);
                                        }

                                    }

                                    #endregion

                                    #region ...some other crap!

                                    // Can e.g. be web crawlers etc.pp

                                    else
                                    {

                                        DebugX.Log(nameof(WebSocketServer) + ": Closing invalid TCP connection!");

                                        webSocketConnection.TCPStream.Close();
                                        webSocketConnection.TCPStream = null;

                                    }

                                    #endregion

                                }

                                DebugX.Log(nameof(WebSocketServer), " Connection closed!");

                                lock (webSocketConnections)
                                {
                                    try
                                    {
                                        webSocketConnections.Remove(webSocketConnection);
                                    }
                                    catch { }
                                }

                                #region OnTCPConnectionClosed

                                //try
                                //{

                                //    OnCloseMessage?.Invoke(Timestamp.Now,
                                //                            this,
                                //                            webSocketConnection,
                                //                            frame,
                                //                            eventTrackingId,
                                //                            token2);

                                //}
                                //catch (Exception e)
                                //{
                                //    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnCloseMessage));
                                //}

                                #endregion

                            }
                            else
                                DebugX.Log(nameof(WebSocketServer), " The given web socket connection is invalid!");

                        }
                        catch (Exception e)
                        {
                            DebugX.Log(nameof(WebSocketServer), " Exception in web socket server thread: " + e.Message + Environment.NewLine + e.StackTrace);
                        }

                    },
                    new WebSocketConnection(this, newTCPConnection));

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


        #region (virtual) ProcessTextMessage  (RequestTimestamp, EventTrackingId, Connection, TextMessage, ...)

        public virtual Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime             RequestTimestamp,
                                                                             EventTracking_Id     EventTrackingId,
                                                                             WebSocketConnection  Connection,
                                                                             String               TextMessage,
                                                                             CancellationToken    CancellationToken)
        {
            return Task.FromResult(
                       new WebSocketTextMessageResponse(
                           EventTrackingId,
                           RequestTimestamp,
                           TextMessage,
                           Timestamp.Now,
                           new JArray().ToString(Newtonsoft.Json.Formatting.None)
                       )
                   );
        }

        #endregion

        #region (virtual) ProcessBinaryMessage(RequestTimestamp, EventTrackingId, Connection, BinaryMessage, ...)

        public virtual Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime             RequestTimestamp,
                                                                                 EventTracking_Id     EventTrackingId,
                                                                                 WebSocketConnection  Connection,
                                                                                 Byte[]               BinaryMessage,
                                                                                 CancellationToken    CancellationToken)
        {
            return Task.FromResult(
                       new WebSocketBinaryMessageResponse(
                           EventTrackingId,
                           RequestTimestamp,
                           BinaryMessage,
                           Timestamp.Now,
                           Array.Empty<Byte>()
                       )
                   );
        }

        #endregion


    }

}
