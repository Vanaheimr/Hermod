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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The established state of an SSH transport after a successful key exchange and NEWKEYS: the
    /// session id, the negotiated algorithms, the exchange hash, the peer's host key and the two
    /// directional ciphers now protecting the connection.
    /// </summary>
    public sealed class SshTransportContext : IDisposable
    {

        #region Properties

        /// <summary>
        /// The session identifier (the exchange hash H of the first key exchange).
        /// </summary>
        public Byte[]                SessionId        { get; }

        /// <summary>
        /// The exchange hash H of the most recent key exchange.
        /// </summary>
        public Byte[]                ExchangeHash     { get; }

        /// <summary>
        /// The algorithms negotiated during KEXINIT.
        /// </summary>
        public NegotiatedAlgorithms  Algorithms       { get; }

        /// <summary>
        /// The server's host-key blob (K_S) as received/sent.
        /// </summary>
        public Byte[]                ServerHostKey    { get; }

        /// <summary>
        /// The cipher protecting outbound packets.
        /// </summary>
        public SshTransportCipher    SendCipher       { get; }

        /// <summary>
        /// The cipher protecting inbound packets.
        /// </summary>
        public SshTransportCipher    ReceiveCipher    { get; }

        /// <summary>
        /// The encrypt-then-MAC for outbound packets, or null for AEAD ciphers.
        /// </summary>
        public ISshMac?              SendMac          { get; }

        /// <summary>
        /// The encrypt-then-MAC for inbound packets, or null for AEAD ciphers.
        /// </summary>
        public ISshMac?              ReceiveMac       { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an established transport context.
        /// </summary>
        public SshTransportContext(Byte[]                SessionId,
                                   Byte[]                ExchangeHash,
                                   NegotiatedAlgorithms  Algorithms,
                                   Byte[]                ServerHostKey,
                                   SshTransportCipher    SendCipher,
                                   SshTransportCipher    ReceiveCipher,
                                   ISshMac?              SendMac        = null,
                                   ISshMac?              ReceiveMac     = null)
        {
            this.SessionId      = SessionId;
            this.ExchangeHash   = ExchangeHash;
            this.Algorithms     = Algorithms;
            this.ServerHostKey  = ServerHostKey;
            this.SendCipher     = SendCipher;
            this.ReceiveCipher  = ReceiveCipher;
            this.SendMac        = SendMac;
            this.ReceiveMac     = ReceiveMac;
        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Dispose the directional ciphers and MACs, wiping their key material.
        /// </summary>
        public void Dispose()
        {
            SendCipher.   Dispose();
            ReceiveCipher.Dispose();
            SendMac?.     Dispose();
            ReceiveMac?.  Dispose();
        }

        #endregion

    }

}
