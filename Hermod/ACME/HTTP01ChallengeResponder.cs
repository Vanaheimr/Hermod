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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// Answers http-01 challenges (RFC 8555, section 8.3) from a Hermod HTTP
    /// server: GET /.well-known/acme-challenge/{token} gives the key
    /// authorization of the token while a challenge for it is open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Port 80, and nothing else.</b> A certificate authority asks over
    /// plain HTTP on port 80 of the name to be proven, from the internet. A
    /// server that cannot be reached there - a time server in a local network,
    /// say - proves its names with dns-01 instead
    /// (<see cref="DNS01ChallengeResponder"/>). Redirects to HTTPS are
    /// followed by Let's Encrypt, so a server that sends everything on port 80
    /// to HTTPS works as long as the HTTPS side answers the path as well.
    /// </para>
    /// <para>
    /// Not for wildcards: a certificate for "*.example.org" is proven through
    /// DNS only.
    /// </para>
    /// </remarks>
    public sealed class HTTP01ChallengeResponder : IACMEChallengeResponder
    {

        #region Data

        /// <summary>
        /// The path under which every token is asked for.
        /// </summary>
        public const String  PathPrefix  = "/.well-known/acme-challenge/";

        private readonly ConcurrentDictionary<String, String> keyAuthorizations = new (StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <inheritdoc/>
        public String  ChallengeType
            => "http-01";

        /// <summary>
        /// How many challenges are open.
        /// </summary>
        public Int32   Open
            => keyAuthorizations.Count;

        #endregion


        #region CanAnswer(Identifier, Wildcard)

        /// <inheritdoc/>
        public Boolean CanAnswer(ACMEIdentifier  Identifier,
                                 Boolean         Wildcard)

            => !Wildcard;

        #endregion

        #region PrepareAsync(Challenge, CancellationToken)

        /// <inheritdoc/>
        public Task PrepareAsync(ACMEChallengeTask  Challenge,
                                 CancellationToken  CancellationToken)
        {
            keyAuthorizations[Challenge.Token] = Challenge.KeyAuthorization;
            return Task.CompletedTask;
        }

        #endregion

        #region CleanUpAsync(Challenge, CancellationToken)

        /// <inheritdoc/>
        public Task CleanUpAsync(ACMEChallengeTask  Challenge,
                                 CancellationToken  CancellationToken)
        {
            keyAuthorizations.TryRemove(Challenge.Token, out _);
            return Task.CompletedTask;
        }

        #endregion


        #region TryGetKeyAuthorization(Token, out KeyAuthorization)

        /// <summary>
        /// The answer to the token, while a challenge for it is open - for a web
        /// server other than Hermod's to give.
        /// </summary>
        public Boolean TryGetKeyAuthorization(String                               Token,
                                              [NotNullWhen(true)] out String?      KeyAuthorization)

            => keyAuthorizations.TryGetValue(Token, out KeyAuthorization);

        #endregion

        #region RegisterAt(HTTPServer, Hostname = null)

        /// <summary>
        /// Have the given HTTP server answer the challenges.
        /// </summary>
        /// <param name="HTTPServer">The server on port 80.</param>
        /// <param name="Hostname">The host name it answers for; any by default.</param>
        public void RegisterAt(HTTPServer     HTTPServer,
                               HTTPHostname?  Hostname   = null)
        {

            HTTPServer.AddMethodCallback(
                Hostname ?? HTTPHostname.Any,
                HTTPMethod.GET,
                HTTPPath.Parse(PathPrefix + "{token}"),
                HTTPDelegate: request => Task.FromResult(Answer(request))
            );

        }

        #endregion

        #region Answer(Request)

        /// <summary>
        /// The response to a request for a token: its key authorization, or 404
        /// when no challenge for it is open.
        /// </summary>
        public HTTPResponse Answer(HTTPRequest Request)
        {

            var token = Request.ParsedURLParametersX.TryGetValue("token", out var value)
                            ? value
                            : Request.Path.ToString().Split('/').LastOrDefault() ?? "";

            return TryGetKeyAuthorization(token, out var keyAuthorization)

                       ? new HTTPResponse.Builder(Request) {
                             HTTPStatusCode  = HTTPStatusCode.OK,
                             ContentType     = HTTPContentType.Application.OCTETSTREAM,
                             Content         = Encoding.ASCII.GetBytes(keyAuthorization),
                             CacheControl    = "no-store"
                         }.AsImmutable

                       : new HTTPResponse.Builder(Request) {
                             HTTPStatusCode  = HTTPStatusCode.NotFound,
                             CacheControl    = "no-store"
                         }.AsImmutable;

        }

        #endregion

    }

}
