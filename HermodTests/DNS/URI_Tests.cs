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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// A URI record's target is any URI of RFC 3986 (RFC 7553 §4.4), carried as
    /// the octets that were written (RFC 7553 §4.5, RFC 3597 §3).
    /// </summary>
    [TestFixture]
    public class URI_Tests
    {

        private static Byte[] RData(IDNSResourceRecord Record)
            => ADNSResourceRecord.RDataOf(Record);

        #region Target_OfAnyScheme_IsCarriedAsWritten(Target)

        /// <summary>
        /// RFC 3986 §1.1.2's examples without "//", read from the presentation
        /// form and off the wire. A URL reader made mailto: into https://mailto:
        /// and refused the other three.
        /// </summary>
        [TestCase("mailto:John.Doe@example.com")]
        [TestCase("news:comp.infosystems.www.servers.unix")]
        [TestCase("tel:+1-816-555-1212")]
        [TestCase("urn:oasis:names:specification:docbook:dtd:xml:4.1.2")]
        [TestCase("ftp://ftp1.example.com/public")]
        public void Target_OfAnyScheme_IsCarriedAsWritten(String Target)
        {

            var octets   = Encoding.ASCII.GetBytes(Target);
            var wire     = (Byte[]) [ 0x00, 0x0a, 0x00, 0x01, .. octets ];

            var written  = ADNSResourceRecord.ParseZoneFileString($"_x._tcp.example. 3600 IN URI 10 1 \"{Target}\"") as URI;
            var generic  = ADNSResourceRecord.ParseZoneFileString($@"_x._tcp.example. 3600 IN URI \# {wire.Length} {Convert.ToHexString(wire)}") as URI;

            Assert.That(written!.Target.ToString(), Is.EqualTo(Target));
            Assert.That(RData(written),             Is.EqualTo(wire));
            Assert.That(generic!.Target.ToString(), Is.EqualTo(Target));
            Assert.That(RData(generic),             Is.EqualTo(wire));

        }

        #endregion

        #region Target_IsNeverGivenASchemeItDidNotHave()

        /// <summary>
        /// Neither of these is a URI, and RFC 7553 asks only that a target is not
        /// empty. They used to come out as https://example.com/x and https://a.
        /// </summary>
        [TestCase("example.com/x")]
        [TestCase("a")]
        public void Target_IsNeverGivenASchemeItDidNotHave(String Target)
        {

            var wire    = (Byte[]) [ 0x00, 0x0a, 0x00, 0x01, .. Encoding.ASCII.GetBytes(Target) ];
            var record  = ADNSResourceRecord.ParseZoneFileString($@"_x._tcp.example. 3600 IN URI \# {wire.Length} {Convert.ToHexString(wire)}") as URI;

            Assert.That(record!.Target.ToString(), Is.EqualTo(Target));
            Assert.That(RData(record),             Is.EqualTo(wire));

        }

        #endregion

        #region Target_ThatIsEmpty_IsRefused()

        /// <summary>
        /// RFC 7553 §4.4: "The Target MUST NOT be an empty URI ("")", and §4.5 that
        /// its length "MUST be greater than zero".
        /// </summary>
        [Test]
        public void Target_ThatIsEmpty_IsRefused()
        {

            Assert.That(ADNSResourceRecord.TryParseZoneFileString("_x._tcp.example. 3600 IN URI 10 1 \"\"",          out _, out _), Is.False);
            Assert.That(ADNSResourceRecord.TryParseZoneFileString(@"_x._tcp.example. 3600 IN URI \# 4 000a0001",    out _, out _), Is.False);

        }

        #endregion

    }

}
