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

using System;
using System.Text;
using System.Net.Sockets;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// The delegate for the WebSocket request log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="WebSocketServer">The sending WebSocket server.</param>
    /// <param name="Request">The incoming request.</param>
    public delegate Task WSRequestLogHandler(DateTime         Timestamp,
                                             WebSocketServer  WebSocketServer,
                                             JArray           Request);

    /// <summary>
    /// The delegate for the WebSocket response log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="WebSocketServer">The sending WebSocket server.</param>
    /// <param name="Request">The incoming WebSocket request.</param>
    /// <param name="Response">The outgoing WebSocket response.</param>
    public delegate Task WSResponseLogHandler(DateTime         Timestamp,
                                              WebSocketServer  WebSocketServer,
                                              JArray           Request,
                                              JArray           Response);


    /// <summary>
    /// A WebSocket server.
    /// </summary>
    public class WebSocketServer
    {

        #region Data

        private readonly List<WebSocketConnection>  webSocketConnections;

        private          Thread?                    listenerThread;

        private readonly CancellationTokenSource    cancellationTokenSource;

        private const    String LogfileName = "CentralSystemWSServer.log";

        #endregion

        #region Properties

        public IEnumerable<WebSocketConnection> WebSocketConnections
            => webSocketConnections;


        public String      HTTPServiceName    { get; }

        public IIPAddress IPAddress
            => IPSocket.IPAddress;

        public IPPort IPPort
            => IPSocket.Port;

        public IPSocket    IPSocket           { get; }

        public DNSClient?  DNSClient          { get; }


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


        public event OnWebSocketMessageRequestDelegate?          OnPingMessageReceived;

        public event OnWebSocketMessageRequestDelegate?          OnPongMessageReceived;


        public event OnCloseMessageDelegate?                     OnCloseMessage;

        #endregion

        #region Constructor(s)

        public WebSocketServer(IIPAddress?  IPAddress         = null,
                               IPPort?      Port              = null,
                               String?      HTTPServiceName   = null,
                               DNSClient?   DNSClient         = null,
                               Boolean      AutoStart         = false)

            : this(new IPSocket(IPAddress ?? IPv4Address.Any,
                                Port      ?? IPPort.HTTP),
                   HTTPServiceName,
                   DNSClient,
                   AutoStart)

        { }

        public WebSocketServer(IPSocket    IPSocket,
                               String?     HTTPServiceName   = null,
                               DNSClient?  DNSClient         = null,
                               Boolean     AutoStart         = false)
        {

            this.IPSocket                 = IPSocket;
            this.HTTPServiceName          = HTTPServiceName ?? "GraphDefined Websocket Server";
            this.DNSClient                = DNSClient;

            this.webSocketConnections     = new List<WebSocketConnection>();
            this.cancellationTokenSource  = new CancellationTokenSource();

            if (AutoStart)
                Start();

        }

        #endregion


        public void Start()
        {

            listenerThread = new Thread(() => {

                var token  = cancellationTokenSource.Token;
                var server = new TcpListener(IPSocket.ToIPEndPoint());
                server.Start();
                DebugX.Log("WebSocket server has started on " + IPSocket.IPAddress + ":" + IPSocket.Port + "...");

                while (!token.IsCancellationRequested)
                {

                    var newTCPClient = server.AcceptTcpClient();

                    Task.Factory.StartNew(async context => {

                        try
                        {

                            if (context is WebSocketConnection WSConnection)
                            {

                                webSocketConnections.Add(WSConnection);
                                var stream                      = WSConnection.TcpClient.GetStream();
                                stream.ReadTimeout              = 20000;
                                stream.WriteTimeout             = 1000;
                                var cts2                        = CancellationTokenSource.CreateLinkedTokenSource(token);
                                var token2                      = cts2.Token;
                                Byte[]? bytes                   = null;
                                Byte[] bytesLeftOver            = Array.Empty<Byte>();
                                String? httpMethod              = null;
                                Boolean IsStillHTTP             = true;
                                var lastWebSocketPingTimestamp  = Timestamp.Now;
                                var WebSocketPingEvery          = TimeSpan.FromSeconds(20);

                                HTTPResponse? httpResponse      = null;

                                #region Send OnNewTCPConnection event

                                try
                                {

                                    var OnNewTCPConnectionLocal = OnNewTCPConnection;

                                    if (OnNewTCPConnectionLocal is not null)
                                    {

                                        var responseTask = OnNewTCPConnectionLocal(Timestamp.Now,
                                                                                   this,
                                                                                   WSConnection,
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

                                DebugX.Log("New web socket connection from: " + WSConnection.RemoteSocket.IPAddress + ":" + WSConnection.RemoteSocket.Port + "...");

                                while (!token2.IsCancellationRequested && stream is not null)
                                {

                                    if (bytes is null)
                                    {

                                        while (!stream.DataAvailable)
                                        {

                                            #region Send a regular web socket Ping

                                            if (stream is not null &&
                                                Timestamp.Now > lastWebSocketPingTimestamp + WebSocketPingEvery)
                                            {

                                                stream.Write(new WebSocketFrame(WebSocketFrame.Fin.Final,
                                                                                WebSocketFrame.MaskStatus.Off,
                                                                                new Byte[] { 0x00, 0x00, 0x00, 0x00 },
                                                                                WebSocketFrame.Opcodes.Ping,
                                                                                Guid.NewGuid().ToByteArray(),
                                                                                WebSocketFrame.Rsv.Off,
                                                                                WebSocketFrame.Rsv.Off,
                                                                                WebSocketFrame.Rsv.Off).ToByteArray());

                                                stream.Flush();

                                                DebugX.Log(nameof(WebSocketServer) + ": Ping sent!");

                                                lastWebSocketPingTimestamp = Timestamp.Now;

                                            }

                                            #endregion

                                            Thread.Sleep(5);

                                        };

                                        bytes = new Byte[bytesLeftOver.Length + WSConnection.TcpClient.Available];

                                        if (bytesLeftOver.Length > 0)
                                            Array.Copy(bytesLeftOver, 0, bytes, 0, bytesLeftOver.Length);

                                        stream.Read(bytes, bytesLeftOver.Length, bytes.Length);

                                        httpMethod = IsStillHTTP
                                            ? Encoding.UTF8.GetString(bytes, 0, 4)
                                            : "";

                                    }

                                    #region Web socket handshake...

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
                                                                Server              = HTTPServiceName,
                                                                Date                = Timestamp.Now,
                                                                Connection          = "close"
                                                            }.AsImmutable;

                                        }
                                        else
                                        {

                                            WSConnection.Request = httpRequest;

                                            #region OnValidateWebSocketConnection

                                            var OnValidateWebSocketConnectionLocal = OnValidateWebSocketConnection;
                                            if (OnValidateWebSocketConnectionLocal is not null)
                                            {

                                                httpResponse  = await OnValidateWebSocketConnectionLocal(Timestamp.Now,
                                                                                                         this,
                                                                                                         WSConnection,
                                                                                                         EventTracking_Id.New,
                                                                                                         token2);

                                            }

                                            #endregion


                                            // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                                            // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                                            // 3. Compute SHA-1 and Base64 hash of the new value
                                            // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                                            var swk             = WSConnection.Request.SecWebSocketKey;
                                            var swka            = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                                            var swkaSha1        = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                                            var swkaSha1Base64  = Convert.ToBase64String(swkaSha1);

                                            // HTTP/1.1 101 Switching Protocols
                                            // Connection:              Upgrade
                                            // Upgrade:                 websocket
                                            // Sec-WebSocket-Accept:    s3pPLMBiTxaQ9kYGzzhZRbK+xOo=
                                            // Sec-WebSocket-Protocol:  ocpp1.6
                                            // Sec-WebSocket-Version:   13
                                            if (httpResponse is null)
                                                httpResponse  = new HTTPResponse.Builder(HTTPStatusCode.SwitchingProtocols) {
                                                                    Server                = HTTPServiceName,
                                                                    Connection            = "Upgrade",
                                                                    Upgrade               = "websocket",
                                                                    SecWebSocketAccept    = swkaSha1Base64,
                                                                    SecWebSocketProtocol  = "ocpp1.6",
                                                                    SecWebSocketVersion   = "13"
                                                                }.AsImmutable;

                                        }

                                        var response = (httpResponse.EntirePDU + "\r\n\r\n").ToUTF8Bytes();

                                        stream.Write(response, 0, response.Length);

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
                                            stream.Close();
                                            stream = null;
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
                                                                                                     WSConnection,
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

                                    #region ...or WebSocket frame

                                    else
                                    {

                                        if (WebSocketFrame.TryParse(bytes,
                                                                    out WebSocketFrame?  frame,
                                                                    out UInt64           frameLength,
                                                                    out String?          errorResponse))
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
                                                                             WSConnection,
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
                                                                                         WSConnection,
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
                                                                                                           WSConnection,
                                                                                                           frame.Payload.ToUTF8String(),
                                                                                                           token2);

                                                            // Incoming higher protocol level respones will not produce another response!
                                                            if (textMessageResponse?.Response is not null)
                                                            {

                                                                responseFrame = new WebSocketFrame(WebSocketFrame.Fin.Final,
                                                                                                   WebSocketFrame.MaskStatus.Off,
                                                                                                   Array.Empty<Byte>(),
                                                                                                   WebSocketFrame.Opcodes.Text,
                                                                                                   textMessageResponse.Response.ToUTF8Bytes(),
                                                                                                   WebSocketFrame.Rsv.Off,
                                                                                                   WebSocketFrame.Rsv.Off,
                                                                                                   WebSocketFrame.Rsv.Off);

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
                                                                                              WSConnection,
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
                                                                                           WSConnection,
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
                                                                                                               WSConnection,
                                                                                                               frame.Payload,
                                                                                                               token2);

                                                            // Incoming higher protocol level respones will not produce another response!
                                                            if (binaryMessageResponse?.Response != null)
                                                            {

                                                                responseFrame = new WebSocketFrame(WebSocketFrame.Fin.Final,
                                                                                                   WebSocketFrame.MaskStatus.Off,
                                                                                                   Array.Empty<Byte>(),
                                                                                                   WebSocketFrame.Opcodes.Text,
                                                                                                   binaryMessageResponse.Response,
                                                                                                   WebSocketFrame.Rsv.Off,
                                                                                                   WebSocketFrame.Rsv.Off,
                                                                                                   WebSocketFrame.Rsv.Off);

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
                                                                                                WSConnection,
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
                                                                                          WSConnection,
                                                                                          frame,
                                                                                          eventTrackingId,
                                                                                          token2);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPingMessageReceived));
                                                        }

                                                        #endregion

                                                        DebugX.Log(nameof(WebSocketServer) + ": Ping received!");

                                                        responseFrame = new WebSocketFrame(WebSocketFrame.Fin.Final,
                                                                                           WebSocketFrame.MaskStatus.Off,
                                                                                           new Byte[] { 0x00, 0x00, 0x00, 0x00 },
                                                                                           WebSocketFrame.Opcodes.Pong,
                                                                                           frame.Payload,
                                                                                           WebSocketFrame.Rsv.Off,
                                                                                           WebSocketFrame.Rsv.Off,
                                                                                           WebSocketFrame.Rsv.Off);

                                                        break;

                                                    #endregion

                                                    #region Pong   message

                                                    case WebSocketFrame.Opcodes.Pong:

                                                        #region OnPongMessageReceived

                                                        try
                                                        {

                                                            OnPongMessageReceived?.Invoke(Timestamp.Now,
                                                                                          this,
                                                                                          WSConnection,
                                                                                          frame,
                                                                                          eventTrackingId,
                                                                                          token2);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPongMessageReceived));
                                                        }

                                                        #endregion

                                                        DebugX.Log(nameof(WebSocketServer) + ": Pong received!");

                                                        break;

                                                    #endregion

                                                    #region Close  message

                                                    case WebSocketFrame.Opcodes.Close:

                                                        #region OnCloseMessage

                                                        try
                                                        {

                                                            OnCloseMessage?.Invoke(Timestamp.Now,
                                                                                   this,
                                                                                   WSConnection,
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

                                                        //webSocketConnections.Remove(WSConnection);
                                                        //stream.Close();
                                                        //stream = null;

                                                        break;

                                                    #endregion

                                                }

                                                if (responseFrame is not null && stream is not null)
                                                {

                                                    try
                                                    {
                                                        stream.Write(responseFrame.ToByteArray());
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        DebugX.LogException(e, "Processing a web socket frame in " + nameof(WebSocketServer));
                                                    }

                                                    #region OnMessageResponse

                                                    try
                                                    {

                                                        if (responseFrame is not null)
                                                            OnMessageResponse?.Invoke(Timestamp.Now,
                                                                                      this,
                                                                                      WSConnection,
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
                                            DebugX.Log("Could not parse the given websocket frame: " + errorResponse);
                                        }

                                    }

                                    #endregion

                                }

                                DebugX.Log(nameof(WebSocketServer), " Connection closed!");

                            }
                            else
                                DebugX.Log(nameof(WebSocketServer), " The given websocket connection is invalid!");

                        }
                        catch (Exception e)
                        {
                            DebugX.Log(nameof(WebSocketServer), " Exception in server thread: " + e.Message + Environment.NewLine + e.StackTrace);
                        }

                    },
                    new WebSocketConnection(this, newTCPClient));

                }

                DebugX.Log(nameof(WebSocketServer), " Stopped server on " + IPSocket.IPAddress + ":" + IPSocket.Port);

            });

            listenerThread.Start();

        }



        public virtual Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime             RequestTimestamp,
                                                                             EventTracking_Id     EventTrackingId,
                                                                             WebSocketConnection  Connection,
                                                                             String               TextMessage,
                                                                             CancellationToken    CancellationToken)
        {
            return null;
        }

        public virtual Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime             RequestTimestamp,
                                                                                 EventTracking_Id     EventTrackingId,
                                                                                 WebSocketConnection  Connection,
                                                                                 Byte[]               BinaryMessage,
                                                                                 CancellationToken    CancellationToken)
        {
            return null;
        }


    }

}
