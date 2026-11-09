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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// TXT's and SPF's character-strings, each quoted or not, with RFC 1035
    /// §5.1's escapes in both; and what is refused, refused rather than read
    /// again as it lay.
    /// </summary>
    [TestFixture]
    public class CharacterString_Tests
    {

        #region Txt_ReadsEveryCharacterString(Written, Meant)

        [TestCase(@"""a"" ""b""",     new[] { "a", "b" })]
        [TestCase(@"a b",             new[] { "a", "b" })]
        [TestCase(@"""a"" b",         new[] { "a", "b" })]
        [TestCase(@"a ""b c""",       new[] { "a", "b c" })]
        [TestCase(@"""a""b",          new[] { "a", "b" })]
        [TestCase(@"a\;b",            new[] { "a;b" })]
        [TestCase(@"a\032b",          new[] { "a b" })]
        [TestCase(@"""say \""hi\"" now""", new[] { "say \"hi\" now" })]
        [TestCase(@"""\;12""",        new[] { ";12" })]
        [TestCase(@"""""",            new[] { "" })]
        public void Txt_ReadsEveryCharacterString(String Written, String[] Meant)
        {

            var txt = ADNSResourceRecord.ParseZoneFileString($"x.example. 3600 IN TXT {Written}") as TXT;

            Assert.That(txt!.Strings, Is.EqualTo(Meant));

        }

        #endregion

        #region Txt_RefusesWhatIsNoCharacterString(Written)

        [TestCase(@"""a\256b""")]
        [TestCase(@"a\256b")]
        [TestCase(@"""a\1x""")]
        [TestCase(@"""a\12""")]
        [TestCase(@"""ab\")]
        [TestCase(@"""ab\""")]
        [TestCase(@"""abc")]
        [TestCase(@"a""b")]
        public void Txt_RefusesWhatIsNoCharacterString(String Written)
        {
            Assert.That(ADNSResourceRecord.TryParseZoneFileString($"x.example. 3600 IN TXT {Written}", out _, out _), Is.False);
        }

        #endregion

        #region Txt_InGooglesJsonForm_IsOneText()

        /// <summary>
        /// Google's JSON API writes a TXT's data as the bare text, the strings
        /// joined, where Cloudflare's quotes each. The bare text is one text, and
        /// its presentation format reads back to the same text, also past 255
        /// octets and with a quote and a backslash in it.
        /// </summary>
        [Test]
        public void Txt_InGooglesJsonForm_IsOneText()
        {

            var text     = "v=DKIM1; k=rsa; p=" + new String('A', 300) + " \"quoted\" back\\slash";

            var json     = TXT.TryParseFromJSON(DomainName.Parse("x.example."), TimeSpan.FromHours(1), text);
            var written  = ADNSResourceRecord.ParseZoneFileString($"x.example. 3600 IN TXT {TXT.PresentationOf(text)}") as TXT;

            Assert.That(TXT.IsPresentationFormat(text, out _), Is.False);
            Assert.That(json!.Text,                            Is.EqualTo(text));
            Assert.That(written!.Text,                         Is.EqualTo(text));
            Assert.That(written.Strings.All(s => s.Length <= 255), Is.True);

        }

        #endregion

        #region Txt_JsonData_ThatDoesNotReadToTheEnd_IsOneText(Data)

        /// <summary>
        /// Google's text may begin with a quote. JSON data that does and still
        /// does not read as character-strings to the end is that text; data
        /// that does read to the end is Cloudflare's presentation format. A
        /// zone file has no second form, and refuses the first case.
        /// </summary>
        [TestCase("\"unterminated",       new[] { "\"unterminated" })]
        [TestCase("\"a\" \"unterminated", new[] { "\"a\" \"unterminated" })]
        [TestCase("\"a\" x \"b\"",        new[] { "a", "x", "b" })]
        public void Txt_JsonData_ThatDoesNotReadToTheEnd_IsOneText(String Data, String[] Meant)
        {

            var json = TXT.TryParseFromJSON(DomainName.Parse("x.example."), TimeSpan.FromHours(1), Data);

            Assert.That(json!.Strings, Is.EqualTo(Meant));

        }

        #endregion

        #region Spf_ReadsItsCharacterStringsAndJoinsThem(Written, Meant)

        /// <summary>
        /// RFC 7208 §3.3: several strings "MUST be treated as if those strings
        /// are concatenated together without adding spaces".
        /// </summary>
        [TestCase(@"""v=spf1 a\;b -all""",   "v=spf1 a;b -all")]
        [TestCase(@"""a\""b""",              "a\"b")]
        [TestCase(@"""v=spf1 "" ""-all""",   "v=spf1 -all")]
        [TestCase(@"v=spf1 -all",            "v=spf1-all")]
        public void Spf_ReadsItsCharacterStringsAndJoinsThem(String Written, String Meant)
        {

            var spf = ADNSResourceRecord.ParseZoneFileString($"x.example. 3600 IN SPF {Written}") as SPF;

            Assert.That(spf!.Rules, Is.EqualTo(Meant));

        }

        #endregion

    }

}
