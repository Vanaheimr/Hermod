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
    /// The head of a response — its <c>:status</c> and header fields — surfaced by a
    /// streaming exchange (<see cref="HTTP2ClientStream"/>) as soon as the response
    /// HEADERS arrive, before (and independently of) its body.
    /// </summary>
    public sealed record HTTP2ResponseHead(int Status, List<(string Name, string Value)> Headers)
    {
        public string? HeaderValue(string Name)
            => Headers.FirstOrDefault(h => h.Name == Name).Value;
    }

}
