/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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

using System.Net.Sockets;

#endregion

namespace de.ahzf.Hermod.Sockets.TCP
{

    /// <summary>
    /// A class representing a TCP connection
    /// </summary>
    public class TCPConnection : ATCPConnection
    {

        #region Constructor(s)

        #region TCPConnection()

        /// <summary>
        /// Create a new TCPConnection class
        /// </summary>
        public TCPConnection()
        { }

        #endregion

        #region TCPConnection(TCPClientConnection)

        /// <summary>
        /// Create a new TCPConnection class using the given TcpClient class
        /// </summary>
        public TCPConnection(TcpClient TCPClientConnection)
            : base(TCPClientConnection)
        { }

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

}
