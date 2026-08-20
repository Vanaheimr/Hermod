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
using System.Buffers.Binary;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// One control connection: a sequence of signed CBOR requests, each answered
    /// by exactly one signed CBOR response.
    ///
    /// Every message is framed by a four byte big endian length prefix. CBOR is
    /// self-delimiting, but an explicit length lets the service reject an absurd
    /// message before decoding a single byte of it.
    ///
    ///     +--------+------------------------+
    ///     | length |   CBOR signed message  |
    ///     | 4 byte |      length bytes      |
    ///     +--------+------------------------+
    ///
    /// </summary>
    internal sealed class ControlConnection
    {

        #region Data

        /// <summary>
        /// The length of the frame length prefix in bytes.
        /// </summary>
        public const Int32 FrameHeaderLength = 4;

        private readonly Socket                      socket;
        private readonly RendezvousManager           manager;
        private readonly RendezvousOptions           options;
        private readonly ControlKeyRing              keyRing;
        private readonly NonceCache                  nonceCache;
        private readonly ControlSigner               responseSigner;
        private readonly TimeProvider                timeProvider;
        private readonly ILogger<ControlConnection>  logger;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new control connection.
        /// </summary>
        /// <param name="Socket">The accepted TCP socket.</param>
        /// <param name="Manager">The rendezvous manager.</param>
        /// <param name="Options">The configuration of the rendezvous service.</param>
        /// <param name="KeyRing">The keys that may authorize control commands.</param>
        /// <param name="NonceCache">The nonces of recently accepted requests.</param>
        /// <param name="ResponseSigner">The key this service signs its responses with.</param>
        /// <param name="TimeProvider">A time provider.</param>
        /// <param name="Logger">A logger.</param>
        public ControlConnection(Socket                      Socket,
                                 RendezvousManager           Manager,
                                 RendezvousOptions           Options,
                                 ControlKeyRing              KeyRing,
                                 NonceCache                  NonceCache,
                                 ControlSigner               ResponseSigner,
                                 TimeProvider                TimeProvider,
                                 ILogger<ControlConnection>  Logger)
        {

            this.socket          = Socket;
            this.manager         = Manager;
            this.options         = Options;
            this.keyRing         = KeyRing;
            this.nonceCache      = NonceCache;
            this.responseSigner  = ResponseSigner;
            this.timeProvider    = TimeProvider;
            this.logger          = Logger;

        }

        #endregion


        #region RunAsync(CancellationToken)

        /// <summary>
        /// Read and execute requests until the client disconnects.
        /// </summary>
        /// <param name="CancellationToken">A token to stop this connection.</param>
        public async Task RunAsync(CancellationToken CancellationToken)
        {

            var remoteSocket = IPSocket.FromIPEndPoint(socket.RemoteEndPoint);

            logger.LogDebug("Control connection from {RemoteSocket} accepted.", remoteSocket);

            try
            {

                socket.NoDelay = true;

                await using var stream = new NetworkStream(socket, ownsSocket: false);

                while (!CancellationToken.IsCancellationRequested)
                {

                    #region Read one frame, but do not wait forever

                    using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                    idleTimeout.CancelAfter(options.ControlIdleTimeout);

                    // Cancelling a pending socket read is a best effort operation on
                    // some platforms, but shutting down the socket always ends it.
                    using var cancellation = idleTimeout.Token.Register(
                                                 static socket => ((Socket) socket!).ShutdownSafely(SocketShutdown.Both),
                                                 socket
                                             );

                    var frame = await ReadFrameAsync(stream, idleTimeout.Token).ConfigureAwait(false);

                    if (frame.Outcome == FrameOutcome.EndOfStream)
                    {

                        if (idleTimeout.IsCancellationRequested && !CancellationToken.IsCancellationRequested)
                            logger.LogDebug("Control connection from {RemoteSocket} was idle for more than {Timeout}.",
                                            remoteSocket, options.ControlIdleTimeout);

                        return;

                    }

                    if (frame.Outcome == FrameOutcome.TooLarge)
                    {

                        await SendAsync(
                                  stream,
                                  new ControlResponse(
                                      ResponseCode.CommandTooLong,
                                      $"A control message must not be longer than {options.MaxFrameLength} bytes!"
                                  ),
                                  CancellationToken
                              ).ConfigureAwait(false);

                        logger.LogWarning("Control connection from {RemoteSocket} announced an oversized frame of {Length} bytes!",
                                          remoteSocket, frame.AnnouncedLength);

                        return;

                    }

                    #endregion

                    var response = Execute(frame.Payload!, remoteSocket);

                    await SendAsync(stream, response, CancellationToken).ConfigureAwait(false);

                }

            }
            catch (OperationCanceledException)
            { }
            catch (ObjectDisposedException)
            { }
            catch (SocketException e)
            {
                logger.LogDebug("Control connection from {RemoteSocket} was reset ({SocketError}).",
                                remoteSocket, e.SocketErrorCode);
            }
            catch (IOException e)
            {
                logger.LogDebug("Control connection from {RemoteSocket} failed: {Message}",
                                remoteSocket, e.Message);
            }
            finally
            {

                socket.ShutdownSafely(SocketShutdown.Both);

                logger.LogDebug("Control connection from {RemoteSocket} closed.", remoteSocket);

            }

        }

        #endregion

        #region (private) Execute(Frame, RemoteSocket)

        /// <summary>
        /// Verify and execute one signed control request.
        /// </summary>
        private ControlResponse Execute(Byte[]      Frame,
                                        IPSocket?   RemoteSocket)
        {

            var now = timeProvider.GetUtcNow();

            #region The message must be a well-formed signed CBOR message...

            if (!SignedMessage.TryParse(Frame, out var message, out var errorResponse))
                return new ControlResponse(ResponseCode.InvalidSyntax, errorResponse);

            if (!ControlRequest.TryParse(message.Payload, out var request, out errorResponse))
                return new ControlResponse(ResponseCode.InvalidSyntax, errorResponse);

            #endregion

            #region ...it must be fresh...

            var age = now - request.Timestamp;

            if (age > options.MaxClockSkew || age < -options.MaxClockSkew)
            {

                logger.LogWarning("Rejected a control request from {RemoteSocket}: its timestamp is off by {Age}!",
                                  RemoteSocket, age);

                return new ControlResponse(
                           ResponseCode.Unauthorized,
                           $"The timestamp of the request is off by {age.TotalSeconds:F0} seconds, at most {options.MaxClockSkew.TotalSeconds:F0} are accepted!",
                           RequestNonce: request.Nonce
                       );

            }

            #endregion

            #region ...it must be signed by enough known and currently valid keys...

            if (!message.TryVerify(keyRing,
                                   now,
                                   options.RequiredSignatures,
                                   out var verifiedBy,
                                   out errorResponse))
            {

                logger.LogWarning("Rejected an unauthorized control request from {RemoteSocket}: {Error}",
                                  RemoteSocket, errorResponse);

                return new ControlResponse(
                           ResponseCode.Unauthorized,
                           errorResponse,
                           RequestNonce: request.Nonce
                       );

            }

            #endregion

            #region ...and it must not be a replay

            if (!nonceCache.TryUse(request.Nonce, now, options.MaxClockSkew))
            {

                logger.LogWarning("Rejected a replayed control request from {RemoteSocket}, signed by {Keys}!",
                                  RemoteSocket, String.Join(", ", verifiedBy.Select(key => key.Id)));

                return new ControlResponse(
                           ResponseCode.Unauthorized,
                           "This request was already executed!",
                           RequestNonce: request.Nonce
                       );

            }

            #endregion

            logger.LogInformation("Executing {Command} from {RemoteSocket}, authorized by {Keys}.",
                                  request.Command, RemoteSocket, String.Join(", ", verifiedBy.Select(key => key.Id)));

            #region Execute

            // Whoever reaches this TCP port is known by nothing but the keys that
            // signed the request: those keys own the rendezvous they open, and
            // only they may close it again.
            var authorization = new ControlAuthorization(verifiedBy);

            RendezvousSession?  session = null;
            CommandResponse     commandResponse;

            if (request.Command is ConnectPortsCommand connectPorts)
                manager.TryConnectPorts   (connectPorts,    authorization, out session, out commandResponse);

            else if (request.Command is DisconnectPortsCommand disconnectPorts)
                manager.TryDisconnectPorts(disconnectPorts, authorization, out session, out commandResponse);

            else
                commandResponse = CommandResponse.Error(
                                      ResponseCode.UnknownCommand,
                                      $"Unknown command '{request.Command.CommandName}'!"
                                  );

            #endregion

            return ControlResponse.From(
                       commandResponse,
                       session,
                       request.Nonce,
                       now
                   );

        }

        #endregion


        #region (private, static) ReadFrameAsync(Stream, CancellationToken)

        private enum FrameOutcome
        {
            Frame,
            EndOfStream,
            TooLarge
        }

        private readonly record struct FrameResult(FrameOutcome  Outcome,
                                                   Byte[]?       Payload,
                                                   Int32         AnnouncedLength);

        /// <summary>
        /// Read one length prefixed frame.
        /// </summary>
        private async Task<FrameResult> ReadFrameAsync(Stream             Stream,
                                                       CancellationToken  CancellationToken)
        {

            var header = new Byte[FrameHeaderLength];

            if (!await ReadExactlyAsync(Stream, header, CancellationToken).ConfigureAwait(false))
                return new FrameResult(FrameOutcome.EndOfStream, null, 0);

            var length = BinaryPrimitives.ReadInt32BigEndian(header);

            if (length <= 0 || length > options.MaxFrameLength)
                return new FrameResult(FrameOutcome.TooLarge, null, length);

            var payload = ArrayPool<Byte>.Shared.Rent(length);

            try
            {

                if (!await ReadExactlyAsync(Stream, payload.AsMemory(0, length), CancellationToken).ConfigureAwait(false))
                    return new FrameResult(FrameOutcome.EndOfStream, null, length);

                return new FrameResult(FrameOutcome.Frame, payload[..length], length);

            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(payload);
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

        #region (private) SendAsync(Stream, Response, CancellationToken)

        /// <summary>
        /// Sign and send the given response.
        /// </summary>
        private async Task SendAsync(Stream             Stream,
                                     ControlResponse    Response,
                                     CancellationToken  CancellationToken)
        {

            var payload  = Response.ToByteArray();
            var message  = SignedMessage.Create(payload, responseSigner).ToByteArray();
            var frame    = new Byte[FrameHeaderLength + message.Length];

            BinaryPrimitives.WriteInt32BigEndian(frame, message.Length);
            message.CopyTo(frame, FrameHeaderLength);

            await Stream.WriteAsync(frame, CancellationToken).ConfigureAwait(false);
            await Stream.FlushAsync(CancellationToken).      ConfigureAwait(false);

        }

        #endregion

    }

}
