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
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Transaction signatures, RFC 8945: sign an outgoing DNS message with a
    /// shared secret, and verify an incoming one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This works on serialized messages rather than on <c>DNSPacket</c>, because
    /// that is what TSIG actually authenticates. §4.3.2 signs "the DNS message in
    /// wire format, before the TSIG RR has been added" — the exact octets, not a
    /// re-serialization of an object graph, which could differ in compression or
    /// record order and would then authenticate something the peer never saw.
    /// </para>
    /// <para>
    /// TSIG is a meta-RR (§4.1): it is never stored in a zone, never cached, and
    /// exists only for the lifetime of one message. It is always the last record
    /// in the additional section, and it is not itself covered by the MAC.
    /// </para>
    /// </remarks>
    public static class TSIGSigner
    {

        #region Data

        /// <summary>
        /// The class every TSIG record carries: ANY (RFC 8945 §4.2).
        /// </summary>
        public const UInt16  TSIGClassANY      = 255;

        /// <summary>
        /// The TTL every TSIG record carries: zero (RFC 8945 §4.2).
        /// </summary>
        public const UInt32  TSIGTimeToLive    = 0;

        /// <summary>
        /// The default fudge in seconds; RFC 8945 §10 recommends 300.
        /// </summary>
        public const UInt16  DefaultFudge      = 300;

        /// <summary>
        /// TSIG error: the signature does not verify (RFC 8945 §4.3).
        /// </summary>
        public const UInt16  BADSIG            = 16;

        /// <summary>
        /// TSIG error: the key is not known to the verifier.
        /// </summary>
        public const UInt16  BADKEY            = 17;

        /// <summary>
        /// TSIG error: the time signed falls outside the fudge window.
        /// </summary>
        public const UInt16  BADTIME           = 18;

        #endregion


        #region (static) Sign  (Message, Key, TimeSigned = null, Fudge = 300, RequestMAC = null)

        /// <summary>
        /// Append a TSIG record to a serialized DNS message, signing everything
        /// before it.
        /// </summary>
        /// <param name="Message">The complete DNS message, without a TSIG record.</param>
        /// <param name="Key">The shared secret to sign with.</param>
        /// <param name="TimeSigned">Unix seconds; the current time when omitted.</param>
        /// <param name="Fudge">The permitted clock skew in seconds.</param>
        /// <param name="RequestMAC">When signing a *response*, the MAC of the request being answered — §4.3.1 folds it in so a response cannot be replayed against a different request.</param>
        /// <returns>The message with the TSIG record appended and ARCOUNT incremented.</returns>
        public static Byte[] Sign(Byte[]    Message,
                                  TSIGKey   Key,
                                  UInt64?   TimeSigned   = null,
                                  UInt16    Fudge        = DefaultFudge,
                                  Byte[]?   RequestMAC   = null)
        {

            if (Message.Length < 12)
                throw new ArgumentException("A DNS message is at least 12 octets.", nameof(Message));

            var timeSigned  = TimeSigned ?? (UInt64) DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var originalId  = BinaryPrimitives.ReadUInt16BigEndian(Message.AsSpan(0, 2));

            var mac         = ComputeMAC(Message,
                                         Key,
                                         timeSigned,
                                         Fudge,
                                         Error:       0,
                                         OtherData:   [],
                                         RequestMAC:  RequestMAC);

            var record      = BuildTSIGRecord(Key, timeSigned, Fudge, mac, originalId, Error: 0, OtherData: []);

            var signed      = new Byte[Message.Length + record.Length];
            Buffer.BlockCopy(Message, 0, signed, 0,              Message.Length);
            Buffer.BlockCopy(record,  0, signed, Message.Length, record.Length);

            // ARCOUNT counts the TSIG record, even though the MAC did not cover it.
            var arCount     = BinaryPrimitives.ReadUInt16BigEndian(signed.AsSpan(10, 2));
            BinaryPrimitives.WriteUInt16BigEndian(signed.AsSpan(10, 2), (UInt16) (arCount + 1));

            return signed;

        }

        #endregion

        #region (static) Verify(SignedMessage, Key, Now = null, RequestMAC = null)

        /// <summary>
        /// Verify the TSIG record on a received message.
        /// </summary>
        /// <param name="SignedMessage">The message as received, TSIG record included.</param>
        /// <param name="Key">The shared secret expected to have signed it.</param>
        /// <param name="Now">Unix seconds to check the time window against; the current time when omitted.</param>
        /// <param name="RequestMAC">When verifying a *response*, the MAC of the request that was sent.</param>
        public static TSIGVerificationResult Verify(Byte[]    SignedMessage,
                                                    TSIGKey   Key,
                                                    UInt64?   Now          = null,
                                                    Byte[]?   RequestMAC   = null)
        {

            if (!TryStripTSIG(SignedMessage, out var unsigned, out var tsig) ||
                tsig is null || unsigned is null)
                return TSIGVerificationResult.Failed(BADSIG, "The message carries no TSIG record as its last additional record.");

            if (tsig.DomainName.ToString().TrimEnd('.').Equals(Key.Name.FullName.TrimEnd('.'),
                                                              StringComparison.OrdinalIgnoreCase) == false)
                return TSIGVerificationResult.Failed(BADKEY, $"The message is signed with key '{tsig.DomainName}', not '{Key.Name}'.");

            if (tsig.AlgorithmName != Key.Algorithm)
                return TSIGVerificationResult.Failed(BADKEY, $"The message is signed with algorithm '{tsig.AlgorithmName}', not '{Key.Algorithm}'.");

            if (!TSIGAlgorithms.IsSupported(tsig.AlgorithmName))
                return TSIGVerificationResult.Failed(BADKEY, $"Algorithm '{tsig.AlgorithmName}' is not supported.");

            // §5.2.3: the time check comes before the MAC comparison is trusted,
            // but the MAC is still computed — a wrong key must not be reported as
            // a clock problem.
            var expected = ComputeMAC(unsigned,
                                      Key,
                                      tsig.TimeSigned,
                                      tsig.Fudge,
                                      tsig.Error,
                                      tsig.OtherData,
                                      RequestMAC);

            if (!CryptographicOperations.FixedTimeEquals(expected, tsig.MAC))
                return TSIGVerificationResult.Failed(BADSIG, "The MAC does not verify under this key.");

            var now   = Now ?? (UInt64) DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var skew  = now > tsig.TimeSigned
                            ? now - tsig.TimeSigned
                            : tsig.TimeSigned - now;

            if (skew > tsig.Fudge)
                return TSIGVerificationResult.Failed(BADTIME, $"Signed at {tsig.TimeSigned}, checked at {now}: {skew} s apart, fudge is {tsig.Fudge} s.");

            return TSIGVerificationResult.Success(tsig);

        }

        #endregion

        #region (static) Verify(SignedMessage, Keys, Now = null, RequestMAC = null)

        /// <summary>
        /// Verify a received message against whichever of the given keys it
        /// claims to be signed with.
        /// </summary>
        /// <param name="SignedMessage">The message as received.</param>
        /// <param name="Keys">The keys this party holds.</param>
        /// <param name="Now">Unix seconds to check the time window against.</param>
        /// <param name="RequestMAC">When verifying a response, the MAC of the request that was sent.</param>
        /// <remarks>
        /// A responder cannot know which key to use before looking: the key is
        /// named by the TSIG record's owner name, which is inside the message it
        /// is about to authenticate. Reading that name first is safe — it selects
        /// a candidate, it does not grant anything.
        /// </remarks>
        public static TSIGVerificationResult Verify(Byte[]                SignedMessage,
                                                    IEnumerable<TSIGKey>  Keys,
                                                    UInt64?               Now          = null,
                                                    Byte[]?               RequestMAC   = null)
        {

            if (!TryStripTSIG(SignedMessage, out _, out var tsig) || tsig is null)
                return TSIGVerificationResult.Failed(BADSIG, "The message carries no TSIG record as its last additional record.");

            var named = Keys.FirstOrDefault(key => key.Name.FullName.TrimEnd('.').
                                                       Equals(tsig.DomainName.ToString().TrimEnd('.'),
                                                              StringComparison.OrdinalIgnoreCase));

            if (named is null)
                return TSIGVerificationResult.Failed(BADKEY, $"No key named '{tsig.DomainName}' is configured.", tsig);

            return Verify(SignedMessage, named, Now, RequestMAC);

        }

        #endregion

        #region (static) BuildErrorResponse(SignedRequest, Error, Key)

        /// <summary>
        /// Build the NOTAUTH reply that RFC 8945 §5.2 owes the sender of a
        /// request that failed verification.
        /// </summary>
        /// <param name="SignedRequest">The request that failed.</param>
        /// <param name="Error">The TSIG error code to report.</param>
        /// <param name="Key">The key to sign the reply with, or null when the failure was that no such key exists.</param>
        /// <remarks>
        /// The reply echoes the header and question and carries a TSIG with the
        /// error code and an empty MAC. It is unsigned when the failure was
        /// BADKEY or BADSIG: there is no shared secret to sign with, and §5.2
        /// says so explicitly — a MAC computed with a key the peer does not hold
        /// would be noise.
        /// </remarks>
        public static Byte[]? BuildErrorResponse(Byte[]    SignedRequest,
                                                 UInt16    Error,
                                                 TSIGKey?  Key   = null)
        {

            if (SignedRequest.Length < 12)
                return null;

            try
            {

                if (!TryStripTSIG(SignedRequest, out var unsigned, out var tsig) ||
                    unsigned is null || tsig is null)
                    return null;

                var response = new Byte[unsigned.Length];
                Buffer.BlockCopy(unsigned, 0, response, 0, unsigned.Length);

                // QR = 1, RCODE = NOTAUTH (9), everything else as it arrived.
                var flags    = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));
                flags        = (UInt16) ((flags | 0x8000) & 0xFFF0 | (UInt16) DNSResponseCodes.NotAuthorized);
                BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), flags);

                // No answers of any kind travel with an authentication failure.
                BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(6,  2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(8,  2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(10, 2), 0);

                var errorKey  = Key ?? new TSIGKey(DomainName.ParseLenient(tsig.DomainName.ToString()),
                                                   [],
                                                   tsig.AlgorithmName);

                var record    = BuildTSIGRecord(errorKey,
                                                tsig.TimeSigned,
                                                tsig.Fudge,
                                                MAC:         [],
                                                OriginalID:  tsig.OriginalID,
                                                Error:       Error,
                                                OtherData:   []);

                var result    = new Byte[response.Length + record.Length];
                Buffer.BlockCopy(response, 0, result, 0,               response.Length);
                Buffer.BlockCopy(record,   0, result, response.Length, record.Length);

                BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(10, 2), 1);

                return result;

            }
            catch
            {
                return null;
            }

        }

        #endregion

        #region (static) ComputeMAC(Message, Key, TimeSigned, Fudge, Error, OtherData, RequestMAC = null)

        /// <summary>
        /// Compute the MAC over a message and the TSIG variables, per RFC 8945 §4.3.
        /// </summary>
        /// <param name="Message">The DNS message *without* the TSIG record, and with ARCOUNT already excluding it.</param>
        /// <param name="Key">The shared secret.</param>
        /// <param name="TimeSigned">Unix seconds, carried as a 48-bit field.</param>
        /// <param name="Fudge">The permitted clock skew in seconds.</param>
        /// <param name="Error">The TSIG error code.</param>
        /// <param name="OtherData">The other-data field, normally empty.</param>
        /// <param name="RequestMAC">The request's MAC when signing or checking a response.</param>
        /// <remarks>
        /// The digest input is, in order (§4.3.3): the request MAC prefixed with
        /// its 2-octet length if this is a response; the DNS message; then the
        /// "TSIG variables" — the key name in canonical form, class ANY, TTL 0,
        /// the algorithm name in canonical form, the 48-bit time signed, the
        /// fudge, the error, and the other-data with its length.
        ///
        /// Note what is *not* in there: the TSIG record's own RDATA as it appears
        /// on the wire. The variables are re-serialized in a fixed form, so a
        /// peer that lays the record out differently still computes the same MAC.
        /// </remarks>
        public static Byte[] ComputeMAC(Byte[]    Message,
                                        TSIGKey   Key,
                                        UInt64    TimeSigned,
                                        UInt16    Fudge,
                                        UInt16    Error,
                                        Byte[]    OtherData,
                                        Byte[]?   RequestMAC   = null)
        {

            using var digestInput = new MemoryStream();

            // §4.3.1 — a response folds in the MAC of the request it answers.
            if (RequestMAC is not null && RequestMAC.Length > 0)
            {
                digestInput.WriteUInt16BE((UInt16) RequestMAC.Length);
                digestInput.Write(RequestMAC, 0, RequestMAC.Length);
            }

            // §4.3.2 — the message, exactly as it goes on the wire.
            digestInput.Write(Message, 0, Message.Length);

            // §4.3.3 — the TSIG variables.
            var keyName    = DNSTools.SerializeCanonicalName(Key.Name.FullName);
            var algorithm  = DNSTools.SerializeCanonicalName(Key.Algorithm.FullName);

            digestInput.Write(keyName, 0, keyName.Length);
            digestInput.WriteUInt16BE(TSIGClassANY);
            digestInput.WriteUInt32BE(TSIGTimeToLive);
            digestInput.Write(algorithm, 0, algorithm.Length);

            // Time signed is 48 bits: the top 16, then the bottom 32.
            digestInput.WriteUInt16BE((UInt16) ((TimeSigned >> 32) & 0xFFFF));
            digestInput.WriteUInt32BE((UInt32) ( TimeSigned        & 0xFFFFFFFF));

            digestInput.WriteUInt16BE(Fudge);
            digestInput.WriteUInt16BE(Error);
            digestInput.WriteUInt16BE((UInt16) OtherData.Length);
            digestInput.Write(OtherData, 0, OtherData.Length);

            return TSIGAlgorithms.ComputeHMAC(Key.Algorithm,
                                              Key.Secret,
                                              digestInput.ToArray());

        }

        #endregion

        #region (static) TryStripTSIG(SignedMessage, out UnsignedMessage, out Record)

        /// <summary>
        /// Split a received message into the part the MAC covers and the TSIG
        /// record itself.
        /// </summary>
        /// <param name="SignedMessage">The message as received.</param>
        /// <param name="UnsignedMessage">The message with the TSIG record removed and ARCOUNT decremented — what the MAC was computed over.</param>
        /// <param name="Record">The TSIG record that was removed.</param>
        /// <remarks>
        /// The message ID is restored to the TSIG's Original ID. A forwarder is
        /// allowed to rewrite the ID in flight (§5.3.1), and the MAC was taken
        /// over the original, so leaving the new one in place would fail every
        /// such message.
        /// </remarks>
        public static Boolean TryStripTSIG(Byte[]        SignedMessage,
                                           out Byte[]?   UnsignedMessage,
                                           out TSIG?     Record)
        {

            UnsignedMessage  = null;
            Record           = null;

            if (SignedMessage.Length < 12)
                return false;

            var arCount = BinaryPrimitives.ReadUInt16BigEndian(SignedMessage.AsSpan(10, 2));

            if (arCount == 0)
                return false;

            try
            {

                // Walk the message to find where the last record starts. TSIG must
                // be the last additional record (§5.1), so anything else is not a
                // signed message as far as this code is concerned.
                var offset = DNSTools.FindLastRecordOffset(SignedMessage);

                if (offset < 0)
                    return false;

                using var stream = new MemoryStream(SignedMessage);
                stream.Position  = offset;

                var owner        = DNSTools.ExtractName(stream);
                var type         = (DNSResourceRecordTypes) stream.ReadUInt16BE();

                if (type != DNSResourceRecordTypes.TSIG)
                    return false;

                stream.Position  = offset;
                Record           = new TSIG(DomainName.ParseLenient(owner), ReReadFrom(stream, offset));

                var unsigned     = new Byte[offset];
                Buffer.BlockCopy(SignedMessage, 0, unsigned, 0, offset);

                BinaryPrimitives.WriteUInt16BigEndian(unsigned.AsSpan(10, 2), (UInt16) (arCount - 1));
                BinaryPrimitives.WriteUInt16BigEndian(unsigned.AsSpan(0,  2), Record.OriginalID);

                UnsignedMessage  = unsigned;

                return true;

            }
            catch
            {
                return false;
            }

        }

        #endregion


        #region (private static) BuildTSIGRecord (Key, TimeSigned, Fudge, MAC, OriginalID, Error, OtherData)

        /// <summary>
        /// Serialize a TSIG record. Names are written uncompressed: §4.2 forbids
        /// compression here, because a verifier that re-serializes the record
        /// has no message context to resolve a pointer against.
        /// </summary>
        private static Byte[] BuildTSIGRecord(TSIGKey  Key,
                                              UInt64   TimeSigned,
                                              UInt16   Fudge,
                                              Byte[]   MAC,
                                              UInt16   OriginalID,
                                              UInt16   Error,
                                              Byte[]   OtherData)
        {

            using var record = new MemoryStream();

            var owner        = DNSTools.SerializeCanonicalName(Key.Name.FullName);
            record.Write(owner, 0, owner.Length);

            record.WriteUInt16BE((UInt16) DNSResourceRecordTypes.TSIG);
            record.WriteUInt16BE(TSIGClassANY);
            record.WriteUInt32BE(TSIGTimeToLive);

            using var rdata  = new MemoryStream();

            var algorithm    = DNSTools.SerializeCanonicalName(Key.Algorithm.FullName);
            rdata.Write(algorithm, 0, algorithm.Length);

            rdata.WriteUInt16BE((UInt16) ((TimeSigned >> 32) & 0xFFFF));
            rdata.WriteUInt32BE((UInt32) ( TimeSigned        & 0xFFFFFFFF));
            rdata.WriteUInt16BE(Fudge);
            rdata.WriteUInt16BE((UInt16) MAC.Length);
            rdata.Write(MAC, 0, MAC.Length);
            rdata.WriteUInt16BE(OriginalID);
            rdata.WriteUInt16BE(Error);
            rdata.WriteUInt16BE((UInt16) OtherData.Length);
            rdata.Write(OtherData, 0, OtherData.Length);

            var rdataBytes   = rdata.ToArray();
            record.WriteUInt16BE((UInt16) rdataBytes.Length);
            record.Write(rdataBytes, 0, rdataBytes.Length);

            return record.ToArray();

        }

        #endregion

        #region (private static) ReReadFrom(Stream, Offset)

        /// <summary>
        /// Rewind a stream to just past the owner name so the TSIG constructor,
        /// which expects to start at CLASS, can read the record.
        /// </summary>
        private static Stream ReReadFrom(Stream Stream, Int32 Offset)
        {

            Stream.Position = Offset;

            DNSTools.ExtractName(Stream);
            Stream.Position += 2;                      // TYPE, which the caller already read

            return Stream;

        }

        #endregion

    }

}
