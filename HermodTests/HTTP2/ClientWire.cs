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

#region Usings

using System.Text;
using System.Diagnostics;
using System.Buffers.Binary;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Everything an <see cref="HTTP2ClientConnection"/> on a
    /// <see cref="HoldingH2Transport"/> sends, as the server reads it, one line
    /// per frame. A task of its own reads it as it is sent: the transport takes
    /// only so much before its other end reads, and the connection's DATA writer
    /// loop must not wait for the test to come round to it.
    ///
    /// A line names the stream and the frame's type, and what tells frames apart
    /// in these tests: a DATA frame's length and END_STREAM; a header block's
    /// method, path (or, for a plain CONNECT, authority) and priority field, as
    /// the server decodes them, with one decoder that sees every block in order;
    /// the stream and field value a PRIORITY_UPDATE carries; and the error code
    /// of RST_STREAM and GOAWAY. SETTINGS, WINDOW_UPDATE and PING say nothing
    /// here, and are left out.
    /// </summary>
    internal sealed class ClientWire : IAsyncDisposable
    {

        private readonly HoldingH2Transport       transport;
        private readonly HPACKDecoder             decoder     = new();
        private readonly List<String>             lines       = [];
        private readonly CancellationTokenSource  stop        = new();
        private readonly Task                     reading;
        private          TaskCompletionSource     changed     = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // A header block split over CONTINUATION frames, until its END_HEADERS.
        private          MemoryStream?            headerBlock;
        private          HTTP2Frame?              headersFrame;

        public ClientWire(HoldingH2Transport Transport)
        {
            this.transport  = Transport;
            this.reading    = Task.Run(ReadAsync);
        }


        /// <summary>
        /// The lines read so far.
        /// </summary>
        public Int32 Count
        {
            get
            {
                lock (lines)
                    return lines.Count;
            }
        }

        /// <summary>
        /// Wait until the lines read so far satisfy <paramref name="Condition"/>,
        /// and return them. A client that never sends what is waited for fails
        /// the test after <see cref="HoldingH2Transport.StepTimeout"/>, with
        /// <paramref name="What"/> and everything it did send: only a test that
        /// went wrong waits that long.
        /// </summary>
        public async Task<List<String>> UntilAsync(Func<List<String>, Boolean> Condition, String What)
        {

            var watch = Stopwatch.StartNew();

            while (true)
            {

                List<String> snapshot;
                Task         next;

                lock (lines)
                {
                    snapshot  = [.. lines];
                    next      = changed.Task;
                }

                if (Condition(snapshot))
                    return snapshot;

                var left = HoldingH2Transport.StepTimeout - watch.Elapsed;

                if (left <= TimeSpan.Zero)
                    Assert.Fail($"The client never sent {What}. It sent: {String.Join(" | ", snapshot)}");

                await Task.WhenAny(next, Task.Delay(left));

            }

        }


        #region Line helpers

        /// <summary>
        /// The stream a line is about.
        /// </summary>
        public static UInt32 StreamOf(String Line)

            => UInt32.Parse(Line[..Line.IndexOf(' ')]);

        /// <summary>
        /// Whether a line is a DATA frame.
        /// </summary>
        public static Boolean IsData(String Line)

            => Line.Contains(" DATA ");

        /// <summary>
        /// The DATA bytes these lines carry on a stream.
        /// </summary>
        public static Int64 DataBytesOn(IEnumerable<String> Lines, UInt32 StreamId)

            => Lines.Where (line => IsData(line) && StreamOf(line) == StreamId).
                     Sum   (line => Int64.Parse(line.Split(' ')[2]));

        /// <summary>
        /// The lines about one stream.
        /// </summary>
        public static List<String> On(IEnumerable<String> Lines, UInt32 StreamId)

            => [.. Lines.Where(line => StreamOf(line) == StreamId)];

        #endregion


        #region Reading

        private async Task ReadAsync()
        {

            try
            {

                while (await transport.NextFrameAsync(stop.Token) is { } frame)
                {

                    if (Line(frame) is not { } line)
                        continue;

                    TaskCompletionSource wake;

                    lock (lines)
                    {
                        lines.Add(line);
                        wake     = changed;
                        changed  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    }

                    wake.TrySetResult();

                }

            }
            catch (Exception) when (stop.IsCancellationRequested)
            {
                // Disposed — the test is done.
            }
            catch (EndOfStreamException)
            {
                // The client's side ended before its preface.
            }

        }

        /// <summary>
        /// The line for a frame, or null for one that says nothing here.
        /// </summary>
        private String? Line(HTTP2Frame Frame)
        {

            var payload = Frame.Payload ?? [];

            switch (Frame.Type)
            {

                case HTTP2FrameType.DATA:
                    return $"{Frame.StreamId} DATA {payload.Length}" + (Frame.EndStream ? " END_STREAM" : "");

                case HTTP2FrameType.HEADERS:

                    if (Frame.EndHeaders)
                        return HeaderBlock(Frame, payload);

                    headersFrame = Frame;
                    headerBlock  = new MemoryStream();
                    headerBlock.Write(payload);

                    return null;

                case HTTP2FrameType.CONTINUATION:

                    headerBlock!.Write(payload);

                    if (!Frame.EndHeaders)
                        return null;

                    var block = headerBlock.ToArray();
                    headerBlock = null;

                    return HeaderBlock(headersFrame!, block);

                case HTTP2FrameType.PRIORITY_UPDATE:
                    return $"0 PRIORITY_UPDATE {BinaryPrimitives.ReadUInt32BigEndian(payload) & 0x7FFFFFFFu} " +
                           Encoding.ASCII.GetString(payload.AsSpan(4));

                case HTTP2FrameType.RST_STREAM:
                    return $"{Frame.StreamId} RST_STREAM {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(payload)}";

                case HTTP2FrameType.GOAWAY:
                    return $"0 GOAWAY {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(4))}";

                default:
                    return null;

            }

        }

        /// <summary>
        /// The line for a whole header block: decoded, as the server's decoder
        /// must decode every block, in order.
        /// </summary>
        private String HeaderBlock(HTTP2Frame Headers, Byte[] Block)
        {

            var fields   = decoder.DecodeHeaderBlock(Block);
            var end      = Headers.EndStream ? " END_STREAM" : "";

            String? Field(String Name)
                => fields.FirstOrDefault(field => field.Name == Name).Value;

            // A block without a method is a request's trailers.
            if (Field(":method") is not { } method)
                return $"{Headers.StreamId} HEADERS trailers {String.Join(", ", fields.Select(field => $"{field.Name}={field.Value}"))}{end}";

            var target   = Field(":path") ?? Field(":authority") ?? "?";
            var priority = Field("priority");

            return $"{Headers.StreamId} HEADERS {method} {target}" +
                   (priority is not null ? $" priority={priority}" : "") +
                   end;

        }

        #endregion


        public async ValueTask DisposeAsync()
        {

            stop.Cancel();

            try
            {
                await reading;
            }
            catch
            { }

        }

    }

}
