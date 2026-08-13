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
    /// SIG(0), RFC 2931: sign one DNS message under a public key, and verify one
    /// against the KEY record that names the signer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The asymmetric counterpart to <see cref="TSIGSigner"/>, and the difference
    /// is the point rather than an implementation detail. TSIG needs the same
    /// secret installed at both ends, which does not scale past a handful of
    /// peers who already trust each other. SIG(0) puts the public half in DNS as
    /// a KEY record, so a verifier can look up a signer it has never met —
    /// at the cost of a public key operation per message, which RFC 2931 §2.4
    /// says to spend sparingly.
    /// </para>
    /// <para>
    /// Like TSIG this works on serialized messages rather than on
    /// <c>DNSPacket</c>, and for the same reason: §3.1 signs the message as it
    /// goes on the wire, so a re-serialization that differs in record order or
    /// compression would authenticate something the peer never saw.
    /// </para>
    /// <para>
    /// What SIG(0) does *not* do is worth stating plainly, because it is a common
    /// misreading. It authenticates a message; it says nothing about whether the
    /// data inside is true. And it is only as good as the KEY lookup behind it:
    /// a verifier that fetches the KEY over unauthenticated DNS has moved the
    /// trust problem rather than solved it, which is why RFC 2931 §2.1 expects
    /// the KEY itself to be DNSSEC-signed or otherwise known in advance.
    /// </para>
    /// </remarks>
    public static class SIG0Signer
    {

        #region Data

        /// <summary>The class a SIG(0) record carries: ANY (RFC 2931 §3).</summary>
        public const UInt16    SIG0ClassANY     = 255;

        /// <summary>The TTL a SIG(0) record carries: zero (RFC 2931 §3).</summary>
        public const UInt32    SIG0TimeToLive   = 0;

        /// <summary>
        /// How far either side of "now" the default validity window reaches.
        /// RFC 2931 §3.1: the times "should not normally extend further than 5
        /// minutes into the past and 5 minutes into the future".
        /// </summary>
        public static readonly TimeSpan DefaultValidityWindow = TimeSpan.FromMinutes(5);

        #endregion


        #region (static) Sign(Message, SignerName, Algorithm, PrivateKey, KeyTag, ...)

        /// <summary>
        /// Append a SIG(0) to a serialized DNS message, signing everything before it.
        /// </summary>
        /// <param name="Message">The complete DNS message, without a SIG(0).</param>
        /// <param name="SignerName">The owner name of the KEY record holding the public half.</param>
        /// <param name="Algorithm">The DNSSEC algorithm number of that key.</param>
        /// <param name="PrivateKey">The private key.</param>
        /// <param name="KeyTag">The key tag of the KEY record (RFC 4034 Appendix B).</param>
        /// <param name="Request">When signing a *response*, the request that produced it — §3.1 folds the whole query into the signature, so a response cannot be replayed against a different question.</param>
        /// <param name="Inception">When the signature becomes valid; five minutes ago when omitted.</param>
        /// <param name="Expiration">When it stops being valid; five minutes from now when omitted.</param>
        /// <returns>The message with the SIG(0) appended and ARCOUNT incremented.</returns>
        public static Byte[] Sign(Byte[]               Message,
                                  DomainName           SignerName,
                                  Byte                 Algorithm,
                                  AsymmetricAlgorithm  PrivateKey,
                                  UInt16               KeyTag,
                                  Byte[]?              Request      = null,
                                  DateTimeOffset?      Inception    = null,
                                  DateTimeOffset?      Expiration   = null)
        {

            if (Message.Length < 12)
                throw new ArgumentException("A DNS message is at least 12 octets.", nameof(Message));

            var now         = DateTimeOffset.UtcNow;
            var inception   = (UInt32) (Inception  ?? now - DefaultValidityWindow).ToUnixTimeSeconds();
            var expiration  = (UInt32) (Expiration ?? now + DefaultValidityWindow).ToUnixTimeSeconds();

            var signedData  = BuildSignedData(Message,
                                              Request,
                                              SignerName,
                                              Algorithm,
                                              expiration,
                                              inception,
                                              KeyTag);

            var signature   = DNSSECSigning.Sign(Algorithm, PrivateKey, signedData);

            var record      = BuildSIG0Record(SignerName,
                                              Algorithm,
                                              expiration,
                                              inception,
                                              KeyTag,
                                              signature);

            var signed      = new Byte[Message.Length + record.Length];
            Buffer.BlockCopy(Message, 0, signed, 0,              Message.Length);
            Buffer.BlockCopy(record,  0, signed, Message.Length, record.Length);

            // §3.1 signs the message "before the reply RR counts have been changed
            // for the inclusion of the SIG(0)" — so the count goes up afterwards,
            // and a verifier has to put it back before checking.
            var arCount     = BinaryPrimitives.ReadUInt16BigEndian(signed.AsSpan(10, 2));
            BinaryPrimitives.WriteUInt16BigEndian(signed.AsSpan(10, 2), (UInt16) (arCount + 1));

            return signed;

        }

        #endregion

        #region (static) Verify(SignedMessage, Key, Now = null, Request = null)

        /// <summary>
        /// Verify the SIG(0) on a received message against a KEY record.
        /// </summary>
        /// <param name="SignedMessage">The message as received, SIG(0) included.</param>
        /// <param name="Key">The KEY record of the claimed signer. Its owner name is what the SIG's signer field has to name (RFC 2931 §3).</param>
        /// <param name="Now">The time to check the validity window against; the current time when omitted.</param>
        /// <param name="Request">When verifying a *response*, the request that was sent.</param>
        public static SIG0VerificationResult Verify(Byte[]            SignedMessage,
                                                    KEY               Key,
                                                    DateTimeOffset?   Now       = null,
                                                    Byte[]?           Request   = null)
        {

            var keyName = Key.DomainName.FullName;

            if (!TryStripSIG0(SignedMessage, out var unsigned, out var sig) ||
                sig is null || unsigned is null)
                return SIG0VerificationResult.Failed(SIG0Failure.NotSigned,
                                                     "The message carries no SIG record as its last additional record.");

            if (!sig.IsTransactionSignature)
                return SIG0VerificationResult.Failed(SIG0Failure.NotATransactionSignature,
                                                     $"The trailing SIG covers type {sig.TypeCovered}, so it signs an RRset rather than this message.",
                                                     sig);

            if (!sig.SignerName.FullName.TrimEnd('.').Equals(keyName.TrimEnd('.'),
                                                             StringComparison.OrdinalIgnoreCase))
                return SIG0VerificationResult.Failed(SIG0Failure.UnknownKey,
                                                     $"The message is signed by '{sig.SignerName}', not by '{keyName}'.",
                                                     sig);

            if (sig.Algorithm != Key.Algorithm)
                return SIG0VerificationResult.Failed(SIG0Failure.UnknownKey,
                                                     $"The signature uses algorithm {sig.Algorithm}, the key is algorithm {Key.Algorithm}.",
                                                     sig);

            var keyTag = DNSSECValidator.ComputeKeyTag(Key.Flags, Key.Protocol, Key.Algorithm, Key.PublicKey);

            if (sig.KeyTag != keyTag)
                return SIG0VerificationResult.Failed(SIG0Failure.UnknownKey,
                                                     $"The signature names key tag {sig.KeyTag}, this key tags to {keyTag}.",
                                                     sig);

            // The cryptography comes before the clock. A message signed with the
            // wrong key must not be reported as a timing problem, or an operator
            // spends the afternoon on NTP.
            var signedData = BuildSignedData(unsigned,
                                             Request,
                                             sig.SignerName,
                                             sig.Algorithm,
                                             sig.SignatureExpiration,
                                             sig.SignatureInception,
                                             sig.KeyTag);

            Boolean verified;

            try
            {
                verified = DNSSECValidator.VerifySignature(sig.Algorithm,
                                                           Key.PublicKey,
                                                           signedData,
                                                           sig.Signature);
            }
            catch (Exception e)
            {
                return SIG0VerificationResult.Failed(SIG0Failure.UnsupportedAlgorithm,
                                                     $"Algorithm {sig.Algorithm} could not be applied: {e.Message}",
                                                     sig);
            }

            if (!verified)
                return SIG0VerificationResult.Failed(SIG0Failure.BadSignature,
                                                     "The signature does not verify under this key.",
                                                     sig);

            // §3.1 — the window is what makes a captured message stop working.
            var now = (UInt32) (Now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();

            if (now < sig.SignatureInception || now > sig.SignatureExpiration)
                return SIG0VerificationResult.Failed(SIG0Failure.OutsideValidityPeriod,
                                                     $"Valid from {sig.SignatureInception} to {sig.SignatureExpiration}, checked at {now}.",
                                                     sig);

            return SIG0VerificationResult.Success(sig);

        }

        #endregion

        #region (static) TryStripSIG0(SignedMessage, out UnsignedMessage, out Record)

        /// <summary>
        /// Split a received message into the part the signature covers and the
        /// SIG(0) itself.
        /// </summary>
        /// <param name="SignedMessage">The message as received.</param>
        /// <param name="UnsignedMessage">The message with the SIG(0) removed and ARCOUNT decremented — what was signed.</param>
        /// <param name="Record">The SIG record that was removed.</param>
        /// <remarks>
        /// Unlike TSIG there is no original-ID field to restore: SIG(0) has no
        /// equivalent, so a forwarder that rewrites the message ID in flight
        /// breaks the signature. That is a real difference between the two
        /// mechanisms and not an oversight here — RFC 2931 simply does not
        /// provide for it.
        /// </remarks>
        public static Boolean TryStripSIG0(Byte[]       SignedMessage,
                                           out Byte[]?  UnsignedMessage,
                                           out SIG?     Record)
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

                var offset = DNSTools.FindLastRecordOffset(SignedMessage);

                if (offset < 0)
                    return false;

                using var stream = new MemoryStream(SignedMessage);
                stream.Position  = offset;

                var owner        = DNSTools.ExtractName(stream);
                var type         = (DNSResourceRecordTypes) stream.ReadUInt16BE();

                if (type != DNSResourceRecordTypes.SIG)
                    return false;

                Record           = new SIG(
                                       DomainName.ParseLenient(owner.Length == 0 ? "." : owner),
                                       stream
                                   );

                var unsigned     = new Byte[offset];
                Buffer.BlockCopy(SignedMessage, 0, unsigned, 0, offset);

                BinaryPrimitives.WriteUInt16BigEndian(unsigned.AsSpan(10, 2), (UInt16) (arCount - 1));

                UnsignedMessage  = unsigned;

                return true;

            }
            catch
            {
                return false;
            }

        }

        #endregion

        #region (static) BuildSignedData(Message, Request, SignerName, Algorithm, Expiration, Inception, KeyTag)

        /// <summary>
        /// The exact bytes a SIG(0) signs, per RFC 2931 §3.1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For a request that is
        /// </para>
        /// <code>data = RDATA | request - SIG(0)</code>
        /// <para>
        /// and for a response, which authenticates the whole exchange rather than
        /// one message,
        /// </para>
        /// <code>data = RDATA | full query | response - SIG(0)</code>
        /// <para>
        /// where RDATA is the SIG's own RDATA with the signature field left off
        /// entirely — omitted, not zeroed — and the message is taken before the
        /// SIG(0) was appended, with ARCOUNT not yet counting it.
        /// </para>
        /// </remarks>
        public static Byte[] BuildSignedData(Byte[]      Message,
                                             Byte[]?     Request,
                                             DomainName  SignerName,
                                             Byte        Algorithm,
                                             UInt32      Expiration,
                                             UInt32      Inception,
                                             UInt16      KeyTag)
        {

            using var data = new MemoryStream();

            // (1) the SIG RDATA, signature field omitted.
            data.WriteUInt16BE((UInt16) SIG.TransactionSignature);
            data.WriteByte    (Algorithm);
            data.WriteByte    (0);                       // labels: meaningless for a SIG(0)
            data.WriteUInt32BE(0);                       // original TTL: likewise
            data.WriteUInt32BE(Expiration);
            data.WriteUInt32BE(Inception);
            data.WriteUInt16BE(KeyTag);

            var signer = DNSTools.SerializeCanonicalName(SignerName.FullName);
            data.Write(signer, 0, signer.Length);

            // (2) the request, when this signature covers a transaction.
            if (Request is not null && Request.Length > 0)
                data.Write(Request, 0, Request.Length);

            // (3) the message being signed, as it will go on the wire.
            data.Write(Message, 0, Message.Length);

            return data.ToArray();

        }

        #endregion

        #region (private static) BuildSIG0Record(SignerName, Algorithm, Expiration, Inception, KeyTag, Signature)

        /// <summary>
        /// Serialize the SIG(0) record itself: root owner name, class ANY, TTL 0
        /// (RFC 2931 §3), and an uncompressed signer's name (RFC 2535 §4.1.7).
        /// </summary>
        private static Byte[] BuildSIG0Record(DomainName  SignerName,
                                              Byte        Algorithm,
                                              UInt32      Expiration,
                                              UInt32      Inception,
                                              UInt16      KeyTag,
                                              Byte[]      Signature)
        {

            using var record = new MemoryStream();

            // "To conserve space, the owner name SHOULD be root (a single zero
            // octet)" — and it is meaningless either way.
            record.WriteByte(0x00);

            record.WriteUInt16BE((UInt16) DNSResourceRecordTypes.SIG);
            record.WriteUInt16BE(SIG0ClassANY);
            record.WriteUInt32BE(SIG0TimeToLive);

            using var rdata = new MemoryStream();

            rdata.WriteUInt16BE((UInt16) SIG.TransactionSignature);
            rdata.WriteByte    (Algorithm);
            rdata.WriteByte    (0);
            rdata.WriteUInt32BE(0);
            rdata.WriteUInt32BE(Expiration);
            rdata.WriteUInt32BE(Inception);
            rdata.WriteUInt16BE(KeyTag);

            var signer = DNSTools.SerializeCanonicalName(SignerName.FullName);
            rdata.Write(signer, 0, signer.Length);

            rdata.Write(Signature, 0, Signature.Length);

            var rdataBytes = rdata.ToArray();
            record.WriteUInt16BE((UInt16) rdataBytes.Length);
            record.Write(rdataBytes, 0, rdataBytes.Length);

            return record.ToArray();

        }

        #endregion

    }

}
