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
    /// Domain names in U-labels and A-labels (RFC 5890), and the local domains of the server.
    /// </summary>
    [TestFixture]
    public class IdnaTests
    {

        [TestCase("bücher.test",            "xn--bcher-kva.test")]
        [TestCase("mx.bücher.test.",        "mx.xn--bcher-kva.test.")]
        [TestCase("München.example",        "xn--mnchen-3ya.example")]
        [TestCase("xn--bcher-kva.test",     "xn--bcher-kva.test")]
        [TestCase("Mixed.Case.example",     "Mixed.Case.example")]
        [TestCase("[192.0.2.1]",            "[192.0.2.1]")]
        [TestCase("",                       "")]
        public void ToAscii(String Domain, String Ascii)
            => Assert.That(Idna.ToAscii(Domain), Is.EqualTo(Ascii));


        [TestCase("xn--bcher-kva.test",     "bücher.test")]
        [TestCase("mx.xn--bcher-kva.test.", "mx.bücher.test.")]
        [TestCase("bücher.test",            "bücher.test")]
        [TestCase("hermod.test",            "hermod.test")]
        public void ToUnicode(String Domain, String Unicode)
            => Assert.That(Idna.ToUnicode(Domain), Is.EqualTo(Unicode));


        [TestCase("bücher.test",        "xn--bcher-kva.test",   true)]
        [TestCase("BÜCHER.test",        "XN--BCHER-KVA.TEST.",  true)]
        [TestCase("bücher.test",        "bucher.test",          false)]
        [TestCase("hermod.test",        "HERMOD.test",          true)]
        public void SameDomain(String A, String B, Boolean Same)
            => Assert.That(Idna.SameDomain(A, B), Is.EqualTo(Same));


        [TestCase("xn--bcher-kva.test", "bücher.test",          true)]
        [TestCase("bücher.test",        "xn--bcher-kva.test",   true)]
        [TestCase("bücher.test",        "XN--BCHER-KVA.test",   true)]
        [TestCase("hermod.test",        "Hermod.TEST",          true)]
        [TestCase("bücher.test",        "bucher.test",          false)]
        public void A_local_domain_is_local_in_either_form(String Configured, String Domain, Boolean Local)
            => Assert.That(new SMTPServerConfig { Hostname = "mx.hermod.test", LocalDomains = [ Configured ] }.IsLocalDomain(Domain), Is.EqualTo(Local));


        [TestCase("bücher.test")]
        [TestCase("xn--bcher-kva.test")]
        public void A_local_domain_check_is_asked_about_each_form(String Domain)
        {

            var asked  = new List<String>();
            var config = new SMTPServerConfig { Hostname = "mx.hermod.test", LocalDomainCheck = domain => { asked.Add(domain); return domain == "bücher.test"; } };

            Assert.That(config.IsLocalDomain(Domain), Is.True, String.Join(", ", asked));

        }

    }

}
