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

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// An authentication result of a record of a received DMARC report: the domain
    /// that was checked, and what came of it.
    /// </summary>
    public sealed record DmarcReceivedAuth(String   Domain,
                                           String   Result,
                                           String?  Selector);

    /// <summary>
    /// One record of a received DMARC aggregate report: how many messages one IP
    /// address sent in the name of the domain, and how they fared.
    /// </summary>
    /// <param name="SourceIp">The address they came from.</param>
    /// <param name="Count">How many there were.</param>
    /// <param name="Disposition">What the receiver did: none, quarantine or reject.</param>
    /// <param name="Dkim">DKIM as DMARC evaluated it - aligned: pass or fail.</param>
    /// <param name="Spf">SPF as DMARC evaluated it - aligned: pass or fail.</param>
    /// <param name="HeaderFrom">The domain of their From:.</param>
    /// <param name="DkimResults">Every DKIM signature, aligned or not.</param>
    /// <param name="SpfResults">The SPF results.</param>
    public sealed record DmarcReceivedRecord(String                            SourceIp,
                                             Int64                             Count,
                                             String                            Disposition,
                                             String                            Dkim,
                                             String                            Spf,
                                             String?                           HeaderFrom,
                                             IReadOnlyList<DmarcReceivedAuth>  DkimResults,
                                             IReadOnlyList<DmarcReceivedAuth>  SpfResults)
    {

        /// <summary>
        /// Whether these messages passed DMARC: aligned DKIM or aligned SPF passed.
        /// </summary>
        public Boolean Passed
            => Dkim == "pass" || Spf == "pass";

    }

    /// <summary>
    /// A DMARC aggregate report another receiver sent about a domain of this
    /// server (RFC 7489 §7.2, Appendix C).
    /// </summary>
    public sealed record DmarcReceivedReport(String?                             OrganizationName,
                                             String?                             Email,
                                             String?                             ReportId,
                                             DateTimeOffset?                     Begin,
                                             DateTimeOffset?                     End,
                                             String                              Domain,
                                             String?                             Policy,
                                             String?                             SubdomainPolicy,
                                             Int32?                              Percent,
                                             String?                             Adkim,
                                             String?                             Aspf,
                                             IReadOnlyList<DmarcReceivedRecord>  Records)
    {

        /// <summary>
        /// How many messages the report is about.
        /// </summary>
        public Int64 Total
            => Records.Sum(record => record.Count);

        /// <summary>
        /// How many of them passed DMARC.
        /// </summary>
        public Int64 Passed
            => Records.Where(record => record.Passed).Sum(record => record.Count);

    }


    /// <summary>
    /// Reads a DMARC aggregate report as receivers send it: the XML of RFC 7489
    /// Appendix C - or of DMARCbis, in its namespace - as a file of its own,
    /// gzipped, or the only XML file in a ZIP.
    /// </summary>
    /// <remarks>
    /// What is read is limited: 20 MiB of XML at most, unpacked, and no DTD - a
    /// report is something a stranger sends.
    /// </remarks>
    public static class DmarcReportReader
    {

        #region Data

        /// <summary>
        /// How large the XML of a report may be, unpacked.
        /// </summary>
        public const Int32 MaxXmlOctets = 20 * 1024 * 1024;

        #endregion


        #region Unpack(Content)

        /// <summary>
        /// The XML of an attachment: as it is, gunzipped, or the first .xml of a
        /// ZIP - or null when it is none of these, or larger than allowed.
        /// </summary>
        public static String? Unpack(ReadOnlySpan<Byte> Content)
        {

            try
            {

                Byte[] xml;

                if (Content.Length >= 2 && Content[0] == 0x1F && Content[1] == 0x8B)
                {
                    using var gzip = new GZipStream(new MemoryStream(Content.ToArray()), CompressionMode.Decompress);
                    xml = ReadAtMost(gzip);
                }

                else if (Content.Length >= 4 && Content[0] == (Byte) 'P' && Content[1] == (Byte) 'K' && Content[2] == 3 && Content[3] == 4)
                {

                    using var zip   = new ZipArchive(new MemoryStream(Content.ToArray()), ZipArchiveMode.Read);
                    var       entry = zip.Entries.FirstOrDefault(entry => entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                    if (entry is null)
                        return null;

                    using var stream = entry.Open();
                    xml = ReadAtMost(stream);

                }

                else
                {
                    if (Content.Length > MaxXmlOctets)
                        return null;
                    xml = Content.ToArray();
                }

                var text = Encoding.UTF8.GetString(xml);

                return text.Contains("<feedback", StringComparison.Ordinal) ? text : null;

            }
            catch (InvalidDataException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }

        }

        private static Byte[] ReadAtMost(Stream Stream)
        {

            using var output = new MemoryStream();
            var       buffer = new Byte[81920];
            Int32     read;

            while ((read = Stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                if (output.Length > MaxXmlOctets)
                    throw new InvalidDataException("The report is larger than allowed.");
            }

            return output.ToArray();

        }

        #endregion

        #region Parse(Xml)

        /// <summary>
        /// The report the XML holds, or null when it holds none.
        /// </summary>
        public static DmarcReceivedReport? Parse(String Xml)
        {

            try
            {

                using var reader = XmlReader.Create(new StringReader(Xml), new XmlReaderSettings {
                                                                               DtdProcessing  = DtdProcessing.Prohibit,
                                                                               XmlResolver    = null
                                                                           });

                var feedback = XDocument.Load(reader).Root;

                if (feedback is null || feedback.Name.LocalName != "feedback")
                    return null;

                var metadata  = Child(feedback, "report_metadata");
                var range     = Child(metadata, "date_range");
                var policy    = Child(feedback, "policy_published");
                var domain    = Text(policy, "domain");

                if (domain is null)
                    return null;

                var records   = Children(feedback, "record").Select(record => {

                                    var row         = Child(record, "row");
                                    var evaluated   = Child(row, "policy_evaluated");
                                    var auth        = Child(record, "auth_results");

                                    return new DmarcReceivedRecord(
                                               SourceIp:     Text(row, "source_ip") ?? "",
                                               Count:        Int64.TryParse(Text(row, "count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0,
                                               Disposition:  (Text(evaluated, "disposition") ?? "none").ToLowerInvariant(),
                                               Dkim:         (Text(evaluated, "dkim") ?? "fail").ToLowerInvariant(),
                                               Spf:          (Text(evaluated, "spf")  ?? "fail").ToLowerInvariant(),
                                               HeaderFrom:   Text(Child(record, "identifiers"), "header_from"),
                                               DkimResults:  [.. Children(auth, "dkim").Select(dkim => new DmarcReceivedAuth(Text(dkim, "domain") ?? "", (Text(dkim, "result") ?? "none").ToLowerInvariant(), Text(dkim, "selector")))],
                                               SpfResults:   [.. Children(auth, "spf"). Select(spf  => new DmarcReceivedAuth(Text(spf,  "domain") ?? "", (Text(spf,  "result") ?? "none").ToLowerInvariant(), null))]
                                           );

                                }).ToArray();

                return new DmarcReceivedReport(
                           OrganizationName:  Text(metadata, "org_name"),
                           Email:             Text(metadata, "email"),
                           ReportId:          Text(metadata, "report_id"),
                           Begin:             Seconds(Text(range, "begin")),
                           End:               Seconds(Text(range, "end")),
                           Domain:            domain.ToLowerInvariant(),
                           Policy:            Text(policy, "p"),
                           SubdomainPolicy:   Text(policy, "sp"),
                           Percent:           Int32.TryParse(Text(policy, "pct"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent) ? percent : null,
                           Adkim:             Text(policy, "adkim"),
                           Aspf:              Text(policy, "aspf"),
                           Records:           records
                       );

            }
            catch (XmlException)
            {
                return null;
            }

        }

        #endregion


        #region (private) Child / Children / Text / Seconds

        // By local name: RFC 7489's files have no namespace, DMARCbis's have one.
        private static XElement? Child(XElement? Parent, String Name)
            => Parent?.Elements().FirstOrDefault(element => element.Name.LocalName == Name);

        private static IEnumerable<XElement> Children(XElement? Parent, String Name)
            => Parent?.Elements().Where(element => element.Name.LocalName == Name) ?? [];

        private static String? Text(XElement? Parent, String Name)
            => Child(Parent, Name)?.Value.Trim() is { Length: > 0 } text ? text : null;

        private static DateTimeOffset? Seconds(String? Text)
            => Int64.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                   ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                   : null;

        #endregion

    }

}
