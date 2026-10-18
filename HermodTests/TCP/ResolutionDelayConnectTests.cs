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

using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// ATCPClient resolves a name with the Resolution Delay of RFC 8305 §3: once the A answer is there, an AAAA
    /// answer that does not come is waited for 50 ms, and not for the timeout of the DNS query.
    /// </summary>
    /// <remarks>
    /// ATCPClient - the TCP connections of the HTTP clients, among them the OCPI ones - asked for A and AAAA and
    /// waited for both: a name server dropping the AAAA query kept every connection to a name for the whole DNS
    /// timeout, the IPv4 address long known. Against a resolver that answers A at once and AAAA never, and a
    /// listener on 127.0.0.1.
    /// </remarks>
    [TestFixture]
    public class ResolutionDelayConnectTests
    {

        #region (class) AOnlyResolver

        /// <summary>
        /// A resolver that answers A at once with 127.0.0.1, and AAAA never - until it is cancelled.
        /// </summary>
        private sealed class AOnlyResolver : IDNSClient
        {

            public ConcurrentQueue<DNSResourceRecordTypes> Asked { get; } = new ();

            public Task<DNSInfo> Query(DomainName                           DomainName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = null,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)

                => Query(DNSServiceName.Parse(DomainName.FullName), ResourceRecordTypes, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

            public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                             IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                             TimeSpan?                            Timeout             = null,
                                             Boolean?                             RecursionDesired    = null,
                                             Boolean?                             ForceUpdate         = false,
                                             CancellationToken                    CancellationToken   = default)
            {

                var type = ResourceRecordTypes.Single();

                Asked.Enqueue(type);

                if (type == DNSResourceRecordTypes.AAAA)
                    await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, CancellationToken);

                var name = DomainName.Parse(DNSServiceName.ToString());

                return new DNSInfo(Origin:                 new DNSServerConfig(IPv4Address.Localhost, IPPort.DNS),
                                   QueryId:                1,
                                   IsAuthoritativeAnswer:  false,
                                   IsTruncated:            false,
                                   RecursionDesired:       true,
                                   RecursionAvailable:     true,
                                   ResponseCode:           DNSResponseCodes.NoError,
                                   Answers:                [ new A(name, DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv4Address.Localhost) ],
                                   Authorities:            [],
                                   AdditionalRecords:      [],
                                   IsValid:                true,
                                   IsTimeout:              false,
                                   Timeout:                Timeout ?? TimeSpan.FromSeconds(10),
                                   Runtime:                TimeSpan.Zero);

            }

            public override String ToString()
                => nameof(AOnlyResolver);

            public void Dispose()
            { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        #endregion


        #region AConnectionDoesNotWaitForAnAAAAAnswerOnceItHasTheAAnswer()

        /// <summary>
        /// The A answer at once, no AAAA answer ever: connected to 127.0.0.1 after the Resolution Delay.
        /// </summary>
        [Test]
        public async Task AConnectionDoesNotWaitForAnAAAAAnswerOnceItHasTheAAnswer()
        {

            var listener   = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            try
            {

                var port       = ((IPEndPoint) listener.LocalEndpoint).Port;
                var accepted   = listener.AcceptTcpClientAsync();
                var resolver   = new AOnlyResolver();

                using var client = new TCPClient(
                                       URL.Parse($"tcp://happy-eyeballs.test:{port}"),
                                       ConnectTimeout:  TimeSpan.FromSeconds(5),
                                       DNSClient:       resolver
                                   );

                var stopwatch  = Stopwatch.StartNew();
                var result     = await client.ConnectAsync();
                stopwatch.Stop();

                using var server = await accepted.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Multiple(() => {
                    Assert.That(result.IsSuccess,                        Is.True, String.Join(" | ", result.Errors.Select(error => error.ToString())));
                    Assert.That(client.ResolvedIPAddress?.ToString(),    Is.EqualTo("127.0.0.1"));
                    Assert.That(stopwatch.Elapsed,                       Is.LessThan(TimeSpan.FromSeconds(2)), "not the timeout of the AAAA query");
                    Assert.That(resolver.Asked,                          Does.Contain(DNSResourceRecordTypes.AAAA), "IPv6 is asked for, and preferred, by default");
                });

            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion

        #region IPv4OnlyAsksForIPv4Alone()

        /// <summary>
        /// IPv4Only asks for A alone: there is no AAAA answer to wait for, not even the Resolution Delay.
        /// </summary>
        [Test]
        public async Task IPv4OnlyAsksForIPv4Alone()
        {

            var listener   = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            try
            {

                var port       = ((IPEndPoint) listener.LocalEndpoint).Port;
                var accepted   = listener.AcceptTcpClientAsync();
                var resolver   = new AOnlyResolver();

                using var client = new TCPClient(
                                       URL.Parse($"tcp://ipv4-only.test:{port}"),
                                       PreferIPv4:      IPVersionPreference.IPv4Only,
                                       ConnectTimeout:  TimeSpan.FromSeconds(5),
                                       DNSClient:       resolver
                                   );

                var result     = await client.ConnectAsync();

                using var server = await accepted.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Multiple(() => {
                    Assert.That(result.IsSuccess,                        Is.True, String.Join(" | ", result.Errors.Select(error => error.ToString())));
                    Assert.That(client.ResolvedIPAddress?.ToString(),    Is.EqualTo("127.0.0.1"));
                    Assert.That(resolver.Asked,                          Is.EqualTo(new[] { DNSResourceRecordTypes.A }));
                });

            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion

    }

}
