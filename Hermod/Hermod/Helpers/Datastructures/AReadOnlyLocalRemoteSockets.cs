/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// An abstract local/remote socket combination.
    /// </summary>
    public abstract class AReadOnlyLocalRemoteSockets
    {

        #region Properties

        /// <summary>
        /// The local IP address.
        /// </summary>
        public IIPAddress   LocalIPAddress
            => LocalSocket.IPAddress;

        /// <summary>
        /// The local IP port.
        /// </summary>
        public IPPort       LocalPort
            => LocalSocket.Port;

        /// <summary>
        /// The local socket.
        /// </summary>
        public IPSocket     LocalSocket     { get; }



        /// <summary>
        /// The remote IP address.
        /// </summary>
        public IIPAddress   RemoteIPAddress
            => RemoteSocket.IPAddress;

        /// <summary>
        /// The remote IP port.
        /// </summary>
        public IPPort       RemotePort
            => RemoteSocket.Port;

        /// <summary>
        /// The remote socket.
        /// </summary>
        public IPSocket     RemoteSocket    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract local/remote socket combination.
        /// </summary>
        /// <param name="LocalSocket">The local socket.</param>
        /// <param name="RemoteSocket">The remote socket.</param>
        public AReadOnlyLocalRemoteSockets(IPSocket LocalSocket,
                                           IPSocket RemoteSocket)
        {
            this.LocalSocket   = LocalSocket;
            this.RemoteSocket  = RemoteSocket;
        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {
            return LocalSocket.ToString() + " [local] <-> " + RemoteSocket.ToString() + " [remote]";
        }

        #endregion


    }

}
