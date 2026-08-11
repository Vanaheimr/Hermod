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

using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>M7 SFTP: the root-jailed local file system — real-disk round-trips and traversal containment.</summary>
    [TestFixture]
    public class LocalSftpFileSystemTests
    {

        #region (helper) temp root

        private String root = "";

        [SetUp]
        public void CreateRoot()
        {
            root = Path.Combine(Path.GetTempPath(), "hermod_sftp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void DeleteRoot()
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }

        #endregion


        #region Local_RoundTrip_WriteReadStatListRename

        [Test]
        [CancelAfter(15000)]
        public async Task Local_RoundTrip_WriteReadStatListRename(CancellationToken CancellationToken)
        {

            var fs      = new LocalSftpFileSystem(root);
            var payload = Encoding.UTF8.GetBytes("firmware-image-v3");

            // Create + write.
            var wh = await fs.OpenAsync("/firmware.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);
            await fs.WriteAsync(wh, 0, payload, CancellationToken);
            await fs.CloseAsync(wh, CancellationToken);

            // The bytes really landed on disk under the root.
            Assert.That(File.Exists(Path.Combine(root, "firmware.bin")), Is.True);

            // Read back.
            var rh   = await fs.OpenAsync("/firmware.bin", SftpOpenFlags.Read, CancellationToken);
            var read = await fs.ReadAsync(rh, 0, 1024, CancellationToken);
            await fs.CloseAsync(rh, CancellationToken);

            var stat = await fs.StatAsync("/firmware.bin", CancellationToken);

            await fs.MakeDirectoryAsync("/logs", CancellationToken);
            var dh      = await fs.OpenDirectoryAsync("/", CancellationToken);
            var listing = await fs.ReadDirectoryAsync(dh, CancellationToken);
            await fs.CloseAsync(dh, CancellationToken);

            await fs.RenameAsync("/firmware.bin", "/firmware-latest.bin", CancellationToken);
            var afterRename = await fs.StatAsync("/firmware-latest.bin", CancellationToken);

            await fs.RemoveAsync("/firmware-latest.bin", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(read,            Is.EqualTo(payload));
                Assert.That(stat.Size,       Is.EqualTo(payload.Length));
                Assert.That(listing.Select(e => e.Name), Does.Contain("firmware.bin").And.Contain("logs"));
                Assert.That(afterRename.Size, Is.EqualTo(payload.Length));
                Assert.That(File.Exists(Path.Combine(root, "firmware-latest.bin")), Is.False);
            });

        }

        #endregion

        #region Local_TraversalAttempts_AreContainedInRoot

        [Test]
        [CancelAfter(15000)]
        public void Local_TraversalAttempts_AreContainedInRoot(CancellationToken CancellationToken)
        {

            // Plant a secret OUTSIDE the root to prove it can never be reached.
            var secret = Path.Combine(Path.GetDirectoryName(root)!, "secret_" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(secret, "top-secret");

            try
            {
                var fs = new LocalSftpFileSystem(root);

                Assert.Multiple(() => {
                    foreach (var escape in new[] { "/../" + Path.GetFileName(secret), "/../../etc/passwd", "/..", "/foo/../../bar" })
                    {
                        var ex = Assert.CatchAsync<SftpException>(async () => await fs.StatAsync(escape, CancellationToken), $"escape: {escape}");
                        Assert.That(ex!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied), $"escape: {escape}");
                    }

                    // A path that uses .. but stays within the root is fine.
                    Assert.DoesNotThrowAsync(async () => await fs.RealPathAsync("/sub/../inside", CancellationToken));
                });
            }
            finally
            {
                try { File.Delete(secret); } catch { }
            }

        }

        #endregion

        #region Local_ReadOnly_RefusesMutations

        [Test]
        [CancelAfter(15000)]
        public async Task Local_ReadOnly_RefusesMutations(CancellationToken CancellationToken)
        {

            File.WriteAllText(Path.Combine(root, "readme.txt"), "hello");
            var fs = new LocalSftpFileSystem(root, ReadOnly: true);

            // Reading is fine …
            var rh   = await fs.OpenAsync("/readme.txt", SftpOpenFlags.Read, CancellationToken);
            var read = await fs.ReadAsync(rh, 0, 64, CancellationToken);
            await fs.CloseAsync(rh, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(Encoding.UTF8.GetString(read), Is.EqualTo("hello"));

                // … but every mutation is denied.
                Assert.That(Assert.CatchAsync<SftpException>(async () => await fs.OpenAsync("/new.txt", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken))!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied));
                Assert.That(Assert.CatchAsync<SftpException>(async () => await fs.MakeDirectoryAsync("/dir", CancellationToken))!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied));
                Assert.That(Assert.CatchAsync<SftpException>(async () => await fs.RemoveAsync("/readme.txt", CancellationToken))!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied));
            });

        }

        #endregion

        #region Local_NestedDirectories_RoundTrip

        [Test]
        [CancelAfter(15000)]
        public async Task Local_NestedDirectories_RoundTrip(CancellationToken CancellationToken)
        {

            var fs = new LocalSftpFileSystem(root);

            await fs.MakeDirectoryAsync("/devices", CancellationToken);
            await fs.MakeDirectoryAsync("/devices/dev07", CancellationToken);

            var wh = await fs.OpenAsync("/devices/dev07/boot.log", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);
            await fs.WriteAsync(wh, 0, Encoding.UTF8.GetBytes("line1\nline2\n"), CancellationToken);
            await fs.CloseAsync(wh, CancellationToken);

            var real = await fs.RealPathAsync("/devices/dev07/boot.log", CancellationToken);
            var stat = await fs.StatAsync("/devices/dev07/boot.log", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(real, Is.EqualTo("/devices/dev07/boot.log"));
                Assert.That(stat.Size, Is.EqualTo(12));
                Assert.That(File.Exists(Path.Combine(root, "devices", "dev07", "boot.log")), Is.True);
            });

        }

        #endregion

    }

}
