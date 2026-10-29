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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// When a connection renews its keys by itself (RFC 4253 §9): after <see cref="DataVolume"/> bytes in
    /// either direction or after <see cref="Interval"/>, whichever comes first — the counterpart of
    /// OpenSSH's <c>RekeyLimit</c>.
    /// </summary>
    /// <remarks>
    /// The defaults are the RFC's recommendation, once per gigabyte and once per hour. Independently of
    /// both, a connection rekeys before either packet sequence number passes 2^31 (RFC 4344 §3.1), since
    /// with strict KEX a wrapped sequence number would repeat a nonce.
    /// </remarks>
    public sealed record SshRekeyLimits
    {

        /// <summary>
        /// The packet count after which a rekey is due regardless of the limits (RFC 4344 §3.1).
        /// </summary>
        public const Int64 MaxPacketsPerKey = 1L << 31;

        /// <summary>
        /// RFC 4253 §9: once per gigabyte, once per hour.
        /// </summary>
        public static SshRekeyLimits Default { get; } = new ();

        private readonly Int64 dataVolume = 1L << 30;

        /// <summary>
        /// The payload bytes, sent or received, after which the keys are renewed (default 1 GiB).
        /// Counted per direction: the first direction to reach it triggers the rekey.
        /// </summary>
        public Int64 DataVolume
        {
            get => dataVolume;
            init => dataVolume = value > 0
                                     ? value
                                     : throw new ArgumentOutOfRangeException(nameof(DataVolume), value, "A data volume must be positive.");
        }

        private readonly TimeSpan? interval = TimeSpan.FromHours(1);

        /// <summary>
        /// The time after which the keys are renewed even on a quiet connection (default 1 h), or null
        /// for no time limit.
        /// </summary>
        public TimeSpan? Interval
        {
            get => interval;
            init => interval = value is null || value > TimeSpan.Zero
                                   ? value
                                   : throw new ArgumentOutOfRangeException(nameof(Interval), value, "An interval must be positive.");
        }

    }

}
