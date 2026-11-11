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
    /// RFC 2230 §3.1: a KX answer causes A and AAAA additional section processing
    /// for its exchanger; RFC 2181 §9: what does not fit is left out, TC clear.
    /// </summary>
    [TestFixture]
    public class KXAdditionalSection_Tests
    {

        private static readonly TimeSpan ttl = TimeSpan.FromMinutes(5);

        private static DomainName Name(String Label)
            => DomainName.Parse(Label + ".zone.test.");

        private static InMemoryDNSZone Zone(params IDNSResourceRecord[] Records)

            => new InMemoryDNSZone().
                   Add(
                       new SOA(DomainName.Parse("zone.test."), DNSQueryClasses.IN, ttl,
                               DomainName.Parse("ns.zone.test."), SimpleEMailAddress.Parse("hostmaster@zone.test"),
                               1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), TimeSpan.FromDays(7), TimeSpan.FromMinutes(5)),
                       new NS (DomainName.Parse("zone.test."), DNSQueryClasses.IN, ttl, Name("ns")),
                       new A  (Name("ns"), DNSQueryClasses.IN, ttl, IPv4Address.Parse("192.0.2.53"))
                   ).
                   Add(Records);

        private static DNSPacket KXQuery(String Label)
            => DNSPacket.Query(DNSServiceName.Parse(Name(Label).FullName), 0, DNSResourceRecordTypes.KX);

        #region TheExchangersAddressesAreAdded()

        [Test]
        public async Task TheExchangersAddressesAreAdded()
        {

            var zone     = Zone(new KX  (Name("host"), DNSQueryClasses.IN, ttl, 10, Name("kx")),
                                new A   (Name("kx"),   DNSQueryClasses.IN, ttl, IPv4Address.Parse("192.0.2.10")),
                                new AAAA(Name("kx"),   DNSQueryClasses.IN, ttl, IPv6Address.Parse("2001:db8::10")));

            var response = await new AuthoritativeDNSRequestHandler(zone).ProcessDNSRequest(KXQuery("host"));

            Assert.That(response, Is.Not.Null);
            Assert.That(response!.AnswerRRs.OfType<KX>().Count(),   Is.EqualTo(1));
            Assert.That(response. AdditionalRRs.OfType<A>().Count(),    Is.EqualTo(1));
            Assert.That(response. AdditionalRRs.OfType<AAAA>().Count(), Is.EqualTo(1));

        }

        #endregion

        #region AnExchangerOutsideTheZoneAddsNothing()

        [Test]
        public async Task AnExchangerOutsideTheZoneAddsNothing()
        {

            var zone     = Zone(new KX(Name("host"), DNSQueryClasses.IN, ttl, 10, DomainName.Parse("kx.elsewhere.example.")));

            var response = await new AuthoritativeDNSRequestHandler(zone).ProcessDNSRequest(KXQuery("host"));

            Assert.That(response!.AdditionalRRs.Where(record => record is not OPT), Is.Empty);

        }

        #endregion

        #region AddressesThatDoNotFitAreLeftOutWithTCClear()

        [Test]
        public async Task AddressesThatDoNotFitAreLeftOutWithTCClear()
        {

            // Three KX records fit in 512 octets; their six addresses beside them
            // do not. RFC 2181 §9: leave out whole RRsets, keep the answer, TC clear.
            var label   = new String('x', 58);
            var records = new List<IDNSResourceRecord>();

            for (var i = 0; i < 3; i++)
            {
                var exchanger = DomainName.Parse($"{label}{i}.zone.test.".Remove(57, 1));
                records.Add(new KX  (Name("many"), DNSQueryClasses.IN, ttl, (UInt16) (10 + i), exchanger));
                records.Add(new A   (exchanger,    DNSQueryClasses.IN, ttl, IPv4Address.Parse($"192.0.2.{20 + i}")));
                records.Add(new AAAA(exchanger,    DNSQueryClasses.IN, ttl, IPv6Address.Parse($"2001:db8::{20 + i}")));
            }

            var zone     = Zone([.. records]);
            var request  = KXQuery("many");
            var pipeline = new DNSMessagePipeline(new AuthoritativeDNSRequestHandler(zone));
            var response = await new AuthoritativeDNSRequestHandler(zone).ProcessDNSRequest(request);

            Assert.That(response!.AdditionalRRs.Count(record => record is A or AAAA), Is.EqualTo(6),
                        "all six addresses are in the response before it is put into a datagram");

            var wire     = pipeline.SerializeDatagramResponse(response, request);
            var datagram = DNSPacket.Parse(IPSocket.Parse("127.0.0.1:53"),
                                           IPSocket.Parse("127.0.0.1:5353"),
                                           new MemoryStream(wire));

            Assert.That(wire.Length,                          Is.LessThanOrEqualTo(512));
            Assert.That(datagram.Truncation,                  Is.False);
            // DNSPacket.Parse is the request path and keeps every type but A and
            // OPT opaque, so the answer is counted by its type code.
            Assert.That(datagram.AnswerRRs.Count(record => record.Type == DNSResourceRecordTypes.KX), Is.EqualTo(3));

        }

        #endregion

    }

}
