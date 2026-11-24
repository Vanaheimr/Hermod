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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 4954 §4: the base64 of SASL challenges and responses - the alphabet, groups of four,
    /// the pad character at the end only; "=" and the empty text are no octets.
    /// </summary>
    [TestFixture]
    public class SaslBase64Tests
    {

        [TestCase("",                           "")]
        [TestCase("=",                          "")]
        [TestCase("Zm9v",                       "foo")]
        [TestCase("Zm9vYg==",                   "foob")]
        [TestCase("Zm9vYmE=",                   "fooba")]
        [TestCase("AGFsaWNlAHNlY3JldA==",       "\0alice\0secret")]
        public void Base64_is_decoded(String Text, String Octets)

            => Assert.That(SaslBase64.Decode(Text), Is.EqualTo(Encoding.UTF8.GetBytes(Octets)));


        [TestCase("Zm9 v",          TestName = "A space inside")]
        [TestCase("Zm9v ",          TestName = "A space after it")]
        [TestCase(" Zm9v",          TestName = "A space before it")]
        [TestCase("Zm\t9v",         TestName = "A tab inside")]
        [TestCase("Zm\r\n9v",       TestName = "A line end inside")]
        [TestCase("Zm9vYg",         TestName = "Without its padding")]
        [TestCase("Zm9=Yg==",       TestName = "A pad character inside")]
        [TestCase("=Zm9",           TestName = "A pad character first")]
        [TestCase("Zm9vY===",       TestName = "Three pad characters")]
        [TestCase("Zm9-",           TestName = "The URL alphabet")]
        [TestCase("!!!!",           TestName = "Not the alphabet at all")]
        [TestCase("Zm9vö===",       TestName = "Not ASCII")]
        public void What_is_no_base64_is_refused(String Text)

            => Assert.That(SaslBase64.Decode(Text), Is.Null);

    }

}
