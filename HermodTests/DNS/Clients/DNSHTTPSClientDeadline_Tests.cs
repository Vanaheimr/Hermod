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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// Whether a DoH query keeps to its deadline when there is nobody to ask.
    /// </summary>
    /// <remarks>
    /// The deadline used to be set only once a connection was there. The connect
    /// before it had none, its result was not looked at, and SendRequest then
    /// tried to connect again as often as it may retry, without asking whether
    /// the caller had given up. Against a refused port, a 3 s query came back
    /// after 8.2 s and called that a timeout.
    /// </remarks>
    [TestFixture]
    public class DNSHTTPSClientDeadline_Tests
    {

        /// <summary>A loopback port nothing listens on.</summary>
        private static Int32 ClosedPort()
        {
            var probe = new TcpListener(System.Net.IPAddress.Loopback, 0);
            probe.Start();
            var port  = ((IPEndPoint) probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }


        #region ARefusedConnection_IsAFailure_BeforeTheDeadline()

        /// <summary>
        /// Six seconds rather than three, because Windows answers a refused loopback
        /// connect after about two seconds of SYN retries. The old code needed more
        /// than six: one connect of its own and three more inside SendRequest.
        /// </summary>
        [Test]
        public async Task ARefusedConnection_IsAFailure_BeforeTheDeadline()
        {

            var deadline = TimeSpan.FromSeconds(6);

            await using var client = new DNSHTTPSClient(URL.Parse($"http://127.0.0.1:{ClosedPort()}/dns-query"),
                                                        QueryTimeout: deadline);

            var clock = Stopwatch.StartNew();
            var info  = await client.Query(DNSServiceName.Parse("refused.example."), [ DNSResourceRecordTypes.A ], deadline);
            clock.Stop();

            Assert.Multiple(() => {
                Assert.That(info.IsValid,   Is.False, "there is no answer");
                Assert.That(info.IsTimeout, Is.False, "and it is not a timeout: the connection was refused");
                Assert.That(clock.Elapsed,  Is.LessThan(deadline), "which was known before the deadline");
            });

        }

        #endregion

    }

}
