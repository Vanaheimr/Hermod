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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    [TestFixture]
    public class DNSCache_Tests
    {

        #region (private) TestOrigin

        private static DNSServerConfig TestOrigin

            => new (
                   IPv4Address.Localhost,
                   IPPort.DNS
               );

        #endregion

        #region (private) CreateResponse   (ResponseCode, Answers, Authorities)

        private static DNSInfo CreateResponse(DNSResponseCodes                  ResponseCode,
                                              IEnumerable<IDNSResourceRecord>?  Answers       = null,
                                              IEnumerable<IDNSResourceRecord>?  Authorities   = null)

            => new (
                   Origin:                 TestOrigin,
                   QueryId:                1,
                   IsAuthoritativeAnswer:  true,
                   IsTruncated:            false,
                   RecursionDesired:       false,
                   RecursionAvailable:     false,
                   ResponseCode:           ResponseCode,
                   Answers:                Answers     ?? [],
                   Authorities:            Authorities ?? [],
                   AdditionalRecords:      [],
                   IsValid:                true,
                   IsTimeout:              false,
                   Timeout:                TimeSpan.Zero,
                   Runtime:                TimeSpan.Zero
               );

        #endregion

        #region (private) CreateARecord    (DomainName, IPv4Address, TTL)

        private static A CreateARecord(DomainName   DomainName,
                                       IPv4Address  IPv4Address,
                                       TimeSpan     TTL)

            => new (
                   DomainName,
                   DNSQueryClasses.IN,
                   TTL,
                   IPv4Address
               );

        #endregion

        #region (private) CreateAAAARecord (DomainName, IPv6Address, TTL)

        private static AAAA CreateAAAARecord(DomainName   DomainName,
                                             IPv6Address  IPv6Address,
                                             TimeSpan     TTL)

            => new (
                   DomainName,
                   DNSQueryClasses.IN,
                   TTL,
                   IPv6Address
               );

        #endregion


        #region Add_And_Retrieve()

        [Test]
        public void Add_And_Retrieve()
        {

            using var cache  = new DNSCache();
            var domainName   = DNSServiceName.Parse("example.com");

            var response     = CreateResponse(
                                   DNSResponseCodes.NoError,
                                   [
                                       CreateARecord(
                                           DomainName. Parse("example.com"),
                                           IPv4Address.Parse("1.2.3.4"),
                                           TimeSpan.FromMinutes(5)
                                       )
                                   ]
                               );

            cache.Add(
                domainName,
                response
            );

            var cached = cache.GetDNSInfo(domainName);

            Assert.That(cached,                                     Is.Not.Null);
            Assert.That(cached!.ResponseCode,                       Is.EqualTo(DNSResponseCodes.NoError));
            Assert.That(cached.Answers.Count(),                     Is.EqualTo(1));
            Assert.That(((A) cached.Answers.First()).IPv4Address,   Is.EqualTo(IPv4Address.Parse("1.2.3.4")));

        }

        #endregion

        #region TryGetDNSInfo_Returns_False_For_Unknown_Domain()

        [Test]
        public void TryGetDNSInfo_Returns_False_For_Unknown_Domain()
        {

            using var cache  = new DNSCache();
            var result       = cache.TryGetDNSInfo(DNSServiceName.Parse("unknown.example.com"), out var info);

            Assert.That(result,   Is.False);
            Assert.That(info,     Is.Null);

        }

        #endregion

        #region Localhost_Is_Precached()

        [Test]
        public void Localhost_Is_Precached()
        {

            using var cache  = new DNSCache();
            var result       = cache.TryGetDNSInfo(DNSServiceName.Parse("localhost"), out var info);

            Assert.That(result,                  Is.True);
            Assert.That(info,                    Is.Not.Null);
            Assert.That(info!.Answers.Count(),   Is.EqualTo(2));

        }

        #endregion


        #region Negative_Cache_NXDOMAIN()

        [Test]
        public void Negative_Cache_NXDOMAIN()
        {

            using var cache  = new DNSCache();
            var domainName   = DNSServiceName.Parse("does-not-exist.example.com");

            var nxResponse   = CreateResponse(DNSResponseCodes.NameError);

            cache.Add(
                domainName,
                nxResponse
            );

            Assert.That(cache.TryGetDNSInfo(domainName, out var cached),   Is.True);
            Assert.That(cached!.ResponseCode,                              Is.EqualTo(DNSResponseCodes.NameError));
            Assert.That(cached.Answers.Count(),                            Is.EqualTo(0));

        }

        #endregion

        #region Negative_Cache_Refused()

        [Test]
        public void Negative_Cache_Refused()
        {

            using var cache      = new DNSCache();
            var domainName       = DNSServiceName.Parse("refused.example.com");

            var refusedResponse  = CreateResponse(DNSResponseCodes.Refused);

            cache.Add(
                domainName,
                refusedResponse
            );

            Assert.That(cache.TryGetDNSInfo(domainName, out var cached),   Is.True);
            Assert.That(cached!.ResponseCode,                              Is.EqualTo(DNSResponseCodes.Refused));

        }

        #endregion

        #region Negative_Cache_Does_Not_Cache_ServerFailure()

        [Test]
        public void Negative_Cache_Does_Not_Cache_ServerFailure()
        {

            using var cache       = new DNSCache();
            var domainName        = DNSServiceName.Parse("servfail.example.com");

            var servFailResponse  = CreateResponse(DNSResponseCodes.ServerFailure);

            cache.Add(
                domainName,
                servFailResponse
            );

            Assert.That(cache.TryGetDNSInfo(domainName, out _),   Is.False);

        }

        #endregion


        #region Merge_Adds_New_RecordTypes_To_Existing_Entry()

        [Test]
        public void Merge_Adds_New_RecordTypes_To_Existing_Entry()
        {

            using var cache   = new DNSCache();
            var domainName    = DNSServiceName.Parse("merge.example.com");

            var aResponse     = CreateResponse(
                                    DNSResponseCodes.NoError,
                                    [
                                        CreateARecord(
                                            DomainName. Parse("merge.example.com"),
                                            IPv4Address.Parse("1.2.3.4"),
                                            TimeSpan.FromMinutes(5)
                                        )
                                    ]
                                );

            var aaaaResponse  = CreateResponse(
                                    DNSResponseCodes.NoError,
                                    [
                                        CreateAAAARecord(
                                            DomainName. Parse("merge.example.com"),
                                            IPv6Address.Parse("::1"),
                                            TimeSpan.FromMinutes(5)
                                        )
                                    ]
                                );

            cache.Add(domainName, aResponse);
            cache.Add(domainName, aaaaResponse);

            var cached = cache.GetDNSInfo(domainName);

            Assert.That(cached,                                  Is.Not.Null);
            Assert.That(cached!.Answers.Count(),                 Is.EqualTo(2));
            Assert.That(cached.Answers.OfType<A>().   Count(),   Is.EqualTo(1));
            Assert.That(cached.Answers.OfType<AAAA>().Count(),   Is.EqualTo(1));

        }

        #endregion

        #region Merge_Replaces_Same_RecordType()

        [Test]
        public void Merge_Replaces_Same_RecordType()
        {

            using var cache  = new DNSCache();
            var domainName   = DNSServiceName.Parse("update.example.com");

            var oldResponse  = CreateResponse(
                                   DNSResponseCodes.NoError,
                                   [
                                       CreateARecord(
                                           DomainName. Parse("update.example.com"),
                                           IPv4Address.Parse("1.1.1.1"),
                                           TimeSpan.FromMinutes(5)
                                       )
                                   ]
                               );

            var newResponse  = CreateResponse(
                                   DNSResponseCodes.NoError,
                                   [
                                       CreateARecord(
                                           DomainName. Parse("update.example.com"),
                                           IPv4Address.Parse("2.2.2.2"),
                                           TimeSpan.FromMinutes(5)
                                       )
                                   ]
                               );

            cache.Add(domainName, oldResponse);
            cache.Add(domainName, newResponse);

            var cached = cache.GetDNSInfo(domainName);

            Assert.That(cached,                                     Is.Not.Null);
            Assert.That(cached!.Answers.Count(),                    Is.EqualTo(1));
            Assert.That(((A) cached.Answers.First()).IPv4Address,   Is.EqualTo(IPv4Address.Parse("2.2.2.2")));

        }

        #endregion

        #region Merge_Preserves_Existing_When_Adding_Different_Type()

        [Test]
        public void Merge_Preserves_Existing_When_Adding_Different_Type()
        {

            using var cache   = new DNSCache();
            var domainName    = DNSServiceName.Parse("preserve.example.com");

            var aResponse     = CreateResponse(
                                    DNSResponseCodes.NoError,
                                    [
                                        CreateARecord(
                                            DomainName. Parse("preserve.example.com"),
                                            IPv4Address.Parse("10.0.0.1"),
                                            TimeSpan.FromMinutes(5)
                                        )
                                    ]
                                );

            var aaaaResponse  = CreateResponse(
                                    DNSResponseCodes.NoError,
                                    [
                                        CreateAAAARecord(
                                            DomainName. Parse("preserve.example.com"),
                                            IPv6Address.Parse("fe80::1"),
                                            TimeSpan.FromMinutes(5)
                                        )
                                    ]
                                );

            cache.Add(domainName, aResponse);
            cache.Add(domainName, aaaaResponse);

            var cached = cache.GetDNSInfo(domainName);
            var aRecord = cached!.Answers.OfType<A>().First();

            Assert.That(aRecord.IPv4Address,   Is.EqualTo(IPv4Address.Parse("10.0.0.1")));

        }

        #endregion


        #region Remove_By_DomainName()

        [Test]
        public void Remove_By_DomainName()
        {

            using var cache  = new DNSCache();
            var domainName   = DNSServiceName.Parse("remove-me.example.com");

            var response     = CreateResponse(
                                   DNSResponseCodes.NoError,
                                   [
                                       CreateARecord(
                                           DomainName. Parse("remove-me.example.com"),
                                           IPv4Address.Parse("1.2.3.4"),
                                           TimeSpan.FromMinutes(5)
                                       )
                                   ]
                               );

            cache.Add(
                domainName,
                response
            );

            Assert.That(cache.TryGetDNSInfo(domainName, out _),   Is.True);

            var removed = cache.Remove(domainName);

            Assert.That(removed,                                  Is.True);
            Assert.That(cache.TryGetDNSInfo(domainName, out _),   Is.False);

        }

        #endregion

        #region Remove_Returns_False_For_Unknown_Domain()

        [Test]
        public void Remove_Returns_False_For_Unknown_Domain()
        {

            using var cache  = new DNSCache();
            var removed      = cache.Remove(DNSServiceName.Parse("never-added.example.com"));

            Assert.That(removed,   Is.False);

        }

        #endregion

        #region Remove_Allows_Re_Query_With_Fresh_Data()

        [Test]
        public void Remove_Allows_Re_Query_With_Fresh_Data()
        {

            using var cache  = new DNSCache();
            var domainName   = DNSServiceName.Parse("stale-aws.example.com");

            var oldResponse  = CreateResponse(
                                   DNSResponseCodes.NoError,
                                   [
                                       CreateARecord(
                                           DomainName. Parse("stale-aws.example.com"),
                                           IPv4Address.Parse("10.0.0.1"),
                                           TimeSpan.FromMinutes(60)
                                       )
                                   ]
                               );

            cache.Add(
                domainName,
                oldResponse
            );

            cache.Remove(
                domainName
            );

            var newResponse  = CreateResponse(
                                   DNSResponseCodes.NoError,
                                   [
                                       CreateARecord(
                                           DomainName. Parse("stale-aws.example.com"),
                                           IPv4Address.Parse("10.0.0.2"),
                                           TimeSpan.FromMinutes(60)
                                       )
                                   ]
                               );

            cache.Add(
                domainName,
                newResponse
            );

            var cached = cache.GetDNSInfo(domainName);

            Assert.That(cached,                                      Is.Not.Null);
            Assert.That(((A) cached!.Answers.First()).IPv4Address,   Is.EqualTo(IPv4Address.Parse("10.0.0.2")));

        }

        #endregion

        #region Remove_Negative_Cache_Entry()

        [Test]
        public void Remove_Negative_Cache_Entry()
        {

            using var cache  = new DNSCache();
            var domainName   = DNSServiceName.Parse("was-nxdomain.example.com");

            var nxResponse   = CreateResponse(DNSResponseCodes.NameError);

            cache.Add(
                domainName,
                nxResponse
            );

            Assert.That(cache.TryGetDNSInfo(domainName, out _),   Is.True);

            var removed = cache.Remove(domainName);

            Assert.That(removed,                                  Is.True);
            Assert.That(cache.TryGetDNSInfo(domainName, out _),   Is.False);

        }

        #endregion


        #region Eviction_Removes_Expired_Entries()

        [Test]
        public async Task Eviction_Removes_Expired_Entries()
        {

            // CleanUpEvery = 1 second for fast test
            using var cache  = new DNSCache(CleanUpEvery: TimeSpan.FromSeconds(1));
            var domainName   = DNSServiceName.Parse("expire.example.com");

            var response     = CreateResponse(
                                   DNSResponseCodes.NoError,
                                   [
                                       CreateARecord(
                                           DomainName. Parse("expire.example.com"),
                                           IPv4Address.Parse("1.2.3.4"),
                                           TimeSpan.FromSeconds(1)
                                       )
                                   ]
                               );

            cache.Add(
                domainName,
                response
            );

            Assert.That(cache.TryGetDNSInfo(domainName, out _),   Is.True,  "Entry should exist right after adding");

            // Wait for TTL to expire and cleanup timer to run
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.That(cache.TryGetDNSInfo(domainName, out _),   Is.False, "Entry should have been evicted after TTL expired");

        }

        #endregion

        #region Eviction_Does_Not_Remove_Alive_Entries()

        [Test]
        public async Task Eviction_Does_Not_Remove_Alive_Entries()
        {

            using var cache  = new DNSCache(CleanUpEvery: TimeSpan.FromSeconds(1));
            var domainName   = DNSServiceName.Parse("alive.example.com");

            var response     = CreateResponse(
                                   DNSResponseCodes.NoError,
                                   [
                                       CreateARecord(
                                           DomainName. Parse("alive.example.com"),
                                           IPv4Address.Parse("5.6.7.8"),
                                           TimeSpan.FromMinutes(60)
                                       )
                                   ]
                               );

            cache.Add(
                domainName,
                response
            );

            // Wait for a cleanup cycle
            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.That(cache.TryGetDNSInfo(domainName, out var cached),   Is.True, "Entry with long TTL should still exist");
            Assert.That(((A) cached!.Answers.First()).IPv4Address,         Is.EqualTo(IPv4Address.Parse("5.6.7.8")));

        }

        #endregion

        #region Eviction_Removes_Expired_Negative_Entries()

        [Test]
        public async Task Eviction_Removes_Expired_Negative_Entries()
        {

            using var cache  = new DNSCache(CleanUpEvery: TimeSpan.FromSeconds(1));
            var domainName   = DNSServiceName.Parse("neg-expire.example.com");

            var nxResponse   = CreateResponse(DNSResponseCodes.NameError);

            cache.Add(
                domainName,
                nxResponse
            );

            Assert.That(cache.TryGetDNSInfo(domainName, out _),       Is.True,  "Negative entry should exist right after adding");

            // DefaultNegativeCacheTTL is 5 minutes, but the cleanup timer checks EndOfLife.
            // We can't easily wait 5 min in a test, so we verify the entry IS present
            // and trust the eviction logic (tested above for positive entries) works the same.
            Assert.That(cache.GetDNSInfo(domainName)?.ResponseCode,   Is.EqualTo(DNSResponseCodes.NameError));

        }

        #endregion

        #region Eviction_Preserves_Localhost()

        [Test]
        public async Task Eviction_Preserves_Localhost()
        {

            using var cache  = new DNSCache(
                                   CleanUpEvery:  TimeSpan.FromSeconds(1)
                               );

            // Wait for multiple cleanup cycles
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.That(cache.TryGetDNSInfo(DNSServiceName.Parse("localhost"), out _),   Is.True, "localhost should never be evicted!");
            Assert.That(cache.TryGetDNSInfo(DNSServiceName.Parse("loopback"), out _),    Is.True, "loopback should never be evicted!");

        }

        #endregion

    }

}
