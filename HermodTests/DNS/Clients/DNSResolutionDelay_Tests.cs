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

using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// The addresses to connect to, with the Resolution Delay of RFC 8305 §3: once one family has answered with
    /// addresses, the other is waited for 50 ms and no longer; a negative answer is no reason to stop waiting.
    /// </summary>
    /// <remarks>
    /// Against a resolver whose answers come when the test says, for each record type on its own, or never.
    /// </remarks>
    [TestFixture]
    public class DNSResolutionDelay_Tests
    {

        #region Data

        private static readonly DomainName   name   = DomainName.Parse("happy.example");
        private static readonly IPv4Address  ipv4   = IPv4Address.Parse("192.0.2.1");
        private static readonly IPv6Address  ipv6   = IPv6Address.Parse("2001:db8::1");

        /// <summary>
        /// The IPv6 address as it is written.
        /// </summary>
        private static readonly String       V6     = ipv6.ToString();

        /// <summary>
        /// An answer that never comes.
        /// </summary>
        private static readonly TimeSpan     never  = System.Threading.Timeout.InfiniteTimeSpan;

        #endregion

        #region (class) ScriptedResolver

        /// <summary>
        /// A resolver that answers each record type after the given time, with the given address or with none,
        /// or never - until it is cancelled, which it records.
        /// </summary>
        private sealed class ScriptedResolver(TimeSpan      IPv6After,
                                              IPv6Address?  IPv6,
                                              TimeSpan      IPv4After,
                                              IPv4Address?  IPv4) : IDNSClient
        {

            public Int32 Cancelled;

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

                var type  = ResourceRecordTypes.Single();
                var after = type == DNSResourceRecordTypes.AAAA ? IPv6After : IPv4After;

                try
                {
                    await Task.Delay(after, CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref Cancelled);
                    throw;
                }

                IDNSResourceRecord? answer = type == DNSResourceRecordTypes.AAAA
                                                 ? IPv6 is null ? null : new AAAA(name, DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv6.Value)
                                                 : IPv4 is null ? null : new A   (name, DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv4.Value);

                return new DNSInfo(Origin:                 new DNSServerConfig(IPv4Address.Localhost, IPPort.DNS),
                                   QueryId:                1,
                                   IsAuthoritativeAnswer:  false,
                                   IsTruncated:            false,
                                   RecursionDesired:       true,
                                   RecursionAvailable:     true,
                                   ResponseCode:           DNSResponseCodes.NoError,
                                   Answers:                answer is null ? [] : [ answer ],
                                   Authorities:            [],
                                   AdditionalRecords:      [],
                                   IsValid:                true,
                                   IsTimeout:              false,
                                   Timeout:                Timeout ?? TimeSpan.FromSeconds(10),
                                   Runtime:                after);

            }

            public override String ToString()
                => nameof(ScriptedResolver);

            public void Dispose()
            { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        #endregion

        #region (static) Resolve(Resolver, ResolutionDelay = null)

        private static async Task<(String[] Addresses, TimeSpan Elapsed)> Resolve(IDNSClient  Resolver,
                                                                                  TimeSpan?   ResolutionDelay   = null)
        {

            var stopwatch  = Stopwatch.StartNew();
            var addresses  = await Resolver.Query_IPAddressesToConnect(name, ResolutionDelay: ResolutionDelay);

            return ([ .. addresses.Select(address => address.ToString()) ], stopwatch.Elapsed);

        }

        #endregion


        #region AnIPv4AnswerWaitsTheResolutionDelayForIPv6AndNoLonger()

        /// <summary>
        /// The A answer first, and no AAAA answer ever: the IPv4 address after the 50 ms of the Resolution Delay,
        /// not after the timeout of the AAAA query, which is cancelled.
        /// </summary>
        [Test]
        public async Task AnIPv4AnswerWaitsTheResolutionDelayForIPv6AndNoLonger()
        {

            var resolver = new ScriptedResolver(never, ipv6, TimeSpan.Zero, ipv4);

            var (addresses, elapsed) = await Resolve(resolver);

            Assert.That(addresses,           Is.EqualTo(new[] { "192.0.2.1" }));
            Assert.That(elapsed,             Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(40)), "the Resolution Delay is waited for");
            Assert.That(elapsed,             Is.LessThan(TimeSpan.FromSeconds(1)));
            Assert.That(resolver.Cancelled,  Is.EqualTo(1), "the AAAA query still out is cancelled");

        }

        #endregion

        #region AnIPv6AnswerWithinTheDelayComesFirst()

        /// <summary>
        /// The A answer first, the AAAA answer within the Resolution Delay: both, IPv6 first.
        /// </summary>
        [Test]
        public async Task AnIPv6AnswerWithinTheDelayComesFirst()
        {

            var (addresses, _) = await Resolve(new ScriptedResolver(TimeSpan.FromMilliseconds(10), ipv6, TimeSpan.Zero, ipv4),
                                               TimeSpan.FromMilliseconds(500));

            Assert.That(addresses, Is.EqualTo(new String[] { V6, "192.0.2.1" }));

        }

        #endregion

        #region AnIPv6AnswerFirstWaitsTheDelayForIPv4ToFallBackOn()

        /// <summary>
        /// The AAAA answer first: the A answer is waited for the Resolution Delay as well, so that IPv4 is there
        /// to fall back on - and no longer, when it does not come.
        /// </summary>
        [Test]
        public async Task AnIPv6AnswerFirstWaitsTheDelayForIPv4ToFallBackOn()
        {

            var (both,      _)        = await Resolve(new ScriptedResolver(TimeSpan.Zero, ipv6, TimeSpan.FromMilliseconds(10), ipv4),
                                                      TimeSpan.FromMilliseconds(500));

            var (ipv6Only,  elapsed)  = await Resolve(new ScriptedResolver(TimeSpan.Zero, ipv6, never, ipv4));

            Assert.That(both,      Is.EqualTo(new String[] { V6, "192.0.2.1" }));
            Assert.That(ipv6Only,  Is.EqualTo(new String[] { V6 }));
            Assert.That(elapsed,   Is.LessThan(TimeSpan.FromSeconds(1)));

        }

        #endregion

        #region ANegativeAnswerIsNoReasonToStopWaiting()

        /// <summary>
        /// An AAAA answer without addresses first: the A answer is waited for as long as it takes, here 300 ms,
        /// six times the Resolution Delay.
        /// </summary>
        [Test]
        public async Task ANegativeAnswerIsNoReasonToStopWaiting()
        {

            var (addresses, elapsed) = await Resolve(new ScriptedResolver(TimeSpan.Zero, null, TimeSpan.FromMilliseconds(300), ipv4));

            Assert.That(addresses, Is.EqualTo(new[] { "192.0.2.1" }));
            Assert.That(elapsed,   Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250)));

        }

        #endregion

        #region NoAnswerAtAllIsNoAddress()

        /// <summary>
        /// Neither family with addresses: none, once both have answered.
        /// </summary>
        [Test]
        public async Task NoAnswerAtAllIsNoAddress()
        {

            var (addresses, _) = await Resolve(new ScriptedResolver(TimeSpan.Zero, null, TimeSpan.FromMilliseconds(20), null));

            Assert.That(addresses, Is.Empty);

        }

        #endregion

        #region AResolutionDelayOfItsOwn()

        /// <summary>
        /// A Resolution Delay given with the call is the one waited for.
        /// </summary>
        [Test]
        public async Task AResolutionDelayOfItsOwn()
        {

            var resolver               = new ScriptedResolver(TimeSpan.FromMilliseconds(150), ipv6, TimeSpan.Zero, ipv4);

            var (shortDelay, _)        = await Resolve(resolver, TimeSpan.FromMilliseconds(20));
            var (longDelay,  _)        = await Resolve(resolver, TimeSpan.FromMilliseconds(600));

            Assert.That(shortDelay,    Is.EqualTo(new[] { "192.0.2.1" }));
            Assert.That(longDelay,     Is.EqualTo(new String[] { V6, "192.0.2.1" }));
            Assert.That(IDNSTCPClientExtensions.DefaultResolutionDelay, Is.EqualTo(TimeSpan.FromMilliseconds(50)));

        }

        #endregion

        #region AllTheAddressesOfANameAreStillWaitedFor()

        /// <summary>
        /// Query_IPAddresses, for all the addresses of a name, still waits for both answers.
        /// </summary>
        [Test]
        public async Task AllTheAddressesOfANameAreStillWaitedFor()
        {

            var addresses = await new ScriptedResolver(TimeSpan.FromMilliseconds(200), ipv6, TimeSpan.Zero, ipv4).Query_IPAddresses(name);

            Assert.That(addresses.Select(address => address.ToString()), Is.EquivalentTo(new String[] { V6, "192.0.2.1" }));

        }

        #endregion

        #region TheCallerCancels() / AnAddressNeedsNoQuery()

        [Test]
        public void TheCallerCancels()
        {

            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            Assert.That(async () => await new ScriptedResolver(never, ipv6, never, ipv4).Query_IPAddressesToConnect(name, CancellationToken: cancellation.Token),
                        Throws.InstanceOf<OperationCanceledException>());

        }

        [Test]
        public async Task AnAddressNeedsNoQuery()
        {

            var resolver  = new ScriptedResolver(never, ipv6, never, ipv4);
            var addresses = await resolver.Query_IPAddressesToConnect(URL.Parse("https://[2001:db8::7]:4460/"));

            Assert.That(addresses.Select(address => address.ToString()), Is.EqualTo(new[] { IPv6Address.Parse("2001:db8::7").ToString() }));

        }

        #endregion

    }

}
