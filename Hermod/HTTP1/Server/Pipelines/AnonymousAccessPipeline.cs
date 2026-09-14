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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Marks the requests that may be served without credentials as the anonymous user.
    /// </summary>
    /// <remarks>
    /// This is what HTTPServer.AddAuth(...) used to do before the old HTTP implementation was
    /// deprecated: the auth chain never rejected a request, it only decided which IUser to hang
    /// on it. Pipelines are that extension point now — they run on every request, before the URL
    /// is matched to a handler, and a pipeline that returns no response lets the request carry on.
    ///
    /// Like the old chain, this only assigns a user. Nothing here refuses a request, and the
    /// per-handler URLAuthentication that would is not enforced yet.
    ///
    /// Put this after <see cref="HTTPAuthPipeline"/>, which identifies the request from a cookie,
    /// an API key or HTTP Basic Auth — those know more about a request than a path list does, and
    /// this pipeline deliberately does not overrule them.
    /// </remarks>
    /// <param name="IsAnonymous">Whether the given request may be served anonymously.</param>
    public class AnonymousAccessPipeline(Func<HTTPRequest, Boolean> IsAnonymous) : AHTTPPipeline
    {

        #region Properties

        /// <summary>
        /// Whether the given request may be served anonymously.
        /// </summary>
        public Func<HTTPRequest, Boolean> IsAnonymous { get; } = IsAnonymous;

        #endregion


        #region (override) ProcessHTTPRequest(Request, CancellationToken = default)

        public override Task<(HTTPRequest, HTTPResponse?)>

            ProcessHTTPRequest(HTTPRequest        Request,
                               CancellationToken  CancellationToken   = default)

        {

            if (Request.User is null && IsAnonymous(Request))
                Request.User = HTTPExtAPI.Anonymous;

            return Task.FromResult<(HTTPRequest, HTTPResponse?)>((Request, null));

        }

        #endregion

    }

}
