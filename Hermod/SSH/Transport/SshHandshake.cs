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
using System.IO.Pipelines;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Drives the minimal modern SSH transport handshake over an <see cref="IDuplexPipe"/>:
    /// version exchange, KEXINIT negotiation, <c>curve25519-sha256</c> key exchange with an
    /// <c>ssh-ed25519</c> host key, key derivation and NEWKEYS, ending with the AES-256-GCM ciphers in
    /// place. Strict-KEX markers are advertised and the outcome recorded (RFC 4253, RFC 8731, RFC 5647).
    /// </summary>
    public static class SshHandshake
    {

        #region ClientHandshakeAsync(Pipe, LocalIdentification, VerifyHostKey = null, CancellationToken = default)

        /// <summary>
        /// Perform the client side of the handshake.
        /// </summary>
        /// <param name="Pipe">The duplex transport.</param>
        /// <param name="LocalIdentification">Our identification string.</param>
        /// <param name="VerifyHostKey">
        /// An optional callback that receives the server's host-key blob and returns true to accept it.
        /// If null, the host key is accepted (M1 has no persistent trust store yet).
        /// </param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async ValueTask<SshTransportContext> ClientHandshakeAsync(IDuplexPipe               Pipe,
                                                                                SshIdentificationString?  LocalIdentification  = null,
                                                                                Func<Byte[], Boolean>?    VerifyHostKey        = null,
                                                                                String[]?                 Ciphers              = null,
                                                                                String[]?                 Macs                 = null,
                                                                                String[]?                 KeyExchanges         = null,
                                                                                String[]?                 HostKeyAlgorithms    = null,
                                                                                CancellationToken         CancellationToken    = default)
        {

            var localId  = LocalIdentification ?? SshIdentificationString.Default;

            // 1. Version exchange.
            await SshVersionExchange.WriteAsync(Pipe.Output, localId, CancellationToken).ConfigureAwait(false);
            var remote   = await SshVersionExchange.ReadAsync(Pipe.Input, CancellationToken).ConfigureAwait(false);
            var vC       = localId.ToWireBytes();
            var vS       = remote.WireBytes;

            // 2. KEXINIT exchange + negotiation.
            var localKexInit = KexInitMessage.CreateLocal(IsServer: false, Ciphers, Macs, KeyExchanges, HostKeyAlgorithms);
            var iC           = localKexInit.Encode();
            await WritePacketAsync(Pipe.Output, NullTransportCipher.Instance, iC, CancellationToken).ConfigureAwait(false);

            var iS            = await SshPacketFraming.ReadPacketAsync(Pipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken).ConfigureAwait(false);
            var remoteKexInit = KexInitMessage.Decode(iS);
            var negotiated    = AlgorithmNegotiation.Negotiate(localKexInit, remoteKexInit, WeAreServer: false);
            SshKexCore.EnsureSupported(negotiated);

            // 3. KEX init: send our ephemeral public value (an ECDH point, a DH e, or a KEM+X25519 blob).
            using var kex = SshKeyExchange.Create(negotiated.KeyExchange);
            var qC        = kex.StartClient();
            await WritePacketAsync(Pipe.Output, NullTransportCipher.Instance, SshKexCore.BuildEcdhInit(qC), CancellationToken).ConfigureAwait(false);

            // 4. KEX reply: host key, server public value, signature.
            var reply = await SshPacketFraming.ReadPacketAsync(Pipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken).ConfigureAwait(false);
            var (kS, qS, signatureBlob) = SshKexCore.ParseEcdhReply(reply);

            // 5. Shared secret, exchange hash, signature verification.
            var rawSecret  = kex.ClientFinish(qS);
            var kEncoded   = kex.EncodeSharedSecret(rawSecret);
            var h          = ExchangeHash.Compute(kex.HashAlgorithm, vC, vS, iC, iS, kS, qC, qS, kEncoded);

            if (!SshSignature.Verify(kS, h, signatureBlob))
                throw new SshWireException("The server's host-key signature over the exchange hash is invalid!");

            // Fail closed: the signature proves key possession, not host identity, so a missing verifier
            // means the server is unauthenticated and the connection is trivially machine-in-the-middled.
            if (VerifyHostKey is null)
                throw new SshWireException("No host-key verification was supplied, so the server's identity cannot be established. "
                                           + "Pass a verifier (e.g. HostKeyPolicy.ForHost(host, port)), or SshHostKeyVerification.AcceptAnyUnsafe to skip it deliberately.");

            if (!VerifyHostKey(kS))
                throw new SshWireException("The server's host key was rejected by the host-key policy.");

            // 6. NEWKEYS + key derivation.
            var sessionId = h;
            await ExchangeNewKeysAsync(Pipe, CancellationToken).ConfigureAwait(false);

            var derive = SshKexCore.MakeDeriver(kex.HashAlgorithm, kEncoded, h, sessionId);
            var (sendCipher,    sendMac)    = SshKexCore.BuildDirection(negotiated.CipherClientToServer, negotiated.MacClientToServer, derive, Kdf.KeyLetter.EncryptionKeyClientToServer, Kdf.KeyLetter.InitialIVClientToServer, Kdf.KeyLetter.IntegrityKeyClientToServer);
            var (receiveCipher, receiveMac) = SshKexCore.BuildDirection(negotiated.CipherServerToClient, negotiated.MacServerToClient, derive, Kdf.KeyLetter.EncryptionKeyServerToClient, Kdf.KeyLetter.InitialIVServerToClient, Kdf.KeyLetter.IntegrityKeyServerToClient);

            return new SshTransportContext(sessionId, h, negotiated, kS, sendCipher, receiveCipher, sendMac, receiveMac);

        }

        #endregion

        #region ServerHandshakeAsync(Pipe, HostKey, LocalIdentification = null, CancellationToken = default)

        /// <summary>
        /// Perform the server side of the handshake.
        /// </summary>
        /// <param name="Pipe">The duplex transport.</param>
        /// <param name="HostKey">The server's Ed25519 host key.</param>
        /// <param name="LocalIdentification">Our identification string.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async ValueTask<SshTransportContext> ServerHandshakeAsync(IDuplexPipe               Pipe,
                                                                                ISshHostKey               HostKey,
                                                                                SshIdentificationString?  LocalIdentification  = null,
                                                                                String[]?                 Ciphers              = null,
                                                                                String[]?                 Macs                 = null,
                                                                                String[]?                 KeyExchanges         = null,
                                                                                CancellationToken         CancellationToken    = default)
        {

            var localId  = LocalIdentification ?? SshIdentificationString.Default;

            // 1. Version exchange.
            await SshVersionExchange.WriteAsync(Pipe.Output, localId, CancellationToken).ConfigureAwait(false);
            var remote   = await SshVersionExchange.ReadAsync(Pipe.Input, CancellationToken).ConfigureAwait(false);
            var vS       = localId.ToWireBytes();
            var vC       = remote.WireBytes;

            // 2. KEXINIT exchange + negotiation. The server offers only its host key's algorithms.
            var localKexInit = KexInitMessage.CreateLocal(IsServer: true, Ciphers, Macs, KeyExchanges, [.. HostKey.AlgorithmNames]);
            var iS           = localKexInit.Encode();
            await WritePacketAsync(Pipe.Output, NullTransportCipher.Instance, iS, CancellationToken).ConfigureAwait(false);

            var iC            = await SshPacketFraming.ReadPacketAsync(Pipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken).ConfigureAwait(false);
            var remoteKexInit = KexInitMessage.Decode(iC);
            var negotiated    = AlgorithmNegotiation.Negotiate(remoteKexInit, localKexInit, WeAreServer: true);
            SshKexCore.EnsureSupported(negotiated);

            // 3. KEX init: the client's ephemeral public value.
            var initPayload = await SshPacketFraming.ReadPacketAsync(Pipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken).ConfigureAwait(false);
            var qC          = SshKexCore.ParseEcdhInit(initPayload);

            // 4. Compute the server public value + shared secret, the exchange hash, then sign it.
            using var kex       = SshKeyExchange.Create(negotiated.KeyExchange);
            var (qS, rawSecret) = kex.ServerRespond(qC);
            var kEncoded        = kex.EncodeSharedSecret(rawSecret);

            var kS         = HostKey.PublicKeyBlob;
            var h          = ExchangeHash.Compute(kex.HashAlgorithm, vC, vS, iC, iS, kS, qC, qS, kEncoded);
            var signature  = HostKey.Sign(negotiated.HostKey, h);

            await WritePacketAsync(Pipe.Output, NullTransportCipher.Instance, SshKexCore.BuildEcdhReply(kS, qS, signature), CancellationToken).ConfigureAwait(false);

            // 5. NEWKEYS + key derivation.
            var sessionId = h;
            await ExchangeNewKeysAsync(Pipe, CancellationToken).ConfigureAwait(false);

            var derive = SshKexCore.MakeDeriver(kex.HashAlgorithm, kEncoded, h, sessionId);
            var (sendCipher,    sendMac)    = SshKexCore.BuildDirection(negotiated.CipherServerToClient, negotiated.MacServerToClient, derive, Kdf.KeyLetter.EncryptionKeyServerToClient, Kdf.KeyLetter.InitialIVServerToClient, Kdf.KeyLetter.IntegrityKeyServerToClient);
            var (receiveCipher, receiveMac) = SshKexCore.BuildDirection(negotiated.CipherClientToServer, negotiated.MacClientToServer, derive, Kdf.KeyLetter.EncryptionKeyClientToServer, Kdf.KeyLetter.InitialIVClientToServer, Kdf.KeyLetter.IntegrityKeyClientToServer);

            return new SshTransportContext(sessionId, h, negotiated, kS, sendCipher, receiveCipher, sendMac, receiveMac);

        }

        #endregion


        #region (private) ExchangeNewKeysAsync(Pipe, CancellationToken)

        private static async ValueTask ExchangeNewKeysAsync(IDuplexPipe Pipe, CancellationToken CancellationToken)
        {

            await WritePacketAsync(Pipe.Output, NullTransportCipher.Instance, [ (Byte) SshMessageNumber.NewKeys ], CancellationToken).ConfigureAwait(false);

            var newKeys = await SshPacketFraming.ReadPacketAsync(Pipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken).ConfigureAwait(false);

            if (newKeys.Length < 1 || newKeys[0] != (Byte) SshMessageNumber.NewKeys)
                throw new SshWireException("Expected SSH_MSG_NEWKEYS (21) to complete the key exchange!");

        }

        #endregion

        #region (private) WritePacketAsync(Output, Cipher, Payload, CancellationToken)

        private static async ValueTask WritePacketAsync(PipeWriter          Output,
                                                        SshTransportCipher  Cipher,
                                                        Byte[]              Payload,
                                                        CancellationToken   CancellationToken)
        {
            SshPacketFraming.WritePacket(Output, Cipher, Payload);
            await Output.FlushAsync(CancellationToken).ConfigureAwait(false);
        }

        #endregion

    }

}
