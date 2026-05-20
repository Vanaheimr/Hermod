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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// EDNS TCP Keepalive option (RFC 7828).
    /// In a query the option SHOULD be sent with zero length (no timeout value)
    /// to signal keepalive support.  In a response the server includes the
    /// idle timeout in units of 100 milliseconds.
    ///
    /// Wire format:
    ///   Query:    empty (0 bytes)
    ///   Response: 2 bytes — idle TIMEOUT in units of 100 ms
    /// </summary>
    public class EDNSKeepaliveOption : EDNSOption
    {

        #region Properties

        /// <summary>
        /// The idle timeout as a TimeSpan, or null if this is a query
        /// (where no timeout value is sent).
        /// </summary>
        public TimeSpan?  IdleTimeout    { get; }

        /// <summary>
        /// The raw timeout value in units of 100 milliseconds, or null for queries.
        /// </summary>
        public UInt16?    TimeoutValue   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EDNS TCP Keepalive option for a query (no timeout value).
        /// </summary>
        public EDNSKeepaliveOption()

            : base(EDNSOptionCode.Keepalive, [])

        {
            this.IdleTimeout   = null;
            this.TimeoutValue  = null;
        }

        /// <summary>
        /// Create a new EDNS TCP Keepalive option with a specific idle timeout.
        /// </summary>
        /// <param name="IdleTimeout">The idle timeout.</param>
        public EDNSKeepaliveOption(TimeSpan IdleTimeout)

            : base(EDNSOptionCode.Keepalive,
                   Serialize(IdleTimeout))

        {
            this.IdleTimeout   = IdleTimeout;
            this.TimeoutValue  = (UInt16) Math.Min(IdleTimeout.TotalMilliseconds / 100.0, UInt16.MaxValue);
        }

        /// <summary>
        /// Create a new EDNS TCP Keepalive option from a raw timeout value.
        /// </summary>
        /// <param name="TimeoutValue">The raw timeout value in units of 100 ms.</param>
        public EDNSKeepaliveOption(UInt16 TimeoutValue)

            : base(EDNSOptionCode.Keepalive,
                   [(Byte) (TimeoutValue >> 8), (Byte) (TimeoutValue & 0xFF)])

        {
            this.TimeoutValue  = TimeoutValue;
            this.IdleTimeout   = TimeSpan.FromMilliseconds(TimeoutValue * 100.0);
        }

        #endregion


        #region (private static) Serialize(IdleTimeout)

        private static Byte[] Serialize(TimeSpan IdleTimeout)
        {

            var value = (UInt16) Math.Min(IdleTimeout.TotalMilliseconds / 100.0, UInt16.MaxValue);

            return [
                (Byte) (value >> 8),
                (Byte) (value & 0xFF)
            ];

        }

        #endregion

        #region (static) Parse(Data)

        /// <summary>
        /// Parse an EDNS TCP Keepalive option from raw data bytes.
        /// </summary>
        /// <param name="Data">The raw option data.</param>
        public new static EDNSKeepaliveOption Parse(Byte[] Data)
        {

            // Query: empty data (0 bytes) — signal keepalive support
            if (Data.Length == 0)
                return new EDNSKeepaliveOption();

            // Response: 2 bytes — idle timeout
            if (Data.Length == 2)
                return new EDNSKeepaliveOption(
                           (UInt16) ((Data[0] << 8) | Data[1])
                       );

            throw new ArgumentException($"EDNS Keepalive option must be 0 or 2 bytes, got {Data.Length}!", nameof(Data));

        }

        #endregion

        #region (static) CreateQuery()

        /// <summary>
        /// Create a keepalive option for use in a TCP/TLS query (empty, no timeout value).
        /// </summary>
        public static EDNSKeepaliveOption CreateQuery()
            => new();

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this EDNS TCP Keepalive option.
        /// </summary>
        public override String ToString()

            => IdleTimeout.HasValue
                   ? $"Keepalive timeout={IdleTimeout.Value.TotalSeconds:F1}s"
                   : "Keepalive (query)";

        #endregion

    }

}
