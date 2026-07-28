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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

/// <summary>
/// Shared interface of the TLS 1.3 handshake engine for both roles. The QUIC layer feeds in
/// received CRYPTO bytes per encryption level and pulls out those to send; derived keys appear as
/// <see cref="HandshakeSecrets"/> / <see cref="ApplicationSecrets"/>.
/// </summary>
public interface ITlsHandshake : IDisposable
{
    /// <summary>
    /// Hands (ordered) received CRYPTO bytes of a level to the handshake machine.
    /// </summary>
    void ProvideCrypto(EncryptionLevel level, ReadOnlySpan<byte> data);

    /// <summary>
    /// Fetches the next CRYPTO message to send (with its target level), if any.
    /// </summary>
    bool TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] data);

    CipherSuite? NegotiatedCipherSuite { get; }
    HandshakeTrafficSecrets? HandshakeSecrets { get; }
    ApplicationTrafficSecrets? ApplicationSecrets { get; }

    /// <summary>
    /// <c>true</c> once the handshake is complete from this side's point of view.
    /// </summary>
    bool IsComplete { get; }

    /// <summary>
    /// The peer's raw quic_transport_parameters (opaque to TLS).
    /// </summary>
    byte[]? PeerQuicTransportParameters { get; }

    /// <summary>
    /// The <c>client_early_traffic_secret</c> for 0-RTT (RFC 8446 §7.1), once available: on the client
    /// when offering early_data, on the server when accepting. <c>null</c> when no 0-RTT is involved.
    /// </summary>
    byte[]? EarlyTrafficSecret { get; }

    /// <summary>
    /// The cipher suite of the 0-RTT keys (that of the ticket); needed because it is fixed before the ServerHello.
    /// </summary>
    CipherSuite? EarlyDataCipherSuite { get; }

    /// <summary>
    /// <c>true</c> when 0-RTT (early_data) was actually accepted (client: per EncryptedExtensions).
    /// </summary>
    bool EarlyDataAccepted { get; }

    /// <summary>
    /// TLS keying-material exporter (RFC 8446 §7.5): provides both sides with identical keying
    /// material for the same label and context (e.g. for channel binding or the WebTransport
    /// exporter, draft-webtrans-http3 §4.7). Only available after the application secrets have been
    /// derived — before that, <see cref="InvalidOperationException"/>.
    /// </summary>
    byte[] ExportKeyingMaterial(string label, ReadOnlySpan<byte> context, int length);
}
