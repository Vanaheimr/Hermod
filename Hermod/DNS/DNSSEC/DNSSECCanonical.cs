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

            var canonical = new List<(Byte[] RData, Byte[] Record)>();

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

                var rdata = CanonicalRData(resourceRecord);

                record.WriteUInt16BE((UInt16) rdata.Length);
                record.Write        (rdata, 0, rdata.Length);

                canonical.Add((rdata, record.ToArray()));

            }

            // RFC 4034 §6.3: the RRs are sorted "by treating the RDATA portion of
            // the canonical form of each RR as a left-justified unsigned octet
            // sequence" — the RDATA alone. The prefix above is the same for every
            // record of the set except for one field, RDLENGTH, which stands in
            // front of the RDATA: sorting whole records would put a shorter record
            // first whatever its octets say, so x.y.example. (01 'x') would follow
            // aa.example. (02 'a' 'a') rather than precede it.
            canonical.Sort((left, right) => Compare(left.RData, right.RData));

            foreach (var (_, record) in canonical)
                stream.Write(record, 0, record.Length);

            return stream.ToArray();

        }

        #endregion

        #region (static) CanonicalRData(ResourceRecord)

        /// <summary>
        /// The RDATA of a resource record in its DNSSEC canonical form (RFC 4034
        /// §6.2): uncompressed, and with the names it holds lowercased if — and
        /// only if — its type is one whose names the canonical form lowercases.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Item 3 of §6.2 names the types: "NS, MD, MF, CNAME, SOA, MB, MG, MR, PTR,
        /// HINFO, MINFO, MX, HINFO, RP, AFSDB, RT, SIG, PX, NXT, NAPTR, KX, SRV,
        /// DNAME, A6, RRSIG, or NSEC". RFC 6840 §5.1 takes two off it — NSEC, whose
        /// next name is not lowercased, and HINFO, since "HINFO records contain no
        /// domain names" — and RFC 3597 §7 closes it: for every type published after RFC
        /// 3597 "the canonical form is such that no downcasing of embedded domain
        /// names takes place". So an MX's exchanger and a KX's are lowercased, and
        /// an LP's FQDN or a HIP's rendezvous servers keep the case they were
        /// written in.
        /// </para>
        /// <para>
        /// The list is closed, so it is written out here type code by type code,
        /// with each type's layout — and it works on the octets rather than on a
        /// record class, because RFC 3597 §7 applies to the old types whether or
        /// not this build has a parser for them: an RT or a PX that arrives as an
        /// unknown record is still lowercased the way its signer lowercased it.
        /// </para>
        /// <para>
        /// Only the 26 letters A to Z are changed, as §6.2 says: "all uppercase
        /// US-ASCII letters". Any other octet of a label is left as it is.
        /// </para>
        /// </remarks>
        /// <param name="ResourceRecord">A resource record.</param>
        public static Byte[] CanonicalRData(IDNSResourceRecord ResourceRecord)

            => CanonicalRData((UInt16) ResourceRecord.Type,
                              ADNSResourceRecord.RDataOf(ResourceRecord));


        /// <summary>
        /// The canonical form of the given RDATA of the given type (RFC 4034 §6.2
        /// item 3, RFC 6840 §5.1, RFC 3597 §7).
        /// </summary>
        /// <param name="Type">The type code of the record the RDATA belongs to.</param>
        /// <param name="RData">The uncompressed RDATA.</param>
        public static Byte[] CanonicalRData(UInt16  Type,
                                            Byte[]  RData)
        {

            var canonical = (Byte[]) RData.Clone();

            switch (Type)
            {

                // One name and nothing else: NS, MD, MF, CNAME, MB, MG, MR, PTR
                // (RFC 1035 §3.3), and DNAME (RFC 6672 §2.1).
                case 2: case 3: case 4: case 5: case 7: case 8: case 9: case 12: case 39:
                    LowercaseNames(canonical, 0, 1);
                    break;

                // Two names: SOA's MNAME and RNAME, with its five numbers behind
                // them (RFC 1035 §3.3.13); MINFO's RMAILBX and EMAILBX (§3.3.7);
                // RP's mailbox and TXT name (RFC 1183 §2.2).
                case 6: case 14: case 17:
                    LowercaseNames(canonical, 0, 2);
                    break;

                // A 16-bit field and a name: MX (RFC 1035 §3.3.9), AFSDB and RT
                // (RFC 1183 §1, §3.3), KX (RFC 2230 §3.1).
                case 15: case 18: case 21: case 36:
                    LowercaseNames(canonical, 2, 1);
                    break;

                // A 16-bit preference and two names: PX (RFC 2163 §4).
                case 26:
                    LowercaseNames(canonical, 2, 2);
                    break;

                // Priority, weight and port, then the target: SRV (RFC 2782).
                case 33:
                    LowercaseNames(canonical, 6, 1);
                    break;

                // Order and preference, three character-strings, then the
                // replacement: NAPTR (RFC 3403 §4.1).
                case 35:
                    {

                        var offset = 4;

                        for (var i = 0; i < 3 && offset < canonical.Length; i++)
                            offset += 1 + canonical[offset];

                        LowercaseNames(canonical, offset, 1);

                    }
                    break;

                // Eighteen octets of fixed fields, then the signer's name, then the
                // signature: SIG (RFC 2535 §4.1) and RRSIG (RFC 4034 §3.1), the
                // latter kept on the list by RFC 6840 §5.1.
                case 24: case 46:
                    LowercaseNames(canonical, 18, 1);
                    break;

                // The next domain name, then the type bit map: NXT (RFC 2535 §5.2).
                // NSEC has the same layout and is off the list (RFC 6840 §5.1).
                case 30:
                    LowercaseNames(canonical, 0, 1);
                    break;

                // A6 (RFC 2874 §3.1): a prefix length, the address suffix that
                // length leaves, and — unless the length is zero — the prefix name.
                case 38:
                    if (canonical.Length > 0 && canonical[0] is > 0 and <= 128)
                        LowercaseNames(canonical, 1 + (128 - canonical[0] + 7) / 8, 1);
                    break;

            }

            return canonical;

        }

        #endregion

        #region (private static) LowercaseNames(RData, Offset, Count)

        /// <summary>
        /// Lowercase the letters A to Z in the labels of the given number of
        /// consecutive uncompressed names, starting at the given offset.
        /// </summary>
        /// <remarks>
        /// RDATA serialized for DNSSEC is uncompressed by item 1 of §6.2, so a
        /// pointer here would be a malformed record; the walk stops at one rather
        /// than reading past it, and stops at the end of the RDATA in any case.
        /// </remarks>
        private static void LowercaseNames(Byte[]  RData,
                                           Int32   Offset,
                                           Int32   Count)
        {

            var index = Offset;

            for (var name = 0; name < Count; name++)
            {

                while (index < RData.Length)
                {

                    var length = RData[index];

                    if (length == 0)
                    {
                        index++;
                        break;
                    }

                    if ((length & 0xC0) != 0)
                        return;

                    var end = Math.Min(index + 1 + length, RData.Length);

                    for (var i = index + 1; i < end; i++)
                        if (RData[i] is >= (Byte) 'A' and <= (Byte) 'Z')
                            RData[i] = (Byte) (RData[i] + ('a' - 'A'));

                    index += 1 + length;

                }

            }

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
