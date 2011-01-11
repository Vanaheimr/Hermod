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

    /// <summary>
    /// A class representing a TCP connection
    /// </summary>
    public class CustomTCPConnection : ATCPConnection
    {

        #region Constructor(s)

        #region CustomTCPConnection()

        /// <summary>
        /// Create a new TCPConnection class
        /// </summary>
        public CustomTCPConnection()
        { }

        #endregion

        #region CustomTCPConnection(myTCPClientConnection)

        /// <summary>
        /// Create a new TCPConnection class using the given TcpClient class
        /// </summary>
        public CustomTCPConnection(TcpClient myTCPClientConnection)
            : base(myTCPClientConnection)
        {
            WriteToResponseStream("Hello..." + Environment.NewLine);
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


}
