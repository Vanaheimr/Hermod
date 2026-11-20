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
    /// Produces every available representation of Path — the variants proactive
    /// content negotiation (RFC 9110, Section 12) will pick among — or an empty
    /// list if the resource doesn't exist (404). A one-element list is the
    /// ordinary "no negotiation" case; <see cref="HTTPResourceHandler"/> is just
    /// that, adapted. When several variants tie on client preference, the first
    /// in the list wins, so an app can express its own preference ordering.
    /// </summary>
    public delegate Task<IReadOnlyList<HTTPResource>> HTTPVariantHandler(
        string                             Path,
        List<(string Name, string Value)>  RequestHeaders,
        CancellationToken                  CancellationToken
    );

}
