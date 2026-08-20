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

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// A client of the rendezvous control endpoint: it takes a remote socket,
    /// one or more private keys and a port specification, sends the signed CBOR
    /// request and reports what came back.
    ///
    /// The connection is opened per request and closed afterwards, which keeps
    /// this client simple and stateless - a control command is a rare event,
    /// not a data stream.
    /// </summary>
    public sealed class RendezvousControlClient : IDisposable
    {

        #region Data

        private readonly ControlSigner[]  signers;
        private readonly ControlKeyRing?  serverKeys;
        private readonly ILogger          logger;
        private          Boolean          disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The remote control endpoint.
        /// </summary>
        public IPSocket   RemoteSocket   { get; }

        /// <summary>
        /// The maximum time to wait for a response.
        /// </summary>
        public TimeSpan   Timeout        { get; }

        /// <summary>
        /// The maximum length of a response frame in bytes.
        /// </summary>
        public Int32      MaxFrameLength { get; }

        /// <summary>
        /// The keys this client signs its requests with.
        /// </summary>
        public IReadOnlyList<ControlSigner>  Signers
            => signers;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new rendezvous control client.
        /// </summary>
        /// <param name="RemoteSocket">The remote control endpoint.</param>
        /// <param name="Signers">One or more private keys to sign the requests with.</param>
        /// <param name="ServerKeys">The public keys of the service. When given, a response with an unknown or invalid signature is rejected.</param>
        /// <param name="Timeout">An optional timeout, 30 seconds otherwise.</param>
        /// <param name="MaxFrameLength">The maximum length of a response frame in bytes.</param>
        /// <param name="Logger">An optional logger.</param>
        public RendezvousControlClient(IPSocket          RemoteSocket,
                                       ControlSigner[]   Signers,
                                       ControlKeyRing?   ServerKeys       = null,
                                       TimeSpan?         Timeout          = null,
                                       Int32             MaxFrameLength   = 65536,
                                       ILogger?          Logger           = null)
        {

            if (Signers is null || Signers.Length == 0)
                throw new ArgumentException("At least one signer is required, as every request must be signed!", nameof(Signers));

            this.RemoteSocket    = RemoteSocket;
            this.signers         = [.. Signers];
            this.serverKeys      = ServerKeys;
            this.Timeout         = Timeout ?? TimeSpan.FromSeconds(30);
            this.MaxFrameLength  = MaxFrameLength;
            this.logger          = Logger  ?? NullLogger.Instance;

        }

        /// <summary>
        /// Create a new rendezvous control client with a single signing key.
        /// </summary>
        /// <param name="RemoteSocket">The remote control endpoint.</param>
        /// <param name="Signer">The private key to sign the requests with.</param>
        /// <param name="ServerKeys">The public keys of the service.</param>
        /// <param name="Timeout">An optional timeout, 30 seconds otherwise.</param>
        /// <param name="Logger">An optional logger.</param>
        public RendezvousControlClient(IPSocket          RemoteSocket,
                                       ControlSigner     Signer,
                                       ControlKeyRing?   ServerKeys   = null,
                                       TimeSpan?         Timeout      = null,
                                       ILogger?          Logger       = null)

            : this(RemoteSocket, [Signer], ServerKeys, Timeout, 65536, Logger)

        { }

        #endregion


        #region ConnectPortsAsync   (Ports, Profile = null, Description = null, CancellationToken = default)

        /// <summary>
        /// Ask the service to open a rendezvous.
        ///
        /// The keys of this client become the owners of the new rendezvous:
        /// only they - or an administrator key - may close it again.
        /// </summary>
        /// <param name="Ports">Two or more port specifications, use PortSpecification.Random for '?'.</param>
        /// <param name="Profile">An optional transfer profile.</param>
        /// <param name="Description">An optional description, e.g. "SSH rendezvous for maintenance work".</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<ControlClientResponse> ConnectPortsAsync(IEnumerable<PortSpecification>  Ports,
                                                             TransferProfile?                Profile             = null,
                                                             String?                         Description         = null,
                                                             CancellationToken               CancellationToken   = default)

            => SendAsync(
                   new ConnectPortsCommand([.. Ports], Profile, Description),
                   CancellationToken
               );

        #endregion

        #region DisconnectPortsAsync(Ports, Description = null, CancellationToken = default)

        /// <summary>
        /// Ask the service to close a rendezvous.
        /// This only works for a rendezvous opened by one of the keys of this
        /// client, unless one of them is an administrator key.
        /// </summary>
        /// <param name="Ports">One or more TCP ports of the rendezvous.</param>
        /// <param name="Description">An optional comment why this rendezvous is closed.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<ControlClientResponse> DisconnectPortsAsync(IEnumerable<IPPort>  Ports,
                                                                String?              Description         = null,
                                                                CancellationToken    CancellationToken   = default)

            => SendAsync(
                   new DisconnectPortsCommand([.. Ports], Description),
                   CancellationToken
               );

        #endregion

        #region SendAsync(Command, CancellationToken = default)

        /// <summary>
        /// Sign and send the given command, and wait for the response.
        /// </summary>
        /// <param name="Command">A control command.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<ControlClientResponse> SendAsync(RendezvousCommand  Command,
                                                           CancellationToken  CancellationToken   = default)
        {

            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(Command);

            var request = new ControlRequest(Command);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            timeout.CancelAfter(Timeout);

            try
            {

                using var client = new TcpClient();

                await client.ConnectAsync(RemoteSocket.IPAddress.ToDotNet(),
                                          RemoteSocket.Port.ToUInt16(),
                                          timeout.Token).
                             ConfigureAwait(false);

                client.NoDelay = true;

                await using var stream = client.GetStream();

                #region Send the signed request

                var payload  = request.ToByteArray();
                var message  = SignedMessage.Create(payload, signers).ToByteArray();
                var frame    = new Byte[4 + message.Length];

                BinaryPrimitives.WriteInt32BigEndian(frame, message.Length);
                message.CopyTo(frame, 4);

                await stream.WriteAsync(frame, timeout.Token).ConfigureAwait(false);
                await stream.FlushAsync(timeout.Token).       ConfigureAwait(false);

                #endregion

                #region Read the signed response

                var header = new Byte[4];

                if (!await ReadExactlyAsync(stream, header, timeout.Token).ConfigureAwait(false))
                    return ControlClientResponse.Failed(request, "The control endpoint closed the connection!");

                var length = BinaryPrimitives.ReadInt32BigEndian(header);

                if (length <= 0 || length > MaxFrameLength)
                    return ControlClientResponse.Failed(request, $"The control endpoint announced an invalid response of {length} bytes!");

                var responseFrame = new Byte[length];

                if (!await ReadExactlyAsync(stream, responseFrame, timeout.Token).ConfigureAwait(false))
                    return ControlClientResponse.Failed(request, "The control endpoint closed the connection while sending its response!");

                #endregion

                #region Verify and parse it

                if (!SignedMessage.TryParse(responseFrame, out var signedResponse, out var errorResponse))
                    return ControlClientResponse.Failed(request, $"Could not read the response: {errorResponse}");

                IReadOnlyList<ControlKey> signedBy = [];

                if (serverKeys is not null &&
                    !signedResponse.TryVerify(serverKeys,
                                              DateTimeOffset.UtcNow,
                                              RequiredSignatures: 1,
                                              out signedBy,
                                              out errorResponse))
                {
                    return ControlClientResponse.Failed(request, $"The response is not properly signed: {errorResponse}");
                }

                if (!ControlResponse.TryParse(signedResponse.Payload, out var response, out errorResponse))
                    return ControlClientResponse.Failed(request, $"Could not read the response: {errorResponse}");

                #endregion

                #region The response must answer *this* request

                if (response.RequestNonce is not null &&
                    !CryptographicOperations.FixedTimeEquals(response.RequestNonce, request.Nonce))
                {
                    return ControlClientResponse.Failed(request, "The response answers a different request!");
                }

                #endregion

                logger.LogDebug("{Command} -> {Response}", Command, response);

                return new ControlClientResponse(request, response, signedBy, null);

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {
                return ControlClientResponse.Failed(request, $"The control endpoint did not answer within {Timeout.TotalSeconds:F0} seconds!");
            }
            catch (SocketException e)
            {
                return ControlClientResponse.Failed(request, $"Could not reach the control endpoint {RemoteSocket}: {e.SocketErrorCode}!");
            }
            catch (IOException e)
            {
                return ControlClientResponse.Failed(request, $"The control connection failed: {e.Message}");
            }

        }

        #endregion

        #region (private, static) ReadExactlyAsync(Stream, Buffer, CancellationToken)

        private static async Task<Boolean> ReadExactlyAsync(Stream             Stream,
                                                            Memory<Byte>       Buffer,
                                                            CancellationToken  CancellationToken)
        {

            var offset = 0;

            while (offset < Buffer.Length)
            {

                var read = await Stream.ReadAsync(Buffer[offset..], CancellationToken).ConfigureAwait(false);

                if (read == 0)
                    return false;

                offset += read;

            }

            return true;

        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Dispose the signers of this client.
        /// </summary>
        public void Dispose()
        {

            if (disposed)
                return;

            disposed = true;

            GC.SuppressFinalize(this);

        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a text representation of this client.
        /// </summary>
        public override String ToString()

            => $"Rendezvous control client -> {RemoteSocket}, signing with {String.Join(", ", signers.Select(signer => signer.KeyId))}";

        #endregion

    }

}
