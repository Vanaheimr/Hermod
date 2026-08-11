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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Unit tests for the SSH_MSG_KEXINIT message and the algorithm negotiation (RFC 4253, section 7.1).
    /// </summary>
    [TestFixture]
    public class KexInitNegotiationTests
    {

        #region Encode / decode

        [Test]
        public void KexInit_EncodeDecode_RoundTrip()
        {

            var original = KexInitMessage.CreateLocal(IsServer: true);
            var decoded  = KexInitMessage.Decode(original.Encode());

            Assert.Multiple(() => {
                Assert.That(decoded.Cookie,                    Is.EqualTo(original.Cookie));
                Assert.That(decoded.KexAlgorithms,             Is.EqualTo(original.KexAlgorithms));
                Assert.That(decoded.ServerHostKeyAlgorithms,   Is.EqualTo(original.ServerHostKeyAlgorithms));
                Assert.That(decoded.EncryptionClientToServer,  Is.EqualTo(original.EncryptionClientToServer));
                Assert.That(decoded.CompressionServerToClient, Is.EqualTo(original.CompressionServerToClient));
                Assert.That(decoded.LanguagesClientToServer,   Is.Empty);
                Assert.That(decoded.FirstKexPacketFollows,     Is.False);
            });

        }

        [Test]
        public void KexInit_Encode_StartsWithMessageNumber20()
        {
            Assert.That(KexInitMessage.CreateLocal(IsServer: false).Encode()[0],
                        Is.EqualTo((Byte) SshMessageNumber.KexInit));
        }

        #endregion

        #region Negotiation

        [Test]
        public void Negotiate_PicksModernDefaults()
        {

            var client  = KexInitMessage.CreateLocal(IsServer: false);
            var server  = KexInitMessage.CreateLocal(IsServer: true);

            var result  = AlgorithmNegotiation.Negotiate(client, server, WeAreServer: true);

            Assert.Multiple(() => {
                Assert.That(result.KeyExchange,           Is.EqualTo(SshAlgorithmNames.Kex.MlKem768X25519Sha256));
                Assert.That(result.HostKey,               Is.EqualTo(SshAlgorithmNames.HostKey.Ed25519));
                Assert.That(result.CipherClientToServer,  Is.EqualTo(SshAlgorithmNames.Cipher.ChaCha20Poly1305));
                Assert.That(result.CipherServerToClient,  Is.EqualTo(SshAlgorithmNames.Cipher.ChaCha20Poly1305));
                Assert.That(result.StrictKex,             Is.True);
                Assert.That(result.ExtensionInfo,         Is.True);
            });

        }

        [Test]
        public void Negotiate_ClientPreferenceWins()
        {

            var cookie = new Byte[16];

            // The client prefers the libssh alias first; the server supports both — client's order wins.
            var client = new KexInitMessage(cookie,
                             KexAlgorithms:             [ SshAlgorithmNames.Kex.Curve25519Sha256LibSsh, SshAlgorithmNames.Kex.Curve25519Sha256 ],
                             ServerHostKeyAlgorithms:   [ SshAlgorithmNames.HostKey.Ed25519 ],
                             EncryptionClientToServer:  [ SshAlgorithmNames.Cipher.Aes256Gcm ],
                             EncryptionServerToClient:  [ SshAlgorithmNames.Cipher.Aes256Gcm ],
                             MacClientToServer:         [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                             MacServerToClient:         [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                             CompressionClientToServer: [ SshAlgorithmNames.Compression.None ],
                             CompressionServerToClient: [ SshAlgorithmNames.Compression.None ],
                             LanguagesClientToServer:   [],
                             LanguagesServerToClient:   []);

            var server = new KexInitMessage(cookie,
                             KexAlgorithms:             [ SshAlgorithmNames.Kex.Curve25519Sha256, SshAlgorithmNames.Kex.Curve25519Sha256LibSsh ],
                             ServerHostKeyAlgorithms:   [ SshAlgorithmNames.HostKey.Ed25519 ],
                             EncryptionClientToServer:  [ SshAlgorithmNames.Cipher.Aes256Gcm ],
                             EncryptionServerToClient:  [ SshAlgorithmNames.Cipher.Aes256Gcm ],
                             MacClientToServer:         [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                             MacServerToClient:         [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                             CompressionClientToServer: [ SshAlgorithmNames.Compression.None ],
                             CompressionServerToClient: [ SshAlgorithmNames.Compression.None ],
                             LanguagesClientToServer:   [],
                             LanguagesServerToClient:   []);

            Assert.That(AlgorithmNegotiation.Negotiate(client, server, WeAreServer: false).KeyExchange,
                        Is.EqualTo(SshAlgorithmNames.Kex.Curve25519Sha256LibSsh));

        }

        [Test]
        public void Negotiate_NoCommonCipher_Throws()
        {

            var cookie = new Byte[16];

            var client = new KexInitMessage(cookie,
                             [ SshAlgorithmNames.Kex.Curve25519Sha256 ],
                             [ SshAlgorithmNames.HostKey.Ed25519 ],
                             [ "aes128-ctr" ],                              // only CTR
                             [ "aes128-ctr" ],
                             [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                             [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                             [ SshAlgorithmNames.Compression.None ],
                             [ SshAlgorithmNames.Compression.None ],
                             [], []);

            var server = new KexInitMessage(cookie,
                             [ SshAlgorithmNames.Kex.Curve25519Sha256 ],
                             [ SshAlgorithmNames.HostKey.Ed25519 ],
                             [ SshAlgorithmNames.Cipher.Aes256Gcm ],        // only GCM
                             [ SshAlgorithmNames.Cipher.Aes256Gcm ],
                             [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                             [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                             [ SshAlgorithmNames.Compression.None ],
                             [ SshAlgorithmNames.Compression.None ],
                             [], []);

            Assert.Throws<SshWireException>(() => AlgorithmNegotiation.Negotiate(client, server, WeAreServer: true));

        }

        [Test]
        public void Negotiate_StrictKex_OnlyWhenPeerAdvertises()
        {

            var cookie = new Byte[16];

            // A peer (here the client) that does NOT advertise strict-KEX.
            var clientNoStrict = new KexInitMessage(cookie,
                                     [ SshAlgorithmNames.Kex.Curve25519Sha256 ],   // no strict marker
                                     [ SshAlgorithmNames.HostKey.Ed25519 ],
                                     [ SshAlgorithmNames.Cipher.Aes256Gcm ],
                                     [ SshAlgorithmNames.Cipher.Aes256Gcm ],
                                     [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                                     [ SshAlgorithmNames.Mac.HmacSha2_256 ],
                                     [ SshAlgorithmNames.Compression.None ],
                                     [ SshAlgorithmNames.Compression.None ],
                                     [], []);

            var server = KexInitMessage.CreateLocal(IsServer: true);

            Assert.That(AlgorithmNegotiation.Negotiate(clientNoStrict, server, WeAreServer: true).StrictKex,
                        Is.False);

        }

        #endregion

    }

}
