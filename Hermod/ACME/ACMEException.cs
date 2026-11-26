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

using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// What an ACME server says went wrong: a problem document (RFC 7807) with
    /// one of the error types of RFC 8555, section 6.7.
    /// </summary>
    /// <param name="Type">The error type, as "urn:ietf:params:acme:error:unauthorized".</param>
    /// <param name="Detail">The server's words for it.</param>
    /// <param name="Status">The HTTP status code it came with.</param>
    /// <param name="Identifier">The identifier it is about, for a subproblem.</param>
    /// <param name="Subproblems">The problems of single identifiers of an order.</param>
    public sealed record ACMEProblem(String                       Type,
                                     String?                      Detail,
                                     Int32?                       Status,
                                     ACMEIdentifier?              Identifier,
                                     IReadOnlyList<ACMEProblem>   Subproblems)
    {

        /// <summary>
        /// The prefix of every error type of RFC 8555.
        /// </summary>
        public const String ErrorNamespace = "urn:ietf:params:acme:error:";

        /// <summary>
        /// The error type without <see cref="ErrorNamespace"/>: "badNonce",
        /// "unauthorized", "rateLimited" ...
        /// </summary>
        public String ShortType
            => Type.StartsWith(ErrorNamespace, StringComparison.Ordinal)
                   ? Type[ErrorNamespace.Length..]
                   : Type;

        internal static ACMEProblem? Parse(JObject? JSON)

            => JSON is null
                   ? null
                   : new ACMEProblem(
                         JSON["type"]?.Value<String>() ?? "about:blank",
                         JSON["detail"]?.Value<String>(),
                         JSON["status"]?.Type == JTokenType.Integer ? JSON["status"]!.Value<Int32>() : null,
                         JSON["identifier"] is JObject identifier ? ACMEIdentifier.Parse(identifier) : null,
                         (JSON["subproblems"] as JArray)?.OfType<JObject>().Select(Parse).OfType<ACMEProblem>().ToArray() ?? []
                     );

        /// <summary>
        /// The problem in one line: its type, what the server said, and what it
        /// said about each identifier.
        /// </summary>
        public override String ToString()

            => String.Concat(
                   ShortType,
                   Detail is not null ? $": {Detail}" : "",
                   String.Concat(Subproblems.Select(subproblem => $"; {subproblem.Identifier?.Value ?? "?"}: {subproblem.ShortType}{(subproblem.Detail is not null ? $" - {subproblem.Detail}" : "")}"))
               );

    }


    /// <summary>
    /// An ACME request that did not do what it was for.
    /// </summary>
    public sealed class ACMEException : Exception
    {

        /// <summary>
        /// The problem the server named, if it named one.
        /// </summary>
        public ACMEProblem?  Problem       { get; }

        /// <summary>
        /// When the server would be asked again, where it said - after a rate
        /// limit, say.
        /// </summary>
        public TimeSpan?     RetryAfter    { get; }

        /// <summary>
        /// A failure the server did not put into a problem document.
        /// </summary>
        public ACMEException(String      Message,
                             Exception?  InnerException  = null)

            : base(Message, InnerException)

        { }

        /// <summary>
        /// A failure the server named.
        /// </summary>
        public ACMEException(String       Message,
                             ACMEProblem  Problem,
                             TimeSpan?    RetryAfter   = null)

            : base($"{Message}: {Problem}")

        {
            this.Problem     = Problem;
            this.RetryAfter  = RetryAfter;
        }

    }

}
