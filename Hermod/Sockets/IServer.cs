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

namespace eu.Vanaheimr.Hermod.Datastructures
{

    /// <summary>
    /// A generic server interface.
    /// </summary>
    public interface IServer : IDisposable
    {

        /// <summary>
        /// The server is running and ready for serving requests.
        /// </summary>
        Boolean    IsRunning { get; }
        
        /// <summary>
        /// The listening IP address.
        /// </summary>
        IIPAddress IPAddress { get; }

        /// <summary>
        /// The listening IP port.
        /// </summary>
        IPPort     Port      { get; }


        /// <summary>
        /// Start the server.
        /// </summary>
        void Start();

        /// <summary>
        /// Shutdown the server.
        /// </summary>
        void Shutdown(Boolean Wait = true);

        /// <summary>
        /// The shutdown of the server was requested.
        /// </summary>
        Boolean StopRequested { get; }


        String ToString();

    }

}
