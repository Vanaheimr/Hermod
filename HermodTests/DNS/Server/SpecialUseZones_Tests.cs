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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Server
{

    /// <summary>
    /// What the authoritative handler answers for the special-use names of RFC 6761 §6.1 to §6.4: localhost and
    /// "invalid" names whatever the zone holds, test names and private-address reverse names where the zone is
    /// not authoritative.
    /// </summary>
    [TestFixture]
    public class SpecialUseZones_Tests
    {

        #region Data

        private static readonly TimeSpan ttl = TimeSpan.FromMinutes(5);

        #endregion

        #region (private) Zone(params Records)

        // zone.test., with an A at www.zone.test., and the records given beside it.
        private static InMemoryDNSZone Zone(params IDNSResourceRecord[] Records)

            => new InMemoryDNSZone().
                   Add(
                       new SOA(DomainName.Parse("zone.test."),
                               DNSQueryClasses.IN,
                               ttl,
                               DomainName.Parse("ns.zone.test."),
                               SimpleEMailAddress.Parse("hostmaster@zone.test"),
                               1,
                               TimeSpan.FromHours(1),
                               TimeSpan.FromMinutes(10),
                               TimeSpan.FromDays(7),
                               TimeSpan.FromMinutes(5)),
                       new NS (DomainName.Parse("zone.test."),     DNSQueryClasses.IN, ttl, DomainName.Parse("ns.zone.test.")),
                       new A  (DomainName.Parse("www.zone.test."), DNSQueryClasses.IN, ttl, IPv4Address.Parse("192.0.2.1"))
                   ).
                   Add(Records);

        #endregion

        #region (private) Ask(Zone, Name, Type)

        private static async Task<DNSResponse> Ask(InMemoryDNSZone         Zone,
                                                   String                  Name,
                                                   DNSResourceRecordTypes  Type)
        {

            var response = await new AuthoritativeDNSRequestHandler(Zone).
                                     ProcessDNSRequest(DNSPacket.Query(DNSServiceName.Parse(Name), 0, Type));

            Assert.That(response, Is.Not.Null);

            return response!;

        }

        #endregion

        #region (private) AssertCitesTheSpecialUseDomain(Response, Domain)

        // RFC 6303 §3: "@ 10800 IN SOA @ nobody.invalid. 1 3600 1200 604800 10800".
        private static void AssertCitesTheSpecialUseDomain(DNSResponse  Response,
                                                           String       Domain)
        {

            var soas = Response.AuthorityRRs.OfType<SOA>().ToArray();

            Assert.That(soas, Has.Length.EqualTo(1), "the negative answer cites one SOA (RFC 2308 §3)");

            var soa = soas[0];

            Assert.That(soa.DomainName.FullName.TrimEnd('.'), Is.EqualTo(Domain.TrimEnd('.')), "the SOA of the special-use domain");
            Assert.That(soa.Server.    FullName.TrimEnd('.'), Is.EqualTo(Domain.TrimEnd('.')), "the domain is its own primary server");
            Assert.That(soa.EMail.ToString(),                 Is.EqualTo("nobody@invalid"));
            Assert.That(soa.TimeToLive,                       Is.EqualTo(TimeSpan.FromSeconds(10800)));
            Assert.That(soa.Minimum,                          Is.EqualTo(TimeSpan.FromSeconds(10800)), "the negative TTL matches the SOA's own");

        }

        #endregion


        #region ALocalhostName_IsTheLoopback(Name, Type, Count)

        [TestCase("localhost.",      DNSResourceRecordTypes.A,    1)]
        [TestCase("www.localhost.",  DNSResourceRecordTypes.AAAA, 1)]
        [TestCase("a.b.LocalHost.",  DNSResourceRecordTypes.Any,  2)]
        public async Task ALocalhostName_IsTheLoopback(String Name, DNSResourceRecordTypes Type, Int32 Count)
        {

            var response = await Ask(Zone(), Name, Type);

            Assert.That(response.ResponseCode,        Is.EqualTo(DNSResponseCodes.NoError));
            Assert.That(response.AuthoritativeAnswer, Is.True);
            Assert.That(response.AnswerRRs.Count(),   Is.EqualTo(Count));
            Assert.That(response.AuthorityRRs,        Is.Empty);

            foreach (var answer in response.AnswerRRs)
            {

                Assert.That(answer.DomainName.FullName.TrimEnd('.'), Is.EqualTo(Name.TrimEnd('.')), "owned by the name asked, as asked");
                Assert.That(answer.TimeToLive,                       Is.EqualTo(TimeSpan.FromSeconds(10800)));

                if (answer is A a)
                    Assert.That(a.IPv4Address,    Is.EqualTo(IPv4Address.Localhost));

                else if (answer is AAAA aaaa)
                    Assert.That(aaaa.IPv6Address, Is.EqualTo(IPv6Address.Localhost));

                else
                    Assert.Fail($"an answer of type {answer.Type}");

            }

        }

        #endregion

        #region AnotherTypeOfALocalhostName_IsNoData()

        [Test]
        public async Task AnotherTypeOfALocalhostName_IsNoData()
        {

            var response = await Ask(Zone(), "mail.localhost.", DNSResourceRecordTypes.MX);

            Assert.That(response.ResponseCode,        Is.EqualTo(DNSResponseCodes.NoError), "the name has its addresses, so it exists");
            Assert.That(response.AuthoritativeAnswer, Is.True);
            Assert.That(response.AnswerRRs,           Is.Empty);

            AssertCitesTheSpecialUseDomain(response, "localhost.");

        }

        #endregion

        #region AnInvalidName_IsNXDomain(Name, Type)

        [TestCase("invalid.",       DNSResourceRecordTypes.A)]
        [TestCase("host.INVALID.",  DNSResourceRecordTypes.AAAA)]
        [TestCase("a.b.invalid.",   DNSResourceRecordTypes.TXT)]
        public async Task AnInvalidName_IsNXDomain(String Name, DNSResourceRecordTypes Type)
        {

            var response = await Ask(Zone(), Name, Type);

            Assert.That(response.ResponseCode,        Is.EqualTo(DNSResponseCodes.NameError));
            Assert.That(response.AuthoritativeAnswer, Is.True);
            Assert.That(response.AnswerRRs,           Is.Empty);

            AssertCitesTheSpecialUseDomain(response, "invalid.");

        }

        #endregion

        #region AnOnionName_IsNXDomain(Name, Type)

        // RFC 7686 §2, item 5: "Authoritative servers MUST respond to queries for .onion with NXDOMAIN."
        [TestCase("onion.",                DNSResourceRecordTypes.A)]
        [TestCase("hiddenservice.ONION.",  DNSResourceRecordTypes.AAAA)]
        [TestCase("a.b.onion.",            DNSResourceRecordTypes.TXT)]
        public async Task AnOnionName_IsNXDomain(String Name, DNSResourceRecordTypes Type)
        {

            var response = await Ask(Zone(), Name, Type);

            Assert.That(response.ResponseCode,        Is.EqualTo(DNSResponseCodes.NameError));
            Assert.That(response.AuthoritativeAnswer, Is.True);
            Assert.That(response.AnswerRRs,           Is.Empty);

            AssertCitesTheSpecialUseDomain(response, "onion.");

        }

        #endregion

        #region TheZone_DoesNotChangeALocalhostOrAnInvalidName()

        [Test]
        public async Task TheZone_DoesNotChangeALocalhostOrAnInvalidName()
        {

            // RFC 6761 §6.3 and §6.4, item 6: their answers "cannot be modified by local configuration". RFC 7686
            // §2, item 6: operators "MUST NOT configure an authoritative DNS server to answer queries for .onion".
            var zone = Zone(
                           new A    (DomainName.Parse("localhost."),      DNSQueryClasses.IN, ttl, IPv4Address.Parse("192.0.2.97")),
                           new CNAME(DomainName.Parse("mail.localhost."), DNSQueryClasses.IN, ttl, DomainName.Parse("www.zone.test.")),
                           new A    (DomainName.Parse("host.invalid."),   DNSQueryClasses.IN, ttl, IPv4Address.Parse("192.0.2.98")),
                           new A    (DomainName.Parse("host.onion."),     DNSQueryClasses.IN, ttl, IPv4Address.Parse("192.0.2.99"))
                       );

            var onion     = await Ask(zone, "host.onion.", DNSResourceRecordTypes.A);

            Assert.That(onion.ResponseCode, Is.EqualTo(DNSResponseCodes.NameError));
            Assert.That(onion.AnswerRRs,    Is.Empty);

            var localhost = await Ask(zone, "localhost.", DNSResourceRecordTypes.A);

            Assert.That(localhost.AnswerRRs.OfType<A>().Select(a => a.IPv4Address), Is.EqualTo(new[] { IPv4Address.Localhost }));

            // An alias at a localhost name is configuration too: NODATA, without the CNAME.
            var mail      = await Ask(zone, "mail.localhost.", DNSResourceRecordTypes.MX);

            Assert.That(mail.ResponseCode, Is.EqualTo(DNSResponseCodes.NoError));
            Assert.That(mail.AnswerRRs,    Is.Empty);

            var invalid   = await Ask(zone, "host.invalid.", DNSResourceRecordTypes.A);

            Assert.That(invalid.ResponseCode, Is.EqualTo(DNSResponseCodes.NameError));
            Assert.That(invalid.AnswerRRs,    Is.Empty);

        }

        #endregion

        #region ATestName_OutsideTheZone_IsNXDomain()

        [Test]
        public async Task ATestName_OutsideTheZone_IsNXDomain()
        {

            var response = await Ask(Zone(), "other.test.", DNSResourceRecordTypes.A);

            Assert.That(response.ResponseCode,        Is.EqualTo(DNSResponseCodes.NameError), "not REFUSED: RFC 6761 §6.2, item 5");
            Assert.That(response.AuthoritativeAnswer, Is.True);
            Assert.That(response.AnswerRRs,           Is.Empty);

            AssertCitesTheSpecialUseDomain(response, "test.");

        }

        #endregion

        #region ATestName_AboveTheZone_IsNoData()

        [Test]
        public async Task ATestName_AboveTheZone_IsNoData()
        {

            // zone.test. is below test., and NXDOMAIN would deny it (RFC 8020 §2).
            var response = await Ask(Zone(), "test.", DNSResourceRecordTypes.A);

            Assert.That(response.ResponseCode, Is.EqualTo(DNSResponseCodes.NoError));
            Assert.That(response.AnswerRRs,    Is.Empty);

            AssertCitesTheSpecialUseDomain(response, "test.");

        }

        #endregion

        #region ATestNameInTheZone_IsTheZones()

        [Test]
        public async Task ATestNameInTheZone_IsTheZones()
        {

            var found    = await Ask(Zone(), "www.zone.test.", DNSResourceRecordTypes.A);

            Assert.That(found.AnswerRRs.OfType<A>().Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("192.0.2.1")));

            // And the zone's own "does not exist", with the zone's own SOA.
            var missing  = await Ask(Zone(), "nx.zone.test.", DNSResourceRecordTypes.A);

            Assert.That(missing.ResponseCode,                                            Is.EqualTo(DNSResponseCodes.NameError));
            Assert.That(missing.AuthorityRRs.OfType<SOA>().Single().DomainName.FullName.TrimEnd('.'), Is.EqualTo("zone.test"));

        }

        #endregion

        #region APrivateAddressReverseName_ThatIsNotServed_IsNegative(Name, Domain, ResponseCode)

        [TestCase("1.0.0.10.in-addr.arpa.",    "10.in-addr.arpa.",      DNSResponseCodes.NameError)]
        [TestCase("4.3.16.172.in-addr.arpa.",  "16.172.in-addr.arpa.",  DNSResponseCodes.NameError)]
        [TestCase("31.172.IN-ADDR.ARPA.",      "31.172.in-addr.arpa.",  DNSResponseCodes.NameError)]
        [TestCase("2.168.192.in-addr.arpa.",   "168.192.in-addr.arpa.", DNSResponseCodes.NameError)]
        [TestCase("1.168.192.in-addr.arpa.",   "168.192.in-addr.arpa.", DNSResponseCodes.NoError)]
        [TestCase("168.192.in-addr.arpa.",     "168.192.in-addr.arpa.", DNSResponseCodes.NoError)]
        public async Task APrivateAddressReverseName_ThatIsNotServed_IsNegative(String Name, String Domain, DNSResponseCodes ResponseCode)
        {

            // The zone holds a PTR at 5.1.168.192.in-addr.arpa., outside its apex; the names above it are NODATA.
            var zone     = Zone(new PTR(DomainName.Parse("5.1.168.192.in-addr.arpa."), DNSQueryClasses.IN, ttl, DNSServiceName.Parse("www.zone.test.")));

            var response = await Ask(zone, Name, DNSResourceRecordTypes.PTR);

            Assert.That(response.ResponseCode, Is.EqualTo(ResponseCode));
            Assert.That(response.AnswerRRs,    Is.Empty);

            AssertCitesTheSpecialUseDomain(response, Domain);

            var served   = await Ask(zone, "5.1.168.192.in-addr.arpa.", DNSResourceRecordTypes.PTR);

            Assert.That(served.AnswerRRs.OfType<PTR>().Count(), Is.EqualTo(1), "the PTR that is served is the configuration that gives a positive answer");

        }

        #endregion

        #region ANameThatIsNotSpecial_IsStillRefused(Name)

        [TestCase("example.com.")]
        [TestCase("localhost.example.")]
        [TestCase("15.172.in-addr.arpa.")]
        [TestCase("32.172.in-addr.arpa.")]
        [TestCase("016.172.in-addr.arpa.")]
        [TestCase("192.168.in-addr.arpa.")]
        [TestCase("onions.")]
        [TestCase("onion.example.")]
        [TestCase("name.alt.")]
        public async Task ANameThatIsNotSpecial_IsStillRefused(String Name)
        {

            var response = await Ask(Zone(), Name, DNSResourceRecordTypes.A);

            Assert.That(response.ResponseCode,        Is.EqualTo(DNSResponseCodes.Refused));
            Assert.That(response.AuthoritativeAnswer, Is.False);
            Assert.That(response.AuthorityRRs,        Is.Empty);

        }

        #endregion

        #region ALinkLocalName_IsNXDomain(Name, Domain)

        // RFC 6762 §22.1, item 5: "like caching DNS servers, SHOULD generate immediate NXDOMAIN responses".
        [TestCase("local.",                     "local.")]
        [TestCase("printer.Local.",             "local.")]
        [TestCase("4.3.254.169.in-addr.arpa.",  "254.169.in-addr.arpa.")]
        [TestCase("1.8.e.f.ip6.arpa.",          "8.e.f.ip6.arpa.")]
        [TestCase("F.B.E.F.IP6.ARPA.",          "b.e.f.ip6.arpa.")]
        public async Task ALinkLocalName_IsNXDomain(String Name, String Domain)
        {

            var response = await Ask(Zone(), Name, DNSResourceRecordTypes.A);

            Assert.That(response.ResponseCode,        Is.EqualTo(DNSResponseCodes.NameError));
            Assert.That(response.AuthoritativeAnswer, Is.True);
            Assert.That(response.AnswerRRs,           Is.Empty);

            AssertCitesTheSpecialUseDomain(response, Domain);

        }

        #endregion

        #region ALinkLocalName_IsServed_WhenToldTo_OrWhenAskedByMulticast()

        [Test]
        public async Task ALinkLocalName_IsServed_WhenToldTo_OrWhenAskedByMulticast()
        {

            var zone       = Zone(new A(DomainName.Parse("printer.local."), DNSQueryClasses.IN, ttl, IPv4Address.Parse("192.0.2.100")));

            // By default the record is not served (RFC 6762 §22.1, item 5).
            var byDefault  = await Ask(zone, "printer.local.", DNSResourceRecordTypes.A);

            Assert.That(byDefault.ResponseCode, Is.EqualTo(DNSResponseCodes.NameError));

            // The option item 5 lets server software offer: the zone answers, and a name it does not hold is
            // outside it, as any other name is.
            var serving    = new AuthoritativeDNSRequestHandler(zone, ServeLinkLocalNames: true);

            var served     = await serving.ProcessDNSRequest(DNSPacket.Query(DNSServiceName.Parse("printer.local."), 0, DNSResourceRecordTypes.A));
            var elsewhere  = await serving.ProcessDNSRequest(DNSPacket.Query(DNSServiceName.Parse("scanner.local."), 0, DNSResourceRecordTypes.A));

            Assert.That(served!.AnswerRRs.OfType<A>().Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("192.0.2.100")));
            Assert.That(elsewhere!.ResponseCode,                            Is.EqualTo(DNSResponseCodes.Refused));

            // A query that arrived on the multicast listener is answered from the zone without the option.
            var request    = DNSPacket.Query(DNSServiceName.Parse("printer.local."), 0, DNSResourceRecordTypes.A);
            request.ReceivedViaMulticast = true;

            var multicast  = await new AuthoritativeDNSRequestHandler(zone).ProcessDNSRequest(request);

            Assert.That(multicast!.AnswerRRs.OfType<A>().Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("192.0.2.100")));

        }

        #endregion

        #region AQuestionInAnotherClass_IsTheZones()

        [Test]
        public async Task AQuestionInAnotherClass_IsTheZones()
        {

            // localhost. A in class CH (3): the special-use names are names of the Internet class.
            Byte[] query = [ 0x12, 0x34, 0x01, 0x00,  0x00, 0x01, 0x00, 0x00,  0x00, 0x00, 0x00, 0x00,
                             9, (Byte) 'l', (Byte) 'o', (Byte) 'c', (Byte) 'a', (Byte) 'l', (Byte) 'h', (Byte) 'o', (Byte) 's', (Byte) 't', 0,
                             0x00, 0x01,  0x00, 0x03 ];

            var request  = DNSPacket.Parse(IPSocket.Parse("127.0.0.1:53"),
                                           IPSocket.Parse("127.0.0.1:5353"),
                                           new MemoryStream(query));

            Assert.That(request.Questions.Single().QueryClass, Is.EqualTo(DNSQueryClasses.CH));

            var response = await new AuthoritativeDNSRequestHandler(Zone()).ProcessDNSRequest(request);

            Assert.That(response,                Is.Not.Null);
            Assert.That(response!.ResponseCode,  Is.EqualTo(DNSResponseCodes.Refused));
            Assert.That(response.AnswerRRs,      Is.Empty);

        }

        #endregion

        #region TheZone_SaysWhetherItHoldsNamesBelow(Name, HoldsNamesBelow)

        [TestCase("test.",                   true)]
        [TestCase("other.test.",             false)]
        [TestCase("1.168.192.in-addr.arpa.", true)]
        [TestCase("2.168.192.in-addr.arpa.", false)]
        public async Task TheZone_SaysWhetherItHoldsNamesBelow(String Name, Boolean HoldsNamesBelow)
        {

            var zone   = Zone(new PTR(DomainName.Parse("5.1.168.192.in-addr.arpa."), DNSQueryClasses.IN, ttl, DNSServiceName.Parse("www.zone.test.")));

            var result = await zone.Lookup(new DNSQuestion(DNSServiceName.Parse(Name), DNSResourceRecordTypes.A, DNSQueryClasses.IN));

            Assert.That(result.Status,          Is.EqualTo(DNSZoneLookupStatus.NotAuthoritative));
            Assert.That(result.HoldsNamesBelow, Is.EqualTo(HoldsNamesBelow));

        }

        #endregion

        #region SpecialUseNames_KnowTheirDomains(Name, Domain)

        [TestCase("localhost.",               "localhost.")]
        [TestCase("a.LOCALHOST.",             "localhost.")]
        [TestCase("x.invalid.",               "invalid.")]
        [TestCase("x.Onion.",                 "onion.")]
        [TestCase("printer.local.",           "local.")]
        [TestCase("4.3.254.169.in-addr.arpa.","254.169.in-addr.arpa.")]
        [TestCase("1.9.E.F.ip6.arpa.",        "9.e.f.ip6.arpa.")]
        [TestCase("1.1254.169.in-addr.arpa.", null)]
        [TestCase("x.Test.",                  "test.")]
        [TestCase("10.in-addr.arpa.",         "10.in-addr.arpa.")]
        [TestCase("9.9.9.10.in-addr.arpa.",   "10.in-addr.arpa.")]
        [TestCase("16.172.in-addr.arpa.",     "16.172.in-addr.arpa.")]
        [TestCase("1.2.31.172.in-addr.arpa.", "31.172.in-addr.arpa.")]
        [TestCase("168.192.In-Addr.Arpa.",    "168.192.in-addr.arpa.")]
        [TestCase("example.",                 null)]
        [TestCase("in-addr.arpa.",            null)]
        [TestCase("172.in-addr.arpa.",        null)]
        [TestCase("192.in-addr.arpa.",        null)]
        [TestCase("15.172.in-addr.arpa.",     null)]
        [TestCase("32.172.in-addr.arpa.",     null)]
        [TestCase("016.172.in-addr.arpa.",    null)]
        [TestCase("169.192.in-addr.arpa.",    null)]
        [TestCase("10.ip6.arpa.",             null)]
        [TestCase("10.in-addr.arpa.example.", null)]
        public void SpecialUseNames_KnowTheirDomains(String Name, String? Domain)
        {

            var name = DNSServiceName.Parse(Name);

            Assert.That(SpecialUseNames.DomainOf(name), Is.EqualTo(Domain));

            var linkLocal = Domain is "local." or "254.169.in-addr.arpa." || Domain?.EndsWith(".e.f.ip6.arpa.") == true;

            Assert.That(SpecialUseNames.IsPrivateAddressReverse(name), Is.EqualTo(Domain?.EndsWith("in-addr.arpa.") == true && !linkLocal));
            Assert.That(SpecialUseNames.IsLinkLocal(name),             Is.EqualTo(linkLocal));
            Assert.That(SpecialUseNames.IsTest(name),                  Is.EqualTo(Domain == "test."));

        }

        #endregion

    }

}
