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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The ClientHello of a TLS connection over TCP, read before any TLS
    /// stack has seen it: which stack takes the connection depends on what
    /// the client offers. A client that offers raw public keys (RFC 7250)
    /// is answered by the stack that speaks them, and every other client by
    /// SslStream, as before.
    /// </summary>
    /// <remarks>
    /// Nothing is decided here and nothing is answered: the bytes that were
    /// read are kept, and go in front of the stream for the stack that
    /// takes the connection (<see cref="PrefixedStream"/>). What cannot be
    /// read as a ClientHello offers no raw public keys, and is SslStream's
    /// to refuse.
    /// </remarks>
    public sealed class TLSClientHelloPeek
    {

        #region Data

        private const Int32  RecordHeaderLength      = 5;
        private const Byte   HandshakeRecord         = 22;
        private const Byte   ClientHelloMessage      = 1;

        // RFC 8446, section 5.1: a record of plaintext has at most 2^14 bytes.
        private const Int32  MaxRecordLength         = 16384;

        // A ClientHello with a post-quantum key share has about 2 kB. One that does
        // not fit here is not looked at.
        private const Int32  MaxClientHelloLength    = 65536;

        #endregion

        #region Properties

        /// <summary>
        /// Everything that was read from the stream, as it came.
        /// </summary>
        public Byte[]                Bytes                      { get; }

        /// <summary>
        /// The certificate types the client accepts from the server
        /// (server_certificate_type), or null where it sent none.
        /// </summary>
        public IReadOnlyList<Byte>?  ServerCertificateTypes     { get; }

        /// <summary>
        /// The certificate types the client can present
        /// (client_certificate_type), or null where it sent none.
        /// </summary>
        public IReadOnlyList<Byte>?  ClientCertificateTypes     { get; }

        /// <summary>
        /// Whether the client takes a raw public key from the server.
        /// </summary>
        public Boolean               TakesARawServerKey
            => ServerCertificateTypes?.Contains((Byte) TLSCertificateType.RawPublicKey) == true;

        /// <summary>
        /// Whether the client can show its own key raw.
        /// </summary>
        public Boolean               OffersARawClientKey
            => ClientCertificateTypes?.Contains((Byte) TLSCertificateType.RawPublicKey) == true;

        #endregion

        #region Constructor(s)

        private TLSClientHelloPeek(Byte[]                Bytes,
                                   IReadOnlyList<Byte>?  ServerCertificateTypes   = null,
                                   IReadOnlyList<Byte>?  ClientCertificateTypes   = null)
        {

            this.Bytes                   = Bytes;
            this.ServerCertificateTypes  = ServerCertificateTypes;
            this.ClientCertificateTypes  = ClientCertificateTypes;

        }

        #endregion


        #region (static) ReadAsync(Stream, CancellationToken)

        /// <summary>
        /// Read the ClientHello from the given stream: the records it comes
        /// in, and not one byte more.
        /// </summary>
        /// <param name="Stream">A stream at the very beginning of a TLS connection.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public static async Task<TLSClientHelloPeek> ReadAsync(Stream             Stream,
                                                               CancellationToken  CancellationToken)
        {

            var everything  = new MemoryStream();
            var handshake   = new MemoryStream();
            var header      = new Byte[RecordHeaderLength];

            while (true)
            {

                // One record: its header ...
                if (!await ReadExactly(Stream, header, everything, CancellationToken).ConfigureAwait(false))
                    break;

                var length = (header[3] << 8) | header[4];

                if (header[0] != HandshakeRecord || length == 0 || length > MaxRecordLength)
                    break;

                // ... and what it holds.
                var fragment = new Byte[length];

                if (!await ReadExactly(Stream, fragment, everything, CancellationToken).ConfigureAwait(false))
                    break;

                handshake.Write(fragment);

                if (handshake.Length < 4)
                    continue;

                var message        = handshake.GetBuffer();
                var messageLength  = 4 + ((message[1] << 16) | (message[2] << 8) | message[3]);

                if (message[0] != ClientHelloMessage || messageLength > MaxClientHelloLength)
                    break;

                // The ClientHello may go on in the next record.
                if (handshake.Length < messageLength)
                    continue;

                return ClientHelloParser.TryParse(message.AsSpan(0, messageLength), out var clientHello) && clientHello is not null
                           ? new TLSClientHelloPeek(everything.ToArray(), clientHello.ServerCertificateTypes, clientHello.ClientCertificateTypes)
                           : new TLSClientHelloPeek(everything.ToArray());

            }

            return new TLSClientHelloPeek(everything.ToArray());

        }

        #endregion

        #region (private static) ReadExactly(Stream, Buffer, Everything, CancellationToken)

        /// <summary>
        /// Fill the buffer from the stream. What was read is kept, also
        /// where the stream ended before the buffer was full.
        /// </summary>
        private static async Task<Boolean> ReadExactly(Stream             Stream,
                                                       Byte[]             Buffer,
                                                       MemoryStream       Everything,
                                                       CancellationToken  CancellationToken)
        {

            var filled = 0;

            while (filled < Buffer.Length)
            {

                var read = await Stream.ReadAsync(Buffer.AsMemory(filled), CancellationToken).ConfigureAwait(false);

                if (read == 0)
                    break;

                filled += read;

            }

            Everything.Write(Buffer, 0, filled);

            return filled == Buffer.Length;

        }

        #endregion

    }

}
