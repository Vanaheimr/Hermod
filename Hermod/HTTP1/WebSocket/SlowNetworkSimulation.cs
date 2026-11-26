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

using System.Diagnostics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A slow network, simulated for a WebSocket connection whose
    /// SlowNetworkSimulationDelay is set: its data goes out a byte at a time,
    /// at the pace of one byte per delay.
    /// </summary>
    /// <remarks>
    /// The pace is kept by a stopwatch, not by waiting the delay after every
    /// byte. A wait is no shorter than the timer allows, and on Windows that is
    /// 15.6 ms unless some program asks for finer: a delay of 1 ms per byte
    /// became 15.6 ms per byte, and a 101 Switching Protocols of some 300 bytes
    /// took five seconds rather than the third of a second it was set up to
    /// take. Five seconds is as long as a close waits for a 101 on its way out,
    /// and a close that gave up on it ended the connection in the middle of the
    /// 101 (WebSocketShutdownTests). Now a byte that is behind its time goes
    /// out at once, and only one that is ahead of it waits: still a byte at a
    /// time, on every platform at the pace asked for, and a delay that is
    /// longer than the timer's resolution is waited much as before.
    /// </remarks>
    internal static class SlowNetworkSimulation
    {

        #region WriteAsync(Stream, Data, DelayPerByte, CancellationToken = default)

        /// <summary>
        /// Write the data to the stream a byte at a time, flushing each, at the
        /// pace of one byte per the given delay.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="Data">The data to write.</param>
        /// <param name="DelayPerByte">The time each byte takes.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async Task WriteAsync(Stream             Stream,
                                            Byte[]             Data,
                                            TimeSpan           DelayPerByte,
                                            CancellationToken  CancellationToken   = default)
        {

            var pace = Stopwatch.StartNew();

            for (var i = 0; i < Data.Length; i++)
            {

                await Stream.WriteAsync(Data.AsMemory(i, 1),
                                        CancellationToken);

                await Stream.FlushAsync(CancellationToken);

                var ahead = DelayPerByte * (i + 1) - pace.Elapsed;

                if (ahead > TimeSpan.Zero)
                    await Task.Delay(ahead,
                                     CancellationToken);

            }

        }

        #endregion

    }

}
