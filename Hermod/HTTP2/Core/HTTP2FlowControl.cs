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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// The receive windows both roles grant their peer (RFC 9113, Section 6.9),
    /// and why they are as large as they are.
    ///
    /// Each stream has a window of its own, and the connection one more, which
    /// all of its streams draw on together: DATA needs room in both. The server
    /// gives back the window of a streamed request body, or of tunnel bytes, only
    /// once the application has read them (consumption-driven backpressure), so
    /// the windows bound what it holds for readers that have not read yet: a
    /// stream's window what one stream can make it hold, the connection's what
    /// all of them can together. A reader that is slow, or has stalled, keeps the
    /// window of what it has not read, of its own stream and as much of the
    /// connection's. Were the connection window no larger than a stream's, one
    /// such stream could take all of it, and the peer could send DATA on no
    /// other stream of the connection: a log file written away slowly would hold
    /// up the OCPP channel next to it.
    ///
    /// So the connection window is four stream windows by default: up to three
    /// streams can stall with their whole window taken, and the others still
    /// have a stream window to share. Each stream window in it costs memory,
    /// though: a connection can be made to hold up to 4 MiB for readers that do
    /// not read, where it used to be 1 MiB. Smaller saves memory, and leaves the
    /// others less when streams stall; at or below a stream window, one stalled
    /// stream stops all others again. Larger lets more streams stall, and lets a
    /// connection carry more at a time over a long round trip: what flows is
    /// bounded by window ÷ round-trip time, for a stream by the stream window
    /// (1 MiB in 100 ms: 10 MiB/s) and for all streams of a connection together
    /// by the connection window (4 MiB in 100 ms: 40 MiB/s). The README compares
    /// this with what other implementations chose.
    ///
    /// The client gives back the stream window of tunnel bytes and of a
    /// streamed response only as the application reads them, too, so a stream
    /// window bounds what it holds for an application that does not read. The
    /// connection window it gives back on receipt, read or not: it bounds only
    /// what the server can have in flight, and no stream the application has not
    /// read yet can keep window from the one it reads now.
    /// </summary>
    public static class HTTP2FlowControl
    {

        /// <summary>
        /// The window RFC 9113 starts every stream and the connection with: 65 535
        /// octets (Section 6.9.2). A connection window can be raised from it with a
        /// WINDOW_UPDATE, but not lowered.
        /// </summary>
        public const Int32 InitialWindowSize            = 65_535;

        /// <summary>
        /// The receive window of each stream: the SETTINGS_INITIAL_WINDOW_SIZE both
        /// roles advertise, 1 MiB.
        /// </summary>
        public const Int32 StreamWindowSize             = 1024 * 1024;

        /// <summary>
        /// The receive window of the connection both roles grant by default: four
        /// stream windows, 4 MiB.
        /// </summary>
        public const Int32 DefaultConnectionWindowSize  = 4 * StreamWindowSize;

        /// <summary>
        /// Return a connection window size that can be granted, or throw: at least
        /// <see cref="InitialWindowSize"/>, and at most 2^31 − 1 octets
        /// (Section 6.9.1), which is <see cref="Int32.MaxValue"/>.
        /// </summary>
        /// <param name="ConnectionWindowSize">The connection window size to check.</param>
        /// <param name="ParameterName">The parameter or property it was given as.</param>
        internal static Int32 CheckConnectionWindowSize(Int32 ConnectionWindowSize, String ParameterName)
        {

            if (ConnectionWindowSize < InitialWindowSize)
                throw new ArgumentOutOfRangeException(ParameterName,
                                                      ConnectionWindowSize,
                                                      $"A connection window can be raised from RFC 9113's {InitialWindowSize} octets, not lowered below them.");

            return ConnectionWindowSize;

        }

    }

}
