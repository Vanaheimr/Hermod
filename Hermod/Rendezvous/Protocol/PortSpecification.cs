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

using System.Globalization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// A requested TCP port of a rendezvous: either a fixed port number,
    /// or '?' asking the service to pick a free port.
    /// </summary>
    public readonly record struct PortSpecification
    {

        #region Properties

        /// <summary>
        /// The requested TCP port, or null when the service shall pick a free port.
        /// </summary>
        public IPPort?  Port        { get; }

        /// <summary>
        /// Whether the service shall pick a free port.
        /// </summary>
        public Boolean  IsRandom
            => !Port.HasValue;

        #endregion

        #region Constructor(s)

        private PortSpecification(IPPort? Port)
        {
            this.Port = Port;
        }

        #endregion


        #region (static) Random

        /// <summary>
        /// A port that will be picked by the service ('?').
        /// </summary>
        public static PortSpecification Random { get; } = new (null);

        #endregion

        #region (static) Fixed   (Port)

        /// <summary>
        /// A fixed TCP port.
        /// </summary>
        /// <param name="Port">A TCP port, must not be zero.</param>
        public static PortSpecification Fixed(IPPort Port)

            => Port.IsZero
                   ? throw new ArgumentOutOfRangeException(nameof(Port), "A fixed TCP port must not be zero!")
                   : new (Port);

        #endregion

        #region (static) TryParse(Text, out PortSpecification)

        /// <summary>
        /// Try to parse the given text as a port specification.
        /// </summary>
        /// <param name="Text">A text representation of a port specification.</param>
        /// <param name="PortSpecification">The parsed port specification.</param>
        public static Boolean TryParse(ReadOnlySpan<Char>     Text,
                                       out PortSpecification  PortSpecification)
        {

            if (Text.Length == 1 && Text[0] == '?')
            {
                PortSpecification = Random;
                return true;
            }

            // NumberStyles.None rejects signs, whitespace and thousands separators,
            // UInt16.TryParse rejects everything above 65535.
            if (UInt16.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) &&
                port > 0)
            {
                PortSpecification = new (IPPort.Parse(port));
                return true;
            }

            PortSpecification = default;
            return false;

        }

        #endregion


        #region ToString()

        /// <summary>
        /// Return a text representation of this port specification.
        /// </summary>
        public override String ToString()

            => Port?.ToString() ?? "?";

        #endregion

    }

}
