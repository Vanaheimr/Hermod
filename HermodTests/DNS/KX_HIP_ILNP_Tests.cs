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
    /// KX (RFC 2230), HIP (RFC 8005) and the four ILNP types of RFC 6742: each
    /// read from the presentation form as BIND writes it, written back the same
    /// way, and kept as the octets that arrived when its RDATA is not what the
    /// type says.
    /// </summary>
    [TestFixture]
    public class KX_HIP_ILNP_Tests
    {

        private static String Hex(Byte[] Bytes)
            => Convert.ToHexString(Bytes).ToLowerInvariant();

        private static String RDataText(IDNSResourceRecord Record)
            => Record.ToZoneFileString().Split($" {ADNSResourceRecord.TypeName(Record.Type)} ")[1].Trim();

        #region PresentationForm_RoundTrips(Line, Expected, WireHex)

        /// <summary>
        /// One line per type, its RDATA as BIND writes it back, and its wire form.
        /// </summary>
        [TestCase("KX 10 Mail.Example.",                    "10 Mail.Example.",                     "000a044d61696c074578616d706c6500")]
        [TestCase("LP 10 L64.example.",                     "10 L64.example.",                      "000a034c3634076578616d706c6500")]
        [TestCase("NID 10 0014:4FFF:FF20:EE64",             "10 14:4fff:ff20:ee64",                 "000a00144fffff20ee64")]
        [TestCase("L64 10 2001:0DB8:1140:1000",             "10 2001:db8:1140:1000",                "000a20010db811401000")]
        [TestCase("L32 10 10.1.2.0",                        "10 10.1.2.0",                          "000a0a010200")]
        [TestCase("HIP 2 ab AwEAAQ== rvs.example.",         "2 AB AwEAAQ== rvs.example.",           "01020004ab03010001037276730765" + "78616d706c6500")]
        public void PresentationForm_RoundTrips(String Line, String Expected, String WireHex)
        {

            var record = ADNSResourceRecord.ParseZoneFileString($"probe.example. 300 IN {Line}");

            Assert.That(RDataText(record),                       Is.EqualTo(Expected));
            Assert.That(Hex(ADNSResourceRecord.RDataOf(record)), Is.EqualTo(WireHex));

        }

        #endregion

        #region Refused(Line)

        /// <summary>
        /// "::" in a NID (RFC 6742 §2.1.2), a leading zero in an L32 (Erratum 3413),
        /// an LP that points at its own owner (§2.4.1.2), and a HIP with no key
        /// (RFC 8005 §5).
        /// </summary>
        [TestCase("NID 10 0014:4fff::ee64")]
        [TestCase("L32 10 10.1.02.0")]
        [TestCase("LP 10 probe.example.")]
        [TestCase("HIP 2 ab")]
        [TestCase("KX 65536 mail.example.")]
        public void Refused(String Line)
        {

            Assert.That(ADNSResourceRecord.TryParseZoneFileString($"probe.example. 300 IN {Line}", out _, out _), Is.False);

        }

        #endregion

        #region RDataOfTheWrongShape_IsKeptAsItArrived(Type, Hex)

        /// <summary>
        /// A compressed KX exchanger, a NID of nine octets, a HIP whose key runs off
        /// the end: kept, written back unchanged, and shown in RFC 3597 §5's form.
        /// </summary>
        [TestCase("KX",  "000ac00c")]
        [TestCase("NID", "000a00144fffff20ee")]
        [TestCase("HIP", "01020002ab01")]
        public void RDataOfTheWrongShape_IsKeptAsItArrived(String Type, String RDataHex)
        {

            var rdata  = Convert.FromHexString(RDataHex);
            var record = ADNSResourceRecord.ParseZoneFileString($@"probe.example. 300 IN {Type} \# {rdata.Length} {RDataHex}");

            Assert.That(Hex(ADNSResourceRecord.RDataOf(record)), Is.EqualTo(RDataHex));
            Assert.That(RDataText(record),                       Is.EqualTo($@"\# {rdata.Length} {RDataHex}"));

        }

        #endregion

    }

}
