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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Regression tests for certificate critical-option handling.
    ///
    /// <para>
    /// PROTOCOL.certkeys requires that a critical option a verifier does not <i>understand</i> causes the
    /// certificate to be refused. The M9 security review found <c>force-command</c>, <c>source-address</c>
    /// and <c>verify-required</c> listed as "known" purely so such certificates would pass — while
    /// nothing anywhere read their values. The effect was worse than not supporting them: a CA issuing
    /// <c>force-command="/usr/bin/backup-only"</c> believed it was constraining the holder, and the
    /// server silently granted unrestricted access instead.
    /// </para>
    ///
    /// <para>
    /// "Understood" must therefore mean "enforced". Until enforcement exists, these certificates are
    /// rejected — a loud failure the operator can see, rather than a silent escalation.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Security")]
    public class CertificateCriticalOptionTests
    {

        private static readonly DateTimeOffset Now = new (2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

        #region (private) issue a user certificate carrying the given critical options

        private static (SshCertificate Certificate, SshCertificateAuthorityTrust Trust) Issue(
            params (String Name, String Value)[] CriticalOptions)
        {

            var ca      = SshHostKey.GenerateEd25519();
            var subject = SshHostKey.GenerateEd25519();

            var builder = new OpenSshCertificateBuilder {
                Serial       = 1,
                Type         = SshCertType.User,
                KeyId        = "restricted",
                Principals   = [ "achim" ],
                ValidAfter   = Now.AddDays(-1),
                ValidBefore  = Now.AddDays(1)
            };

            foreach (var (name, value) in CriticalOptions)
                builder.CriticalOptions.Add(new KeyValuePair<String, Byte[]>(name, Encoding.UTF8.GetBytes(value)));

            return (builder.Sign(subject.PublicKeyBlob, ca),
                    new SshCertificateAuthorityTrust().TrustCA(ca));

        }

        #endregion


        #region UnenforcedCriticalOption_IsRejected

        /// <summary>
        /// Each of these encodes a restriction the CA intends to bind the holder to. Accepting the
        /// certificate while ignoring the restriction is privilege escalation relative to the CA's
        /// intent, so the certificate must be refused while enforcement is missing.
        /// </summary>
        [Test]
        [TestCase("force-command",   "/usr/bin/backup-only")]
        [TestCase("source-address",  "10.0.0.0/8")]
        [TestCase("verify-required", "")]
        public void UnenforcedCriticalOption_IsRejected(String Option, String Value)
        {

            var (certificate, trust) = Issue((Option, Value));

            var validation = SshCertificateValidator.Validate(certificate, SshCertType.User, "achim", trust, Now);

            Assert.Multiple(() => {

                Assert.That(validation.IsValid, Is.False,
                            $"a certificate carrying the unenforced critical option '{Option}' must be rejected, "
                            + "not accepted with the restriction silently dropped");

                Assert.That(validation.Reason, Does.Contain(Option).IgnoreCase,
                            "the rejection must name the option, so an operator can see why");

            });

        }

        #endregion

        #region CertificateWithoutCriticalOptions_StillValidates

        /// <summary>Failing closed must not break ordinary, unrestricted certificates.</summary>
        [Test]
        public void CertificateWithoutCriticalOptions_StillValidates()
        {

            var (certificate, trust) = Issue();

            var validation = SshCertificateValidator.Validate(certificate, SshCertType.User, "achim", trust, Now);

            Assert.That(validation.IsValid, Is.True, validation.Reason);

        }

        #endregion

        #region UnknownCriticalOption_IsStillRejected

        /// <summary>The original PROTOCOL.certkeys rule still holds for genuinely unknown options.</summary>
        [Test]
        public void UnknownCriticalOption_IsStillRejected()
        {

            var (certificate, trust) = Issue(("some-future-option@example.com", "x"));

            Assert.That(SshCertificateValidator.Validate(certificate, SshCertType.User, "achim", trust, Now).IsValid,
                        Is.False);

        }

        #endregion

    }

}
