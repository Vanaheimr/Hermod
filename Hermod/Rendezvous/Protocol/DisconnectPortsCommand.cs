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
    /// Close an existing rendezvous and remove its TCP listeners.
    /// All given ports must belong to the same rendezvous.
    ///
    ///     DisconnectPorts(20000, 30000)
    ///     DisconnectPorts([20000, 30000], "Maintenance is done")
    ///     DisconnectPorts(20000)
    ///
    /// The request must be signed by a key that opened this rendezvous,
    /// or by an administrator key.
    /// </summary>
    /// <param name="Ports">One or more TCP ports of the rendezvous.</param>
    /// <param name="Description">An optional comment why this rendezvous is closed.</param>
    public sealed record DisconnectPortsCommand(IReadOnlyList<IPPort>  Ports,
                                                String?                Description = null)

        : RendezvousCommand(Description)

    {

        /// <summary>
        /// The name of this command.
        /// </summary>
        public const String Name = "DisconnectPorts";

        /// <summary>
        /// The name of this command.
        /// </summary>
        public override String CommandName
            => Name;

        /// <summary>
        /// Return a text representation of this command.
        /// </summary>
        public override String ToString()

            => $"{Name}([{String.Join(", ", Ports)}]{(Description is not null ? $", \"{Description}\"" : "")})";

    }

}
