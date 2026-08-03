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

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.PKI
{

    /// <summary>
    /// Reading the Subject Alternative Name extension of a certificate.
    ///
    /// These exist because of a defect that no test on the author's machine could have caught.
    /// The names used to be read by rendering the extension with AsnEncodedData.Format(), which
    /// delegates to the operating system — CryptFormatObject on Windows — and returns localized
    /// text: the same certificate produced "DNS-Name=host" on a German installation and
    /// "DNS Name=host" on an English one. Norn's NTS-KE hostname verification filtered on the
    /// German spelling, found nothing anywhere else, and rejected certificates that were
    /// perfectly valid. It took a CI runner set to en-US to see it.
    ///
    /// So the point of these tests is not that the decoder works, but that its output is decided
    /// here rather than by whatever language the host happens to be installed in.
    /// </summary>
    [TestFixture]
    public class SubjectAlternativeName_Tests
    {

        #region (private static) CertificateWith(SubjectAlternativeNames)

        /// <summary>
        /// A throwaway self-signed certificate carrying the given subject alternative names.
        /// Built with the BCL rather than the PKI factory: these tests are about decoding an
        /// extension, so the smallest thing that produces one is the right fixture.
        /// </summary>
        /// <param name="Build">Adds the alternative names under test.</param>
        private static X509Certificate2 CertificateWith(Action<SubjectAlternativeNameBuilder>? Build = null)
        {

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var request   = new CertificateRequest(
                                "CN=san.example.org",
                                key,
                                HashAlgorithmName.SHA256
                            );

            if (Build is not null)
            {
                var builder = new SubjectAlternativeNameBuilder();
                Build(builder);
                request.CertificateExtensions.Add(builder.Build());
            }

            return request.CreateSelfSigned(
                       DateTimeOffset.UtcNow.AddDays(-1),
                       DateTimeOffset.UtcNow.AddDays( 1)
                   );

        }

        #endregion


        #region GetDnsNames_ReturnsEveryEntryInOrder()

        /// <summary>
        /// Every dNSName, in the order the certificate carries them, and nothing else.
        /// A decoder that returns only the first name is the failure this guards: the old code
        /// fell back to GetNameInfo, which reports one name, so a certificate valid for its
        /// second or third name was refused.
        /// </summary>
        [Test]
        public void GetDnsNames_ReturnsEveryEntryInOrder()
        {

            var certificate = CertificateWith(builder => {
                                  builder.AddDnsName("first.example.org");
                                  builder.AddDnsName("second.example.org");
                                  builder.AddDnsName("third.example.org");
                              });

            Assert.That(certificate.GetDNSNames(),
                        Is.EqualTo(new[] {
                            "first.example.org",
                            "second.example.org",
                            "third.example.org"
                        }).AsCollection);

        }

        #endregion

        #region GetDnsNames_ReturnsTheNameItself_WithoutAPrefix()

        /// <summary>
        /// The value, not a rendering of it. Anything that has to be stripped before use is a
        /// prefix somebody will eventually get wrong.
        /// </summary>
        [Test]
        public void GetDnsNames_ReturnsTheNameItself_WithoutAPrefix()
        {

            var certificate = CertificateWith(builder => builder.AddDnsName("bare.example.org"));

            Assert.That(certificate.GetDNSNames().Single(), Is.EqualTo("bare.example.org"));

        }

        #endregion

        #region GetIPAddresses_ReturnsBothFamilies()

        /// <summary>iPAddress entries, IPv4 and IPv6 alike.</summary>
        [Test]
        public void GetIPAddresses_ReturnsBothFamilies()
        {

            var certificate = CertificateWith(builder => {
                                  builder.AddIpAddress(System.Net.IPAddress.Parse("192.0.2.10"));
                                  builder.AddIpAddress(System.Net.IPAddress.Parse("2001:db8::1"));
                              });

            Assert.That(certificate.GetIPAddresses().Select(ipAddress => ipAddress.ToString()),
                        Is.EqualTo(new[] { "192.0.2.10", "2001:db8::1" }).AsCollection);

        }

        #endregion

        #region NamesOfBothKinds_AreReportedSeparately()

        /// <summary>
        /// A certificate carrying both kinds: each accessor returns its own kind and does not
        /// leak the other. Hostname verification must never match a hostname against an address
        /// entry, so the separation is the point.
        /// </summary>
        [Test]
        public void NamesOfBothKinds_AreReportedSeparately()
        {

            var certificate = CertificateWith(builder => {
                                  builder.AddDnsName("mixed.example.org");
                                  builder.AddIpAddress(System.Net.IPAddress.Loopback);
                              });

            Assert.Multiple(() => {

                Assert.That(certificate.GetDNSNames(),
                            Is.EqualTo(new[] { "mixed.example.org" }).AsCollection);

                Assert.That(certificate.GetIPAddresses().Select(ipAddress => ipAddress.ToString()),
                            Is.EqualTo(new[] { "127.0.0.1" }).AsCollection);

            });

        }

        #endregion

        #region ACertificateWithoutTheExtension_HasNoNames()

        /// <summary>
        /// No extension is not an error. A caller asking "which names?" gets none and can fall
        /// back; throwing here would turn a certificate without SANs into a handshake failure.
        /// </summary>
        [Test]
        public void ACertificateWithoutTheExtension_HasNoNames()
        {

            var certificate = CertificateWith();

            Assert.Multiple(() => {
                Assert.That(certificate.GetDNSNames(),                     Is.Empty);
                Assert.That(certificate.GetIPAddresses(),                  Is.Empty);
                Assert.That(certificate.DecodeSubjectAlternativeNames(),   Is.Empty);
            });

        }

        #endregion

        #region AMalformedExtension_YieldsNoNamesRatherThanThrowing()

        /// <summary>
        /// A certificate can carry an extension that does not decode. These read like property
        /// accesses, and a remote peer controls the bytes, so the answer is "no names" — not an
        /// exception out of the middle of certificate validation.
        /// </summary>
        [Test]
        public void AMalformedExtension_YieldsNoNamesRatherThanThrowing()
        {

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var request   = new CertificateRequest("CN=malformed.example.org", key, HashAlgorithmName.SHA256);

            // Not valid DER for a GeneralNames sequence.
            request.CertificateExtensions.Add(
                new X509Extension(
                    new Oid("2.5.29.17"),
                    [ 0xFF, 0xFF, 0xFF ],
                    critical: false
                )
            );

            var certificate = request.CreateSelfSigned(
                                  DateTimeOffset.UtcNow.AddDays(-1),
                                  DateTimeOffset.UtcNow.AddDays( 1)
                              );

            Assert.Multiple(() => {
                Assert.That(() => certificate.GetDNSNames().   ToArray(), Throws.Nothing);
                Assert.That(() => certificate.GetIPAddresses().ToArray(), Throws.Nothing);
                Assert.That(certificate.GetDNSNames(),                    Is.Empty);
            });

        }

        #endregion

        #region DecodeSubjectAlternativeNames_UsesFixedPrefixes()

        /// <summary>
        /// The string form, for the callers that still use it: exactly "DNS-Name=" and
        /// "IP-Address=", written by this library.
        ///
        /// This is the regression test for the original defect, and it is worth knowing where it
        /// bites: on a German installation the old implementation produced these same strings,
        /// so a revert would still pass here — and fail the moment CI runs it in English. That
        /// is precisely how the defect escaped in the first place.
        /// </summary>
        [Test]
        public void DecodeSubjectAlternativeNames_UsesFixedPrefixes()
        {

            var certificate = CertificateWith(builder => {
                                  builder.AddDnsName("prefixed.example.org");
                                  builder.AddIpAddress(System.Net.IPAddress.Parse("192.0.2.10"));
                              });

            Assert.That(certificate.DecodeSubjectAlternativeNames(),
                        Is.EqualTo(new[] {
                            "DNS-Name=prefixed.example.org",
                            "IP-Address=192.0.2.10"
                        }).AsCollection);

        }

        #endregion

        #region TheDecodedNames_DoNotDependOnTheCurrentCulture()

        /// <summary>
        /// Identical output under cultures that break naive string handling — tr-TR for its
        /// dotless i, de-DE for the culture this was written in, and the invariant culture.
        ///
        /// This pins the managed half of the problem only: the original defect came from the
        /// operating system's UI language, which no CurrentCulture setting reaches. The other
        /// half is pinned by not asking the operating system at all.
        /// </summary>
        [Test]
        public void TheDecodedNames_DoNotDependOnTheCurrentCulture()
        {

            var certificate = CertificateWith(builder => {
                                  builder.AddDnsName("İstanbul.example.org");
                                  builder.AddDnsName("plain.example.org");
                                  builder.AddIpAddress(System.Net.IPAddress.Parse("192.0.2.10"));
                              });

            var original    = CultureInfo.CurrentCulture;
            var results     = new List<String[]>();

            try
            {
                foreach (var culture in new[] { "tr-TR", "de-DE", "en-US", "" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(culture);
                    results.Add([.. certificate.DecodeSubjectAlternativeNames()]);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }

            Assert.Multiple(() => {
                foreach (var result in results)
                    Assert.That(result, Is.EqualTo(results[0]).AsCollection);
            });

        }

        #endregion

    }

}
