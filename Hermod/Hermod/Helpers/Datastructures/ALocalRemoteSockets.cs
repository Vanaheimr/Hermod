/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets
{

    /// <summary>
    /// An abstract class for all TCP streams and UDP packets.
    /// </summary>
    public abstract class ALocalRemoteSockets : IDisposable
    {

        #region Properties

        #region LocalIPAddress

        /// <summary>
        /// The local IP address.
        /// </summary>
        public IIPAddress LocalIPAddress
            => LocalSocket.IPAddress;

        #endregion

        #region LocalPort

        /// <summary>
        /// The local IP port.
        /// </summary>
        public IPPort LocalPort
            => LocalSocket.Port;

        #endregion

        #region LocalSocket

        private IPSocket localSocket;

        /// <summary>
        /// The local socket.
        /// </summary>
        public IPSocket LocalSocket
        {

            get
            {
                return localSocket;
            }

            set
            {
                localSocket = value;
            }

        }

        #endregion


        #region RemoteIPAddress

        /// <summary>
        /// The remote IP address.
        /// </summary>
        public IIPAddress RemoteIPAddress
            => remoteSocket.IPAddress;

        #endregion

        #region RemotePort

        /// <summary>
        /// The remote IP port.
        /// </summary>
        public IPPort RemotePort
            => remoteSocket.Port;

        #endregion

        #region RemoteSocket

        private IPSocket remoteSocket;

        /// <summary>
        /// The remote socket.
        /// </summary>
        public IPSocket RemoteSocket
        {

            get
            {
                return remoteSocket;
            }

            set
            {
                remoteSocket = value;
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region ALocalRemoteSockets()

        /// <summary>
        /// Create a new abstract ALocalRemoteSockets.
        /// </summary>
        public ALocalRemoteSockets()
        { }

        #endregion

        #region ALocalRemoteSockets(LocalSocket, RemoteSocket)

        /// <summary>
        /// Create a new abstract ALocalRemoteSockets.
        /// </summary>
        /// <param name="LocalSocket">The local socket of this UDP packet.</param>
        /// <param name="RemoteSocket">The remote socket of this UDP packet.</param>
        public ALocalRemoteSockets(IPSocket LocalSocket, IPSocket RemoteSocket)
        {
            this.LocalSocket   = LocalSocket;
            this.RemoteSocket  = RemoteSocket;
        }

        #endregion

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
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => $"{LocalSocket} [local] <-> {RemoteSocket} [remote]";

        #endregion

    }

}
