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
    /// Open a new rendezvous: The service starts one TCP listener per given port
    /// and connects the arriving clients with each other.
    ///
    ///     ConnectPorts([20000, 30000])
    ///     ConnectPorts([?, 30000])
    ///     ConnectPorts([20000, ?],     "SSH rendezvous for maintenance work")
    ///     ConnectPorts([?, ?],         Interactive)
    ///     ConnectPorts([?, ?, ?],      "The Friday chat", Interactive, Echo)
    ///
    /// The keys that signed the request become the owners of the new rendezvous:
    /// only they - or an administrator key - may close it again.
    /// </summary>
    /// <param name="Ports">Two or more requested TCP ports. '?' asks the service to pick a free port.</param>
    /// <param name="Profile">An optional transfer profile, null uses the configured default.</param>
    /// <param name="Description">An optional description of this rendezvous, e.g. "SSH rendezvous for maintenance work".</param>
    /// <param name="EchoToSender">Whether a client also receives what it sends itself, so that every client sees the very same byte stream.</param>
    public sealed record ConnectPortsCommand(IReadOnlyList<PortSpecification>  Ports,
                                             TransferProfile?                 Profile        = null,
                                             String?                          Description    = null,
                                             Boolean                          EchoToSender   = false)

        : RendezvousCommand(Description)

    {

        /// <summary>
        /// The name of this command.
        /// </summary>
        public const String Name = "ConnectPorts";

        /// <summary>
        /// The name of this command.
        /// </summary>
        public override String CommandName
            => Name;

        /// <summary>
        /// Return a text representation of this command.
        /// </summary>
        public override String ToString()

            => $"{Name}([{String.Join(", ", Ports)}]{(Description is not null ? $", \"{Description}\"" : "")}{(Profile.HasValue ? $", {Profile.Value.AsText()}" : "")}{(EchoToSender ? ", Echo" : "")})";

    }

}
