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
    /// Establishment over SSH"): the test vectors of its Appendix A.3, both ends agreeing for every key
    /// exchange method, a rekey that leaves it alone, and the way to a subsystem's handler.
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
        /// Appendix A.3 of the white paper, computed independently with Python's hashlib and hmac: K, H
        /// and the session id of a first key exchange, the exporter_secret, and the keys of NTS for
        /// AEAD_AES_SIV_CMAC_256 (15, 32 octets) and AEAD_AES_128_GCM_SIV (30, 16 octets).
        /// </summary>
        private static IEnumerable<TestCaseData> AppendixA3()
        {

            // curve25519-sha256: K the shared secret of RFC 7748, section 6.1, as an mpint.
            yield return new TestCaseData("SHA256",
                                          "000000204a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742",
                                          "202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f",
                                          "ab559ef9a78c59a68f7be3063df264e2a7ccbe83a49f76ed2aed1327cf89776e",
                                          "dea46ad2f5498c2f4636e418e06b84ed1ab74a06797ca4d7f9419b0f47bd4bff",
                                          "bdf09b3596939c2567de9b7ae120fea55c3baaa5656ac5d5844ff14414e767b9",
                                          "01f71b04899fbdf0e37f6ffc6bb2cc0d",
                                          "da4fbe70c611efc785d3546edf62b914").
                             SetName("curve25519-sha256");

            // curve25519-sha256: K the output of RFC 7748, section 5.2, whose mpint needs a leading zero.
            yield return new TestCaseData("SHA256",
                                          "0000002100c3da55379de9c6908e94ea4df28d084f32eccf03491c71f754b4075577a28552",
                                          "a0a1a2a3a4a5a6a7a8a9aaabacadaeafb0b1b2b3b4b5b6b7b8b9babbbcbdbebf",
                                          "8cd543f0ab53001f308b027ad13bd42ff218559a5d26d025a410a806ef7b9c77",
                                          "5463349908b586196892f4c621e52964a7b64428ec431b2009c2a1eaac5dd2cd",
                                          "34b31784626a48b9caa20f3eebb99532f42b87506194edb67a1b72f460a13fdd",
                                          "ffa2348ed77888f8b965ddfdcc6c963f",
                                          "4ee4ae55ea836386fa71fb157baa82b4").
                             SetName("curve25519-sha256, leading zero");

            // mlkem768x25519-sha256: K a string.
            yield return new TestCaseData("SHA256",
                                          "00000020404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f",
                                          "606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7f",
                                          "2d7dce6143d5be80605b07662a01e4735b32202106be64c66f8392dc51d887f6",
                                          "006d1458c971e5c25b59288aa25aaf35e135ae64266bc8b82a14983f16a11781",
                                          "3301489acc7496bb08bf1769222f8039c789ba2d23aeb822cc79947e7a7a5af4",
                                          "df50c064297b06a9099a472c4b8b5ff2",
                                          "a79ccf9e6cac7fdd1899db6b31536b21").
                             SetName("mlkem768x25519-sha256");

            // sntrup761x25519-sha512: K a string, and SHA-512 for the hash and the HMAC.
            yield return new TestCaseData("SHA512",
                                          "00000040808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9fa0a1a2a3a4a5a6a7a8a9aaabacadaeafb0b1b2b3b4b5b6b7b8b9babbbcbdbebf",
                                          "c0c1c2c3c4c5c6c7c8c9cacbcccdcecfd0d1d2d3d4d5d6d7d8d9dadbdcdddedfe0e1e2e3e4e5e6e7e8e9eaebecedeeeff0f1f2f3f4f5f6f7f8f9fafbfcfdfeff",
                                          "2d3d49427d4a01a90e66867b65064c5ae726c4ceb833aa06cf03a8a54e1f667c36289f14bfd84e15d4a6793e1bbc7532f3ab4bdc68bab34b0aa471a5068ff4dd",
                                          "356ef4ca9fbb594237a81ac5d95877f549b0298ff85c9762f634cee3176c36c7",
                                          "b63587f8efaa50f6eec020478570a6371ba1a3eac0755a0dfe51bcdd2642eb44",
                                          "29c34f622be0969ddf619fdb01b92f1b",
                                          "41d3e1c1f4b942c6973980ed62efb0dc").
                             SetName("sntrup761x25519-sha512");

        }

        [TestCaseSource(nameof(AppendixA3))]
        public void TestVectors_OfAppendixA3(String  Hash,
                                             String  K,
                                             String  H,
                                             String  ExporterSecret,
                                             String  C2S15,
                                             String  S2C15,
                                             String  C2S30,
                                             String  S2C30)
        {

            var hash   = new HashAlgorithmName(Hash);
            var h      = Convert.FromHexString(H);

            // The first key exchange: the session id is its H.
            var secret = SshExporter.ComputeSecret(hash, Convert.FromHexString(K), h, h);

            using var exporter = SshExporter.Create(hash, Convert.FromHexString(K), h, h);

            Assert.Multiple(() => {

                Assert.That(Convert.ToHexStringLower(secret),                                                   Is.EqualTo(ExporterSecret));

                Assert.That(Convert.ToHexStringLower(exporter.Export(NTSLabel, NTSContext(15, false), 32)),    Is.EqualTo(C2S15));
                Assert.That(Convert.ToHexStringLower(exporter.Export(NTSLabel, NTSContext(15, true),  32)),    Is.EqualTo(S2C15));
                Assert.That(Convert.ToHexStringLower(exporter.Export(NTSLabel, NTSContext(30, false), 16)),    Is.EqualTo(C2S30));
                Assert.That(Convert.ToHexStringLower(exporter.Export(NTSLabel, NTSContext(30, true),  16)),    Is.EqualTo(S2C30));

                // The same, from the secret alone - what a server that hands the exchange on would do.
                Assert.That(Convert.ToHexStringLower(SshExporter.Expand(hash, secret, NTSLabel, NTSContext(15, false), 32)), Is.EqualTo(C2S15));

            });

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

            var c2s = client.Exporter!.Export(NTSLabel, NTSContext(15, false), 32);

            Assert.Multiple(() => {

                Assert.That(client.Algorithms.KeyExchange,       Is.EqualTo(KeyExchange));
                Assert.That(client.Exporter!.HashAlgorithm.Name,  Is.EqualTo(Hash));
                Assert.That(server.Exporter!.HashAlgorithm.Name,  Is.EqualTo(Hash));

                Assert.That(server.Exporter.Export(NTSLabel, NTSContext(15, false), 32), Is.EqualTo(c2s));

                // Another context, other keys - and no key is the connection's own session id.
                Assert.That(server.Exporter.Export(NTSLabel, NTSContext(15, true),  32), Is.Not.EqualTo(c2s));
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

            Assert.That(c2.Exporter!.Export(NTSLabel, NTSContext(15, false), 32),
                        Is.Not.EqualTo(c1.Exporter!.Export(NTSLabel, NTSContext(15, false), 32)));

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

            var before          = client.Exporter!.Export(NTSLabel, NTSContext(15, false), 32);
            var exchangeHash    = (Byte[]) client.ExchangeHash.Clone();

            await Task.WhenAll(client.RekeyAsync(CancellationToken).AsTask(),
                               server.RekeyAsync(CancellationToken).AsTask());

            Assert.Multiple(() => {

                // A rekey it was: a new H ...
                Assert.That(client.KeyExchangeCount,  Is.EqualTo(2));
                Assert.That(client.ExchangeHash,      Is.Not.EqualTo(exchangeHash));

                // ... and the same exporter on both ends, as TLS 1.3 keeps its own across KeyUpdate.
                Assert.That(client.Exporter.Export(NTSLabel, NTSContext(15, false), 32), Is.EqualTo(before));
                Assert.That(server.Exporter!.Export(NTSLabel, NTSContext(15, false), 32), Is.EqualTo(before));

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
                Assert.That(() => exporter.Export(NTSLabel, NTSContext(15, false), 32), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(exporter.ToString(), Is.EqualTo("SSH exporter (SHA256)"));
            });

        }

        #endregion


        #region ASubsystemHandler_GetsTheExporterOfItsConnection

        /// <summary>
        /// The way NTS-KE over SSH takes on a server: its handler finds the exporter in the session, and
        /// the client in its connection - the same keys on both ends, the same on every channel of one
        /// connection, and others on the next connection.
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
                                                                  ? async (context, token) => await context.Channel.SendDataAsync(
                                                                                                        context.Session.Exporter?.Export(NTSLabel, NTSContext(15, false), 32) ?? [],
                                                                                                        token)
                                                                  : null)
                                     });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            async Task<SshClient> ConnectAsync()
                => await SshClient.ConnectAsync("127.0.0.1", (UInt16) server.LocalEndPoint.Port.ToInt32(), new SshClientOptions {
                       Username       = "cs001",
                       VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                       Credentials    = [ userKey ]
                   }, CancellationToken);

            static async Task<Byte[]> KeyFromTheServerAsync(SshClient Client, CancellationToken CancellationToken)
            {
                var channel  = await Client.OpenSubsystemAsync(Subsystem, CancellationToken);
                using var memory = new MemoryStream();
                await channel.Input.CopyToAsync(memory, CancellationToken);
                return memory.ToArray();
            }

            await using var first   = await ConnectAsync();
            await using var second  = await ConnectAsync();

            var firstKey   = await KeyFromTheServerAsync(first,  CancellationToken);
            var againKey   = await KeyFromTheServerAsync(first,  CancellationToken);
            var secondKey  = await KeyFromTheServerAsync(second, CancellationToken);

            Assert.Multiple(() => {

                Assert.That(firstKey,   Has.Length.EqualTo(32), "The handler found no exporter in the session.");
                Assert.That(firstKey,   Is.EqualTo(first. Exporter.Export(NTSLabel, NTSContext(15, false), 32)));
                Assert.That(againKey,   Is.EqualTo(firstKey));

                Assert.That(secondKey,  Is.EqualTo(second.Exporter.Export(NTSLabel, NTSContext(15, false), 32)));
                Assert.That(secondKey,  Is.Not.EqualTo(firstKey));

            });

        }

        #endregion

    }

}
