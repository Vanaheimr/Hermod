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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The guessed first key-exchange packet (RFC 4253 §7.1) and Dropbear's
    /// <c>kexguess2@matt.ucc.asn.au</c> refinement of it.
    ///
    /// <para>
    /// This is a regression suite for a real interop defect. A peer may append a <i>guessed</i> key-exchange
    /// packet to its KEXINIT; if the guess turns out wrong the receiver must read and discard it. We never
    /// did, so every Dropbear client — which guesses on every connection — only interoperated when the
    /// negotiated method happened to be the one it guessed. Six of seven key exchanges failed, and the
    /// symptom was a bad host-key signature, because we had parsed a packet meant for a different algorithm
    /// into the exchange hash.
    /// </para>
    ///
    /// <para>
    /// The extension exists because the RFC rule is nearly useless: it only accepts a guess when the host-key
    /// algorithm matches too, and a client rarely knows which host key it will be offered. Advertising it
    /// brings a hazard of its own, pinned below: both peers send the <i>same</i> name, so unlike the
    /// <c>-c</c>/<c>-s</c> marker pairs it could be negotiated as the key exchange itself.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    public class KexGuessTests
    {

        #region (private) helpers

        private static KexInitMessage PeerKexInit(String[] KexAlgorithms,
                                                  String[] HostKeyAlgorithms,
                                                  Boolean  FirstKexPacketFollows = true)

            => new (Cookie:                    RandomNumberGenerator.GetBytes(16),
                    KexAlgorithms:             KexAlgorithms,
                    ServerHostKeyAlgorithms:   HostKeyAlgorithms,
                    EncryptionClientToServer:  [ "chacha20-poly1305@openssh.com" ],
                    EncryptionServerToClient:  [ "chacha20-poly1305@openssh.com" ],
                    MacClientToServer:         [ "hmac-sha2-256-etm@openssh.com" ],
                    MacServerToClient:         [ "hmac-sha2-256-etm@openssh.com" ],
                    CompressionClientToServer: [ "none" ],
                    CompressionServerToClient: [ "none" ],
                    LanguagesClientToServer:   [],
                    LanguagesServerToClient:   [],
                    FirstKexPacketFollows:     FirstKexPacketFollows);

        #endregion


        #region GuessRules

        /// <summary>
        /// RFC 4253 §7.1: the guess counts only when the key exchange <b>and</b> the host-key algorithm the
        /// peer listed first are the ones negotiated.
        /// </summary>
        [Test]
        public void Guess_IsCorrect_OnlyWhenBothAlgorithmsMatch()
        {

            var peer = PeerKexInit([ "curve25519-sha256", "ecdh-sha2-nistp256" ], [ "ssh-ed25519", "rsa-sha2-512" ]);

            Assert.Multiple(() => {

                Assert.That(KexInitMessage.GuessWasCorrect(peer, "curve25519-sha256", "ssh-ed25519"), Is.True,
                            "both match, so the guessed packet is the real one");

                Assert.That(KexInitMessage.GuessWasCorrect(peer, "ecdh-sha2-nistp256", "ssh-ed25519"), Is.False,
                            "a different key exchange was negotiated, so the guess must be discarded");

                Assert.That(KexInitMessage.GuessWasCorrect(peer, "curve25519-sha256", "rsa-sha2-512"), Is.False,
                            "the host-key algorithm differs, which under the RFC rule invalidates the guess");

            });

        }

        /// <summary>
        /// With <c>kexguess2</c> agreed, the host-key algorithm drops out of the test — that narrowing is the
        /// entire point of the extension, and getting it wrong strands every Dropbear client.
        /// </summary>
        [Test]
        public void KexGuess2_IgnoresTheHostKeyAlgorithm()
        {

            var peer = PeerKexInit([ "curve25519-sha256" ], [ "rsa-sha2-512", "ssh-ed25519" ]);

            Assert.Multiple(() => {

                Assert.That(KexInitMessage.GuessWasCorrect(peer, "curve25519-sha256", "ssh-ed25519", KexGuess2: true), Is.True,
                            "the key exchange matches, and kexguess2 asks about nothing else");

                Assert.That(KexInitMessage.GuessWasCorrect(peer, "ecdh-sha2-nistp256", "ssh-ed25519", KexGuess2: true), Is.False,
                            "kexguess2 still requires the key exchange itself to match");

            });

        }

        /// <summary>
        /// The markers travel inside the key-exchange name-list, so they must not be mistaken for the peer's
        /// first choice when judging a guess.
        /// </summary>
        [Test]
        public void Guess_IgnoresTheMarkersInTheNameList()
        {

            var peer = PeerKexInit([ "kexguess2@matt.ucc.asn.au", "curve25519-sha256", "ext-info-c" ],
                                   [ "ssh-ed25519" ]);

            Assert.That(KexInitMessage.GuessWasCorrect(peer, "curve25519-sha256", "ssh-ed25519"), Is.True,
                        "the first *algorithm* is curve25519-sha256; the marker before it is not a guess");

        }

        #endregion

        #region Markers_AreNeverNegotiatedAsTheKeyExchange

        /// <summary>
        /// The hazard advertising <c>kexguess2</c> introduces: both peers send the identical name, so a
        /// name-list intersection would happily "agree" on it. Nothing would then implement the negotiated
        /// key exchange. The same must hold for the ext-info and strict-KEX markers.
        /// </summary>
        [Test]
        public void Markers_AreNeverNegotiatedAsTheKeyExchange()
        {

            // Deliberately hostile ordering: every marker sits before the only real algorithm.
            var client = PeerKexInit([ "kexguess2@matt.ucc.asn.au", "ext-info-c", "kex-strict-c-v00@openssh.com", "curve25519-sha256" ],
                                     [ "ssh-ed25519" ],
                                     FirstKexPacketFollows: false);

            var server = PeerKexInit([ "kexguess2@matt.ucc.asn.au", "ext-info-s", "kex-strict-s-v00@openssh.com", "curve25519-sha256" ],
                                     [ "ssh-ed25519" ],
                                     FirstKexPacketFollows: false);

            var negotiated = AlgorithmNegotiation.Negotiate(client, server, WeAreServer: true);

            Assert.Multiple(() => {

                Assert.That(negotiated.KeyExchange, Is.EqualTo("curve25519-sha256"),
                            "a marker must never be selected as the key exchange");

                Assert.That(negotiated.KexGuess2, Is.True,  "both sides advertised kexguess2");
                Assert.That(negotiated.StrictKex, Is.True,  "and the strict-KEX markers must still be seen");
                Assert.That(negotiated.ExtensionInfo, Is.True);

            });

        }

        #endregion

        #region OurKexInit_AdvertisesKexGuess2

        /// <summary>
        /// We only get the single-packet behaviour from Dropbear if we say we honour the guess, so the
        /// marker has to be on the wire — in both roles.
        /// </summary>
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OurKexInit_AdvertisesKexGuess2(Boolean AsServer)
        {

            var kexInit = KexInitMessage.CreateLocal(AsServer);

            Assert.Multiple(() => {

                Assert.That(kexInit.KexAlgorithms, Does.Contain("kexguess2@matt.ucc.asn.au"));

                Assert.That(kexInit.FirstKexPacketFollows, Is.False,
                            "we never guess ourselves — we only have to understand a peer that does");

            });

        }

        #endregion

    }

}
