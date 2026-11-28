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

using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Buffers;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public sealed partial class DNSVerifier(IDNSClient  DNSClient,
                                            ILogger    Logger)
    {

        /// <summary>
        /// Evaluate SPF, DKIM, DMARC and ARC for an inbound message.
        /// </summary>
        /// <param name="checkSpf">Report SPF. False: the result says <see cref="SPFResult.None"/>.</param>
        /// <param name="checkDkim">Report DKIM. False: the result says <see cref="DkimResult.None"/>.</param>
        /// <param name="checkDmarc">Evaluate DMARC. False: the result says <see cref="DmarcResult.None"/>
        /// and carries no DMARC evaluation. DMARC needs SPF and DKIM for its alignment, so while
        /// it is checked they are evaluated for it even when they are not reported themselves.</param>
        public async Task<DnsVerificationResult> VerifyAsync(String                senderDomain,
                                                             System.Net.IPAddress  clientIp,
                                                             String                mailFrom,
                                                             String                heloHostname,
                                                             EMailMessage          message,
                                                             CancellationToken     ct          = default,
                                                             Boolean               checkSpf    = true,
                                                             Boolean               checkDkim   = true,
                                                             Boolean               checkDmarc  = true)
        {

            // Without a domain - a null reverse-path and a HELO that names none - there is no SPF
            // to ask; DKIM and DMARC still are.
            var spfTask   = (checkSpf || checkDmarc) && !String.IsNullOrEmpty(senderDomain)
                                ? VerifySpfAsync(senderDomain, clientIp, mailFrom, heloHostname, ct)
                                : Task.FromResult<(SPFResult Result, String? Record)>((SPFResult.None, null));
            var dkimTask  = checkDkim || checkDmarc
                                ? VerifyDkimSignaturesAsync(message, ct)
                                : Task.FromResult<(DkimResult Result, String? Details, String? Domain, IReadOnlyList<String> Passing, IReadOnlyList<DkimSignatureOutcome> Signatures)>((DkimResult.None, null, null, [], []));
            var mxTask    = !String.IsNullOrEmpty(senderDomain)
                                ? GetMxRecordsAsync(Idna.ToAscii(senderDomain), ct)
                                : Task.FromResult<String[]>([]);
            var arcTask   = VerifyArcAsync(message, ct);

            await Task.WhenAll(spfTask, dkimTask, mxTask, arcTask);

            // DMARC is anchored on the RFC5322.From domain (RFC 7489 §6.6.1) and needs the
            // SPF/DKIM results to compute identifier alignment, so it runs after the others.
            var fromDomain = DomainOf(message.From);
            var dkim       = dkimTask.Result;
            var dmarc      = checkDmarc
                                 ? await VerifyDmarcAsync(
                                             fromDomain,
                                             senderDomain,             // envelope MAIL FROM domain (SPF-authenticated)
                                             spfTask.Result.Result,
                                             dkim.Result,
                                             dkim.Passing,             // d= of every signature that holds
                                             DomainOf(mailFrom),       // the RFC5321.MailFrom domain - none for a null reverse-path,
                                                                       // whose SPF result is the HELO identity's (RFC 7208 §2.4)
                                             spfTask.Result.Record,    // the SPF record evaluated, and every DKIM signature tried:
                                             dkim.Signatures,          // what a failure report names (RFC 7489 §7.3.1)
                                             ct
                                         )
                                 : (DmarcResult.None, null, null);

            // Whether the author's own domain signed it: aligned as its DMARC record asks, and
            // relaxed where it has none. The signature named is then the author's, not one a
            // mail service put before it.
            var authors    = dkim.Passing.FirstOrDefault(domain => IsAligned(domain, fromDomain, strict: false));
            var aligned    = dmarc.Detail?.DkimAligned ?? authors is not null;
            var named      = dmarc.Detail is { DkimAligned: true } detail
                                 ? detail.DkimDomain
                                 : authors ?? dkim.Domain;

            return new DnsVerificationResult(
                checkSpf  ? spfTask.Result.Result      : SPFResult.None,
                checkSpf  ? spfTask.Result.Record      : null,
                checkDkim ? dkim.Result                : DkimResult.None,
                checkDkim ? dkim.Details               : null,
                dmarc.Result,
                dmarc.Policy,
                mxTask.Result,
                checkDkim ? named                      : null,
                dmarc.Detail,
                arcTask.Result,
                aligned
            );

        }

        // Extract the (lower-cased) domain of an addr-spec such as "alice@example.com".
        private static String DomainOf(String? address)
        {
            if (String.IsNullOrEmpty(address))
                return "";
            var at = address.LastIndexOf('@');
            return at >= 0 && at < address.Length - 1
                       ? address[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant()
                       : "";
        }

        #region SPF Verification

        // RFC 7208 §4.6.4: the total number of DNS-querying terms
        // (a, mx, ptr, exists, include, redirect) MUST NOT exceed 10.
        private const int MaxSpfDnsLookups = 10;

        // RFC 7208 §4.6.4: "SPF implementations SHOULD limit "void lookups" to two" - terms whose
        // query returns no records, or NXDOMAIN.
        private const int MaxSpfVoidLookups = 2;

        // RFC 7208 §4.6.4: the MX names of one "mx", the PTR names of one "ptr", each looked up.
        private const int MaxSpfNamesPerTerm = 10;

        // Mutable lookup budget shared across the whole (recursive) evaluation.
        private sealed class SpfLookupState
        {
            public int                     Lookups;
            public int                     VoidLookups;
            // The validated domain names of the client, once a %{p} has asked for them (RFC 7208 §7.3).
            public IReadOnlyList<String>?  ValidatedNames;
        }

        /// <summary>
        /// The end of check_host() before a result is reached: a DNS error ("temperror", RFC 7208
        /// §4.4, §5) or a record that cannot be evaluated ("permerror") - from wherever in the
        /// evaluation, an include: or a redirect= among it, it is the topmost result (§5.2).
        /// </summary>
        private sealed class SpfAbort(SPFResult Result) : Exception($"SPF {Result}")
        {
            public SPFResult Result { get; } = Result;
        }

        /// <summary>
        /// What the SPF record of a domain says about mail from the given address - asked
        /// by whoever publishes it, before anybody else does: "does my record let my own
        /// server send?" Evaluated as for a MAIL FROM of postmaster@ the domain.
        /// </summary>
        /// <param name="Domain">The domain whose SPF record is evaluated.</param>
        /// <param name="ClientIp">The address mail would come from.</param>
        /// <param name="HeloHostname">The name that server greets with, for %{h} macros; the domain by default.</param>
        /// <returns>The result, and the record it came from (null when there is none).</returns>
        public Task<(SPFResult Result, String? Record)> CheckSpfAsync(String                Domain,
                                                                      System.Net.IPAddress  ClientIp,
                                                                      String?               HeloHostname   = null,
                                                                      CancellationToken     ct             = default)

            => VerifySpfAsync(Domain, ClientIp, $"postmaster@{Domain}", HeloHostname ?? Domain, ct);

        private async Task<(SPFResult Result, string? Record)> VerifySpfAsync(
            string               domain,
            System.Net.IPAddress clientIp,
            string               mailFrom,
            string               heloHostname,
            CancellationToken    ct)
        {

            string? spfRecord = null;

            // RFC 8616 §4: "An IDN in MAIL FROM can be either U-labels or A-labels. All U-labels
            // MUST be converted to A-labels before being used for an SPF validation. This includes
            // both the labels in the name used for the original DNS lookup, described in Section 3
            // of [RFC7208], and those used in the macro expansion of domain-spec, described in
            // Section 7 of [RFC7208]." The domain, the HELO identity and the sender's domain - the
            // %{d}, %{h}, %{o} and %{s} of the macros - are in A-labels from here on.
            domain        = Idna.ToAscii(domain);
            heloHostname  = Idna.ToAscii(heloHostname);

            if (mailFrom.LastIndexOf('@') is var mailFromAt and > 0)
                mailFrom  = mailFrom[..(mailFromAt + 1)] + Idna.ToAscii(mailFrom[(mailFromAt + 1)..]);

            try
            {

                // RFC 7208 §5: "SPF implementations on IPv6 servers need to handle both "AAAA" and
                // "A" records, for clients on IPv4-mapped IPv6 addresses" - ::ffff:192.0.2.1 is
                // 192.0.2.1, for ip4, a, mx and the macros alike.
                if (clientIp.IsIPv4MappedToIPv6)
                    clientIp = clientIp.MapToIPv4();

                spfRecord = await LookupSpfRecordAsync(domain, ct);
                if (spfRecord is null)
                    return (SPFResult.None, null);

                Logger.Log(LogLevel.Debug, $"SPF record for {domain}: {spfRecord}");

                // Macro context (RFC 7208 §7). An empty MAIL FROM uses "postmaster@<helo>".
                string local, senderDom, sender;
                if (string.IsNullOrEmpty(mailFrom))
                {
                    local = "postmaster"; senderDom = heloHostname; sender = $"postmaster@{heloHostname}";
                }
                else
                {
                    var at    = mailFrom.IndexOf('@');
                    local     = at > 0 ? mailFrom[..at]       : "postmaster";
                    senderDom = at > 0 ? mailFrom[(at + 1)..] : mailFrom;
                    sender    = mailFrom;
                }
                var ctx = new SpfMacroContext(sender, local, senderDom, heloHostname);

                var result = await EvaluateSpfAsync(spfRecord, clientIp, domain, ctx, new SpfLookupState(), ct);
                return (result, spfRecord);

            }
            catch (SpfAbort abort)
            {
                Logger.Log(LogLevel.Debug, $"SPF evaluation for {domain} ended with {abort.Result}");
                return (abort.Result, spfRecord);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                Logger.Log(LogLevel.Warning, $"SPF verification error for {domain}: {ex.Message}");
                return (SPFResult.TempError, spfRecord);
            }

        }

        /// <summary>
        /// Evaluate an SPF record against the client IP (RFC 7208): its syntax first, as a
        /// whole, then its terms from left to right - the a/mx/ptr/ip4/ip6/include/exists/all
        /// mechanisms, the redirect modifier, dual-CIDR lengths, and the shared limits of ten
        /// DNS-querying terms and two void lookups across recursion.
        /// </summary>
        /// <exception cref="SpfAbort">A DNS error, or a record or limit that ends check_host().</exception>
        private async Task<SPFResult> EvaluateSpfAsync(
            string                spfRecord,
            System.Net.IPAddress  clientIp,
            string                domain,
            SpfMacroContext       ctx,
            SpfLookupState        state,
            CancellationToken     ct)
        {

            // RFC 7208 §4.6: "The syntax of the record is validated first, and if there are any
            // syntax errors anywhere in the record, check_host() returns immediately with the
            // result "permerror", without further interpretation or evaluation."
            if (!IsValidSpfRecord(spfRecord))
                throw new SpfAbort(SPFResult.PermError);

            var terms = spfRecord.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? redirectDomain = null;

            foreach (var term in terms.Skip(1))
            {

                // Modifiers are "name=value" (redirect=, exp=, unknown) - the syntax check told them apart.
                if (IsModifier(term, out var modifierName, out var modifierValue))
                {
                    if (modifierName.Equals("redirect", StringComparison.OrdinalIgnoreCase))
                        redirectDomain = modifierValue;
                    // exp= and unknown modifiers are ignored (RFC 7208 §6)
                    continue;
                }

                var qualifier = term[0] switch
                {
                    '+' => SPFResult.Pass,
                    '-' => SPFResult.Fail,
                    '~' => SPFResult.SoftFail,
                    '?' => SPFResult.Neutral,
                    _   => SPFResult.Pass   // default qualifier
                };

                // The name of the mechanism without case; its argument as it was written -
                // an upper-case macro is URL-escaped (RFC 7208 §7.3).
                var mechanism   = term[0] is '+' or '-' or '~' or '?' ? term[1..] : term;
                var split       = mechanism.IndexOfAny([ ':', '/' ]);
                var name        = (split < 0 ? mechanism : mechanism[..split]).ToLowerInvariant();
                var argument    = split < 0 ? "" : mechanism[split..];

                switch (name)
                {

                    case "all":
                        return qualifier;

                    case "ip4":
                        if (clientIp.AddressFamily == AddressFamily.InterNetwork && IpMatchesCidr(clientIp, argument[1..]))
                            return qualifier;
                        break;

                    case "ip6":
                        if (clientIp.AddressFamily == AddressFamily.InterNetworkV6 && IpMatchesCidr(clientIp, argument[1..]))
                            return qualifier;
                        break;

                    case "a":
                    {
                        CountLookup(state);

                        var (rawTarget, ip4Cidr, ip6Cidr) = ParseTarget(StripColon(argument), domain);
                        var target = await ExpandAsync(rawTarget);

                        if (Matches(clientIp, await SpfAddressesAsync(target, clientIp.AddressFamily, state, true, ct), ip4Cidr, ip6Cidr))
                            return qualifier;
                        break;
                    }

                    case "mx":
                    {
                        CountLookup(state);

                        var (rawTarget, ip4Cidr, ip6Cidr) = ParseTarget(StripColon(argument), domain);
                        var target = await ExpandAsync(rawTarget);

                        if (await MatchMxAsync(clientIp, target, ip4Cidr, ip6Cidr, state, ct))
                            return qualifier;
                        break;
                    }

                    case "ptr":
                    {
                        CountLookup(state);

                        var target = argument.Length > 0
                                         ? await ExpandAsync(StripColon(argument))
                                         : domain;

                        if (await MatchPtrAsync(clientIp, target, state, ct))
                            return qualifier;
                        break;
                    }

                    case "include":
                    {
                        CountLookup(state);

                        var includeDomain = await ExpandAsync(StripColon(argument));

                        // RFC 7208 §5.2: the result of check_host() for the included domain - a
                        // "temperror" or "permerror" there is the topmost result (thrown), and
                        // "none" is a "permerror".
                        var includeRecord = await LookupSpfRecordAsync(includeDomain, ct)
                                                ?? throw new SpfAbort(SPFResult.PermError);

                        var includeResult = await EvaluateSpfAsync(includeRecord, clientIp, includeDomain, ctx, state, ct);
                        if (includeResult == SPFResult.Pass)
                            return qualifier;
                        break;   // fail, softfail, neutral: no match, keep going
                    }

                    case "exists":
                    {
                        CountLookup(state);

                        var existsDomain = await ExpandAsync(StripColon(argument));

                        // RFC 7208 §5.7: "The resulting domain name is used for a DNS A RR lookup
                        // (even when the connection type is IPv6). If any A record is returned,
                        // this mechanism matches."
                        if ((await SpfAddressesAsync(existsDomain, AddressFamily.InterNetwork, state, true, ct)).Count > 0)
                            return qualifier;
                        break;
                    }

                    default:
                        // Unknown mechanism: the syntax check refused it already (RFC 7208 §4.6.1).
                        throw new SpfAbort(SPFResult.PermError);

                }

            }

            // No mechanism matched: apply redirect (if no 'all' was present), else default Neutral.
            if (redirectDomain is not null)
            {

                CountLookup(state);

                var redirectTarget = await ExpandAsync(redirectDomain);

                // RFC 7208 §6.1: "if no SPF record is found, or if the <target-name> is malformed,
                // the result is a "permerror" rather than "none"".
                var redirectRecord = await LookupSpfRecordAsync(redirectTarget, ct)
                                         ?? throw new SpfAbort(SPFResult.PermError);

                return await EvaluateSpfAsync(redirectRecord, clientIp, redirectTarget, ctx, state, ct);

            }

            return SPFResult.Neutral;


            // A domain-spec expanded (RFC 7208 §7) - with the validated domain name of the client
            // for a %{p}, looked up only where a %{p} asks for it.
            async Task<String> ExpandAsync(String Spec)

                => SpfMacros.Expand(Spec,
                                    domain,
                                    clientIp,
                                    SpfMacros.Uses(Spec, 'p')
                                        ? ctx with { ValidatedDomain = await ValidatedDomainAsync(clientIp, domain, state, ct) }
                                        : ctx);

        }

        /// <summary>
        /// The %{p} macro (RFC 7208 §7.3): "the validated domain name of &lt;ip&gt;. The
        /// procedure for finding the validated domain name is defined in Section 5.5. If the
        /// &lt;domain&gt; is present in the list of validated domains, it SHOULD be used.
        /// Otherwise, if a subdomain of the &lt;domain&gt; is present, it SHOULD be used.
        /// Otherwise, any name from the list can be used." Null for "unknown": "If there are no
        /// validated domain names or if a DNS error occurs". The PTR query counts against the
        /// limit of ten lookups, and of its names the first ten are validated (§4.6.4); the
        /// names are looked up once for the whole of check_host().
        /// </summary>
        private async Task<String?> ValidatedDomainAsync(System.Net.IPAddress clientIp, string domain, SpfLookupState state, CancellationToken ct)
        {

            if (state.ValidatedNames is null)
            {

                CountLookup(state);

                IReadOnlyList<String> names;

                try
                {
                    var response = await SpfQueryAsync(BuildReverseName(clientIp), DNSResourceRecordTypes.PTR, ct);
                    names = [ .. (response?.Answers.OfType<PTR>() ?? []).Select(ptr => ptr.Target.FullName.TrimEnd('.')) ];
                }
                catch (SpfAbort)
                {
                    names = [];
                }

                var validated = new List<String>();

                foreach (var name in names.Take(MaxSpfNamesPerTerm))
                {
                    try
                    {
                        if ((await SpfAddressesAsync(name, clientIp.AddressFamily, state, false, ct)).Any(ip => ip.Equals(clientIp)))
                            validated.Add(name);
                    }
                    catch (SpfAbort)
                    {
                        // A DNS error for this name: skipped, as for ptr (§5.5).
                    }
                }

                state.ValidatedNames = validated;

            }

            domain = domain.TrimEnd('.');

            return state.ValidatedNames.FirstOrDefault(name => name.Equals(domain, StringComparison.OrdinalIgnoreCase)) ??
                   state.ValidatedNames.FirstOrDefault(name => name.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)) ??
                   state.ValidatedNames.FirstOrDefault();

        }

        private static void CountLookup(SpfLookupState state)
        {
            if (++state.Lookups > MaxSpfDnsLookups)
                throw new SpfAbort(SPFResult.PermError);
        }

        /// <summary>
        /// RFC 7208 §4.6.4: a term whose lookup returned no records, or NXDOMAIN - the third is a "permerror".
        /// </summary>
        private static void CountVoidLookup(SpfLookupState state)
        {
            if (++state.VoidLookups > MaxSpfVoidLookups)
                throw new SpfAbort(SPFResult.PermError);
        }

        // ":domain-spec..." without its colon.
        private static string StripColon(string argument)
            => argument.StartsWith(':') ? argument[1..] : argument;

        // Parse "domain/ip4cidr//ip6cidr" (any part optional) into a target domain (still
        // possibly containing macros) + CIDR lengths.
        private static (string Domain, int Ip4Cidr, int Ip6Cidr) ParseTarget(string spec, string defaultDomain)
        {
            var ip4Cidr = 32;
            var ip6Cidr = 128;

            var domainPart = spec;
            var slash      = CidrStart(spec);
            if (slash >= 0)
            {
                domainPart = spec[..slash];
                var m = Regex.Match(spec[slash..], @"^(?:/(\d+))?(?://(\d+))?$");
                if (m.Groups[1].Success) ip4Cidr = int.Parse(m.Groups[1].Value);
                if (m.Groups[2].Success) ip6Cidr = int.Parse(m.Groups[2].Value);
            }

            return (string.IsNullOrEmpty(domainPart) ? defaultDomain : domainPart, ip4Cidr, ip6Cidr);
        }

        /// <summary>
        /// Where the dual-cidr-length of an "a" or "mx" argument begins: at the first "/" outside
        /// the braces of a macro, -1 without one. RFC 7208 §7.1: "/" is a delimiter too, and
        /// "a:%{d/}.example.com" has no prefix length.
        /// </summary>
        private static Int32 CidrStart(String Argument)
        {

            for (var i = 0; i < Argument.Length; i++)
            {

                if (Argument[i] == '/')
                    return i;

                if (Argument[i] == '%' && i + 1 < Argument.Length)
                {
                    if (Argument[i + 1] == '{')
                    {
                        var end = Argument.IndexOf('}', i + 2);
                        if (end < 0)
                            return -1;
                        i = end;
                    }
                    else
                        i++;   // "%%", "%_", "%-"
                }

            }

            return -1;

        }

        private static bool Matches(System.Net.IPAddress clientIp, IEnumerable<System.Net.IPAddress> addresses, int ip4Cidr, int ip6Cidr)
        {
            var cidr = clientIp.AddressFamily == AddressFamily.InterNetwork ? ip4Cidr : ip6Cidr;
            return addresses.Any(ip => ip.AddressFamily == clientIp.AddressFamily && IpMatchesCidr(clientIp, $"{ip}/{cidr}"));
        }

        private async Task<bool> MatchMxAsync(System.Net.IPAddress clientIp, string domain, int ip4Cidr, int ip6Cidr, SpfLookupState state, CancellationToken ct)
        {

            var response = await SpfQueryAsync(domain, DNSResourceRecordTypes.MX, ct);
            var mxHosts  = (response?.Answers.OfType<MX>() ?? []).
                               OrderBy(mx => mx.Preference).
                               Select (mx => mx.Exchange.FullName.TrimEnd('.')).
                               Where  (host => host.Length > 0).
                               ToList();

            if (mxHosts.Count == 0)
            {
                CountVoidLookup(state);
                return false;
            }

            // RFC 7208 §4.6.4: "the evaluation of each "MX" record MUST NOT result in querying more
            // than 10 address records ... If this limit is exceeded, the "mx" mechanism MUST produce
            // a "permerror" result."
            if (mxHosts.Count > MaxSpfNamesPerTerm)
                throw new SpfAbort(SPFResult.PermError);

            foreach (var mx in mxHosts)
            {
                if (Matches(clientIp, await SpfAddressesAsync(mx, clientIp.AddressFamily, state, false, ct), ip4Cidr, ip6Cidr))
                    return true;
            }

            return false;

        }

        /// <summary>
        /// RFC 7208 §5.5: the client's reverse names whose addresses include the client's
        /// ("validated"), the first ten - and whether one of them is the target name or within
        /// it. A DNS error of the PTR lookup is no match, one of an address lookup skips that name.
        /// </summary>
        private async Task<bool> MatchPtrAsync(System.Net.IPAddress clientIp, string target, SpfLookupState state, CancellationToken ct)
        {

            IReadOnlyList<String> names;

            try
            {
                var response = await SpfQueryAsync(BuildReverseName(clientIp), DNSResourceRecordTypes.PTR, ct);
                names = [ .. (response?.Answers.OfType<PTR>() ?? []).Select(ptr => ptr.Target.FullName.TrimEnd('.')) ];
            }
            catch (SpfAbort)
            {
                return false;
            }

            if (names.Count == 0)
            {
                CountVoidLookup(state);
                return false;
            }

            target = target.TrimEnd('.');

            foreach (var name in names.Take(MaxSpfNamesPerTerm))
            {

                if (!name.Equals(target, StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith("." + target, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    if ((await SpfAddressesAsync(name, clientIp.AddressFamily, state, false, ct)).Any(ip => ip.Equals(clientIp)))
                        return true;
                }
                catch (SpfAbort)
                {
                    // A DNS error for this name: skipped.
                }

            }

            return false;

        }

        /// <summary>
        /// The A or AAAA records of a name, as the client's address family asks (RFC 7208 §5);
        /// none counts as a void lookup when the term's own lookup is asked.
        /// </summary>
        private async Task<IReadOnlyList<System.Net.IPAddress>> SpfAddressesAsync(string                        name,
                                                                                  AddressFamily                 family,
                                                                                  SpfLookupState                state,
                                                                                  bool                          countVoid,
                                                                                  CancellationToken             ct)
        {

            var response  = await SpfQueryAsync(name,
                                                 family == AddressFamily.InterNetwork ? DNSResourceRecordTypes.A : DNSResourceRecordTypes.AAAA,
                                                 ct);

            var addresses = new List<System.Net.IPAddress>();

            if (response is not null)
            {
                if (family == AddressFamily.InterNetwork)
                    foreach (var a in response.Answers.OfType<A>())
                        addresses.Add(new System.Net.IPAddress(a.IPv4Address.GetBytes()));
                else
                    foreach (var aaaa in response.Answers.OfType<AAAA>())
                        addresses.Add(new System.Net.IPAddress(aaaa.IPv6Address.GetBytes()));
            }

            if (addresses.Count == 0 && countVoid)
                CountVoidLookup(state);

            return addresses;

        }

        /// <summary>
        /// One query of check_host(): its answer, or null for a name that does not exist (RCODE 3)
        /// or is no domain name at all. RFC 7208 §5: "if the DNS server returns an error (RCODE
        /// other than 0 or 3) or the query times out, the mechanism stops and the topmost
        /// check_host() returns "temperror"".
        /// </summary>
        /// <exception cref="SpfAbort">"temperror".</exception>
        private async Task<DNSInfo?> SpfQueryAsync(string name, DNSResourceRecordTypes type, CancellationToken ct)
        {

            if (DNSServiceName.TryParse(name.TrimEnd('.')) is not { } serviceName)
                return null;

            DNSInfo response;

            try
            {
                response = await DNSClient.Query(serviceName, [ type ], CancellationToken: ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Debug, $"SPF: {type} lookup of {name} failed: {ex.Message}");
                throw new SpfAbort(SPFResult.TempError);
            }

            if (response.IsTimeout)
                throw new SpfAbort(SPFResult.TempError);

            return response.ResponseCode switch {
                DNSResponseCodes.NoError    => response,
                DNSResponseCodes.NameError  => null,
                _                           => throw new SpfAbort(SPFResult.TempError)
            };

        }

        /// <summary>
        /// The SPF record of a domain, or null when it has none (RFC 7208 §4.4, §4.5): of its TXT
        /// records those whose version section is exactly "v=spf1" - "terminated by either an SP
        /// character or the end of the record", so "v=spf10" is none - and "If the resultant
        /// record set includes more than one record, check_host() produces the "permerror"
        /// result." A malformed domain or NXDOMAIN is "none" (§4.3).
        /// </summary>
        /// <exception cref="SpfAbort">"temperror" for a DNS error, "permerror" for two records.</exception>
        private async Task<String?> LookupSpfRecordAsync(String domain, CancellationToken ct)
        {

            var response = await SpfQueryAsync(domain, DNSResourceRecordTypes.TXT, ct);
            if (response is null)
                return null;

            var records  = response.Answers.
                               OfType<TXT>().
                               Select(txt => txt.Text).
                               Where (IsSpfRecord).
                               ToList();

            return records.Count switch {
                0 => null,
                1 => records[0],
                _ => throw new SpfAbort(SPFResult.PermError)
            };

        }

        /// <summary>
        /// Whether a TXT record is an SPF record: its version section is exactly "v=spf1",
        /// followed by a space or nothing (RFC 7208 §4.5).
        /// </summary>
        internal static Boolean IsSpfRecord(String Record)

            => Record.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase) &&
               (Record.Length == 6 || Record[6] == ' ');

        #region SPF syntax (RFC 7208 §4.6, §12)

        private static readonly Regex SpfModifierName  = new (@"^[A-Za-z][A-Za-z0-9\-_.]*$",  RegexOptions.CultureInvariant);
        private static readonly Regex SpfIp4Network    = new (@"^(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])(\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])){3}$", RegexOptions.CultureInvariant);
        private static readonly Regex SpfDualCidr      = new (@"^(?:/(0|[1-9][0-9]?))?(?://(0|[1-9][0-9]{0,2}))?$", RegexOptions.CultureInvariant);
        private static readonly Regex SpfTopLabel      = new (@"^(?:[A-Za-z0-9]*[A-Za-z][A-Za-z0-9]*|[A-Za-z0-9]+-[A-Za-z0-9\-]*[A-Za-z0-9])$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether an SPF record is free of syntax errors anywhere (RFC 7208 §4.6, the ABNF of
        /// §12): the version, then directives - mechanisms with their arguments, addresses and
        /// prefix lengths - and modifiers, redirect= and exp= at most once each (§6).
        /// </summary>
        internal static Boolean IsValidSpfRecord(String Record)
        {

            var terms = Record.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (terms.Length == 0 || !terms[0].Equals("v=spf1", StringComparison.OrdinalIgnoreCase))
                return false;

            var redirects = 0;
            var exps      = 0;

            foreach (var term in terms.Skip(1))
            {

                if (IsModifier(term, out var name, out var value))
                {

                    // unknown-modifier = name "=" macro-string: any macro letter; redirect= and
                    // exp= take a domain-spec, without c, r and t (RFC 7208 §7.2).
                    if (!IsMacroString(value, ExplanationLetters: true))
                        return false;

                    if (name.Equals("redirect", StringComparison.OrdinalIgnoreCase) && (++redirects > 1 || !IsDomainSpec(value)))
                        return false;

                    if (name.Equals("exp",      StringComparison.OrdinalIgnoreCase) && (++exps      > 1 || !IsDomainSpec(value)))
                        return false;

                    continue;

                }

                if (!IsValidMechanism(term[0] is '+' or '-' or '~' or '?' ? term[1..] : term))
                    return false;

            }

            return true;

        }

        /// <summary>
        /// Whether a term is a modifier, name "=" value - with a name of a letter and letters,
        /// digits, "-", "_" or ".", before any ":" or "/" of a mechanism.
        /// </summary>
        private static Boolean IsModifier(String Term, out String Name, out String Value)
        {

            Name  = "";
            Value = "";

            var equals = Term.IndexOf('=');
            if (equals <= 0)
                return false;

            var stop   = Term.IndexOfAny([ ':', '/' ]);
            if (stop >= 0 && stop < equals)
                return false;

            if (!SpfModifierName.IsMatch(Term[..equals]))
                return false;

            Name  = Term[..equals];
            Value = Term[(equals + 1)..];
            return true;

        }

        private static Boolean IsValidMechanism(String Mechanism)
        {

            var split    = Mechanism.IndexOfAny([ ':', '/' ]);
            var name     = (split < 0 ? Mechanism : Mechanism[..split]).ToLowerInvariant();
            var argument = split < 0 ? "" : Mechanism[split..];

            switch (name)
            {

                case "all":
                    return argument.Length == 0;

                case "include":
                case "exists":
                    return argument.StartsWith(':') && IsDomainSpec(argument[1..]);

                case "ptr":
                    return argument.Length == 0 || (argument.StartsWith(':') && IsDomainSpec(argument[1..]));

                case "a":
                case "mx":
                {
                    var cidr = argument;
                    if (argument.StartsWith(':'))
                    {
                        var slash = CidrStart(argument);
                        if (!IsDomainSpec(slash < 0 ? argument[1..] : argument[1..slash]))
                            return false;
                        cidr = slash < 0 ? "" : argument[slash..];
                    }
                    var match = SpfDualCidr.Match(cidr);
                    return match.Success &&
                           (!match.Groups[1].Success || Int32.Parse(match.Groups[1].Value) <= 32) &&
                           (!match.Groups[2].Success || Int32.Parse(match.Groups[2].Value) <= 128);
                }

                case "ip4":
                {
                    if (!argument.StartsWith(':'))
                        return false;
                    var slash   = argument.IndexOf('/');
                    var network = slash < 0 ? argument[1..] : argument[1..slash];
                    var cidr    = slash < 0 ? "" : argument[slash..];
                    return SpfIp4Network.IsMatch(network) &&
                           (cidr.Length == 0 || (Regex.IsMatch(cidr, "^/(0|[1-9][0-9]?)$") && Int32.Parse(cidr[1..]) <= 32));
                }

                case "ip6":
                {
                    if (!argument.StartsWith(':'))
                        return false;
                    var slash   = argument.IndexOf('/');
                    var network = slash < 0 ? argument[1..] : argument[1..slash];
                    var cidr    = slash < 0 ? "" : argument[slash..];
                    return !network.Contains('%') &&
                           System.Net.IPAddress.TryParse(network, out var address) &&
                           address.AddressFamily == AddressFamily.InterNetworkV6 &&
                           (cidr.Length == 0 || (Regex.IsMatch(cidr, "^/(0|[1-9][0-9]{0,2})$") && Int32.Parse(cidr[1..]) <= 128));
                }

                default:
                    return false;

            }

        }

        /// <summary>
        /// domain-spec = macro-string domain-end; domain-end = ( "." toplabel [ "." ] ) / macro-expand.
        /// </summary>
        private static Boolean IsDomainSpec(String Spec)
        {

            if (Spec.Length == 0 || !IsMacroString(Spec, ExplanationLetters: false))
                return false;

            // Ends in a macro-expand: "%{d}", "%%", "%_", "%-".
            if (Spec.EndsWith('}') || Regex.IsMatch(Spec, "%[%_-]$"))
                return true;

            var name = Spec.EndsWith('.') ? Spec[..^1] : Spec;
            var dot  = name.LastIndexOf('.');

            return dot >= 0 && SpfTopLabel.IsMatch(name[(dot + 1)..]);

        }

        /// <summary>
        /// macro-string = *( macro-expand / macro-literal ); macro-expand = ( "%{" macro-letter
        /// transformers *delimiter "}" ) / "%%" / "%_" / "%-"; macro-literal = %x21-24 / %x26-7E.
        /// c, r and t are for explanations only (RFC 7208 §7.2).
        /// </summary>
        private static Boolean IsMacroString(String Text, Boolean ExplanationLetters)
        {

            for (var i = 0; i < Text.Length; i++)
            {

                var c = Text[i];

                if (c != '%')
                {
                    if (c < 0x21 || c > 0x7E)
                        return false;
                    continue;
                }

                if (i + 1 >= Text.Length)
                    return false;

                var next = Text[i + 1];

                if (next is '%' or '_' or '-')
                {
                    i++;
                    continue;
                }

                if (next != '{')
                    return false;

                var end = Text.IndexOf('}', i + 2);
                if (end < 0)
                    return false;

                var macro = Text[(i + 2)..end];
                // transformers = *DIGIT [ "r" ], and "If a DIGIT is specified, the value MUST be
                // nonzero" (§7.3).
                if (!Regex.IsMatch(macro, ExplanationLetters ? "^[slodiphcrtvSLODIPHCRTV](?:0*[1-9][0-9]*)?[rR]?[.\\-+,/_=]*$"
                                                             : "^[slodiphvSLODIPHV](?:0*[1-9][0-9]*)?[rR]?[.\\-+,/_=]*$"))
                    return false;

                i = end;

            }

            return true;

        }

        #endregion

        /// <summary>
        /// Reverse (PTR) lookup for an IP address via the Hermod DNS client.
        /// Returns the first PTR target hostname (without trailing dot), or null.
        /// </summary>
        public async Task<String?> ReverseLookupAsync(System.Net.IPAddress ip, CancellationToken ct = default)
        {
            try
            {
                var response = await DNSClient.Query(
                                         DNSServiceName.Parse(BuildReverseName(ip)),
                                         [ DNSResourceRecordTypes.PTR ],
                                         CancellationToken: ct
                                     );

                var ptr = response.Answers.OfType<PTR>().FirstOrDefault();
                return ptr?.Target.FullName.TrimEnd('.');
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Debug, $"PTR lookup failed for {ip}: {ex.Message}");
                return null;
            }
        }

        // Build the reverse-DNS query name: "4.3.2.1.in-addr.arpa" / nibble-reversed "...ip6.arpa".
        private static String BuildReverseName(System.Net.IPAddress ip)

            => ReversedName(ip) + (ip.AddressFamily == AddressFamily.InterNetwork ? ".in-addr.arpa" : ".ip6.arpa");

        // "4.3.2.1" / the 32 nibbles of an IPv6 address, the last first - for reverse DNS and
        // the blocklists alike (RFC 5782 §2.1, §2.4).
        private static String ReversedName(System.Net.IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();

            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return String.Join('.', bytes.Reverse());

            var sb = new StringBuilder();
            for (var i = bytes.Length - 1; i >= 0; i--)
            {
                sb.Append((bytes[i] & 0x0F).ToString("x")).Append('.');
                sb.Append((bytes[i] >> 4)  .ToString("x")).Append('.');
            }
            return sb.ToString(0, sb.Length - 1);
        }

        private static bool IpMatchesCidr(System.Net.IPAddress ip, string cidr)
        {
            try
            {
                var parts = cidr.Split('/');
                var network = System.Net.IPAddress.Parse(parts[0]);
                var prefixLength = parts.Length > 1 ? int.Parse(parts[1]) : (ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128);

                var ipBytes = ip.GetAddressBytes();
                var networkBytes = network.GetAddressBytes();

                if (ipBytes.Length != networkBytes.Length)
                    return false;

                var fullBytes = prefixLength / 8;
                var remainingBits = prefixLength % 8;

                for (int i = 0; i < fullBytes; i++)
                {
                    if (ipBytes[i] != networkBytes[i])
                        return false;
                }

                if (remainingBits > 0 && fullBytes < ipBytes.Length)
                {
                    var mask = (byte)(0xFF << (8 - remainingBits));
                    if ((ipBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region DKIM Verification

        /// <summary>
        /// The DKIM signatures of a message as it is - the first that holds, or why
        /// none does - without SPF, DMARC, ARC or the MX lookup VerifyAsync makes:
        /// for whoever wants to know whether a message they signed will be found
        /// signed, before it leaves.
        /// </summary>
        /// <param name="RawMessage">The message, CRLF and all.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<(DkimResult Result, string? Details, string? Domain)> VerifyDkimAsync(Byte[]             RawMessage,
                                                                                                CancellationToken  CancellationToken = default)
        {
            var (result, details, domain, _, _) = await VerifyDkimSignaturesAsync(EMailMessage.Parse(RawMessage), CancellationToken);
            return (result, details, domain);
        }

        // RFC 6376 §6.1 lets a verifier limit the signatures it tries; each costs a DNS query.
        private const Int32 MaxDkimSignatures = 10;

        /// <summary>
        /// Every DKIM signature of a message, not only the first that holds: DMARC passes
        /// on any that is aligned with the From: domain (RFC 7489 §4.2), and the signature
        /// of a mail service often comes before the author's own.
        /// </summary>
        /// <returns>The first that holds - or, when none does, the last tried and why it
        /// failed - the d= of every one that holds, and every signature tried with its d=, i=,
        /// s= and result, for a failure report (RFC 6591 §3.2.3).</returns>
        private async Task<(DkimResult Result, string? Details, string? Domain, IReadOnlyList<String> Passing, IReadOnlyList<DkimSignatureOutcome> Signatures)> VerifyDkimSignaturesAsync(
            EMailMessage      message,
            CancellationToken ct)
        {
            try
            {
                // Parse the raw message so canonicalization sees the original bytes
                // (message.Headers are already unfolded/trimmed and unusable for DKIM) - its
                // octets, read one character per octet: the UTF-8 decoding would have made
                // U+FFFD of an 8-bit body in another charset, and the hash another one.
                var (headerBlock, body) = DkimCanonicalization.Split(MessageOctets.ToText(message.GetBytes()));
                var fields              = DkimCanonicalization.ParseFields(headerBlock);
                var dkimFields          = fields.Where(f => f.Name.Equals("DKIM-Signature", StringComparison.OrdinalIgnoreCase)).ToList();

                if (dkimFields.Count == 0)
                    return (DkimResult.None, "No DKIM signature found", null, [], []);

                (DkimResult Result, string? Details, string? Domain)? first = null;
                (DkimResult Result, string? Details) last = (DkimResult.Fail, "All DKIM signatures failed verification");
                string? lastDomain = null;
                var passing    = new List<String>();
                var signatures = new List<DkimSignatureOutcome>();

                foreach (var dkimField in dkimFields.Take(MaxDkimSignatures))
                {

                    // d= of the signature being evaluated (for Authentication-Results header.d).
                    var tags   = ParseDkimHeader(dkimField.RawValue);
                    var domain = tags.GetValueOrDefault("d");
                    var result = await VerifySingleDkimSignature(dkimField, fields, body, ct);

                    if (!String.IsNullOrEmpty(domain))
                        signatures.Add(new DkimSignatureOutcome(domain, tags.GetValueOrDefault("i"), tags.GetValueOrDefault("s"), result.Result));

                    if (result.Result == DkimResult.Pass)
                    {
                        first ??= (result.Result, result.Details, domain);
                        if (!String.IsNullOrEmpty(domain))
                            passing.Add(domain);
                    }
                    else
                    {
                        last       = result;
                        lastDomain = domain;
                    }

                }

                return first is { } pass
                           ? (pass.Result, pass.Details, pass.Domain, passing, signatures)
                           : (last.Result, last.Details, lastDomain,  passing, signatures);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"DKIM verification error: {ex.Message}");
                return (DkimResult.TempError, ex.Message, null, [], []);
            }
        }

        private async Task<(DkimResult Result, string? Details)> VerifySingleDkimSignature(
            DkimHeaderField        dkimField,
            List<DkimHeaderField>  fields,
            string                 body,
            CancellationToken      ct)
        {
            var dkimParams = ParseDkimHeader(dkimField.RawValue, out var duplicateTag);

            // RFC 6376 §3.2: "Tags with duplicate names MUST NOT occur within a single tag-list;
            // if a tag name does occur more than once, the entire tag-list is invalid" - taken,
            // the last d= would be verified while a reader of the first takes it for the
            // signature's.
            //
            // A signature that cannot be evaluated - a tag-list that is invalid, a required tag
            // missing, another version, no key or a key record that is invalid - is "permerror"
            // (RFC 8601 §2.7.1: "The message could not be verified due to some error that is
            // unrecoverable"), not "fail", which says it was verified and did not hold.
            if (duplicateTag is not null)
                return (DkimResult.PermError, $"The DKIM-Signature names {duplicateTag}= more than once (RFC 6376 §3.2)");

            // RFC 6376 §6.1.1: "If any tag listed as "required" in Section 3.5 is omitted from the
            // DKIM-Signature header field, the Verifier MUST ignore the DKIM-Signature header field
            // and return PERMFAIL" - v=, a=, b=, bh=, d=, h= and s=.
            if (!dkimParams.TryGetValue("v",  out var version)    ||
                !dkimParams.TryGetValue("a",  out var algorithm)  ||
                !dkimParams.TryGetValue("b",  out var signature)  ||
                !dkimParams.TryGetValue("bh", out var bodyHash)   ||
                !dkimParams.TryGetValue("d",  out var domain)     ||
                !dkimParams.TryGetValue("h",  out var headerList) ||
                !dkimParams.TryGetValue("s",  out var selector))
            {
                return (DkimResult.PermError, "Missing required DKIM parameters (RFC 6376 §3.5: v=, a=, b=, bh=, d=, h=, s=)");
            }

            // §6.1.1: "Verifiers MUST return PERMFAIL (incompatible version) when presented a
            // DKIM-Signature header field with a "v=" tag that is inconsistent with this
            // specification"; §3.5, v=: "It MUST have the value "1"".
            if (version != "1")
                return (DkimResult.PermError, $"A DKIM-Signature of version {version} (RFC 6376 §6.1.1)");

            // §6.1.1: "Implementers MUST meticulously validate the format and values in the
            // DKIM-Signature header field; any inconsistency or unexpected values MUST cause the
            // header field to be completely ignored and the Verifier to return PERMFAIL
            // (signature syntax error)" - t=, x=, l= and q= among them.
            if (OptionalTagError(dkimParams, out var bodyLength) is { } syntaxError)
                return (DkimResult.PermError, syntaxError);

            // §6.1.1: "If the "h=" tag does not include the From header field, the Verifier MUST
            // ignore the DKIM-Signature header field and return PERMFAIL (From field not signed)" -
            // a signature that holds whatever the From says says nothing about the author.
            if (!headerList.Split(':').Any(name => name.Trim().Equals("From", StringComparison.OrdinalIgnoreCase)))
                return (DkimResult.Fail, "The signature does not sign From (RFC 6376 §6.1.1)");

            // §6.1.1: without i=, "the Verifier MUST behave as though the value of that tag were
            // "@d""; "Verifiers MUST confirm that the domain specified in the "d=" tag is the same
            // as or a parent domain of the domain part of the "i=" tag. If not, the DKIM-Signature
            // header field MUST be ignored".
            var identity        = dkimParams.GetValueOrDefault("i", "@" + domain);
            var at              = identity.LastIndexOf('@');
            var identityDomain  = at < 0 ? "" : Idna.ToAscii(identity[(at + 1)..].Trim().TrimEnd('.'));
            var signingDomain   = Idna.ToAscii(domain.Trim().TrimEnd('.'));
            var sameDomain      = identityDomain.Equals(signingDomain, StringComparison.OrdinalIgnoreCase);

            if (!sameDomain && !identityDomain.EndsWith("." + signingDomain, StringComparison.OrdinalIgnoreCase))
                return (DkimResult.Fail, $"i={identity} is not within d={domain} (RFC 6376 §6.1.1)");

            // rsa-sha256, and ed25519-sha256 (RFC 8463) - nothing else is verified. RFC 8301
            // §3.1: "rsa-sha1 MUST NOT be used for signing or verifying", its signatures "have
            // permanently failed evaluation"; an algorithm not known is not SHA-1 either.
            if (!algorithm.Equals("rsa-sha256",     StringComparison.OrdinalIgnoreCase) &&
                !algorithm.Equals("ed25519-sha256", StringComparison.OrdinalIgnoreCase))
                return (DkimResult.Fail, $"The signature algorithm {algorithm} is not verified (RFC 8301 §3.1)");

            // Get public key from DNS. RFC 8616 §5 allows U-labels in d= and s= of an EAI
            // message; §3: "When a domain name found in a mail header field ... includes U-labels,
            // those labels MUST be translated to A-labels before being looked up in the DNS".
            var dkimDomain = Idna.ToAscii($"{selector}._domainkey.{domain}");
            var (keyLookupFailed, dkimRecord) = await GetDkimKeyRecordAsync(dkimDomain, ct);

            // §6.1.2: "If the query for the public key fails to respond, the Verifier MAY seek a
            // later verification attempt by returning TEMPFAIL (key unavailable)" - RFC 8601
            // §2.7.1, temperror: "a temporary inability to retrieve a public key"; DMARC then
            // cannot apply the policy (RFC 7489 §6.6.2). "If the query for the public key fails
            // because the corresponding key record does not exist, the Verifier MUST immediately
            // return PERMFAIL (no key for signature)."
            if (keyLookupFailed)
                return (DkimResult.TempError, $"The lookup of the DKIM key at {dkimDomain} failed (RFC 6376 §6.1.2)");

            if (dkimRecord is null)
                return (DkimResult.PermError, $"No DKIM record found at {dkimDomain}");

            var dkimRecordParams = ParseDkimRecord(dkimRecord, out var duplicateKeyTag);

            // §6.1.2: "If the result returned from the query does not adhere to the format defined
            // in this specification, the Verifier MUST ignore the key record and return PERMFAIL
            // (key syntax error)" - a tag named twice (§3.2), or a v= that is not the first tag
            // (§3.6.1: "This tag MUST be the first tag in the record").
            if (duplicateKeyTag is not null)
                return (DkimResult.PermError, $"The DKIM key record names {duplicateKeyTag}= more than once (RFC 6376 §3.2)");

            if (dkimRecordParams.ContainsKey("v") && FirstTagName(dkimRecord) != "v")
                return (DkimResult.PermError, "The v= tag of the DKIM key record is not its first (RFC 6376 §3.6.1)");

            if (!dkimRecordParams.TryGetValue("p", out var publicKeyBase64))
                return (DkimResult.PermError, "No public key in DKIM record");

            // §6.1.2: "If the "p=" tag is empty, the key has been revoked; the Verifier MUST treat
            // this as a failed signature check and return PERMFAIL (key revoked)".
            if (String.IsNullOrWhiteSpace(publicKeyBase64))
                return (DkimResult.Fail, "The DKIM key has been revoked (RFC 6376 §6.1.2: p= is empty)");

            // RFC 6376 §6.1.2: "If the public-key data is not suitable for use with the algorithm
            // and key types defined by the "a=" and "k=" tags in the DKIM-Signature header field,
            // the Verifier MUST immediately return PERMFAIL (inappropriate key algorithm)" - rsa,
            // the default, and ed25519 (RFC 8463) are the types there are.
            var keyType = dkimRecordParams.GetValueOrDefault("k", "rsa").Trim().ToLowerInvariant();

            if (keyType is not "rsa" and not "ed25519")
                return (DkimResult.Fail, $"A key of type k={keyType} (RFC 6376 §6.1.2)");

            // §6.1.2: "If the "h=" tag exists in the public-key record and the hash algorithm
            // implied by the "a=" tag in the DKIM-Signature header field is not included in the
            // contents of the "h=" tag, the Verifier MUST ignore the key record" - both algorithms
            // verified here hash with SHA-256.
            if (dkimRecordParams.TryGetValue("h", out var keyHashes) &&
                !keyHashes.Split(':').Any(hash => hash.Trim().Equals("sha256", StringComparison.OrdinalIgnoreCase)))
                return (DkimResult.Fail, $"The key allows h={keyHashes}, not sha256 (RFC 6376 §6.1.2)");

            // §3.6.1, s=: "Verifiers for a given service type MUST ignore this record if the
            // appropriate type is not listed" - "*" or "email".
            if (dkimRecordParams.TryGetValue("s", out var services) &&
                !services.Split(':').Any(service => service.Trim() == "*" || service.Trim().Equals("email", StringComparison.OrdinalIgnoreCase)))
                return (DkimResult.Fail, $"The key is for s={services}, not email (RFC 6376 §3.6.1)");

            // §3.6.1, t=s: "the "i=" domain MUST NOT be a subdomain of "d="".
            if (dkimRecordParams.TryGetValue("t", out var flags) &&
                flags.Split(':').Any(flag => flag.Trim().Equals("s", StringComparison.OrdinalIgnoreCase)) &&
                !sameDomain)
                return (DkimResult.Fail, $"The key has t=s, and i={identity} is in a subdomain of d={domain} (RFC 6376 §3.6.1)");

            var (headerCanon, bodyCanon) = DkimCanonicalization.ParseMethods(dkimParams.GetValueOrDefault("c"));

            // Body hash over the wire octets, via the shared canonicalizer.
            var canonicalizedBody = DkimCanonicalization.CanonicalizeBody(body, bodyCanon);
            var bodyBytes         = MessageOctets.ToOctets(canonicalizedBody);

            // §3.5, l=: "This value MUST NOT be larger than the actual number of octets in the
            // canonicalized message body" - and §3.7: the body is hashed "canonicalized using the
            // body canonicalization algorithm specified in the "c=" tag and then truncated to the
            // length specified in the "l=" tag".
            if (bodyLength > bodyBytes.Length)
                return (DkimResult.PermError, $"l={bodyLength} is larger than the body of {bodyBytes.Length} octets (RFC 6376 §3.5)");

            var hashedLength      = (Int32) (bodyLength ?? bodyBytes.Length);
            var computedBodyHash  = Convert.ToBase64String(SHA256.HashData(bodyBytes.AsSpan(0, hashedLength)));

            // §3.5, bh=: "Whitespace is ignored in this value".
            if (!DkimCanonicalization.SameBase64(computedBodyHash, bodyHash))
                return (DkimResult.Fail, $"Body hash mismatch: expected {bodyHash}, got {computedBodyHash}");

            // §6.1.3: "All data beyond that limit is not validated by DKIM. Hence, Verifiers might
            // treat a message that contains bytes beyond the indicated body length with suspicion
            // and can choose to treat the signature as if it were invalid (e.g., by returning
            // PERMFAIL (unsigned content))" - what follows l= could be anyone's (§8.2).
            if (hashedLength < bodyBytes.Length)
                return (DkimResult.Fail, $"The signature covers {hashedLength} of the {bodyBytes.Length} octets of the body, l={bodyLength} (RFC 6376 §6.1.3: unsigned content)");

            try
            {
                var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
                var signatureBytes = Convert.FromBase64String(signature.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", ""));

                var signedHeaders = headerList.Split(':');
                var signingInput  = DkimCanonicalization.BuildHeaderHashInput(fields, signedHeaders, dkimField, headerCanon);

                Boolean isValid;

                // Ed25519 (RFC 8463): the key is its 32 octets, the signature is over the
                // SHA-256 hash of the input - and the algorithm has to be the key's.
                if (algorithm.Equals("ed25519-sha256", StringComparison.OrdinalIgnoreCase) || keyType == "ed25519")
                {

                    if (!algorithm.Equals("ed25519-sha256", StringComparison.OrdinalIgnoreCase) || keyType != "ed25519")
                        return (DkimResult.Fail, $"The signature is {algorithm}, the key in DNS is {keyType}");

                    if (publicKeyBytes.Length != Ed25519PublicKeyParameters.KeySize)
                        return (DkimResult.Fail, "An Ed25519 key in DNS has 32 octets");

                    var hash     = SHA256.HashData(MessageOctets.ToOctets(signingInput));
                    var verifier = new Ed25519Signer();
                    verifier.Init(false, new Ed25519PublicKeyParameters(publicKeyBytes, 0));
                    verifier.BlockUpdate(hash, 0, hash.Length);
                    isValid      = verifier.VerifySignature(signatureBytes);

                }
                else
                {

                    using var rsa = RSA.Create();
                    rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                    // RFC 8301 §3.2: "Verifiers MUST NOT consider signatures using RSA keys of less
                    // than 1024 bits as valid signatures".
                    if (rsa.KeySize < 1024)
                        return (DkimResult.Fail, $"An RSA key of {rsa.KeySize} bits (RFC 8301 §3.2: at least 1024)");

                    isValid = rsa.VerifyData(MessageOctets.ToOctets(signingInput), signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                }

                return isValid
                    ? (DkimResult.Pass, $"DKIM signature valid for domain {domain}")
                    : (DkimResult.Fail, "Signature verification failed");
            }
            catch (Exception ex)
            {
                return (DkimResult.Fail, $"Signature verification error: {ex.Message}");
            }
        }

        /// <summary>
        /// The tags of a DKIM-Signature, from its value as read for the hash - one character per
        /// octet. The values are UTF-8: RFC 6376 §3.5 is amended by RFC 8616 §5, d=, i= and s= of
        /// an EAI message may be in U-labels - and "sig.bücher.test" is not "sig.bã¼cher.test".
        /// </summary>
        private static Dictionary<string, string> ParseDkimHeader(string header)
            => ParseDkimHeader(header, out _);

        /// <summary>
        /// What is wrong with the t=, x=, l= and q= of a DKIM-Signature (RFC 6376 §3.5), or null;
        /// and the body length its l= gives, or null for the whole body.
        /// </summary>
        private static String? OptionalTagError(Dictionary<String, String> Tags, out Int64? BodyLength)
        {

            BodyLength = null;

            // sig-t-tag = %x74 [FWS] "=" [FWS] 1*12DIGIT, and sig-x-tag the same; "implementations
            // MAY consider any value longer than 12 digits to be infinite".
            Int64? timestamp  = null;
            Int64? expiration = null;

            if (Tags.TryGetValue("t", out var t))
            {
                if (!IsDigits(t))
                    return $"t={t} is no timestamp (RFC 6376 §3.5)";
                timestamp = SecondsOf(t);
            }

            if (Tags.TryGetValue("x", out var x))
            {
                if (!IsDigits(x))
                    return $"x={x} is no expiration (RFC 6376 §3.5)";
                expiration = SecondsOf(x);
            }

            // "The value of the "x=" tag MUST be greater than the value of the "t=" tag if both
            // are present."
            if (expiration <= timestamp)
                return $"x={x} is not after t={t} (RFC 6376 §3.5)";

            // sig-l-tag = %x6c [FWS] "=" [FWS] 1*76DIGIT - more than 18 digits are more octets
            // than any body has.
            if (Tags.TryGetValue("l", out var l))
            {
                if (!IsDigits(l) || l.Length > 76)
                    return $"l={l} is no body length (RFC 6376 §3.5)";
                BodyLength = l.Length > 18 ? Int64.MaxValue : Int64.Parse(l, System.Globalization.CultureInfo.InvariantCulture);
            }

            // sig-q-tag = %x71 [FWS] "=" [FWS] sig-q-tag-method *([FWS] ":" [FWS] sig-q-tag-method);
            // "Currently, the only valid value is "dns/txt""; "The only option defined for the
            // "dns" query type is "txt", which MUST be included"; "Unrecognized query mechanisms
            // MUST be ignored" - and with none left there is no way to the key.
            if (Tags.TryGetValue("q", out var q))
            {

                var methods = q.Split(':').Select(method => method.Trim()).ToList();

                if (methods.Any(method => method.Length == 0))
                    return $"q={q} is no list of query methods (RFC 6376 §3.5)";

                if (methods.Any(method => method.Split('/')[0].Equals("dns", StringComparison.OrdinalIgnoreCase) &&
                                          !method.Equals("dns/txt", StringComparison.OrdinalIgnoreCase)))
                    return $"q={q}: the option \"txt\" of the query type \"dns\" MUST be included (RFC 6376 §3.5)";

                if (!methods.Any(method => method.Equals("dns/txt", StringComparison.OrdinalIgnoreCase)))
                    return $"q={q} names no query method the verifier knows (RFC 6376 §3.5: dns/txt)";

            }

            return null;

        }

        private static Boolean IsDigits(String Value)
            => Value.Length > 0 && Value.All(Char.IsAsciiDigit);

        private static Int64 SecondsOf(String Digits)
            => Digits.Length > 12 ? Int64.MaxValue : Int64.Parse(Digits, System.Globalization.CultureInfo.InvariantCulture);

        /// <param name="DuplicateTag">The name of a tag the tag-list names more than once, or null.</param>
        private static Dictionary<string, string> ParseDkimHeader(string header, out String? DuplicateTag)
            => ParseTagList(Utf8OfOctets(header).Replace("\r\n", "").Replace("\n", "").Replace("\t", " "), out DuplicateTag);

        /// <summary>
        /// A tag-list (RFC 6376 §3.2): tag-spec *( ";" tag-spec ) [ ";" ], tag-spec = [FWS]
        /// tag-name [FWS] "=" [FWS] tag-value [FWS]. "Tags MUST be interpreted in a case-sensitive
        /// manner" - "D=" is no "d=" - and "Tags with duplicate names MUST NOT occur within a single
        /// tag-list; if a tag name does occur more than once, the entire tag-list is invalid": the
        /// first such name is given back, for the caller to refuse the tag-list.
        /// </summary>
        private static Dictionary<string, string> ParseTagList(String TagList, out String? DuplicateTag)
        {

            var result   = new Dictionary<string, string>(StringComparer.Ordinal);
            DuplicateTag = null;

            foreach (var part in TagList.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key   = trimmed[..eqIndex].Trim();
                    var value = trimmed[(eqIndex + 1)..].Trim();
                    if (!result.TryAdd(key, value))
                        DuplicateTag ??= key;
                }
            }

            return result;

        }

        /// <summary>
        /// The name of the first tag of a tag-list, or null.
        /// </summary>
        private static String? FirstTagName(String TagList)
        {

            var first = TagList.Split(';', StringSplitOptions.RemoveEmptyEntries).
                                Select(tag => tag.Trim()).
                                FirstOrDefault(tag => tag.Length > 0);

            if (first is null)
                return null;

            var equals = first.IndexOf('=');

            return equals > 0 ? first[..equals].Trim() : null;

        }

        private static readonly UTF8Encoding StrictUtf8 = new (encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>
        /// Text read one character per octet, decoded as the UTF-8 it is; as it is when it is
        /// ASCII, or no UTF-8.
        /// </summary>
        private static String Utf8OfOctets(String Octets)
        {

            if (Octets.All(c => c < 0x80) || Octets.Any(c => c > 0xFF))
                return Octets;

            try
            {
                return StrictUtf8.GetString(Encoding.Latin1.GetBytes(Octets));
            }
            catch (DecoderFallbackException)
            {
                return Octets;
            }

        }

        private static Dictionary<string, string> ParseDkimRecord(string record)
            => ParseTagList(record, out _);

        private static Dictionary<string, string> ParseDkimRecord(string record, out String? DuplicateTag)
            => ParseTagList(record, out DuplicateTag);

        #endregion

        #region DMARC Verification

        /// <summary>
        /// Evaluate DMARC (RFC 7489) for the RFC5322.From domain: locate the policy record
        /// (falling back to the organizational domain), then test SPF/DKIM identifier
        /// alignment. DMARC passes when at least one of SPF or DKIM produced a "pass" whose
        /// authenticated domain is aligned with the From domain. On failure the applicable
        /// policy (p or, for a sub-domain using the org-domain record, sp) is returned after
        /// pct sampling.
        /// </summary>
        /// <param name="fromDomain">The RFC5322.From header domain.</param>
        /// <param name="spfAuthDomain">The MAIL FROM domain SPF authenticated.</param>
        /// <param name="spfResult">The SPF result.</param>
        /// <param name="dkimResult">The DKIM result.</param>
        /// <param name="dkimPassing">The d= domain of every DKIM signature that holds.</param>
        /// <param name="envelopeFromDomain">The RFC5321.MailFrom domain; empty for a null reverse-path, when SPF checked the HELO identity.</param>
        /// <param name="spfRecord">The SPF record evaluated, if there was one.</param>
        /// <param name="dkimSignatures">Every DKIM signature tried, with its d=, i=, s= and result.</param>
        private async Task<(DmarcResult Result, string? Policy, DmarcEvaluation? Detail)> VerifyDmarcAsync(
            string                               fromDomain,
            string                               spfAuthDomain,
            SPFResult                            spfResult,
            DkimResult                           dkimResult,
            IReadOnlyList<String>                dkimPassing,
            string                               envelopeFromDomain,
            string?                              spfRecord,
            IReadOnlyList<DkimSignatureOutcome>  dkimSignatures,
            CancellationToken                    ct)
        {
            try
            {
                if (string.IsNullOrEmpty(fromDomain))
                    return (DmarcResult.None, null, null);

                // RFC 8616 §6: "If the domain name in the RFC5322.From field is an IDN, all U-labels
                // in the domain are converted to A-labels before further processing" - the record
                // looked up is the A-labels', and the identifiers are aligned by their A-labels.
                fromDomain = Idna.ToAscii(fromDomain);

                // DMARC record for the From domain itself, else the organizational domain
                // (RFC 7489 §6.6.3). The org-domain record's sp= governs sub-domains. The
                // Organizational Domain is asked "If the set is now empty": when the From domain
                // answered that it has no record - a lookup that failed answered nothing, and its
                // record, which may say otherwise, is not replaced by another one.
                var lookup        = await LookupDmarcRecordsAsync($"_dmarc.{fromDomain}", ct);
                var policyDomain  = fromDomain;
                var usedOrgRecord = false;

                if (!lookup.Failed && lookup.Records.Length == 0)
                {
                    var orgDomain = PublicSuffixList.GetOrganizationalDomain(fromDomain);
                    if (!orgDomain.Equals(fromDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        lookup        = await LookupDmarcRecordsAsync($"_dmarc.{orgDomain}", ct);
                        policyDomain  = orgDomain;
                        usedOrgRecord = true;
                    }
                }

                if (lookup.Failed)
                {
                    Logger.Log(LogLevel.Info, $"DMARC: the record of {policyDomain} could not be read - no policy applied");
                    return (DmarcResult.TempError, null, null);
                }

                // "If the remaining set contains multiple records or no records, policy discovery
                // terminates and DMARC processing is not applied to this message."
                if (lookup.Records.Length != 1)
                {
                    if (lookup.Records.Length > 1)
                        Logger.Log(LogLevel.Info, $"DMARC: {lookup.Records.Length} records at {policyDomain} - no policy applied");
                    return (DmarcResult.None, null, null);
                }

                var record = lookup.Records[0];

                Logger.Log(LogLevel.Debug, $"DMARC record for {fromDomain}: {record}");

                var dmarc  = ParseDmarcRecord(record);

                // "If a retrieved policy record does not contain a valid "p" tag, or contains an
                // "sp" tag that is not valid, then: 1. if a "rua" tag is present and contains at
                // least one syntactically valid reporting URI, the Mail Receiver SHOULD act as if a
                // record containing a valid "v" tag and "p=none" was retrieved, and continue
                // processing; 2. otherwise, the Mail Receiver applies no DMARC processing to this
                // message."
                if (!dmarc.PolicyValid)
                {

                    if (!HasValidReportUri(dmarc.Rua))
                    {
                        Logger.Log(LogLevel.Info, $"DMARC: the record of {policyDomain} has no valid policy and no rua - no policy applied");
                        return (DmarcResult.None, null, null);
                    }

                    dmarc = dmarc with { Policy = "none", SubdomainPolicy = null };

                }

                var policy = (usedOrgRecord && dmarc.SubdomainPolicy is not null)
                                 ? dmarc.SubdomainPolicy
                                 : dmarc.Policy;

                // --- identifier alignment (RFC 7489 §3.1) - any DKIM signature that holds will do (§4.2) ---
                var dkimDomain  = dkimPassing.FirstOrDefault(domain => IsAligned(domain, fromDomain, dmarc.StrictDkim)) ??
                                  dkimPassing.FirstOrDefault();
                var spfAligned  = spfResult  == SPFResult.Pass  && IsAligned(spfAuthDomain, fromDomain, dmarc.StrictSpf);
                var dkimAligned = dkimResult == DkimResult.Pass && IsAligned(dkimDomain,    fromDomain, dmarc.StrictDkim);

                var pass            = spfAligned || dkimAligned;

                // RFC 7489 §6.6.2: "If neither passes and one or both of them fail due to a
                // temporary error, the Receiver evaluating the message is unable to conclude that
                // the DMARC mechanism had a permanent failure; they therefore cannot apply the
                // advertised DMARC policy." A temporary error of a check whose domain is not
                // aligned changes nothing: it could not have passed DMARC.
                var temporary       = !pass &&
                                      ((spfResult == SPFResult.TempError && IsAligned(spfAuthDomain, fromDomain, dmarc.StrictSpf)) ||
                                       dkimSignatures.Any(signature => signature.Result == DkimResult.TempError &&
                                                                       IsAligned(signature.Domain, fromDomain, dmarc.StrictDkim)));

                var effectivePolicy = pass || temporary ? policy : ApplyPct(policy, dmarc.Percent);
                var disposition     = !pass && !temporary
                                          ? effectivePolicy switch {
                                                "reject"     => DmarcDisposition.Reject,
                                                "quarantine" => DmarcDisposition.Quarantine,
                                                _            => DmarcDisposition.None
                                            }
                                          : DmarcDisposition.None;

                var detail = new DmarcEvaluation(
                    HeaderFromDomain: fromDomain,
                    PolicyDomain:     policyDomain,
                    RequestedPolicy:  dmarc.Policy,
                    SubdomainPolicy:  dmarc.SubdomainPolicy,
                    EffectivePolicy:  effectivePolicy,
                    Percent:          dmarc.Percent,
                    StrictSpf:        dmarc.StrictSpf,
                    StrictDkim:       dmarc.StrictDkim,
                    Rua:              dmarc.Rua,
                    Ruf:              dmarc.Ruf,
                    FailureOptions:   dmarc.FailureOptions,
                    SpfResult:        spfResult,
                    SpfDomain:        string.IsNullOrEmpty(spfAuthDomain) ? null : spfAuthDomain,
                    SpfAligned:       spfAligned,
                    DkimResult:       dkimResult,
                    DkimDomain:       dkimDomain,
                    DkimAligned:      dkimAligned,
                    Result:           pass      ? DmarcResult.Pass
                                    : temporary ? DmarcResult.TempError
                                                : DmarcResult.Fail,
                    Disposition:      disposition,
                    EnvelopeFromDomain: envelopeFromDomain,
                    SpfOnHelo:          envelopeFromDomain.Length == 0,
                    SpfRecord:          spfRecord,
                    DkimSignatures:     dkimSignatures
                );

                return (detail.Result, effectivePolicy, detail);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"DMARC verification error for {fromDomain}: {ex.Message}");
                return (DmarcResult.TempError, null, null);
            }
        }

        // RFC 7489 §3.1.1/§3.1.2: an authenticated identifier is aligned with the From domain
        // if it matches exactly (strict) or shares the same organizational domain (relaxed).
        private static bool IsAligned(string? authDomain, string fromDomain, bool strict)
        {
            if (string.IsNullOrEmpty(authDomain) || string.IsNullOrEmpty(fromDomain))
                return false;

            // A domain in U-labels and the same domain in A-labels are aligned (RFC 8616 §6).
            authDomain = Idna.ToAscii(authDomain.TrimEnd('.'));
            fromDomain = Idna.ToAscii(fromDomain.TrimEnd('.'));

            if (authDomain.Equals(fromDomain, StringComparison.OrdinalIgnoreCase))
                return true;   // exact match satisfies both strict and relaxed

            if (strict)
                return false;

            return PublicSuffixList.GetOrganizationalDomain(authDomain)
                       .Equals(PublicSuffixList.GetOrganizationalDomain(fromDomain), StringComparison.OrdinalIgnoreCase);
        }

        // pct% of failing messages get the published policy; the remainder are demoted one
        // step: reject -> quarantine, quarantine -> none (RFC 7489 §6.6.4: "If the email is not
        // subject to the "reject" policy (due to the "pct" tag), the Mail Receiver SHOULD treat
        // the email as though the "quarantine" policy applies"). pct=0 selects no message, so
        // under p=reject every one is quarantined - not let through as under p=none.
        private static string ApplyPct(string policy, int percent)
        {
            if (percent >= 100) return policy;
            if (percent >  0 && Random.Shared.Next(100) < percent) return policy;

            return policy switch {
                "reject"     => "quarantine",
                "quarantine" => "none",
                _            => "none"
            };
        }

        private readonly record struct DmarcRecord(
            bool    PolicyValid,
            string  Policy,
            string? SubdomainPolicy,
            bool    StrictSpf,
            bool    StrictDkim,
            int     Percent,
            string? Rua,
            string? Ruf,
            string? FailureOptions
        );

        /// <summary>
        /// RFC 7489 §7.1: before sending a report to a destination outside the policy domain's
        /// organizational domain, verify the destination has consented by publishing a
        /// "v=DMARC1" TXT record at <c>&lt;policy-domain&gt;._report._dmarc.&lt;destination&gt;</c>.
        /// Destinations within the same organizational domain are authorized implicitly.
        /// </summary>
        public async Task<bool> IsExternalReportingAuthorizedAsync(
            string            policyDomain,
            string            destinationDomain,
            CancellationToken ct = default)

            => (await CheckExternalReportingAsync(policyDomain, destinationDomain, ct)).Authorized;

        /// <summary>
        /// RFC 7489 §7.1: whether a destination outside the policy domain's organizational
        /// domain consented to its reports (see <see cref="IsExternalReportingAuthorizedAsync"/>),
        /// and the rua and ruf its own record carries: "If a "rua" or "ruf" tag is thus
        /// discovered, replace the corresponding value extracted from the domain's DMARC policy
        /// record with the one found in this record". A destination within the organizational
        /// domain consents implicitly and replaces nothing.
        /// </summary>
        public async Task<(Boolean Authorized, String? Rua, String? Ruf)> CheckExternalReportingAsync(
            string            policyDomain,
            string            destinationDomain,
            CancellationToken ct = default)
        {
            if (PublicSuffixList.GetOrganizationalDomain(policyDomain)
                    .Equals(PublicSuffixList.GetOrganizationalDomain(destinationDomain), StringComparison.OrdinalIgnoreCase))
                return (true, null, null);

            var authName = $"{policyDomain}._report._dmarc.{destinationDomain}";
            var record   = (await LookupDmarcRecordsAsync(authName, ct)).Records.FirstOrDefault();
            if (record is null)
                return (false, null, null);

            var tags = ParseDmarcRecord(record);
            return (true, tags.Rua, tags.Ruf);
        }

        private static DmarcRecord ParseDmarcRecord(string record)
        {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in record.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = part.IndexOf('=');
                if (eq > 0)
                    tags[part[..eq].Trim()] = part[(eq + 1)..].Trim();
            }

            // RFC 7489 §6.3, §6.4: p= is required, and p= and sp= are "none", "quarantine" or
            // "reject" (dmarc-request).
            static bool IsPolicy(string? value) => value?.ToLowerInvariant() is "none" or "quarantine" or "reject";

            tags.TryGetValue("p",  out var requested);
            tags.TryGetValue("sp", out var subPolicy);

            var valid  = IsPolicy(requested) && (subPolicy is null || IsPolicy(subPolicy));
            var policy = IsPolicy(requested) ? requested!.ToLowerInvariant() : "none";

            var percent = 100;
            if (tags.TryGetValue("pct", out var pctStr) && int.TryParse(pctStr, out var pctVal))
                percent = Math.Clamp(pctVal, 0, 100);

            bool Strict(string tag) => tags.TryGetValue(tag, out var v) &&
                                       v.Trim().Equals("s", StringComparison.OrdinalIgnoreCase);

            return new DmarcRecord(
                valid,
                policy,
                IsPolicy(subPolicy) ? subPolicy!.ToLowerInvariant() : null,
                Strict("aspf"),
                Strict("adkim"),
                percent,
                tags.GetValueOrDefault("rua"),
                tags.GetValueOrDefault("ruf"),
                tags.GetValueOrDefault("fo")
            );
        }

        /// <summary>
        /// Whether a rua value has at least one syntactically valid reporting URI (RFC 7489
        /// §6.4: dmarc-uri = URI [ "!" 1*DIGIT [ "k" / "m" / "g" / "t" ] ], comma-separated).
        /// </summary>
        private static Boolean HasValidReportUri(String? Rua)

            => Rua is not null &&
               Rua.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).
                   Select(uri => ReportUriSizeLimit.Replace(uri, "")).
                   Any   (uri => Uri.TryCreate(uri, UriKind.Absolute, out _));

        private static readonly Regex ReportUriSizeLimit = new (@"![0-9]+[kmgtKMGT]?$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether a TXT record is a DMARC record: it starts with a v= tag whose value is
        /// "DMARC1" - precisely, RFC 7489 §6.3: "It MUST have the value of "DMARC1". The value
        /// of this tag MUST match precisely", and §6.4: dmarc-version = "v" *WSP "=" *WSP %x44
        /// %x4d %x41 %x52 %x43 %x31, followed by the separator or the end.
        /// </summary>
        internal static Boolean IsDmarcRecord(String Record)
            => DmarcVersion.IsMatch(Record);

        private static readonly Regex DmarcVersion = new (@"^[Vv][ \t]*=[ \t]*DMARC1[ \t]*(;|$)", RegexOptions.CultureInvariant);

        #endregion

        #region DNS Helpers

        /// <summary>
        /// The DMARC records at a name (RFC 7489 §6.6.3): of its TXT records those that are
        /// DMARC records - "Records that do not start with a "v=" tag that identifies the
        /// current version of DMARC are discarded". Failed when the lookup failed - a DNS error
        /// other than NXDOMAIN, or no answer at all: §6.6.3 sets "any indication that there is
        /// no such record" apart from "a transient DNS error".
        /// </summary>
        private async Task<(Boolean Failed, String[] Records)> LookupDmarcRecordsAsync(String Name, CancellationToken ct)
        {

            // DNSServiceName (not DomainName) so underscore labels like _dmarc.* validate.
            if (DNSServiceName.TryParse(Name.TrimEnd('.')) is not { } serviceName)
                return (false, []);

            DNSInfo response;

            try
            {
                response = await DNSClient.Query(serviceName, [ DNSResourceRecordTypes.TXT ], CancellationToken: ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Debug, $"TXT lookup failed for {Name}: {ex.Message}");
                return (true, []);
            }

            if (response.IsTimeout)
                return (true, []);

            return response.ResponseCode switch {
                DNSResponseCodes.NoError    => (false, response.Answers.OfType<TXT>().Select(txt => txt.Text).Where(IsDmarcRecord).ToArray()),
                DNSResponseCodes.NameError  => (false, []),
                _                           => (true,  [])
            };

        }

        /// <summary>
        /// The DKIM key record at selector._domainkey.domain (RFC 6376 §3.6.1): its v= tag is
        /// "RECOMMENDED, default is "DKIM1"", so a record without one is a key record too; one
        /// that begins with v= and another value "MUST be discarded". Of several records, the
        /// first with a p= tag (§6.1.2 lets the verifier choose). Failed when the lookup failed -
        /// a DNS error other than NXDOMAIN, a timeout, no answer at all: §6.1.2 sets a query that
        /// "fails to respond" apart from one that finds "the corresponding key record does not
        /// exist".
        /// </summary>
        /// <returns>Whether the lookup failed, and the key record - null when there is none.</returns>
        private async Task<(Boolean Failed, String? Record)> GetDkimKeyRecordAsync(String domain, CancellationToken ct)
        {

            DNSInfo response;

            try
            {
                response = await DNSClient.Query(
                                     DNSServiceName.Parse(domain),
                                     [ DNSResourceRecordTypes.TXT ],
                                     CancellationToken: ct
                                 );
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Debug, $"TXT lookup failed for {domain}: {ex.Message}");
                return (true, null);
            }

            if (response.IsTimeout)
                return (true, null);

            if (response.ResponseCode == DNSResponseCodes.NameError)
                return (false, null);

            if (response.ResponseCode != DNSResponseCodes.NoError)
                return (true, null);

            var records = response.Answers.
                              OfType<TXT>().
                              Select(txt => txt.Text).
                              Where (IsDkimKeyRecord).
                              ToArray();

            return (false,
                    records.FirstOrDefault(record => ParseDkimRecord(record).ContainsKey("p")) ??
                    records.FirstOrDefault());

        }

        /// <summary>
        /// Whether a TXT record may be a DKIM key record: it does not begin with a v= tag, or
        /// that tag is "DKIM1" - compared as a string, "DKIM1.0" is not it (RFC 6376 §3.6.1).
        /// </summary>
        internal static Boolean IsDkimKeyRecord(String Record)
        {

            var first   = Record.Split(';', StringSplitOptions.RemoveEmptyEntries).
                                 Select(tag => tag.Trim()).
                                 FirstOrDefault(tag => tag.Length > 0);

            if (first is null)
                return false;

            var equals  = first.IndexOf('=');

            return equals <= 0 ||
                   !first[..equals].Trim().Equals("v", StringComparison.Ordinal) ||
                   first[(equals + 1)..].Trim() == "DKIM1";

        }

        public async Task<String[]> GetMxRecordsAsync(String domain, CancellationToken ct)
        {
            try
            {
                var response = await DNSClient.Query(
                                         DomainName.Parse(domain),
                                         [ DNSResourceRecordTypes.MX ],
                                         CancellationToken: ct
                                     );

                // Ordered by preference (lowest = highest priority), returned as bare hostnames.
                return response.Answers.
                           OfType<MX>().
                           OrderBy(mx => mx.Preference).
                           Select(mx => mx.Exchange.FullName.TrimEnd('.')).
                           ToArray();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Debug, $"MX lookup failed for {domain}: {ex.Message}");
                return [];
            }
        }

        #endregion

    }

}
