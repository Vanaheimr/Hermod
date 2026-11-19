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

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// What the relay records for TLS-RPT (RFC 8460) about a session: through the smart host
    /// "localhost", whose MTA-STS policy (RFC 8461 §3.4) the test serves, to the recipient
    /// domain next.example. The finding T-4 of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region Setup

        private static String StsPolicyText(String Mode, String Mx = "localhost")
            => $"version: STSv1\r\nmode: {Mode}\r\nmx: {Mx}\r\nmax_age: 604800\r\n";

        /// <summary>
        /// A self-signed certificate for "localhost": trusted by nobody, and expired when so asked.
        /// </summary>
        private static X509Certificate2 SelfSignedLocalhost(Boolean Expired = false)
        {

            using var rsa  = RSA.Create(2048);
            var request    = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names      = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());

            using var cert = Expired
                                 ? request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1))
                                 : request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);

        }

        /// <param name="Mode">The mode of the smart host's MTA-STS policy, or null for none.</param>
        /// <param name="Certificate">The certificate of the smart host; null: it offers no STARTTLS.</param>
        /// <param name="Mx">The mx of the policy.</param>
        /// <param name="TrustCertificate">Trust any certificate, through a validator of the operator's own.</param>
        private static async Task<(SendResult Result, TlsRptEvent[] Events)> SendUnderPolicy(String?            Mode,
                                                                                            X509Certificate2?  Certificate,
                                                                                            String             Mx                = "localhost",
                                                                                            Boolean            TrustCertificate  = false)
        {

            using var nextHop = new NextHop(Certificate: Certificate);
            using var cts     = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var events        = new ConcurrentQueue<TlsRptEvent>();

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname               = "relay.hermod.test",
                                                    SmartHost                   = "localhost",
                                                    SmartHostPort               = nextHop.Port,
                                                    ConnectTimeoutMs            = 3_000,
                                                    ReadTimeoutMs               = 5_000,
                                                    WriteTimeoutMs              = 3_000,
                                                    MtaStsHttpHandler           = new PolicyHost { Domain = "localhost", Policy = StsPolicyText(Mode ?? "none", Mx) },
                                                    RemoteCertificateValidator  = TrustCertificate ? (_, _, _, _) => true : null
                                                },
                                                null,
                                                new PolicyDns { Domain = "localhost", Txt = Mode is null ? [] : [ "v=STSv1; id=20261119;" ] },
                                                new QuietLogger(),
                                                events.Enqueue);

            var result = await client.SendAsync(PolicyDomain, "sender@client.example", [ "you@next.example" ], "Subject: tlsrpt\r\n\r\nhello\r\n", ct: cts.Token);

            await Task.Delay(100);
            return (result, [ .. events ]);

        }

        private static String Explain(SendResult Result, TlsRptEvent[] Events)
            => $"{Result.Status} {Result.ResponseCode} {Result.ResponseText}; events: {String.Join(", ", Events.Select(e => e.ToString()))}";

        #endregion


        #region T-4: through a smart host

        [Test(Description = "RFC 8460 §4.4: \"policy-domain\" is \"The Policy Domain against which the MTA-STS or DANE policy is defined\" - through a smart host its own (RFC 8461 §3.4), not the recipient's")]
        public async Task Through_a_smart_host_the_session_is_reported_to_the_smart_hosts_domain()
        {

            var (result, events) = await SendUnderPolicy("enforce", SelfSignedLocalhost());

            Assert.That(events.Select(e => e.PolicyDomain), Is.EqualTo(new[] { "localhost" }), Explain(result, events));

        }

        #endregion

    }

}
