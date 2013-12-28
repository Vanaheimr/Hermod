/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

namespace eu.Vanaheimr.Hermod.Sockets.UDP
{

    /// <summary>
    /// A generic UDP packet.
    /// </summary>
    /// <typeparam name="TData">The type of the message/payload.</typeparam>
    public class UDPPacket<TData> : AReadOnlyLocalRemoteSockets
    {

        #region Properties

        #region ServerTimestamp

        private DateTime _ServerTimestamp;

        /// <summary>
        /// The timestamp of the packet.
        /// </summary>
        public DateTime ServerTimestamp
        {
            get
            {
                return _ServerTimestamp;
            }
        }

        #endregion

        #region Payload

        private TData _Payload;

        /// <summary>
        /// The message/payload of the packet.
        /// </summary>
        public TData Payload
        {
            get
            {
                return _Payload;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region UDPPacket(LocalSocket, RemoteSocket, Payload)

        /// <summary>
        /// Create a new UDP packet.
        /// </summary>
        /// <param name="LocalSocket">The local IP socket.</param>
        /// <param name="RemoteSocket">The remote IP socket.</param>
        /// <param name="Payload">The message/payload of the packet.</param>
        public UDPPacket(IPSocket  LocalSocket,
                         IPSocket  RemoteSocket,
                         TData     Payload)

            : this(DateTime.Now, LocalSocket, RemoteSocket, Payload)

        { }

        #endregion

        #region UDPPacket(ServerTimestamp, LocalSocket, RemoteSocket, Payload)

        /// <summary>
        /// Create a new UDP packet.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the packet.</param>
        /// <param name="LocalSocket">The local IP socket.</param>
        /// <param name="RemoteSocket">The remote IP socket.</param>
        /// <param name="Payload">The message/payload of the packet.</param>
        public UDPPacket(DateTime  ServerTimestamp,
                         IPSocket  LocalSocket,
                         IPSocket  RemoteSocket,
                         TData     Payload)

            : base(LocalSocket, RemoteSocket)

        {
            this._ServerTimestamp  = ServerTimestamp;
            this._Payload          = Payload;
        }

        #endregion

        #endregion

    }

}
