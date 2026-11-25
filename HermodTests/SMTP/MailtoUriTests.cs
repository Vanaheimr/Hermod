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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The mailto URIs DMARC (RFC 7489 §6.2) and TLS-RPT (RFC 8460 §3) name their report
    /// destinations by: RFC 6068 §2, percent-encoding and all, and no URI where RFC 3986 has
    /// none. The findings R-8, R-9, T-5 and T-6 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class MailtoUriTests
    {

        [TestCase("mailto:dmarc@client.example",                                  new[] { "dmarc@client.example" },                       TestName = "mailto: an address of the usual kind")]
        [TestCase("MAILTO:dmarc@client.example",                                  new[] { "dmarc@client.example" },                       TestName = "mailto: the scheme in upper case")]
        [TestCase("mailto:dmarc%21reports@client.example",                        new[] { "dmarc!reports@client.example" },               TestName = "mailto: an exclamation point percent-encoded")]
        [TestCase("mailto:dmarc%2breports@client.example",                        new[] { "dmarc+reports@client.example" },               TestName = "mailto: lower-case hex digits")]
        [TestCase("mailto:a@client.example%2Cb@client.example",                   new[] { "a@client.example", "b@client.example" },        TestName = "mailto: two addresses, the comma encoded")]
        [TestCase("mailto:%22a%2Cb%22@client.example",                            new[] { "\"a,b\"@client.example" },                     TestName = "mailto: a comma in a quoted local part")]
        [TestCase("mailto:%22a%20b%22@client.example",                            new[] { "\"a b\"@client.example" },                     TestName = "mailto: a space in a quoted local part")]
        [TestCase("mailto:r%C3%A9ports@client.example",                           new[] { "r\u00E9ports@client.example" },                TestName = "mailto: UTF-8 percent-encoded")]
        [TestCase("mailto:dmarc@client.example?subject=DMARC%20report",           new[] { "dmarc@client.example" },                       TestName = "mailto: a subject is no part of the address")]
        [TestCase("mailto:?to=dmarc@client.example",                              new[] { "dmarc@client.example" },                       TestName = "mailto: the address in a to header field")]
        [TestCase("mailto:a@client.example?to=b@client.example&body=x",           new[] { "a@client.example", "b@client.example" },        TestName = "mailto: an address in to and one in a to header field")]
        [TestCase("mailto:dmarc@[192.0.2.1]",                                     new[] { "dmarc@[192.0.2.1]" },                          TestName = "mailto: a domain literal")]
        [Description("RFC 6068 §2: mailtoURI = \"mailto:\" [ to ] [ hfields ], to = addr-spec *(\",\" addr-spec ), its characters percent-encoded in UTF-8; RFC 7489 §6.2: a comma or an exclamation point of the URI's own \"MUST be encoded per Section 2.1 of [URI]\"")]
        public void A_mailto_URI_names_its_addresses(String Uri, String[] Addresses)

            => Assert.That(MailtoUri.Addresses(Uri), Is.EqualTo(Addresses));


        [TestCase("mailto:dmarc@client.example\r\nBcc: victim@client.example",    TestName = "No mailto URI: a CR LF in it")]
        [TestCase("mailto:dmarc@client.example\nBcc: victim@client.example",      TestName = "No mailto URI: an LF in it")]
        [TestCase("mailto:dmarc@client.example\rBcc: victim@client.example",      TestName = "No mailto URI: a CR in it")]
        [TestCase("mailto:dmarc @client.example",                                 TestName = "No mailto URI: a space in it")]
        [TestCase("mailto:r\u00E9ports@client.example",                           TestName = "No mailto URI: a non-ASCII character in it")]
        [TestCase("mailto:dmarc%ZZ@client.example",                               TestName = "No mailto URI: a % that is no percent-encoding")]
        [TestCase("mailto:dmarc%C3@client.example",                               TestName = "No mailto URI: octets that are no UTF-8")]
        [TestCase("https://reports.client.example/dmarc",                         TestName = "No mailto URI: another scheme")]
        [Description("RFC 3986 §2: a URI has no line break, no space, no control and no non-ASCII character, and a \"%\" begins a percent-encoding - a URI without one is none, and names no address")]
        public void Not_a_mailto_URI_names_no_address(String Uri)

            => Assert.That(MailtoUri.Addresses(Uri), Is.Empty);


        [TestCase("mailto:dmarc%0D%0ABcc:%20victim@client.example",               TestName = "No address: a line break percent-encoded")]
        [TestCase("mailto:dmarc%00@client.example",                               TestName = "No address: a NUL percent-encoded")]
        [TestCase("mailto:dmarc",                                                 TestName = "No address: no domain")]
        [TestCase("mailto:@client.example",                                       TestName = "No address: no local part")]
        [TestCase("mailto:a..b@client.example",                                   TestName = "No address: two dots")]
        [TestCase("mailto:",                                                      TestName = "No address: none at all")]
        [Description("RFC 6068 §2: addr-spec = local-part \"@\" domain, local-part = dot-atom-text / quoted-string - what decodes to no addr-spec is no address, a line break least of all")]
        public void What_is_no_addr_spec_is_no_address(String Uri)

            => Assert.That(MailtoUri.Addresses(Uri), Is.Empty);


        [TestCase("dmarc@client.example",          "mailto:dmarc@client.example")]
        [TestCase("dmarc!reports@client.example",  "mailto:dmarc%21reports@client.example")]
        [TestCase("\"a,b\"@client.example",        "mailto:%22a%2Cb%22@client.example")]
        [TestCase("r\u00E9ports@client.example",   "mailto:r%C3%A9ports@client.example")]
        [Description("RFC 6068 §2, RFC 7489 §6.2: the mailto URI of an address - \"!\" and \",\" encoded, which DMARC takes for delimiters of its own - names that address again")]
        public void The_mailto_URI_of_an_address_names_it(String Address, String Uri)
        {

            Assert.That(MailtoUri.Of(Address),                      Is.EqualTo(Uri));
            Assert.That(MailtoUri.Addresses(MailtoUri.Of(Address)), Is.EqualTo(new[] { Address }));

        }

    }

}
