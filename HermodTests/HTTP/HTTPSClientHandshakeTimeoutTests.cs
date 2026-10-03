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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// An HTTPS client gives the TLS handshake its connect timeout.
    /// </summary>
    /// <remarks>
    /// RequestTimeout starts once the connection is up, so it never covered the
    /// handshake, and nothing else did: a server that took the connection and the
    /// ClientHello and then said nothing held a request for as long as its caller's
    /// token allowed - with none, for good.
    /// </remarks>
    [TestFixture]
    public class HTTPSClientHandshakeTimeoutTests
    {

        private static readonly RemoteTLSServerCertificateValidationHandler<IHTTPClient> AnyCertificate

            = (sender, certificate, certificateChain, client, policyErrors) => TLSValidationResult.Success();

        /// <summary>
        /// A request that is not bounded fails the test instead of hanging the suite.
        /// </summary>
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);


        [Test]
        public async Task A_server_that_never_answers_the_ClientHello_cannot_hold_a_request()
        {

            using var server  = new SilentTLSServer();
            using var client  = new HTTPSClient(
                                    URL.Parse($"https://127.0.0.1:{server.Port}"),
                                    AnyCertificate,
                                    ConnectTimeout:      TimeSpan.FromSeconds(2),
                                    MaxNumberOfRetries:  2
                                );

            var stopwatch = Stopwatch.StartNew();
            var request   = client.GET(HTTPPath.Root);

            if (await Task.WhenAny(request, Task.Delay(Guard)) != request)
                Assert.Fail($"the request was still running after {Guard.TotalSeconds} s: the TLS handshake is not bounded");

            var response  = await request;
            stopwatch.Stop();

            var reason    = response.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.Multiple(() => {
                Assert.That(server.ClientHellos,   Is.EqualTo(2),                         "two attempts, each started a handshake");
                Assert.That(stopwatch.Elapsed,     Is.LessThan(TimeSpan.FromSeconds(15)), "each handshake gets the 2 s connect timeout");
                Assert.That(response.HTTPStatusCode, Is.Not.EqualTo(HTTPStatusCode.OK));
                Assert.That(reason,                Does.Contain("did not complete within"), reason);
            });

        }

    }

}
