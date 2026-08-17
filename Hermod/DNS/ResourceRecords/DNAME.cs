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
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS DNAME resource records.
    /// </summary>
    public static class DNS_DNAME_Extensions
    {

        #region CacheDNAME(this DNSClient, DomainName, Target, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS DNAME record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Target">The target domain of this DNAME resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheDNAME(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      DomainName       Target,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new DNAME(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Target
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The outcome of applying a DNAME to a query name (RFC 6672 §2.2).
    /// </summary>
    public enum DNAMESubstitution
    {

        /// <summary>
        /// The name was rewritten.
        /// </summary>
        Redirected,

        /// <summary>
        /// The name is not subordinate to the DNAME's owner, so the DNAME does
        /// not apply. RFC 6672 §2.3: "the owner name of a DNAME is not
        /// redirected itself" — nor is anything beside or above it.
        /// </summary>
        NotSubordinate,

        /// <summary>
        /// The rewritten name would be longer than the 255 octets a domain name
        /// has room for. RFC 6672 §2.2 answers this with YXDOMAIN rather than by
        /// truncating, and sends the DNAME along as the proof.
        /// </summary>
        ExceedsNameLimit

    }


    /// <summary>
    /// The DNS Delegation Name (DNAME) resource record (RFC 6672).
    /// A DNAME record provides redirection for an entire subtree of the
    /// domain name tree. It is similar to CNAME but applies to all names
    /// beneath the owner, not just the owner itself.
    /// </summary>
    public class DNAME : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Delegation Name (DNAME) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.DNAME;

        #endregion

        #region Properties

        /// <summary>
        /// The target domain name for this DNAME delegation.
        /// All names under the DNAME owner are rewritten to be under this target.
        /// </summary>
        public DomainName  Target    { get; }

        #endregion

        #region Constructor

        #region DNAME(DomainName, Stream)

        /// <summary>
        /// Create a new DNAME resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this DNAME resource record.</param>
        /// <param name="Stream">A stream containing the DNAME resource record data.</param>
        public DNAME(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Target = DomainName.Parse(
                              DNSTools.ExtractName(Stream)
                          );

        }

        #endregion

        #region DNAME(DomainName, Class, TimeToLive, Target)

        /// <summary>
        /// Create a new DNS DNAME resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this DNAME resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Target">The target domain of this DNAME resource record.</param>
        public DNAME(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     DomainName       Target)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Target = Target;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from a DNS JSON API "data" field
        /// (e.g. Google dns.google/resolve or Cloudflare cloudflare-dns.com/dns-query).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        /// <returns>The parsed resource record, or null if parsing fails.</returns>
        public static DNAME? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var target = Data.EndsWith('.') ? Data : Data + ".";
                return new DNAME(Name, DNSQueryClasses.IN, TimeToLive, DNS.DomainName.Parse(target));
            }
            catch { return null; }
        }

        #endregion

        #region (static) TrySubstitute(QName, Owner, Target, out Rewritten)

        /// <summary>
        /// Apply the RFC 6672 §2.2 substitution: replace the suffix of
        /// <paramref name="QName"/> that matches <paramref name="Owner"/> with
        /// <paramref name="Target"/>.
        /// </summary>
        /// <param name="QName">The name being sought.</param>
        /// <param name="Owner">The owner name of the DNAME.</param>
        /// <param name="Target">The DNAME's target.</param>
        /// <param name="Rewritten">The rewritten name, when the DNAME applies and fits.</param>
        /// <remarks>
        /// <para>
        /// The two ways this does not produce a name are as interesting as the
        /// one way it does, which is why they are told apart rather than folded
        /// into a false.
        /// </para>
        /// <para>
        /// A name that is <i>not subordinate</i> to the owner is left alone —
        /// including the owner itself. RFC 6672 §2.3: "the owner name of a DNAME
        /// is not redirected itself". That is the whole difference from a CNAME,
        /// and it is why a zone can hold both a DNAME and, say, an MX at the same
        /// name without contradiction.
        /// </para>
        /// <para>
        /// A name that <i>would not fit</i> is an error and not a truncation.
        /// The wire format gives a domain name 255 octets and the substitution
        /// can ask for more, since the prefix is kept and only the suffix is
        /// swapped — a longer target lengthens every name beneath it at once.
        /// RFC 6672 §2.2 answers that with YXDOMAIN.
        /// </para>
        /// </remarks>
        public static DNAMESubstitution TrySubstitute(DNSServiceName                        QName,
                                                      DNSServiceName                        Owner,
                                                      DomainName                            Target,
                                                      [NotNullWhen(true)] out DNSServiceName?  Rewritten)
        {

            Rewritten = null;

            var qnameLabels  = Labels(QName.FullName);
            var ownerLabels  = Labels(Owner.FullName);
            var targetLabels = Labels(Target.FullName);

            // Strictly subordinate: the owner has to be a proper suffix, so a
            // QNAME equal to the owner has no prefix to carry over and is not
            // redirected.
            if (qnameLabels.Length <= ownerLabels.Length)
                return DNAMESubstitution.NotSubordinate;

            var prefixLength = qnameLabels.Length - ownerLabels.Length;

            for (var i = 0; i < ownerLabels.Length; i++)
            {
                if (!String.Equals(qnameLabels[prefixLength + i], ownerLabels[i], StringComparison.OrdinalIgnoreCase))
                    return DNAMESubstitution.NotSubordinate;
            }

            var result = new String[prefixLength + targetLabels.Length];
            Array.Copy(qnameLabels,  0, result, 0,            prefixLength);
            Array.Copy(targetLabels, 0, result, prefixLength, targetLabels.Length);

            // RFC 1035 §2.3.4: 255 octets for the whole name, counting one length
            // octet per label plus the root's terminating zero. Measuring the
            // presentation string instead would be off by one per label and would
            // miss escaped characters entirely.
            var wireLength = 1;

            foreach (var label in result)
                wireLength += 1 + Encoding.ASCII.GetByteCount(label);

            if (wireLength > 255)
                return DNAMESubstitution.ExceedsNameLimit;

            Rewritten = DNSServiceName.Parse(String.Join('.', result) + ".");

            return DNAMESubstitution.Redirected;

        }


        /// <summary>
        /// The labels of a name, without the empty one a trailing dot leaves behind.
        /// </summary>
        private static String[] Labels(String Name)

            => Name.TrimEnd('.').Length == 0
                   ? []
                   : Name.TrimEnd('.').Split('.');

        #endregion

        #region (static) SynthesizeCNAME(QName, Rewritten, TimeToLive)

        /// <summary>
        /// The CNAME a server puts beside the DNAME in the answer section
        /// (RFC 6672 §3.1).
        /// </summary>
        /// <param name="QName">The name that was asked for — the CNAME's owner.</param>
        /// <param name="Rewritten">The result of the substitution — the CNAME's target.</param>
        /// <param name="TimeToLive">The DNAME's TTL.</param>
        /// <remarks>
        /// <para>
        /// It exists for resolvers that do not know what a DNAME is. They see an
        /// ordinary alias and follow it; a resolver that does know ignores the
        /// synthesized record and applies the DNAME itself.
        /// </para>
        /// <para>
        /// The TTL is the DNAME's, and this is where RFC 6672 changed RFC 2672:
        /// the older specification synthesized the CNAME with a TTL of zero, so
        /// that nothing would cache a redirection its source could outlive. §3.1
        /// now equates the two and requires resolvers to accept either, which is
        /// why a test that pins this value has to say which document it is
        /// reading.
        /// </para>
        /// <para>
        /// It is not signed, and cannot be: the server made it up while answering.
        /// RFC 6672 §3.1 says as much — a validator authenticates the DNAME and
        /// re-derives the CNAME for itself.
        /// </para>
        /// </remarks>
        public static CNAME SynthesizeCNAME(DNSServiceName  QName,
                                            DNSServiceName  Rewritten,
                                            TimeSpan        TimeToLive)

            // Qualified, because inside this class "DomainName" is the inherited
            // owner-name property rather than the type.
            => new (DNS.DomainName.ParseLenient(QName.FullName),
                    DNSQueryClasses.IN,
                    TimeToLive,
                    DNS.DomainName.ParseLenient(Rewritten.FullName));

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => Target.ToString();

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression (true by default).</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            var tempStream = new MemoryStream();

            // RDATA: Target Name
            Target.Serialize(
                tempStream,
                (Int32) Stream.Position + 2,
                false,   // RFC 3597 §4: DNAME postdates RFC 1035, so its target is not compressible.
                CompressionOffsets
            );


            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH (2 bytes): Variable, when compression is used!
            Stream.WriteUInt16BE(tempStream.Length);

            // Copy RDATA (tempStream) to main stream
            tempStream.Position = 0;
            tempStream.CopyTo(Stream);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{Target}, {base.ToString()}";

        #endregion

    }

}
