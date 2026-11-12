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

using System.Text;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    #region WebSocketHTTPVersion

    /// <summary>
    /// The version of HTTP a WebSocket client opens its WebSocket over.
    /// </summary>
    public enum WebSocketHTTPVersion
    {

        /// <summary>
        /// HTTP/1.1 and its Upgrade (RFC 6455, Section 4), and nothing else.
        /// </summary>
        HTTP1_1,

        /// <summary>
        /// HTTP/2 and its extended CONNECT (RFC 8441), and nothing else: a server
        /// that does not agree to "h2", or does not offer WebSockets over it, is
        /// not connected to.
        /// </summary>
        HTTP2,

        /// <summary>
        /// HTTP/3 and its extended CONNECT (RFC 9220), and nothing else: over
        /// QUIC, and so over TLS. A server that is not reached over it, or does
        /// not offer WebSockets over it, is not connected to.
        /// </summary>
        HTTP3,

        /// <summary>
        /// What the server agrees to: "h2" and "http/1.1" are offered (ALPN,
        /// RFC 7301), and HTTP/2 is used where the server chooses it and offers
        /// WebSockets over it, HTTP/1.1 otherwise. Without TLS there is nothing
        /// to agree with, and it is HTTP/1.1. HTTP/3 is used where the server
        /// said before that it is reached over it (Alt-Svc, RFC 7838), for as
        /// long as it said: see <see cref="WebSocketAlternativeServices"/>.
        /// </summary>
        Auto

    }

    #endregion


    #region WebSocketOverHTTP2

    /// <summary>
    /// A WebSocket over HTTP/2 (RFC 8441), for a WebSocket client and a
    /// WebSocket server that speak the opening handshake of HTTP/1.1.
    /// </summary>
    /// <remarks>
    /// <b>The handshake is translated, not written a second time.</b> Over
    /// HTTP/2 a WebSocket is opened by an extended CONNECT with
    /// <c>:protocol = websocket</c> and accepted by a 2xx; the frames of
    /// RFC 6455 then travel in the DATA frames of that stream. What a client
    /// offers and what a server accepts beside that - the subprotocol, the
    /// extensions, who the client is and what it shows for it - are the same
    /// header fields as over HTTP/1.1. So the client and the server of this
    /// library keep the handshake they have, with everything it checks and
    /// everything it refuses, and one stream on either side turns the request
    /// and the answer of the one into those of the other:
    /// <see cref="WebSocketOverHTTP2ClientStream"/> and
    /// <see cref="WebSocketOverHTTP2ServerStream"/>.
    ///
    /// What does not cross: <c>Upgrade</c>, <c>Connection</c>,
    /// <c>Sec-WebSocket-Key</c> and <c>Sec-WebSocket-Accept</c> have no meaning
    /// over HTTP/2 (RFC 8441, Section 5) and are neither sent nor expected
    /// there; each side is given the ones its handshake looks for.
    /// </remarks>
    public static class WebSocketOverHTTP2
    {

        #region Data

        /// <summary>
        /// The value of the <c>:protocol</c> pseudo-header field of a WebSocket.
        /// </summary>
        public const String Protocol = "websocket";

        private const String AcceptGUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>
        /// The header fields of the HTTP/1.1 handshake that do not go over HTTP/2.
        /// </summary>
        private static readonly HashSet<String> NotOverHTTP2 = new (StringComparer.OrdinalIgnoreCase) {
            "Host", "Connection", "Upgrade", "Sec-WebSocket-Key", "Sec-WebSocket-Accept",
            "Keep-Alive", "Proxy-Connection", "Transfer-Encoding", "Content-Length"
        };

        #endregion


        #region (internal) EndOfHead(Bytes, Length)

        /// <summary>
        /// Where the head of an HTTP/1.1 message ends: the index after its empty
        /// line, or -1 while there is none.
        /// </summary>
        internal static Int32 EndOfHead(Byte[] Bytes, Int32 Length)
        {

            for (var i = 3; i < Length; i++)
                if (Bytes[i - 3] == 0x0d && Bytes[i - 2] == 0x0a && Bytes[i - 1] == 0x0d && Bytes[i] == 0x0a)
                    return i + 1;

            return -1;

        }

        #endregion

        #region (internal) HeadOf(Bytes, Length, out FirstLine, out Headers)

        /// <summary>
        /// The first line and the header fields of the head of an HTTP/1.1 message.
        /// </summary>
        internal static void HeadOf(Byte[]                              Bytes,
                                    Int32                               Length,
                                    out String                          FirstLine,
                                    out List<(String Name, String Value)>  Headers)
        {

            var lines  = Encoding.UTF8.GetString(Bytes, 0, Length).Split("\r\n");

            FirstLine  = lines.Length > 0 ? lines[0] : "";
            Headers    = [];

            foreach (var line in lines.Skip(1))
            {

                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;

                Headers.Add((line[..colon].Trim(), line[(colon + 1)..].Trim()));

            }

        }

        #endregion

        #region (internal) IsAFieldOfOneLine(Name, Value)

        /// <summary>
        /// Whether a header field can be written into the head of an HTTP/1.1
        /// message as it is: a name that is a token and a value without a line
        /// break or a NUL, which would end the line, or the head, where its
        /// sender wants it to end.
        /// </summary>
        internal static Boolean IsAFieldOfOneLine(String Name, String Value)
        {

            if (Name.Length == 0)
                return false;

            foreach (var c in Name)
                if (c <= 0x20 || c >= 0x7f || c == ':')
                    return false;

            foreach (var c in Value)
                if (c == '\r' || c == '\n' || c == '\0')
                    return false;

            return true;

        }

        #endregion


        #region TryUpgradeRequestOf(ConnectHeaders, out RequestBytes, out Error)

        /// <summary>
        /// The HTTP/1.1 request asking for the upgrade to a WebSocket that says
        /// what the given extended CONNECT says.
        /// </summary>
        /// <param name="ConnectHeaders">The header fields of the extended CONNECT, its pseudo-header fields among them.</param>
        /// <param name="RequestBytes">The request, its empty line included.</param>
        /// <param name="Error">Why there is none.</param>
        public static Boolean TryUpgradeRequestOf(IEnumerable<(String Name, String Value)>  ConnectHeaders,
                                                  out Byte[]                                RequestBytes,
                                                  out String?                               Error)
        {

            RequestBytes   = [];
            Error          = null;

            String? authority  = null;
            String? path       = null;
            var     fields     = new List<(String Name, String Value)>();

            foreach (var (name, value) in ConnectHeaders)
            {

                if      (name == ":authority") authority = value;
                else if (name == ":path")      path      = value;
                else if (name.StartsWith(':')) continue;
                else if (NotOverHTTP2.Contains(name))
                {
                    // "host" beside ":authority" says nothing new, and the rest is
                    // of one connection of HTTP/1.1 (RFC 9113, Section 8.2.2).
                    if (!name.Equals("host", StringComparison.OrdinalIgnoreCase))
                    {
                        Error = $"The header field '{name}' is of a connection of HTTP/1.1, and does not go over HTTP/2.";
                        return false;
                    }
                }
                else if (!IsAFieldOfOneLine(name, value))
                {
                    Error = $"The header field '{name}' cannot be a line of a request.";
                    return false;
                }
                else
                    fields.Add((name, value));

            }

            if (authority is null || authority.Length == 0 || !IsAFieldOfOneLine("Host", authority) || authority.Contains(' '))
            {
                Error = "The extended CONNECT names no authority.";
                return false;
            }

            if (path is null || !path.StartsWith('/') || !IsAFieldOfOneLine("Path", path) || path.Contains(' '))
            {
                Error = "The extended CONNECT names no path.";
                return false;
            }

            var request = new StringBuilder();

            request.Append("GET ").Append(path).Append(" HTTP/1.1\r\n");
            request.Append("Host: ").Append(authority).Append("\r\n");
            request.Append("Upgrade: websocket\r\n");
            request.Append("Connection: Upgrade\r\n");
            request.Append("Sec-WebSocket-Key: ").Append(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))).Append("\r\n");

            foreach (var (name, value) in fields)
                request.Append(name).Append(": ").Append(value).Append("\r\n");

            request.Append("\r\n");

            RequestBytes = Encoding.UTF8.GetBytes(request.ToString());
            return true;

        }

        #endregion

        #region FieldsOverHTTP2(Headers)

        /// <summary>
        /// The header fields of an HTTP/1.1 handshake that go over HTTP/2, their
        /// names in lower case as HTTP/2 wants them (RFC 9113, Section 8.2).
        /// </summary>
        public static List<(String Name, String Value)> FieldsOverHTTP2(IEnumerable<(String Name, String Value)> Headers)

            => [.. Headers.
                       Where (field => !NotOverHTTP2.Contains(field.Name) && !field.Name.StartsWith(':')).
                       Select(field => (field.Name.ToLowerInvariant(), field.Value))];

        #endregion

        #region (internal) AcceptOf(SecWebSocketKey)

        /// <summary>
        /// The Sec-WebSocket-Accept a server answers the given Sec-WebSocket-Key
        /// with (RFC 6455, Section 4.2.2).
        /// </summary>
        internal static String AcceptOf(String SecWebSocketKey)

            => Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(SecWebSocketKey + AcceptGUID)));

        #endregion

    }

    #endregion


    #region AWebSocketOverHTTPClientStream

    /// <summary>
    /// What a WebSocket client writes its HTTP/1.1 upgrade request to and reads
    /// the answer and its WebSocket frames from, while the WebSocket is opened
    /// by an extended CONNECT: over HTTP/2 (RFC 8441) or over HTTP/3 (RFC 9220).
    /// </summary>
    /// <remarks>
    /// The request is taken apart once its empty line was written and sent as
    /// an extended CONNECT. A 2xx is read back as the "101 Switching Protocols"
    /// the client waits for, with the Sec-WebSocket-Accept of the key it sent
    /// and every header field of the server's answer; any other status as that
    /// status, with its header fields - a challenge, a Retry-After - and
    /// without a body. After that what is written goes into the tunnel, and
    /// what the tunnel delivers is read.
    ///
    /// What is one version's alone is how the CONNECT is sent and what is let
    /// go of in the end: <see cref="ConnectAsync"/> and <see cref="LetGo"/>.
    /// </remarks>
    public abstract class AWebSocketOverHTTPClientStream : Stream
    {

        #region Data

        private readonly MemoryStream          request      = new();
        private readonly TaskCompletionSource  answered     = new (TaskCreationOptions.RunContinuationsAsynchronously);
        private          IHTTP2Tunnel?         tunnel;
        private          Byte[]                pending      = [];
        private          Int32                 pendingAt;
        private          Boolean               requestSent;
        private          Int32                 disposed;

        #endregion

        #region Properties

        public override Boolean  CanRead      => true;
        public override Boolean  CanWrite     => true;
        public override Boolean  CanSeek      => false;
        public override Boolean  CanTimeout   => true;
        public override Int64    Length       => throw new NotSupportedException();

        public override Int64    Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override Int32    ReadTimeout    { get; set; } = Timeout.Infinite;
        public override Int32    WriteTimeout   { get; set; } = Timeout.Infinite;

        #endregion


        #region (protected abstract) ConnectAsync(Authority, Path, Fields, CancellationToken)

        /// <summary>
        /// Send the extended CONNECT with <c>:protocol = websocket</c> and say
        /// what the server answered: its status, its header fields and, where
        /// it accepted with a 2xx, the tunnel.
        /// </summary>
        /// <param name="Authority">The authority of the request: what its Host header field said.</param>
        /// <param name="Path">The path and the query of the request.</param>
        /// <param name="Fields">The header fields of the request that go with an extended CONNECT, their names in lower case.</param>
        /// <param name="CancellationToken">A token to cancel this.</param>
        protected abstract Task<(Int32 Status, IEnumerable<(String Name, String Value)> Headers, IHTTP2Tunnel? Tunnel)>

            ConnectAsync(String                              Authority,
                         String                              Path,
                         List<(String Name, String Value)>   Fields,
                         CancellationToken                   CancellationToken);

        #endregion

        #region (protected abstract) LetGo()

        /// <summary>
        /// Give up the tunnel and what was made for this WebSocket underneath
        /// it. Called once, when the stream is disposed, and must not block.
        /// </summary>
        protected abstract void LetGo();

        #endregion


        #region Writing

        public override async ValueTask WriteAsync(ReadOnlyMemory<Byte>  buffer,
                                                   CancellationToken     cancellationToken = default)
        {

            if (tunnel is not null)
            {
                await tunnel.WriteAsync(buffer.ToArray(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (requestSent)
                // The line breaks a client writes behind its request, and
                // whatever is written to a WebSocket the server refused.
                return;

            request.Write(buffer.Span);

            var end = WebSocketOverHTTP2.EndOfHead(request.GetBuffer(), (Int32) request.Length);
            if (end < 0)
                return;

            requestSent = true;

            try
            {
                await OpenAsync(request.GetBuffer(), end, cancellationToken).ConfigureAwait(false);
                answered.TrySetResult();
            }
            catch (Exception e)
            {
                answered.TrySetException(e);
                throw;
            }

        }

        public override Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Write(Byte[] buffer, Int32 offset, Int32 count)
            => WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override void Flush()
        { }

        public override Task FlushAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        #endregion

        #region (private) OpenAsync(Head, Length, CancellationToken)

        private async Task OpenAsync(Byte[] Head, Int32 Length, CancellationToken CancellationToken)
        {

            WebSocketOverHTTP2.HeadOf(Head, Length, out var requestLine, out var headers);

            // GET /path?query HTTP/1.1
            var parts      = requestLine.Split(' ');
            var path       = parts.Length >= 2 ? parts[1] : "/";
            var authority  = headers.FirstOrDefault(field => field.Name.Equals("Host",              StringComparison.OrdinalIgnoreCase)).Value ?? "";
            var key        = headers.FirstOrDefault(field => field.Name.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase)).Value ?? "";

            var answer     = new StringBuilder();

            var (status, answerHeaders, accepted) = await ConnectAsync(
                                                              authority,
                                                              path,
                                                              WebSocketOverHTTP2.FieldsOverHTTP2(headers),
                                                              CancellationToken
                                                          ).ConfigureAwait(false);

            if (accepted is not null)
            {

                tunnel = accepted;

                answer.Append("HTTP/1.1 101 Switching Protocols\r\n");
                answer.Append("Upgrade: websocket\r\n");
                answer.Append("Connection: Upgrade\r\n");
                answer.Append("Sec-WebSocket-Accept: ").Append(WebSocketOverHTTP2.AcceptOf(key)).Append("\r\n");

            }
            else
                answer.Append("HTTP/1.1 ").Append(status).Append(' ').
                       Append(HTTPStatusCode.TryParseUInt16((UInt16) status, out var statusCode)
                                  ? statusCode.Name
                                  : "Refused").
                       Append("\r\n");

            foreach (var (name, value) in WebSocketOverHTTP2.FieldsOverHTTP2(answerHeaders))
                if (WebSocketOverHTTP2.IsAFieldOfOneLine(name, value))
                    answer.Append(name).Append(": ").Append(value).Append("\r\n");

            answer.Append("\r\n");

            pending    = Encoding.UTF8.GetBytes(answer.ToString());
            pendingAt  = 0;

        }

        #endregion

        #region Reading

        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       buffer,
                                                         CancellationToken  cancellationToken = default)
        {

            if (buffer.Length == 0)
                return 0;

            // Nothing is read before the request was answered.
            await answered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (pendingAt >= pending.Length)
            {

                // Refused, and the answer was read: this is its end.
                if (tunnel is null)
                    return 0;

                var chunk = await tunnel.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (chunk is null)
                    return 0;

                pending    = chunk;
                pendingAt  = 0;

            }

            var take = Math.Min(buffer.Length, pending.Length - pendingAt);
            pending.AsSpan(pendingAt, take).CopyTo(buffer.Span);
            pendingAt += take;

            return take;

        }

        public override Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {

            using var timeout = ReadTimeout > 0
                                    ? new CancellationTokenSource(ReadTimeout)
                                    : new CancellationTokenSource();

            try
            {
                return ReadAsync(buffer.AsMemory(offset, count), timeout.Token).AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw new IOException("Reading from the WebSocket over its extended CONNECT timed out.");
            }

        }

        #endregion

        #region Not supported

        public override Int64 Seek(Int64 offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(Int64 value)
            => throw new NotSupportedException();

        #endregion

        #region Dispose(Disposing)

        protected override void Dispose(Boolean Disposing)
        {

            if (Disposing && Interlocked.Exchange(ref disposed, 1) == 0)
            {

                answered.TrySetException(new ObjectDisposedException(GetType().Name));

                // Nobody may wait for it, and a task that failed unobserved is reported.
                _ = answered.Task.Exception;

                try
                {
                    LetGo();
                }
                catch
                { }

            }

            base.Dispose(Disposing);

        }

        #endregion

    }

    #endregion

    #region WebSocketOverHTTP2ClientStream

    /// <summary>
    /// What a WebSocket client writes its HTTP/1.1 upgrade request to and reads
    /// the answer and its WebSocket frames from, while the WebSocket is opened
    /// by an extended CONNECT on an HTTP/2 connection (RFC 8441): see
    /// <see cref="AWebSocketOverHTTPClientStream"/>.
    /// </summary>
    public sealed class WebSocketOverHTTP2ClientStream(HTTP2ClientConnection  Connection,
                                                       URIScheme              Scheme) : AWebSocketOverHTTPClientStream
    {

        #region Data

        private HTTP2ClientTunnel? tunnel;

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP/2 connection the WebSocket is opened on.
        /// </summary>
        public HTTP2ClientConnection  HTTP2Connection   => Connection;

        /// <summary>
        /// The tunnel of the WebSocket, once the server accepted it.
        /// </summary>
        public HTTP2ClientTunnel?     Tunnel            => tunnel;

        #endregion


        #region (protected override) ConnectAsync(Authority, Path, Fields, CancellationToken)

        protected override async Task<(Int32 Status, IEnumerable<(String Name, String Value)> Headers, IHTTP2Tunnel? Tunnel)>

            ConnectAsync(String                              Authority,
                         String                              Path,
                         List<(String Name, String Value)>   Fields,
                         CancellationToken                   CancellationToken)

        {

            try
            {

                tunnel = await Connection.OpenTunnelAsync(
                                   Authority,
                                   WebSocketOverHTTP2.Protocol,
                                   Scheme,
                                   Path,
                                   Fields,
                                   CancellationToken: CancellationToken
                               ).ConfigureAwait(false);

                return (200, tunnel.ResponseHeaders, tunnel);

            }
            catch (HTTP2TunnelRefusedException refusal)
            {
                return (refusal.Status, refusal.Headers, null);
            }

        }

        #endregion

        #region (protected override) LetGo()

        /// <summary>
        /// Give the tunnel up and close the HTTP/2 connection: it was made for
        /// this WebSocket. The transport underneath stays its owner's.
        /// </summary>
        protected override void LetGo()
        {

            _ = Task.Run(async () => {
                try
                {
                    if (tunnel is not null)
                        await tunnel.DisposeAsync().ConfigureAwait(false);
                    await Connection.CloseAsync().ConfigureAwait(false);
                }
                catch
                { }
            });

        }

        #endregion

    }

    #endregion


    #region WebSocketOverHTTP2ServerStream

    /// <summary>
    /// What a WebSocket server reads the WebSocket frames of a client from and
    /// writes its answer to the upgrade request and its own frames to, while
    /// the WebSocket was opened by an extended CONNECT on an HTTP/2 connection
    /// (RFC 8441).
    /// </summary>
    /// <remarks>
    /// The server is handed the upgrade request the extended CONNECT stands
    /// for (<see cref="WebSocketOverHTTP2.TryUpgradeRequestOf"/>) in front of
    /// this stream, and answers it here as it answers every other. The head of
    /// that answer is not sent: <see cref="Answer"/> says its status and its
    /// header fields, for the HTTP/2 connection to accept the CONNECT with a
    /// 200 where the server wrote a 101, and to refuse it with the server's
    /// status otherwise. <see cref="RunAsync"/> is then what runs on the
    /// tunnel: it lasts until the server closes this stream.
    /// </remarks>
    public sealed class WebSocketOverHTTP2ServerStream : Stream
    {

        #region Data

        private readonly MemoryStream                                                              head      = new();
        private readonly TaskCompletionSource<(UInt16 Status, List<(String Name, String Value)> Headers)>  answer    = new (TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<IHTTP2Tunnel>                                        accepted  = new (TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource                                                      closed    = new (TaskCreationOptions.RunContinuationsAsynchronously);
        private          Byte[]                                                                    pending   = [];
        private          Int32                                                                     pendingAt;
        private          Boolean                                                                   answerRead;
        private          Boolean                                                                   refused;

        #endregion

        #region Properties

        /// <summary>
        /// The status and the header fields the server answered the upgrade
        /// request with. Fails where the server closed the stream without an
        /// answer.
        /// </summary>
        public Task<(UInt16 Status, List<(String Name, String Value)> Headers)> Answer
            => answer.Task;

        public override Boolean  CanRead      => true;
        public override Boolean  CanWrite     => true;
        public override Boolean  CanSeek      => false;
        public override Boolean  CanTimeout   => true;
        public override Int64    Length       => throw new NotSupportedException();

        public override Int64    Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override Int32    ReadTimeout    { get; set; } = Timeout.Infinite;
        public override Int32    WriteTimeout   { get; set; } = Timeout.Infinite;

        #endregion


        #region RunAsync(Tunnel, CancellationToken)

        /// <summary>
        /// Run the WebSocket on the tunnel of its accepted CONNECT: until the
        /// server closes this stream, or the token is cancelled.
        /// </summary>
        public async Task RunAsync(IHTTP2Tunnel Tunnel, CancellationToken CancellationToken)
        {

            accepted.TrySetResult(Tunnel);

            using var registration = CancellationToken.Register(() => closed.TrySetResult());

            await closed.Task.ConfigureAwait(false);

        }

        #endregion

        #region Writing

        public override async ValueTask WriteAsync(ReadOnlyMemory<Byte>  buffer,
                                                   CancellationToken     cancellationToken = default)
        {

            if (refused)
                return;

            if (!answerRead)
            {

                head.Write(buffer.Span);

                var end = WebSocketOverHTTP2.EndOfHead(head.GetBuffer(), (Int32) head.Length);
                if (end < 0)
                    return;

                answerRead = true;

                WebSocketOverHTTP2.HeadOf(head.GetBuffer(), end, out var statusLine, out var headers);

                // HTTP/1.1 101 Switching Protocols
                var parts = statusLine.Split(' ');

                if (parts.Length < 2 || !UInt16.TryParse(parts[1], out var status))
                {
                    refused = true;
                    answer.TrySetException(new IOException($"The WebSocket server answered the upgrade request with '{statusLine}'."));
                    return;
                }

                refused = status != 101;
                answer.TrySetResult((status, headers));

                // The body of a refusal is not sent: over HTTP/2 a CONNECT that
                // is refused is answered with its status and its header fields.
                if (refused || end == head.Length)
                    return;

                // Frames the server wrote in one piece with its answer.
                buffer = head.GetBuffer().AsMemory(end, (Int32) head.Length - end).ToArray();

            }

            var tunnel = await accepted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            await tunnel.WriteAsync(buffer.ToArray(), cancellationToken).ConfigureAwait(false);

        }

        public override Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Write(Byte[] buffer, Int32 offset, Int32 count)
            => WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override void Flush()
        { }

        public override Task FlushAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        #endregion

        #region Reading

        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       buffer,
                                                         CancellationToken  cancellationToken = default)
        {

            if (buffer.Length == 0)
                return 0;

            if (pendingAt >= pending.Length)
            {

                IHTTP2Tunnel tunnel;

                try
                {
                    tunnel = await accepted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    // Closed before there was a tunnel: its end.
                    return 0;
                }

                var chunk = await tunnel.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (chunk is null)
                    return 0;

                pending    = chunk;
                pendingAt  = 0;

            }

            var take = Math.Min(buffer.Length, pending.Length - pendingAt);
            pending.AsSpan(pendingAt, take).CopyTo(buffer.Span);
            pendingAt += take;

            return take;

        }

        public override Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {

            using var timeout = ReadTimeout > 0
                                    ? new CancellationTokenSource(ReadTimeout)
                                    : new CancellationTokenSource();

            try
            {
                return ReadAsync(buffer.AsMemory(offset, count), timeout.Token).AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw new IOException("Reading from the WebSocket over HTTP/2 timed out.");
            }

        }

        #endregion

        #region Not supported

        public override Int64 Seek(Int64 offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(Int64 value)
            => throw new NotSupportedException();

        #endregion

        #region Dispose(Disposing)

        /// <summary>
        /// The server is done with the WebSocket: its tunnel ends, and a CONNECT
        /// that was not answered yet is not accepted.
        /// </summary>
        protected override void Dispose(Boolean Disposing)
        {

            if (Disposing)
            {
                answer.  TrySetException(new IOException("The WebSocket server closed the connection without answering the upgrade request."));
                accepted.TrySetException(new ObjectDisposedException(nameof(WebSocketOverHTTP2ServerStream)));
                closed.  TrySetResult();

                // Nobody may wait for them, and a task that failed unobserved is reported.
                _ = answer.  Task.Exception;
                _ = accepted.Task.Exception;
            }

            base.Dispose(Disposing);

        }

        #endregion

    }

    #endregion

}
