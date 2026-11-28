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
using System.Net.Security;
using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A tunnel through a forward proxy (RFC 9110, Section 9.3.6): the stream
    /// to the server, and what the TLS to the proxy came to, where there was TLS.
    /// </summary>
    /// <param name="Stream">The stream to the server: bytes written to it reach the server, and the proxy passes them on unread.</param>
    /// <param name="ProxyTLSSession">The TLS session with the proxy, or null where the proxy is reached without TLS.</param>
    public sealed class HTTPProxyTunnel(Stream       Stream,
                                        TLSSession?  ProxyTLSSession)
    {

        #region Data

        /// <summary>
        /// The most bytes the proxy's answer to the CONNECT may take, up to the
        /// end of its header section.
        /// </summary>
        public const Int32 MaxResponseHeaderSize = 16 * 1024;

        #endregion

        #region Properties

        /// <summary>
        /// The stream to the server: bytes written to it reach the server, and
        /// the proxy passes them on unread.
        /// </summary>
        public Stream       Stream             { get; } = Stream;

        /// <summary>
        /// The TLS session with the proxy, or null where the proxy is reached without TLS.
        /// </summary>
        public TLSSession?  ProxyTLSSession    { get; } = ProxyTLSSession;

        #endregion


        #region OpenAsync (TCPStream, Proxy, TargetHost, TargetPort, Timeout, CancellationToken = default)

        /// <summary>
        /// Ask the proxy at the other end of the given TCP stream for a tunnel
        /// to the given host and port. The proxy is reached over TLS where it is
        /// an "https" proxy, and the CONNECT carries Proxy-Authorization where
        /// the proxy was given an authentication or a one-time password.
        /// </summary>
        /// <param name="TCPStream">The TCP connection to the proxy. It is the tunnel's, and closed with it.</param>
        /// <param name="Proxy">The proxy.</param>
        /// <param name="TargetHost">The host of the server: a name, an IPv4 address, or an IPv6 address in brackets.</param>
        /// <param name="TargetPort">The TCP port of the server.</param>
        /// <param name="Timeout">How long the TLS to the proxy and its answer to the CONNECT may take together.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="HTTPProxyException">The proxy could not be reached as configured, or did not open the tunnel.</exception>
        public static async Task<HTTPProxyTunnel> OpenAsync(Stream             TCPStream,
                                                            HTTPProxy          Proxy,
                                                            String             TargetHost,
                                                            IPPort             TargetPort,
                                                            TimeSpan           Timeout,
                                                            CancellationToken  CancellationToken   = default)
        {

            if (Proxy.WhyItCannotBeUsed() is String why)
                throw new HTTPProxyException(why);

            if (TargetHost.Length == 0 || TargetHost.Any(character => character <= ' ' || character == '/' || character == '@'))
                throw new HTTPProxyException($"'{TargetHost}' is no host a tunnel can be asked for.");

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            deadline.CancelAfter(Timeout);

            var stream      = TCPStream;
            var tlsSession  = (TLSSession?) null;

            try
            {

                if (Proxy.IsTLS)
                {
                    tlsSession = await ToTheProxyOverTLS(TCPStream, Proxy, deadline.Token).ConfigureAwait(false);
                    stream     = tlsSession.Stream;
                }

                var authority  = $"{TargetHost}:{TargetPort.ToUInt16()}";
                var request    = new StringBuilder();

                request.Append($"CONNECT {authority} HTTP/1.1\r\n");
                request.Append($"Host: {authority}\r\n");

                if (ProxyAuthorizationOf(Proxy, tlsSession) is String proxyAuthorization)
                    request.Append($"Proxy-Authorization: {proxyAuthorization}\r\n");

                request.Append("\r\n");

                await stream.WriteAsync(Encoding.ASCII.GetBytes(request.ToString()), deadline.Token).ConfigureAwait(false);
                await stream.FlushAsync(deadline.Token).ConfigureAwait(false);

                var (statusCode, reason, proxyAuthenticate) = await ReadResponseAsync(stream, deadline.Token).ConfigureAwait(false);

                if (statusCode is < 200 or > 299)
                {

                    HTTPStatusCode.TryParseUInt16(statusCode, out var httpStatusCode);

                    throw new HTTPProxyException(
                              statusCode == 407
                                  ? $"The proxy {Proxy} wants to be told who is connecting through it (407 {reason})" +
                                    (proxyAuthenticate.Count > 0 ? $", and takes: {proxyAuthenticate.AggregateWith("; ")}." : ".")
                                  : $"The proxy {Proxy} did not open a tunnel to {authority}: {statusCode} {reason}.",
                              httpStatusCode,
                              proxyAuthenticate
                          );

                }

                return new HTTPProxyTunnel(stream, tlsSession);

            }
            catch (Exception e)
            {

                try { await stream.DisposeAsync().ConfigureAwait(false); } catch { }

                if (e is HTTPProxyException)
                    throw;

                if (e is OperationCanceledException && deadline.IsCancellationRequested && !CancellationToken.IsCancellationRequested)
                    throw new HTTPProxyException($"The proxy {Proxy} did not open a tunnel to {TargetHost}:{TargetPort.ToUInt16()} within {Timeout.TotalSeconds:0.#} seconds.", null, null, e);

                if (e is OperationCanceledException)
                    throw;

                throw new HTTPProxyException($"No tunnel through the proxy {Proxy}: {e.Message}", null, null, e);

            }

        }

        #endregion


        #region (private static) ToTheProxyOverTLS   (TCPStream, Proxy, CancellationToken)

        /// <summary>
        /// The TLS to the proxy: TLS 1.3 by the stack that checks key pins and
        /// exports keying material, where the proxy's key is pinned, and by
        /// SslStream with the proxy's validator otherwise.
        /// </summary>
        private static async Task<TLSSession> ToTheProxyOverTLS(Stream             TCPStream,
                                                                HTTPProxy          Proxy,
                                                                CancellationToken  CancellationToken)
        {

            // A name is indicated, an address is not (RFC 6066, Section 3).
            var serverName = Proxy.URL.Host.IsIPv4 || Proxy.URL.Host.IsIPv6
                                 ? null
                                 : Proxy.Host;

            if (Proxy.ServerKeyPins is KeyPinPolicy serverKeyPins)
                return await BouncyCastleTLS.AuthenticateAsClientAsync(
                                 TCPStream,
                                 new RawPublicKeyTLSClientParameters {
                                     ServerKeyPins            = serverKeyPins,
                                     OnServerKeyPinValidated  = Proxy.OnServerKeyPinValidated,
                                     ServerName               = serverName,
                                     ApplicationProtocols     = [ "http/1.1" ]
                                 },
                                 LeaveInnerStreamOpen:  false,
                                 CancellationToken:     CancellationToken
                             ).ConfigureAwait(false);

            var sslStream = new SslStream(TCPStream, leaveInnerStreamOpen: false);

            await sslStream.AuthenticateAsClientAsync(
                      new SslClientAuthenticationOptions {
                          TargetHost                           = Proxy.Host.Trim('[', ']'),
                          ApplicationProtocols                 = [ SslApplicationProtocol.Http11 ],
                          EnabledSslProtocols                  = SslProtocols.Tls13,
                          EncryptionPolicy                     = EncryptionPolicy.RequireEncryption,
                          RemoteCertificateValidationCallback  = Proxy.RemoteCertificateValidator
                      },
                      CancellationToken
                  ).ConfigureAwait(false);

            return TLSSession.Of(sslStream);

        }

        #endregion

        #region (private static) ProxyAuthorizationOf(Proxy, TLSSession)

        /// <summary>
        /// What goes in Proxy-Authorization: a one-time password of this moment
        /// under the TOTP login, bound to the TLS to the proxy where its
        /// configuration says so; the HTTP authentication otherwise; or nothing.
        /// </summary>
        private static String? ProxyAuthorizationOf(HTTPProxy    Proxy,
                                                    TLSSession?  TLSSession)
        {

            if (Proxy.TOTPConfig is not TOTPConfig totpConfig || Proxy.TOTPLogin is not String totpLogin)
                return Proxy.Authentication?.HTTPText;

            Byte[]? material = null;

            if (totpConfig.TLSChannelBinding == true)
            {

                // WhyItCannotBeUsed has seen to it that there is TLS whose key
                // is pinned; a session that exports nothing is still not let
                // through with a weaker password.
                if (TLSSession is not { CanExportKeyingMaterial: true })
                    throw new HTTPProxyException("The TOTP configuration of the proxy asks for TLS channel binding, and the TLS to the proxy exports no keying material to bind one with.");

                material = TLSSession.ExportKeyingMaterial(
                               TOTPGenerator.TLSExporterLabel,
                               [],
                               TOTPGenerator.TLSExporterLength
                           );

            }

            var totp = TOTPGenerator.GenerateTOTP(
                           totpConfig.SharedSecret,
                           totpConfig.ValidityTime,
                           totpConfig.Length,
                           totpConfig.Alphabet,
                           null,
                           material,
                           totpConfig.HashAlgorithm
                       ).Current;

            return HTTPTOTPAuthentication.Create(totpLogin.Trim(), totp).HTTPText;

        }

        #endregion

        #region (private static) ReadResponseAsync   (Stream, CancellationToken)

        /// <summary>
        /// The proxy's answer, up to the end of its header section: its status,
        /// the reason, and the challenges of its Proxy-Authenticate fields.
        /// Read one byte at a time, as the server's first bytes may follow
        /// right after it.
        /// </summary>
        private static async Task<(UInt16 StatusCode, String Reason, IReadOnlyList<String> ProxyAuthenticate)>

            ReadResponseAsync(Stream             Stream,
                              CancellationToken  CancellationToken)

        {

            var buffer  = new Byte[MaxResponseHeaderSize];
            var length  = 0;

            while (true)
            {

                if (length == buffer.Length)
                    throw new HTTPProxyException($"The proxy's answer to the CONNECT is longer than {MaxResponseHeaderSize} bytes up to the end of its header section.");

                var read = await Stream.ReadAsync(buffer.AsMemory(length, 1), CancellationToken).ConfigureAwait(false);

                if (read == 0)
                    throw new HTTPProxyException("The proxy closed the connection before it had answered the CONNECT.");

                length++;

                if (length >= 4                &&
                    buffer[length - 4] == '\r' &&
                    buffer[length - 3] == '\n' &&
                    buffer[length - 2] == '\r' &&
                    buffer[length - 1] == '\n')
                    break;

            }

            var lines       = Encoding.Latin1.GetString(buffer, 0, length - 4).Split("\r\n");
            var statusLine  = lines[0].Split(' ', 3);

            if (statusLine.Length < 2                                ||
                !statusLine[0].StartsWith("HTTP/1.", StringComparison.Ordinal) ||
                statusLine[1].Length != 3                            ||
                !UInt16.TryParse(statusLine[1], out var statusCode))
                throw new HTTPProxyException($"The proxy's answer to the CONNECT does not begin with a status line: '{lines[0]}'.");

            var proxyAuthenticate = lines.Skip(1).
                                          Select (line  => line.Split(':', 2)).
                                          Where  (field => field.Length == 2 && field[0].Trim().Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase)).
                                          Select (field => field[1].Trim()).
                                          ToArray();

            return (statusCode,
                    statusLine.Length > 2 ? statusLine[2] : "",
                    proxyAuthenticate);

        }

        #endregion

    }


    /// <summary>
    /// A tunnel through a forward proxy could not be opened: the proxy was not
    /// reached as configured, refused the CONNECT, or answered something else.
    /// </summary>
    /// <param name="Message">What went wrong.</param>
    /// <param name="HTTPStatusCode">The status the proxy answered with, where it answered.</param>
    /// <param name="ProxyAuthenticate">The challenges of the proxy's Proxy-Authenticate fields, where it sent any.</param>
    /// <param name="InnerException">What it went wrong with.</param>
    public sealed class HTTPProxyException(String                  Message,
                                           HTTPStatusCode?         HTTPStatusCode      = null,
                                           IReadOnlyList<String>?  ProxyAuthenticate   = null,
                                           Exception?              InnerException      = null) : IOException(Message, InnerException)
    {

        /// <summary>
        /// The status the proxy answered with: 407 where it was not told who is
        /// connecting, or not believed; null where it did not answer.
        /// </summary>
        public HTTPStatusCode?        HTTPStatusCode       { get; } = HTTPStatusCode;

        /// <summary>
        /// The challenges of the proxy's Proxy-Authenticate fields.
        /// </summary>
        public IReadOnlyList<String>  ProxyAuthenticate    { get; } = ProxyAuthenticate ?? [];

    }

}
