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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A small, deterministic DNS zone store useful for tests and simple authoritative deployments.
    /// </summary>
    public sealed class InMemoryDNSZone : IDNSZoneStore
    {

        private readonly ConcurrentDictionary<DNSServiceName, List<IDNSResourceRecord>> records = [];


        public InMemoryDNSZone Add(params IDNSResourceRecord[] ResourceRecords)
            => Add((IEnumerable<IDNSResourceRecord>) ResourceRecords);


        public InMemoryDNSZone Add(IEnumerable<IDNSResourceRecord> ResourceRecords)
        {

            foreach (var resourceRecord in ResourceRecords)
            {

                records.AddOrUpdate(
                    resourceRecord.DomainName,
                    _ => [ resourceRecord ],
                    (_, existingRecords) => {

                        lock (existingRecords)
                        {
                            existingRecords.Add(resourceRecord);
                            return existingRecords;
                        }

                    }
                );

            }

            return this;

        }


        public InMemoryDNSZone Set(params IDNSResourceRecord[] ResourceRecords)
            => Set((IEnumerable<IDNSResourceRecord>) ResourceRecords);


        public InMemoryDNSZone Set(IEnumerable<IDNSResourceRecord> ResourceRecords)
        {

            foreach (var resourceRecordGroup in ResourceRecords.GroupBy(resourceRecord => resourceRecord.DomainName))
            {

                var replacementRecords = resourceRecordGroup.ToArray();
                var replacementKeys    = replacementRecords.
                                             Select(resourceRecord => (resourceRecord.Type, resourceRecord.Class)).
                                             ToHashSet();

                records.AddOrUpdate(
                    resourceRecordGroup.Key,
                    _ => [.. replacementRecords],
                    (_, existingRecords) => {

                        lock (existingRecords)
                        {
                            existingRecords.RemoveAll(resourceRecord => replacementKeys.Contains((resourceRecord.Type, resourceRecord.Class)));
                            existingRecords.AddRange(replacementRecords);
                            return existingRecords;
                        }

                    }
                );

            }

            return this;

        }


        public InMemoryDNSZone Remove(DNSServiceName          DomainName,
                                      DNSResourceRecordTypes? ResourceRecordType = null,
                                      DNSQueryClasses?        QueryClass         = null)
        {

            if (!records.TryGetValue(DomainName, out var existingRecords))
                return this;

            lock (existingRecords)
            {

                existingRecords.RemoveAll(resourceRecord =>
                    (!ResourceRecordType.HasValue || resourceRecord.Type  == ResourceRecordType.Value) &&
                    (!QueryClass.        HasValue || resourceRecord.Class == QueryClass.        Value)
                );

                if (existingRecords.Count == 0)
                    records.TryRemove(DomainName, out _);

            }

            return this;

        }


        public InMemoryDNSZone AddZoneFileString(String     ZoneFileString,
                                                 TimeSpan?  DefaultTimeToLive = null)
        {

            Add(ADNSResourceRecord.ParseZoneFileString(
                    ZoneFileString,
                    DefaultTimeToLive
                ));

            return this;

        }


        public Task<DNSZoneLookupResult> Lookup(DNSQuestion       Question,
                                                CancellationToken  CancellationToken = default)
        {

            CancellationToken.ThrowIfCancellationRequested();

            if (!records.TryGetValue(Question.DomainName, out var nameRecords))
                return Task.FromResult(DNSZoneLookupResult.NameError());

            IDNSResourceRecord[] snapshot;

            lock (nameRecords)
                snapshot = [.. nameRecords];

            var answers = snapshot.
                              Where(resourceRecord =>
                                  (Question.QueryClass == DNSQueryClasses.ANY ||
                                   resourceRecord.Class  == Question.QueryClass) &&
                                  (Question.QueryType  == DNSResourceRecordTypes.Any ||
                                   resourceRecord.Type == Question.QueryType)).
                              ToArray();

            return Task.FromResult(
                answers.Length > 0
                    ? DNSZoneLookupResult.Found(answers)
                    : DNSZoneLookupResult.NoData()
            );

        }


        public static InMemoryDNSZone CreateDemoZone()
        {

            var zone = new InMemoryDNSZone();

            zone.Add(
                new A(
                    DomainName.Parse("api1.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    IPv4Address.Parse("141.24.12.2")
                ),
                new AAAA(
                    DomainName.Parse("api2.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    IPv6Address.Parse("::2")
                ),
                new SRV(
                    DNSServiceName.Parse("_ocpp._tls.api2.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    10,
                    20,
                    IPPort.Parse(443),
                    DomainName.Parse("api2.example.org.")
                ),
                new SSHFP(
                    DomainName.Parse("api2.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    SSHFP_Algorithm.ECDSA,
                    SSHFP_FingerprintType.SHA256,
                    "0095d7637f456888505741e952a1e7ff635e018f9a95c9b3b38af4bb9fdb0c36".FromHEX()
                ),
                new TXT(
                    DomainName.Parse("api2.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    "Hello world!"
                )
            );

            return zone;

        }

    }

}
