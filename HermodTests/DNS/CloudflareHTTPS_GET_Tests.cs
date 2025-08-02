/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    // https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/

    // => https://dns.cloudflare.com/dns-query

    /// <summary>
    /// Some Cloudflare DNS HTTPS GET tests.
    /// </summary>
    [TestFixture]
    public class CloudflareHTTPS_GET_Tests
    {

        #region Setup/Teardown

        private DNSHTTPSClient? client;

        [OneTimeSetUp]
        public void InitTests()
        {

            client = DNSHTTPSClient.Cloudflare_DNSName(Mode: DNSHTTPSMode.GET);

        }

        [OneTimeTearDown]
        public void ShutdownTests()
        {

            client?.Dispose();

        }

        #endregion


        #region CloudflareUDP_charging_cloud__A()

        [Test]
        public async Task CloudflareUDP_charging_cloud__A()
        {

            if (client is null)
            {
                Assert.Fail("The DNS client is null!");
                return;
            }

            var response = await client.Query<A>(DomainName.Parse("charging.cloud"));

            Assert.That(response,                        Is.Not.Null);
            Assert.That(response.IsValid,                Is.True);
            Assert.That(response.Answers.Count,          Is.EqualTo(1), $"{client.RemoteURL} failed!");

            if (response.Answers.First() is not A answer)
            {
                Assert.Fail("The 1st answer is not a DNS A resource record!");
                return;
            }

            Assert.That(answer.DomainName. ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer.Class,                    Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer.Type,                     Is.EqualTo(DNSResourceRecordTypes.A));
            Assert.That(answer.IPv4Address.ToString(),   Is.EqualTo("23.88.66.160"));

        }

        #endregion

        #region CloudflareUDP_charging_cloud__AAAA()

        [Test]
        public async Task CloudflareUDP_charging_cloud__AAAA()
        {

            if (client is null)
            {
                Assert.Fail("The DNS client is null!");
                return;
            }

            var response = await client.Query<AAAA>(DomainName.Parse("charging.cloud"));

            Assert.That(response,                        Is.Not.Null);
            Assert.That(response.IsValid,                Is.True);
            Assert.That(response.Answers.Count,          Is.EqualTo(1), $"{client.RemoteURL} failed!");

            if (response.Answers.First() is not AAAA answer)
            {
                Assert.Fail("The 1st answer is not a DNS AAAA resource record!");
                return;
            }

            Assert.That(answer.DomainName. ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer.Class,                    Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer.Type,                     Is.EqualTo(DNSResourceRecordTypes.AAAA));
            Assert.That(answer.IPv6Address.ToString(),   Is.EqualTo("2a01:04f8:0272:41de:0000:0000:0000:0002"));

        }

        #endregion

        #region CloudflareUDP_charging_cloud__MX()

        [Test]
        public async Task CloudflareUDP_charging_cloud__MX()
        {

            if (client is null)
            {
                Assert.Fail("The DNS client is null!");
                return;
            }

            var response = await client.Query<MX>(DomainName.Parse("charging.cloud"));

            Assert.That(response,                       Is.Not.Null);
            Assert.That(response.IsValid,               Is.True);
            Assert.That(response.Answers.Count,         Is.EqualTo(1));

            if (response.Answers.First() is not MX answer)
            {
                Assert.Fail("The 1st answer is not a DNS MX resource record!");
                return;
            }

            Assert.That(answer.DomainName.ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer.Class,                   Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer.Type,                    Is.EqualTo(DNSResourceRecordTypes.MX));
            Assert.That(answer.Exchange.  ToString(),   Is.EqualTo("mail.graphdefined.com."));
            Assert.That(answer.Preference,              Is.EqualTo(10));

        }

        #endregion

        #region CloudflareUDP_charging_cloud__TXT()

        [Test]
        public async Task CloudflareUDP_charging_cloud__TXT()
        {

            if (client is null)
            {
                Assert.Fail("The DNS client is null!");
                return;
            }

            var response = await client.Query<TXT>(DomainName.Parse("charging.cloud"));

            Assert.That(response,                        Is.Not.Null);
            Assert.That(response.IsValid,                Is.True);
            Assert.That(response.Answers.Count,          Is.EqualTo(3));

            if (response.Answers.ElementAt(0) is not TXT answer1)
            {
                Assert.Fail("The 1st answer is not a DNS TXT resource record!");
                return;
            }

            if (response.Answers.ElementAt(1) is not TXT answer2)
            {
                Assert.Fail("The 2nd answer is not a DNS TXT resource record!");
                return;
            }

            if (response.Answers.ElementAt(2) is not TXT answer3)
            {
                Assert.Fail("The 3rd answer is not a DNS TXT resource record!");
                return;
            }

            Assert.That(answer1.DomainName.ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer1.Class,                   Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer1.Type,                    Is.EqualTo(DNSResourceRecordTypes.TXT));

            Assert.That(answer2.DomainName.ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer2.Class,                   Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer2.Type,                    Is.EqualTo(DNSResourceRecordTypes.TXT));

            Assert.That(answer3.DomainName.ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer3.Class,                   Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer3.Type,                    Is.EqualTo(DNSResourceRecordTypes.TXT));

            var answers  = new HashSet<String> {
                               answer1.Text,
                               answer2.Text,
                               answer3.Text
                           };

            Assert.That(answers.Contains("The secure, privacy-aware and scalable e-mobility protocol architecture"),  Is.True);
            Assert.That(answers.Contains("v=spf1 a mx -all"),                                                         Is.True);
            Assert.That(answers.Contains("google-site-verification=LRWWULOmv2wDo9muA8IprEWEu1JtLZsP7sZINc6fS-Y"),     Is.True);

        }

        #endregion


        #region CloudflareUDP_open_charging_cloud__A()

        [Test]
        public async Task CloudflareUDP_open_charging_cloud__A()
        {

            if (client is null)
            {
                Assert.Fail("The DNS client is null!");
                return;
            }

            var response = await client.Query<A>(DomainName.Parse("open.charging.cloud"));

            Assert.That(response,                        Is.Not.Null);
            Assert.That(response.IsValid,                Is.True);
            Assert.That(response.Answers.Count,          Is.EqualTo(2));

            if (response.Answers.First(c => c.Type == DNSResourceRecordTypes.CNAME) is not CNAME answer1)
            {
                Assert.Fail("The 1st answer is not a DNS CNAME resource record!");
                return;
            }

            if (response.Answers.First(c => c.Type == DNSResourceRecordTypes.A)     is not A     answer2)
            {
                Assert.Fail("The 2nd answer is not a DNS A resource record!");
                return;
            }

            Assert.That(answer1.DomainName. ToString(),   Is.EqualTo("open.charging.cloud."));
            Assert.That(answer1.Class,                    Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer1.Type,                     Is.EqualTo(DNSResourceRecordTypes.CNAME));
            Assert.That(answer1.CName.      ToString(),   Is.EqualTo("charging.cloud."));

            Assert.That(answer2.DomainName. ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer2.Class,                    Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer2.Type,                     Is.EqualTo(DNSResourceRecordTypes.A));
            Assert.That(answer2.IPv4Address.ToString(),   Is.EqualTo("23.88.66.160"));

        }

        #endregion

        #region CloudflareUDP_open_charging_cloud__AAAA()

        [Test]
        public async Task CloudflareUDP_open_charging_cloud__AAAA()
        {

            if (client is null)
            {
                Assert.Fail("The DNS client is null!");
                return;
            }

            var response = await client.Query<AAAA>(DomainName.Parse("open.charging.cloud"));

            Assert.That(response,                        Is.Not.Null);
            Assert.That(response.IsValid,                Is.True);
            Assert.That(response.Answers.Count,          Is.EqualTo(2));

            if (response.Answers.First(c => c.Type == DNSResourceRecordTypes.CNAME) is not CNAME answer1)
            {
                Assert.Fail("The 1st answer is not a DNS CNAME resource record!");
                return;
            }

            if (response.Answers.First(c => c.Type == DNSResourceRecordTypes.AAAA)  is not AAAA  answer2)
            {
                Assert.Fail("The 2nd answer is not a DNS AAAA resource record!");
                return;
            }

            Assert.That(answer1.DomainName. ToString(),   Is.EqualTo("open.charging.cloud."));
            Assert.That(answer1.Class,                    Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer1.Type,                     Is.EqualTo(DNSResourceRecordTypes.CNAME));
            Assert.That(answer1.CName.      ToString(),   Is.EqualTo("charging.cloud."));

            Assert.That(answer2.DomainName. ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer2.Class,                    Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer2.Type,                     Is.EqualTo(DNSResourceRecordTypes.AAAA));
            Assert.That(answer2.IPv6Address.ToString(),   Is.EqualTo("2a01:04f8:0272:41de:0000:0000:0000:0002"));

        }

        #endregion

        #region CloudflareUDP_open_charging_cloud__MX()

        [Test]
        public async Task CloudflareUDP_open_charging_cloud__MX()
        {

            if (client is null)
            {
                Assert.Fail("The DNS client is null!");
                return;
            }

            var response = await client.Query<MX>(DomainName.Parse("open.charging.cloud"));

            Assert.That(response,                       Is.Not.Null);
            Assert.That(response.IsValid,               Is.True);
            Assert.That(response.Answers.Count,          Is.EqualTo(2));

            if (response.Answers.First(c => c.Type == DNSResourceRecordTypes.CNAME) is not CNAME answer1)
            {
                Assert.Fail("The 1st answer is not a DNS CNAME resource record!");
                return;
            }

            if (response.Answers.First(c => c.Type == DNSResourceRecordTypes.MX)    is not MX    answer2)
            {
                Assert.Fail("The 2nd answer is not a DNS MX resource record!");
                return;
            }

            Assert.That(answer1.DomainName. ToString(),   Is.EqualTo("open.charging.cloud."));
            Assert.That(answer1.Class,                    Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer1.Type,                     Is.EqualTo(DNSResourceRecordTypes.CNAME));
            Assert.That(answer1.CName.      ToString(),   Is.EqualTo("charging.cloud."));

            Assert.That(answer2.DomainName. ToString(),   Is.EqualTo("charging.cloud."));
            Assert.That(answer2.Class,                    Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer2.Type,                     Is.EqualTo(DNSResourceRecordTypes.MX));
            Assert.That(answer2.Exchange.  ToString(),    Is.EqualTo("mail.graphdefined.com."));
            Assert.That(answer2.Preference,               Is.EqualTo(10));

        }

        #endregion


        #region CloudflareUDP__ocpp_tls_api_charging_cloud__SRV()

        [Test]
        public async Task CloudflareUDP_ocpp_tls_api_charging_cloud__SRV()
        {

            if (client is null)
            {
                Assert.Fail("The DNS client is null!");
                return;
            }

            var response = await client.Query<SRV>(DNSServiceName.Parse("_ocpp._tls.api.charging.cloud"));

            Assert.That(response,                        Is.Not.Null);
            Assert.That(response.IsValid,                Is.True);
            Assert.That(response.Answers.Count,          Is.EqualTo(3));

            if (response.Answers.ElementAt(0) is not SRV answer1)
            {
                Assert.Fail("The 1st answer is not a DNS SRV resource record!");
                return;
            }

            if (response.Answers.ElementAt(1) is not SRV answer2)
            {
                Assert.Fail("The 2nd answer is not a DNS SRV resource record!");
                return;
            }

            if (response.Answers.ElementAt(2) is not SRV answer3)
            {
                Assert.Fail("The 3rd answer is not a DNS SRV resource record!");
                return;
            }

            Assert.That(answer1.DomainName.ToString(),   Is.EqualTo("_ocpp._tls.api.charging.cloud."));
            Assert.That(answer1.Class,                   Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer1.Type,                    Is.EqualTo(DNSResourceRecordTypes.SRV));

            Assert.That(answer2.DomainName.ToString(),   Is.EqualTo("_ocpp._tls.api.charging.cloud."));
            Assert.That(answer2.Class,                   Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer2.Type,                    Is.EqualTo(DNSResourceRecordTypes.SRV));

            Assert.That(answer3.DomainName.ToString(),   Is.EqualTo("_ocpp._tls.api.charging.cloud."));
            Assert.That(answer3.Class,                   Is.EqualTo(DNSQueryClasses.IN));
            Assert.That(answer3.Type,                    Is.EqualTo(DNSResourceRecordTypes.SRV));

            var answers  = new HashSet<SRV> {
                               answer1,
                               answer2,
                               answer3
                           };

            var answerA  = answers.FirstOrDefault(srv => srv.Target.ToString() == "api1.charging.cloud.");
            var answerB  = answers.FirstOrDefault(srv => srv.Target.ToString() == "api2.charging.cloud.");
            var answerC  = answers.FirstOrDefault(srv => srv.Target.ToString() == "api3.charging.cloud.");

        }

        #endregion


    }

}
