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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Answers CORS preflights, and nothing else.
    ///
    /// Why this is a pipeline and not server behaviour: in this stack the
    /// resource decides what its own semantics mean, and that rule presupposes
    /// the request reaches the resource. A preflight never does. It is an
    /// OPTIONS request for a method the route has no handler for, so routing
    /// answers it before any handler is consulted — which means the handler is
    /// not declining to deal with it, it is never offered the chance. A
    /// component installed ahead of routing is the only place the application
    /// can take that decision back.
    ///
    /// Opt-in, like <see cref="HTTPAuthPipeline"/>: a server that does not add
    /// one behaves exactly as before. Everything that is not a preflight is
    /// passed through untouched — including an ordinary OPTIONS, which the
    /// router now answers itself (RFC 9110 §9.3.7).
    ///
    /// What it deliberately does NOT do is add Access-Control-Allow-Origin to
    /// ordinary responses. That is per-resource, the handler already sets it,
    /// and a pipeline that rewrote every response would be making the policy
    /// decision this stack leaves to the resource.
    /// </summary>
    /// <param name="Policy">What the application permits.</param>
    public class HTTPCORSPipeline(CORSPolicy Policy) : AHTTPPipeline()
    {

        #region Properties

        public CORSPolicy Policy { get; } = Policy;

        #endregion


        #region (override) ProcessHTTPRequest(Request, CancellationToken = default)

        public override Task<(HTTPRequest, HTTPResponse?)>

            ProcessHTTPRequest(HTTPRequest        Request,
                               CancellationToken  CancellationToken   = default)

        {

            // Not a preflight: carry on. The predicate is WHATWG Fetch's own
            // three conditions and carries no policy, so an OPTIONS request that
            // merely asks about a resource still reaches the router.
            if (!Request.IsCORSPreflight)
                return Task.FromResult<(HTTPRequest, HTTPResponse?)>((Request, null));

            var origin        = Request.Origin!;
            var allowOrigin   = Policy.AllowOrigin(origin);

            // A refused preflight is answered 403 rather than left to fall
            // through to the router. Falling through would produce a 204 from
            // the automatic OPTIONS answer — a success the browser would then
            // reject for missing Access-Control-Allow-Origin, which reads in a
            // developer console as "the server is broken" rather than "the
            // server said no".
            if (allowOrigin is null)
                return Task.FromResult<(HTTPRequest, HTTPResponse?)>((
                           Request,
                           Refuse(Request, $"The origin '{origin}' is not permitted.")
                       ));

            var requestedMethod = HTTPMethod.TryParse(Request.AccessControlRequestMethod!);

            if (requestedMethod is null || !Policy.Allows(requestedMethod))
                return Task.FromResult<(HTTPRequest, HTTPResponse?)>((
                           Request,
                           Refuse(Request, $"The method '{Request.AccessControlRequestMethod}' is not permitted for '{origin}'.")
                       ));

            if (!Policy.AllowsHeaders(Request.AccessControlRequestHeaders))
                return Task.FromResult<(HTTPRequest, HTTPResponse?)>((
                           Request,
                           Refuse(Request, $"Not every requested header is permitted for '{origin}'.")
                       ));

            var response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode               = HTTPStatusCode.NoContent,
                               Date                         = Timestamp.Now,
                               AccessControlAllowOrigin     = allowOrigin,
                               AccessControlAllowMethods    = Policy.AllowedMethods,
                               Connection                   = ConnectionType.KeepAlive
                           };

            if (Policy.AllowedHeaders.Count > 0)
                response.AccessControlAllowHeaders = [.. Policy.AllowedHeaders];

            if (Policy.MaxAge.HasValue)
                response.AccessControlMaxAge = (UInt64) Policy.MaxAge.Value.TotalSeconds;

            if (Policy.AllowCredentials)
                response.Set("Access-Control-Allow-Credentials", "true");

            // The answer depends on the Origin that asked, so a cache that
            // ignored it would hand one origin's permission to another.
            response.Vary = "Origin";

            return Task.FromResult<(HTTPRequest, HTTPResponse?)>((Request, response.AsImmutable));

        }

        #endregion

        #region (private static) Refuse(Request, Description)

        /// <summary>
        /// A refusal carries no Access-Control-Allow-* at all. The browser will
        /// block the call either way; what this buys is that the reason is
        /// legible to whoever reads the response rather than inferred from an
        /// absence.
        /// </summary>
        private static HTTPResponse Refuse(HTTPRequest Request, String Description)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = HTTPStatusCode.Forbidden,
                   Date            = Timestamp.Now,
                   ContentType     = HTTPContentType.Text.PLAIN,
                   Content         = Description.ToUTF8Bytes(),
                   Vary            = "Origin",
                   Connection      = ConnectionType.KeepAlive
               }.AsImmutable;

        #endregion

    }

}
