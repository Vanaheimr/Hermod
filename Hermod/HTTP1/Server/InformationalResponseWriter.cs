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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Writes the interim (1xx) responses of one request onto its connection,
    /// while its handler runs and not after (RFC 9110, Section 15.2).
    ///
    /// The server reads, handles and answers the requests of a connection one
    /// after the other, so while a handler runs the connection is its own.
    /// Once the handler has returned, the server closes this writer before it
    /// writes the final response. A write still in progress finishes first,
    /// and any later one is refused, so no 1xx can follow the final response
    /// or land inside it.
    /// </summary>
    internal sealed class InformationalResponseWriter
    {

        #region Data

        private readonly Stream         stream;
        private readonly SemaphoreSlim  writeLock  = new (1, 1);
        private          Boolean        closed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A writer for the interim responses of one request.
        /// </summary>
        /// <param name="Stream">The connection's stream, after TLS where there is TLS.</param>
        public InformationalResponseWriter(Stream Stream)
        {
            this.stream = Stream;
        }

        #endregion


        #region Send  (Octets, CancellationToken = default)

        /// <summary>
        /// Write one serialised interim response. False when it was not written:
        /// the final response is on its way, or the connection failed.
        /// </summary>
        /// <param name="Octets">The status line, the field lines and the empty line.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<Boolean> Send(Byte[]             Octets,
                                        CancellationToken  CancellationToken   = default)
        {

            await writeLock.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                if (closed)
                    return false;

                await stream.WriteAsync(Octets,     CancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(CancellationToken).ConfigureAwait(false);

                return true;

            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or NotSupportedException)
            {
                // Advisory: a hint that could not be sent is a hint the client
                // does not get, and the final response will say what failed.
                return false;
            }
            finally
            {
                writeLock.Release();
            }

        }

        #endregion

        #region Close ()

        /// <summary>
        /// Refuse every interim response from now on, once a write still in
        /// progress has finished.
        /// </summary>
        public async Task Close()
        {

            await writeLock.WaitAsync().ConfigureAwait(false);

            closed = true;

            writeLock.Release();

        }

        #endregion

    }

}
