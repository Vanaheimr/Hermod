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

using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Whole files moved between streams and an SFTP server: an upload that never writes over a file
    /// that is there, a download that is taken up at an offset and refuses a file over its limit, a
    /// read error that is not taken for the end of the file - and a host key the policy does not
    /// take, told apart from every other way a handshake fails.
    /// </summary>
    [TestFixture]
    public class SftpTransferTests
    {

        #region Helpers

        private static async Task<(SftpClient Sftp, SshTransport Client, Task Server)> ConnectAsync(ISftpFileSystem FileSystem, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var server = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                    var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                    await SftpServer.ServeAsync(duplex, FileSystem, CancellationToken: CancellationToken);
                }
                catch { }
            }, CancellationToken);

            var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);
            var sftp = await SftpClient.OpenAsync(client, CancellationToken);
            return (sftp, client, server);

        }

        private static async Task EndAsync(SftpClient Sftp, SshTransport Client, Task Server)
        {
            await Sftp.DisposeAsync();
            using (Client) { }
            await Server;
        }

        private static async Task<Byte[]> ReadBackAsync(SftpClient Sftp, String Path, CancellationToken CancellationToken)
            => await Sftp.DownloadAsync(Path, CancellationToken);

        /// <summary>
        /// A stream that hands out at most a few bytes per read, as a network or a compressor does.
        /// </summary>
        private sealed class TrickleStream(Byte[] Content, Int32 PerRead) : Stream
        {
            private Int32 position;
            public  Int32 Reads { get; private set; }
            public override Boolean CanRead  => true;
            public override Boolean CanSeek  => false;
            public override Boolean CanWrite => false;
            public override Int64   Length   => throw new NotSupportedException();
            public override Int64   Position { get => position; set => throw new NotSupportedException(); }
            public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)
            {
                Reads++;
                var count = Math.Min(Math.Min(Count, PerRead), Content.Length - position);
                Array.Copy(Content, position, Buffer, Offset, count);
                position += count;
                return count;
            }
            public override void  Flush() { }
            public override Int64 Seek(Int64 Offset, SeekOrigin Origin) => throw new NotSupportedException();
            public override void  SetLength(Int64 Value)                => throw new NotSupportedException();
            public override void  Write(Byte[] Buffer, Int32 Offset, Int32 Count) => throw new NotSupportedException();
        }

        /// <summary>
        /// A file system that fails a read from an offset on, and may say a file is larger than it is.
        /// </summary>
        private sealed class FaultyFileSystem(ISftpFileSystem Inner) : ISftpFileSystem
        {

            public Int64?  FailReadsFrom  { get; init; }
            public Int64   SizeLie        { get; init; }

            public ValueTask<String> OpenAsync(String Path, SftpOpenFlags Flags, CancellationToken CancellationToken = default)
                => Inner.OpenAsync(Path, Flags, CancellationToken);

            public ValueTask<Byte[]> ReadAsync(String Handle, Int64 Offset, Int32 Length, CancellationToken CancellationToken = default)
                => FailReadsFrom is { } from && Offset >= from
                       ? throw new SftpException(SftpStatusCode.PermissionDenied, "Not any more.")
                       : Inner.ReadAsync(Handle, Offset, Length, CancellationToken);

            public ValueTask WriteAsync(String Handle, Int64 Offset, ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
                => Inner.WriteAsync(Handle, Offset, Data, CancellationToken);

            public ValueTask CloseAsync(String Handle, CancellationToken CancellationToken = default)
                => Inner.CloseAsync(Handle, CancellationToken);

            public ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => Inner.OpenDirectoryAsync(Path, CancellationToken);

            public ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(String Handle, CancellationToken CancellationToken = default)
                => Inner.ReadDirectoryAsync(Handle, CancellationToken);

            public async ValueTask<SftpFileAttributes> StatAsync(String Path, CancellationToken CancellationToken = default)
            {
                var attributes = await Inner.StatAsync(Path, CancellationToken);
                return SizeLie == 0
                           ? attributes
                           : SftpFileAttributes.File((attributes.Size ?? 0) + SizeLie);
            }

            public ValueTask MakeDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => Inner.MakeDirectoryAsync(Path, CancellationToken);

            public ValueTask RemoveAsync(String Path, CancellationToken CancellationToken = default)
                => Inner.RemoveAsync(Path, CancellationToken);

            public ValueTask RemoveDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => Inner.RemoveDirectoryAsync(Path, CancellationToken);

            public ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default)
                => Inner.RenameAsync(OldPath, NewPath, CancellationToken);

            public ValueTask<String> RealPathAsync(String Path, CancellationToken CancellationToken = default)
                => Inner.RealPathAsync(Path, CancellationToken);

        }

        private sealed class Told : IProgress<Int64>
        {
            public List<Int64> Values { get; } = [];
            public void Report(Int64 Value) { lock (Values) Values.Add(Value); }
        }

        #endregion


        #region Upload_AStream_ArrivesWhole

        [Test]
        [CancelAfter(20000)]
        public async Task Upload_AStream_ArrivesWhole(CancellationToken CancellationToken)
        {

            var (sftp, client, server) = await ConnectAsync(new InMemorySftpFileSystem(), CancellationToken);
            var content = RandomNumberGenerator.GetBytes(200_000);
            var source  = new TrickleStream(content, 1000);
            var told    = new Told();

            var uploaded = await sftp.UploadAsync("/upload/log.bin", source, Progress: told, CancellationToken: CancellationToken);

            Assert.That(uploaded,                                               Is.EqualTo(content.Length));
            Assert.That(await ReadBackAsync(sftp, "/upload/log.bin", CancellationToken), Is.EqualTo(content));
            Assert.That(told.Values,                                            Is.Ordered.Ascending);
            Assert.That(told.Values.Last(),                                     Is.EqualTo(content.Length), "the last progress is the whole file");

            // 200 reads of 1000 bytes became 7 chunks of 30 KiB: far fewer round trips than reads.
            Assert.That(told.Values,                                            Has.Count.LessThan(source.Reads / 10));

            await EndAsync(sftp, client, server);

        }

        #endregion

        #region Upload_AnEmptyStream_MakesAnEmptyFile

        [Test]
        [CancelAfter(20000)]
        public async Task Upload_AnEmptyStream_MakesAnEmptyFile(CancellationToken CancellationToken)
        {

            var (sftp, client, server) = await ConnectAsync(new InMemorySftpFileSystem(), CancellationToken);

            var uploaded = await sftp.UploadAsync("/empty.bin", new MemoryStream(), Exclusive: true, CancellationToken: CancellationToken);

            Assert.That(uploaded,                                              Is.Zero);
            Assert.That((await sftp.StatAsync("/empty.bin", CancellationToken)).Size, Is.Zero);

            await EndAsync(sftp, client, server);

        }

        #endregion

        #region Upload_Exclusive_NeverWritesOverAFileThatIsThere

        [Test]
        [CancelAfter(20000)]
        public async Task Upload_Exclusive_NeverWritesOverAFileThatIsThere(CancellationToken CancellationToken)
        {

            var fs       = new InMemorySftpFileSystem();
            var existing = RandomNumberGenerator.GetBytes(5000);
            fs.AddFile("/upload/log.bin", existing);
            var (sftp, client, server) = await ConnectAsync(fs, CancellationToken);

            var refused = (await Assert.ThrowsAsync<SftpException>(async () =>
                               await sftp.UploadAsync("/upload/log.bin", new MemoryStream(RandomNumberGenerator.GetBytes(100)), Exclusive: true, CancellationToken: CancellationToken)))!;

            Assert.That(refused.Code,                                                    Is.EqualTo(SftpStatusCode.Failure));
            Assert.That(await ReadBackAsync(sftp, "/upload/log.bin", CancellationToken), Is.EqualTo(existing), "the file that was there is untouched");

            // The same name is free for a file that is not there yet.
            var content = RandomNumberGenerator.GetBytes(100);
            await sftp.UploadAsync("/upload/log2.bin", new MemoryStream(content), Exclusive: true, CancellationToken: CancellationToken);
            Assert.That(await ReadBackAsync(sftp, "/upload/log2.bin", CancellationToken), Is.EqualTo(content));

            await EndAsync(sftp, client, server);

        }

        #endregion

        #region Upload_NotExclusive_ReplacesTheFile

        [Test]
        [CancelAfter(20000)]
        public async Task Upload_NotExclusive_ReplacesTheFile(CancellationToken CancellationToken)
        {

            var fs = new InMemorySftpFileSystem();
            fs.AddFile("/image.bin", RandomNumberGenerator.GetBytes(5000));
            var (sftp, client, server) = await ConnectAsync(fs, CancellationToken);

            var content = RandomNumberGenerator.GetBytes(100);
            await sftp.UploadAsync("/image.bin", new MemoryStream(content), CancellationToken: CancellationToken);

            Assert.That(await ReadBackAsync(sftp, "/image.bin", CancellationToken), Is.EqualTo(content), "shorter than what was there, and nothing of that is left");

            await EndAsync(sftp, client, server);

        }

        #endregion


        #region Download_IntoAStream_ArrivesWhole

        [Test]
        [CancelAfter(20000)]
        public async Task Download_IntoAStream_ArrivesWhole(CancellationToken CancellationToken)
        {

            var fs      = new InMemorySftpFileSystem();
            var content = RandomNumberGenerator.GetBytes(200_000);
            fs.AddFile("/firmware/image.bin", content);
            var (sftp, client, server) = await ConnectAsync(fs, CancellationToken);

            using var sink = new MemoryStream();
            var told       = new Told();
            var downloaded = await sftp.DownloadAsync("/firmware/image.bin", sink, MaxSize: content.Length, Progress: told, CancellationToken: CancellationToken);

            Assert.That(downloaded,          Is.EqualTo(content.Length));
            Assert.That(sink.ToArray(),      Is.EqualTo(content));
            Assert.That(told.Values,         Is.Ordered.Ascending);
            Assert.That(told.Values.Last(),  Is.EqualTo(content.Length));

            await EndAsync(sftp, client, server);

        }

        #endregion

        #region Download_FromAnOffset_ContinuesWhereItStopped

        [Test]
        [CancelAfter(20000)]
        public async Task Download_FromAnOffset_ContinuesWhereItStopped(CancellationToken CancellationToken)
        {

            var fs      = new InMemorySftpFileSystem();
            var content = RandomNumberGenerator.GetBytes(100_000);
            fs.AddFile("/firmware/image.bin", content);
            var (sftp, client, server) = await ConnectAsync(fs, CancellationToken);

            // What an interrupted attempt left behind ...
            using var sink = new MemoryStream();
            sink.Write(content, 0, 33_333);

            // ... and the rest of it.
            var downloaded = await sftp.DownloadAsync("/firmware/image.bin", sink, Offset: 33_333, CancellationToken: CancellationToken);

            Assert.That(downloaded,      Is.EqualTo(content.Length - 33_333));
            Assert.That(sink.ToArray(),  Is.EqualTo(content));

            // From its very end on there is nothing left, and that is no error.
            using var nothing = new MemoryStream();
            Assert.That(await sftp.DownloadAsync("/firmware/image.bin", nothing, Offset: content.Length, CancellationToken: CancellationToken), Is.Zero);

            await EndAsync(sftp, client, server);

        }

        #endregion

        #region Download_OverTheLimit_ReadsNothing

        [Test]
        [CancelAfter(20000)]
        public async Task Download_OverTheLimit_ReadsNothing(CancellationToken CancellationToken)
        {

            var inner = new InMemorySftpFileSystem();
            inner.AddFile("/firmware/image.bin", RandomNumberGenerator.GetBytes(50_000));

            // Every read fails: were one made, the test would see another exception.
            var (sftp, client, server) = await ConnectAsync(new FaultyFileSystem(inner) { FailReadsFrom = 0 }, CancellationToken);

            using var sink = new MemoryStream();
            var tooLarge = (await Assert.ThrowsAsync<SftpFileTooLargeException>(async () =>
                                await sftp.DownloadAsync("/firmware/image.bin", sink, MaxSize: 49_999, CancellationToken: CancellationToken)))!;

            Assert.That(tooLarge.Size,   Is.EqualTo(50_000));
            Assert.That(tooLarge.Limit,  Is.EqualTo(49_999));
            Assert.That(sink.Length,     Is.Zero);

            await EndAsync(sftp, client, server);

        }

        #endregion

        #region Download_AnOffsetBehindTheEnd_IsRefused

        [Test]
        [CancelAfter(20000)]
        public async Task Download_AnOffsetBehindTheEnd_IsRefused(CancellationToken CancellationToken)
        {

            var fs = new InMemorySftpFileSystem();
            fs.AddFile("/firmware/image.bin", RandomNumberGenerator.GetBytes(1000));
            var (sftp, client, server) = await ConnectAsync(fs, CancellationToken);

            // The remote file is shorter than what is here already: it is another file than before.
            using var sink = new MemoryStream();
            Assert.That((await Assert.ThrowsAsync<SftpException>(async () =>
                             await sftp.DownloadAsync("/firmware/image.bin", sink, Offset: 1001, CancellationToken: CancellationToken)))!.Code,
                        Is.EqualTo(SftpStatusCode.Failure));
            Assert.That(sink.Length, Is.Zero);

            await EndAsync(sftp, client, server);

        }

        #endregion

        #region Download_AReadError_IsNotTheEndOfTheFile

        [Test]
        [CancelAfter(20000)]
        public async Task Download_AReadError_IsNotTheEndOfTheFile(CancellationToken CancellationToken)
        {

            var inner = new InMemorySftpFileSystem();
            inner.AddFile("/firmware/image.bin", RandomNumberGenerator.GetBytes(200_000));
            var (sftp, client, server) = await ConnectAsync(new FaultyFileSystem(inner) { FailReadsFrom = 90_000 }, CancellationToken);

            // Into a stream ...
            using var sink = new MemoryStream();
            Assert.That((await Assert.ThrowsAsync<SftpException>(async () =>
                             await sftp.DownloadAsync("/firmware/image.bin", sink, CancellationToken: CancellationToken)))!.Code,
                        Is.EqualTo(SftpStatusCode.PermissionDenied));

            // ... as bytes: not the first 90000 of them, handed over as if they were the file ...
            Assert.That((await Assert.ThrowsAsync<SftpException>(async () =>
                             await sftp.DownloadAsync("/firmware/image.bin", CancellationToken)))!.Code,
                        Is.EqualTo(SftpStatusCode.PermissionDenied));

            // ... and through a file stream.
            await using var stream = await sftp.OpenFileStreamAsync("/firmware/image.bin", SftpOpenFlags.Read, CancellationToken);
            stream.Position = 95_000;
            Assert.That((await Assert.ThrowsAsync<SftpException>(async () =>
                             await stream.ReadAsync(new Byte[100], CancellationToken)))!.Code,
                        Is.EqualTo(SftpStatusCode.PermissionDenied));

            await EndAsync(sftp, client, server);

        }

        #endregion

        #region Download_AFileShorterThanTheServerSaid_IsNotTheFile

        [Test]
        [CancelAfter(20000)]
        public async Task Download_AFileShorterThanTheServerSaid_IsNotTheFile(CancellationToken CancellationToken)
        {

            var inner = new InMemorySftpFileSystem();
            inner.AddFile("/firmware/image.bin", RandomNumberGenerator.GetBytes(70_000));
            var (sftp, client, server) = await ConnectAsync(new FaultyFileSystem(inner) { SizeLie = 5000 }, CancellationToken);

            using var sink = new MemoryStream();
            var cutShort = (await Assert.ThrowsAsync<SftpException>(async () =>
                                await sftp.DownloadAsync("/firmware/image.bin", sink, CancellationToken: CancellationToken)))!;

            Assert.That(cutShort.Code,     Is.EqualTo(SftpStatusCode.Failure));
            Assert.That(cutShort.Message,  Does.Contain("70000").And.Contain("75000"));

            await EndAsync(sftp, client, server);

        }

        #endregion


        #region AHostKeyThePolicyDoesNotTake_IsToldApart

        [Test]
        [CancelAfter(20000)]
        public async Task AHostKeyThePolicyDoesNotTake_IsToldApart(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var server = Task.Run(async () =>
            {
                try   { using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            Byte[]? asked = null;

            var rejected = (await Assert.ThrowsAsync<SshHostKeyRejectedException>(async () =>
                                await SshTransport.ClientHandshakeAsync(clientPipe,
                                                                        VerifyHostKey:      key => { asked = key; return false; },
                                                                        CancellationToken:  CancellationToken)))!;

            Assert.That(rejected,          Is.InstanceOf<SshWireException>(), "what catches wire exceptions goes on catching this");
            Assert.That(rejected.HostKey,  Is.Not.Empty);
            Assert.That(rejected.HostKey,  Is.EqualTo(asked), "the key the policy was asked about");

            await clientPipe.Output.CompleteAsync();
            await clientPipe.Input.CompleteAsync();
            await server;

        }

        #endregion

    }

}
