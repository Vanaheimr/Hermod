/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.UDP
{

    /// <summary>
    /// A generic UDP packet.
    /// </summary>
    /// <typeparam name="TData">The type of the message/payload.</typeparam>
    public class UDPPacket<TData> : AReadOnlyLocalRemoteSockets
    {

        #region Properties

        #region UDPReceiver

        private readonly UDPReceiver<TData> _UDPReceiver;

        /// <summary>
        /// The associated UDP receiver.
        /// </summary>
        public UDPReceiver<TData> UDPReceiver
        {
            get
            {
                return _UDPReceiver;
            }
        }

        #endregion

        #region ServerTimestamp

        private DateTime _ServerTimestamp;

        /// <summary>
        /// The timestamp of the packet arrival at the UDP receiver.
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
        /// The message/payload of the UDP packet.
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

        /// <summary>
        /// Create a new UDP packet.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the packet arrival  at the UDP receiver.</param>
        /// <param name="LocalSocket">The local/receiving IP socket of the UDP packet.</param>
        /// <param name="RemoteSocket">The remote  IP socket of the UDP packet.</param>
        /// <param name="Payload">The message/payload of the UDP packet.</param>
        public UDPPacket(UDPReceiver<TData>  UDPReceiver,
                         DateTime            ServerTimestamp,
                         IPSocket            LocalSocket,
                         IPSocket            RemoteSocket,
                         TData               Payload)

            : base(LocalSocket, RemoteSocket)

        {

            this._ServerTimestamp  = ServerTimestamp;
            this._Payload          = Payload;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            return "UDP packet received at " + ServerTimestamp +
                                    " from " + RemoteSocket.ToString() +
                                    " to " + LocalSocket.ToString();

        }

        #endregion

    }

}
