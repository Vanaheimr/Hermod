/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets
{

    /// <summary>
    /// An abstract local/remote socket combination.
    /// </summary>
    public abstract class AReadOnlyLocalRemoteSockets : IDisposable
    {

        #region Properties

        #region LocalIPAddress

        /// <summary>
        /// The local IP address.
        /// </summary>
        public IIPAddress LocalIPAddress
        {
            get
            {
                return _LocalSocket.IPAddress;
            }
        }

        #endregion

        #region LocalPort

        /// <summary>
        /// The local IP port.
        /// </summary>
        public IPPort LocalPort
        {
            get
            {
                return LocalSocket.Port;
            }
        }

        #endregion

        #region LocalSocket

        private readonly IPSocket _LocalSocket;

        /// <summary>
        /// The local socket.
        /// </summary>
        public IPSocket LocalSocket
        {
            get
            {
                return _LocalSocket;
            }
        }

        #endregion


        #region RemoteIPAddress

        /// <summary>
        /// The remote IP address.
        /// </summary>
        public IIPAddress RemoteIPAddress
        {
            get
            {
                return _RemoteSocket.IPAddress;
            }
        }

        #endregion

        #region RemotePort

        /// <summary>
        /// The remote IP port.
        /// </summary>
        public IPPort RemotePort
        {
            get
            {
                return _RemoteSocket.Port;
            }
        }

        #endregion

        #region RemoteSocket

        private readonly IPSocket _RemoteSocket;

        /// <summary>
        /// The remote socket.
        /// </summary>
        public IPSocket RemoteSocket
        {
            get
            {
                return _RemoteSocket;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract local/remote socket combination.
        /// </summary>
        /// <param name="LocalSocket">The local socket.</param>
        /// <param name="RemoteSocket">The remote socket.</param>
        public AReadOnlyLocalRemoteSockets(IPSocket LocalSocket, IPSocket RemoteSocket)
        {
            this._LocalSocket   = LocalSocket;
            this._RemoteSocket  = RemoteSocket;
        }

        #endregion


        #region IDisposable Members

        /// <summary>
        /// Dispose this packet.
        /// </summary>
        public virtual void Dispose()
        { }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return LocalSocket.ToString() + " [local] <-> " + RemoteSocket.ToString() + " [remote]";
        }

        #endregion

    }

}
