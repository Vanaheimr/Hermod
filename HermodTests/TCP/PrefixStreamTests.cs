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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP;

/// <summary>
/// PrefixStream hands over bytes it already holds without waiting for the inner stream.
/// </summary>
/// <remarks>
/// This is the bug behind the EmptyEventSource_StillSendsTheRetryPreamble flake on the
/// Debian CI leg. When an HTTP response header and the first body bytes arrive in the same
/// read, the client puts the leftover bytes into a PrefixStream. PrefixStream copied them
/// out and then - unless the caller's buffer happened to be exactly full - went on to read
/// the inner stream for the rest. On a Server-Sent-Events stream there is no rest until the
/// server has an event to send, so the bytes already in hand were never delivered and the
/// client sat there until its own timeout. Whether it happened at all came down to whether
/// two writes were coalesced into one TCP segment, which is why it only ever showed up
/// under load, on one platform.
///
/// A stream that never answers is the honest model of the inner stream here: a quiet SSE
/// connection is open, healthy, and silent. Every read below is therefore bounded, so a
/// regression fails the assertion instead of hanging the suite.
/// </remarks>
[TestFixture]
public class PrefixStreamTests
{

    #region (class) SilentStream

    /// <summary>
    /// An open, healthy, silent peer: a read blocks until the test releases it, which is
    /// what a Server-Sent-Events connection looks like before its first event.
    /// </summary>
    /// <remarks>
    /// Disposing releases anyone still blocked, so a regression wedges the assertion rather
    /// than a thread.
    /// </remarks>
    sealed class SilentStream : Stream
    {

        private readonly ManualResetEventSlim released = new (false);

        public Int32 ReadAttempts;

        public override Boolean CanRead  => true;
        public override Boolean CanSeek  => false;
        public override Boolean CanWrite => false;
        public override Int64   Length   => throw new NotSupportedException();

        public override Int64 Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)
        {
            Interlocked.Increment(ref ReadAttempts);
            released.Wait();
            return 0;
        }

        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       Buffer,
                                                         CancellationToken  CancellationToken = default)
        {
            Interlocked.Increment(ref ReadAttempts);
            await Task.Run(() => released.Wait(), CancellationToken);
            return 0;
        }

        public override void Flush()                           { }
        public override Int64 Seek(Int64 O, SeekOrigin S)      => throw new NotSupportedException();
        public override void SetLength(Int64 V)                => throw new NotSupportedException();
        public override void Write(Byte[] B, Int32 O, Int32 C) => throw new NotSupportedException();

        protected override void Dispose(Boolean Disposing)
        {

            if (Disposing)
            {
                released.Set();
                released.Dispose();
            }

            base.Dispose(Disposing);

        }

    }

    #endregion


    #region A read shorter than the prefix does not touch the inner stream

    [Test]
    public async Task ReadAsync_ReturnsThePrefixWithoutReadingTheInnerStream()
    {

        using var inner   = new SilentStream();
        var prefixStream  = new PrefixStream("retry: 7000\n\n".ToUTF8Bytes(), inner, LeaveInnerStreamOpen: true);

        // 1024 is what StreamReader asks for, and the mismatch with the 13 bytes on hand
        // is exactly what used to send this read into the inner stream.
        var buffer  = new Byte[1024];
        var read    = await prefixStream.ReadAsync(buffer.AsMemory()).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() => {
            Assert.That(read,                          Is.EqualTo(13));
            Assert.That(buffer[..read].ToUTF8String(), Is.EqualTo("retry: 7000\n\n"));
            Assert.That(inner.ReadAttempts,            Is.Zero, "the inner stream must not be read while prefix bytes remain");
        });

    }

    #endregion

    #region The synchronous path behaves the same

    [Test]
    public void Read_ReturnsThePrefixWithoutReadingTheInnerStream()
    {

        using var inner   = new SilentStream();
        var prefixStream  = new PrefixStream("retry: 7000\n\n".ToUTF8Bytes(), inner, LeaveInnerStreamOpen: true);

        var buffer  = new Byte[1024];
        var read    = 0;

        // Read takes no cancellation token, so the greedy version would block here until the
        // using above releases it. Run it somewhere it can be left behind.
        Assert.That(Task.Run(() => read = prefixStream.Read(buffer, 0, buffer.Length)).Wait(TimeSpan.FromSeconds(5)),
                    Is.True, "Read blocked although 13 bytes were already available");

        Assert.Multiple(() => {
            Assert.That(read,                          Is.EqualTo(13));
            Assert.That(buffer[..read].ToUTF8String(), Is.EqualTo("retry: 7000\n\n"));
            Assert.That(inner.ReadAttempts,            Is.Zero);
        });

    }

    #endregion

    #region A StreamReader over it yields the first line - the SSE case, in miniature

    [Test]
    public async Task StreamReader_ReadsTheFirstLineOutOfThePrefixAlone()
    {

        using var inner   = new SilentStream();
        var prefixStream  = new PrefixStream("retry: 7000\n\n".ToUTF8Bytes(), inner, LeaveInnerStreamOpen: true);

        using var reader  = new StreamReader(prefixStream);

        var firstLine     = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(firstLine, Is.EqualTo("retry: 7000"));

    }

    #endregion

    #region Once the prefix is spent, reads go to the inner stream

    [Test]
    public async Task AfterThePrefix_ReadsContinueOnTheInnerStream()
    {

        using var inner   = new MemoryStream("second".ToUTF8Bytes());
        var prefixStream  = new PrefixStream("first".ToUTF8Bytes(), inner, LeaveInnerStreamOpen: true);

        var buffer        = new Byte[64];

        var first         = await prefixStream.ReadAsync(buffer.AsMemory());
        Assert.Multiple(() => {
            Assert.That(buffer[..first].ToUTF8String(), Is.EqualTo("first"));
            // The prefix is reported as fully consumed: AHTTPServer shifts its connection
            // buffer by this many bytes, so a short read must still account for all of them.
            Assert.That(prefixStream.PrefixConsumed, Is.EqualTo(5));
        });

        var second        = await prefixStream.ReadAsync(buffer.AsMemory());
        Assert.That(buffer[..second].ToUTF8String(), Is.EqualTo("second"));

        Assert.That(await prefixStream.ReadAsync(buffer.AsMemory()), Is.Zero, "the inner stream is at its end");

    }

    #endregion

    #region A caller with a buffer smaller than the prefix gets the rest on the next read

    [Test]
    public async Task AShortBuffer_DrainsThePrefixAcrossSeveralReads()
    {

        using var inner   = new SilentStream();
        var prefixStream  = new PrefixStream("retry: 7000\n\n".ToUTF8Bytes(), inner, LeaveInnerStreamOpen: true);

        var buffer        = new Byte[5];
        var collected     = new List<Byte>();

        for (var i = 0; i < 3; i++)
        {
            var read = await prefixStream.ReadAsync(buffer.AsMemory()).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            collected.AddRange(buffer[..read]);
        }

        Assert.Multiple(() => {
            Assert.That(collected.ToArray().ToUTF8String(), Is.EqualTo("retry: 7000\n\n"));
            Assert.That(inner.ReadAttempts,                 Is.Zero);
        });

    }

    #endregion

}
