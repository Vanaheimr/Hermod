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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients.Google
{

    /// <summary>
    /// Some Google DNS HTTPS JSON tests.
    /// </summary>
    [TestFixture]
    public class GoogleHTTPS_JSON_Tests : ADNSTests
    {

        [OneTimeSetUp]
        public void InitTests()
        {

            // IPv4Only. The endpoint here is a name, it resolves to an A record
            // and an AAAA record, and IPVersionPreference.PreferIPv6 takes the AAAA -
            // it is the default, being the enum first member and therefore the value
            // when nobody sets one, and its fallback reads "if no IPv6 address is
            // available", where here one is available and merely unroutable. On a host
            // without an IPv6 route that cost the full query timeout, reported as a
            // server failure. There is no _Random_IPv4 to reach for on this transport:
            // DoH goes to a URL, not to an address.
            client  = DNSHTTPSClient.Google(
                          Mode:                         DNSHTTPSMode.JSON,
                          RemoteCertificateValidator:   TLSValidationExtensions.AskTheOS,
                          PreferIPv4:                   IPVersionPreference.IPv4Only,
                          DNSClient:                    new DNSClient(
                                                            SearchForIPv4DNSServers: true,
                                                            SearchForIPv6DNSServers: false
                                                        ),
                          LoggerFactory:                logs
                      );

        }

    }

}
