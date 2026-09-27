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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Puts a WebSocket server on an HTTP path.
    /// </summary>
    /// <remarks>
    /// <b>One listener, one certificate, one port, several paths.</b> Until this
    /// existed a WebSocket needed a TCP server of its own, which meant a second
    /// port and a second copy of the certificate for anybody who wanted a
    /// WebSocket and ordinary HTTP side by side - and plenty of protocols want
    /// exactly that. XMPP over WebSocket (RFC 7395) and its file uploads
    /// (XEP-0363) are the case this was written for: <c>/xmpp</c> speaks frames
    /// and <c>/upload</c> takes a PUT, and no client should have to be told two
    /// numbers.
    ///
    /// <b>The handshake is not here.</b> This decides whether a request is
    /// asking for an upgrade and, if so, hands the connection to
    /// <see cref="AWebSocketServer.AcceptUpgradedConnectionAsync"/> along with
    /// the request that asked - and the WebSocket server's own, single
    /// implementation of RFC 6455 section 4.2.1 does the rest: validate, choose
    /// the subprotocol, negotiate permessage-deflate, write the 101. Two copies
    /// of a security handshake is how one of them ends up a version behind.
    ///
    /// <b>Nor is the firewall.</b> The TCP connection the request arrived on
    /// goes along as well, and the WebSocket server asks its handlers of
    /// OnValidateTCPConnection about it as though it had accepted the
    /// connection itself - which on a port of its own it would have.
    /// </remarks>
    public static class WebSocketUpgrade
    {

        #region IsUpgradeRequest(Request)

        /// <summary>
        /// Is this request asking to stop speaking HTTP?
        /// </summary>
        /// <remarks>
        /// RFC 6455 section 4.1: <c>Upgrade: websocket</c> and a
        /// <c>Connection</c> that lists <c>Upgrade</c> among its tokens. The
        /// second is a list and is checked as one - "keep-alive, Upgrade" is
        /// what a good many clients send, and a plain equality test refuses them
        /// for no reason.
        ///
        /// Everything else about the handshake - the key, the version, the
        /// subprotocols - is the WebSocket server's to judge, and it refuses
        /// with the right status if it does not like what it finds. What is
        /// decided here is only which of the two protocols this path is being
        /// asked for.
        /// </remarks>
        public static Boolean IsUpgradeRequest(HTTPRequest Request)
        {

            var upgrade = Request.GetHeaderField("Upgrade") ?? "";

            if (!upgrade.Trim().Equals("websocket", StringComparison.OrdinalIgnoreCase))
                return false;

            var connection = Request.GetHeaderField("Connection") ?? "";

            return connection.Split(',').
                              Any(token => token.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase));

        }

        #endregion

        #region For(WebSocketServer)

        /// <summary>
        /// An HTTP handler that turns this path into a WebSocket endpoint.
        /// </summary>
        /// <param name="WebSocketServer">
        /// The WebSocket server that will speak on the connection. It must
        /// <b>not</b> be started: it is never going to accept anything itself,
        /// it is only being lent its protocol. Its handlers of
        /// OnValidateTCPConnection are still asked about every connection, when
        /// it is handed over.
        /// </param>
        /// <example>
        /// <code>
        /// api.AddHandler(HTTPMethod.GET,
        ///                HTTPPath.Parse("/xmpp"),
        ///                HTTPDelegate: WebSocketUpgrade.For(webSocketServer));
        /// </code>
        /// </example>
        public static HTTPDelegate For(AWebSocketServer WebSocketServer)

            => request => {

                   #region Not an upgrade request

                   // RFC 9110, section 15.5.23. The same answer the WebSocket
                   // server gives on a port of its own, and it is the useful one:
                   // it names what this path is for, so a browser opening it by
                   // hand is told rather than left with a blank page.
                   if (!IsUpgradeRequest(request))
                       return Task.FromResult(
                                  new HTTPResponse.Builder(request) {
                                      HTTPStatusCode  = HTTPStatusCode.UpgradeRequired,
                                      Upgrade         = "websocket",
                                      Connection      = ConnectionType.Close,
                                      ContentType     = HTTPContentType.Text.PLAIN,
                                      Content         = "This is a WebSocket endpoint.".ToUTF8Bytes()
                                  }.AsImmutable
                              );

                   #endregion

                   #region No stream to hand over

                   // Only reachable if this handler is called by something that
                   // is not the HTTP server - a test harness, a pipeline. Said
                   // out loud rather than thrown, because a NullReferenceException
                   // here would be read as a fault in the WebSocket code.
                   if (request.NetworkStream is null)
                       return Task.FromResult(
                                  new HTTPResponse.Builder(request) {
                                      HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                      Connection      = ConnectionType.Close,
                                      ContentType     = HTTPContentType.Text.PLAIN,
                                      Content         = "This HTTP server cannot hand over a connection.".ToUTF8Bytes()
                                  }.AsImmutable
                              );

                   #endregion

                   // The status says what is about to happen; the worker is what
                   // actually happens. AHTTPServer does not write this response -
                   // the WebSocket server writes the real 101 itself, because
                   // only it knows the Sec-WebSocket-Accept and what was
                   // negotiated. See HTTPResponse.UpgradeWorker.
                   return Task.FromResult(
                              new HTTPResponse.Builder(request) {

                                  HTTPStatusCode  = HTTPStatusCode.SwitchingProtocols,
                                  Upgrade         = "websocket",
                                  Connection      = ConnectionType.Upgrade,

                                  UpgradeWorker   = (response, stream)

                                      => WebSocketServer.AcceptUpgradedConnectionAsync(
                                             NetworkStream:      stream,
                                             LocalSocket:        request.LocalSocket,
                                             RemoteSocket:       request.RemoteSocket,
                                             RequestBytes:       $"{request.EntirePDU}\r\n\r\n".ToUTF8Bytes(),
                                             ClientCertificate:  request.ClientCertificate,

                                             // What the WebSocket server's handlers of
                                             // OnValidateTCPConnection are asked about - the
                                             // connection the HTTP server accepted, since the
                                             // WebSocket server accepted none of its own.
                                             TCPConnection:      request.TCPConnection,

                                             CancellationToken:  request.CancellationToken
                                         )

                              }.AsImmutable
                          );

               };

        #endregion

    }

}
