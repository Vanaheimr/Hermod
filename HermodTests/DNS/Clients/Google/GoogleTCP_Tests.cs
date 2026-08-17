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

    // ARSoft.Tools.Net

    /// <summary>
    /// Some Google DNS TCP tests.
    /// </summary>
    [TestFixture]
    public class GoogleTCP_Tests : ADNSTests
    {

        /// <remarks>
        /// <c>_IPv4</c>, as the UDP fixtures have said for a while.
        /// <c>Google_Random</c> draws from
        /// <c>[IPv4_1, IPv4_2, IPv6_1, IPv6_2]</c>, once per fixture, so on a
        /// machine without an IPv6 route this whole fixture passed or failed by
        /// coin toss - measured over five runs, the set of failing fixtures
        /// moved every time. What is examined here is the TCP transport, not
        /// whether the host happens to have IPv6.
        /// </remarks>
        [OneTimeSetUp]
        public void InitTests()
        {
            client = DNSTCPClient.Google_Random_IPv4(LoggerFactory: logs);
        }

    }

}
