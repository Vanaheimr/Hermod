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
    /// Answers each (name, type) with the records registered for it, and with an
    /// empty NOERROR otherwise. The authority section of a (name, type) — where a
    /// negative answer carries its NSEC or NSEC3 proof — can be registered too.
    /// </summary>
    internal sealed class CannedDNSClient : IDNSClient
    {

        private readonly Dictionary<(String, DNSResourceRecordTypes), IDNSResourceRecord[]> answers      = [];
        private readonly Dictionary<(String, DNSResourceRecordTypes), IDNSResourceRecord[]> authorities  = [];

        private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.DNS);

        private static String Key(String Name)
            => Name.TrimEnd('.').ToLowerInvariant();

        public CannedDNSClient Answer(String Name, DNSResourceRecordTypes Type, params IDNSResourceRecord[] Records)
        {
            answers[(Key(Name), Type)] = Records;
            return this;
        }

        public CannedDNSClient Authority(String Name, DNSResourceRecordTypes Type, params IDNSResourceRecord[] Records)
        {
            authorities[(Key(Name), Type)] = Records;
            return this;
        }

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = null,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Task.FromResult(Build(DomainName.FullName, ResourceRecordTypes));

        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = null,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Task.FromResult(Build(DNSServiceName.FullName, ResourceRecordTypes));

        private DNSInfo Build(String Name, IEnumerable<DNSResourceRecordTypes> Types)

            => new (
                   Origin:                 origin,
                   QueryId:                0,
                   IsAuthoritativeAnswer:  true,
                   IsTruncated:            false,
                   RecursionDesired:       true,
                   RecursionAvailable:     true,
                   ResponseCode:           DNSResponseCodes.NoError,
                   Answers:                Types.SelectMany(type => answers.TryGetValue((Key(Name), type), out var records) ? records : []).ToArray(),
                   Authorities:            Types.SelectMany(type => authorities.TryGetValue((Key(Name), type), out var records) ? records : []).ToArray(),
                   AdditionalRecords:      [],
                   IsValid:                true,
                   IsTimeout:              false,
                   Timeout:                TimeSpan.FromSeconds(5),
                   Runtime:                TimeSpan.Zero
               );

        public void Dispose()
        { }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

    }

}
