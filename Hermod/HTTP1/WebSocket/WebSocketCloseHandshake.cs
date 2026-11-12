/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// The wait between a close frame and the close of the connection underneath.
    /// </summary>
    /// <remarks>
    /// A WebSocket connection that sends its close frame and closes its socket in
    /// the same breath may reset the connection: when there is data on the socket
    /// that nobody read, or when the other side still sends. And a reset discards
    /// what the other side has received and not yet read - the close frame, and
    /// the messages before it. So whoever closes waits, for a time that is
    /// bounded, until the other side has closed: with a close frame of its own,
    /// or by ending the stream.
    /// </remarks>
    internal static class WebSocketCloseHandshake
    {

        /// <summary>
        /// No more than this is kept of what comes in during the wait, when
        /// looking for a close frame: more than that is just read and dropped.
        /// </summary>
        private const Int32 MaxBufferedForParsing = 1024 * 1024;


        #region WaitForThePeerToClose(Stream, ReadSemaphore, PeerClosed, Timeout, CancellationToken)

        /// <summary>
        /// Wait until the other side has closed, for no longer than the given time.
        /// </summary>
        /// <remarks>
        /// The loop that reads the connection tells when the other side has
        /// closed, by completing <paramref name="PeerClosed"/>. It does not read
        /// while it is held in a message handler - and it is such a handler that
        /// closes, where a message is answered by hanging up - or when it has
        /// ended. Then the connection is read here: up to a close frame, or to
        /// the end of the stream. What else comes is of no more use to anybody.
        /// </remarks>
        /// <param name="Stream">The stream the frames are read from.</param>
        /// <param name="ReadSemaphore">Whoever reads the stream holds this.</param>
        /// <param name="PeerClosed">Completed by whoever reads the stream, when the other side has closed.</param>
        /// <param name="Timeout">How long to wait.</param>
        /// <param name="CancellationToken">An optional cancellation token to end the wait early.</param>
        public static async Task WaitForThePeerToClose(Stream             Stream,
                                                       SemaphoreSlim      ReadSemaphore,
                                                       Task               PeerClosed,
                                                       TimeSpan           Timeout,
                                                       CancellationToken  CancellationToken)
        {

            if (Timeout <= TimeSpan.Zero || PeerClosed.IsCompleted)
                return;

            using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            giveUp.CancelAfter(Timeout);

            Task? turnToRead = null;

            try
            {

                turnToRead = ReadSemaphore.WaitAsync(giveUp.Token);

                await Task.WhenAny(turnToRead, PeerClosed).ConfigureAwait(false);

                if (turnToRead.IsCompletedSuccessfully)
                {

                    var           buffer    = new Byte[4096];
                    var           received  = new MemoryStream();
                    var           parsing   = true;
                    Task<Int32>?  read      = null;

                    try
                    {
                        while (!PeerClosed.IsCompleted)
                        {

                            read = Stream.ReadAsync(buffer, giveUp.Token).AsTask();

                            // The loop may have been handed the other side's close frame
                            // just before it gave the reading away: that ends the wait
                            // as well, whether the stream then ends or not.
                            await Task.WhenAny(read, PeerClosed).ConfigureAwait(false);

                            if (!read.IsCompleted)
                                break;

                            var count = await read.ConfigureAwait(false);

                            if (count == 0)
                                break;

                            if (parsing)
                            {

                                received.Write(buffer, 0, count);

                                if (HasCloseFrame(received, out parsing))
                                    break;

                            }

                        }
                    }
                    finally
                    {

                        // No read is left behind on a stream that is given back.
                        if (read is not null && !read.IsCompleted)
                        {

                            try
                            {
                                giveUp.Cancel();
                                await read.ConfigureAwait(false);
                            }
                            catch
                            { }

                        }

                        ReadSemaphore.Release();
                        turnToRead = null;

                    }

                }

            }
            catch
            {
                // Gone, reset, or too slow: closed all the same.
            }
            finally
            {

                // A turn to read that comes when it is no longer wanted is given back.
                if (turnToRead is not null)
                {

                    try
                    {
                        giveUp.Cancel();
                    }
                    catch
                    { }

                    _ = turnToRead.ContinueWith(
                            task => {
                                if (task.IsCompletedSuccessfully)
                                    ReadSemaphore.Release();
                            },
                            CancellationToken.None,
                            TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default
                        );

                }

            }

        }

        #endregion

        #region (private) HasCloseFrame(Received, out GoOnParsing)

        /// <summary>
        /// Whether there is a close frame among the frames received, which are
        /// taken off what was received as they are found.
        /// </summary>
        /// <param name="Received">What was received and not yet understood.</param>
        /// <param name="GoOnParsing">False when what was received are no frames, or too much of them: it is then only read to its end.</param>
        private static Boolean HasCloseFrame(MemoryStream  Received,
                                             out Boolean   GoOnParsing)
        {

            GoOnParsing  = true;

            var bytes    = Received.ToArray();
            var offset   = 0;

            while (offset < bytes.Length)
            {

                var result = WebSocketFrame.Parse(
                                 bytes.AsSpan(offset),
                                 out var frame,
                                 out var length,
                                 out _,
                                 out _,
                                 AllowRsv1: true
                             );

                if (result == WebSocketFrame.ParseResult.IncompleteData)
                    break;

                if (result != WebSocketFrame.ParseResult.Success || frame is null || length == 0)
                {
                    GoOnParsing = false;
                    return false;
                }

                if (frame.Opcode == WebSocketFrame.Opcodes.Close)
                    return true;

                offset += (Int32) length;

            }

            Received.SetLength(0);
            Received.Write(bytes, offset, bytes.Length - offset);

            if (Received.Length > MaxBufferedForParsing)
                GoOnParsing = false;

            return false;

        }

        #endregion

    }

}
