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
    /// Answers DNS queries from an authoritative zone store.
    /// </summary>
    public sealed class AuthoritativeDNSRequestHandler : IDNSRequestHandler
    {

        private readonly IDNSZoneStore zoneStore;

        /// <summary>
        /// How many CNAMEs will be followed before giving up. RFC 1034 §4.3.2 sets
        /// no limit but warns that a chain can loop; this bounds the work even when
        /// the loop detection below cannot (for instance a chain that grows through
        /// a store which synthesizes names).
        /// </summary>
        private const Int32 MaxCanonicalNameChainLength = 16;

        /// <summary>
        /// How many DNAME redirections will be applied to one question before
        /// giving up.
        /// </summary>
        /// <remarks>
        /// RFC 6672 §2.2 notes that "fairly lengthy valid chains of DNAME RRs"
        /// are legitimate, so this is not a conformance limit but a stop. The
        /// case it exists for is a DNAME pointing inside its own subtree —
        /// <c>example. DNAME sub.example.</c> — which rewrites a name into a
        /// longer one every pass. That does terminate, because the 255-octet
        /// limit eventually refuses it, but only after adding a label at a time;
        /// this ends it in a bounded number of lookups instead.
        /// </remarks>
        private const Int32 MaxRedirectionChainLength   = 16;


        public Boolean  RecursionAvailable  { get; }

        /// <summary>
        /// The UDP payload size this server advertises in its own OPT record
        /// (RFC 6891 §6.2.4). The default of 1232 bytes follows the DNS Flag Day
        /// 2020 recommendation: it stays below common path-MTU limits, so
        /// responses do not fragment.
        /// </summary>
        public UInt16   UDPPayloadSize      { get; }

        /// <summary>
        /// The secret this server issues and recognises DNS Cookies with
        /// (RFC 7873). Null leaves cookies switched off entirely.
        /// </summary>
        public Byte[]?  DNSCookieSecret     { get; }

        /// <summary>
        /// Whether a query must carry a valid server cookie to be answered.
        /// </summary>
        /// <remarks>
        /// RFC 7873 §5.2.3 leaves this to the server: a query with only a client
        /// cookie may be answered, or answered BADCOOKIE with a fresh server
        /// cookie so the client can ask again. Answering is the friendlier
        /// default and the useless one — the point of cookies is to make a
        /// spoofed source address expensive, and that only happens once an
        /// answer costs a round trip the spoofer cannot complete. So this is the
        /// switch that turns the mechanism from decoration into defence, and it
        /// is off by default because turning it on changes what every existing
        /// client sees.
        /// </remarks>
        public Boolean  RequireDNSCookies   { get; }

        /// <summary>
        /// Whether unicast queries for the link-local names of Multicast DNS - "local." and the link-local
        /// reverse domains "254.169.in-addr.arpa." and "8.e.f.ip6.arpa." to "b.e.f.ip6.arpa.", with every name
        /// below them - are answered from the zone.
        /// </summary>
        /// <remarks>
        /// Off by default. RFC 6762 §22.1, item 5: "Authoritative DNS servers SHOULD NOT by default be
        /// configurable to answer queries for these names, and, like caching DNS servers, SHOULD generate
        /// immediate NXDOMAIN responses for all such queries they may receive. DNS server software MAY provide a
        /// configuration option to override this default, for testing purposes or other specialized uses." This
        /// is that option, for a network that serves ".local" by unicast DNS. A query that arrives on the
        /// multicast listener is answered from the zone either way.
        /// </remarks>
        public Boolean  ServeLinkLocalNames { get; }


        public AuthoritativeDNSRequestHandler(IDNSZoneStore  ZoneStore,
                                              Boolean        RecursionAvailable   = false,
                                              UInt16         UDPPayloadSize       = 1232,
                                              Byte[]?        DNSCookieSecret      = null,
                                              Boolean        RequireDNSCookies    = false,
                                              Boolean        ServeLinkLocalNames  = false)
        {

            this.zoneStore            = ZoneStore;
            this.RecursionAvailable   = RecursionAvailable;
            this.UDPPayloadSize       = UDPPayloadSize;
            this.DNSCookieSecret      = DNSCookieSecret;
            this.RequireDNSCookies    = RequireDNSCookies;
            this.ServeLinkLocalNames  = ServeLinkLocalNames;

        }


        #region (private) BuildResponseOPT(Request, ResponseCode)

        /// <summary>
        /// Build the OPT record for a response, or null when the request had none.
        /// </summary>
        /// <remarks>
        /// RFC 6891 §6.1.1: a responder that implements EDNS "MUST include an OPT
        /// record in their respective responses" — an EDNS query is answered with
        /// an EDNS response. The OPT advertises this server's own payload size
        /// (§6.2.4) and carries the upper 8 bits of an extended RCODE (§6.1.3).
        /// Options are deliberately not echoed: unknown options MUST be ignored
        /// (§6.1.2), and reflecting them would make the server an amplifier.
        /// </remarks>
        private OPT? BuildResponseOPT(DNSPacket         Request,
                                      DNSResponseCodes  ResponseCode)
        {

            if (!Request.AdditionalRRs.OfType<OPT>().Any())
                return null;

            return new OPT(
                       UDPPayloadSize:  UDPPayloadSize,
                       ExtendedRCODE:   (Byte) ((Int32) ResponseCode >> 4),
                       Version:         0,
                       Flags:           0,
                       Options:         BuildResponseCookie(Request) is { } cookie ? [ cookie ] : null
                   );

        }

        #endregion

        #region (private) BuildResponseCookie(Request) / ReadRequestCookie(Request) / HasValidServerCookie(Request)

        /// <summary>
        /// The COOKIE option to send back, or null when there is nothing to say.
        /// </summary>
        /// <remarks>
        /// RFC 7873 §5.2.3: a query carrying only a client cookie gets a server
        /// cookie in reply — "servers MUST, at least occasionally, respond to
        /// such requests to inform the client of the correct Server Cookie",
        /// because a client that is never told one can never present one. The
        /// client cookie is echoed exactly, since §5.3 has the client discard any
        /// response where it is not.
        /// </remarks>
        private EDNSCookieOption? BuildResponseCookie(DNSPacket Request)
        {

            if (DNSCookieSecret is null)
                return null;

            var requestCookie = ReadRequestCookie(Request);

            if (requestCookie is null)
                return null;

            return new EDNSCookieOption(
                       requestCookie.ClientCookie,
                       DNSCookies.Create(
                           requestCookie.ClientCookie,
                           Request.RemoteSocket.IPAddress,
                           DNSCookieSecret
                       )
                   );

        }


        private static EDNSCookieOption? ReadRequestCookie(DNSPacket Request)

            => Request.AdditionalRRs.
                       OfType<OPT>().
                       FirstOrDefault()?.
                       Options.
                       OfType<EDNSCookieOption>().
                       FirstOrDefault();


        /// <summary>
        /// Whether the request returned a server cookie this server issued, to
        /// this client cookie, from this address, recently enough.
        /// </summary>
        private Boolean HasValidServerCookie(DNSPacket Request)
        {

            if (DNSCookieSecret is null)
                return false;

            var requestCookie = ReadRequestCookie(Request);

            return requestCookie?.ServerCookie is not null &&
                   DNSCookies.Validate(
                       requestCookie.ServerCookie,
                       requestCookie.ClientCookie,
                       Request.RemoteSocket.IPAddress,
                       DNSCookieSecret
                   );

        }

        #endregion


        #region (private) FollowCanonicalNames(Question, Answers, CancellationToken)

        /// <summary>
        /// Apply the CNAME rule: if the queried node holds a CNAME and QTYPE is not
        /// CNAME, copy the CNAME into the answer section and restart the query at
        /// the canonical name.
        /// </summary>
        /// <remarks>
        /// RFC 1034 §4.3.2 step 3a. A CNAME answers a query of *any* type at that
        /// name — the rule is about the node, not about which types it happens to
        /// hold. Without it an alias answers only QTYPE=CNAME and everything else
        /// returns NOERROR with an empty answer section, which a resolver reads as
        /// "this name definitively has no such record" and caches. The alias then
        /// silently stops working for every client that does not already know it is
        /// one.
        ///
        /// The chase stops at the zone edge: when the canonical name is not held
        /// here, the CNAMEs gathered so far are returned and the resolver continues
        /// from them, which is what RFC 1034 expects of an authoritative server.
        /// </remarks>
        /// <param name="Question">The original question.</param>
        /// <param name="Answers">The answer section being assembled.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        private async Task FollowCanonicalNames(DNSQuestion               Question,
                                                List<IDNSResourceRecord>  Answers,
                                                Boolean                   DNSSECOK,
                                                CancellationToken         CancellationToken)
        {

            // A CNAME query is answered directly by the store, and ANY already
            // returns the CNAME alongside everything else at the node, so neither
            // needs a restart.
            if (Question.QueryType == DNSResourceRecordTypes.CNAME ||
                Question.QueryType == DNSResourceRecordTypes.Any)
            {
                return;
            }

            var currentName = Question.DomainName;

            // RFC 1034 §4.3.2 warns that a chain may loop. Track the names already
            // visited rather than relying on the depth limit alone — a two-element
            // cycle would otherwise be walked sixteen times before stopping, and the
            // same CNAME would be added to the answer section on every pass.
            var visited     = new HashSet<DNSServiceName> { currentName };

            for (var depth = 0; depth < MaxCanonicalNameChainLength; depth++)
            {

                var aliasResult = await zoneStore.Lookup(
                                            new DNSQuestion(
                                                currentName,
                                                DNSResourceRecordTypes.CNAME,
                                                Question.QueryClass
                                            ),
                                            DNSSECOK,
                                            CancellationToken
                                        ).ConfigureAwait(false);

                if (aliasResult.Status != DNSZoneLookupStatus.Found)
                    return;

                var alias = aliasResult.AnswerRRs.OfType<CNAME>().FirstOrDefault();

                if (alias is null)
                    return;

                Answers.Add(alias);

                var target = DNSServiceName.Parse(alias.CName.FullName);

                if (!visited.Add(target))
                    return;

                // Restart the original query at the canonical name.
                var targetResult = await zoneStore.Lookup(
                                             new DNSQuestion(
                                                 target,
                                                 Question.QueryType,
                                                 Question.QueryClass
                                             ),
                                             DNSSECOK,
                                             CancellationToken
                                         ).ConfigureAwait(false);

                if (targetResult.Status == DNSZoneLookupStatus.Found)
                {
                    Answers.AddRange(targetResult.AnswerRRs);
                    return;
                }

                // NoData at the target means it may itself be an alias; NameError
                // means the chain leaves this zone and the next pass will stop.
                currentName = target;

            }

        }

        #endregion

        #region (private) AddAddressesOfKeyExchangers(Answers, AdditionalRecords, DNSSECOK, CancellationToken)

        /// <summary>
        /// The additional section processing a KX answer causes: the A and AAAA
        /// records of every exchanger it names, from the zones this server holds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// RFC 2230 §3.1: "KX records MUST cause type A additional section
        /// processing for the host specified by EXCHANGER. In the event that the
        /// host processing the DNS transaction supports IPv6, KX records MUST also
        /// cause type AAAA additional section processing." Hermod supports IPv6,
        /// so both. Additional section processing is RFC 1034 §4.3.2 step 6,
        /// "Using local data only": an exchanger outside the zones held here, below
        /// a zone cut, or without addresses adds nothing.
        /// </para>
        /// <para>
        /// With the DO bit the RRSIGs come along, because RFC 4035 §3.1.1 says
        /// "When placing a signed RRset in the Additional section, the name server
        /// MUST also place its RRSIG RRs in the Additional section" — and the
        /// lookup returns them with the RRset when asked with DO. An RRset already
        /// in the answer or the additional section is not added twice.
        /// </para>
        /// <para>
        /// What is added here is optional in the sense of RFC 2181 §9: if it does
        /// not fit, it is left out whole and TC stays clear, which is
        /// <see cref="DNSMessagePipeline.SerializeDatagramResponse"/>'s part.
        /// </para>
        /// </remarks>
        private async Task AddAddressesOfKeyExchangers(List<IDNSResourceRecord>  Answers,
                                                       List<IDNSResourceRecord>  AdditionalRecords,
                                                       Boolean                   DNSSECOK,
                                                       CancellationToken         CancellationToken)
        {

            var exchangers = Answers.OfType<KX>().
                                     Where   (kx => kx.UnparsedRData is null &&
                                                    kx.Exchanger.FullName.TrimEnd('.').Length > 0).
                                     Select  (kx => (Name: kx.Exchanger, kx.Class)).
                                     DistinctBy(exchanger => exchanger.Name.FullName.ToLowerInvariant()).
                                     ToArray ();

            foreach (var (exchanger, queryClass) in exchangers)
            {

                // Additional data is never worth an answer: a name the lookup
                // cannot be asked about is skipped rather than thrown over.
                if (DNSServiceName.TryParse(exchanger.FullName) is not { } exchangerName)
                    continue;

                foreach (var type in new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA })
                {

                    if (Answers.Concat(AdditionalRecords).Any(record => record.Type == type &&
                                                                        String.Equals(record.DomainName.FullName.TrimEnd('.'),
                                                                                      exchanger.FullName.TrimEnd('.'),
                                                                                      StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var lookupResult = await zoneStore.Lookup(
                                                 new DNSQuestion(
                                                     exchangerName,
                                                     type,
                                                     queryClass
                                                 ),
                                                 DNSSECOK,
                                                 CancellationToken
                                             ).ConfigureAwait(false);

                    if (lookupResult.Status != DNSZoneLookupStatus.Found)
                        continue;

                    AdditionalRecords.AddRange(
                        lookupResult.AnswerRRs.Where(record => record.Type == type ||
                                                               record is RRSIG signature && signature.TypeCovered == type)
                    );

                }

            }

        }

        #endregion


        public async Task<DNSResponse?> ProcessDNSRequest(DNSPacket          Request,
                                                          CancellationToken  CancellationToken = default)
        {

            if (Request.QueryOrResponse != DNSQueryResponse.Query)
                return null;

            DNSResponse Error(DNSResponseCodes ResponseCode)
            {

                var opt = BuildResponseOPT(Request, ResponseCode);

                return Request.CreateResponse(
                           Opcode:               Request.Opcode,
                           AuthoritativeAnswer:  false,
                           Truncation:           false,

                           // RFC 1035 §4.1.1 has RD "copied into the response", but
                           // the bit it describes is the one that "directs the name
                           // server to pursue the query recursively" — a property of
                           // a QUERY. A reply of NOTIMP has just said it did not
                           // understand the request, so echoing a bit whose meaning
                           // belongs to the opcode it rejected asserts something it
                           // cannot know. RFC 6895 §2 leaves the combination open;
                           // ISC's genreport calls the echo a failure and BIND clears
                           // the bit. Every other flag was already cleared on this
                           // path — RD was the lone exception.
                           RecursionDesired:     Request.Opcode == 0 && Request.RecursionDesired,
                           RecursionAvailable:   RecursionAvailable,
                           ResponseCode:         ResponseCode,
                           AnswerRRs:            [],
                           AuthorityRRs:         [],
                           AdditionalRRs:        opt is not null ? [ opt ] : []
                       );

            }

            // RFC 6891 §6.1.3: "If a responder does not implement the VERSION
            // level of the request, then it MUST respond with RCODE=BADVERS."
            // Only EDNS version 0 is defined today.
            var requestOPT = Request.AdditionalRRs.OfType<OPT>().FirstOrDefault();

            if (requestOPT is not null && requestOPT.Version > 0)
                return Error(DNSResponseCodes.BadVersion);

            if (Request.Opcode != 0)
                return Error(DNSResponseCodes.NotImplemented);

            // RFC 7873 §5.2.3/§5.2.4: without a server cookie this server issued,
            // the query is answered BADCOOKIE — and the reply carries a fresh one,
            // so the client's next attempt succeeds. That round trip is the whole
            // mechanism: it costs a legitimate client nothing it cannot pay, and
            // costs a spoofed source address an answer it will never see.
            if (DNSCookieSecret is not null &&
                RequireDNSCookies           &&
                ReadRequestCookie(Request) is not null &&
                !HasValidServerCookie(Request))
            {
                return Error(DNSResponseCodes.BadCookie);
            }

            var questions = Request.Questions.ToArray();
            if (questions.Length == 0)
                return Error(DNSResponseCodes.FormatError);

            var answers            = new List<IDNSResourceRecord>();
            var authorities        = new List<IDNSResourceRecord>();
            var additionalRecords  = new List<IDNSResourceRecord>();
            var responseCode       = DNSResponseCodes.NoError;
            var foundName          = false;
            var referred           = false;
            var notAuthoritative   = false;

            // RFC 4035 §3.2.1: the DO bit is what licenses this server to send
            // RRSIG and NSEC/NSEC3 records at all. A responder that ships them
            // unasked bloats every answer of every legacy client.
            var dnssecOK           = requestOPT?.DnssecOK == true;

            foreach (var question in questions)
            {

                // RFC 6672 §3.2 loops back to the start of the algorithm after a
                // DNAME substitution, so the question being answered is not
                // necessarily the one that was asked.
                var currentQuestion = question;

                for (var redirection = 0; ; redirection++)
                {

                    // RFC 6761 §6.3 and §6.4, items 5 and 6: a localhost name and
                    // an "invalid" name have the answers the specification gives
                    // them, whatever the zone holds at them, so the zone is not
                    // asked. So has a .onion name (RFC 7686 §2, items 5 and 6),
                    // and a link-local name asked by unicast unless this server
                    // was told to serve those (RFC 6762 §22.1, item 5).
                    var lookupResult = SpecialUseZones.FixedBySpecification(
                                           currentQuestion,
                                           AnswerLinkLocalNames: !ServeLinkLocalNames && !Request.ReceivedViaMulticast
                                       );
                    var specialUse   = lookupResult is not null;

                    if (lookupResult is null)
                    {

                        lookupResult = await zoneStore.Lookup(currentQuestion, dnssecOK, CancellationToken).
                                                       ConfigureAwait(false);

                        // §6.1 and §6.2, item 5: a test name or a private-address
                        // reverse name is answered negatively "unless explicitly
                        // configured by the administrator to give positive
                        // answers". What the zone serves is that configuration;
                        // what it is not authoritative for gets the negative
                        // answer, and not the REFUSED any other such name gets.
                        if (lookupResult.Status == DNSZoneLookupStatus.NotAuthoritative &&
                            SpecialUseZones.NegativeUnlessServed(currentQuestion, lookupResult.HoldsNamesBelow) is { } negative)
                        {
                            lookupResult = negative;
                            specialUse   = true;
                        }

                    }

                    authorities.      AddRange(lookupResult.AuthorityRRs);
                    additionalRecords.AddRange(lookupResult.AdditionalRRs);

                    if (lookupResult.Status == DNSZoneLookupStatus.Redirect)
                    {

                        foundName = true;

                        // §3.1: the DNAME goes into the answer "in all cases" —
                        // before the substitution is attempted, because §2.2 wants
                        // it there as the proof even when the substitution is what
                        // fails.
                        answers.AddRange(lookupResult.AnswerRRs);

                        var dname = lookupResult.AnswerRRs.OfType<DNAME>().FirstOrDefault();

                        if (dname is null || redirection >= MaxRedirectionChainLength)
                            break;

                        var substitution = DNAME.TrySubstitute(
                                               currentQuestion.DomainName,
                                               dname.DomainName,
                                               dname.Target,
                                               out var rewritten
                                           );

                        if (substitution == DNAMESubstitution.ExceedsNameLimit)
                        {
                            // §2.2: "the server returns an RCODE of YXDOMAIN". The
                            // name it could not build is longer than a name can be,
                            // which is a different thing from the name not existing
                            // — NXDOMAIN here would have resolvers cache the
                            // absence of a whole subtree.
                            responseCode = DNSResponseCodes.YXDomain;
                            break;
                        }

                        if (substitution != DNAMESubstitution.Redirected)
                            break;

                        // §3.1: the synthesized CNAME, for resolvers that do not
                        // implement DNAME. Its owner is the name that was asked
                        // for, not the DNAME's.
                        answers.Add(
                            DNAME.SynthesizeCNAME(
                                currentQuestion.DomainName,
                                rewritten!,
                                dname.TimeToLive
                            )
                        );

                        currentQuestion = new DNSQuestion(
                                              rewritten!,
                                              currentQuestion.QueryType,
                                              currentQuestion.QueryClass
                                          );

                        continue;

                    }

                    if (lookupResult.Status == DNSZoneLookupStatus.Found)
                    {
                        foundName = true;
                        answers.AddRange(lookupResult.AnswerRRs);
                    }
                    else if (lookupResult.Status == DNSZoneLookupStatus.NoData)
                    {

                        foundName = true;

                        // The name exists but holds nothing of the requested type. That is
                        // NODATA — unless the node is an alias, in which case RFC 1034
                        // §4.3.2 requires the CNAME to be returned instead. A special-use
                        // answer is no node of the zone's, and an alias the zone holds at
                        // a localhost name is configuration RFC 6761 sets aside.
                        if (!specialUse)
                            await FollowCanonicalNames(
                                      currentQuestion,
                                      answers,
                                      dnssecOK,
                                      CancellationToken
                                  ).ConfigureAwait(false);

                    }
                    else if (lookupResult.Status == DNSZoneLookupStatus.Referral)
                    {
                        foundName = true;
                        referred  = true;
                    }
                    else if (lookupResult.Status == DNSZoneLookupStatus.NotAuthoritative)
                    {
                        notAuthoritative = true;
                    }

                    break;

                }

            }

            // RFC 1034 §4.3.2: step 2 looks for a zone that is an ancestor of QNAME,
            // and without one there is no step 3 to run. REFUSED says that;
            // NXDOMAIN would instead assert the name does not exist anywhere.
            //
            // Only when nothing was found at all, though. A CNAME or DNAME chain
            // that leaves this zone ends on a name out of bailiwick by design —
            // RFC 6672 §2.2 has the synthesized CNAME returned and the resolver
            // chase the target — and that answer is NOERROR with what was found,
            // not a refusal of the question that was answered.
            var refused = notAuthoritative && !foundName;

            if (refused)
                responseCode = DNSResponseCodes.Refused;

            else if (!foundName)
                responseCode = DNSResponseCodes.NameError;

            // RFC 2230 §3.1: a KX answer causes A and AAAA additional section
            // processing for its exchangers. A referral answers nothing itself,
            // and its additional section is the glue.
            if (!referred)
                await AddAddressesOfKeyExchangers(
                          answers,
                          additionalRecords,
                          dnssecOK,
                          CancellationToken
                      ).ConfigureAwait(false);

            // RFC 6891 §6.1.1: mirror EDNS by carrying an OPT in the response.
            var responseOPT = BuildResponseOPT(Request, responseCode);

            if (responseOPT is not null)
                additionalRecords.Add(responseOPT);

            return Request.CreateResponse(
                       Opcode:               Request.Opcode,
                       Truncation:           false,
                       RecursionDesired:     Request.RecursionDesired,
                       RecursionAvailable:   RecursionAvailable,
                       ResponseCode:         responseCode,
                       AnswerRRs:            answers,
                       AuthorityRRs:         authorities,
                       AdditionalRRs:        additionalRecords,

                       // RFC 1035 §4.1.1: AA covers the name in the question, and a
                       // referral is precisely the case where this server does not
                       // own it — the NS records in the authority section are a
                       // pointer somewhere else, not an authoritative answer.
                       AuthoritativeAnswer:  !referred && !refused
                   );

        }

    }

}
