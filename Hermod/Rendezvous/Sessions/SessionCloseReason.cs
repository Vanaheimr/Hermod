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

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// Why a rendezvous was closed.
    /// </summary>
    public enum SessionCloseReason
    {

        /// <summary>
        /// The clients disconnected.
        /// </summary>
        ClientDisconnected,

        /// <summary>
        /// Not all clients did arrive before the rendezvous timeout elapsed.
        /// </summary>
        RendezvousTimeout,

        /// <summary>
        /// No payload was relayed before the idle timeout elapsed.
        /// </summary>
        IdleTimeout,

        /// <summary>
        /// An authorized DisconnectPorts command was received.
        /// </summary>
        DisconnectRequested,

        /// <summary>
        /// The service is shutting down.
        /// </summary>
        ServiceShutdown,

        /// <summary>
        /// An unexpected error occurred.
        /// </summary>
        Error

    }

}
