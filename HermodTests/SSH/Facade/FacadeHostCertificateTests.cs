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
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// A server with a host certificate that offers its plain key too, as sshd and AsyncSSH do: the façade
    /// asks for the certificate, so a client trusting the host CA is shown it - and a client pinning the
    /// plain key still gets in, as the certificate is tried again as the key it certifies.
    /// </summary>
    [TestFixture]
    public class FacadeHostCertificateTests
    {

        #region Data

        private static readonly ISshHostKey CA       = SshHostKey.GenerateEd25519();
        private static readonly ISshHostKey HostKey  = SshHostKey.GenerateEd25519();

        #endregion


        #region TheHostCertificate_IsShown_AndTrustedThroughItsCA

        [Test]
        [CancelAfter(20000)]
        public async Task TheHostCertificate_IsShown_AndTrustedThroughItsCA(CancellationToken CancellationToken)
        {

            var (server, port, userKey) = await StartAsync(CancellationToken);

            try
            {

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                                                                          Username       = "achim",
                                                                          VerifyHostKey  = HostKeyPolicy.HostCertificate(new SshCertificateAuthorityTrust().TrustCA(CA)).ForHost("127.0.0.1", IPPort.Parse(port)),
                                                                          Credentials    = [ userKey ]
                                                                      }, CancellationToken);

                Assert.That((await client.ExecuteAsync("hostname", CancellationToken)).StandardOutput, Is.EqualTo("ran: hostname\n"));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region APinOnThePlainKey_StillHolds

        /// <summary>
        /// The client pins the plain key and knows no CA: the certificate it is shown is tried again as the
        /// key it certifies, which is the pinned one - and a pin on another key still turns the server away.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task APinOnThePlainKey_StillHolds(CancellationToken CancellationToken)
        {

            var (server, port, userKey) = await StartAsync(CancellationToken);

            try
            {

                var shown = new List<Byte[]>();

                await using (var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                                                                           Username       = "achim",
                                                                           VerifyHostKey  = blob => { shown.Add(blob); return blob.AsSpan().SequenceEqual(HostKey.PublicKeyBlob); },
                                                                           Credentials    = [ userKey ]
                                                                       }, CancellationToken))
                {
                    Assert.That((await client.ExecuteAsync("hostname", CancellationToken)).StandardOutput, Is.EqualTo("ran: hostname\n"));
                }

                Assert.Multiple(() => {
                    Assert.That(shown.Count,                                              Is.EqualTo(2), "the certificate, then the key it certifies");
                    Assert.That(SshCertificate.IsCertificateAlgorithm(new SshPacketReader(shown[0]).ReadString()), Is.True, "the certificate was shown first");
                    Assert.That(shown[1],                                                 Is.EqualTo(HostKey.PublicKeyBlob));
                });

                var somebodyElse = SshHostKey.GenerateEd25519();

                await Assert.CatchAsync<SshWireException>(async () => {
                    await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                                                                              Username       = "achim",
                                                                              VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(somebodyElse.PublicKeyBlob),
                                                                              Credentials    = [ userKey ]
                                                                          }, CancellationToken);
                }, "a pin on another key still fails");

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion


        #region (private) StartAsync(CancellationToken)

        // The plain key and its certificate, in that order - the order of the server's offer is not what
        // decides, the client's preference is.
        private static async Task<(SshServer Server, UInt16 Port, ISshHostKey UserKey)> StartAsync(CancellationToken CancellationToken)
        {

            var userKey     = SshHostKey.GenerateEd25519();
            var certificate = new OpenSshCertificateBuilder {
                                  Serial       = 1,
                                  Type         = SshCertType.Host,
                                  KeyId        = "host",
                                  Principals   = [ "127.0.0.1" ],
                                  ValidAfter   = TimeProvider.System.GetUtcNow().AddHours(-1),
                                  ValidBefore  = TimeProvider.System.GetUtcNow().AddHours(1)
                              }.Sign(HostKey.PublicKeyBlob, CA);

            var server = new SshServer(new SshServerOptions {
                             HostKeys       = [ HostKey, new CertifiedKey(HostKey, certificate) ],
                             Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                             ExecHandler    = async (ctx, ct) => { await ctx.WriteAsync($"ran: {ctx.Command}\n", ct); return 0; }
                         });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return (server, (UInt16) server.LocalEndPoint.Port.ToInt32(), userKey);

        }

        #endregion

    }

}
