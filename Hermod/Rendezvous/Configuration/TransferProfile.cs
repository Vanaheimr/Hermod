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
    /// The intended traffic characteristics of a rendezvous.
    /// The service uses this hint to size its relay buffers and to tune the TCP
    /// parameters of the accepted sockets.
    /// </summary>
    public enum TransferProfile
    {

        /// <summary>
        /// A sensible middle ground for unknown traffic (the default).
        /// </summary>
        Balanced,

        /// <summary>
        /// Low latency, small messages: chat, SSH, remote control, telemetry.
        /// Nagle's algorithm is disabled, buffers are kept small and TCP keep-alive
        /// probes are sent early, so that dead peers are detected quickly.
        /// </summary>
        Interactive,

        /// <summary>
        /// High throughput, large messages: file transfers, backups, database dumps.
        /// Larger relay buffers reduce the number of system calls per megabyte and
        /// keep-alive probes are sent lazily.
        /// </summary>
        Bulk

    }


    /// <summary>
    /// Extension methods for transfer profiles.
    /// </summary>
    public static class TransferProfileExtensions
    {

        #region (static) TryParse (Text, out Profile)

        /// <summary>
        /// Try to parse the given text as a transfer profile.
        /// Matching is case-insensitive and ignores '-' and '_', so that
        /// "low-latency", "lowLatency" and "LOW_LATENCY" are all accepted.
        /// </summary>
        /// <param name="Text">A text representation of a transfer profile.</param>
        /// <param name="Profile">The parsed transfer profile.</param>
        public static Boolean TryParse(ReadOnlySpan<Char>   Text,
                                       out TransferProfile  Profile)
        {

            Span<Char> normalized = stackalloc Char[32];
            var length = 0;

            foreach (var character in Text)
            {

                if (character is '-' or '_')
                    continue;

                if (length == normalized.Length)
                {
                    Profile = default;
                    return false;
                }

                normalized[length++] = Char.ToLowerInvariant(character);

            }

            switch (normalized[..length])
            {

                case "balanced":
                case "default":
                case "normal":
                case "auto":
                    Profile = TransferProfile.Balanced;
                    return true;

                case "interactive":
                case "lowlatency":
                case "latency":
                case "chat":
                case "ssh":
                case "terminal":
                    Profile = TransferProfile.Interactive;
                    return true;

                case "bulk":
                case "bulktransfer":
                case "throughput":
                case "transfer":
                case "file":
                case "filetransfer":
                case "backup":
                    Profile = TransferProfile.Bulk;
                    return true;

                default:
                    Profile = default;
                    return false;

            }

        }

        #endregion

        #region (static) AsText  (this Profile)

        /// <summary>
        /// Return the canonical text representation of the given transfer profile.
        /// </summary>
        /// <param name="Profile">A transfer profile.</param>
        public static String AsText(this TransferProfile Profile)

            => Profile switch {
                   TransferProfile.Interactive  => "Interactive",
                   TransferProfile.Bulk         => "Bulk",
                   _                            => "Balanced"
               };

        #endregion

    }

}
