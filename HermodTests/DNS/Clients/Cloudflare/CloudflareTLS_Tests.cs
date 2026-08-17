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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients.Cloudflare
{

    // https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-tls/
    // one.one.one.one

    // https://dnscrypt.info

    // https://developers.cloudflare.com/1.1.1.1/infrastructure/extended-dns-error-codes/

    /// <summary>
    /// Some Cloudflare DNS TLS tests.
    /// </summary>
    [TestFixture]
    public class CloudflareTLS_Tests : ADNSTests
    {

        [OneTimeSetUp]
        public void InitTests()
        {

            // Was Cloudflare_DNSName, and that one failed every run rather than
            // every other one - a different symptom of the same cause. A name
            // resolves to an A record and an AAAA record;
            // IPVersionPreference.PreferIPv6 is the default, being the enum's
            // first member and therefore the value when nobody sets one; and its
            // fallback reads "if no IPv6 address is available", where here one
            // is available and merely unroutable. Cloudflare_DNSName offers no
            // PreferIPv4 to say otherwise, so this goes the way of the TCP
            // fixtures instead.
            //
            // What is given up with the name is the SNI and the name check in
            // the certificate. What is gained is a fixture that measures DoT
            // rather than the host's IPv6 situation.
            client = DNSTLSClient.Cloudflare_Random_IPv4(
                         RemoteCertificateValidator:   TLSValidationExtensions.AskTheOS,
                         LoggerFactory:                logs
                     );

        }

    }

}
