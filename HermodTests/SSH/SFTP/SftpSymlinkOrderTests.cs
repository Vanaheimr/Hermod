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
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// SSH_FXP_SYMLINK carries two paths, and nothing in the protocol says which is which: the draft
    /// (draft-ietf-secsh-filexfer-02) sends the link first, OpenSSH sends the target first, and most
    /// clients follow OpenSSH. libssh follows the draft towards every server that does not call itself
    /// OpenSSH - so towards ours it did, and got a link at its target's path, pointing at where the link
    /// was meant to be. The server tells the two apart by the client's identification string.
    /// </summary>
    [TestFixture]
    public class SftpSymlinkOrderTests
    {

        #region (private) A scripted channel and a file system that notes the links it is asked for

        private sealed class ScriptedChannel(params Byte[][] Incoming) : ISftpDuplex
        {

            private readonly Byte[]  incoming  = [ .. Incoming.SelectMany(bytes => bytes) ];
            private          Int32   position;

            public List<Byte[]>  Sent  { get; } = [];

            public ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
            {

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
                => ValueTask.CompletedTask;

        }

        private sealed class LinkRecordingFileSystem : ISftpFileSystem
        {

            private readonly InMemorySftpFileSystem inner = new ();

            public List<(String LinkPath, String TargetPath)> Links { get; } = [];

            public ValueTask CreateSymbolicLinkAsync(String LinkPath, String TargetPath, CancellationToken CancellationToken = default)
            {
                Links.Add((LinkPath, TargetPath));
                return ValueTask.CompletedTask;
            }

            public ValueTask<String> OpenAsync(String Path, SftpOpenFlags Flags, CancellationToken CancellationToken = default)
                => inner.OpenAsync(Path, Flags, CancellationToken);

            public ValueTask<Byte[]> ReadAsync(String Handle, Int64 Offset, Int32 Length, CancellationToken CancellationToken = default)
                => inner.ReadAsync(Handle, Offset, Length, CancellationToken);

            public ValueTask WriteAsync(String Handle, Int64 Offset, ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
                => inner.WriteAsync(Handle, Offset, Data, CancellationToken);

            public ValueTask CloseAsync(String Handle, CancellationToken CancellationToken = default)
                => inner.CloseAsync(Handle, CancellationToken);

            public ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => inner.OpenDirectoryAsync(Path, CancellationToken);

            public ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(String Handle, CancellationToken CancellationToken = default)
                => inner.ReadDirectoryAsync(Handle, CancellationToken);

            public ValueTask<SftpFileAttributes> StatAsync(String Path, CancellationToken CancellationToken = default)
                => inner.StatAsync(Path, CancellationToken);

            public ValueTask MakeDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => inner.MakeDirectoryAsync(Path, CancellationToken);

            public ValueTask RemoveAsync(String Path, CancellationToken CancellationToken = default)
                => inner.RemoveAsync(Path, CancellationToken);

            public ValueTask RemoveDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => inner.RemoveDirectoryAsync(Path, CancellationToken);

            public ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default)
                => inner.RenameAsync(OldPath, NewPath, CancellationToken);

            public ValueTask<String> RealPathAsync(String Path, CancellationToken CancellationToken = default)
                => inner.RealPathAsync(Path, CancellationToken);

        }

        // SSH_FXP_INIT (1), version 3.
        private static readonly Byte[] Init = [ 0, 0, 0, 5,  1,  0, 0, 0, 3 ];

        /// <summary>
        /// SSH_FXP_SYMLINK (20) with request id 7 and its two paths in the order given.
        /// </summary>
        private static Byte[] Symlink(String First, String Second)
        {

            var first   = Encoding.UTF8.GetBytes(First);
            var second  = Encoding.UTF8.GetBytes(Second);
            var packet  = new Byte[4 + 1 + 4 + 4 + first.Length + 4 + second.Length];
            var offset  = 0;

            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset), (UInt32) (packet.Length - 4)); offset += 4;
            packet[offset++] = 20;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset), 7);                             offset += 4;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset), (UInt32) first.Length);         offset += 4;
            first. CopyTo(packet, offset);                                                               offset += first.Length;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset), (UInt32) second.Length);        offset += 4;
            second.CopyTo(packet, offset);

            return packet;

        }

        #endregion


        #region Symlink_IsReadInTheOrderTheClientSendsIt

        /// <summary>
        /// The same link, sent the way each client sends it: in OpenSSH's order by default and by
        /// OpenSSH, libssh2 and anyone unnamed, in the draft's by libssh. Each becomes the one link.
        /// </summary>
        [TestCase(null,                                  false)]
        [TestCase("SSH-2.0-OpenSSH_10.0p2 Debian-7",     false)]
        [TestCase("SSH-2.0-libssh2_1.11.1",              false)]
        [TestCase("SSH-2.0-paramiko_5.1.0",              false)]
        [TestCase("SSH-2.0-libssh_0.11.5",               true)]
        [TestCase("SSH-2.0-libssh_0.10.6",               true)]
        public async Task Symlink_IsReadInTheOrderTheClientSendsIt(String? Client, Boolean DraftOrder)
        {

            var identification = Client is null ? null : SshIdentificationString.Parse(Client);
            var fileSystem     = new LinkRecordingFileSystem();
            var channel        = new ScriptedChannel(Init,
                                                     DraftOrder
                                                         ? Symlink("/dir/link.bin", "data.bin")
                                                         : Symlink("data.bin", "/dir/link.bin"));

            Assert.That(SftpServer.SendsSymlinkInDraftOrder(identification), Is.EqualTo(DraftOrder));

            await SftpServer.ServeAsync(channel, fileSystem, ClientIdentification: identification);

            Assert.Multiple(() => {

                Assert.That(fileSystem.Links, Is.EqualTo(new[] { ("/dir/link.bin", "data.bin") }),
                            "the link where the client meant it, holding what it meant it to");

                // SSH_FXP_VERSION, then SSH_FXP_STATUS (101) for request 7 with SSH_FX_OK.
                Assert.That(channel.Sent, Has.Count.EqualTo(2));
                Assert.That(channel.Sent[1][4],                                              Is.EqualTo(101));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(channel.Sent[1].AsSpan(5)), Is.EqualTo(7u));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(channel.Sent[1].AsSpan(9)), Is.EqualTo(0u));

            });

        }

        #endregion

    }

}
