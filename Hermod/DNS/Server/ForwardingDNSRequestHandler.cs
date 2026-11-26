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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Answers DNS queries by asking a resolver further up - a forwarder, RFC 8499 §6:
    /// what a client asks is asked of the given DNS client, and what that one learns
    /// is handed back. Where a local handler is given, it is asked first, and only
    /// what it refuses - names outside its zones - is passed on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer is never authoritative (AA is clear), says that recursion is
    /// available (RA), and carries the response code, the answers, the authorities
    /// and the additional records of the resolver further up. Its OPT record is not
    /// passed on: the EDNS of one hop is not that of the next (RFC 6891 §6.1.1), so a
    /// request with an OPT record gets this server's own.
    /// </para>
    /// <para>
    /// The AD bit is never set: this server validates nothing itself, and RFC 4035
    /// §3.2.3 lets only a server that did set it. Whoever needs validated answers
    /// validates them.
    /// </para>
    /// <para>
    /// A resolver further up that does not answer, or not in time, makes the answer
    /// SERVFAIL, RFC 8914's "No Reachable Authority" aside: there is nothing else a
    /// client could do with it but try again.
    /// </para>
    /// </remarks>
    public sealed class ForwardingDNSRequestHandler : IDNSRequestHandler
    {

        #region Data

        /// <summary>
        /// The size of the UDP payload this server says it accepts, where the
        /// request carries an OPT record: RFC 9715's 1232 octets.
        /// </summary>
        public const UInt16 DefaultUDPPayloadSize = 1232;

        private readonly Func<IDNSClient>  upstream;

        #endregion

        #region Properties

        /// <summary>
        /// The resolver further up, as it is now.
        /// </summary>
        public IDNSClient           Upstream
            => upstream();

        /// <summary>
        /// The handler asked first - with the zones this server answers itself -, or
        /// null where everything is passed on.
        /// </summary>
        public IDNSRequestHandler?  Local            { get; }

        /// <summary>
        /// How long the resolver further up may take, or null for its own timeout.
        /// </summary>
        public TimeSpan?            Timeout          { get; }

        /// <summary>
        /// The size of the UDP payload this server says it accepts.
        /// </summary>
        public UInt16               UDPPayloadSize   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Answer DNS queries by asking the given DNS client.
        /// </summary>
        /// <param name="Upstream">The resolver further up.</param>
        /// <param name="Local">An optional handler asked first, whose refusals alone are passed on.</param>
        /// <param name="Timeout">How long the resolver further up may take (default: its own timeout).</param>
        /// <param name="UDPPayloadSize">The size of the UDP payload this server says it accepts (default: 1232).</param>
        public ForwardingDNSRequestHandler(IDNSClient           Upstream,
                                           IDNSRequestHandler?  Local            = null,
                                           TimeSpan?            Timeout          = null,
                                           UInt16?              UDPPayloadSize   = null)

            : this(() => Upstream,
                   Local,
                   Timeout,
                   UDPPayloadSize)

        { }

        /// <summary>
        /// Answer DNS queries by asking whichever DNS client the given function returns
        /// at the time: the resolver of a program whose name servers change while it runs.
        /// </summary>
        /// <param name="Upstream">The resolver further up, as it is at the time of a query.</param>
        /// <param name="Local">An optional handler asked first, whose refusals alone are passed on.</param>
        /// <param name="Timeout">How long the resolver further up may take (default: its own timeout).</param>
        /// <param name="UDPPayloadSize">The size of the UDP payload this server says it accepts (default: 1232).</param>
        public ForwardingDNSRequestHandler(Func<IDNSClient>     Upstream,
                                           IDNSRequestHandler?  Local            = null,
                                           TimeSpan?            Timeout          = null,
                                           UInt16?              UDPPayloadSize   = null)
        {

            this.upstream        = Upstream ?? throw new ArgumentNullException(nameof(Upstream));
            this.Local           = Local;
            this.Timeout         = Timeout;
            this.UDPPayloadSize  = UDPPayloadSize ?? DefaultUDPPayloadSize;

        }

        #endregion


        #region ProcessDNSRequest(Request, CancellationToken = default)

        /// <summary>
        /// Answer the given request: from the local handler where it answers, from
        /// the resolver further up otherwise.
        /// </summary>
        /// <param name="Request">The request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public async Task<DNSResponse?> ProcessDNSRequest(DNSPacket          Request,
                                                          CancellationToken  CancellationToken = default)
        {

            if (Request.QueryOrResponse != DNSQueryResponse.Query)
                return null;

            // RFC 6891 §6.1.3: only EDNS version 0 is defined.
            var requestOPT = Request.AdditionalRRs.OfType<OPT>().FirstOrDefault();

            if (requestOPT is not null && requestOPT.Version > 0)
                return Answer(Request, DNSResponseCodes.BadVersion);

            if (Request.Opcode != 0)
                return Answer(Request, DNSResponseCodes.NotImplemented);

            // RFC 9619: a query has exactly one question.
            var questions = Request.Questions.ToArray();
            if (questions.Length != 1)
                return Answer(Request, DNSResponseCodes.FormatError);

            var question = questions[0];

            if (Local is not null)
            {

                var local = await Local.ProcessDNSRequest(Request, CancellationToken).ConfigureAwait(false);

                // What the local zones answer - a name, the lack of one, an error - is
                // the answer. Only a refusal says "not mine", RFC 1034 §4.3.2 step 2.
                if (local is null || local.ResponseCode != DNSResponseCodes.Refused)
                    return local;

            }

            // Nothing but the Internet class is asked further up: a resolver
            // further up answers no other class, and Chaos queries ask about this
            // server, which has nothing to say about itself.
            if (question.QueryClass != DNSQueryClasses.IN)
                return Answer(Request, DNSResponseCodes.Refused);

            DNSInfo info;

            try
            {

                info = await upstream().Query(
                                 question.DomainName,
                                 [ question.QueryType ],
                                 Timeout,
                                 RecursionDesired:   true,
                                 ForceUpdate:        false,
                                 CancellationToken:  CancellationToken
                             ).ConfigureAwait(false);

            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return Answer(Request, DNSResponseCodes.ServerFailure);
            }

            if (!info.IsValid || info.IsTimeout)
                return Answer(Request, DNSResponseCodes.ServerFailure);

            return Answer(
                       Request,
                       info.ResponseCode,
                       info.Answers,
                       info.Authorities,
                       info.AdditionalRecords.Where(record => record is not OPT)
                   );

        }

        #endregion

        #region (private) Answer(Request, ResponseCode, Answers = null, Authorities = null, Additionals = null)

        private DNSResponse Answer(DNSPacket                         Request,
                                   DNSResponseCodes                  ResponseCode,
                                   IEnumerable<IDNSResourceRecord>?  Answers       = null,
                                   IEnumerable<IDNSResourceRecord>?  Authorities   = null,
                                   IEnumerable<IDNSResourceRecord>?  Additionals   = null)
        {

            var additionals = new List<IDNSResourceRecord>(Additionals ?? []);

            // RFC 6891 §7: a request with an OPT record gets one back, which carries
            // the upper eight bits of an extended response code.
            if (Request.AdditionalRRs.OfType<OPT>().Any())
                additionals.Add(new OPT(
                                    UDPPayloadSize:  UDPPayloadSize,
                                    ExtendedRCODE:   (Byte) ((Int32) ResponseCode >> 4),
                                    Version:         0,
                                    Flags:           0
                                ));

            return Request.CreateResponse(
                       Opcode:               Request.Opcode,
                       AuthoritativeAnswer:  false,
                       Truncation:           false,
                       // RFC 1035 §4.1.1 copies RD into the response - for a QUERY,
                       // and not for an opcode this server does not implement.
                       RecursionDesired:     Request.Opcode == 0 && Request.RecursionDesired,
                       RecursionAvailable:   true,
                       ResponseCode:         ResponseCode,
                       AnswerRRs:            Answers     ?? [],
                       AuthorityRRs:         Authorities ?? [],
                       AdditionalRRs:        additionals
                   );

        }

        #endregion

    }

}
