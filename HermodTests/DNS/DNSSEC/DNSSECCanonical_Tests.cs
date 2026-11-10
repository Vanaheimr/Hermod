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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.DNSSEC
{

    /// <summary>
    /// RFC 4034 §6.2 item 3: the names in the RDATA of the types RFC 3597 §7
    /// lists are lowercased in the canonical form, and the names of every later
    /// type are not.
    /// </summary>
    [TestFixture]
    public class DNSSECCanonical_Tests
    {

        private static readonly DomainName Owner = DomainName.Parse("probe.example.");
        private static readonly TimeSpan   TTL   = TimeSpan.FromHours(1);

        private static Byte[] SignedData(IDNSResourceRecord Record)

            => DNSSECCanonical.SignedData(
                   [ Record ],
                   new RRSIG(Owner, DNSQueryClasses.IN, TTL, Record.Type, 13, 2, 3600,
                             1893456000, 1735689600, 12345, DomainName.Parse("example."), [ 0x00 ])
               );

        #region AnMxSignsLikeItsLowercaseTwin()

        /// <summary>
        /// "Mail.Example." and "mail.example." are one name (RFC 4343), and an MX
        /// is on the list, so BIND signs both over the same octets. So must Hermod.
        /// </summary>
        [Test]
        public void AnMxSignsLikeItsLowercaseTwin()
        {

            var mixed = new MX(Owner, DNSQueryClasses.IN, TTL, 10, DomainName.Parse("Mail.Example."));
            var lower = new MX(Owner, DNSQueryClasses.IN, TTL, 10, DomainName.Parse("mail.example."));

            Assert.That(SignedData(mixed), Is.EqualTo(SignedData(lower)));

        }

        #endregion

        #region AnLpSignsItsNameAsWritten()

        /// <summary>
        /// LP (RFC 6742) came after RFC 3597, and "no downcasing of embedded domain
        /// names takes place" for it: the two spellings sign differently.
        /// </summary>
        [Test]
        public void AnLpSignsItsNameAsWritten()
        {

            var mixed = new LP(Owner, DNSQueryClasses.IN, TTL, 10, DomainName.Parse("Subnet.Example."));
            var lower = new LP(Owner, DNSQueryClasses.IN, TTL, 10, DomainName.Parse("subnet.example."));

            Assert.That(SignedData(mixed), Is.Not.EqualTo(SignedData(lower)));

        }

        #endregion

        #region OnlyTheNamesAreLowercased()

        /// <summary>
        /// An SRV whose priority, weight and port read as "AB", "CD" and "EF": the
        /// target is lowercased, the numbers in front of it are not.
        /// </summary>
        [Test]
        public void OnlyTheNamesAreLowercased()
        {

            var rdata = Convert.FromHexString("414243444546" + "03536970" + "074578616d706c65" + "00");

            Assert.That(Convert.ToHexString(DNSSECCanonical.CanonicalRData((UInt16) DNSResourceRecordTypes.SRV, rdata)),
                        Is.EqualTo("414243444546" + "03736970" + "076578616D706C65" + "00"));

        }

        #endregion

    }

}
