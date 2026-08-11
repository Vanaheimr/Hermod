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

using System.Buffers;

using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A bidirectional byte channel over an open SSH session channel (RFC 4254): it sends and receives
    /// channel data with window-based flow control, reassembling inbound <c>CHANNEL_DATA</c> into a byte
    /// stream. Used for subsystems such as SFTP, whose length-prefixed messages flow both ways.
    /// </summary>
    public sealed class SshChannelDuplex : ISftpDuplex
    {

        #region Data

        private const UInt32 InitialWindow  = 2 * 1024 * 1024;
        private const UInt32 MaxPacket      = 32 * 1024;

        private readonly SshTransport  transport;
        private readonly UInt32         remoteChannel;
        private readonly Queue<Byte[]>  inbound  = new ();
        private Byte[]                  current  = [];
        private Int32                   offset;
        private UInt32                  myWindow;
        private Int64                   remoteWindow;
        private Boolean                 remoteClosed;
        private Boolean                 remoteEof;

        #endregion

        #region Constructor(s)

        internal SshChannelDuplex(SshTransport Transport, UInt32 RemoteChannel, UInt32 RemoteWindow)
        {
            this.transport      = Transport;
            this.remoteChannel  = RemoteChannel;
            this.remoteWindow   = RemoteWindow;
            this.myWindow       = InitialWindow;
        }

        #endregion


        #region SendAsync(Data, CancellationToken)

        /// <summary>Send bytes as channel data, chunked to the maximum packet size.</summary>
        public async ValueTask SendAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
        {

            var pos = 0;
            while (pos < Data.Length)
            {
                var chunk = Math.Min((Int32) MaxPacket, Data.Length - pos);
                await transport.SendPacketAsync(BuildChannelData(remoteChannel, Data.Slice(pos, chunk).Span), CancellationToken).ConfigureAwait(false);
                remoteWindow -= chunk;
                pos += chunk;
            }

        }

        #endregion

        #region ReadExactAsync(Count, CancellationToken)

        /// <summary>Read exactly <paramref name="Count"/> bytes from the channel (throws at EOF).</summary>
        public async ValueTask<Byte[]> ReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
        {

            var result  = new Byte[Count];
            var written  = 0;

            while (written < Count)
            {

                if (offset >= current.Length)
                    await FillAsync(CancellationToken).ConfigureAwait(false);

                var available = Math.Min(current.Length - offset, Count - written);
                Array.Copy(current, offset, result, written, available);
                offset   += available;
                written  += available;

            }

            return result;

        }

        /// <summary>Try to read exactly <paramref name="Count"/> bytes; returns null at a clean EOF.</summary>
        public async ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
        {
            try   { return await ReadExactAsync(Count, CancellationToken).ConfigureAwait(false); }
            catch (SshChannelClosedException) { return null; }
        }

        #endregion

        #region ReadSomeAsync(Buffer, CancellationToken)

        /// <summary>
        /// Read up to <c>Buffer.Length</c> bytes (at least one, unless at EOF), returning the count read or 0
        /// at a clean end-of-stream — the byte-stream read semantics a <see cref="Stream"/> expects.
        /// </summary>
        public async ValueTask<Int32> ReadSomeAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
        {

            if (offset >= current.Length)
            {
                try   { await FillAsync(CancellationToken).ConfigureAwait(false); }
                catch (SshChannelClosedException) { return 0; }
            }

            var available = Math.Min(current.Length - offset, Buffer.Length);
            current.AsSpan(offset, available).CopyTo(Buffer.Span);
            offset += available;
            return available;

        }

        #endregion

        #region CloseAsync(CancellationToken)

        /// <summary>Send EOF and CLOSE for this channel.</summary>
        public async ValueTask CloseAsync(CancellationToken CancellationToken = default)
        {
            await transport.SendPacketAsync(Simple(SshMessageNumber.ChannelEof,   remoteChannel), CancellationToken).ConfigureAwait(false);
            await transport.SendPacketAsync(Simple(SshMessageNumber.ChannelClose, remoteChannel), CancellationToken).ConfigureAwait(false);
        }

        #endregion


        #region (private) FillAsync — pull the next inbound data chunk, handling control messages

        private async ValueTask FillAsync(CancellationToken CancellationToken)
        {

            while (true)
            {

                if (inbound.Count > 0)
                {
                    current = inbound.Dequeue();
                    offset  = 0;

                    // Replenish our receive window as we consume data.
                    myWindow = myWindow > (UInt32) current.Length ? myWindow - (UInt32) current.Length : 0;
                    if (myWindow < InitialWindow / 2)
                    {
                        var increment = InitialWindow - myWindow;
                        await transport.SendPacketAsync(BuildWindowAdjust(remoteChannel, increment), CancellationToken).ConfigureAwait(false);
                        myWindow += increment;
                    }

                    return;
                }

                // EOF or CLOSE with no buffered data left means no more inbound bytes will ever arrive.
                if (remoteClosed || remoteEof)
                    throw new SshChannelClosedException();

                var payload = await transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                switch (message)
                {

                    case SshMessageNumber.ChannelData:
                    {
                        var reader = new SshPacketReader(payload);
                        reader.ReadByte(); reader.ReadUInt32();
                        inbound.Enqueue(reader.ReadBinaryString());
                        break;
                    }

                    case SshMessageNumber.ChannelWindowAdjust:
                    {
                        var reader = new SshPacketReader(payload);
                        reader.ReadByte(); reader.ReadUInt32();
                        remoteWindow += reader.ReadUInt32();
                        break;
                    }

                    case SshMessageNumber.ChannelEof:
                        remoteEof = true;
                        break;

                    case SshMessageNumber.ChannelClose:
                        remoteClosed = true;
                        break;

                    default:
                        break;   // ignore other channel/transport traffic

                }

            }

        }

        #endregion

        #region (private) builders

        private static Byte[] BuildChannelData(UInt32 Recipient, ReadOnlySpan<Byte> Data)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelData);
            w.WriteUInt32(Recipient); w.WriteBinaryString(Data);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildWindowAdjust(UInt32 Recipient, UInt32 Increment)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelWindowAdjust);
            w.WriteUInt32(Recipient); w.WriteUInt32(Increment);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] Simple(SshMessageNumber Message, UInt32 Recipient)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) Message); w.WriteUInt32(Recipient);
            return abw.WrittenSpan.ToArray();
        }

        #endregion

    }


    /// <summary>Thrown when a channel is closed while more data was expected.</summary>
    public sealed class SshChannelClosedException : Exception
    {
        /// <summary>Create a new channel-closed exception.</summary>
        public SshChannelClosedException() : base("The SSH channel was closed.") { }
    }

}
