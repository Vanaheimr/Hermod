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

        public event OnNewTCPConnectionDelegate        OnNewTCPConnection;

        public event OnNewWebSocketConnectionDelegate  OnNewWebSocketConnection;

        public event OnWebSocketMessageDelegate        OnMessage;

        public event OnWebSocketTextMessageDelegate    OnTextMessage;

        public event OnWebSocketBinaryMessageDelegate  OnBinaryMessage;

        public event OnWebSocketMessageDelegate        OnPingMessage;

        public event OnWebSocketMessageDelegate        OnPongMessage;

        public event OnCloseMessageDelegate            OnCloseMessage;

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

                    Task.Factory.StartNew(context => {

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

                                    var responseTask = OnNewTCPConnectionLocal(DateTime.UtcNow,
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

                            while (!token2.IsCancellationRequested)
                            {

                                while (!stream.DataAvailable);                //ToDo: Might be an infinite loop
                                while (WSConnection.TcpClient.Available < 4); // match against "GET "

                                var bytes = new Byte[WSConnection.TcpClient.Available];
                                stream.Read(bytes, 0, WSConnection.TcpClient.Available);

                                var data  = Encoding.UTF8.GetString(bytes);

                                #region Web socket handshake...

                                if (data.StartsWith("GET "))
                                {

                                    //Console.WriteLine("===== Handshaking from client =====\n{0}", data);

                                    var lines     = data.Split(new String[] { "\r\n" },
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
                                    // Upgrade: websocket
                                    // Connection: Upgrade
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

                                    // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                                    var response        = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols\r\n" +
                                                                                 "Connection: Upgrade\r\n" +
                                                                                 "Upgrade: websocket\r\n" +
                                                                                 "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                                    stream.Write(response, 0, response.Length);

                                    #region Send OnNewWebSocketConnection event

                                    try
                                    {

                                        var OnNewWebSocketConnectionLocal = OnNewWebSocketConnection;

                                        if (OnNewWebSocketConnectionLocal != null)
                                        {

                                            var responseTask = OnNewWebSocketConnectionLocal(DateTime.UtcNow,
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

                                }

                                #endregion

                                #region ...or WebSocket frame

                                else
                                {

                                    var             frame             = WebSocketFrame.Parse(bytes);
                                    var             eventTrackingId   = EventTracking_Id.New;
                                    WebSocketFrame  responseFrame     = null;

                                    #region Send OnMessage event

                                    try
                                    {

                                        var OnMessageLocal = OnMessage;

                                        if (OnMessageLocal != null)
                                        {

                                            var responseTask = OnMessageLocal(DateTime.UtcNow,
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

                                            try
                                            {

                                                var OnTextMessageLocal = OnTextMessage;

                                                if (OnTextMessageLocal != null)
                                                {

                                                    var responseTask = OnTextMessageLocal(DateTime.UtcNow,
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

                                            break;

                                        #endregion

                                        #region Binary message

                                        case WebSocketFrame.Opcodes.Binary:

                                            try
                                            {

                                                var OnBinaryMessageLocal = OnBinaryMessage;

                                                if (OnBinaryMessageLocal != null)
                                                {

                                                    var responseTask = OnBinaryMessageLocal(DateTime.UtcNow,
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

                                            break;

                                        #endregion

                                        #region Ping   message

                                        case WebSocketFrame.Opcodes.Ping:

                                            try
                                            {

                                                //var OnCloseMessageLocal = OnCloseMessage;

                                                //if (OnCloseMessageLocal != null)
                                                //{

                                                //    var responseTask = OnCloseMessageLocal(DateTime.UtcNow,
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

                                                //    var responseTask = OnCloseMessageLocal(DateTime.UtcNow,
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

                                            try
                                            {

                                                webSocketConnections.Remove(WSConnection);

                                                var OnCloseMessageLocal = OnCloseMessage;

                                                if (OnCloseMessageLocal != null)
                                                {

                                                    var responseTask = OnCloseMessageLocal(DateTime.UtcNow,
                                                                                           this,
                                                                                           WSConnection,
                                                                                           frame,
                                                                                           eventTrackingId,
                                                                                           token2);

                                                    responseTask.Wait(TimeSpan.FromSeconds(10));

                                                }

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessage));
                                            }

                                            break;

                                        #endregion

                                    }


                                    if (responseFrame != null)
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
