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

using System.IO.Pipelines;

// Our own "ExchangeHash" property below would otherwise shadow the ExchangeHash helper type.
using ExchangeHashCalc = org.GraphDefined.Vanaheimr.Hermod.SSH.ExchangeHash;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A stateful SSH transport over an <see cref="IDuplexPipe"/>: it performs the initial key exchange,
    /// then owns the mutable per-direction cipher/MAC and packet-sequence-number state so that packets
    /// can be sent and received with correct framing — and so that a later key <b>re-exchange</b>
    /// (rekeying, RFC 4253 §9) can install fresh keys while preserving the original session id.
    /// </summary>
    /// <remarks>
    /// With strict-KEX (Terrapin mitigation) both sequence numbers reset to zero after <i>every</i>
    /// SSH_MSG_NEWKEYS, initial or subsequent — matching OpenSSH. A rekey is quiescent here: no data
    /// packets are expected to interleave with the KEX exchange (draining in-flight traffic during a
    /// rekey belongs to the connection layer). Instances are single reader / single writer.
    /// </remarks>
    public sealed class SshTransport : IDisposable
    {

        #region Data

        private readonly IDuplexPipe             pipe;
        private readonly Boolean                 isServer;
        private readonly Byte[]                  vC;
        private readonly Byte[]                  vS;
        private readonly ISshHostKey?            hostKey;         // server side
        private readonly Func<Byte[], Boolean>?  verifyHostKey;   // client side
        private readonly String[]?               ciphers;
        private readonly String[]?               macs;
        private readonly String[]?               keyExchanges;
        private readonly String[]?               hostKeyAlgorithms;
        private readonly String[]                serverSignatureAlgorithms;

        private readonly Dictionary<String, String>  peerExtensions = [];

        private SshTransportCipher   sendCipher;
        private ISshMac?             sendMac;
        private UInt32               sendSequenceNumber;

        private SshTransportCipher   receiveCipher;
        private ISshMac?             receiveMac;
        private UInt32               receiveSequenceNumber;

        private Byte[]               sessionId      = [];
        private Byte[]               exchangeHash   = [];
        private Byte[]               serverHostKey  = [];
        private NegotiatedAlgorithms algorithms     = null!;

        // Whether a key exchange is currently running. Only strict KEX cares: it forbids the otherwise
        // always-legal SSH_MSG_IGNORE / DEBUG / UNIMPLEMENTED while the exchange is in flight.
        private Boolean              keyExchangeInProgress;

        #endregion

        #region Properties

        /// <summary>The session identifier (the exchange hash of the <i>first</i> key exchange); stable across rekeys.</summary>
        public Byte[]                SessionId         => sessionId;

        /// <summary>The exchange hash H of the most recent key exchange.</summary>
        public Byte[]                ExchangeHash      => exchangeHash;

        /// <summary>The algorithms negotiated in the most recent key exchange.</summary>
        public NegotiatedAlgorithms  Algorithms        => algorithms;

        /// <summary>The peer's host-key blob (K_S) from the most recent key exchange.</summary>
        public Byte[]                ServerHostKey     => serverHostKey;

        /// <summary>Whether this end is the server.</summary>
        public Boolean               IsServer          => isServer;

        /// <summary>How many key exchanges have completed (1 after the handshake, +1 per rekey).</summary>
        public Int32                 KeyExchangeCount  { get; private set; }

        /// <summary>The extensions received from the peer via SSH_MSG_EXT_INFO (RFC 8308), if any.</summary>
        public IReadOnlyDictionary<String, String>  PeerExtensions  => peerExtensions;

        /// <summary>
        /// The peer's "server-sig-algs" list (the signature algorithms a server accepts for public-key
        /// auth), parsed from a received EXT_INFO, or null if the peer never advertised it.
        /// </summary>
        public String[]?             PeerServerSignatureAlgorithms
            => peerExtensions.TryGetValue(SshExtensionNames.ServerSigAlgs, out var value)
                   ? value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   : null;

        #endregion

        #region Constructor(s)

        private SshTransport(IDuplexPipe             Pipe,
                             Boolean                 IsServer,
                             Byte[]                  V_C,
                             Byte[]                  V_S,
                             ISshHostKey?            HostKey,
                             Func<Byte[], Boolean>?  VerifyHostKey,
                             String[]?               Ciphers,
                             String[]?               Macs,
                             String[]?               KeyExchanges,
                             String[]?               HostKeyAlgorithms,
                             String[]?               ServerSignatureAlgorithms)
        {

            this.pipe                       = Pipe;
            this.isServer                   = IsServer;
            this.vC                         = V_C;
            this.vS                         = V_S;
            this.hostKey                    = HostKey;
            this.verifyHostKey              = VerifyHostKey;
            this.ciphers                    = Ciphers;
            this.macs                       = Macs;
            this.keyExchanges               = KeyExchanges;
            this.hostKeyAlgorithms          = HostKeyAlgorithms;
            this.serverSignatureAlgorithms  = ServerSignatureAlgorithms ?? SshExtensionNames.DefaultServerSignatureAlgorithms;

            // Before the first NEWKEYS everything travels in the clear (cipher "none", no MAC).
            this.sendCipher         = NullTransportCipher.Instance;
            this.receiveCipher      = NullTransportCipher.Instance;

        }

        #endregion


        #region (static) ClientHandshakeAsync(Pipe, ...)

        /// <summary>
        /// Connect as the client: version exchange, then the initial key exchange, returning an
        /// established transport ready for encrypted traffic and later rekeys.
        /// </summary>
        public static async ValueTask<SshTransport> ClientHandshakeAsync(IDuplexPipe               Pipe,
                                                                         SshIdentificationString?  LocalIdentification  = null,
                                                                         Func<Byte[], Boolean>?    VerifyHostKey        = null,
                                                                         String[]?                 Ciphers              = null,
                                                                         String[]?                 Macs                 = null,
                                                                         String[]?                 KeyExchanges         = null,
                                                                         String[]?                 HostKeyAlgorithms    = null,
                                                                         CancellationToken         CancellationToken    = default)
        {

            var localId  = LocalIdentification ?? SshIdentificationString.Default;
            await SshVersionExchange.WriteAsync(Pipe.Output, localId, CancellationToken).ConfigureAwait(false);
            var remote   = await SshVersionExchange.ReadAsync(Pipe.Input, CancellationToken).ConfigureAwait(false);

            var transport = new SshTransport(Pipe, IsServer: false, localId.ToWireBytes(), remote.WireBytes,
                                             HostKey: null, VerifyHostKey, Ciphers, Macs, KeyExchanges, HostKeyAlgorithms,
                                             ServerSignatureAlgorithms: null);

            await transport.PerformKeyExchangeAsync(IsInitial: true, PeerKexInit: null, CancellationToken).ConfigureAwait(false);

            return transport;

        }

        #endregion

        #region (static) ServerHandshakeAsync(Pipe, HostKey, ...)

        /// <summary>
        /// Accept as the server: version exchange, then the initial key exchange, returning an
        /// established transport ready for encrypted traffic and later rekeys.
        /// </summary>
        public static async ValueTask<SshTransport> ServerHandshakeAsync(IDuplexPipe               Pipe,
                                                                         ISshHostKey               HostKey,
                                                                         SshIdentificationString?  LocalIdentification        = null,
                                                                         String[]?                 Ciphers                    = null,
                                                                         String[]?                 Macs                       = null,
                                                                         String[]?                 KeyExchanges               = null,
                                                                         String[]?                 ServerSignatureAlgorithms  = null,
                                                                         CancellationToken         CancellationToken          = default)
        {

            var localId  = LocalIdentification ?? SshIdentificationString.Default;
            await SshVersionExchange.WriteAsync(Pipe.Output, localId, CancellationToken).ConfigureAwait(false);
            var remote   = await SshVersionExchange.ReadAsync(Pipe.Input, CancellationToken).ConfigureAwait(false);

            var transport = new SshTransport(Pipe, IsServer: true, remote.WireBytes, localId.ToWireBytes(),
                                             HostKey, VerifyHostKey: null, Ciphers, Macs, KeyExchanges, HostKeyAlgorithms: null,
                                             ServerSignatureAlgorithms);

            await transport.PerformKeyExchangeAsync(IsInitial: true, PeerKexInit: null, CancellationToken).ConfigureAwait(false);

            return transport;

        }

        #endregion


        #region SendPacketAsync(Payload, CancellationToken)

        /// <summary>
        /// Frame, encrypt and flush one packet under the current send cipher, advancing the send
        /// sequence number.
        /// </summary>
        public async ValueTask SendPacketAsync(ReadOnlyMemory<Byte> Payload, CancellationToken CancellationToken = default)
        {

            SshPacketFraming.WritePacket(pipe.Output, sendCipher, Payload.Span, sendSequenceNumber, sendMac);
            await pipe.Output.FlushAsync(CancellationToken).ConfigureAwait(false);

            unchecked { sendSequenceNumber++; }   // wraps at 2^32 (RFC 4253 §6.4)

        }

        #endregion

        #region ReceivePacketAsync(CancellationToken)

        /// <summary>
        /// Read, decrypt and authenticate one packet under the current receive cipher, advancing the
        /// receive sequence number.
        ///
        /// <para>
        /// SSH_MSG_IGNORE, SSH_MSG_DEBUG and SSH_MSG_UNIMPLEMENTED are consumed here rather than handed
        /// on: RFC 4253 §11 lets a peer send them <i>at any time</i>, so every caller would otherwise have
        /// to skip them and any that forgot would break against peers that use them — AsyncSSH, for one,
        /// pads authentication with SSH_MSG_IGNORE to blunt traffic analysis.
        /// </para>
        ///
        /// <para>
        /// The exception is strict KEX: while a key exchange is in flight it requires that no unexpected
        /// packet is tolerated, explicitly including these three, because silently dropping them is what
        /// let Terrapin (CVE-2023-48795) shift the sequence numbers. There they are fatal instead.
        /// </para>
        /// </summary>
        public async ValueTask<Byte[]> ReceivePacketAsync(CancellationToken CancellationToken = default)
        {

            while (true)
            {

                var payload = await SshPacketFraming.ReadPacketAsync(pipe.Input, receiveCipher, receiveSequenceNumber, receiveMac, CancellationToken).ConfigureAwait(false);

                unchecked { receiveSequenceNumber++; }

                if (payload.Length < 1)
                    return payload;

                var messageNumber = (SshMessageNumber) payload[0];

                if (messageNumber is not (SshMessageNumber.Ignore or SshMessageNumber.Debug or SshMessageNumber.Unimplemented))
                    return payload;

                if (keyExchangeInProgress && algorithms is { StrictKex: true })
                    throw new SshWireException($"Strict KEX forbids {messageNumber} ({payload[0]}) during a key exchange.");

            }

        }

        #endregion


        #region RekeyAsync(CancellationToken)

        /// <summary>
        /// Initiate a key re-exchange: send a fresh KEXINIT and run the exchange, installing new keys
        /// while keeping the original session id. Both peers may initiate simultaneously (each sends its
        /// KEXINIT and reads the other's) — the standard symmetric case.
        /// </summary>
        public ValueTask RekeyAsync(CancellationToken CancellationToken = default)
            => PerformKeyExchangeAsync(IsInitial: false, PeerKexInit: null, CancellationToken);

        #endregion

        #region RespondToRekeyAsync(PeerKexInit, CancellationToken)

        /// <summary>
        /// Respond to a peer-initiated rekey whose KEXINIT has already been read from the stream (e.g.
        /// by a receive loop): send our own KEXINIT and run the exchange using the peer's KEXINIT.
        /// </summary>
        /// <param name="PeerKexInit">The peer's KEXINIT payload that triggered the rekey.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public ValueTask RespondToRekeyAsync(Byte[] PeerKexInit, CancellationToken CancellationToken = default)
            => PerformKeyExchangeAsync(IsInitial: false, PeerKexInit, CancellationToken);

        #endregion


        #region (private) PerformKeyExchangeAsync(IsInitial, PeerKexInit, CancellationToken)

        private async ValueTask PerformKeyExchangeAsync(Boolean            IsInitial,
                                                        Byte[]?            PeerKexInit,
                                                        CancellationToken  CancellationToken)
        {

            // Marks the window in which strict KEX refuses SSH_MSG_IGNORE / DEBUG / UNIMPLEMENTED
            // instead of skipping them (see ReceivePacketAsync).
            keyExchangeInProgress = true;

            try
            {

            // 1. Send our KEXINIT (a server offers only its host key's algorithms).
            var localKexInit = KexInitMessage.CreateLocal(isServer, ciphers, macs, keyExchanges,
                                                          isServer ? [.. hostKey!.AlgorithmNames] : hostKeyAlgorithms);
            var iLocal = localKexInit.Encode();
            await SendPacketAsync(iLocal, CancellationToken).ConfigureAwait(false);

            // 2. Obtain the peer's KEXINIT (already read on a peer-initiated rekey, else read it now).
            var iRemote = PeerKexInit ?? await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
            if (iRemote.Length < 1 || iRemote[0] != (Byte) SshMessageNumber.KexInit)
                throw new SshWireException($"Expected SSH_MSG_KEXINIT (20) to start the key exchange, but found message number {(iRemote.Length > 0 ? iRemote[0] : -1)}!");

            var remoteKexInit = KexInitMessage.Decode(iRemote);

            // I_C is always the client's KEXINIT, I_S the server's.
            var (iC, iS)   = isServer ? (iRemote, iLocal) : (iLocal, iRemote);
            var clientInit = isServer ? remoteKexInit : localKexInit;
            var serverInit = isServer ? localKexInit  : remoteKexInit;

            var negotiated = AlgorithmNegotiation.Negotiate(clientInit, serverInit, WeAreServer: isServer);
            SshKexCore.EnsureSupported(negotiated);

            // RFC 4253 §7.1: a peer may append a *guessed* first key-exchange packet to its KEXINIT. If the
            // guess turns out wrong it must be read and discarded, otherwise the next read would parse a
            // packet meant for a different algorithm. Dropbear guesses on every connection, so without
            // this its clients only ever interoperate on whichever method they happen to prefer.
            if (remoteKexInit.FirstKexPacketFollows &&
                !KexInitMessage.GuessWasCorrect(remoteKexInit, negotiated.KeyExchange, negotiated.HostKey, negotiated.KexGuess2))
            {
                await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
            }

            // 3. The method-specific exchange (client sends INIT, server replies) — shared with the
            //    initial handshake. e/f (classic DH) or Q_C/Q_S (ECDH) are carried transparently.
            using var kex = SshKeyExchange.Create(negotiated.KeyExchange);

            Byte[] kS, h, kEncoded;

            if (isServer)
            {

                var initPayload      = await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var qC               = SshKexCore.ParseEcdhInit(initPayload);
                var (qS, rawSecret)  = kex.ServerRespond(qC);

                kEncoded        = kex.EncodeSharedSecret(rawSecret);
                kS              = hostKey!.PublicKeyBlob;
                h               = ExchangeHashCalc.Compute(kex.HashAlgorithm, vC, vS, iC, iS, kS, qC, qS, kEncoded);

                var signature   = hostKey!.Sign(negotiated.HostKey, h);
                await SendPacketAsync(SshKexCore.BuildEcdhReply(kS, qS, signature), CancellationToken).ConfigureAwait(false);

            }
            else
            {

                var qC          = kex.StartClient();
                await SendPacketAsync(SshKexCore.BuildEcdhInit(qC), CancellationToken).ConfigureAwait(false);

                var reply                   = await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var (kServer, qS, sigBlob)  = SshKexCore.ParseEcdhReply(reply);
                kS                          = kServer;

                var rawSecret   = kex.ClientFinish(qS);
                kEncoded        = kex.EncodeSharedSecret(rawSecret);
                h               = ExchangeHashCalc.Compute(kex.HashAlgorithm, vC, vS, iC, iS, kS, qC, qS, kEncoded);

                if (!SshSignature.Verify(kS, h, sigBlob))
                    throw new SshWireException("The server's host-key signature over the exchange hash is invalid!");

                // The signature above only proves the peer holds the private half of the key it just
                // presented — possession, not identity. Without a verifier there is nothing binding that
                // key to the host we meant to reach, so a missing one must fail closed: silently
                // accepting would leave every connection open to a machine-in-the-middle.
                if (verifyHostKey is null)
                    throw new SshWireException("No host-key verification was supplied, so the server's identity cannot be established. "
                                               + "Pass a verifier (e.g. HostKeyPolicy.ForHost(host, port)), or SshHostKeyVerification.AcceptAnyUnsafe to skip it deliberately.");

                if (!verifyHostKey(kS))
                    throw new SshWireException("The server's host key was rejected by the host-key policy.");

            }

            // 4. The session id is fixed by the FIRST exchange and reused as the KDF salt for rekeys.
            if (IsInitial)
                sessionId = h;

            var derive = SshKexCore.MakeDeriver(kex.HashAlgorithm, kEncoded, h, sessionId);

            // Directional keys: we send in our own direction and receive in the peer's.
            var (newSendCipher, newSendMac) = isServer
                ? SshKexCore.BuildDirection(negotiated.CipherServerToClient, negotiated.MacServerToClient, derive, Kdf.KeyLetter.EncryptionKeyServerToClient, Kdf.KeyLetter.InitialIVServerToClient, Kdf.KeyLetter.IntegrityKeyServerToClient)
                : SshKexCore.BuildDirection(negotiated.CipherClientToServer, negotiated.MacClientToServer, derive, Kdf.KeyLetter.EncryptionKeyClientToServer, Kdf.KeyLetter.InitialIVClientToServer, Kdf.KeyLetter.IntegrityKeyClientToServer);

            var (newReceiveCipher, newReceiveMac) = isServer
                ? SshKexCore.BuildDirection(negotiated.CipherClientToServer, negotiated.MacClientToServer, derive, Kdf.KeyLetter.EncryptionKeyClientToServer, Kdf.KeyLetter.InitialIVClientToServer, Kdf.KeyLetter.IntegrityKeyClientToServer)
                : SshKexCore.BuildDirection(negotiated.CipherServerToClient, negotiated.MacServerToClient, derive, Kdf.KeyLetter.EncryptionKeyServerToClient, Kdf.KeyLetter.InitialIVServerToClient, Kdf.KeyLetter.IntegrityKeyServerToClient);

            // 5. NEWKEYS: our own is the last packet under the old send keys; the peer's is the last
            //    under the old receive keys. Swap each direction independently, right after the boundary.
            var newKeysMessage = new Byte[] { (Byte) SshMessageNumber.NewKeys };
            await SendPacketAsync(newKeysMessage, CancellationToken).ConfigureAwait(false);
            SwapSend(newSendCipher, newSendMac, negotiated.StrictKex);

            var newKeys = await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
            if (newKeys.Length < 1 || newKeys[0] != (Byte) SshMessageNumber.NewKeys)
                throw new SshWireException(
                          $"Expected SSH_MSG_NEWKEYS (21) to complete the key exchange, but found " +
                          $"{(newKeys.Length > 0 ? ((SshMessageNumber) newKeys[0]).ToString() : "an empty packet")}. " +
                          $"Negotiated {negotiated.KeyExchange} / {negotiated.HostKey}; the peer listed " +
                          $"{(remoteKexInit.KexAlgorithms.Length > 0 ? remoteKexInit.KexAlgorithms[0] : "-")} / " +
                          $"{(remoteKexInit.ServerHostKeyAlgorithms.Length > 0 ? remoteKexInit.ServerHostKeyAlgorithms[0] : "-")} first " +
                          $"and {(remoteKexInit.FirstKexPacketFollows ? "did" : "did not")} guess. " +
                          $"A peer rejecting our signature here means the two sides computed different exchange hashes.");
            SwapReceive(newReceiveCipher, newReceiveMac, negotiated.StrictKex);

            algorithms     = negotiated;
            exchangeHash   = h;
            serverHostKey  = kS;
            KeyExchangeCount++;

            // RFC 8308 §3.1: when ext-info was negotiated, the server sends SSH_MSG_EXT_INFO
            // (server-sig-algs) as the very next packet after its first NEWKEYS. Not repeated on rekeys.
            if (IsInitial && isServer && negotiated.ExtensionInfo)
            {
                var extInfo = ExtInfoMessage.ForServerSigAlgs(serverSignatureAlgorithms);
                await SendPacketAsync(extInfo.Encode(), CancellationToken).ConfigureAwait(false);
            }

            }
            finally
            {
                keyExchangeInProgress = false;
            }

        }

        #endregion

        #region TryHandleExtInfo(Payload)

        /// <summary>
        /// If <paramref name="Payload"/> is an SSH_MSG_EXT_INFO message, parse it and record the peer's
        /// extensions (e.g. "server-sig-algs"), returning true; otherwise leave state untouched and
        /// return false. A receive loop calls this on each packet so extension state stays transport-side.
        /// </summary>
        public Boolean TryHandleExtInfo(ReadOnlySpan<Byte> Payload)
        {

            if (Payload.Length < 1 || Payload[0] != (Byte) SshMessageNumber.ExtInfo)
                return false;

            var extInfo = ExtInfoMessage.Decode(Payload);

            foreach (var extension in extInfo.Extensions)
                peerExtensions[extension.Key] = extension.Value;

            return true;

        }

        #endregion

        #region (private) SwapSend / SwapReceive

        private void SwapSend(SshTransportCipher NewCipher, ISshMac? NewMac, Boolean StrictKex)
        {

            var oldCipher = sendCipher;
            var oldMac    = sendMac;

            sendCipher = NewCipher;
            sendMac    = NewMac;

            // Strict-KEX resets the sequence number to zero after every NEWKEYS (initial and subsequent).
            if (StrictKex)
                sendSequenceNumber = 0;

            DisposeCipher(oldCipher);
            oldMac?.Dispose();

        }

        private void SwapReceive(SshTransportCipher NewCipher, ISshMac? NewMac, Boolean StrictKex)
        {

            var oldCipher = receiveCipher;
            var oldMac    = receiveMac;

            receiveCipher = NewCipher;
            receiveMac    = NewMac;

            if (StrictKex)
                receiveSequenceNumber = 0;

            DisposeCipher(oldCipher);
            oldMac?.Dispose();

        }

        // Never dispose the shared "none" singleton (used before the first NEWKEYS).
        private static void DisposeCipher(SshTransportCipher Cipher)
        {
            if (!ReferenceEquals(Cipher, NullTransportCipher.Instance))
                Cipher.Dispose();
        }

        #endregion


        #region Dispose()

        /// <summary>Dispose the current directional ciphers and MACs, wiping their key material.</summary>
        public void Dispose()
        {
            DisposeCipher(sendCipher);
            DisposeCipher(receiveCipher);
            sendMac?.Dispose();
            receiveMac?.Dispose();
        }

        #endregion

    }

}
