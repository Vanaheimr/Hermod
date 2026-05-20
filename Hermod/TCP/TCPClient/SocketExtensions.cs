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

using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.TCP
{

    /// <summary>
    /// Extension methods for sockets.
    /// </summary>
    public static class SocketExtensions
    {

        #region Poll(this Socket, Mode, CancellationToken)

        public static void Poll(this Socket        Socket,
                                SelectMode         Mode,
                                CancellationToken  CancellationToken)
        {

            if (!CancellationToken.CanBeCanceled)
                return;

            if (Socket is not null)
            {
                do
                {
                    CancellationToken.ThrowIfCancellationRequested();
                } while (!Socket.Poll(1000, Mode));
            }

            else
                CancellationToken.ThrowIfCancellationRequested();

        }

        #endregion

    }

}
