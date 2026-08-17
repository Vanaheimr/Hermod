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

using System.Buffers;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The SSH_MSG_KEXINIT message (RFC 4253, section 7.1): the cookie and the ten algorithm
    /// name-lists each side offers, plus the first-KEX-packet-follows flag.
    /// </summary>
    /// <remarks>
    /// The full encoded payload (starting with the message-number byte) is what enters the
    /// key-exchange hash as I_C / I_S, so <see cref="Encode"/> and <see cref="Decode"/> must be exact.
    /// </remarks>
    public sealed class KexInitMessage
    {

        #region Properties

        public Byte[]    Cookie                        { get; }
        public String[]  KexAlgorithms                 { get; }
        public String[]  ServerHostKeyAlgorithms       { get; }
        public String[]  EncryptionClientToServer      { get; }
        public String[]  EncryptionServerToClient      { get; }
        public String[]  MacClientToServer             { get; }
        public String[]  MacServerToClient             { get; }
        public String[]  CompressionClientToServer     { get; }
        public String[]  CompressionServerToClient     { get; }
        public String[]  LanguagesClientToServer       { get; }
        public String[]  LanguagesServerToClient       { get; }
        public Boolean   FirstKexPacketFollows         { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a KEXINIT message from all its fields.
        /// </summary>
        public KexInitMessage(Byte[]    Cookie,
                              String[]  KexAlgorithms,
                              String[]  ServerHostKeyAlgorithms,
                              String[]  EncryptionClientToServer,
                              String[]  EncryptionServerToClient,
                              String[]  MacClientToServer,
                              String[]  MacServerToClient,
                              String[]  CompressionClientToServer,
                              String[]  CompressionServerToClient,
                              String[]  LanguagesClientToServer,
                              String[]  LanguagesServerToClient,
                              Boolean   FirstKexPacketFollows = false)
        {

            if (Cookie.Length != 16)
                throw new ArgumentException("The KEXINIT cookie must be 16 bytes!", nameof(Cookie));

            this.Cookie                     = Cookie;
            this.KexAlgorithms              = KexAlgorithms;
            this.ServerHostKeyAlgorithms    = ServerHostKeyAlgorithms;
            this.EncryptionClientToServer   = EncryptionClientToServer;
            this.EncryptionServerToClient   = EncryptionServerToClient;
            this.MacClientToServer          = MacClientToServer;
            this.MacServerToClient          = MacServerToClient;
            this.CompressionClientToServer  = CompressionClientToServer;
            this.CompressionServerToClient  = CompressionServerToClient;
            this.LanguagesClientToServer    = LanguagesClientToServer;
            this.LanguagesServerToClient    = LanguagesServerToClient;
            this.FirstKexPacketFollows      = FirstKexPacketFollows;

        }

        #endregion


        #region (static) CreateLocal(IsServer)

        /// <summary>
        /// The default key-exchange preference list (most preferred first, without the markers).
        /// </summary>
        public static readonly String[] DefaultKeyExchanges =
        [
            // Post-quantum hybrids first (OpenSSH's modern default preference).
            SshAlgorithmNames.Kex.MlKem768X25519Sha256,
            SshAlgorithmNames.Kex.SntruP761X25519Sha512,
            SshAlgorithmNames.Kex.SntruP761X25519Sha512LibSsh,
            SshAlgorithmNames.Kex.Curve25519Sha256,
            SshAlgorithmNames.Kex.Curve25519Sha256LibSsh,
            SshAlgorithmNames.Kex.EcdhNistP256,
            SshAlgorithmNames.Kex.EcdhNistP384,
            SshAlgorithmNames.Kex.EcdhNistP521,
            SshAlgorithmNames.Kex.DhGroup16Sha512,
            SshAlgorithmNames.Kex.DhGroup14Sha256
        ];

        /// <summary>
        /// The default host-key algorithm preference list a client accepts (most preferred first).
        /// </summary>
        public static readonly String[] DefaultHostKeyAlgorithms =
        [
            SshAlgorithmNames.HostKey.Ed25519,
            SshAlgorithmNames.HostKey.EcdsaNistP256,
            SshAlgorithmNames.HostKey.EcdsaNistP384,
            SshAlgorithmNames.HostKey.EcdsaNistP521,
            SshAlgorithmNames.HostKey.RsaSha2_512,
            SshAlgorithmNames.HostKey.RsaSha2_256,
            // Certificate host keys are also accepted (used when a server presents a host certificate).
            SshAlgorithmNames.HostKey.Ed25519Cert,
            SshAlgorithmNames.HostKey.EcdsaNistP256Cert,
            SshAlgorithmNames.HostKey.EcdsaNistP384Cert,
            SshAlgorithmNames.HostKey.EcdsaNistP521Cert,
            SshAlgorithmNames.HostKey.SshRsaCert
        ];

        /// <summary>
        /// The default cipher preference list (most preferred first).
        /// </summary>
        public static readonly String[] DefaultCiphers =
        [
            SshAlgorithmNames.Cipher.ChaCha20Poly1305,
            SshAlgorithmNames.Cipher.Aes256Gcm,
            SshAlgorithmNames.Cipher.Aes128Gcm,
            SshAlgorithmNames.Cipher.Aes256Ctr,
            SshAlgorithmNames.Cipher.Aes128Ctr
        ];

        /// <summary>
        /// The default MAC preference list (encrypt-then-MAC; ignored when an AEAD cipher wins).
        /// </summary>
        public static readonly String[] DefaultMacs =
        [
            SshAlgorithmNames.Mac.HmacSha2_256Etm,
            SshAlgorithmNames.Mac.HmacSha2_512Etm
        ];


        /// <summary>
        /// Build our own KEXINIT with the modern algorithm portfolio (curve25519-sha256, ssh-ed25519,
        /// AES-GCM / AES-CTR + HMAC-SHA2-ETM) plus the strict-KEX and ext-info markers for our role.
        /// </summary>
        /// <param name="IsServer">Whether we are the server (selects the -s markers) or client (-c).</param>
        /// <param name="Ciphers">Optional cipher preference override (both directions).</param>
        /// <param name="Macs">Optional MAC preference override (both directions).</param>
        /// <param name="KeyExchanges">Optional key-exchange preference override (without the markers).</param>
        /// <param name="HostKeyAlgorithms">Optional host-key algorithm override (a server passes its key's algorithms).</param>
        /// <summary>
        /// Whether the peer's guessed key-exchange packet — the one it appended to its KEXINIT after
        /// setting <see cref="FirstKexPacketFollows"/> — turned out to be the right one.
        ///
        /// <para>
        /// RFC 4253 §7.1 makes the guess valid only when <b>both</b> the key exchange algorithm and the
        /// host-key algorithm the peer listed first are the ones actually negotiated. If either differs
        /// the guess is wrong and the receiver must read and discard that packet, or it will parse a
        /// message meant for a different algorithm — which is precisely what happens with Dropbear, whose
        /// <c>kexguess2@matt.ucc.asn.au</c> clients always send a guess.
        /// </para>
        /// </summary>
        /// <param name="PeerKexInit">The peer's KEXINIT.</param>
        /// <param name="NegotiatedKeyExchange">The key exchange method that was actually negotiated.</param>
        /// <param name="NegotiatedHostKey">The host-key algorithm that was actually negotiated.</param>
        /// <param name="KexGuess2">
        /// Whether both sides agreed <c>kexguess2@matt.ucc.asn.au</c>, which drops the host-key algorithm
        /// from the test and leaves only the key exchange — see <see cref="SshAlgorithmNames.Kex.KexGuess2"/>.
        /// </param>
        public static Boolean GuessWasCorrect(KexInitMessage  PeerKexInit,
                                              String          NegotiatedKeyExchange,
                                              String          NegotiatedHostKey,
                                              Boolean         KexGuess2 = false)
        {

            var kexNames = PeerKexInit.KexAlgorithms.Where(name => !SshAlgorithmNames.Kex.Markers.Contains(name, StringComparer.Ordinal)).ToArray();

            if (kexNames.Length == 0 || !String.Equals(kexNames[0], NegotiatedKeyExchange, StringComparison.Ordinal))
                return false;

            return KexGuess2 ||
                   (PeerKexInit.ServerHostKeyAlgorithms.Length > 0 &&
                    String.Equals(PeerKexInit.ServerHostKeyAlgorithms[0], NegotiatedHostKey, StringComparison.Ordinal));

        }


        public static KexInitMessage CreateLocal(Boolean    IsServer,
                                                 String[]?  Ciphers            = null,
                                                 String[]?  Macs               = null,
                                                 String[]?  KeyExchanges       = null,
                                                 String[]?  HostKeyAlgorithms  = null)
        {

            var ciphers = Ciphers ?? DefaultCiphers;
            var macs    = Macs    ?? DefaultMacs;

            // Key exchange list = the offered methods followed by the ext-info, strict-KEX and
            // guessed-KEX markers.
            var kex = new List<String>(KeyExchanges ?? DefaultKeyExchanges)
                      {
                          IsServer ? SshAlgorithmNames.Kex.ExtInfoServer   : SshAlgorithmNames.Kex.ExtInfoClient,
                          IsServer ? SshAlgorithmNames.Kex.StrictKexServer : SshAlgorithmNames.Kex.StrictKexClient,
                          SshAlgorithmNames.Kex.KexGuess2
                      };

            return new (
                   Cookie:                    RandomNumberGenerator.GetBytes(16),
                   KexAlgorithms:             [.. kex],
                   ServerHostKeyAlgorithms:   HostKeyAlgorithms ?? DefaultHostKeyAlgorithms,
                   EncryptionClientToServer:  ciphers,
                   EncryptionServerToClient:  ciphers,
                   MacClientToServer:         macs,
                   MacServerToClient:         macs,
                   CompressionClientToServer: [ SshAlgorithmNames.Compression.None ],
                   CompressionServerToClient: [ SshAlgorithmNames.Compression.None ],
                   LanguagesClientToServer:   [],
                   LanguagesServerToClient:   []
               );

        }

        #endregion


        #region Encode()

        /// <summary>
        /// Encode this message into its SSH payload (starting with the SSH_MSG_KEXINIT byte).
        /// </summary>
        public Byte[] Encode()
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteByte((Byte) SshMessageNumber.KexInit);

            var span = abw.GetSpan(16);
            Cookie.CopyTo(span);
            abw.Advance(16);

            writer.WriteNameList(KexAlgorithms);
            writer.WriteNameList(ServerHostKeyAlgorithms);
            writer.WriteNameList(EncryptionClientToServer);
            writer.WriteNameList(EncryptionServerToClient);
            writer.WriteNameList(MacClientToServer);
            writer.WriteNameList(MacServerToClient);
            writer.WriteNameList(CompressionClientToServer);
            writer.WriteNameList(CompressionServerToClient);
            writer.WriteNameList(LanguagesClientToServer);
            writer.WriteNameList(LanguagesServerToClient);
            writer.WriteBoolean(FirstKexPacketFollows);
            writer.WriteUInt32(0);   // reserved

            return abw.WrittenSpan.ToArray();

        }

        #endregion

        #region (static) Decode(Payload)

        /// <summary>
        /// Decode a KEXINIT message from its SSH payload.
        /// </summary>
        /// <param name="Payload">The payload, starting with the SSH_MSG_KEXINIT byte.</param>
        public static KexInitMessage Decode(ReadOnlySpan<Byte> Payload)
        {

            var reader = new SshPacketReader(Payload);

            var messageNumber = reader.ReadByte();
            if (messageNumber != (Byte) SshMessageNumber.KexInit)
                throw new SshWireException($"Expected SSH_MSG_KEXINIT (20), but found message number {messageNumber}!");

            var cookie = reader.ReadBytes(16).ToArray();

            var kex        = reader.ReadNameList();
            var hostKey    = reader.ReadNameList();
            var encC2S     = reader.ReadNameList();
            var encS2C     = reader.ReadNameList();
            var macC2S     = reader.ReadNameList();
            var macS2C     = reader.ReadNameList();
            var compC2S    = reader.ReadNameList();
            var compS2C    = reader.ReadNameList();
            var langC2S    = reader.ReadNameList();
            var langS2C    = reader.ReadNameList();
            var firstKex   = reader.ReadBoolean();
            reader.ReadUInt32();  // reserved (ignored)

            return new KexInitMessage(
                       cookie, kex, hostKey, encC2S, encS2C,
                       macC2S, macS2C, compC2S, compS2C, langC2S, langS2C, firstKex
                   );

        }

        #endregion

    }

}
