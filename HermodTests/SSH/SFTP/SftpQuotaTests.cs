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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M7 SFTP quotas: the per-session size/count tracker rejects at the boundary and names the file to discard.
    /// </summary>
    [TestFixture]
    public class SftpQuotaTests
    {

        #region FileCount_Quota_RejectsTheOneOverTheLimit

        [Test]
        public void FileCount_Quota_RejectsTheOneOverTheLimit()
        {

            var quota = new SftpQuotaTracker(new SftpLimits { MaxFileCount = 2 });

            quota.CheckCanCreate("/a"); quota.RegisterWritable("h1", "/a", WasCreated: true);
            quota.CheckCanCreate("/b"); quota.RegisterWritable("h2", "/b", WasCreated: true);

            var ex = Assert.Throws<SftpQuotaExceededException>(() => quota.CheckCanCreate("/c"));

            Assert.Multiple(() => {
                Assert.That(quota.FilesCreated, Is.EqualTo(2));
                Assert.That(ex!.Code,           Is.EqualTo(SftpStatusCode.Failure));
                Assert.That(ex.PathToCleanup,   Is.EqualTo("/c"));
            });

        }

        #endregion

        #region FileSize_Quota_RejectsTheOverrun_AndNamesTheFile

        [Test]
        public void FileSize_Quota_RejectsTheOverrun_AndNamesTheFile()
        {

            var quota = new SftpQuotaTracker(new SftpLimits { MaxFileSize = 100 });
            quota.RegisterWritable("h1", "/big.bin", WasCreated: true);

            Assert.DoesNotThrow(() => quota.OnWrite("h1", 0, 100));   // exactly at the limit is fine

            var ex = Assert.Throws<SftpQuotaExceededException>(() => quota.OnWrite("h1", 100, 1));   // one byte over

            Assert.That(ex!.PathToCleanup, Is.EqualTo("/big.bin"));

        }

        #endregion

        #region SessionBytes_Quota_AccumulatesAcrossWrites

        [Test]
        public void SessionBytes_Quota_AccumulatesAcrossWrites()
        {

            var quota = new SftpQuotaTracker(new SftpLimits { MaxBytesPerSession = 150 });
            quota.RegisterWritable("h1", "/f1", WasCreated: true);

            Assert.DoesNotThrow(() => quota.OnWrite("h1", 0, 100));   // 100 total

            var ex = Assert.Throws<SftpQuotaExceededException>(() => quota.OnWrite("h1", 100, 100));   // would be 200 > 150

            Assert.Multiple(() => {
                Assert.That(quota.SessionBytesWritten, Is.EqualTo(100), "the rejected write must not count toward the total");
                Assert.That(ex!.PathToCleanup,         Is.EqualTo("/f1"));
            });

        }

        #endregion

    }

}
