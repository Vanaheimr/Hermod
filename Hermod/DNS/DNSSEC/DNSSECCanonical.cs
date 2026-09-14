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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The octets a DNSSEC signature is taken over: RFC 4034 §3.1.8.1's signed
    /// data, built from §6's canonical form.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared between the validator and the signer on purpose, and the purpose is
    /// worth writing down, because "the signer and the verifier agree with each
    /// other" is normally a reason to distrust a test rather than to trust one.
    /// It holds here only because of where the fixtures come from: the suite
    /// validates signatures made by BIND, so these bytes are measured against an
    /// implementation that has never seen this code, and it hands zones this code
    /// signs to BIND's <c>dnssec-verify</c>. Both directions catch a
    /// canonicalisation error — dropping the canonical ordering of an RRset kills
    /// one validation test and seven signing ones, which is the honest measure of
    /// how thin the first margin was on its own.
    /// </para>
    /// <para>
    /// Two copies would be the worse risk. Findings 46 and 48 were both a second
    /// implementation of something that already existed, quietly disagreeing with
    /// the first.
    /// </para>
    /// </remarks>
    public static class DNSSECCanonical
    {

        #region (static) SignedData(RRSet, Signature)

        /// <summary>
        /// The octets covered by an RRSIG: the signature's own RDATA without the
        /// signature field, followed by the RRset in canonical form and canonical
        /// order (RFC 4034 §3.1.8.1).
        /// </summary>
        /// <param name="RRSet">The resource records the signature covers.</param>
        /// <param name="Signature">The signature, whose <c>Signature</c> field is not read.</param>
        public static Byte[] SignedData(IEnumerable<IDNSResourceRecord>  RRSet,
                                        RRSIG                            Signature)
        {

            using var stream = new MemoryStream();

            stream.WriteUInt16BE((UInt16) Signature.TypeCovered);
            stream.WriteByte    (Signature.Algorithm);
            stream.WriteByte    (Signature.Labels);
            stream.WriteUInt32BE(Signature.OriginalTTL);
            stream.WriteUInt32BE(Signature.SignatureExpiration);
            stream.WriteUInt32BE(Signature.SignatureInception);
            stream.WriteUInt16BE(Signature.KeyTag);

            // RFC 4034 §6.2: the signer's name is lowercased and uncompressed.
            var signerName = DNSTools.SerializeCanonicalName(Signature.SignerName.FullName);
            stream.Write(signerName, 0, signerName.Length);

            var canonical = new List<Byte[]>();

            foreach (var resourceRecord in RRSet)
            {

                using var record = new MemoryStream();

                // RFC 4035 §5.3.2: what was signed is the wildcard, not the name
                // the server expanded it to.
                var owner = DNSTools.SerializeCanonicalName(
                                SignedOwnerName(resourceRecord.DomainName.FullName, Signature.Labels)
                            );

                record.Write        (owner, 0, owner.Length);
                record.WriteUInt16BE((UInt16) Signature.TypeCovered);
                record.WriteUInt16BE((UInt16) resourceRecord.Class);

                // The TTL the signature was made with, not the one the record is
                // carrying now — a cache counts the latter down.
                record.WriteUInt32BE(Signature.OriginalTTL);

                var rdata = ADNSResourceRecord.RDataOf(resourceRecord);

                record.WriteUInt16BE((UInt16) rdata.Length);
                record.Write        (rdata, 0, rdata.Length);

                canonical.Add(record.ToArray());

            }

            // RFC 4034 §6.3: the RRs are sorted by their canonical RDATA, which
            // after the identical prefix above means sorting the octets.
            canonical.Sort(Compare);

            foreach (var record in canonical)
                stream.Write(record, 0, record.Length);

            return stream.ToArray();

        }

        #endregion

        #region (static) SignedOwnerName(OwnerName, Labels)

        /// <summary>
        /// The owner name an RRSIG was made over, which for a wildcard-expanded
        /// RRset is the wildcard rather than the name it was expanded to
        /// (RFC 4035 §5.3.2).
        /// </summary>
        /// <param name="OwnerName">The owner name of the record as it arrived.</param>
        /// <param name="Labels">The RRSIG's labels field (RFC 4034 §3.1.3).</param>
        public static String SignedOwnerName(String  OwnerName,
                                             Byte    Labels)
        {

            var labels = OwnerName.TrimEnd('.').Split('.');

            // The root name splits into a single empty label; there is nothing to
            // reconstruct, and treating it as one real label would be wrong.
            if (labels.Length == 1 && labels[0].Length == 0)
                return OwnerName;

            // Not a wildcard expansion: the owner name is what was signed. Labels
            // greater than the actual count means the RRSIG disagrees with the
            // record it covers; leaving the name alone lets the signature check
            // fail on its own rather than inventing a name here.
            if (Labels >= labels.Length)
                return OwnerName;

            // A wildcard directly at the root would carry Labels = 0.
            if (Labels == 0)
                return "*.";

            return String.Concat("*.", String.Join('.', labels[(labels.Length - Labels)..]), ".");

        }

        #endregion

        #region (static) LabelCount(OwnerName)

        /// <summary>
        /// The labels field of an RRSIG over this owner name (RFC 4034 §3.1.3):
        /// the number of labels, "not counting the null label for the root and
        /// not counting any leading asterisk label".
        /// </summary>
        /// <param name="OwnerName">An owner name.</param>
        public static Byte LabelCount(String OwnerName)
        {

            var trimmed = OwnerName.TrimEnd('.');

            if (trimmed.Length == 0)
                return 0;

            var labels = trimmed.Split('.');
            var count  = labels.Length;

            if (labels[0] == "*")
                count--;

            return (Byte) count;

        }

        #endregion

        #region (static) Compare(A, B)

        /// <summary>
        /// Byte-by-byte comparison, shorter first when one is a prefix of the
        /// other — the ordering RFC 4034 §6.3 puts an RRset into.
        /// </summary>
        public static Int32 Compare(Byte[]  A,
                                    Byte[]  B)
        {

            var length = Math.Min(A.Length, B.Length);

            for (var i = 0; i < length; i++)
            {
                if (A[i] < B[i]) return -1;
                if (A[i] > B[i]) return  1;
            }

            return A.Length.CompareTo(B.Length);

        }

        #endregion

    }

}
