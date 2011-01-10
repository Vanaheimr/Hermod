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
using System.Net;

using de.ahzf.Hermod.Sockets.TCP;
using de.ahzf.Hermod.Datastructures;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    ///  This http server will listen on a port and maps incoming urls to methods of HTTPServiceType. 
    /// </summary>
    /// <typeparam name="HTTPServiceType">A http service handler.</typeparam>
    public class HTTPServer<HTTPServiceType> : AHTTPServer<HTTPServiceType>
        where HTTPServiceType : class, IHTTPService, new()
    {

        #region Data

        private readonly TCPServer<HTTPConnection<HTTPServiceType>> _TCPServer;

        #endregion

        #region Properties

        #region IPAdress

        /// <summary>
        /// Gets the IPAddress on which the HTTPServer listens.
        /// </summary>
        public IIPAddress IPAddress
        {
            get
            {
                
                if (_TCPServer != null)
                    return _TCPServer.IPAddress;

                return null;

            }
        }

        #endregion

        #region Port

        /// <summary>
        /// Gets the port on which the HTTPServer listens.
        /// </summary>
        public IPPort Port
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.Port;

                return null;
            
            }
        }

        #endregion

        #region IsRunning

        /// <summary>
        /// True while the HTTPServer is listening for new clients.
        /// </summary>
        public Boolean IsRunning
        {
            get
            {
                
                if (_TCPServer != null)
                    return _TCPServer.IsRunning;

                return false;

            }
        }

        #endregion

        #region StopRequested

        /// <summary>
        /// The HTTPServer was requested to stop and will no
        /// longer accept new client connections
        /// </summary>
        public Boolean StopRequested
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.StopRequested;

                return false;

            }
        }

        #endregion

        #region NumberOfClients

        /// <summary>
        /// The current number of connected clients
        /// </summary>
        public UInt64 NumberOfClients
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.NumberOfClients;

                return 0;

            }
        }

        #endregion

        #region MaxClientConnections

        /// <summary>
        /// The maximum number of pending client connections
        /// </summary>
        public UInt32 MaxClientConnections
        {
            get
            {
                
                if (_TCPServer != null)
                    return _TCPServer.MaxClientConnections;

                return 0;

            }
        }

        #endregion

        #region ClientTimeout

        /// <summary>
        /// Will set the ClientTimeout for all incoming client connections
        /// </summary>
        public UInt32 ClientTimeout
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.ClientTimeout;

                return 0;

            }
        }

        #endregion

        #region DefaultServerName

        private const String _DefaultServerName = "Hermod HTTP Server v0.1";

        /// <summary>
        /// The default server name.
        /// </summary>
        public virtual String DefaultServerName
        {
            get
            {
                return _DefaultServerName;
            }
        }

        #endregion

        #endregion

        #region Events

        //public delegate void ExceptionOccuredHandler(Object mySender, Exception myException);
        //public event ExceptionOccuredHandler OnExceptionOccured;

        public delegate void NewHTTPServiceHandler(HTTPServiceType myHTTPServiceType);
        public event         NewHTTPServiceHandler OnNewHTTPService;

        #endregion

        #region Constructor(s)

        #region HTTPServer()

        /// <summary>
        /// Initialize the HTTPServer using IPAddress.Any, http port 80 and start the server.
        /// </summary>
        public HTTPServer(NewHTTPServiceHandler NewHTTPConnectionHandler = null)
            : this(IPv4Address.Any, IPPort.HTTP, NewHTTPConnectionHandler, true)
        { }

        #endregion

        #region HTTPServer(myPort, NewHTTPServiceHandler = null, myAutoStart = false)

        /// <summary>
        /// Initialize the HTTPServer using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="myPort">The listening port</param>
        /// <param name="Autostart"></param>
        public HTTPServer(IPPort myPort, NewHTTPServiceHandler NewHTTPServiceHandler = null, Boolean Autostart = false)
            : this(IPv4Address.Any, myPort, NewHTTPServiceHandler, Autostart)
        { }

        #endregion

        #region HTTPServer(myIIPAddress, myPort, NewHTTPServiceHandler = null, myAutoStart = false)

        /// <summary>
        /// Initialize the HTTPServer using the given parameters.
        /// </summary>
        /// <param name="myIIPAddress">The listening IP address(es)</param>
        /// <param name="myPort">The listening port</param>
        /// <param name="Autostart"></param>
        public HTTPServer(IIPAddress myIIPAddress, IPPort myPort, NewHTTPServiceHandler NewHTTPServiceHandler = null, Boolean Autostart = false)
        {

            ServerName = _DefaultServerName;

            if (NewHTTPServiceHandler != null)
                OnNewHTTPService += NewHTTPServiceHandler;

            _TCPServer = new TCPServer<HTTPConnection<HTTPServiceType>>(myIIPAddress, myPort, NewHTTPConnection =>
            {

                NewHTTPConnection.ServerName   = ServerName;
                NewHTTPConnection.HTTPSecurity = HTTPSecurity;
                NewHTTPConnection.URLMapping    = _URLMapping;
                NewHTTPConnection.NewHTTPServiceHandler = OnNewHTTPService;

                try
                {
                    NewHTTPConnection.ProcessHTTP();
                }
                catch (Exception e)
                {
                    //ToDo: Do error logging!
                }

            }, false);


            if (Autostart)
                _TCPServer.Start();

        }

        #endregion

        #region HTTPServer(myIPSocket, NewHTTPServiceHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the HTTPServer using the given parameters.
        /// </summary>
        /// <param name="myIPSocket">The listening IPSocket.</param>
        /// <param name="NewHTTPServiceHandler"></param>
        /// <param name="Autostart"></param>
        public HTTPServer(IPSocket myIPSocket, NewHTTPServiceHandler NewHTTPServiceHandler = null, Boolean Autostart = false)
            : this(myIPSocket.IPAddress, myIPSocket.Port, NewHTTPServiceHandler, Autostart)
        { }

        #endregion

        #endregion

        
        #region ToString()

        public override String ToString()
        {

            var _TypeName    = this.GetType().Name;
            var _GenericType = this.GetType().GetGenericArguments()[0].Name;

            var _Running = "";
            if (_TCPServer.IsRunning) _Running = " (running)";

            return String.Concat(_TypeName.Remove(_TypeName.Length - 2), "<", _GenericType, "> ", _TCPServer.IPAddress.ToString(), ":", _TCPServer.Port, _Running);

        }

        #endregion

    }


    #region HTTPServer -> HTTPServer<DefaultHTTPService>

    public class HTTPServer : HTTPServer<DefaultHTTPService>
    {

        #region Constructor(s)

        #region HTTPServer(myPort, NewHTTPConnectionHandler = null, myAutoStart = false)

        /// <summary>
        /// Initialize the HTTPServer using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="myPort">The listening port</param>
        /// <param name="Autostart"></param>
        public HTTPServer(IPPort myPort, NewHTTPServiceHandler NewHTTPConnectionHandler = null, Boolean Autostart = false)
            : this(IPv4Address.Any, myPort, NewHTTPConnectionHandler, Autostart)
        { }

        #endregion

        #region HTTPServer(myIIPAddress, myPort, NewHTTPConnectionHandler = null, myAutoStart = false)

        /// <summary>
        /// Initialize the HTTPServer using the given parameters.
        /// </summary>
        /// <param name="myIIPAddress">The listening IP address(es)</param>
        /// <param name="myPort">The listening port</param>
        /// <param name="Autostart"></param>
        public HTTPServer(IIPAddress myIIPAddress, IPPort myPort, NewHTTPServiceHandler NewHTTPConnectionHandler = null, Boolean Autostart = false)
            : base(myIIPAddress, myPort, NewHTTPConnectionHandler, Autostart)
        { }

        #endregion

        #region HTTPServer(myIPSocket, NewHTTPConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the HTTPServer using the given parameters.
        /// </summary>
        /// <param name="myIPSocket">The listening IPSocket.</param>
        /// <param name="NewHTTPConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public HTTPServer(IPSocket myIPSocket, NewHTTPServiceHandler NewHTTPConnectionHandler = null, Boolean Autostart = false)
            : this(myIPSocket.IPAddress, myIPSocket.Port, NewHTTPConnectionHandler, Autostart)
        { }

        #endregion

        #endregion

        #region ToString()

        public override String ToString()
        {

            var _Running = "";
            if (IsRunning) _Running = " (running)";

            return String.Concat(this.GetType().Name, " ", IPAddress.ToString(), ":", Port, _Running);

        }

        #endregion

    }

    #endregion

}
