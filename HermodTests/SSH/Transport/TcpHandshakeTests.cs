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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{


    /// <summary>
    /// The M1 handshake over real TCP sockets, on both IPv4 loopback and IPv6 loopback (::1),
    /// exercising the socket-to-pipe adapter and confirming IPv6 is first-class.
    /// </summary>
    [TestFixture]
    public class TcpHandshakeTests
    {

        #region (private) RunOverTcpAsync(LoopbackAddress, CancellationToken)

        private static async Task RunOverTcpAsync(IIPAddress LoopbackAddress, CancellationToken CancellationToken)
        {

            var hostKey = Ed25519KeyPair.Generate();

            using var listener = SshTcpListener.Start(new IPSocket(LoopbackAddress, IPPort.Auto));
            var localSocket = listener.LocalEndPoint;

            // Server: accept one connection and run the server handshake.
            var serverTask = Task.Run(async () =>
            {
                var pipe = await listener.AcceptAsync(CancellationToken);
                return await SshHandshake.ServerHandshakeAsync(pipe, hostKey, CancellationToken: CancellationToken);
            }, CancellationToken);

            // Client: connect and run the client handshake.
            var clientPipe = await SshTcp.ConnectAsync(new IPSocket(LoopbackAddress, localSocket.Port), CancellationToken);
            using var client = await SshHandshake.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            using var server = await serverTask;

            Assert.Multiple(() => {
                Assert.That(client.SessionId, Is.EqualTo(server.SessionId));
                Assert.That(SshEd25519.ParsePublicKeyBlob(client.ServerHostKey), Is.EqualTo(hostKey.PublicKey));
            });

        }

        #endregion


        [Test]
        [CancelAfter(15000)]
        public async Task Handshake_OverIPv4Loopback(CancellationToken CancellationToken)
        {
            await RunOverTcpAsync(IPv4Address.Localhost, CancellationToken);
        }

        [Test]
        [CancelAfter(15000)]
        public async Task Handshake_OverIPv6Loopback(CancellationToken CancellationToken)
        {

            if (!System.Net.Sockets.Socket.OSSupportsIPv6)
                Assert.Ignore("IPv6 is not available on this host.");

            await RunOverTcpAsync(IPv6Address.Localhost, CancellationToken);

        }

    }

}
