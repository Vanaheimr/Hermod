﻿/*
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

using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The remote socket is the "other side" of a socket connection.
    /// It is a combination of a remote IP address and a remote port.
    /// </summary>
    public interface IRemoteSocket : IDisposable
    {

        /// <summary>
        /// The remote IP address.
        /// </summary>
        IIPAddress RemoteIPAddress  { get; }

        /// <summary>
        /// The remote port.
        /// </summary>
        IPPort     RemotePort       { get; }

        /// <summary>
        /// The remote socket.
        /// </summary>
        IPSocket   RemoteSocket     { get; }

    }

}
