﻿/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    // https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/

    // => https://dns.cloudflare.com/dns-query

    /// <summary>
    /// Some Cloudflare DNS HTTPS POST tests.
    /// </summary>
    [TestFixture]
    public class CloudflareHTTPS_POST_Tests2 : ADNSTests
    {

        [OneTimeSetUp]
        public void InitTests()
        {

            client  = DNSHTTPSClient.Cloudflare_DNSName(
                          Mode:       DNSHTTPSMode.POST,
                          DNSClient:  new DNSClient(
                                          SearchForIPv4DNSServers: true,
                                          SearchForIPv6DNSServers: false
                                      )
                      );

        }

    }

}
