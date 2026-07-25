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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Security.Cryptography;


    /// <summary>
    /// Digest fields (RFC 9530): <c>Content-Digest</c>, <c>Repr-Digest</c> and the
    /// two <c>Want-…</c> fields that ask for them — an integrity check on the bytes
    /// themselves, end to end, independent of whatever the transport already
    /// guarantees hop by hop.
    ///
    /// The two digests answer different questions, and the difference is the whole
    /// point of having both:
    ///
    ///   * <c>Content-Digest</c> describes the <i>content</i> — exactly the octets
    ///     carried in this one message. For a <c>206</c> that is the slice, not the
    ///     resource.
    ///   * <c>Repr-Digest</c> describes the <i>selected representation</i>
    ///     (RFC 9110, Section 8.1) — the resource as a whole, unaffected by
    ///     <c>Content-Range</c>. It is what lets a receiver verify a download it
    ///     assembled out of several range responses, none of which it could have
    ///     checked against a content digest at the time.
    ///
    /// Both are computed <b>after</b> any content coding: representation data is
    /// defined to be in its <c>Content-Encoding</c>, so a sender digests what it
    /// puts on the wire and a receiver digests what it took off, before decoding.
    /// Doing it the other way round produces a digest neither peer can reproduce.
    ///
    /// Only <c>sha-256</c> and <c>sha-512</c> are computed here. They are the two
    /// entries the RFC's registry still marks active; everything else in it
    /// (<c>md5</c>, <c>sha</c>, <c>unixsum</c>, <c>unixcksum</c>, <c>adler</c>,
    /// <c>crc32c</c>) is deprecated or was never collision-resistant to begin with,
    /// and a digest field is an integrity claim — honouring a broken algorithm
    /// would make it a false one.
    /// </summary>
    public static class HTTPDigest
    {

        #region Data

        /// <summary>
        /// The digest algorithms this stack computes, best first. Also the
        /// preference order used to break a tie when a peer wants several equally.
        /// </summary>
        public static readonly String[] Supported = ["sha-256", "sha-512"];

        /// <summary>
        /// A ready-made <c>want-content-digest</c> / <c>want-repr-digest</c> field
        /// value advertising exactly <see cref="Supported"/>, in that order. The
        /// weights are RFC 9530 Section 4 preferences (0 = unacceptable, 10 = most
        /// preferred), not quality values.
        /// </summary>
        public const String Want = "sha-256=10, sha-512=5";

        #endregion


        #region IsSupported (Algorithm)

        /// <summary>Whether an algorithm token names something we can compute.</summary>
        public static Boolean IsSupported(String Algorithm)

            => Supported.Contains(Algorithm.Trim(), StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Compute (Content, Algorithm)

        /// <summary>Digest of Content under one of <see cref="Supported"/>.</summary>
        public static Byte[] Compute(Byte[] Content, String Algorithm)

            => Algorithm.Trim().ToLowerInvariant() switch {
                   "sha-256" => SHA256.HashData(Content),
                   "sha-512" => SHA512.HashData(Content),
                   _         => throw new InvalidOperationException($"Unsupported digest algorithm '{Algorithm}'")
               };

        #endregion

        #region CreateIncremental (Algorithm)

        /// <summary>
        /// An <see cref="IncrementalHash"/> for one of <see cref="Supported"/> — for
        /// digesting a representation that arrives in pieces (a resumed download)
        /// without ever holding it whole in memory.
        /// </summary>
        public static IncrementalHash CreateIncremental(String Algorithm)

            => IncrementalHash.CreateHash(
                   Algorithm.Trim().ToLowerInvariant() switch {
                       "sha-256" => HashAlgorithmName.SHA256,
                       "sha-512" => HashAlgorithmName.SHA512,
                       _         => throw new InvalidOperationException($"Unsupported digest algorithm '{Algorithm}'")
                   });

        #endregion

        #region FieldValue (Content, Algorithm)

        /// <summary>
        /// A complete <c>content-digest</c> / <c>repr-digest</c> field value for
        /// Content — a one-member dictionary whose value is an RFC 9651 Byte
        /// Sequence, e.g. <c>sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:</c>.
        /// </summary>
        public static String FieldValue(Byte[] Content, String Algorithm)

            => $"{Algorithm.Trim().ToLowerInvariant()}=:{Convert.ToBase64String(Compute(Content, Algorithm))}:";

        /// <summary>
        /// The field value for an already-computed digest — the incremental
        /// counterpart of <see cref="FieldValue(Byte[], String)"/>.
        /// </summary>
        public static String FieldValueFor(Byte[] Digest, String Algorithm)

            => $"{Algorithm.Trim().ToLowerInvariant()}=:{Convert.ToBase64String(Digest)}:";

        #endregion

        #region Parse (FieldValue)

        /// <summary>
        /// Parse a digest field value into algorithm → digest.
        ///
        /// The grammar is an RFC 9651 Dictionary of Byte Sequences: comma-separated
        /// <c>key=:base64:</c> members, each optionally carrying parameters we have
        /// no use for and therefore drop. A member we cannot make sense of is
        /// skipped rather than guessed at — a wrong guess here would be a false
        /// integrity verdict, which is worse than no verdict. A repeated key keeps
        /// the last occurrence, as the structured-field grammar prescribes.
        /// </summary>
        public static Dictionary<String, Byte[]> Parse(String FieldValue)
        {

            var result = new Dictionary<String, Byte[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawMember in SplitMembers(FieldValue))
            {

                var member = rawMember.Trim();

                var equals = member.IndexOf('=');
                if (equals <= 0)
                    continue;   // a bare key is a Boolean member, not a digest

                var key   = member[..equals].Trim().ToLowerInvariant();
                var value = member[(equals + 1)..].Trim();

                if (value.Length < 2 || value[0] != ':')
                    continue;

                var closing = value.IndexOf(':', 1);
                if (closing < 0)
                    continue;   // unterminated byte sequence

                // Everything past the closing colon is parameters — ignored.
                var base64 = value[1..closing];
                var buffer = new Byte[(base64.Length / 4 + 1) * 3];

                if (!Convert.TryFromBase64String(base64, buffer, out var written))
                    continue;

                result[key] = buffer[..written];

            }

            return result;

        }

        #endregion

        #region Verify (FieldValue, Content)

        /// <summary>
        /// Check a digest field against the octets it describes.
        ///
        /// Every supported algorithm the field names is checked, not just the first
        /// — a sender that offers two digests is asserting both, and a peer that
        /// disagrees with either one is not to be trusted about the other. A field
        /// that names none of them (or that we could not parse at all) yields
        /// <see cref="HTTPDigestVerification.Unsupported"/>: nothing was checked,
        /// which is emphatically not the same answer as "it matched".
        /// </summary>
        public static HTTPDigestVerification Verify(String? FieldValue, Byte[] Content)
        {

            if (FieldValue is null || FieldValue.Trim().Length == 0)
                return HTTPDigestVerification.NotPresent;

            var digests  = Parse(FieldValue);
            var verified = false;

            foreach (var algorithm in Supported)
            {

                if (!digests.TryGetValue(algorithm, out var expected))
                    continue;

                if (!expected.AsSpan().SequenceEqual(Compute(Content, algorithm)))
                    return HTTPDigestVerification.Mismatch;

                verified = true;

            }

            return verified
                       ? HTTPDigestVerification.Match
                       : HTTPDigestVerification.Unsupported;

        }

        #endregion

        #region SelectAlgorithm (WantFieldValue)

        /// <summary>
        /// Pick the algorithm to answer a <c>want-content-digest</c> /
        /// <c>want-repr-digest</c> field with (RFC 9530, Section 4): among the ones
        /// we support, the highest positive preference wins, ties broken by our own
        /// order.
        ///
        /// An absent (or unparseable) field means the peer expressed no opinion, so
        /// our default algorithm is used. Null is returned only when the peer
        /// actively ruled every algorithm we have out — either by weighting them 0
        /// or by asking exclusively for ones we will not compute — in which case
        /// the right answer is to send no digest at all rather than one that was
        /// declined.
        /// </summary>
        public static String? SelectAlgorithm(String? WantFieldValue)
        {

            if (WantFieldValue is null || WantFieldValue.Trim().Length == 0)
                return Supported[0];

            var preferences = ParseWant(WantFieldValue);

            if (preferences.Count == 0)
                return Supported[0];

            String? best           = null;
            var     bestPreference = 0;

            foreach (var algorithm in Supported)
                if (preferences.TryGetValue(algorithm, out var preference) &&
                    preference > bestPreference)   // strict '>' keeps our own order on a tie
                {
                    bestPreference = preference;
                    best           = algorithm;
                }

            return best;

        }

        #endregion

        #region ParseWant (FieldValue)

        /// <summary>
        /// Parse a <c>want-…-digest</c> field value into algorithm → preference
        /// (RFC 9530, Section 4 — an integer 0…10, where 0 means unacceptable). A
        /// bare key is a Boolean member rather than an integer one; it is read as a
        /// weak "yes" (1) instead of being dropped, since a peer that names an
        /// algorithm at all clearly wants it.
        /// </summary>
        public static Dictionary<String, Int32> ParseWant(String FieldValue)
        {

            var result = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawMember in SplitMembers(FieldValue))
            {

                var member    = rawMember.Trim();
                var semicolon = member.IndexOf(';');

                if (semicolon >= 0)
                    member = member[..semicolon].Trim();   // drop parameters

                var equals     = member.IndexOf('=');
                var preference = 1;
                String key;

                if (equals < 0)
                    key = member.ToLowerInvariant();

                else
                {

                    key = member[..equals].Trim().ToLowerInvariant();

                    if (!Int32.TryParse(member[(equals + 1)..].Trim(), out preference))
                        continue;

                }

                if (key.Length == 0)
                    continue;

                result[key] = Math.Clamp(preference, 0, 10);

            }

            return result;

        }

        #endregion

        #region (private) SplitMembers (Value)

        /// <summary>
        /// Split a structured-field dictionary into its members. Base64 cannot
        /// contain a comma, so this only matters for the parameters a member may
        /// carry — a quoted string there legitimately can. Byte sequences are
        /// skipped over as well, so that a stray comma inside one (there cannot be
        /// a valid one, but there can be a malformed one) does not split a member
        /// in half and turn one unparseable member into two.
        /// </summary>
        private static IEnumerable<String> SplitMembers(String Value)
        {

            var start          = 0;
            var inByteSequence = false;
            var inQuotes       = false;

            for (var i = 0; i < Value.Length; i++)
            {

                var character = Value[i];

                if (inQuotes)
                {

                    if (character == '\\')
                        i++;                     // the next character is escaped

                    else if (character == '"')
                        inQuotes = false;

                    continue;

                }

                switch (character)
                {

                    case '"':
                        inQuotes       = true;
                        break;

                    case ':':
                        inByteSequence = !inByteSequence;
                        break;

                    case ',' when !inByteSequence:
                        yield return Value[start..i];
                        start = i + 1;
                        break;

                }

            }

            yield return Value[start..];

        }

        #endregion

    }

}
