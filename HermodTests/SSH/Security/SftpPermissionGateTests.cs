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
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Regression tests for the SFTP access-profile gate, driven at the <b>wire level</b>.
    ///
    /// <para>
    /// Two bypasses were found by the M9 security review, and both were invisible to the existing
    /// profile tests because those drive the high-level <see cref="SftpClient"/>, which never emits the
    /// packets an attacker would send:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <c>SSH_FXP_EXTENDED</c> fell into a permissive <c>_ =&gt; None</c> fallback, and
    ///     <c>AllowsSftp(None)</c> is always true — so <c>posix-rename@openssh.com</c> (which deletes the
    ///     target first) gave a download-only session arbitrary delete and rename.
    ///   </description></item>
    ///   <item><description>
    ///     <c>SSH_FXP_OPEN</c> counted only <c>Write</c>/<c>Create</c> as writing, while the file system
    ///     also opens for write on <c>Truncate</c>/<c>Append</c> — so <c>pflags = TRUNC</c> alone let a
    ///     download-only session zero out any readable file without ever sending a WRITE.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// These therefore speak raw SFTP over an <see cref="ISftpDuplex"/> rather than going through the
    /// client, which is exactly the attacker's vantage point.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Security")]
    public class SftpPermissionGateTests
    {

        #region (private) a loopback ISftpDuplex pair

        private sealed class PipeDuplex : ISftpDuplex
        {

            private readonly PipeReader reader;
            private readonly PipeWriter writer;

            private PipeDuplex(PipeReader Reader, PipeWriter Writer)
            {
                this.reader = Reader;
                this.writer = Writer;
            }

            public static (ISftpDuplex Client, ISftpDuplex Server) CreatePair()
            {
                var clientToServer = new Pipe();
                var serverToClient = new Pipe();
                return (new PipeDuplex(serverToClient.Reader, clientToServer.Writer),
                        new PipeDuplex(clientToServer.Reader, serverToClient.Writer));
            }

            public async ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
            {

                var buffer = new Byte[Count];
                var filled = 0;

                while (filled < Count)
                {

                    var result = await reader.ReadAsync(CancellationToken).ConfigureAwait(false);

                    if (result.Buffer.IsEmpty && result.IsCompleted)
                    {
                        reader.AdvanceTo(result.Buffer.Start);
                        return filled == 0 ? null : throw new EndOfStreamException();
                    }

                    var take = (Int32) Math.Min(Count - filled, result.Buffer.Length);
                    result.Buffer.Slice(0, take).CopyTo(buffer.AsSpan(filled));
                    filled += take;
                    reader.AdvanceTo(result.Buffer.GetPosition(take));

                }

                return buffer;

            }

            public async ValueTask<Byte[]> ReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
                => await TryReadExactAsync(Count, CancellationToken).ConfigureAwait(false)
                       ?? throw new EndOfStreamException();

            public async ValueTask SendAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
                => await writer.WriteAsync(Data, CancellationToken).ConfigureAwait(false);

            public ValueTask CloseAsync(CancellationToken CancellationToken = default)
            {
                writer.Complete();
                return ValueTask.CompletedTask;
            }

        }

        #endregion

        #region (private) raw SFTP helpers

        private static async ValueTask SendPacketAsync(ISftpDuplex Duplex, Byte[] Body, CancellationToken CancellationToken)
        {
            var framed = new Byte[4 + Body.Length];
            BinaryPrimitives.WriteUInt32BigEndian(framed, (UInt32) Body.Length);
            Body.CopyTo(framed, 4);
            await Duplex.SendAsync(framed, CancellationToken);
        }

        private static async ValueTask<Byte[]> ReadPacketAsync(ISftpDuplex Duplex, CancellationToken CancellationToken)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(await Duplex.ReadExactAsync(4, CancellationToken));
            return await Duplex.ReadExactAsync((Int32) length, CancellationToken);
        }

        /// <summary>Start a server over the given profile/file system and complete the INIT/VERSION exchange.</summary>
        private static async ValueTask<ISftpDuplex> StartServerAsync(SshAccessProfile        Profile,
                                                                     InMemorySftpFileSystem  FileSystem,
                                                                     CancellationToken       CancellationToken)
        {

            var (client, server) = PipeDuplex.CreatePair();

            _ = Task.Run(async () => {
                try { await SftpServer.ServeAsync(server, FileSystem, Profile, CancellationToken: CancellationToken); }
                catch { /* torn down with the test */ }
            }, CancellationToken);

            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Init);
            w.WriteUInt32(3);
            await SendPacketAsync(client, abw.WrittenSpan.ToArray(), CancellationToken);

            var version = await ReadPacketAsync(client, CancellationToken);
            Assert.That(version[0], Is.EqualTo((Byte) SftpPacketType.Version));

            return client;

        }

        /// <summary>Read a reply and return its status code (fails the test if it is not a STATUS packet).</summary>
        private static SftpStatusCode StatusOf(Byte[] Packet)
        {
            Assert.That(Packet[0], Is.EqualTo((Byte) SftpPacketType.Status),
                        $"expected SSH_FXP_STATUS, got packet type {Packet[0]} — the request was carried out instead of refused");
            var reader = new SshPacketReader(Packet.AsSpan(1));
            _ = reader.ReadUInt32();                       // request id
            return (SftpStatusCode) reader.ReadUInt32();
        }

        private static Byte[] OpenPacket(UInt32 RequestId, String Path, UInt32 PFlags)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Open);
            w.WriteUInt32(RequestId);
            w.WriteString(Path);
            w.WriteUInt32(PFlags);
            w.WriteUInt32(0);       // empty attribute set
            return abw.WrittenSpan.ToArray();
        }

        private static async ValueTask<Boolean> ExistsAsync(InMemorySftpFileSystem FileSystem, String Path, CancellationToken CancellationToken)
        {
            try   { await FileSystem.StatAsync(Path, CancellationToken); return true; }
            catch { return false; }
        }

        private static Byte[] PosixRenamePacket(UInt32 RequestId, String OldPath, String NewPath)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Extended);
            w.WriteUInt32(RequestId);
            w.WriteString("posix-rename@openssh.com");
            w.WriteString(OldPath);
            w.WriteString(NewPath);
            return abw.WrittenSpan.ToArray();
        }

        #endregion


        #region DownloadOnly_CannotDeleteViaPosixRenameExtension

        /// <summary>
        /// <c>posix-rename@openssh.com</c> deletes its target before renaming, so it is a delete
        /// primitive. A download-only session must not reach it.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task DownloadOnly_CannotDeleteViaPosixRenameExtension(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            fileSystem.AddFile("/firmware.bin", Encoding.UTF8.GetBytes("firmware-image-v2"));
            fileSystem.AddFile("/decoy.bin",    Encoding.UTF8.GetBytes("decoy"));

            var client = await StartServerAsync(SshAccessProfile.SftpDownloadOnly, fileSystem, CancellationToken);

            await SendPacketAsync(client, PosixRenamePacket(1, "/decoy.bin", "/firmware.bin"), CancellationToken);
            var status         = StatusOf(await ReadPacketAsync(client, CancellationToken));
            var firmwareIntact = await ExistsAsync(fileSystem, "/firmware.bin", CancellationToken);

            Assert.Multiple(() => {

                Assert.That(status, Is.EqualTo(SftpStatusCode.PermissionDenied),
                            "posix-rename must be denied to a download-only session");

                Assert.That(firmwareIntact, Is.True,
                            "the firmware image must still be there — posix-rename deletes its target first");

            });

            await client.CloseAsync(CancellationToken);

        }

        #endregion

        #region UploadOnly_CannotRenameViaPosixRenameExtension

        /// <summary>The upload-only (log collection) preset must stay append-only: no clobbering earlier logs.</summary>
        [Test]
        [CancelAfter(20000)]
        public async Task UploadOnly_CannotRenameViaPosixRenameExtension(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            fileSystem.AddFile("/day1.log", Encoding.UTF8.GetBytes("monday"));
            fileSystem.AddFile("/day2.log", Encoding.UTF8.GetBytes("tuesday"));

            var client = await StartServerAsync(SshAccessProfile.SftpUploadOnly, fileSystem, CancellationToken);

            await SendPacketAsync(client, PosixRenamePacket(1, "/day2.log", "/day1.log"), CancellationToken);
            var status   = StatusOf(await ReadPacketAsync(client, CancellationToken));
            var day1Kept = await ExistsAsync(fileSystem, "/day1.log", CancellationToken);
            var day2Kept = await ExistsAsync(fileSystem, "/day2.log", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(status,   Is.EqualTo(SftpStatusCode.PermissionDenied));
                Assert.That(day1Kept, Is.True, "an earlier log must not be clobbered");
                Assert.That(day2Kept, Is.True);
            });

            await client.CloseAsync(CancellationToken);

        }

        #endregion

        #region DownloadOnly_CannotTruncateOrCreateViaOpenFlags

        /// <summary>
        /// TRUNC and APPEND write to the file just as WRITE does. Opening with them must demand write
        /// permission, or a download-only session zeroes files without ever sending a WRITE packet.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        [TestCase(0x00000010u, "TRUNC alone")]
        [TestCase(0x00000011u, "READ|TRUNC")]
        [TestCase(0x00000004u, "APPEND alone")]
        [TestCase(0x00000014u, "APPEND|TRUNC")]
        public async Task DownloadOnly_CannotTruncateOrCreateViaOpenFlags(UInt32 PFlags, String What, CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            fileSystem.AddFile("/firmware.bin", Encoding.UTF8.GetBytes("firmware-image-v2"));

            var client = await StartServerAsync(SshAccessProfile.SftpDownloadOnly, fileSystem, CancellationToken);

            await SendPacketAsync(client, OpenPacket(1, "/firmware.bin", PFlags), CancellationToken);
            var status = StatusOf(await ReadPacketAsync(client, CancellationToken));

            Assert.That(status, Is.EqualTo(SftpStatusCode.PermissionDenied),
                        $"opening with {What} writes to the file, so it must require write permission");

            await client.CloseAsync(CancellationToken);

        }

        #endregion

        #region DownloadOnly_PlainReadOpenStillWorks

        /// <summary>The tightening must not break the profile's legitimate use: a plain read still opens.</summary>
        [Test]
        [CancelAfter(20000)]
        public async Task DownloadOnly_PlainReadOpenStillWorks(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            fileSystem.AddFile("/firmware.bin", Encoding.UTF8.GetBytes("firmware-image-v2"));

            var client = await StartServerAsync(SshAccessProfile.SftpDownloadOnly, fileSystem, CancellationToken);

            await SendPacketAsync(client, OpenPacket(1, "/firmware.bin", 0x00000001u), CancellationToken);   // READ
            var reply = await ReadPacketAsync(client, CancellationToken);

            Assert.That(reply[0], Is.EqualTo((Byte) SftpPacketType.Handle),
                        "a read-only open must still be granted to a download-only session");

            await client.CloseAsync(CancellationToken);

        }

        #endregion

        #region UnknownExtension_IsDeniedRatherThanWavedThrough

        /// <summary>
        /// The gate now denies by default: an extension nobody classified must not slip through, so that
        /// adding one later cannot silently reopen this hole.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task UnknownExtension_IsDeniedRatherThanWavedThrough(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            var client     = await StartServerAsync(SshAccessProfile.SftpDownloadOnly, fileSystem, CancellationToken);

            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Extended);
            w.WriteUInt32(1);
            w.WriteString("some-future-extension@example.com");
            await SendPacketAsync(client, abw.WrittenSpan.ToArray(), CancellationToken);

            var status = StatusOf(await ReadPacketAsync(client, CancellationToken));

            Assert.That(status, Is.EqualTo(SftpStatusCode.PermissionDenied),
                        "an unclassified extension must be denied under a restrictive profile");

            await client.CloseAsync(CancellationToken);

        }

        #endregion

    }

}
