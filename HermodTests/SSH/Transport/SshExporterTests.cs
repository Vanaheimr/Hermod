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

using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The keying material exporter of a connection (section 4.3 of the white paper "NTS Key
    /// Establishment over SSH"): the test vectors of its Appendix A.3 and of the SSH Key Binding
    /// record, both ends agreeing for every key exchange method, a nonce that sets an export apart, a
    /// rekey that leaves it alone, and the way to a subsystem's handler.
    /// </summary>
    [TestFixture]
    public class SshExporterTests
    {

        #region (private) Fixture

        /// <summary>
        /// The label and the contexts of NTS (RFC 8915, section 5.1), which the test vectors use.
        /// </summary>
        private const String NTSLabel = "EXPORTER-network-time-security";

        private static Byte[] NTSContext(UInt16 AEADAlgorithm, Boolean ServerToClient)
            => [ 0x00, 0x00, (Byte) (AEADAlgorithm >> 8), (Byte) AEADAlgorithm, ServerToClient ? (Byte) 0x01 : (Byte) 0x00 ];

        /// <summary>
        /// A fresh nonce, as NTS-KE over SSH sends one with every key exchange of method 0x0001.
        /// </summary>
        private static Byte[] Nonce()
            => RandomNumberGenerator.GetBytes(32);

        private static async Task<(SshTransport Client, SshTransport Server)> HandshakeAsync(String[]?          KeyExchanges,
                                                                                             CancellationToken  CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, KeyExchanges: KeyExchanges, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, KeyExchanges: KeyExchanges, CancellationToken: CancellationToken);

            var client = await clientTask;
            var server = await serverTask;

            // EXT_INFO follows the server's first NEWKEYS; take it off, so that a rekey finds the line clear.
            if (client.Algorithms.ExtensionInfo)
            {
                var extInfo = await client.ReceivePacketAsync(CancellationToken);
                Assert.That(client.TryHandleExtInfo(extInfo), Is.True, "The first server packet must be EXT_INFO.");
            }

            return (client, server);

        }

        #endregion


        #region TestVectors_OfAppendixA3

        /// <summary>
        /// The nonce of every vector of Appendix A.3: 1f 1e ... 01 00.
        /// </summary>
        private const String A3Nonce = "1f1e1d1c1b1a191817161514131211100f0e0d0c0b0a09080706050403020100";

        /// <summary>
        /// Appendix A.3 of the white paper, computed independently with Python's hashlib and hmac: K, H
        /// and the session id of a first key exchange, the exporter_secret, the PRK for the nonce, and
        /// the keys of NTS for AEAD_AES_SIV_CMAC_256 (15, 32 octets) and AEAD_AES_128_GCM_SIV (30,
        /// 16 octets).
        /// </summary>
        private static IEnumerable<TestCaseData> AppendixA3()
        {

            // curve25519-sha256: K the shared secret of RFC 7748, section 6.1, as an mpint.
            yield return new TestCaseData("SHA256",
                                          "000000204a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742",
                                          "202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f",
                                          "ab559ef9a78c59a68f7be3063df264e2a7ccbe83a49f76ed2aed1327cf89776e",
                                          "a2d98772f94b8fb96dd30707c7c9ad0d278d0ff90a411ad709ce7069b1e4781f",
                                          "ea9163026944be623b625a50b8f59aa1daf843949e0f98634f178af6e248330a",
                                          "d9376278448d5de12b0d3ebc3c06bf26c0ccc0c85ac869b6a23d10213b7fc402",
                                          "339eabe19ea8469bbf993ac405a89417",
                                          "26823c8acd419d57fd901749dc6d5d76").
                             SetName("curve25519-sha256");

            // curve25519-sha256: K the output of RFC 7748, section 5.2, whose mpint needs a leading zero.
            yield return new TestCaseData("SHA256",
                                          "0000002100c3da55379de9c6908e94ea4df28d084f32eccf03491c71f754b4075577a28552",
                                          "a0a1a2a3a4a5a6a7a8a9aaabacadaeafb0b1b2b3b4b5b6b7b8b9babbbcbdbebf",
                                          "8cd543f0ab53001f308b027ad13bd42ff218559a5d26d025a410a806ef7b9c77",
                                          "24e54cd9033daa7bb1e5047b90a6a0f53f24e62ad9b9e743a3dfb17a159e4ffb",
                                          "6d38e2ff8aaaab4842eb4cb3d66712d93d8675efc0cea721f3828f8de3fce947",
                                          "3a177a3612e473add6c214cbdf858b59e7012e0e68676f57692998fb5192a208",
                                          "21462f863d040ff37b49a64406aa2c95",
                                          "5ca842af0fdccb0f11f0318e2529b6a1").
                             SetName("curve25519-sha256, leading zero");

            // mlkem768x25519-sha256: K a string.
            yield return new TestCaseData("SHA256",
                                          "00000020404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f",
                                          "606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7f",
                                          "2d7dce6143d5be80605b07662a01e4735b32202106be64c66f8392dc51d887f6",
                                          "d4a0a37898cd1bf17500c5c0a6c9285d66087742df355873f912dd058fc94027",
                                          "10f3f639e505ca255ac1a51d235bac0d0ba15b078774e0c13f7f97a15b93df05",
                                          "8a1e71fceb65e99f0d171f6472e8c9bd7f92f20ad8a5085d3bf9b1d7fec9c28f",
                                          "7d654fc273012de0e7a6f3f18b906fbf",
                                          "846d60f8628227bbd7a7871b32829302").
                             SetName("mlkem768x25519-sha256");

            // sntrup761x25519-sha512: K a string, and SHA-512 for the hash and the HMAC.
            yield return new TestCaseData("SHA512",
                                          "00000040808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9fa0a1a2a3a4a5a6a7a8a9aaabacadaeafb0b1b2b3b4b5b6b7b8b9babbbcbdbebf",
                                          "c0c1c2c3c4c5c6c7c8c9cacbcccdcecfd0d1d2d3d4d5d6d7d8d9dadbdcdddedfe0e1e2e3e4e5e6e7e8e9eaebecedeeeff0f1f2f3f4f5f6f7f8f9fafbfcfdfeff",
                                          "2d3d49427d4a01a90e66867b65064c5ae726c4ceb833aa06cf03a8a54e1f667c36289f14bfd84e15d4a6793e1bbc7532f3ab4bdc68bab34b0aa471a5068ff4dd",
                                          "ac0a32957f57e5f41866387c4d8313183a6d93b8995d2c84acfe5070af26189480d5dd64e36838e2ac5af9ea77c5c5d8a6d8c207f556880fe5e6e251dbc31615",
                                          "d8df38f63a6c606738f00b416ee530c2c5b8d54881a72a73e983eac1968bf5b5",
                                          "b611446e12a524681f9fd023eee1a57468a624a3f73b0dd7c6a6d45dd2297414",
                                          "891071d4dcddf3ec6a2074d58ebe5059",
                                          "423acfe0d50b1c0e571835f70cb4fef0").
                             SetName("sntrup761x25519-sha512");

        }

        [TestCaseSource(nameof(AppendixA3))]
        public void TestVectors_OfAppendixA3(String  Hash,
                                             String  K,
                                             String  H,
                                             String  ExporterSecret,
                                             String  PRK,
                                             String  C2S15,
                                             String  S2C15,
                                             String  C2S30,
                                             String  S2C30)
        {

            var hash   = new HashAlgorithmName(Hash);
            var h      = Convert.FromHexString(H);
            var nonce  = Convert.FromHexString(A3Nonce);

            // The first key exchange: the session id is its H.
            var secret = SshExporter.ComputeSecret(hash, Convert.FromHexString(K), h, h);

            using var exporter = SshExporter.Create(hash, Convert.FromHexString(K), h, h);

            Assert.Multiple(() => {

                Assert.That(Convert.ToHexStringLower(secret),                                                          Is.EqualTo(ExporterSecret));
                Assert.That(Convert.ToHexStringLower(SshExporter.Extract(hash, secret, nonce)),                       Is.EqualTo(PRK));

                Assert.That(Convert.ToHexStringLower(exporter.Export(nonce, NTSLabel, NTSContext(15, false), 32)),    Is.EqualTo(C2S15));
                Assert.That(Convert.ToHexStringLower(exporter.Export(nonce, NTSLabel, NTSContext(15, true),  32)),    Is.EqualTo(S2C15));
                Assert.That(Convert.ToHexStringLower(exporter.Export(nonce, NTSLabel, NTSContext(30, false), 16)),    Is.EqualTo(C2S30));
                Assert.That(Convert.ToHexStringLower(exporter.Export(nonce, NTSLabel, NTSContext(30, true),  16)),    Is.EqualTo(S2C30));

                // The same, from the secret alone - what a server that hands the exchange on would do.
                Assert.That(Convert.ToHexStringLower(SshExporter.Expand(hash, secret, nonce, NTSLabel, NTSContext(15, false), 32)), Is.EqualTo(C2S15));

            });

        }

        #endregion

        #region TestVectors_FromTheExporterSecret

        /// <summary>
        /// The vectors of method 0x0001 in Appendix A.3 of the specification of the SSH Key Binding
        /// record, which begin at the exporter_secret: 00 01 ... 1f, with the nonce 20 21 ... 3f and
        /// SHA-256 - checked with Python's hmac as well.
        /// </summary>
        [TestCase(15, 32, "dcf81015c16cb4b955624949e824e89f45cac437995a873e938ec3c7adc0218b", "6b91d1a3ad1eb448fa5010e20dd79b280f7c303aec2252b464a4642fb70ea12d")]
        [TestCase(30, 16, "fd8733d215d9d3a79f0d95b86884ed28",                                 "f7fe123370cdd2ed478906c34bd6d7b8")]
        [TestCase(31, 32, "05d23579f0905080af0243173053808adad6d276ed0560a9fe64ef714b690554", "d72234762b25d744d00016818b8f23ad65d1ac5c85009d1ada6215046c73ebcc")]
        public void TestVectors_FromTheExporterSecret(Int32 AEADAlgorithm, Int32 Length, String C2S, String S2C)
        {

            var secret  = Enumerable.Range(0x00, 32).Select(i => (Byte) i).ToArray();
            var nonce   = Enumerable.Range(0x20, 32).Select(i => (Byte) i).ToArray();

            Assert.Multiple(() => {

                Assert.That(Convert.ToHexStringLower(SshExporter.Extract(HashAlgorithmName.SHA256, secret, nonce)),
                            Is.EqualTo("a27b86e7a70a029cba778d6f738d952696d6d8361b95103dd84ae9df6af063af"));

                Assert.That(Convert.ToHexStringLower(SshExporter.Expand(HashAlgorithmName.SHA256, secret, nonce, NTSLabel, NTSContext((UInt16) AEADAlgorithm, false), Length)),
                            Is.EqualTo(C2S));

                Assert.That(Convert.ToHexStringLower(SshExporter.Expand(HashAlgorithmName.SHA256, secret, nonce, NTSLabel, NTSContext((UInt16) AEADAlgorithm, true),  Length)),
                            Is.EqualTo(S2C));

            });

        }

        #endregion

        #region AnExport_NeedsANonce

        [Test]
        public void AnExport_NeedsANonce()
        {

            var h = Enumerable.Range(0x20, 32).Select(i => (Byte) i).ToArray();

            using var exporter = SshExporter.Create(HashAlgorithmName.SHA256, Convert.FromHexString("000000204a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742"), h, h);

            Assert.That(() => exporter.Export([], NTSLabel, NTSContext(15, false), 32), Throws.ArgumentException);

        }

        #endregion

        #region Info_IsTheLabelAndTheContext_AsSSHStrings

        [Test]
        public void Info_IsTheLabelAndTheContext_AsSSHStrings()

            // The info of Appendix A.1 of the white paper: the same for both sources of NTS-KE over SSH.
            => Assert.That(Convert.ToHexStringLower(SshExporter.Info(NTSLabel, NTSContext(15, false))),
                           Is.EqualTo("0000001e" + "4558504f525445522d6e6574776f726b2d74696d652d7365637572697479" + "00000005" + "0000000f00"));

        #endregion


        #region BothEnds_Agree(KeyExchange, Hash)

        [TestCase(SshAlgorithmNames.Kex.Curve25519Sha256,       "SHA256")]
        [TestCase(SshAlgorithmNames.Kex.EcdhNistP256,           "SHA256")]
        [TestCase(SshAlgorithmNames.Kex.EcdhNistP384,           "SHA384")]
        [TestCase(SshAlgorithmNames.Kex.EcdhNistP521,           "SHA512")]
        [TestCase(SshAlgorithmNames.Kex.DhGroup14Sha256,        "SHA256")]
        [TestCase(SshAlgorithmNames.Kex.DhGroup16Sha512,        "SHA512")]
        [TestCase(SshAlgorithmNames.Kex.MlKem768X25519Sha256,   "SHA256")]
        [TestCase(SshAlgorithmNames.Kex.SntruP761X25519Sha512,  "SHA512")]
        [CancelAfter(30000)]
        public async Task BothEnds_Agree(String KeyExchange, String Hash, CancellationToken CancellationToken)
        {

            var (client, server) = await HandshakeAsync([ KeyExchange ], CancellationToken);

            using var _c = client;
            using var _s = server;

            var nonce  = Nonce();
            var c2s    = client.Exporter!.Export(nonce, NTSLabel, NTSContext(15, false), 32);

            Assert.Multiple(() => {

                Assert.That(client.Algorithms.KeyExchange,       Is.EqualTo(KeyExchange));
                Assert.That(client.Exporter!.HashAlgorithm.Name,  Is.EqualTo(Hash));
                Assert.That(server.Exporter!.HashAlgorithm.Name,  Is.EqualTo(Hash));

                Assert.That(server.Exporter.Export(nonce, NTSLabel, NTSContext(15, false), 32), Is.EqualTo(c2s));

                // Another context, other keys; another nonce, other keys - and no key is the
                // connection's own session id.
                Assert.That(server.Exporter.Export(nonce,   NTSLabel, NTSContext(15, true),  32), Is.Not.EqualTo(c2s));
                Assert.That(server.Exporter.Export(Nonce(), NTSLabel, NTSContext(15, false), 32), Is.Not.EqualTo(c2s));
                Assert.That(c2s, Is.Not.EqualTo(client.SessionId.AsSpan(0, 32).ToArray()));

            });

        }

        #endregion

        #region AnotherConnection_GetsOtherKeys

        [Test]
        [CancelAfter(15000)]
        public async Task AnotherConnection_GetsOtherKeys(CancellationToken CancellationToken)
        {

            var (c1, s1) = await HandshakeAsync(null, CancellationToken);
            var (c2, s2) = await HandshakeAsync(null, CancellationToken);

            using var _c1 = c1; using var _s1 = s1;
            using var _c2 = c2; using var _s2 = s2;

            // The same nonce, and still other keys.
            var nonce = Nonce();

            Assert.That(c2.Exporter!.Export(nonce, NTSLabel, NTSContext(15, false), 32),
                        Is.Not.EqualTo(c1.Exporter!.Export(nonce, NTSLabel, NTSContext(15, false), 32)));

        }

        #endregion

        #region Rekey_LeavesTheExporterAlone

        [Test]
        [CancelAfter(15000)]
        public async Task Rekey_LeavesTheExporterAlone(CancellationToken CancellationToken)
        {

            var (client, server) = await HandshakeAsync(null, CancellationToken);

            using var _c = client;
            using var _s = server;

            var nonce           = Nonce();
            var before          = client.Exporter!.Export(nonce, NTSLabel, NTSContext(15, false), 32);
            var exchangeHash    = (Byte[]) client.ExchangeHash.Clone();

            await Task.WhenAll(client.RekeyAsync(CancellationToken).AsTask(),
                               server.RekeyAsync(CancellationToken).AsTask());

            Assert.Multiple(() => {

                // A rekey it was: a new H ...
                Assert.That(client.KeyExchangeCount,  Is.EqualTo(2));
                Assert.That(client.ExchangeHash,      Is.Not.EqualTo(exchangeHash));

                // ... and the same exporter on both ends, as TLS 1.3 keeps its own across KeyUpdate.
                Assert.That(client.Exporter.Export (nonce, NTSLabel, NTSContext(15, false), 32), Is.EqualTo(before));
                Assert.That(server.Exporter!.Export(nonce, NTSLabel, NTSContext(15, false), 32), Is.EqualTo(before));

            });

        }

        #endregion

        #region TheEndOfTheConnection_WipesTheExporter

        [Test]
        [CancelAfter(15000)]
        public async Task TheEndOfTheConnection_WipesTheExporter(CancellationToken CancellationToken)
        {

            var (client, server) = await HandshakeAsync(null, CancellationToken);

            var exporter = server.Exporter!;

            server.Dispose();
            client.Dispose();

            Assert.Multiple(() => {
                Assert.That(() => exporter.Export(Nonce(), NTSLabel, NTSContext(15, false), 32), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(exporter.ToString(), Is.EqualTo("SSH exporter (SHA256)"));
            });

        }

        #endregion


        #region ASubsystemHandler_GetsTheExporterOfItsConnection

        /// <summary>
        /// The way NTS-KE over SSH takes on a server: its handler finds the exporter in the session, and
        /// the client in its connection. The handler sends a nonce of its own and the key it exports
        /// with it: the same key on both ends, another one on every channel of one connection, and
        /// others on the next connection.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task ASubsystemHandler_GetsTheExporterOfItsConnection(CancellationToken CancellationToken)
        {

            const String Subsystem = "exporter@test.open.charging.cloud";

            var hostKey  = SshHostKey.GenerateEd25519();
            var userKey  = SshHostKey.GenerateEd25519();

            await using var server = new SshServer(new SshServerOptions {
                                         HostKeys       = [ hostKey ],
                                         Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                                         Subsystems     = (session, name, ct) => ValueTask.FromResult<SshSubsystemHandler?>(
                                                              name == Subsystem
                                                                  ? async (context, token) => {
                                                                        var nonce          = Nonce();
                                                                        Byte[] nonceAndKey = [ .. nonce, .. context.Session.Exporter?.Export(nonce, NTSLabel, NTSContext(15, false), 32) ?? [] ];
                                                                        await context.Channel.SendDataAsync(nonceAndKey, token);
                                                                    }
                                                                  : null)
                                     });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            async Task<SshClient> ConnectAsync()
                => await SshClient.ConnectAsync("127.0.0.1", (UInt16) server.LocalEndPoint.Port.ToInt32(), new SshClientOptions {
                       Username       = "cs001",
                       VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                       Credentials    = [ userKey ]
                   }, CancellationToken);

            // The server's key, and the one the client exports with the server's nonce.
            static async Task<(Byte[] Server, Byte[] Client)> KeysAsync(SshClient Client, CancellationToken CancellationToken)
            {
                var channel  = await Client.OpenSubsystemAsync(Subsystem, CancellationToken);
                using var memory = new MemoryStream();
                await channel.Input.CopyToAsync(memory, CancellationToken);
                var received = memory.ToArray();
                Assert.That(received, Has.Length.EqualTo(64), "The handler found no exporter in the session.");
                return (received[32..], Client.Exporter.Export(received[..32], NTSLabel, NTSContext(15, false), 32));
            }

            await using var first   = await ConnectAsync();
            await using var second  = await ConnectAsync();

            var firstKeys   = await KeysAsync(first,  CancellationToken);
            var againKeys   = await KeysAsync(first,  CancellationToken);
            var secondKeys  = await KeysAsync(second, CancellationToken);

            Assert.Multiple(() => {

                Assert.That(firstKeys. Server,  Is.EqualTo(firstKeys. Client));
                Assert.That(againKeys. Server,  Is.EqualTo(againKeys. Client));
                Assert.That(secondKeys.Server,  Is.EqualTo(secondKeys.Client));

                Assert.That(againKeys. Server,  Is.Not.EqualTo(firstKeys.Server), "Another channel of the connection, another nonce.");
                Assert.That(secondKeys.Server,  Is.Not.EqualTo(firstKeys.Server));

            });

        }

        #endregion

    }

}
