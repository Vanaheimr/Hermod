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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    #region WebSocketOverHTTP3Tunnel

    /// <summary>
    /// The tunnel of an extended CONNECT over HTTP/3, for what reads and writes
    /// the tunnel of one over HTTP/2: the bridge streams of
    /// <see cref="WebSocketOverHTTP2"/>, which are the same over both.
    /// </summary>
    /// <param name="Tunnel">The tunnel over HTTP/3.</param>
    public sealed class WebSocketOverHTTP3Tunnel(Http3Tunnel Tunnel) : HTTP2.IHTTP2Tunnel
    {

        /// <summary>
        /// The tunnel over HTTP/3.
        /// </summary>
        public Http3Tunnel HTTP3Tunnel => Tunnel;

        public Task<Byte[]?> ReadAsync(CancellationToken CancellationToken)
            => Tunnel.ReadAsync(CancellationToken);

        public Task WriteAsync(Byte[] Data, CancellationToken CancellationToken)
            => Tunnel.WriteAsync(Data, CancellationToken);

    }

    #endregion

    #region WebSocketOverHTTP3ClientStream

    /// <summary>
    /// What a WebSocket client writes its HTTP/1.1 upgrade request to and reads
    /// the answer and its WebSocket frames from, while the WebSocket is opened
    /// by an extended CONNECT on an HTTP/3 connection (RFC 9220): see
    /// <see cref="AWebSocketOverHTTPClientStream"/>.
    /// </summary>
    /// <param name="Client">The HTTP/3 client, connected. It was made for this WebSocket and is closed with it.</param>
    /// <param name="AnswerTimeout">How long the server's answer to the CONNECT is waited for.</param>
    public sealed class WebSocketOverHTTP3ClientStream(Http3Client  Client,
                                                       TimeSpan     AnswerTimeout) : AWebSocketOverHTTPClientStream
    {

        #region Data

        private Http3Tunnel? tunnel;

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP/3 client the WebSocket is opened with.
        /// </summary>
        public Http3Client   HTTP3Client   => Client;

        /// <summary>
        /// The tunnel of the WebSocket, once the server accepted it.
        /// </summary>
        public Http3Tunnel?  Tunnel        => tunnel;

        #endregion


        #region (protected override) ConnectAsync(Authority, Path, Fields, CancellationToken)

        protected override async Task<(Int32 Status, IEnumerable<(String Name, String Value)> Headers, HTTP2.IHTTP2Tunnel? Tunnel)>

            ConnectAsync(String                              Authority,
                         String                              Path,
                         List<(String Name, String Value)>   Fields,
                         CancellationToken                   CancellationToken)

        {

            var streamId  = await Client.QueryAsync(
                                      connection => connection.SendExtendedConnect(
                                                        Authority,
                                                        Path,
                                                        WebSocketOverHTTP2.Protocol,
                                                        [.. Fields.Select(field => new HeaderField(field.Name, field.Value))]
                                                    ),
                                      CancellationToken
                                  ).ConfigureAwait(false);

            var                         status    = 0;
            IReadOnlyList<HeaderField>  headers   = [];
            Http3Tunnel?                accepted  = null;
            var                         gone      = false;

            var answered  = await Client.WaitUntilAsync(
                                      connection => {

                                          if (connection.TryGetConnectResponse(streamId, out status, out headers, out accepted))
                                              return true;

                                          gone = connection.IsClosed  ||
                                                 connection.IsClosing ||
                                                 connection.IsDraining ||
                                                 connection.IsIdleTimedOut ||
                                                 connection.IsRequestRejected(streamId) ||
                                                 connection.RequestResetErrorCode(streamId).HasValue;

                                          return gone;

                                      },
                                      AnswerTimeout,
                                      CancellationToken
                                  ).ConfigureAwait(false);

            if (gone)
                throw new IOException("The HTTP/3 connection ended, or the server reset the stream, before the extended CONNECT of the WebSocket was answered.");

            if (!answered)
                throw new IOException($"The server did not answer the extended CONNECT of the WebSocket within {AnswerTimeout.TotalSeconds:0.#} seconds.");

            tunnel = accepted;

            return (status,
                    headers.Select(field => (field.Name, field.Value)),
                    accepted is not null
                        ? new WebSocketOverHTTP3Tunnel(accepted)
                        : null);

        }

        #endregion

        #region (protected override) LetGo()

        /// <summary>
        /// End the tunnel and close the HTTP/3 connection with its socket: both
        /// were made for this WebSocket.
        /// </summary>
        protected override void LetGo()
        {

            _ = Task.Run(async () => {
                try
                {
                    tunnel?.Complete();
                    await Client.CloseAsync().ConfigureAwait(false);
                }
                catch
                { }
                try
                {
                    await Client.DisposeAsync().ConfigureAwait(false);
                }
                catch
                { }
            });

        }

        #endregion

    }

    #endregion

}
