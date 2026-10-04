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
    /// RFC 9824 Compact Denial of Existence, as 1.1.1.1 served it on 2026-10-04 with DO
    /// set: a NODATA for mail.ietf.org. A whose NSEC names \000.mail.ietf.org. as the
    /// next domain name, and the DNSKEY RRset of ietf.org. that signed it.
    /// </summary>
    [TestFixture]
    public class CompactDenial_Tests
    {

        #region Data

        private const String MailIetfOrgNoData =
            "424281A00001000000040001046D61696C0469657466036F72670000010001C01100060001000007080032046A696C6C026E730A636C6F7564666C61726503636F6D0003646E73C033900A0C06000027100000096000093A8000000708C011002E000100000708005C00060D02000007086AC3B7AA6AC0F88A86C90469657466036F7267002B05EE3916FE7207B4C5503E117E620BB647B11E1B3B0056B2C2DF475994C0686AAB11DE4F8DBDF2809C99026DE2D9190567FFCD33FDF4D1BBFC1E59553A7387C00C002F000100000708001F0100046D61696C0469657466036F7267000009000D800C540B0D04C00101C0C00C002E000100000708005C002F0D03000007086AC3B7AA6AC0F88A86C90469657466036F726700C1912B27C0C2A8BA2C6BC784E1615A069422F0C39B48E77857F8AC507B55811B543658155EE2C84F96019A7137ACD73D86947C9F397E60B655442CC27CC740A100002904D0000080000000";

        private const String IetfOrgDNSKEY =
            "424281A000010003000000010469657466036F72670000300001C00C00300001000007E100440100030DA09311112CF9138818CD2FEAE970EBBD4D6A30F6088C25B325A39ABBC5CD1197AA098283E5AAF421177C2AA5D714992A9957D1BCC18F98CD71F1F1806B65E148C00C00300001000007E100440101030D99DB2CC14CABDC33D6D77DA63A2F15F71112584F234E8D1DC428E39E8A4A97E1AA271A555DC90701E17E2A4C4B6F120B7C32D44F4AC02BD894CF2D4BE7778A19C00C002E0001000007E1005C00300D0200000E106B10FE7F6AC092FF09430469657466036F72670075748D764320FED979AA6EB00EE47D40EEF55E6EE70D6BBD8CA8EBBE86DBBEB8D850F7CA6A88BC9250A321B8BCB5DF4D7B822405891F9734E94037F9525545C000002904D0000080000000";

        private static DNSInfo Read(String Recording, String QName, DNSResourceRecordTypes QType)

            => DNSInfo.ReadResponse(
                   new DNSServerConfig(IPv4Address.Localhost, IPPort.DNS),
                   0x4242,
                   [ new DNSQuestion(DNSServiceName.Parse(QName), QType, DNSQueryClasses.IN) ],
                   new MemoryStream(Convert.FromHexString(Recording)),
                   TimeSpan.FromSeconds(5),
                   TimeSpan.Zero
               );

        #endregion


        #region A_Compact_NODATA_Is_Read_As_A_Negative_Answer()

        /// <summary>
        /// The NSEC's next domain name begins with a label of one zero octet. Read with
        /// the hostname rules it threw, and the response came back as ServerFailure.
        /// </summary>
        [Test]
        public void A_Compact_NODATA_Is_Read_As_A_Negative_Answer()
        {

            var response = Read(MailIetfOrgNoData, "mail.ietf.org", DNSResourceRecordTypes.A);
            var nsec     = response.Authorities.OfType<NSEC>().Single();

            Assert.That(response.ResponseCode,               Is.EqualTo(DNSResponseCodes.NoError));
            Assert.That(response.IsValid,                    Is.True);
            Assert.That(response.Authorities.OfType<SOA>(),  Has.Exactly(1).Items);
            Assert.That(nsec.NextDomainName.Labels,          Is.EqualTo(new[] { "\0", "mail", "ietf", "org" }));

        }

        #endregion

        #region The_Zero_Octet_Label_Survives_Into_The_Canonical_Form()

        /// <summary>
        /// Reading the name is half of it: a validator rebuilds the canonical RDATA and
        /// checks the zone's signature over it (RFC 4034 §6.2). The signature ietf.org.
        /// made over this NSEC verifies only if the zero octet is written back as the
        /// one-octet label it was.
        /// </summary>
        [Test]
        public void The_Zero_Octet_Label_Survives_Into_The_Canonical_Form()
        {

            var response  = Read(MailIetfOrgNoData, "mail.ietf.org", DNSResourceRecordTypes.A);
            var nsec      = response.Authorities.OfType<NSEC>().Single();
            var signature = response.Authorities.OfType<RRSIG>().Single(rrsig => rrsig.TypeCovered == DNSResourceRecordTypes.NSEC);

            var keys      = Read(IetfOrgDNSKEY, "ietf.org", DNSResourceRecordTypes.DNSKEY).Answers.OfType<DNSKEY>();
            var key       = keys.Single(dnskey => DNSSECValidator.ComputeKeyTag(dnskey) == signature.KeyTag);

            Assert.That(Convert.ToHexString(ADNSResourceRecord.RDataOf(nsec)),
                        Does.StartWith("0100046D61696C0469657466036F726700"),
                        "the next domain name goes back on the wire as \\000.mail.ietf.org.");

            Assert.That(new DNSSECValidator(new FakeDNSClient()).ValidateRRSig([ nsec ], signature, key),
                        Is.EqualTo(DNSSECValidationResult.Secure),
                        "ietf.org.'s signature over the NSEC verifies");

        }

        #endregion

    }

}
