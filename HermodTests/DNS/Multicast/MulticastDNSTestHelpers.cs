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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Multicast
{

    #region CapturedDatagram

    /// <summary>
    /// A datagram captured on an in-memory Multicast DNS network, already parsed.
    /// </summary>
    /// <param name="Timestamp">The time of sending.</param>
    /// <param name="Sender">The sending transport.</param>
    /// <param name="Destination">The unicast destination, or null for a multicast.</param>
    /// <param name="Message">The parsed message.</param>
    internal sealed record CapturedDatagram(DateTimeOffset                 Timestamp,
                                            InMemoryMulticastDNSTransport  Sender,
                                            IPSocket?                      Destination,
                                            MulticastDNSMessage            Message)
    {

        /// <summary>
        /// Whether the datagram was multicast (no unicast destination).
        /// </summary>
        public Boolean IsMulticast
            => Destination is null;

        /// <summary>
        /// The answer records without their Multicast DNS bits.
        /// </summary>
        public IEnumerable<IDNSResourceRecord> AnswerRecords
            => Message.Answers.Select(answer => answer.Record);

        /// <summary>
        /// The additional records without their Multicast DNS bits.
        /// </summary>
        public IEnumerable<IDNSResourceRecord> AdditionalRecords
            => Message.Additionals.Select(additional => additional.Record);

    }

    #endregion

    #region PacketCapture

    /// <summary>
    /// Captures every datagram sent within an in-memory Multicast DNS network.
    /// The network delivers synchronously, so a datagram sent by an awaited call
    /// is captured before that call returns.
    /// </summary>
    internal sealed class PacketCapture
    {

        #region Data

        private readonly List<CapturedDatagram>  datagrams    = [];
        private readonly List<String>            parseErrors  = [];

        #endregion

        #region Properties

        /// <summary>
        /// All captured datagrams in the order of sending.
        /// </summary>
        public IReadOnlyList<CapturedDatagram>  All
        {
            get
            {
                lock (datagrams)
                    return [.. datagrams];
            }
        }

        /// <summary>
        /// The captured queries.
        /// </summary>
        public IReadOnlyList<CapturedDatagram>  Queries
            => [.. All.Where(datagram => datagram.Message.IsQuery)];

        /// <summary>
        /// The captured responses (announcements, answers and goodbyes).
        /// </summary>
        public IReadOnlyList<CapturedDatagram>  Responses
            => [.. All.Where(datagram => datagram.Message.IsResponse)];

        /// <summary>
        /// The errors of datagrams that could not be parsed (expected to stay empty).
        /// </summary>
        public IReadOnlyList<String>            ParseErrors
        {
            get
            {
                lock (datagrams)
                    return [.. parseErrors];
            }
        }

        /// <summary>
        /// The number of captured datagrams.
        /// </summary>
        public Int32                            Count
        {
            get
            {
                lock (datagrams)
                    return datagrams.Count;
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Attach a capture to the given network.
        /// </summary>
        /// <param name="Network">An in-memory Multicast DNS network.</param>
        public PacketCapture(InMemoryMulticastDNSNetwork Network)
        {
            Network.OnDatagramSent += OnDatagramSent;
        }

        #endregion


        #region (private) OnDatagramSent(Timestamp, Sender, Payload, Destination)

        private Task OnDatagramSent(DateTimeOffset                 Timestamp,
                                    InMemoryMulticastDNSTransport  Sender,
                                    ReadOnlyMemory<Byte>           Payload,
                                    IPSocket?                      Destination)
        {

            if (MulticastDNSMessage.TryParse(Payload, out var message, out var error))
            {
                lock (datagrams)
                    datagrams.Add(new CapturedDatagram(Timestamp, Sender, Destination, message));
            }
            else
            {
                lock (datagrams)
                    parseErrors.Add(error);
            }

            return Task.CompletedTask;

        }

        #endregion

        #region PTRQueriesFor(ServiceType)

        /// <summary>
        /// The captured queries asking for the PTR records of the given service type.
        /// </summary>
        /// <param name="ServiceType">A DNS-SD service type.</param>
        public IReadOnlyList<CapturedDatagram> PTRQueriesFor(DNSServiceName ServiceType)

            => [.. Queries.Where(query => query.Message.Questions.Any(question => question.Type == DNSResourceRecordTypes.PTR &&
                                                                                    question.Name.Equals(ServiceType)))];

        #endregion

        #region Clear()

        /// <summary>
        /// Forget every captured datagram.
        /// </summary>
        public void Clear()
        {
            lock (datagrams)
            {
                datagrams.  Clear();
                parseErrors.Clear();
            }
        }

        #endregion

    }

    #endregion

    #region ShiftableTimeProvider

    /// <summary>
    /// A time provider whose clock can be moved forward at will, while its timers
    /// stay real ones: it lets a test age a cache entry beyond the one-second rules
    /// of RFC 6762 §7.1 and §10.2 without waiting for that second to pass.
    /// </summary>
    internal sealed class ShiftableTimeProvider : TimeProvider
    {

        private Int64 offsetTicks;

        /// <summary>
        /// The current offset of the clock.
        /// </summary>
        public TimeSpan Offset
            => TimeSpan.FromTicks(Interlocked.Read(ref offsetTicks));

        /// <summary>
        /// Move the clock forward by the given time span.
        /// </summary>
        /// <param name="By">A time span.</param>
        public void Shift(TimeSpan By)
            => Interlocked.Add(ref offsetTicks, By.Ticks);

        /// <summary>
        /// The (shifted) current time.
        /// </summary>
        public override DateTimeOffset GetUtcNow()
            => base.GetUtcNow() + Offset;

    }

    #endregion


    /// <summary>
    /// Shared helpers of the Multicast DNS tests: fast option sets, a DNS-SD record set,
    /// well-known names and polling helpers.
    /// </summary>
    internal static class MulticastDNSTestHelpers
    {

        #region Names

        /// <summary>
        /// The host name "myhost.local." as domain name.
        /// </summary>
        public static readonly DomainName      MyHost       = DomainName.    Parse("myhost.local.");

        /// <summary>
        /// The host name "myhost.local." as DNS service name (the owner name type of resource records).
        /// </summary>
        public static readonly DNSServiceName  MyHostName   = DNSServiceName.Parse("myhost.local.");

        /// <summary>
        /// The service type "_test._tcp.local.".
        /// </summary>
        public static readonly DNSServiceName  TestService  = DNSServiceName.Parse("_test._tcp.local.");

        /// <summary>
        /// The service instance name "myhost._test._tcp.local.".
        /// </summary>
        public static readonly DNSServiceName  MyInstance   = DNSServiceName.Parse("myhost._test._tcp.local.");

        #endregion

        #region FastResponderOptions(MinRecordMulticastInterval = null, ProbeInterval = null, SendNegativeResponses = true)

        /// <summary>
        /// Responder options with intervals of a few milliseconds.
        /// </summary>
        /// <param name="MinRecordMulticastInterval">An optional per-record multicast rate limit (default: the RFC value of one second).</param>
        /// <param name="ProbeInterval">An optional probe interval (default: 10 ms).</param>
        /// <param name="SendNegativeResponses">Whether NSEC records are sent for missing types (default: true).</param>
        public static MulticastDNSResponderOptions FastResponderOptions(TimeSpan?  MinRecordMulticastInterval   = null,
                                                                        TimeSpan?  ProbeInterval                = null,
                                                                        Boolean    SendNegativeResponses        = true)

            => new () {
                   ProbeInterval               = ProbeInterval ?? TimeSpan.FromMilliseconds(10),
                   MaxInitialProbeDelay        = TimeSpan.Zero,
                   AnnouncementInterval        = TimeSpan.FromMilliseconds(10),
                   MinSharedResponseDelay      = TimeSpan.Zero,
                   MaxSharedResponseDelay      = TimeSpan.FromMilliseconds(1),
                   MinTruncatedQueryDelay      = TimeSpan.FromMilliseconds(1),
                   MaxTruncatedQueryDelay      = TimeSpan.FromMilliseconds(2),
                   MinRecordMulticastInterval  = MinRecordMulticastInterval ?? MulticastDNS.MinRecordMulticastInterval,
                   SendNegativeResponses       = SendNegativeResponses
               };

        #endregion

        #region FastClientOptions(RequestUnicastResponses = true, IncludeLinkLocalIPv6 = false, InitialBrowseInterval = null)

        /// <summary>
        /// Client options with timeouts and intervals of a few milliseconds.
        /// </summary>
        /// <param name="RequestUnicastResponses">Whether one-shot queries set the QU bit (default: true).</param>
        /// <param name="IncludeLinkLocalIPv6">Whether link-local IPv6 addresses are returned (default: false).</param>
        /// <param name="InitialBrowseInterval">An optional initial interval of the browser query loop (default: 50 ms).</param>
        public static MulticastDNSClientOptions FastClientOptions(Boolean    RequestUnicastResponses   = true,
                                                                  Boolean    IncludeLinkLocalIPv6      = false,
                                                                  TimeSpan?  InitialBrowseInterval     = null)

            => new () {
                   QueryTimeout               = TimeSpan.FromMilliseconds(500),
                   ResponseGracePeriod        = TimeSpan.FromMilliseconds(20),
                   RetransmissionInterval     = TimeSpan.FromMilliseconds(100),
                   CacheSweepInterval         = TimeSpan.FromMilliseconds(50),
                   GoodbyeDelay               = TimeSpan.Zero,
                   BrowseMaintenanceInterval  = TimeSpan.FromMilliseconds(50),
                   InitialBrowseInterval      = InitialBrowseInterval ?? TimeSpan.FromMilliseconds(50),
                   RequestUnicastResponses    = RequestUnicastResponses,
                   IncludeLinkLocalIPv6       = IncludeLinkLocalIPv6
               };

        #endregion

        #region ServiceRecords(Host, Address, Port, ServiceType = "_test._tcp.local.", Instance = null, TXTStrings = null, PTRTimeToLive = null)

        /// <summary>
        /// The DNS-SD record set of a service instance: A, SRV, TXT and the service type PTR.
        /// </summary>
        /// <param name="Host">The host name, e.g. "myhost.local.".</param>
        /// <param name="Address">The IPv4 address of the host.</param>
        /// <param name="Port">The port of the service.</param>
        /// <param name="ServiceType">The service type (default: "_test._tcp.local.").</param>
        /// <param name="Instance">The instance label (default: the first label of the host name).</param>
        /// <param name="TXTStrings">The TXT character-strings (default: "txtver=1" and an URL).</param>
        /// <param name="PTRTimeToLive">The time-to-live of the PTR record (default: 4500 s).</param>
        public static IDNSResourceRecord[] ServiceRecords(String                Host,
                                                          String                Address,
                                                          UInt16                Port,
                                                          String                ServiceType     = "_test._tcp.local.",
                                                          String?               Instance        = null,
                                                          IEnumerable<String>?  TXTStrings      = null,
                                                          TimeSpan?             PTRTimeToLive   = null)
        {

            var hostName      = DomainName.    Parse(Host);
            var serviceName   = DNSServiceName.Parse(ServiceType);
            var instanceName  = DNSServiceName.Parse($"{Instance ?? Host.Split('.')[0]}.{ServiceType}");

            return [
                new A  (hostName,     DNSQueryClasses.IN, TimeSpan.FromSeconds(120),                     IPv4Address.Parse(Address)),
                new SRV(instanceName, DNSQueryClasses.IN, TimeSpan.FromSeconds(120),                     0, 0, IPPort.Parse(Port), hostName),
                new TXT(instanceName, DNSQueryClasses.IN, TimeSpan.FromSeconds(4500),                    TXTStrings ?? new[] { "txtver=1", $"url=https://{Host}:{Port}/" }),
                new PTR(serviceName,  DNSQueryClasses.IN, PTRTimeToLive ?? TimeSpan.FromSeconds(4500),   instanceName)
            ];

        }

        #endregion

        #region PeerSocket(Address, Port = null)

        /// <summary>
        /// The socket of another Multicast DNS host on the link, speaking from the
        /// Multicast DNS port unless another port is given (a legacy resolver).
        /// </summary>
        /// <param name="Address">The IPv4 address of the peer.</param>
        /// <param name="Port">An optional source port (default: 5353).</param>
        public static IPSocket PeerSocket(String  Address,
                                          IPPort? Port   = null)

            => new (IPv4Address.Parse(Address),
                    Port ?? MulticastDNS.Port);

        #endregion


        #region WaitFor(Probe, Timeout = null)

        /// <summary>
        /// Poll the given probe every 10 ms until it returns a value (default timeout: 5 s).
        /// </summary>
        /// <param name="Probe">A probe returning the awaited value, or null.</param>
        /// <param name="Timeout">An optional timeout.</param>
        public static async Task<T> WaitFor<T>(Func<T?>   Probe,
                                               TimeSpan?  Timeout   = null)

            where T : class

        {

            var deadline = DateTimeOffset.UtcNow + (Timeout ?? TimeSpan.FromSeconds(5));

            while (DateTimeOffset.UtcNow < deadline)
            {
                var result = Probe();
                if (result is not null)
                    return result;
                await Task.Delay(10);
            }

            throw new TimeoutException("The condition was not met in time!");

        }

        #endregion

        #region WaitUntil(Probe, Timeout = null)

        /// <summary>
        /// Poll the given probe every 10 ms until it returns true (default timeout: 5 s).
        /// </summary>
        /// <param name="Probe">A probe of the awaited condition.</param>
        /// <param name="Timeout">An optional timeout.</param>
        public static async Task WaitUntil(Func<Boolean>  Probe,
                                           TimeSpan?      Timeout   = null)
        {

            var deadline = DateTimeOffset.UtcNow + (Timeout ?? TimeSpan.FromSeconds(5));

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (Probe())
                    return;
                await Task.Delay(10);
            }

            throw new TimeoutException("The condition was not met in time!");

        }

        #endregion

    }

}
