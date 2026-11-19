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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The ordered set of ARC header sets found in a message (RFC 8617). Provides parsing from
    /// the raw header fields and construction of the ARC-Seal signing input, shared by the
    /// verifier and the sealer so both hash byte-identically.
    /// </summary>
    public sealed class ArcChain
    {

        /// <summary>
        /// Sets ordered by instance 1..N (only present when <see cref="WellFormed"/>).
        /// </summary>
        public IReadOnlyList<ArcSet> Sets        { get; }

        /// <summary>
        /// True when instances 1..N are contiguous and each has all three header fields.
        /// </summary>
        public Boolean               WellFormed  { get; }

        /// <summary>
        /// The highest instance number found in the chain (RFC 8617 §5.1 step 3: the next
        /// set's instance "is 1 more than the highest instance number found").
        /// </summary>
        public Int32                 MaxInstance { get; }

        /// <summary>
        /// The maximum number of ARC sets (RFC 8617 §5.2 step 1: "The maximum number of ARC
        /// Sets that can be attached to a message is 50"; §4.2.1: "Instance tag values can
        /// range from 1-50 (inclusive)").
        /// </summary>
        public const Int32 MaxSets = 50;

        private ArcChain(IReadOnlyList<ArcSet> sets, Boolean wellFormed, Int32 maxInstance)
        {
            Sets        = sets;
            WellFormed  = wellFormed;
            MaxInstance = maxInstance;
        }

        #region Parse(fields)

        /// <summary>
        /// Group the ARC header fields by instance. Returns null when there are no ARC headers
        /// at all (chain status "none"); otherwise a chain whose <see cref="WellFormed"/> flag
        /// says whether the instances form a valid 1..N sequence.
        /// </summary>
        public static ArcChain? Parse(List<DkimHeaderField> fields)
        {

            var seals = ByInstance(fields, "ARC-Seal");
            var sigs  = ByInstance(fields, "ARC-Message-Signature");
            var aars  = ByInstance(fields, "ARC-Authentication-Results");

            if (seals.Count == 0 && sigs.Count == 0 && aars.Count == 0)
                return null;

            var n = new[] { MaxKey(seals), MaxKey(sigs), MaxKey(aars) }.Max();

            // More than 50 sets: the chain fails at once (RFC 8617 §5.2 step 1) - and no loop
            // below ever counts up to an instance number the sender chose.
            if (n > MaxSets)
                return new ArcChain([], false, n);

            var sets       = new List<ArcSet>();

            // An ARC header field without a valid instance - a tag-list that is invalid, as
            // with a tag twice (RFC 6376 §3.2) - belongs to no set: the chain is malformed.
            var wellFormed = n >= 1 && !seals.ContainsKey(0) && !sigs.ContainsKey(0) && !aars.ContainsKey(0);

            for (var i = 1; i <= n; i++)
            {
                // Each instance must have exactly one of each header (RFC 8617 §5.1.1).
                if (seals.TryGetValue(i, out var seal) && seal.Count == 1 &&
                    sigs. TryGetValue(i, out var sig)  && sig. Count == 1 &&
                    aars. TryGetValue(i, out var aar)  && aar. Count == 1)
                {
                    sets.Add(new ArcSet(i, seal[0], sig[0], aar[0]));
                }
                else
                {
                    wellFormed = false;
                }
            }

            return new ArcChain(sets, wellFormed && sets.Count == n, n);

        }

        #endregion

        #region BuildSealSigningInput(uptoInstance)

        /// <summary>
        /// The input to an ARC-Seal signature at <paramref name="uptoInstance"/> (RFC 8617
        /// §5.1.2): for each instance 1..upto, the relaxed-canonicalized AAR, AMS and AS in
        /// that order, each CRLF-terminated, except the final AS which has its b= value emptied
        /// and no trailing CRLF.
        /// </summary>
        public String BuildSealSigningInput(Int32 uptoInstance)
            => BuildSealSigningInput(Sets, uptoInstance);

        /// <summary>
        /// As above, but over an explicit ordered set list — used by the sealer, which builds
        /// the input for a set that is not yet part of any parsed message.
        /// </summary>
        public static String BuildSealSigningInput(IReadOnlyList<ArcSet> sets, Int32 uptoInstance)
        {

            var sb = new StringBuilder();

            for (var i = 1; i <= uptoInstance; i++)
            {
                var set = sets[i - 1];
                sb.Append(DkimCanonicalization.CanonicalizeHeader(set.AuthResults,      "relaxed")).Append("\r\n");
                sb.Append(DkimCanonicalization.CanonicalizeHeader(set.MessageSignature, "relaxed")).Append("\r\n");

                if (i < uptoInstance)
                    sb.Append(DkimCanonicalization.CanonicalizeHeader(set.Seal, "relaxed")).Append("\r\n");
                else
                    // The seal being signed/verified: b= emptied, NO trailing CRLF.
                    sb.Append(DkimCanonicalization.CanonicalizeHeader(DkimCanonicalization.RemoveSignatureValue(set.Seal), "relaxed"));
            }

            return sb.ToString();

        }

        #endregion

        #region Tag parsing

        /// <summary>
        /// Parse a header value's "k=v; k=v" tag list (shared shape with DKIM/DMARC).
        /// </summary>
        /// <exception cref="FormatException">A tag occurs twice: "Tags with duplicate names
        /// MUST NOT occur within a single tag-list; if a tag name does occur more than once,
        /// the entire tag-list is invalid" (RFC 6376 §3.2, whose tag-lists ARC uses).</exception>
        public static Dictionary<String, String> ParseTags(String value)
        {
            var tags = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            var normalized = value.Replace("\r\n", "").Replace("\n", "").Replace("\t", " ");
            foreach (var part in normalized.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                if (eq > 0 && !tags.TryAdd(part[..eq].Trim(), part[(eq + 1)..].Trim()))
                    throw new FormatException($"The tag {part[..eq].Trim()} occurs twice: the tag-list is invalid");
            }
            return tags;
        }

        /// <summary>
        /// The i= instance number of an ARC header value, or 0 if absent/invalid - also when
        /// the tag-list itself is invalid.
        /// </summary>
        public static Int32 InstanceOf(String value)
        {
            try
            {
                return ParseTags(value).TryGetValue("i", out var s) && Int32.TryParse(s, out var i) ? i : 0;
            }
            catch (FormatException)
            {
                return 0;
            }
        }

        #endregion

        #region (private) helpers

        private static Dictionary<Int32, List<DkimHeaderField>> ByInstance(List<DkimHeaderField> fields, String name)
        {
            var map = new Dictionary<Int32, List<DkimHeaderField>>();
            foreach (var f in fields.Where(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                // Without a valid instance - none, or a tag-list that is invalid - under 0.
                var i = Math.Max(InstanceOf(f.RawValue), 0);
                if (!map.TryGetValue(i, out var list))
                    map[i] = list = [];
                list.Add(f);
            }
            return map;
        }

        private static Int32 MaxKey(Dictionary<Int32, List<DkimHeaderField>> map)
            => map.Count == 0 ? 0 : map.Keys.Max();

        #endregion

    }

}
