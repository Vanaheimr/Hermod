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

using de.ahzf.Hermod.HTTP;
using de.ahzf.Hermod.Sockets.TCP;
using de.ahzf.Hermod.Datastructures;

#endregion

namespace de.ahzf.Hermod.Demo
{

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

            var _TCPServer1 = new TCPServer(new IPv4Address(new Byte[] { 192, 168, 178, 31 }), new IPPort(2001), NewConnection =>
                                               {
                                                   NewConnection.WriteToResponseStream("Hello world!" + Environment.NewLine + Environment.NewLine);
                                                   NewConnection.Close();
                                               }, true);

            Console.WriteLine(_TCPServer1);

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
            var _HTTPServer2 = new HTTPServer<RESTService>(IPv4Address.Localhost, IPPort.HTTP, Autostart: true)
                                   {
                                       ServerName = "Customized Hermod Demo"
                                   };

            Console.WriteLine(_HTTPServer2);

            #endregion

            Console.ReadLine();
            Console.WriteLine("done!");

        }

    }

}
