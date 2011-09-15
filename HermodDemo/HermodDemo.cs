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

#endregion

namespace de.ahzf.Hermod.Demo
{

    //                _Response = Environment.NewLine + "<<<<<<<<<<<<<<<<header>>>>>>>>>>>>>>>>" + Environment.NewLine + HeaderBytes.ToUTF8String() + Environment.NewLine + "<<<<<<<<<<<<<<<<body>>>>>>>>>>>>>>>>" + Environment.NewLine + ResponseBody.ToUTF8String() + Environment.NewLine + "<<<<<<<<<<<<<<<<end>>>>>>>>>>>>>>>>";

    /// <summary>
    /// A simple hermod demo using TCP and HTTP servers.
    /// </summary>
    public class HermodDemo
    {

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

            var _client1     = new HTTPClient(IPv4Address.Localhost, IPPort.HTTP);
            var _request1    = _client1.GET    ("/HelloWorld").
                                        Host   ("localhost").
                                        AddAccept (HTTPContentType.TEXT_UTF8);
            var _request1Str = _request1.ToString();
            Console.WriteLine("Request:" + Environment.NewLine + _request1.HTTPHeader.Aggregate((a, b) => a + Environment.NewLine + b));
            _request1.Execute().
                ContinueWith(RequestTask => { Console.WriteLine("Response: " + Environment.NewLine + RequestTask.Result.ResponseBody.ToUTF8String()); return RequestTask.Result; }).
                ContinueWith(RequestTask => { RequestTask.Result.Accept(HTTPContentType.HTML_UTF8); return RequestTask.Result.Execute().Result; }).
                ContinueWith(RequestTask => { Console.WriteLine("Response: " + Environment.NewLine + RequestTask.Result.ResponseBody.ToUTF8String()); });


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


            var Response = new TCPClientRequest("localhost", 80).Send("GETTT / HTTP/1.1").FinishCurrentRequest().Response;

            Console.ReadLine();
            Console.WriteLine("done!");

        }

    }

}
