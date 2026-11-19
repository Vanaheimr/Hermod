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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Where a redirect response points, and what the follow-up request should look
    /// like once the RFC 9110, Section 15.4 rewriting rules have been applied.
    /// </summary>
    /// <param name="Scheme">Scheme of the target.</param>
    /// <param name="Authority">Authority of the target, written as it would appear in <c>:authority</c> (a default port omitted).</param>
    /// <param name="Path">Path and query of the target; any fragment is dropped, as fragments never travel on the wire.</param>
    /// <param name="Method">Method for the follow-up request — possibly rewritten to GET.</param>
    /// <param name="KeepBody">Whether the original request body is replayed (307/308) or dropped (301/302/303).</param>
    /// <param name="SameOrigin">Whether the target is the same scheme + authority as the request that was redirected.</param>
    public sealed record HTTPRedirectTarget(URIScheme   Scheme,
                                            String      Authority,
                                            String      Path,
                                            HTTPMethod  Method,
                                            Boolean     KeepBody,
                                            Boolean     SameOrigin)
    {

        /// <summary>
        /// The target as an absolute URI string, for logging or handing onwards.
        /// </summary>
        public override String ToString()
            => $"{Scheme}://{Authority}{Path}";

    }

}
