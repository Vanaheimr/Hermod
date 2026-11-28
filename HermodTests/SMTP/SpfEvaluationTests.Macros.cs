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
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The macros of an SPF record as check_host() expands them (RFC 7208 §7.1, §7.3): the
    /// transformers, a "/" as delimiter, the "p" macro. The findings V-36 to V-38 of
    /// SMTPConformanceTests. Each record names, by a macro, the name it looks up under
    /// "exists:", and that name is published: check_host() passes when the macro expands to it.
    /// </summary>
    public partial class SpfEvaluationTests
    {

        #region Transformers (V-36)

        private static readonly SpfMacroContext MacroContext = new ("postmaster@a.b.example", "postmaster", "a.b.example", "a.b.example");

        private static String Expand(String Macro)
            => SpfMacros.Expand(Macro, "a.b.example", System.Net.IPAddress.Parse(Client), MacroContext);

        [TestCase("%{dR}",                       "example.b.a")]
        [TestCase("%{d2R}",                      "b.a")]
        [TestCase("%{d1R}",                      "a")]
        [TestCase("%{dr}",                       "example.b.a")]
        [TestCase("%{d2}",                       "b.example")]
        [TestCase("%{d02}",                      "b.example")]
        [TestCase("%{d127}",                     "a.b.example")]
        [TestCase("%{d2147483647}",              "a.b.example")]
        [TestCase("%{d2147483648}",              "a.b.example")]
        [TestCase("%{d99999999999999999999}",    "a.b.example")]
        [TestCase("%{DR}",                       "example.b.a")]
        [Test(Description = "RFC 7208 §7.1: transformers = *DIGIT [ \"r\" ], an ABNF string and so case-insensitive (RFC 5234 §2.3); §7.3: \"if the value specifies more parts than are available, all the available parts are used\"")]
        public void A_transformer_is_applied(String Macro, String Expanded)
        {
            Assert.That(Expand(Macro), Is.EqualTo(Expanded));
        }

        [TestCase("%{dR}",                       "example.b.a")]
        [TestCase("%{d2R}",                      "b.a")]
        [TestCase("%{d02}",                      "b.example")]
        [TestCase("%{d99999999999999999999}",    "a.b.example")]
        [Test(Description = "RFC 7208 §7.3: the transformers as check_host() applies them, in the name exists: looks up")]
        public async Task A_transformer_is_applied_by_check_host(String Macro, String Expanded)
        {
            var dns = new ZoneDNS().Txt("a.b.example", $"v=spf1 exists:{Macro}.check.example -all").
                                    A  ($"{Expanded}.check.example", "127.0.0.2");

            Assert.That(await CheckHost(dns, "a.b.example"), Is.EqualTo(SPFResult.Pass));
        }

        [TestCase("v=spf1 exists:%{d0}.check.example -all")]
        [TestCase("v=spf1 exists:%{d00}.check.example -all")]
        [TestCase("v=spf1 exists:%{d0r}.check.example -all")]
        [TestCase("v=spf1 a:%{d0}.check.example -all")]
        [TestCase("v=spf1 redirect=%{d0}.check.example")]
        [TestCase($"v=spf1 ip4:{Client} exp=%{{d0}}.check.example -all")]
        [TestCase($"v=spf1 ip4:{Client} foo=%{{d0}} -all")]
        [Test(Description = "RFC 7208 §7.3: \"If a DIGIT is specified, the value MUST be nonzero\" - a DIGIT of 0 is a syntax error, wherever it is, and check_host() a \"permerror\" (§4.6)")]
        public async Task A_digit_of_zero_is_a_syntax_error(String Record)
        {
            var dns = new ZoneDNS().Txt("a.b.example", Record).
                                    A  ("a.b.example.check.example", Client).
                                    Txt("a.b.example.check.example", $"v=spf1 ip4:{Client} -all");

            Assert.That(await CheckHost(dns, "a.b.example"), Is.EqualTo(SPFResult.PermError));
        }

        #endregion

        #region "/" as a delimiter (V-37)

        [TestCase("v=spf1 a:%{d/}.a.example -all",            "a.b.example.a.example",  Client)]
        [TestCase("v=spf1 a:%{d/}.a.example/24 -all",         "a.b.example.a.example",  "192.0.2.1")]
        [TestCase("v=spf1 a:%{d/}.a.example/24//64 -all",     "a.b.example.a.example",  "192.0.2.1")]
        [TestCase("v=spf1 a:%{d./}.a.example -all",           "a.b.example.a.example",  Client)]
        [TestCase("v=spf1 a:%{d2/}.a.example -all",           "a.b.example.a.example",  Client)]
        [TestCase("v=spf1 a:%{d}.a.example/24 -all",          "a.b.example.a.example",  "192.0.2.1")]
        [Test(Description = "RFC 7208 §7.1: delimiter = \".\" / \"-\" / \"+\" / \",\" / \"/\" / \"_\" / \"=\" - a \"/\" within the braces of a macro is a delimiter, and the prefix length of \"a\" begins at the first \"/\" outside them")]
        public async Task A_slash_as_delimiter_in_the_domain_spec_of_a(String Record, String Name, String Address)
        {
            var dns = new ZoneDNS().Txt("a.b.example", Record).
                                    A  (Name, Address);

            Assert.That(await CheckHost(dns, "a.b.example"), Is.EqualTo(SPFResult.Pass));
        }

        [TestCase("v=spf1 mx:%{d/}.mx.example -all",          Client)]
        [TestCase("v=spf1 mx:%{d/}.mx.example/24 -all",       "192.0.2.1")]
        [Test(Description = "RFC 7208 §5.4: mx = \"mx\" [ \":\" domain-spec ] [ dual-cidr-length ] - a \"/\" within the braces of a macro is a delimiter, as for a")]
        public async Task A_slash_as_delimiter_in_the_domain_spec_of_mx(String Record, String Address)
        {
            var dns = new ZoneDNS().Txt("a.b.example", Record).
                                    Mx ("a.b.example.mx.example", 10, "mail.mx.example").
                                    A  ("mail.mx.example", Address);

            Assert.That(await CheckHost(dns, "a.b.example"), Is.EqualTo(SPFResult.Pass));
        }

        [TestCase("v=spf1 a:%{d/}.a.example/33 -all")]
        [TestCase("v=spf1 a:%{d}.a.example/33 -all")]
        [TestCase("v=spf1 mx:%{d/}.mx.example//129 -all")]
        [TestCase("v=spf1 a:%{d/.a.example -all")]
        [Test(Description = "RFC 7208 §4.6: a prefix length out of range after a macro, and a macro without its closing brace are syntax errors still, and check_host() a \"permerror\"")]
        public async Task A_prefix_length_after_a_macro_is_checked(String Record)
        {
            var dns = new ZoneDNS().Txt("a.b.example", Record).
                                    A  ("a.b.example.a.example", Client);

            Assert.That(await CheckHost(dns, "a.b.example"), Is.EqualTo(SPFResult.PermError));
        }

        #endregion

        #region The "p" macro (V-38)

        private static ZoneDNS ReverseOf(ZoneDNS DNS, params (String Name, String Address)[] Names)
        {
            foreach (var (name, address) in Names)
                DNS.Ptr("25.2.0.192.in-addr.arpa", name).
                    A  (name, address);
            return DNS;
        }

        private static ZoneDNS PMacro(String Domain, String Expanded)
            => new ZoneDNS().Txt(Domain, "v=spf1 exists:%{p}.p.example -all").
                             A  ($"{Expanded}.p.example", "127.0.0.2");

        [Test(Description = "RFC 7208 §7.3: \"The \"p\" macro expands to the validated domain name of <ip>\"; \"If there are no validated domain names or if a DNS error occurs, the string \"unknown\" is used\"")]
        public async Task The_p_macro_is_the_validated_name_of_the_client()
        {
            await Assert.MultipleAsync(async () => {

                Assert.That(await CheckHost(ReverseOf(PMacro("x.example", "mail.x.example"), ("mail.x.example", Client)),       "x.example"), Is.EqualTo(SPFResult.Pass),  "a validated name");
                Assert.That(await CheckHost(PMacro("x.example", "unknown"),                                                    "x.example"), Is.EqualTo(SPFResult.Pass),  "no reverse name");
                Assert.That(await CheckHost(ReverseOf(PMacro("x.example", "unknown"), ("mail.x.example", "192.0.2.99")),       "x.example"), Is.EqualTo(SPFResult.Pass),  "a reverse name without the client's address");
                Assert.That(await CheckHost(PMacro("x.example", "unknown").Error("25.2.0.192.in-addr.arpa", DNSResourceRecordTypes.PTR),
                                                                                                                               "x.example"), Is.EqualTo(SPFResult.Pass),  "a DNS error of the PTR lookup");
                Assert.That(await CheckHost(ReverseOf(PMacro("x.example", "mail.x.example"), ("mail.x.example", Client)).
                                                                                     Error("mail.x.example", DNSResourceRecordTypes.A),
                                                                                                                               "x.example"), Is.EqualTo(SPFResult.Fail),  "a DNS error of the A lookup: the name is skipped, and %{p} unknown");

            });
        }

        [Test(Description = "RFC 7208 §7.3: \"If the <domain> is present in the list of validated domains, it SHOULD be used. Otherwise, if a subdomain of the <domain> is present, it SHOULD be used. Otherwise, any name from the list can be used\"")]
        public async Task The_p_macro_prefers_the_domain_and_its_subdomains()
        {
            await Assert.MultipleAsync(async () => {

                Assert.That(await CheckHost(ReverseOf(PMacro("x.example", "x.example"),      ("host.other.example", Client), ("x.example", Client)),      "x.example"), Is.EqualTo(SPFResult.Pass), "the domain");
                Assert.That(await CheckHost(ReverseOf(PMacro("x.example", "mail.x.example"), ("host.other.example", Client), ("mail.x.example", Client)), "x.example"), Is.EqualTo(SPFResult.Pass), "a subdomain");
                Assert.That(await CheckHost(ReverseOf(PMacro("x.example", "host.other.example"), ("host.other.example", Client)),                        "x.example"), Is.EqualTo(SPFResult.Pass), "another name");

            });
        }

        [Test(Description = "RFC 7208 §7.3: uppercase macros \"are then URL escaped\" - %{P} is the validated name, which has nothing to escape")]
        public async Task The_upper_case_p_macro_is_the_validated_name_too()
        {
            var dns = ReverseOf(new ZoneDNS().Txt("x.example", "v=spf1 exists:%{P}.p.example -all").
                                              A  ("mail.x.example.p.example", "127.0.0.2"),
                                ("mail.x.example", Client));

            Assert.That(await CheckHost(dns, "x.example"), Is.EqualTo(SPFResult.Pass));
        }

        [Test(Description = "RFC 7208 §4.6.4: \"When evaluating the \"ptr\" mechanism or the %{p} macro, the number of \"PTR\" resource records queried is included in the overall limit of 10\"")]
        public async Task The_PTR_query_of_the_p_macro_counts_against_the_limit_of_ten()
        {

            static ZoneDNS Terms(Int32 Count)
            {
                var dns = new ZoneDNS().Txt("x.example", "v=spf1 " + String.Join(' ', Enumerable.Range(1, Count).Select(i => $"a:e{i}.x.example")) + " exists:%{p}.p.example -all");
                for (var i = 1; i <= Count; i++)
                    dns.A($"e{i}.x.example", "192.0.2.99");
                return dns;
            }

            await Assert.MultipleAsync(async () => {
                Assert.That(await CheckHost(Terms(8), "x.example"), Is.EqualTo(SPFResult.Fail),       "eight a, the exists and its PTR query: ten");
                Assert.That(await CheckHost(Terms(9), "x.example"), Is.EqualTo(SPFResult.PermError),  "nine a, the exists and its PTR query: eleven");
            });

        }

        [TestCase("%{p}",       true)]
        [TestCase("%{P2r}",     true)]
        [TestCase("x.%{p}.y",   true)]
        [TestCase("%%{p}",      false)]
        [TestCase("%{d}.p",     false)]
        [TestCase("%-%_p",      false)]
        [Test(Description = "Whether a macro-string has a %{p} - the PTR lookup is made for it only")]
        public void Uses_finds_a_macro_of_the_letter(String MacroString, Boolean Expected)
        {
            Assert.That(SpfMacros.Uses(MacroString, 'p'), Is.EqualTo(Expected));
        }

        #endregion

    }

}
