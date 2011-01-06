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
using System.Net.Sockets;

using de.ahzf.Hermod.Sockets.TCP;
using de.ahzf.Hermod.Datastructures;
using de.ahzf.Hermod.HTTP;

#endregion

namespace de.ahzf.Hermod.Demo
{

    public class HermodDemo
    {


        #region DemoTCPConnection

        /// <summary>
        /// A class representing a TCP connection
        /// </summary>
        public class DemoTCPConnection : ATCPConnection
        {

            #region Constructor(s)

            #region DemoTCPConnection()

            /// <summary>
            /// Create a new TCPConnection class
            /// </summary>
            public DemoTCPConnection()
            { }

            #endregion

            #region DemoTCPConnection(myTCPClientConnection)

            /// <summary>
            /// Create a new TCPConnection class using the given TcpClient class
            /// </summary>
            public DemoTCPConnection(TcpClient myTCPClientConnection)
                : base(myTCPClientConnection)
            {
                WriteToResponseStream("Hello Demo!" + Environment.NewLine);
            }

            #endregion

            #endregion


            #region Dispose()

            /// <summary>
            /// Dispose this object
            /// </summary>
            public override void Dispose()
            { }

            #endregion

        }

        #endregion


        public class DemoHTTPService : IHTTPService
        {

            public IHTTPConnection IHTTPConnection
            {
                
                get
                {
                    throw new NotImplementedException();
                }
                
                set
                {
                    throw new NotImplementedException();
                }

            }

        }


        public static void Main(String[] args)
        {


            //using (var _WebClient = new WebClient())
            //{

            //    _WebClient.Encoding = Encoding.UTF8;
            //    //_WebClient.Credentials = new NetworkCredential(userName, password);

            //    //Console.WriteLine(Encoding.UTF8.GetString(_WebClient.DownloadData("http://localhost:8080/ikkh4")));
            //    //Console.WriteLine(_WebClient.DownloadString("http://localhost:8080/ikkh4"));

            //    _WebClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(client_DownloadStringCompleted);
            //    _WebClient.DownloadStringAsync(new Uri("http://localhost:8080/ikkh4"));

            //    //_WebClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded");                
            //    //responseContent = _WebClient.UploadValues("http://localhost:8080/TestPage/Default.aspx", "POST", collection);
            //    //Console.Write(Encoding.UTF8.GetString(responseContent));

            //}


            var _TCPServer1 = new TCPServer(new IPv4Address(new Byte[] { 192, 168, 178, 31 }), new IPPort(2001), NewConnection =>
                                               {
                                                   NewConnection.WriteToResponseStream("Hello world (1)!" + Environment.NewLine + Environment.NewLine);
                                                   NewConnection.Close();
                                               }, true);

            Console.WriteLine(_TCPServer1);

            var _TCPServer2 = new TCPServer<DemoTCPConnection>(IPv4Address.Any, new IPPort(2002), NewConnection =>
                                               {
                                                   NewConnection.WriteToResponseStream("Hello world (2)!" + Environment.NewLine + Environment.NewLine);
                                                   NewConnection.Close();
                                               }, true);

            Console.WriteLine(_TCPServer2);

            var _HTTPServer1 = new HTTPServer(IPv4Address.Any, new IPPort(81), Autostart: true)
                {
                    ServerName = "HermodDemo1"
                };

            Console.WriteLine(_HTTPServer1);

            var _HTTPServer2 = new HTTPServer<RESTService>(IPv4Address.Any, IPPort.HTTP, Autostart: true)
            {
                ServerName = "HermodDemo2"
            };

            Console.WriteLine(_HTTPServer2);

            //var _HTTPServer = new TCPServer<HTTPConnection>(IPAddress.Any, 2001);
            //_HTTPServer.OnNewConnection += AcceptConnection =>
            //                      {
            //                          var _AcceptedConnection = ((HTTPConnection<HTTPServiceType>)AcceptConnection);
            //                          //_AcceptedConnection.ServerName = "ServerName";
            //                          //_AcceptedConnection.HTTPSecurity = HTTPSecurity;
            //                          //_AcceptedConnection.URLParser = _URLMapping;
            //                          //_AcceptedConnection.Timeout = Timeout;
            //                      };


            //// Initialize REST service
            //var _RESTService = new HTTPServer<RESTService>(IPAddress.Any, 80, myAutoStart: true)
            //{
            //    //HTTPSecurity = myHttpWebSecurity,
            //    ServerName   = "gera123"
            //};

            Console.ReadLine();
            Console.WriteLine("done!");


        }

    }

}
