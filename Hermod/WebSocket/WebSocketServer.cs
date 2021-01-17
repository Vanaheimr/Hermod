/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public class WebSocketConnection
    {

        #region Data

        private Dictionary<String, String> _HTTPHeaders;

        #endregion

        #region Properties

        public DateTime                                   Created                       { get; }

        public CancellationTokenSource                    CancellationTokenSource       { get; }

        public WebSocketServer                            WebSocketServer               { get; }

        public TcpClient                                  TcpClient                     { get; }

        public IPSocket                                   LocalSocket                   { get; }

        public IPSocket                                   RemoteSocket                  { get; }

        public String                                     HTTPMethod                    { get; internal set; }

        public HTTPPath                                   HTTPPath                      { get; internal set; }

        public String                                     HTTPVersion                   { get; internal set; }


        public IEnumerable<KeyValuePair<String, String>>  HTTPHeaders
                   => _HTTPHeaders;

        #endregion

        #region Constructor(s)

        public WebSocketConnection(WebSocketServer                            WebSocketServer,
                                   TcpClient                                  TcpClient,
                                   IEnumerable<KeyValuePair<String, String>>  HTTPHeaders = null)
        {

            this.Created                  = DateTime.UtcNow;
            this.CancellationTokenSource  = new CancellationTokenSource();
            this.WebSocketServer          = WebSocketServer;
            this.TcpClient                = TcpClient;
            this.LocalSocket              = new IPSocket(TcpClient.Client.LocalEndPoint  as IPEndPoint);
            this.RemoteSocket             = new IPSocket(TcpClient.Client.RemoteEndPoint as IPEndPoint);
            this._HTTPHeaders             = HTTPHeaders != null
                                                ? HTTPHeaders.ToDictionary(kvp => kvp.Key,
                                                                           kvp => kvp.Value)
                                                : new Dictionary<String, String>();

        }

        #endregion


        internal void AddHTTPHeader(String  Key,
                                    String  Value)
        {

            _HTTPHeaders.Add(Key,
                             Value);

        }

        public String GetHTTPHeader(String Key)
        {

            if (_HTTPHeaders.TryGetValue(Key, out String Value))
                return Value;

            return "";

        }


    }


    public struct WebSocketMessageRespose
    {

        public DateTime        ResponseTimestamp    { get; }

        public WebSocketFrame  Response             { get; }

        public DateTime        RequestTimestamp     { get; }

        public WebSocketFrame  Request              { get; }


    }


    public struct WebSocketTextMessageRespose
    {

        public DateTime  RequestTimestamp     { get; }

        public String    Request              { get; }

        public DateTime  ResponseTimestamp    { get; }

        public String    Response             { get; }


        public WebSocketTextMessageRespose(DateTime  RequestTimestamp,
                                           String    Request,
                                           DateTime  ResponseTimestamp,
                                           String    Response)
        {

            this.RequestTimestamp   = RequestTimestamp;
            this.Request            = Request;
            this.ResponseTimestamp  = ResponseTimestamp;
            this.Response           = Response;

        }


    }

    public struct WebSocketBinaryMessageRespose
    {

        public DateTime  ResponseTimestamp    { get; }

        public Byte[]    Response             { get; }

        public DateTime  RequestTimestamp     { get; }

        public Byte[]    Request              { get; }


    }


    public delegate Task<WebSocketMessageRespose>        OnWebSocketMessageDelegate      (DateTime             Timestamp,
                                                                                          WebSocketConnection  Sender,
                                                                                          EventTracking_Id     EventTrackingId,
                                                                                          CancellationToken    CancellationToken,
                                                                                          WebSocketFrame       Message);

    public delegate Task<WebSocketTextMessageRespose>    OnWebSocketTextMessageDelegate  (DateTime             Timestamp,
                                                                                          WebSocketConnection  Sender,
                                                                                          EventTracking_Id     EventTrackingId,
                                                                                          CancellationToken    CancellationToken,
                                                                                          String               TextMessage);

    public delegate Task<WebSocketBinaryMessageRespose>  OnWebSocketBinaryMessageDelegate(DateTime             Timestamp,
                                                                                          WebSocketConnection  Sender,
                                                                                          EventTracking_Id     EventTrackingId,
                                                                                          CancellationToken    CancellationToken,
                                                                                          Byte[]               BinaryMessage);



    public class WebSocketServer
    {




        public List<TcpClient> tcpClients = new List<TcpClient>();




        #region Events

        public event OnWebSocketMessageDelegate        OnMessage;

        public event OnWebSocketTextMessageDelegate    OnTextMessage;

        public event OnWebSocketBinaryMessageDelegate  OnBinaryMessage;

        public event OnWebSocketMessageDelegate        OnPingMessage;

        public event OnWebSocketMessageDelegate        OnPongMessage;

        public event OnWebSocketMessageDelegate        OnCloseMessage;

        #endregion




        public WebSocketServer(System.Net.IPAddress  ip,
                               Int32                 Port = 80)
        {

            var ListenerThread = new Thread(() => {

                var cts    = new CancellationTokenSource();
                var token  = cts.Token;
                var server = new TcpListener(ip, Port);
                server.Start();
                Console.WriteLine("web socket server has started on {0}:{1}, Waiting for a connection...", ip, Port);

                while (!token.IsCancellationRequested)
                {

                    var wsclient = server.AcceptTcpClient();
                    tcpClients.Add(wsclient);

                    Task.Factory.StartNew(context => {

                        try
                        {

                            var WSConnection  = context as WebSocketConnection;
                            var stream        = WSConnection.TcpClient.GetStream();
                            var cts2          = new CancellationTokenSource();
                            var token2        = cts.Token;

                            Console.WriteLine("A new client connected from " + WSConnection.TcpClient.Client.RemoteEndPoint.ToString());

                            while (!token2.IsCancellationRequested)
                            {

                                while (!stream.DataAvailable);
                                while (WSConnection.TcpClient.Available < 4); // match against "GET "

                                var bytes = new Byte[WSConnection.TcpClient.Available];
                                stream.Read(bytes, 0, WSConnection.TcpClient.Available);

                                var data  = Encoding.UTF8.GetString(bytes);

                                if (data.StartsWith("GET "))
                                {

                                    Console.WriteLine("===== Handshaking from client =====\n{0}", data);

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

                                }

                                else
                                {

                                    var             frame             = WebSocketFrame.ParseFrame(bytes);
                                    var             eventTrackingId   = EventTracking_Id.New;
                                    WebSocketFrame  responseFrame     = null;

                                    #region Send OnMessage event

                                    try
                                    {

                                        var OnMessageLocal = OnMessage;

                                        if (OnMessageLocal != null)
                                        {

                                            var responseTask = OnMessageLocal(DateTime.UtcNow,
                                                                              WSConnection,
                                                                              eventTrackingId,
                                                                              token2,
                                                                              frame);

                                            responseTask.Wait(TimeSpan.FromSeconds(60));

                                            if (responseTask.Result.Response != null)
                                            {
                                                responseFrame = responseTask.Result.Response;
                                            }

                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        e.Log(nameof(WebSocketServer) + "." + nameof(OnMessage));
                                    }

                                    #endregion

                                    switch (frame.Opcode)
                                    {

                                        #region Send OnTextMessage   event

                                        case WebSocketFrame.Opcodes.Text:

                                            try
                                            {

                                                var OnTextMessageLocal = OnTextMessage;

                                                if (OnTextMessageLocal != null)
                                                {

                                                    var responseTask = OnTextMessageLocal(DateTime.UtcNow,
                                                                                          WSConnection,
                                                                                          eventTrackingId,
                                                                                          token2,
                                                                                          frame.Payload.ToUTF8String());

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
                                                e.Log(nameof(WebSocketServer) + "." + nameof(OnTextMessage));
                                            }

                                            break;

                                        #endregion

                                        #region Send OnBinaryMessage event

                                        case WebSocketFrame.Opcodes.Binary:

                                            try
                                            {

                                                var OnBinaryMessageLocal = OnBinaryMessage;

                                                if (OnBinaryMessageLocal != null)
                                                {

                                                    var responseTask = OnBinaryMessageLocal(DateTime.UtcNow,
                                                                                            WSConnection,
                                                                                            eventTrackingId,
                                                                                            token2,
                                                                                            frame.Payload);

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
                                                e.Log(nameof(WebSocketServer) + "." + nameof(OnBinaryMessage));
                                            }

                                            break;

                                        #endregion

                                    }


                                    if (responseFrame != null)
                                    {

                                        var response = responseFrame.ToByteArray();

                                        stream.Write(response,
                                                     0,
                                                     response.Length);

                                    }



                                    //if (frame.Opcode == WebSocketFrame.Opcodes.Text)
                                    //{

                                    //    var text = Encoding.UTF8.GetString(frame.Payload);



                                    //    Console.WriteLine("[" + client.Client.RemoteEndPoint.ToString() + " " +
                                    //                            Thread.CurrentThread.ManagedThreadId + "] " +
                                    //                      text);

                                    //    Console.WriteLine();


                                    //    var responseFrame1 = new WebSocketFrame(WebSocketFrame.Fin.Final,
                                    //                                            WebSocketFrame.MaskStatus.Off,
                                    //                                            new byte[4],
                                    //                                            WebSocketFrame.Opcodes.Text,
                                    //                                            ("Hello world!").ToUTF8Bytes(),
                                    //                                            WebSocketFrame.Rsv.Off,
                                    //                                            WebSocketFrame.Rsv.Off,
                                    //                                            WebSocketFrame.Rsv.Off);

                                    //    var response1 = responseFrame1.ToByteArray();

                                    //    stream.Write(response1, 0, response1.Length);


                                    //    var responseFrame2 = new WebSocketFrame(WebSocketFrame.Fin.Final,
                                    //                                            WebSocketFrame.MaskStatus.Off,
                                    //                                            new byte[4],
                                    //                                            WebSocketFrame.Opcodes.Text,
                                    //                                            ("Echo: >>>" + text + "<<<").ToUTF8Bytes(),
                                    //                                            WebSocketFrame.Rsv.Off,
                                    //                                            WebSocketFrame.Rsv.Off,
                                    //                                            WebSocketFrame.Rsv.Off);

                                    //    var response2 = responseFrame2.ToByteArray();

                                    //    stream.Write(response2, 0, response2.Length);

                                    //}

                                    if (frame.Opcode.IsControl())
                                    {

                                        if (frame.FIN    == WebSocketFrame.Fin.More)
                                            Console.WriteLine(">>> A control frame is fragmented!");

                                        if (frame.Opcode == WebSocketFrame.Opcodes.Close)
                                            Console.WriteLine(">>> WebSocket closed!");

                                        if (frame.Opcode == WebSocketFrame.Opcodes.Ping)
                                            Console.WriteLine(">>> WebSocket ping!");

                                        if (frame.Opcode == WebSocketFrame.Opcodes.Pong)
                                            Console.WriteLine(">>> WebSocket pong!");


                                        if (frame.Payload.Length > 125)
                                            Console.WriteLine("A control frame has too long payload length.");

                                    }

                                }

                            }

                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Exception in web socket client thread: " + e.Message + Environment.NewLine + e.StackTrace);
                        }

                    },
                    new WebSocketConnection(this, wsclient));

                }

            });

            ListenerThread.Start();

        }

    }

}
