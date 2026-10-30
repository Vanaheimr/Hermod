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

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Which stream a DATA writer loop serves next (<see cref="HTTP2SendOrder"/>)
    /// when the priorities tie. Streams that have been served take turns by
    /// recency of their last service. Streams never served had nothing to tell
    /// them apart, and the tie went to whichever came first among the
    /// candidates: the order of the stream manager's dictionary. A dictionary
    /// refills the slots of removed entries first, so on a busy connection a
    /// new stream came ahead of an older one, pick after pick, and an old
    /// response waited for as long as new ones kept coming. h2bench measured
    /// that as single responses waiting 1.5 s at 64 concurrent requests, on a
    /// connection that was busy the whole time.
    ///
    /// Never-served streams now go by stream ID, lowest first: the order the
    /// client asked in, the order RFC 9218, Section 10 has a server serve
    /// non-incremental responses of one urgency in.
    /// </summary>
    [TestFixture]
    public class SendOrderTests
    {

        #region (helpers)

        /// <summary>
        /// A stream with one small chunk queued, the end of its response.
        /// </summary>
        private static HTTP2Stream Pending(UInt32 StreamId, String? Priority = null)
        {

            var stream = new HTTP2Stream(StreamId, InitialSendWindow: 65_535, InitialRecvWindow: 65_535);

            if (Priority is not null)
                stream.Priority = HTTP2Priority.Parse(Priority);

            _ = stream.OutboundQueue.EnqueueAsync(new Byte[13], EndStream: true);

            return stream;

        }

        private static HTTP2Stream? Pick(params HTTP2Stream[] Candidates)

            => HTTP2SendOrder.PickNext(Candidates, ConnectionSendWindow: 1_048_576, out _);

        #endregion


        #region NeverServed_GoByStreamId_WhateverTheCandidatesOrder()

        /// <summary>
        /// Three streams of one urgency, none of them served yet: the lowest
        /// stream ID goes first, in whatever order they are offered.
        /// </summary>
        [Test]
        public void NeverServed_GoByStreamId_WhateverTheCandidatesOrder()
        {

            var stream3 = Pending(3);
            var stream5 = Pending(5);
            var stream7 = Pending(7);

            Assert.Multiple(() =>
            {
                Assert.That(Pick(stream7, stream5, stream3)?.StreamId, Is.EqualTo(3u), "offered 7, 5, 3");
                Assert.That(Pick(stream5, stream3, stream7)?.StreamId, Is.EqualTo(3u), "offered 5, 3, 7");
                Assert.That(Pick(stream7, stream5)?.StreamId,          Is.EqualTo(5u), "offered 7, 5");
            });

        }

        #endregion

        #region StreamManager_OffersANewStreamFirst_OldestIsServedFirst()

        /// <summary>
        /// The case that made the tie matter. Once the stream manager has pruned
        /// closed streams, the next stream it opens is enumerated ahead of older
        /// ones that are still open: the writer loop is offered the newest
        /// first. It serves the oldest all the same.
        /// </summary>
        [Test]
        public void StreamManager_OffersANewStreamFirst_OldestIsServedFirst()
        {

            var manager = new HTTP2StreamManager();

            foreach (var id in new UInt32[] { 1, 3, 5 })
                manager.GetOrCreateStream(id).Open();

            manager.TryGetStream(1)!.CloseRemote();
            manager.TryGetStream(1)!.CloseLocal();
            manager.PruneClosedStreams();

            manager.GetOrCreateStream(7).Open();

            foreach (var id in new UInt32[] { 3, 5, 7 })
                _ = manager.TryGetStream(id)!.OutboundQueue.EnqueueAsync(new Byte[13], EndStream: true);

            var offered = manager.GetSendableStreams();

            Assert.Multiple(() =>
            {

                // The precondition: if this ever lists in stream ID order, the
                // test below no longer shows what it is for.
                Assert.That(offered.Select(stream => stream.StreamId), Is.EqualTo(new UInt32[] { 7, 3, 5 }),
                            "the order the stream manager offers its streams in, after a prune");

                Assert.That(HTTP2SendOrder.PickNext(offered, ConnectionSendWindow: 1_048_576, out _)?.StreamId, Is.EqualTo(3u),
                            "the stream served first");

            });

        }

        #endregion

        #region Served_TakeTurns_BehindNeverServed()

        /// <summary>
        /// What the tie-break leaves as it was: a stream that has been served
        /// waits behind one never served, whatever their IDs, and among served
        /// streams the least recently served goes first.
        /// </summary>
        [Test]
        public void Served_TakeTurns_BehindNeverServed()
        {

            var served3   = Pending(3);
            var served5   = Pending(5);
            var fresh9    = Pending(9);

            served3.LastServedSequence = 20;
            served5.LastServedSequence = 10;

            Assert.Multiple(() =>
            {
                Assert.That(Pick(served3, served5, fresh9)?.StreamId, Is.EqualTo(9u), "a never-served stream against two served ones");
                Assert.That(Pick(served3, served5)?.StreamId,         Is.EqualTo(5u), "the least recently served of two");
            });

        }

        #endregion

        #region Urgency_ComesBeforeStreamId()

        /// <summary>
        /// The stream ID only breaks ties: a later stream of a lower urgency
        /// number is served before an earlier one, and a non-incremental stream
        /// before an incremental one of the same urgency.
        /// </summary>
        [Test]
        public void Urgency_ComesBeforeStreamId()
        {

            Assert.Multiple(() =>
            {
                Assert.That(Pick(Pending(3, "u=4"), Pending(7, "u=1"))?.StreamId, Is.EqualTo(7u), "u=1 on stream 7 against u=4 on stream 3");
                Assert.That(Pick(Pending(3, "i"),   Pending(7))?.StreamId,        Is.EqualTo(7u), "non-incremental stream 7 against incremental stream 3");
            });

        }

        #endregion

    }

}
