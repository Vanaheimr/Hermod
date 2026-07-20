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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// Convenience helpers for putting an <see cref="HTTPAuthenticator"/> in front
    /// of application logic.
    /// </summary>
    public static class HTTPAuthentication
    {

        /// <summary>
        /// Wrap an authenticated handler with the RFC 9110 challenge/response flow:
        /// an unauthenticated request gets <c>401 Unauthorized</c> + the
        /// <c>WWW-Authenticate</c> challenge(s); an authenticated one is passed
        /// through to Inner with its identity. The result is an ordinary
        /// <see cref="HTTP2RequestHandler"/>, so it drops into the server (or in
        /// front of an <see cref="HTTPSemantics"/>-wrapped handler) unchanged.
        /// </summary>
        public static HTTP2RequestHandler RequireAuthentication(
            HTTPAuthenticator               Authenticator,
            HTTPAuthenticatedRequestHandler Inner)
            => async (streamId, requestHeaders, requestBody, cancellationToken) =>
            {

                var identity = await Authenticator.AuthenticateAsync(requestHeaders, cancellationToken);

                if (identity is null)
                {
                    var body    = "401 Unauthorized"u8.ToArray();
                    var headers = new List<(string Name, string Value)> { (":status", "401") };
                    headers.AddRange(Authenticator.BuildChallengeHeaders());
                    headers.Add(("content-type",   "text/plain; charset=utf-8"));
                    headers.Add(("content-length", body.Length.ToString()));
                    return (headers, body);
                }

                return await Inner(identity, streamId, requestHeaders, requestBody, cancellationToken);

            };

    }

}
