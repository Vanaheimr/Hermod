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

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS
{

    [TestFixture]
    public class TLSWardenTests
    {

        private static X509Certificate2 CreateSelfSigned(String          Subject,
                                                         DateTimeOffset  NotBefore,
                                                         DateTimeOffset  NotAfter)
        {

            using var key     = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var       request = new CertificateRequest($"CN={Subject}", key, HashAlgorithmName.SHA256);

            using var cert    = request.CreateSelfSigned(NotBefore, NotAfter);

            return X509CertificateLoader.LoadCertificate(cert.RawData);

        }


        [Test]
        public void ExpiredCertificate_ClampsDaysUntilExpiryToZeroAndMarksExpired()
        {

            var now = DateTimeOffset.UtcNow;

            using var expiredCert = CreateSelfSigned("expired.example.test", now.AddDays(-30), now.AddDays(-5));
            using var chain       = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.Build(expiredCert);

            var result   = new CheckResult("https://expired.example.test", ExpireOk: 14);
            var callback = TLS_Warden.CreateRemoteCertificateValidationFunc(result)!;

            var accepted = callback(null, expiredCert, chain, SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.Multiple(() => {

                Assert.That(accepted, Is.True);
                Assert.That(result.CertificateInfos, Has.Count.EqualTo(1));

                var info = result.CertificateInfos[0];
                Assert.That(info.DaysUntilExpiry, Is.EqualTo(0U), "expired certificates must clamp to 0 days instead of underflowing UInt32");
                Assert.That(info.IsExpired,       Is.True,        "expired certificates must be marked IsExpired");

                Assert.That(result.ErrorMessages.Any(m => m.Contains("https://expired.example.test")),
                            Is.True,
                            "chain error message should reference Check.URL rather than the CheckResult type name");

                Assert.That(result.ErrorMessages.Any(m => m.Contains("expired on:")),
                            Is.True,
                            "expired certificate should record an expiration error");

            });

        }


        [Test]
        public void ExpiringCertificate_HonorsConfiguredExpireOkThreshold()
        {

            var now = DateTimeOffset.UtcNow;

            using var cert  = CreateSelfSigned("soon.example.test", now.AddDays(-1), now.AddDays(20));
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.Build(cert);

            var strictResult = new CheckResult("https://soon.example.test", ExpireOk: 30);
            TLS_Warden.CreateRemoteCertificateValidationFunc(strictResult)!(null, cert, chain, SslPolicyErrors.None);

            var lenientResult = new CheckResult("https://soon.example.test", ExpireOk: 10);
            TLS_Warden.CreateRemoteCertificateValidationFunc(lenientResult)!(null, cert, chain, SslPolicyErrors.None);

            Assert.Multiple(() => {

                Assert.That(strictResult.CertificateInfos[0].IsExpired,  Is.True);
                Assert.That(strictResult.Warnings,                       Has.Count.EqualTo(1));
                Assert.That(strictResult.Warnings[0],                    Does.Not.EndWith(")"));

                Assert.That(lenientResult.CertificateInfos[0].IsExpired, Is.False);
                Assert.That(lenientResult.Warnings,                      Is.Empty);
                Assert.That(lenientResult.Messages,                      Has.Count.EqualTo(1));

            });

        }


        [Test]
        public void EmptyChain_DoesNotThrow()
        {

            var now = DateTimeOffset.UtcNow;

            using var cert       = CreateSelfSigned("empty-chain.example.test", now.AddDays(-1), now.AddDays(30));
            using var emptyChain = new X509Chain();

            var result = new CheckResult("https://empty-chain.example.test");

            Assert.That(
                () => TLS_Warden.CreateRemoteCertificateValidationFunc(result)!(null, cert, emptyChain, SslPolicyErrors.None),
                Throws.Nothing
            );

            Assert.That(result.IsChainValid, Is.Not.Null);

        }

    }

}
