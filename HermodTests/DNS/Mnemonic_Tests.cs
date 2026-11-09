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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// A class or type in a zone-file line is a mnemonic, CLASSnn or TYPEnnn,
    /// and nothing else (RFC 1035 §5.1, RFC 3597 §5). Enum.TryParse took a
    /// decimal and a comma-separated list as well.
    /// </summary>
    [TestFixture]
    public class Mnemonic_Tests
    {

        private const String SignatureTail = "8 2 3600 20301231235959 20200101000000 12345 example. AQID";

        #region ZoneFile_DecimalANamedClassHas_IsTheTTL(Seconds)

        /// <summary>
        /// The header tries the class before the TTL, and 1, 2, 3, 4, 254 and 255
        /// are the numbers of IN, CS, CH, HS, NONE and ANY. Each of them is a TTL.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(254)]
        [TestCase(255)]
        public void ZoneFile_DecimalANamedClassHas_IsTheTTL(Int32 Seconds)
        {

            foreach (var line in new[] { $"x.example. {Seconds} IN A 192.0.2.1",
                                         $"x.example. IN {Seconds} A 192.0.2.1",
                                         $"x.example. {Seconds} A 192.0.2.1" })
            {

                var record = ADNSResourceRecord.ParseZoneFileString(line, DefaultTimeToLive: TimeSpan.FromSeconds(99));

                Assert.That(record.TimeToLive, Is.EqualTo(TimeSpan.FromSeconds(Seconds)), line);
                Assert.That(record.Class,      Is.EqualTo(DNSQueryClasses.IN),            line);

            }

        }

        #endregion

        #region ZoneFile_ListOfMnemonics_IsNoMnemonic(Line)

        /// <summary>
        /// "A,MX" combined to MX and "IN,CH" to CH, in a header and as a Type Covered.
        /// </summary>
        [TestCase("x.example. 3600 IN A,MX 10 mail.example.")]
        [TestCase("x.example. 3600 IN,CH A 192.0.2.1")]
        [TestCase("x.example. 3600 IN RRSIG A,MX " + SignatureTail)]
        [TestCase("x.example. 3600 IN SIG A,MX "   + SignatureTail)]
        public void ZoneFile_ListOfMnemonics_IsNoMnemonic(String Line)
        {
            Assert.That(ADNSResourceRecord.TryParseZoneFileString(Line, out _, out _), Is.False);
        }

        #endregion

        #region ZoneFile_ListOfMnemonics_InATypeBitMap_NamesNoType()

        [Test]
        public void ZoneFile_ListOfMnemonics_InATypeBitMap_NamesNoType()
        {

            var nsec = ADNSResourceRecord.ParseZoneFileString("x.example. 3600 IN NSEC next.example. A,MX") as NSEC;

            Assert.That(nsec, Is.Not.Null);
            Assert.That(ADNSResourceRecord.TypeBitMapContains(nsec!.TypeBitMaps, DNSResourceRecordTypes.MX), Is.False);

        }

        #endregion

        #region ZoneFile_Mnemonics_InAnyCase_AndTheGenericForms_StillRead()

        [Test]
        public void ZoneFile_Mnemonics_InAnyCase_AndTheGenericForms_StillRead()
        {

            var lower    = ADNSResourceRecord.ParseZoneFileString("x.example. 3600 in a 192.0.2.1");
            var generic  = ADNSResourceRecord.ParseZoneFileString(@"x.example. 3600 CLASS1 TYPE1 \# 4 c0000201");
            var chaos    = ADNSResourceRecord.ParseZoneFileString("x.example. 3600 CH TXT \"version\"");

            Assert.That(lower.  Type,  Is.EqualTo(DNSResourceRecordTypes.A));
            Assert.That(lower.  Class, Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(generic.Type,  Is.EqualTo(DNSResourceRecordTypes.A));
            Assert.That(generic.Class, Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(chaos.  Class, Is.EqualTo(DNSQueryClasses.CH));

        }

        #endregion

    }

}
