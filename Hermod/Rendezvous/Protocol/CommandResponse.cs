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
    /// The response of a control command.
    ///
    /// On the wire a response is a single UTF-8 line, terminated by CR LF:
    ///
    ///     OK ConnectPorts([20000, 30000], Interactive)
    ///     ERROR PortInUse TCP port 20000 is already in use!
    ///
    /// </summary>
    /// <param name="Code">The response code.</param>
    /// <param name="Text">The response text.</param>
    public sealed record CommandResponse(ResponseCode  Code,
                                         String        Text)
    {

        #region Properties

        /// <summary>
        /// Whether the command was executed successfully.
        /// </summary>
        public Boolean IsSuccess
            => Code == ResponseCode.OK;

        #endregion


        #region (static) Ok   (Text)

        /// <summary>
        /// Create a new successful response.
        /// </summary>
        /// <param name="Text">The response text.</param>
        public static CommandResponse Ok(String Text)
            => new (ResponseCode.OK, Text);

        #endregion

        #region (static) Error(Code, Message)

        /// <summary>
        /// Create a new error response.
        /// </summary>
        /// <param name="Code">The response code.</param>
        /// <param name="Message">A human readable error message.</param>
        public static CommandResponse Error(ResponseCode  Code,
                                            String        Message)

            => new (Code, Message);

        #endregion

        #region (static) Error(CommandError)

        /// <summary>
        /// Create a new error response.
        /// </summary>
        /// <param name="CommandError">A command error.</param>
        public static CommandResponse Error(CommandError CommandError)
            => new (CommandError.Code, CommandError.Message);

        #endregion


        #region ToProtocolLine()

        /// <summary>
        /// Return the wire representation of this response, without the line terminator.
        /// </summary>
        public String ToProtocolLine()

            => IsSuccess
                   ? $"OK {Text}"
                   : $"ERROR {Code} {Text}";

        #endregion

        #region ToString()

        /// <summary>
        /// Return a text representation of this response.
        /// </summary>
        public override String ToString()
            => ToProtocolLine();

        #endregion

    }

}
