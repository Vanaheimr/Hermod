/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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
using System.Threading;

using de.ahzf.Hermod.HTTP;
using de.ahzf.Hermod.Sockets.TCP;
using de.ahzf.Hermod.Datastructures;
using de.ahzf.Hermod.UnitTests;
using de.ahzf.Hermod.Sockets.UDP;

#endregion

namespace de.ahzf.Hermod.Demo
{

    //                _Response = Environment.NewLine + "<<<<<<<<<<<<<<<<header>>>>>>>>>>>>>>>>" + Environment.NewLine + HeaderBytes.ToUTF8String() + Environment.NewLine + "<<<<<<<<<<<<<<<<body>>>>>>>>>>>>>>>>" + Environment.NewLine + ResponseBody.ToUTF8String() + Environment.NewLine + "<<<<<<<<<<<<<<<<end>>>>>>>>>>>>>>>>";

    /// <summary>
    /// A simple hermod demo using TCP and HTTP servers.
    /// </summary>
    public class HermodDemo
    {

        private static void WriteRequest(HTTPRequestBuilder Request)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Request:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(Request.EntireRequestHeader + Environment.NewLine);
        }

        private static void WriteResponse(HTTPResponse Response)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Response:");
            Console.WriteLine(Response.RawHTTPHeader + Environment.NewLine);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(Response.Content.ToUTF8String() + Environment.NewLine);
        }

        /// <summary>
        /// Main.
        /// </summary>
        /// <param name="myArgs">The arguments.</param>
        public static void Main(String[] myArgs)
        {

            #region Start TCPServers

            //var _TCPServer1 = new TCPServer(new IPv4Address(new Byte[] { 192, 168, 178, 31 }), new IPPort(2001), NewConnection =>
            //                                   {
            //                                       NewConnection.WriteToResponseStream("Hello world!" + Environment.NewLine + Environment.NewLine);
            //                                       NewConnection.Close();
            //                                   }, true);

            //Console.WriteLine(_TCPServer1);

            // The first line of the repose will be served from the CustomTCPConnection handler.
            var _TCPServer2 = new TCPServer<CustomTCPConnection>(IPv4Address.Any, new IPPort(2002), NewConnection =>
                                               {
                                                   NewConnection.WriteToResponseStream("...world!" + Environment.NewLine + Environment.NewLine);
                                                   NewConnection.Close();
                                               }, true);

            Console.WriteLine(_TCPServer2);

            #endregion

            #region Start HTTPServers

            // Although the socket listens on IPv4Address.Any the service is
            // configured to serve clients only on http://localhost:8181
            // More details within DefaultHTTPService.cs
            var _HTTPServer1 = new HTTPServer(IPv4Address.Any, new IPPort(8181), Autostart: true)
                                   {
                                       ServerName = "Default Hermod Demo"
                                   };

            Console.WriteLine(_HTTPServer1);

            // This service uses a custom HTTPService defined within IRESTService.cs
            var _HTTPServer2 = new HTTPServer<IRESTService>(IPv4Address.Any, IPPort.HTTP, Autostart: true)
                                   {
                                       ServerName = "Customized Hermod Demo"
                                   };

            Console.WriteLine(_HTTPServer2);

            #endregion

            #region UDP Servers

            var _UDPServer1 = new UDPServer(new IPPort(5555), NewPacket =>
            {
                //NewPacket.Data = new Byte[10];
             //   NewPacket.Close();
            }, true);

            #endregion

            var _client1     = new HTTPClient(IPv4Address.Localhost, IPPort.HTTP);

            var _request0    = _client1.GET("/HelloWorld").
                                        SetHost("localhorst").
                                        AddAccept(HTTPContentType.TEXT_UTF8, 1);

            var _request1    = _client1.GET("/HelloWorld").
                                        SetHost("localhorst").
                                        AddAccept(HTTPContentType.HTML_UTF8, 1);

            //WriteRequest(_request0.EntireRequestHeader);

            //_client1.Execute(_request0, response => WriteResponse(response.Content.ToUTF8String())).
            //         ContinueWith(HTTPClient => { WriteRequest(_request1.EntireRequestHeader); return HTTPClient.Result; }).
            //         ContinueWith(HTTPClient => HTTPClient.Result.Execute(_request1, response => WriteResponse(response.Content.ToUTF8String()))).
            //         Wait();

            var _client2 = new HTTPClient(IPv4Address.Parse("188.40.47.229"), IPPort.HTTP);
            var _requestA = _client2.GET("/").
                                     SetProtocolVersion(HTTPVersion.HTTP_1_1).
                                     SetHost("www.ahzf.de").
                                     SetUserAgent("Hermod HTTP Client v0.1").
                                     SetConnection("keep-alive").
                                     AddAccept(HTTPContentType.HTML_UTF8, 1);

            var _requestB = _client2.GET("/nfgj").
                                     SetProtocolVersion(HTTPVersion.HTTP_1_1).
                                     SetHost("www.ahzf.de").
                                     SetUserAgent("Hermod HTTP Client v0.1").
                                     SetConnection("keep-alive").
                                     AddAccept(HTTPContentType.HTML_UTF8, 1);

            WriteRequest(_requestA);
            _client2.Execute(_requestA, response => WriteResponse(response)).
                     ContinueWith(Client => Client.Result.Execute(_requestB, response => WriteResponse(response)));


            var _req23a = new HTTPRequestBuilder().
                              SetHTTPMethod      (HTTPMethod.GET).
                              SetProtocolName    ("µHTTP").
                              SetProtocolVersion (new HTTPVersion(2, 0)).
                              SetHost            ("localhorst").
                              SetUserAgent       ("Hermod µHTTP Client").
                              SetContent         ("This the HTTP content...");

            _req23a.QueryString.Add("name",   "alice").
                                Add("friend", "bob").
                                Add("friend", "carol");

            var _req23b = new HTTPRequestBuilder() {
                              HTTPMethod        = HTTPMethod.GET,
                              ProtocolName      = "µHTTP",
                              ProtocolVersion   = new HTTPVersion(2, 0),
                              Host              = "localhorst",
                              UserAgent         = "Hermod µHTTP Client",
                              Content           = "This the HTTP content...".ToUTF8Bytes()
                          };


//            var Response = new TCPClientRequest("localhost", 80).Send("GETTT / HTTP/1.1").FinishCurrentRequest().Response;

            Console.ReadLine();
            Console.WriteLine("done!");

        }

    }

}
