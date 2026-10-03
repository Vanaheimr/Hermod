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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// A DNS-over-TLS client gives the TLS handshake its connect timeout.
    /// </summary>
    /// <remarks>
    /// The query timeout starts once the connection is up, so it never covered the
    /// handshake, and nothing else did: a server that took the connection and the
    /// ClientHello and then said nothing held a query without a cancellation token
    /// for good.
    /// </remarks>
    [TestFixture]
    public class DNSTLSClientHandshakeTimeoutTests
    {

        /// <summary>
        /// A query that is not bounded fails the test instead of hanging the suite.
        /// </summary>
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);


        [Test]
        public async Task A_server_that_never_answers_the_ClientHello_cannot_hold_a_query()
        {

            using var server        = new SilentTLSServer();
            using var loggers       = new DNSTestLoggerFactory();
            await using var client  = new DNSTLSClient(
                                          IPv4Address.Localhost,
                                          TCPPort:                     server.Port,
                                          ConnectTimeout:              TimeSpan.FromSeconds(2),
                                          RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                          LoggerFactory:               loggers
                                      );

            var stopwatch = Stopwatch.StartNew();
            var query     = client.Query<A>(DomainName.Parse("api.example.test."));

            if (await Task.WhenAny(query, Task.Delay(Guard)) != query)
                Assert.Fail($"the query was still running after {Guard.TotalSeconds} s: the TLS handshake is not bounded");

            var response  = await query;
            stopwatch.Stop();

            var log       = String.Join(Environment.NewLine, loggers.Entries);

            Assert.Multiple(() => {
                Assert.That(server.ClientHellos,                Is.EqualTo(1),                         "the client started the handshake, once");
                Assert.That(stopwatch.Elapsed,                  Is.LessThan(TimeSpan.FromSeconds(10)), "the handshake gets the 2 s connect timeout, not the 23.5 s query timeout");
                Assert.That(response.FilteredAnswers.Count(),   Is.Zero);
                Assert.That(log,                                Does.Contain("did not complete within"),
                            "the reason is said, not lost in writing the query into the failed handshake's stream: " + log);
            });

        }

    }

}
