/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Collections;
using System.Reflection;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Unit tests for <see cref="HTTP2StreamManager"/>'s closed-stream pruning
    /// (RFC 9113, Section 5.1.1) — pure in-memory state, no network: closed and
    /// reset streams are removed, open streams are kept, and pruning must never
    /// resurrect a stream ID as if it were fresh/idle.
    /// </summary>
    [TestFixture]
    public class HTTP2StreamManagerTests
    {

        #region (helper) DictCount(mgr)

        /// <summary>
        /// The number of live entries in the manager's internal stream dictionary
        /// (private field, via reflection).
        /// </summary>
        private static Int32 DictCount(HTTP2StreamManager mgr)
        {
            var field = typeof(HTTP2StreamManager).GetField("streams", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return ((IDictionary) field.GetValue(mgr)!).Count;
        }

        #endregion


        #region PruneClosedStreams_RemovesClosedKeepsOpen()

        /// <summary>
        /// Fully-closed and reset streams are pruned; still-open streams survive.
        /// </summary>
        [Test]
        public void PruneClosedStreams_RemovesClosedKeepsOpen()
        {

            var mgr = new HTTP2StreamManager();

            // 1, 3, 5 will be fully closed / reset; 7, 9 stay in-flight (Open).
            foreach (var id in new UInt32[] { 1, 3, 5, 7, 9 })
                mgr.GetOrCreateStream(id).Open();

            mgr.TryGetStream(1)!.CloseRemote(); mgr.TryGetStream(1)!.CloseLocal();
            mgr.TryGetStream(3)!.CloseRemote(); mgr.TryGetStream(3)!.CloseLocal();
            mgr.TryGetStream(5)!.Reset();

            Assert.That(DictCount(mgr), Is.EqualTo(5), "all 5 streams present before pruning");

            mgr.PruneClosedStreams();

            Assert.Multiple(() =>
            {
                Assert.That(DictCount(mgr),          Is.EqualTo(2),  "dictionary shrank to 2 after pruning");
                Assert.That(mgr.TryGetStream(1),     Is.Null,        "stream 1 (closed) removed");
                Assert.That(mgr.TryGetStream(3),     Is.Null,        "stream 3 (closed) removed");
                Assert.That(mgr.TryGetStream(5),     Is.Null,        "stream 5 (reset) removed");
                Assert.That(mgr.TryGetStream(7),     Is.Not.Null,    "stream 7 (open) kept");
                Assert.That(mgr.TryGetStream(9),     Is.Not.Null,    "stream 9 (open) kept");
            });

        }

        #endregion

        #region Pruning_DoesNotResurrectStreamIds()

        /// <summary>
        /// After a stream is pruned, <c>LastPeerStreamId</c> must still reflect
        /// the highest stream ever opened, re-using a pruned ID must still be
        /// rejected, and a genuinely higher new ID must still work.
        /// </summary>
        [Test]
        public void Pruning_DoesNotResurrectStreamIds()
        {

            var mgr = new HTTP2StreamManager();

            mgr.GetOrCreateStream(1).Open();
            mgr.TryGetStream(1)!.CloseRemote(); mgr.TryGetStream(1)!.CloseLocal();
            mgr.PruneClosedStreams();

            Assert.Multiple(() =>
            {
                Assert.That(mgr.TryGetStream(1),    Is.Null,        "stream 1 pruned");
                Assert.That(mgr.LastPeerStreamId,   Is.EqualTo(1u), "LastPeerStreamId still reflects the highest stream ever opened");
            });

            // Re-using stream ID 1 after pruning must still be rejected
            // (ID <= LastPeerStreamId) — pruning must not resurrect a closed ID.
            Assert.That(() => mgr.GetOrCreateStream(1),
                        Throws.TypeOf<HTTP2ConnectionException>(),
                        "re-using pruned stream ID 1 is still rejected");

            // A genuinely new, higher stream ID still works fine.
            var s3 = mgr.GetOrCreateStream(3);
            Assert.That(s3.StreamId == 3 && mgr.LastPeerStreamId == 3, Is.True,
                        "opening a new higher stream ID still works after pruning");

        }

        #endregion

        #region AdjustAllStreamWindows_OnlyTouchesRemainingStreams()

        /// <summary>
        /// A SETTINGS-driven window adjustment applies to the remaining open
        /// stream, and a previously pruned stream does not resurface.
        /// </summary>
        [Test]
        public void AdjustAllStreamWindows_OnlyTouchesRemainingStreams()
        {

            var mgr = new HTTP2StreamManager();

            mgr.GetOrCreateStream(1).Open();
            mgr.TryGetStream(1)!.CloseRemote(); mgr.TryGetStream(1)!.CloseLocal();
            var s3 = mgr.GetOrCreateStream(3);
            s3.Open();

            mgr.PruneClosedStreams();

            var before = s3.SendWindow;
            mgr.AdjustAllStreamWindows(1000);

            Assert.Multiple(() =>
            {
                Assert.That(s3.SendWindow,  Is.EqualTo(before + 1000), "remaining open stream's window was adjusted");
                Assert.That(DictCount(mgr), Is.EqualTo(1),             "pruned stream did not resurface");
            });

        }

        #endregion

    }

}
