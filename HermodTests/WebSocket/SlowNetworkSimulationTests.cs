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

using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// The slow network a WebSocket connection simulates when its
    /// SlowNetworkSimulationDelay is set: a byte at a time, at the pace of one
    /// byte per delay - never faster, and not many times slower either.
    /// </summary>
    /// <remarks>
    /// The delay used to be waited after every byte, and a wait is no shorter
    /// than the timer allows: on Windows 15.6 ms, unless some program asks for
    /// finer. A delay of 1 ms per byte became 15.6 ms per byte, and the 101 of
    /// WebSocketShutdownTests.AClientWhoseAnswerWasOnItsWayIsToldAsWell took
    /// five seconds rather than a third of one - as long as a close waits for a
    /// 101 on its way out, so that the close now and then gave up on it and
    /// ended the connection in the middle of it. On Linux, where a wait of 1 ms
    /// takes about that, the old way passes these tests too: only on Windows
    /// can they tell the two apart.
    /// </remarks>
    [TestFixture]
    public class SlowNetworkSimulationTests
    {

        #region (private) RecordingStream

        /// <summary>
        /// A stream that keeps what is written to it, and when each write came.
        /// </summary>
        private sealed class RecordingStream : Stream
        {

            private readonly Stopwatch       clock  = Stopwatch.StartNew();
            private readonly MemoryStream    data   = new ();

            public List<(Int32 Length, TimeSpan At)>  Writes  { get; } = [];

            public Byte[] Data
                => data.ToArray();

            public override ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
            {
                Writes.Add((Buffer.Length, clock.Elapsed));
                data.Write(Buffer.Span);
                return ValueTask.CompletedTask;
            }

            public override void Write(Byte[] Buffer, Int32 Offset, Int32 Count)
                => WriteAsync(Buffer.AsMemory(Offset, Count)).AsTask().GetAwaiter().GetResult();

            public override void Flush() { }
            public override Task FlushAsync(CancellationToken CancellationToken) => Task.CompletedTask;

            public override Boolean  CanRead   => false;
            public override Boolean  CanSeek   => false;
            public override Boolean  CanWrite  => true;
            public override Int64    Length    => data.Length;
            public override Int64    Position  { get => data.Position; set => throw new NotSupportedException(); }

            public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count) => throw new NotSupportedException();
            public override Int64 Seek(Int64 Offset, SeekOrigin Origin)           => throw new NotSupportedException();
            public override void  SetLength(Int64 Value)                          => throw new NotSupportedException();

        }

        #endregion


        #region OneMillisecondPerByteIsKeptOnEveryPlatform()

        /// <summary>
        /// 300 bytes at 1 ms each take a third of a second: not less, because no
        /// byte goes out before its time, and not five seconds, as they did on
        /// Windows.
        /// </summary>
        [Test]
        public async Task OneMillisecondPerByteIsKeptOnEveryPlatform()
        {

            var data    = Enumerable.Range(0, 300).Select(i => (Byte) i).ToArray();
            var stream  = new RecordingStream();
            var took    = Stopwatch.StartNew();

            await SlowNetworkSimulation.WriteAsync(stream, data, TimeSpan.FromMilliseconds(1));

            took.Stop();

            Assert.Multiple(() => {
                Assert.That(stream.Data,                             Is.EqualTo(data));
                Assert.That(stream.Writes.Select(w => w.Length),     Is.All.EqualTo(1),                       "a byte at a time");
                Assert.That(took.Elapsed,                            Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(299)));
                Assert.That(took.Elapsed,                            Is.LessThan(TimeSpan.FromSeconds(2)),    "15.6 ms per byte would be 4.7 s");
            });

        }

        #endregion

        #region NoByteGoesOutBeforeItsTime()

        /// <summary>
        /// A byte that is behind its time goes out at once, but none goes out
        /// ahead of it: the n-th byte after the first is written no earlier than
        /// n delays after the first. Here with a delay longer than the timer's
        /// resolution, which was waited much as before.
        /// </summary>
        [Test]
        public async Task NoByteGoesOutBeforeItsTime()
        {

            var delay   = TimeSpan.FromMilliseconds(40);
            var stream  = new RecordingStream();

            await SlowNetworkSimulation.WriteAsync(stream, new Byte[10], delay);

            var first   = stream.Writes[0].At;

            Assert.Multiple(() => {
                Assert.That(stream.Writes,  Has.Count.EqualTo(10));
                for (var n = 1; n < stream.Writes.Count; n++)
                    Assert.That(stream.Writes[n].At - first,  Is.GreaterThanOrEqualTo(delay * n - TimeSpan.FromMilliseconds(1)),  $"byte {n}");
            });

        }

        #endregion

    }

}
