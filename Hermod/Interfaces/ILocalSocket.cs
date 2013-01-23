/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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

namespace de.ahzf.Vanaheimr.Hermod.Datastructures
{

    /// <summary>
    /// The local socket is "this side" of a socket connection.
    /// It a combination of a local IPAdress and a local port.
    /// </summary>
    public interface ILocalSocket : IDisposable
    {

        /// <summary>
        /// The local socket.
        /// </summary>
        IPSocket   LocalSocket { get; }

        /// <summary>
        /// The local host.
        /// </summary>
        IIPAddress LocalHost   { get; }

        /// <summary>
        /// The local port.
        /// </summary>
        IPPort     LocalPort   { get; }

    }

}
