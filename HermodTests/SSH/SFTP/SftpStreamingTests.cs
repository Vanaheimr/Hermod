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
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>M7 SFTP: the seekable <see cref="SftpFileStream"/> and pipelined large transfers.</summary>
    [TestFixture]
    public class SftpStreamingTests
    {

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


        #region FileStream_WriteThenReadBack_RoundTrips

        [Test]
        [CancelAfter(20000)]
        public async Task FileStream_WriteThenReadBack_RoundTrips(CancellationToken CancellationToken)
        {

            var (sftp, client, server) = await ConnectAsync(new InMemorySftpFileSystem(), CancellationToken);
            var content = RandomNumberGenerator.GetBytes(90_000);   // several stream chunks

            // Write via a stream.
            await using (var ws = await sftp.OpenFileStreamAsync("/data.bin", SftpOpenFlags.Create | SftpOpenFlags.Write | SftpOpenFlags.Truncate, CancellationToken))
                await new MemoryStream(content).CopyToAsync(ws, CancellationToken);

            // Read the whole thing back via a stream.
            using var sink = new MemoryStream();
            await using (var rs = await sftp.OpenFileStreamAsync("/data.bin", SftpOpenFlags.Read, CancellationToken))
            {
                Assert.That(rs.Length, Is.EqualTo(content.Length), "the stream reports the remote size");
                await rs.CopyToAsync(sink, CancellationToken);
            }

            Assert.That(sink.ToArray(), Is.EqualTo(content));

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

        #region FileStream_Seek_ReadsFromArbitraryOffset

        [Test]
        [CancelAfter(20000)]
        public async Task FileStream_Seek_ReadsFromArbitraryOffset(CancellationToken CancellationToken)
        {

            var fs = new InMemorySftpFileSystem();
            fs.AddFile("/log.txt", Encoding.UTF8.GetBytes("0123456789ABCDEF"));
            var (sftp, client, server) = await ConnectAsync(fs, CancellationToken);

            await using var rs = await sftp.OpenFileStreamAsync("/log.txt", SftpOpenFlags.Read, CancellationToken);

            rs.Seek(10, SeekOrigin.Begin);
            var buf = new Byte[4];
            await rs.ReadExactlyAsync(buf.AsMemory(), CancellationToken);

            var tail = new Byte[3];
            rs.Seek(-3, SeekOrigin.End);
            await rs.ReadExactlyAsync(tail.AsMemory(), CancellationToken);

            Assert.Multiple(() => {
                Assert.That(Encoding.UTF8.GetString(buf),  Is.EqualTo("ABCD"));
                Assert.That(Encoding.UTF8.GetString(tail), Is.EqualTo("DEF"));
            });

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

        #region Pipelined_LargeTransfer_IsByteExact

        [Test]
        [CancelAfter(30000)]
        public async Task Pipelined_LargeTransfer_IsByteExact(CancellationToken CancellationToken)
        {

            var (sftp, client, server) = await ConnectAsync(new InMemorySftpFileSystem(), CancellationToken);

            // 1 MiB spans well over the 16-request pipelining window (16 × 30 KiB ≈ 480 KiB).
            var content = RandomNumberGenerator.GetBytes(1024 * 1024);

            await sftp.UploadAsync("/big.bin", content, CancellationToken);
            var downloaded = await sftp.DownloadAsync("/big.bin", CancellationToken);

            Assert.That(downloaded, Is.EqualTo(content), "pipelined upload+download must be byte-for-byte exact");

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

    }

}
