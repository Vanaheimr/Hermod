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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Block-length padding for DNS messages (RFC 7830, RFC 8467).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A DNS query is short and its length says a great deal about it. Encrypting
    /// the transport hides the name being asked for and leaves the length of the
    /// message behind, which is enough to narrow the field considerably. Padding
    /// is what closes that: RFC 8467 §4.1 — "In Block-Length Padding, a sender
    /// pads each message so that its padded length is a multiple of a chosen
    /// block length."
    /// </para>
    /// <para>
    /// Two block lengths are published, and they are deliberately different.
    /// §4.1: "Clients SHOULD pad queries to the closest multiple of 128 octets",
    /// while a server "SHOULD pad the corresponding response to a multiple of 468
    /// octets". Queries are small and alike, so a small block hides them among
    /// each other; responses vary far more, so the block has to be larger to
    /// collapse them into the same bucket.
    /// </para>
    /// <para>
    /// The block length is a recommendation. What sits underneath it is not:
    /// RFC 7830 §4 — "Responders MUST pad DNS responses when the respective DNS
    /// query included the 'Padding' option, unless doing so would violate the
    /// maximum UDP payload size", and "Responders MUST NOT pad DNS responses when
    /// the respective DNS query did not indicate EDNS(0) support". A responder
    /// gets no say in whether to pad, only in how much.
    /// </para>
    /// <para>
    /// Padding lives inside the OPT record, so a message without EDNS(0) has
    /// nowhere to put it. That is not a limitation being worked around here — it
    /// is the reason the second MUST NOT above can be stated so flatly.
    /// </para>
    /// </remarks>
    public static class DNSPadding
    {

        #region Data

        /// <summary>
        /// RFC 8467 §4.1: "Clients SHOULD pad queries to the closest multiple of
        /// 128 octets."
        /// </summary>
        public const UInt16 QueryBlockSize     = 128;

        /// <summary>
        /// RFC 8467 §4.1: a server "SHOULD pad the corresponding response to a
        /// multiple of 468 octets".
        /// </summary>
        public const UInt16 ResponseBlockSize  = 468;

        #endregion

        #region (static) HasEDNS       (Message)

        /// <summary>
        /// Whether this message indicated EDNS(0) support, which is what decides
        /// whether a response may be padded at all (RFC 7830 §4).
        /// </summary>
        public static Boolean HasEDNS(DNSPacket Message)

            => Message.AdditionalRRs.OfType<OPT>().Any();

        #endregion

        #region (static) IsPadded      (Message)

        /// <summary>
        /// Whether this message carried a Padding option — the condition under
        /// which RFC 7830 §4 turns padding the response from optional into a MUST.
        /// </summary>
        public static Boolean IsPadded(DNSPacket Message)

            => Message.AdditionalRRs.
                       OfType<OPT>().
                       SelectMany(opt => opt.Options).
                       Any(option => option.Code == (UInt16) EDNSOptionCode.Padding);

        #endregion

        #region (static) PayloadSizeOf (Message)

        /// <summary>
        /// The payload size this message advertised, which RFC 7830 §4 turns into
        /// a ceiling on the padded reply: "Padded DNS messages MUST NOT exceed the
        /// number of octets specified in the Requestor's Payload Size field."
        /// </summary>
        /// <returns>
        /// The advertised size, or null when the message announced no EDNS(0) at
        /// all and therefore named no ceiling.
        /// </returns>
        public static UInt16? PayloadSizeOf(DNSPacket Message)

            => Message.AdditionalRRs.OfType<OPT>().FirstOrDefault()?.UDPPayloadSize;

        #endregion

        #region (static) OctetsFor     (MeasuredLength, BlockSize, MaxLength = null)

        /// <summary>
        /// How many padding octets bring a message onto the next block boundary.
        /// </summary>
        /// <param name="MeasuredLength">
        /// The length of the message as serialised <i>with an empty Padding option
        /// already in place</i>. Measuring that way rather than adding four for the
        /// option header keeps the arithmetic honest: the header is in the number
        /// the serialiser produced, so there is nothing left to get wrong about it.
        /// </param>
        /// <param name="BlockSize">The block length to pad to.</param>
        /// <param name="MaxLength">
        /// An upper bound the padded message must not cross — the requestor's
        /// payload size, per RFC 7830 §4: "Padded DNS messages MUST NOT exceed the
        /// number of octets specified in the Requestor's Payload Size field."
        /// </param>
        public static UInt16 OctetsFor(Int32    MeasuredLength,
                                       UInt16   BlockSize,
                                       Int32?   MaxLength   = null)
        {

            if (BlockSize == 0)
                throw new ArgumentOutOfRangeException(nameof(BlockSize), "A block length of zero pads nothing and divides by zero.");

            var remainder = MeasuredLength % BlockSize;
            var octets    = remainder == 0
                                ? 0
                                : BlockSize - remainder;

            // RFC 7830 §4's cap. Note what this does *not* do: it shortens the
            // padding rather than dropping it. A message that cannot reach the
            // block boundary is still better padded than not, and the MUST to pad
            // at all is not conditional on hitting the recommended length.
            if (MaxLength.HasValue && MeasuredLength + octets > MaxLength.Value)
                octets = Math.Max(0, MaxLength.Value - MeasuredLength);

            return (UInt16) Math.Min(octets, UInt16.MaxValue);

        }

        #endregion

        #region (static) WithPadding   (Message, PaddingOctets)

        /// <summary>
        /// The same message with its Padding option set to this many octets.
        /// </summary>
        /// <remarks>
        /// Any Padding option already present is replaced rather than joined,
        /// because RFC 7830 §3 is explicit: "The 'Padding' option MUST occur at
        /// most, once per OPT meta-RR (and hence, at most once per message)."
        /// A message with no OPT record comes back untouched — there is nowhere
        /// for the option to live, and inventing an OPT would change what the
        /// message says about itself.
        /// </remarks>
        public static DNSPacket WithPadding(DNSPacket  Message,
                                            UInt16     PaddingOctets)
        {

            var opt = Message.AdditionalRRs.OfType<OPT>().FirstOrDefault();

            if (opt is null)
                return Message;

            var padded = new OPT(
                             opt.UDPPayloadSize,
                             opt.ExtendedRCODE,
                             opt.Version,
                             opt.Flags,
                             [
                                 .. opt.Options.Where(option => option.Code != (UInt16) EDNSOptionCode.Padding),
                                    new EDNSPaddingOption(PaddingOctets)
                             ]
                         );

            return new DNSPacket(

                       Message.TransactionId,
                       Message.QueryOrResponse,
                       Message.Opcode,
                       Message.AuthoritativeAnswer,
                       Message.Truncation,
                       Message.RecursionDesired,
                       Message.RecursionAvailable,
                       Message.ResponseCode,

                       Message.Questions,
                       Message.AnswerRRs,
                       Message.AuthorityRRs,
                       [
                           .. Message.AdditionalRRs.Where(rr => rr is not OPT),
                              padded
                       ],

                       Message.LocalSocket,
                       Message.RemoteSocket

                   );

        }

        #endregion

    }

}
