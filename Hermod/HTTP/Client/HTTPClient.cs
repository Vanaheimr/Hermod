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
    /// A http client.
    /// </summary>
    public class HTTPClient
    {

        #region Properties

        #region Hostname

        /// <summary>
        /// The Hostname to which the HTTPClient connects.
        /// </summary>
        public String Hostname { get; private set; }

        #endregion

        #region IPAdress

        /// <summary>
        /// The IPAddress to which the HTTPClient connects.
        /// </summary>
        public IIPAddress IPAddress { get; private set; }

        #endregion

        #region Port

        /// <summary>
        /// The port to which the HTTPClient connects.
        /// </summary>
        public IPPort Port { get; private set; }

        #endregion

        #region ClientTimeout

        /// <summary>
        /// Will set the ClientTimeout for all incoming client connections
        /// </summary>
        public UInt32 ClientTimeout { get; private set; }

        #endregion

        #region KeepAlive

        public UInt32 KeepAlive { get; set; }

        #endregion

        #region DefaultClientName

        private const String _DefaultClientName = "Hermod HTTP Client v0.1";

        /// <summary>
        /// The default server name.
        /// </summary>
        public virtual String DefaultClientName
        {
            get
            {
                return _DefaultClientName;
            }
        }

        #endregion

        #endregion

        #region Events

        public delegate void NewHTTPServiceHandler(String myHTTPServiceType);
        public event         NewHTTPServiceHandler OnNewHTTPService;

        #endregion

        #region Constructor(s)

        #region HTTPClient()

        /// <summary>
        /// Create a new HTTP client.
        /// </summary>
        public HTTPClient()
        { }

        #endregion

        #region HTTPClient(IPAddress, Port)

        /// <summary>
        /// Create a new HTTP client.
        /// </summary>
        public HTTPClient(IIPAddress IPAddress, IPPort Port)
        {
            this.IPAddress = IPAddress;
            this.Port      = Port;
        }

        #endregion
                     
        #endregion


        #region SetKeepAlive(KeepAlive)

        /// <summary>
        /// Set the keep-alive value.
        /// </summary>
        /// <param name="KeepAlive">A keep-alive value.</param>
        public HTTPClient SetKeepAlive(UInt32 KeepAlive)
        {
            this.KeepAlive = KeepAlive;
            return this;
        }

        #endregion



        #region CreateRequest(HTTPMethod, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A new HTTPRequest object.</returns>
        public HTTPRequest CreateRequest(HTTPMethod HTTPMethod, String URLPattern = "/")
        {
            return new HTTPRequest(this, HTTPMethod, URLPattern);
        }

        #endregion


        public void Close()
        {
        }


        #region ToString()

        public override String ToString()
        {

            var _TypeName    = this.GetType().Name;
            var _GenericType = this.GetType().GetGenericArguments()[0].Name;

            return String.Concat(IPAddress.ToString(), ":", Port);

        }

        #endregion

    }

}
