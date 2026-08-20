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
    /// The result of a control command.
    /// </summary>
    public enum ResponseCode
    {

        /// <summary>
        /// The command was executed successfully.
        /// </summary>
        OK,

        /// <summary>
        /// The command name is unknown.
        /// </summary>
        UnknownCommand,

        /// <summary>
        /// The command could not be parsed.
        /// </summary>
        InvalidSyntax,

        /// <summary>
        /// The command was longer than the configured maximum command length.
        /// </summary>
        CommandTooLong,

        /// <summary>
        /// The requested TCP port is outside of the configured port range,
        /// or reserved by the service itself.
        /// </summary>
        PortNotAllowed,

        /// <summary>
        /// The requested TCP port is already used by another rendezvous
        /// or by another application.
        /// </summary>
        PortInUse,

        /// <summary>
        /// The command requested more ports than allowed per rendezvous.
        /// </summary>
        TooManyPorts,

        /// <summary>
        /// The service has reached its maximum number of concurrent rendezvous.
        /// </summary>
        TooManySessions,

        /// <summary>
        /// None of the given TCP ports belongs to a known rendezvous.
        /// </summary>
        UnknownSession,

        /// <summary>
        /// The request was not signed by enough known and currently valid keys,
        /// it was replayed, or it wants to close the rendezvous of somebody else.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// The command failed for an unexpected reason.
        /// </summary>
        InternalError

    }

}
