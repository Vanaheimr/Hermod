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
using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Hermod.Modbus;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Modbus
{

    /// <summary>
    /// A Modbus/TCP Security client gives the TLS handshake its connect timeout.
    /// </summary>
    /// <remarks>
    /// The first request connects, and the connect handed the handshake no token: a
    /// device that took the connection and the ClientHello and then said nothing held
    /// that request for good. The request timeout starts only once the request is
    /// written.
    /// </remarks>
    [TestFixture]
    public class ModbusTLSHandshakeTimeoutTests
    {

        /// <summary>
        /// A request that is not bounded fails the test instead of hanging the suite.
        /// </summary>
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);


        [Test]
        public async Task A_device_that_never_answers_the_ClientHello_cannot_hold_a_request()
        {

            using var server        = new SilentTLSServer();
            await using var client  = new ModbusTCPClient(
                                          IPv4Address.Localhost,
                                          server.Port,
                                          RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                          TLSProtocol:                 SslProtocols.Tls12 | SslProtocols.Tls13,
                                          RequestTimeout:              TimeSpan.FromSeconds(2)
                                      );

            var stopwatch = Stopwatch.StartNew();
            var request   = client.ReadHoldingRegisters(9, 4);

            if (await Task.WhenAny(request, Task.Delay(Guard)) != request)
                Assert.Fail($"the request was still running after {Guard.TotalSeconds} s: the TLS handshake is not bounded");

            var exception = await Assert.ThrowsAsync<IOException>(() => request);
            stopwatch.Stop();

            Assert.Multiple(() => {
                Assert.That(server.ClientHellos,  Is.EqualTo(1),                         "the client started the handshake");
                Assert.That(stopwatch.Elapsed,    Is.LessThan(TimeSpan.FromSeconds(10)), "the handshake gets the 2 s connect timeout");
                Assert.That(exception?.Message,   Does.Contain("did not complete within"), exception?.Message);
            });

        }

    }

}
