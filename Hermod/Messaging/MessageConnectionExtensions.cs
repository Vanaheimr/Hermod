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

#region Usings

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Messaging
{

    /// <summary>
    /// Getting at what only one kind of <see cref="IMessageConnection"/> has.
    /// </summary>
    public static class MessageConnectionExtensions
    {

        /// <summary>
        /// The connection as an HTTP WebSocket - its HTTP request and response, its frames - where it is one.
        /// </summary>
        /// <param name="Connection">A message connection.</param>
        /// <param name="WebSocket">The connection as a WebSocket, or null.</param>
        public static Boolean TryGetWebSocket(this IMessageConnection                       Connection,
                                              [NotNullWhen(true)] out IWebSocketConnection?  WebSocket)
        {
            WebSocket = Connection as IWebSocketConnection;
            return WebSocket is not null;
        }

        /// <summary>
        /// The connection as a channel within an SSH session - its login, host key, channel - where it is one.
        /// </summary>
        /// <param name="Connection">A message connection.</param>
        /// <param name="SSH">The connection as an SSH channel, or null.</param>
        public static Boolean TryGetSSH(this IMessageConnection                        Connection,
                                        [NotNullWhen(true)] out ISshMessageConnection?  SSH)
        {
            SSH = Connection as ISshMessageConnection;
            return SSH is not null;
        }

    }

}
