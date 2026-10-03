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

using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// MAIL and RCPT parameters (RFC 5321 §4.1.1.11, §4.1.2): what is accepted, what is
    /// 501 (a known parameter with an invalid value) and what is 555 (not recognised or
    /// not implemented).
    /// </summary>
    [TestFixture]
    public class ESMTPParametersTests
    {

        private static Int32 Mail(String Parameters, Boolean ExtendedSmtp = true)
        {
            var (parsed, error) = ESMTPParameters.Parse(Parameters);
            return (error ?? ESMTPParameters.ValidateMail(parsed, ExtendedSmtp))?.Code ?? 250;
        }

        private static Int32 Rcpt(String Parameters, Boolean ExtendedSmtp = true)
        {
            var (parsed, error) = ESMTPParameters.Parse(Parameters);
            return (error ?? ESMTPParameters.ValidateRcpt(parsed, ExtendedSmtp))?.Code ?? 250;
        }


        [TestCase("",                                      250)]
        [TestCase("SIZE=12345",                            250)]
        [TestCase("size=12345 body=8bitmime",              250, TestName = "Keywords and values are case-insensitive")]
        [TestCase("BODY=7BIT",                             250)]
        [TestCase("SMTPUTF8",                              250)]
        [TestCase("SMTPUTF8 SMTPUTF8",                     250, TestName = "A repeated identical parameter is tolerated (smtplib does this)")]
        [TestCase("RET=HDRS ENVID=QQ314159+2B42",          250)]
        [TestCase("MT-PRIORITY=-9",                        250)]
        [TestCase("MT-PRIORITY=+3",                        250)]
        [TestCase("REQUIRETLS",                            250)]
        [TestCase("AUTH=<>",                               250)]
        [TestCase("AUTH=alice+40hermod.test",              250)]
        [TestCase("X-NO-SUCH-PARAM=1",                     555)]
        [TestCase("BODY=BINARYMIME",                       555, TestName = "BODY=BINARYMIME is not advertised, so 555")]
        [TestCase("SIZE=huge",                             501)]
        [TestCase("SIZE=123456789012345678901",            501, TestName = "SIZE has at most 20 digits")]
        [TestCase("SIZE",                                  501)]
        [TestCase("BODY=9BITMIME",                         501)]
        [TestCase("SMTPUTF8=yes",                          501)]
        [TestCase("RET=BODY",                              501)]
        [TestCase("ENVID=has+zzbadhex",                    501)]
        [TestCase("MT-PRIORITY=10",                        501)]
        [TestCase("SIZE=1 SIZE=2",                         501, TestName = "One parameter with two different values is 501")]
        [TestCase("SIZE=",                                 501)]
        [TestCase("-BAD=1",                                501)]
        public void Mail_parameters(String Parameters, Int32 Expected)
            => Assert.That(Mail(Parameters), Is.EqualTo(Expected));


        [Test]
        public void After_HELO_no_parameter_is_available()
            => Assert.That(Mail("SIZE=100", ExtendedSmtp: false), Is.EqualTo(555));


        [TestCase("",                                      250)]
        [TestCase("NOTIFY=SUCCESS,FAILURE",                250)]
        [TestCase("NOTIFY=never",                          250)]
        [TestCase("NOTIFY=DELAY ORCPT=rfc822;bob@example.org", 250)]
        [TestCase("NOTIFY=NEVER,SUCCESS",                  501, TestName = "NEVER must appear by itself (RFC 3461 §4.1)")]
        [TestCase("NOTIFY=SUCCESS,SUCCESS",                501)]
        [TestCase("NOTIFY=SOMETIMES",                      501)]
        [TestCase("ORCPT=bob@example.org",                 501, TestName = "ORCPT needs an addr-type")]
        [TestCase("X-NO-SUCH-PARAM",                       555)]
        public void Rcpt_parameters(String Parameters, Int32 Expected)
            => Assert.That(Rcpt(Parameters), Is.EqualTo(Expected));

    }

}
