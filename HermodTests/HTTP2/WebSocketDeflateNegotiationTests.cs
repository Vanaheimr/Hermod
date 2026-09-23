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

using H2Deflate = org.GraphDefined.Vanaheimr.Hermod.HTTP2.WebSocketDeflate;
using H3Deflate = org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebSocketDeflate;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// RFC 7692 Section 7.1.2.1 offer negotiation: which <c>Sec-WebSocket-Extensions</c>
    /// offers this stack can honor, and which it must decline.
    ///
    /// Until 2026-09-22 <c>ShouldAccept</c> answered "accepted" to any value whose text
    /// merely contained "permessage-deflate", parameters unread — so an offer capping the
    /// server's compression window at 9 bits was accepted and then served with a 15-bit
    /// window, which a peer sized for 9 could not have decoded. That was caught by the
    /// Autobahn score and fixed by parsing the offer, but nothing pinned the new
    /// behaviour: the fix shipped without a test, and its only witness was a number from
    /// a suite that runs nightly, in Docker, against HTTP/2 alone.
    ///
    /// This is that missing witness, and it is deliberately run against BOTH copies of
    /// the negotiation. Hermod carries the same file twice — <c>HTTP2/WebSocket/</c> and
    /// its byte-identical twin under <c>HTTP3/WebSocket/</c>, whose README asks for the
    /// diff to stay at exactly one line (the namespace). The HTTP/3 copy has no Autobahn
    /// driver pointed at it and would otherwise be certified by nothing at all. Asserting
    /// one table against both means a future fix applied to only one of them fails here,
    /// rather than silently leaving the bug in the half nobody measures.
    ///
    /// Pure in-memory, no network.
    /// </summary>
    [TestFixture]
    public class WebSocketDeflateNegotiationTests
    {

        #region Data

        private delegate Boolean Negotiate(String? ClientOffer, out String? ResponseValue);

        /// <summary>
        /// The two copies under test, named so a failure says which one broke.
        /// </summary>
        private static readonly (String Stack, Negotiate ShouldAccept, String Response)[] Implementations = [
            ("HTTP/2", H2Deflate.ShouldAccept, H2Deflate.Response),
            ("HTTP/3", H3Deflate.ShouldAccept, H3Deflate.Response)
        ];

        /// <summary>
        /// Offer, may we honor it, and why. The Autobahn case numbers name the rows that
        /// suite actually exercises; the rest are the neighbours it never reaches.
        /// </summary>
        private static readonly (String? Offer, Boolean Accept, String Why)[] Offers = [

            // Nothing on offer.
            (null,                                                 false, "no header"),
            ("",                                                   false, "empty header"),
            ("x-webkit-deflate-frame",                             false, "a different extension"),

            // The token must match exactly. The pre-2026-09-22 code compared with
            // StartsWith and would have accepted this one.
            ("permessage-deflate-foo",                             false, "token is a prefix, not a match"),

            // Plain offers, nothing to weigh.
            ("permessage-deflate",                                 true,  "bare offer (Autobahn 13.1/13.2)"),
            (H2Deflate.Offer,                                      true,  "our own client offer"),
            ("permessage-deflate; client_no_context_takeover",     true,  "context takeover is honorable"),
            ("  permessage-deflate  ",                             true,  "surrounding whitespace"),
            ("PERMESSAGE-DEFLATE",                                 true,  "token is case-insensitive"),

            // server_max_window_bits caps OUR window. DeflateStream exposes no control
            // over it, so 15 is the only value we can promise and everything else has to
            // be declined — RFC 7692 Section 7.1.2.1 requires that, it does not merely
            // permit it.
            ("permessage-deflate; server_max_window_bits=15",      true,  "15 is what we do (Autobahn 13.4/13.6)"),
            ("permessage-deflate; server_max_window_bits=\"15\"",  true,  "quoted 15"),
            ("permessage-deflate; server_max_window_bits=9",       false, "9 bits (Autobahn 13.3/13.5)"),
            ("permessage-deflate; server_max_window_bits=8",       false, "8 bits"),
            ("permessage-deflate; server_max_window_bits=14",      false, "14 bits"),
            ("permessage-deflate; server_max_window_bits",         false, "bare, no value — malformed"),
            ("permessage-deflate; server_max_window_bits=abc",     false, "unparseable value"),
            ("permessage-deflate; SERVER_MAX_WINDOW_BITS=9",       false, "parameter name is case-insensitive too"),

            // client_max_window_bits caps the CLIENT's window. Inflating with 15 decodes a
            // stream produced with any smaller window, so we never have to care — accepting
            // these is correct rather than an oversight.
            ("permessage-deflate; client_max_window_bits=9",       true,  "constrains the peer, not us"),
            ("permessage-deflate; client_max_window_bits",         true,  "bare client_max_window_bits"),

            // A list: first honorable offer wins. This is the row that makes Autobahn 13.7
            // negotiable rather than a flat refusal.
            ("permessage-deflate; server_max_window_bits=9, permessage-deflate",
                                                                   true,  "9 declined, fallback taken (Autobahn 13.7)"),
            ("permessage-deflate; server_max_window_bits=9, permessage-deflate; server_max_window_bits=15",
                                                                   true,  "9 declined, 15 taken"),
            ("permessage-deflate; server_max_window_bits=9, permessage-deflate; server_max_window_bits=8",
                                                                   false, "no honorable offer in the list")

        ];

        #endregion


        #region ShouldAccept_HonorsOnlyWhatWeCanDeliver()

        /// <summary>
        /// The whole table against both copies. An accepted offer must also hand back the
        /// response header, and a declined one must hand back null, because the caller
        /// keys "compress this connection" off exactly that.
        /// </summary>
        [Test]
        public void ShouldAccept_HonorsOnlyWhatWeCanDeliver()
        {

            Assert.Multiple(() => {

                foreach (var (stack, shouldAccept, response) in Implementations)
                {
                    foreach (var (offer, accept, why) in Offers)
                    {

                        var where     = $"{stack}: \"{offer ?? "<null>"}\" — {why}";
                        var accepted  = shouldAccept(offer, out var responseValue);

                        Assert.That(accepted,
                                    Is.EqualTo(accept),
                                    where);

                        Assert.That(responseValue,
                                    accept ? Is.EqualTo(response) : Is.Null,
                                    $"{where} (response header)");

                    }
                }

            });

        }

        #endregion

        #region BothCopies_AnswerIdentically()

        /// <summary>
        /// The two copies are meant to stay byte-identical apart from their namespace
        /// line. This states that in behaviour rather than in a README: if a future change
        /// reaches one and not the other, the disagreement lands here — which is how the
        /// HTTP/3 copy came to carry the un-parsed-offer bug for as long as it did.
        /// </summary>
        [Test]
        public void BothCopies_AnswerIdentically()
        {

            Assert.Multiple(() => {

                foreach (var (offer, _, why) in Offers)
                {

                    var h2 = H2Deflate.ShouldAccept(offer, out var h2Response);
                    var h3 = H3Deflate.ShouldAccept(offer, out var h3Response);

                    Assert.That(h3,
                                Is.EqualTo(h2),
                                $"HTTP/2 and HTTP/3 disagree on \"{offer ?? "<null>"}\" — {why}");

                    Assert.That(h3Response,
                                Is.EqualTo(h2Response),
                                $"HTTP/2 and HTTP/3 return different headers for \"{offer ?? "<null>"}\" — {why}");

                }

            });

        }

        #endregion

    }

}
