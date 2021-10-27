/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        private          Thread                     listenerThread;

        private readonly CancellationTokenSource    cancellationTokenSource;

        #endregion

        #region Properties

        public IEnumerable<WebSocketConnection> WebSocketConnections
            => webSocketConnections;


        public IIPAddress IPAddress
            => IPSocket.IPAddress;

        public IPPort IPPort
            => IPSocket.Port;

        public IPSocket   IPSocket     { get; }

        public DNSClient  DNSClient    { get; }


        #endregion

        #region Events

        public event OnNewTCPConnectionDelegate                 OnNewTCPConnection;

        public event OnWebSocketHandshake                       OnWebSocketHandshake;

        public event OnNewWebSocketConnectionDelegate           OnNewWebSocketConnection;


        public event OnWebSocketMessageDelegate                 OnMessage;


        public event OnWebSocketTextMessageRequestDelegate      OnTextMessageRequest;

        public event OnWebSocketTextMessageDelegate             OnTextMessage;

        public event OnWebSocketTextMessageResponseDelegate     OnTextMessageResponse;


        public event OnWebSocketBinaryMessageRequestDelegate    OnBinaryMessageRequest;

        public event OnWebSocketBinaryMessageDelegate           OnBinaryMessage;

        public event OnWebSocketBinaryMessageResponseDelegate   OnBinaryMessageResponse;


        public event OnWebSocketMessageDelegate                 OnPingMessage;

        public event OnWebSocketMessageDelegate                 OnPongMessage;


        public event OnCloseMessageDelegate                     OnCloseMessage;

        #endregion

        #region Constructor(s)

        public WebSocketServer(IIPAddress  IPAddress   = null,
                               IPPort?     Port        = null,
                               DNSClient   DNSClient   = null,
                               Boolean     AutoStart   = false)

            : this(new IPSocket(IPAddress ?? IPv4Address.Any,
                                Port      ?? IPPort.HTTP),
                   DNSClient,
                   AutoStart)

        { }

        public WebSocketServer(IPSocket   IPSocket,
                               DNSClient  DNSClient   = null,
                               Boolean    AutoStart   = false)
        {

            this.IPSocket                 = IPSocket;
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

                            var WSConnection     = context as WebSocketConnection;
                            webSocketConnections.Add(WSConnection);
                            var stream           = WSConnection.TcpClient.GetStream();
                            stream.ReadTimeout   = 20000;
                            stream.WriteTimeout  = 1000;
                            var cts2             = CancellationTokenSource.CreateLinkedTokenSource(token);
                            var token2           = cts2.Token;

                            #region Send OnNewTCPConnection event

                            try
                            {

                                var OnNewTCPConnectionLocal = OnNewTCPConnection;

                                if (OnNewTCPConnectionLocal != null)
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

                            while (!token2.IsCancellationRequested && stream != null)
                            {

                                while (!stream.DataAvailable);                //ToDo: Might be an infinite loop
                                while (WSConnection.TcpClient.Available < 4); // match against "GET "

                                var bytes = new Byte[WSConnection.TcpClient.Available];
                                stream.Read(bytes, 0, WSConnection.TcpClient.Available);

                                var data  = Encoding.UTF8.GetString(bytes, 0, 4);

                                #region Web socket handshake...

                                if (data == "GET ")
                                {

                                    var lines     = bytes.ToUTF8String().
                                                          Split(new String[] { "\r\n" },
                                                                StringSplitOptions.None).
                                                          Where(line => line?.Trim().IsNotNullOrEmpty() == true).
                                                          ToArray();

                                    var HTTPInfo  = lines[0].Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                    WSConnection.HTTPMethod   = HTTPInfo[0];
                                    WSConnection.HTTPPath     = HTTPPath.Parse(HTTPInfo[1]);
                                    WSConnection.HTTPVersion  = HTTPInfo[2];

                                    foreach (var element in lines.Skip(1).
                                                                  Select(line => line.Split(new Char[] { ':' }, 2, StringSplitOptions.None)))
                                    {
                                        if (element[0].Trim().IsNotNullOrEmpty())
                                            WSConnection.AddHTTPHeader(element[0]. Trim(),
                                                                       element[1]?.Trim());
                                    }


                                    // GET /webServices/ocpp/CP3211 HTTP/1.1
                                    // Host: some.server.com:33033
                                    // Upgrade: websocket
                                    // Connection: Upgrade
                                    // Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==
                                    // Sec-WebSocket-Protocol: ocpp1.6, ocpp1.5
                                    // Sec-WebSocket-Version: 13

                                    // HTTP/1.1 101 Switching Protocols
                                    // Connection: Upgrade
                                    // Upgrade: websocket
                                    // Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=
                                    // Sec-WebSocket-Protocol: ocpp1.6


                                    // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                                    // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                                    // 3. Compute SHA-1 and Base64 hash of the new value
                                    // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                                    var swk             = WSConnection.GetHTTPHeader("Sec-WebSocket-Key");
                                    var swka            = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                                    var swkaSha1        = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                                    var swkaSha1Base64  = Convert.ToBase64String(swkaSha1);

                                    #region OnWebSocketHandshake

                                    HTTPResponse httpResponse = null;

                                    var OnWebSocketHandshakeLocal = OnWebSocketHandshake;
                                    if (OnWebSocketHandshakeLocal != null)
                                    {

                                        httpResponse  = await OnWebSocketHandshakeLocal(Timestamp.Now,
                                                                                        this,
                                                                                        WSConnection,
                                                                                        EventTracking_Id.New,
                                                                                        token2);

                                    }

                                    #endregion

                                    if (httpResponse is null)
                                        httpResponse  = new HTTPResponse.Builder(HTTPStatusCode.SwitchingProtocols) {
                                                            Connection          = "Upgrade",
                                                            Upgrade             = "websocket",
                                                            SecWebSocketAccept  = swkaSha1Base64
                                                        }.AsImmutable;

                                    var response = (httpResponse.EntirePDU + "\r\n\r\n").ToUTF8Bytes();

                                    stream.Write(response, 0, response.Length);


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

                                            if (OnNewWebSocketConnectionLocal != null)
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

                                    }

                                }

                                #endregion

                                #region ...or WebSocket frame

                                else
                                {

                                    var            frame            = WebSocketFrame.Parse(bytes);
                                    var            eventTrackingId  = EventTracking_Id.New;
                                    WebSocketFrame responseFrame    = null;

                                    #region Send OnMessage event

                                    try
                                    {

                                        var OnMessageLocal = OnMessage;

                                        if (OnMessageLocal != null)
                                        {

                                            var responseTask = OnMessageLocal(Timestamp.Now,
                                                                              this,
                                                                              WSConnection,
                                                                              frame,
                                                                              eventTrackingId,
                                                                              token2);

                                            responseTask.Wait(TimeSpan.FromSeconds(60));

                                            if (responseTask.Result.Response != null)
                                            {
                                                responseFrame = responseTask.Result.Response;
                                            }

                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnMessage));
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
                                                                             WSConnection,
                                                                             frame.Payload.ToUTF8String(),
                                                                             eventTrackingId,
                                                                             token2);

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessageRequest));
                                            }

                                            #endregion

                                            #region OnTextMessage

                                            try
                                            {

                                                var OnTextMessageLocal = OnTextMessage;
                                                if (OnTextMessageLocal != null)
                                                {

                                                    var responseTask = OnTextMessageLocal(Timestamp.Now,
                                                                                          this,
                                                                                          WSConnection,
                                                                                          frame.Payload.ToUTF8String(),
                                                                                          eventTrackingId,
                                                                                          token2);

                                                    responseTask.Wait(TimeSpan.FromSeconds(60));

                                                    if (responseTask.Result.Response != null)
                                                    {

                                                        responseFrame = new WebSocketFrame(WebSocketFrame.Fin.Final,
                                                                                           WebSocketFrame.MaskStatus.Off,
                                                                                           new Byte[4],
                                                                                           WebSocketFrame.Opcodes.Text,
                                                                                           responseTask.Result.Response.ToUTF8Bytes(),
                                                                                           WebSocketFrame.Rsv.Off,
                                                                                           WebSocketFrame.Rsv.Off,
                                                                                           WebSocketFrame.Rsv.Off);

                                                    }

                                                }

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessage));
                                            }

                                            #endregion

                                            #region OnTextMessageResponse

                                            try
                                            {

                                                OnTextMessageResponse?.Invoke(Timestamp.Now,
                                                                              this,
                                                                              WSConnection,
                                                                              frame.Payload.ToUTF8String(),
                                                                              responseFrame.Payload.ToUTF8String(),
                                                                              eventTrackingId,
                                                                              token2);

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
                                                                               frame.Payload,
                                                                               eventTrackingId,
                                                                               token2);

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnBinaryMessageRequest));
                                            }

                                            #endregion

                                            #region OnBinaryMessage

                                            try
                                            {

                                                var OnBinaryMessageLocal = OnBinaryMessage;

                                                if (OnBinaryMessageLocal != null)
                                                {

                                                    var responseTask = OnBinaryMessageLocal(Timestamp.Now,
                                                                                            this,
                                                                                            WSConnection,
                                                                                            frame.Payload,
                                                                                            eventTrackingId,
                                                                                            token2);

                                                    responseTask.Wait(TimeSpan.FromSeconds(60));

                                                    if (responseTask.Result.Response != null)
                                                    {

                                                        responseFrame = new WebSocketFrame(WebSocketFrame.Fin.Final,
                                                                                           WebSocketFrame.MaskStatus.Off,
                                                                                           new Byte[4],
                                                                                           WebSocketFrame.Opcodes.Text,
                                                                                           responseTask.Result.Response,
                                                                                           WebSocketFrame.Rsv.Off,
                                                                                           WebSocketFrame.Rsv.Off,
                                                                                           WebSocketFrame.Rsv.Off);

                                                    }

                                                }

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnBinaryMessage));
                                            }

                                            #endregion

                                            #region OnBinaryMessageResponse

                                            try
                                            {

                                                OnBinaryMessageResponse?.Invoke(Timestamp.Now,
                                                                                this,
                                                                                WSConnection,
                                                                                frame.Payload,
                                                                                responseFrame.Payload,
                                                                                eventTrackingId,
                                                                                token2);

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

                                            try
                                            {

                                                //var OnCloseMessageLocal = OnCloseMessage;

                                                //if (OnCloseMessageLocal != null)
                                                //{

                                                //    var responseTask = OnCloseMessageLocal(Timestamp.Now,
                                                //                                           this,
                                                //                                           WSConnection,
                                                //                                           frame,
                                                //                                           eventTrackingId,
                                                //                                           token2);

                                                //    responseTask.Wait(TimeSpan.FromSeconds(10));

                                                //}

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessage));
                                            }

                                            break;

                                        #endregion

                                        #region Pong   message

                                        case WebSocketFrame.Opcodes.Pong:

                                            try
                                            {

                                                //var OnCloseMessageLocal = OnCloseMessage;

                                                //if (OnCloseMessageLocal != null)
                                                //{

                                                //    var responseTask = OnCloseMessageLocal(Timestamp.Now,
                                                //                                           this,
                                                //                                           WSConnection,
                                                //                                           frame,
                                                //                                           eventTrackingId,
                                                //                                           token2);

                                                //    responseTask.Wait(TimeSpan.FromSeconds(10));

                                                //}

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessage));
                                            }

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

                                            webSocketConnections.Remove(WSConnection);
                                            stream.Close();
                                            stream = null;

                                            break;

                                        #endregion

                                    }

                                    if (responseFrame != null && stream != null)
                                        stream.Write(responseFrame.ToByteArray());

                                    //if (frame.Opcode.IsControl())
                                    //{

                                    //    if (frame.FIN    == WebSocketFrame.Fin.More)
                                    //        Console.WriteLine(">>> A control frame is fragmented!");

                                    //    if (frame.Payload.Length > 125)
                                    //        Console.WriteLine("A control frame has too long payload length.");

                                    //}

                                }

                                #endregion

                            }

                            DebugX.Log("WebSocket connection closed!");

                        }
                        catch (Exception e)
                        {
                            DebugX.Log("Exception in WebSocket client thread: " + e.Message + Environment.NewLine + e.StackTrace);
                        }

                    },
                    new WebSocketConnection(this, newTCPClient));

                }

                DebugX.Log("WebSocket server on " + IPSocket.IPAddress + ":" + IPSocket.Port + " stopped!");

            });

            listenerThread.Start();

        }

    }

}
