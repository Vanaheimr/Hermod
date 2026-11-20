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
    /// Processes a QUERY request (RFC 10008): the request content (plus its
    /// declared <paramref name="ContentType"/>) is the query; the returned
    /// <see cref="HTTPResource"/> is the *result* of running it, or null for 404.
    /// QUERY is a safe, idempotent, cacheable method that carries its query in the
    /// request body rather than the URL — so unlike <see cref="HTTPResourceHandler"/>
    /// this one also receives the body. <see cref="HTTPResourceSemantics.Answer"/> then runs
    /// the result through the same conditional-request / negotiation / representation
    /// pipeline as a GET, so a QUERY result gets ETags, 304 revalidation, Accept
    /// negotiation and (optionally) a <c>Content-Location</c> for free.
    /// </summary>
    public delegate Task<HTTPResource?> HTTPQueryHandler(
        string                             Path,
        List<(string Name, string Value)>  RequestHeaders,
        byte[]?                            QueryContent,
        string?                            ContentType,
        CancellationToken                  CancellationToken
    );

}
