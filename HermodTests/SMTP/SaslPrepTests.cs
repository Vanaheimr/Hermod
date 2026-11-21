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
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// SASLprep (RFC 4013), and the saslname of SCRAM (RFC 5802 §5.1, §7).
    /// </summary>
    [TestFixture]
    public class SaslPrepTests
    {

        // RFC 4013 §3, Examples - and the steps of §2 one by one.
        [TestCase("I­X",        "IX",        Description = "RFC 4013 §3: SOFT HYPHEN mapped to nothing")]
        [TestCase("user",            "user",      Description = "RFC 4013 §3: no transformation")]
        [TestCase("USER",            "USER",      Description = "RFC 4013 §3: case preserved, will not match #2")]
        [TestCase("ª",          "a",         Description = "RFC 4013 §3: output is NFKC, input in ISO 8859-1")]
        [TestCase("Ⅸ",          "IX",        Description = "RFC 4013 §3: output is NFKC, will match #1")]
        [TestCase("pass word",  "pass word", Description = "§2.1: a non-ASCII space is SPACE")]
        [TestCase("pass word",  "pass word", Description = "§2.1: EM SPACE is SPACE")]
        [TestCase("a‍b",        "ab",        Description = "§2.1: ZERO WIDTH JOINER mapped to nothing")]
        [TestCase("Äpfel",     "Äpfel", Description = "§2.2: composed by NFKC")]
        [TestCase("אב",    "אב", Description = "§2.4: right-to-left only")]
        [TestCase("ا1ب",   "ا1ب", Description = "§2.4: right-to-left at both ends, a digit between")]
        [TestCase("",                "",          Description = "the empty string")]
        public void Prepare(String Value, String Expected)
        {
            Assert.That(SaslPrep.Prepare(Value), Is.EqualTo(Expected));
        }


        [TestCase("\u0007",          Description = "RFC 4013 §3: prohibited character")]
        [TestCase("ا1",    Description = "RFC 4013 §3: violates bidirectional rules")]
        [TestCase("aא",         Description = "RFC 3454 §6: left-to-right and right-to-left characters together")]
        [TestCase("",          Description = "C.3: private use")]
        [TestCase("￾",          Description = "C.4: non-character")]
        [TestCase("�",          Description = "C.6: inappropriate for plain text")]
        [TestCase("‎",          Description = "C.8: change display properties")]
        [TestCase("\u0085",          Description = "C.2.2: a non-ASCII control character")]
        public void Prepare_refuses(String Value)
        {
            Assert.That(SaslPrep.Prepare(Value), Is.Null);
        }


        [Test(Description = "C.5: a surrogate on its own - in code, as attribute arguments, which are UTF-8, cannot carry one")]
        public void Prepare_refuses_a_surrogate_on_its_own()
        {
            Assert.That(SaslPrep.Prepare("a" + (Char) 0xD800 + "b"), Is.Null);
        }


        [Test(Description = "SCRAM's Normalize: a password SASLprep refuses is used as it is")]
        public void Normalize_keeps_a_password_SASLprep_refuses()
        {
            Assert.That(SaslPrep.Normalize("tab\there"), Is.EqualTo("tab\there"));
        }


        [TestCase("alice",      "alice")]
        [TestCase("a=3Db",      "a=b")]
        [TestCase("a=2Cb",      "a,b")]
        [TestCase("a=2c=3db",   "a,=b")]
        [TestCase("a=",         null)]
        [TestCase("a=3",        null)]
        [TestCase("a=b",        null)]
        [TestCase("a=41",       null)]
        [Test(Description = "RFC 5802 §5.1, n: \"The characters ',' or '=' in usernames are sent as '=2C' and '=3D' respectively. If the server receives a username that contains '=' not followed by either '2C' or '3D', then the server MUST fail the authentication\"")]
        public void DecodeSaslName(String Sent, String? Name)
        {
            Assert.That(ScramSha256AuthHandler.DecodeSaslName(Sent), Is.EqualTo(Name));
        }

    }

}
