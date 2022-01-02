/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Styx.Arrows;
using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A generic server interface.
    /// </summary>
    public interface IServer : IArrowSender,
                               IDisposable
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
        /// The listening IP socket.
        /// </summary>
        IPSocket   IPSocket  { get; }


        String ServiceBanner { get; set; }


        /// <summary>
        /// Star the server.
        /// </summary>
        Boolean Start();

        /// <summary>
        /// Start the server after a little delay.
        /// </summary>
        /// <param name="Delay">The delay.</param>
        /// <param name="InBackground">Whether to wait on the main thread or in a background thread.</param>
        Boolean Start(TimeSpan  Delay,
                      Boolean   InBackground  = true);


        /// <summary>
        /// Shutdown the server.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        Boolean Shutdown(String  Message  = null,
                         Boolean Wait     = true);

        /// <summary>
        /// The shutdown of the server was requested.
        /// </summary>
        Boolean StopRequested { get; }


        String ToString();

    }

}
