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

using System.Buffers.Binary;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// An SFTP packet's length comes from the peer, and its buffer was allocated for it before a byte of
    /// it arrived: four bytes made the reading side allocate up to 2 GiB, or fail on a length beyond
    /// Int32 - and a server that failed left the client's channel open. Now the length is bounded at
    /// 256 KiB, as with OpenSSH, and a server whose client breaks the protocol closes the channel.
    /// </summary>
    [TestFixture]
    public class SftpPacketLengthTests
    {

        #region (private) A scripted channel

        /// <summary>
        /// A channel that hands out the bytes it was given and then its end, and notes what it was asked
        /// to read - without allocating for it, so that a test of a 2 GiB length costs nothing.
        /// </summary>
        private sealed class ScriptedChannel(params Byte[][] Incoming) : ISftpDuplex
        {

            private readonly Byte[]  incoming  = [ .. Incoming.SelectMany(bytes => bytes) ];
            private          Int32   position;

            public List<Int32>   ReadsAskedFor  { get; } = [];
            public List<Byte[]>  Sent           { get; } = [];
            public Boolean       Closed         { get; private set; }

            public ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
            {

                ReadsAskedFor.Add(Count);

                if (Count < 0 || incoming.Length - position < Count)
                {
                    position = incoming.Length;
                    return ValueTask.FromResult<Byte[]?>(null);
                }

                var bytes = incoming.AsSpan(position, Count).ToArray();
                position += Count;

                return ValueTask.FromResult<Byte[]?>(bytes);

            }

            public async ValueTask<Byte[]> ReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
                => await TryReadExactAsync(Count, CancellationToken) ?? throw new SshChannelClosedException();

            public ValueTask SendAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            {
                Sent.Add(Data.ToArray());
                return ValueTask.CompletedTask;
            }

            public ValueTask CloseAsync(CancellationToken CancellationToken = default)
            {
                Closed = true;
                return ValueTask.CompletedTask;
            }

        }

        private static Byte[] Length(UInt32 Length)
        {
            var bytes = new Byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, Length);
            return bytes;
        }

        // SSH_FXP_INIT (1), version 3 - and SSH_FXP_VERSION (2), version 3.
        private static readonly Byte[] Init     = [ 0, 0, 0, 5,  1,  0, 0, 0, 3 ];
        private static readonly Byte[] Version  = [ 0, 0, 0, 5,  2,  0, 0, 0, 3 ];

        #endregion


        #region Server_PacketLengthOutOfBounds_EndsTheSessionAndClosesTheChannel

        [TestCase(262_145u)]
        [TestCase(0x7FFF_FFFFu)]
        [TestCase(0xFFFF_FFFFu)]
        [TestCase(0u)]
        public async Task Server_PacketLengthOutOfBounds_EndsTheSessionAndClosesTheChannel(UInt32 PacketLength)
        {

            var channel = new ScriptedChannel(Init, Length(PacketLength));

            Assert.That(async () => await SftpServer.ServeAsync(channel, new InMemorySftpFileSystem()),
                        Throws.InstanceOf<SshWireException>());

            Assert.Multiple(() => {

                Assert.That(channel.ReadsAskedFor.Max(), Is.LessThanOrEqualTo(SftpServer.MaxPacketLength), "a read was sized by the client's length");
                Assert.That(channel.ReadsAskedFor.Min(), Is.GreaterThan(0),                                 "a read was sized by the client's length");
                Assert.That(channel.Closed,              Is.True,                                           "the channel was left open for a client that will wait for an answer");

            });

        }

        #endregion

        #region Server_PacketAtTheLimit_IsServed

        /// <summary>
        /// 256 KiB is still read, and answered: what OpenSSH's server takes, this one takes.
        /// </summary>
        [Test]
        public async Task Server_PacketAtTheLimit_IsServed()
        {

            // SSH_FXP_REALPATH (16), id 7, and a path to fill the packet to the limit.
            var path    = new Byte[SftpServer.MaxPacketLength - 1 - 4 - 4];
            Array.Fill(path, (Byte) 'a');

            var packet  = new Byte[4 + SftpServer.MaxPacketLength];
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(0), (UInt32) SftpServer.MaxPacketLength);
            packet[4] = 16;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(5), 7);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(9), (UInt32) path.Length);
            path.CopyTo(packet, 13);

            var channel = new ScriptedChannel(Init, packet);

            await SftpServer.ServeAsync(channel, new InMemorySftpFileSystem());

            Assert.Multiple(() => {

                Assert.That(channel.ReadsAskedFor, Does.Contain(SftpServer.MaxPacketLength));
                Assert.That(channel.Sent,          Has.Count.EqualTo(2), "VERSION, and the answer to REALPATH");
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(channel.Sent[1].AsSpan(5)), Is.EqualTo(7u), "the answer is not to request 7");

            });

        }

        #endregion

        #region Client_PacketLengthOutOfBounds_FailsTheClient

        /// <summary>
        /// The client reads the server's packets the same way, and is bounded the same way: a request
        /// fails, rather than the client allocating what a server says.
        /// </summary>
        [TestCase(262_145u)]
        [TestCase(0x7FFF_FFFFu)]
        [TestCase(0xFFFF_FFFFu)]
        public async Task Client_PacketLengthOutOfBounds_FailsTheClient(UInt32 PacketLength)
        {

            var channel = new ScriptedChannel(Version, Length(PacketLength));

            await using var sftp = await SftpClient.OpenAsync(channel);

            Assert.That(async () => await sftp.ListDirectoryAsync("/"), Throws.Exception);

            Assert.Multiple(() => {
                Assert.That(channel.ReadsAskedFor.Max(), Is.LessThanOrEqualTo(SftpServer.MaxPacketLength), "a read was sized by the server's length");
                Assert.That(channel.ReadsAskedFor.Min(), Is.GreaterThan(0),                                 "a read was sized by the server's length");
            });

        }

        #endregion

    }

}
