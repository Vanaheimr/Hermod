/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#region Usings

using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Provides the underlying stream of data for network access.
    /// </summary>
    public class MyNetworkStream : NetworkStream
    {

        /// <summary>
        /// Initializes a new NetworkStream for the given IP socket having the given IP socket ownership.
        /// </summary>
        /// <param name="Socket">The IP socket that the NetworkStream will use to send and receive data.</param>
        /// <param name="OwnsSocket">Whether the NetworkStream will take ownership of the IP socket.</param>
        public MyNetworkStream(Socket   Socket,
                               Boolean  OwnsSocket)

            : base(Socket,
                   OwnsSocket)

        { }

        /// <summary>
        /// The underlying IP socket.
        /// </summary>
        public new Socket Socket
            => base.Socket;

        protected override void Dispose(Boolean Disposing)
        {
            try
            {
                base.Dispose(Disposing);
            }
            catch (Exception e)
            {

            }
        }

    }

}
