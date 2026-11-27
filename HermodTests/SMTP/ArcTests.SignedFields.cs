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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The header fields the AMS of the sealer signs (RFC 8617 §4.1.2: the AMS "is syntactically
    /// and semantically identical to a DKIM-Signature header field", and RFC 6376 §5.4): From
    /// whatever the configuration names. The finding A-7 of SMTPConformanceTests.
    /// </summary>
    public partial class ArcTests
    {

        private String SealedWith(String SignedHeaders)

            => Sealer(SignedHeaders).Seal(Message(), "forwarder.example", "spf=pass smtp.mailfrom=origin.example", ArcResult.None);

        /// <summary>
        /// The names in the h= of the AMS the sealer put on top.
        /// </summary>
        private static String[] AmsSignedFieldsOf(String Sealed)
        {
            var field = Regex.Match(Sealed, $@"(?:^|\r\n){AMS}:(.*?)\r\n(?![ \t])", RegexOptions.Singleline).Groups[1].Value;
            return Regex.Match(field, @"(?:^|;)\s*h=([^;]*)").Groups[1].Value.
                       Split(':').Select(name => name.Trim()).Where(name => name.Length > 0).ToArray();
        }


        #region A-7: From signed whatever the configuration names

        [TestCase("to:subject", TestName = "A sealer configured with to:subject signs From all the same")]
        [TestCase("",           TestName = "A sealer configured with nothing signs From all the same")]
        public async Task A_sealer_configured_without_From_signs_From_all_the_same(String SignedHeaders)
        {

            var sealed_ = SealedWith(SignedHeaders);
            var result  = await Validate(sealed_);

            Assert.Multiple(() => {
                Assert.That(AmsSignedFieldsOf(sealed_).Count(name => name.Equals("from", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1), sealed_);
                Assert.That(result, Is.EqualTo(ArcResult.Pass), sealed_);
            });

        }


        [Test(Description = "RFC 8617 §4.1.2, RFC 6376 §5.4: the AMS of a sealer configured without From signs it, and a From changed after sealing breaks the chain")]
        public async Task A_From_changed_after_sealing_breaks_the_chain()
        {

            var changed = SealedWith("to:subject").Replace("From: <news@origin.example>", "From: <ceo@origin.example>");

            Assert.That(await Validate(changed), Is.EqualTo(ArcResult.Fail), changed);

        }


        [Test(Description = "RFC 8617 §4.1.2: a sealer configured with From signs it once, where the configuration names it")]
        public async Task A_sealer_configured_with_From_signs_it_once()
        {

            var sealed_ = SealedWith("to:From:subject");
            var result  = await Validate(sealed_);

            Assert.Multiple(() => {
                Assert.That(AmsSignedFieldsOf(sealed_), Is.EqualTo(new[] { "to", "From", "subject" }), sealed_);
                Assert.That(result,                     Is.EqualTo(ArcResult.Pass),                     sealed_);
            });

        }

        #endregion

    }

}
