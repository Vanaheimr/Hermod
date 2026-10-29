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

    using System.Globalization;
    using System.Diagnostics.CodeAnalysis;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using org.GraphDefined.Vanaheimr.Illias;


    /// <summary>
    /// Problem details for HTTP APIs (RFC 9457): a JSON object, served as
    /// <c>application/problem+json</c>, that says more about an error than its
    /// status code can.
    ///
    /// Section 3.1 defines five members, all optional:
    ///
    ///   * <c>type</c>, a URI reference naming the problem type. Absent, it is
    ///     <c>about:blank</c>: no semantics beyond the status code (Section 4.2.1);
    ///   * <c>status</c>, the status code, advisory only. A generator MUST send the
    ///     same code in the response itself (Section 3.1.2);
    ///   * <c>title</c>, a short summary of the problem type;
    ///   * <c>detail</c>, an explanation of this occurrence;
    ///   * <c>instance</c>, a URI reference naming this occurrence.
    ///
    /// Everything else is an extension member (Section 3.2), which a consumer that
    /// does not know it MUST ignore. Here extensions are kept in
    /// <see cref="Extensions"/>, so a consumer that does know them can read them.
    ///
    /// JSON only. The XML format of Appendix B, <c>application/problem+xml</c>, is
    /// optional, and is not implemented.
    /// </summary>
    public sealed class HTTPProblemDetails
    {

        #region Data

        /// <summary>
        /// The problem type a problem has when it names none (RFC 9457, Section 4.2.1).
        /// </summary>
        public const String AboutBlank = "about:blank";

        /// <summary>
        /// The members RFC 9457 Section 3.1 defines. An extension cannot use these names.
        /// </summary>
        public static readonly String[] Members = ["type", "status", "title", "detail", "instance"];

        #endregion

        #region Properties

        /// <summary>
        /// The problem type, a URI reference. Null when the problem names none,
        /// which means <see cref="AboutBlank"/>.
        /// </summary>
        public String?          Type          { get; }

        /// <summary>
        /// The status code of the response that carries this problem.
        /// </summary>
        public HTTPStatusCode?  Status        { get; }

        /// <summary>
        /// A short, human-readable summary of the problem type.
        /// </summary>
        public String?          Title         { get; }

        /// <summary>
        /// A human-readable explanation of this occurrence of the problem.
        /// </summary>
        public String?          Detail        { get; }

        /// <summary>
        /// A URI reference naming this occurrence of the problem.
        /// </summary>
        public String?          Instance      { get; }

        /// <summary>
        /// The extension members (RFC 9457, Section 3.2), in the order they were given.
        /// </summary>
        public JObject          Extensions    { get; }

        /// <summary>
        /// The problem type, with <see cref="AboutBlank"/> for one that names none.
        /// Section 3.1.1 makes this the problem's primary identifier.
        /// </summary>
        public String           EffectiveType
            => Type ?? AboutBlank;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create problem details.
        /// </summary>
        /// <param name="Type">The problem type, a URI reference. Null means <see cref="AboutBlank"/>.</param>
        /// <param name="Status">The status code of the response that carries this problem.</param>
        /// <param name="Title">A short, human-readable summary of the problem type.</param>
        /// <param name="Detail">A human-readable explanation of this occurrence.</param>
        /// <param name="Instance">A URI reference naming this occurrence.</param>
        /// <param name="Extensions">Extension members. None of them may use a name from <see cref="Members"/>.</param>
        public HTTPProblemDetails(String?          Type         = null,
                                  HTTPStatusCode?  Status       = null,
                                  String?          Title        = null,
                                  String?          Detail       = null,
                                  String?          Instance     = null,
                                  JObject?         Extensions   = null)
        {

            var extensions = Extensions is not null
                                 ? (JObject) Extensions.DeepClone()
                                 : new JObject();

            foreach (var member in Members)
                if (extensions.ContainsKey(member))
                    throw new ArgumentException($"'{member}' is a member RFC 9457 Section 3.1 defines, not an extension.",
                                                nameof(Extensions));

            this.Type        = Type;
            this.Status      = Status;
            this.Title       = Title;
            this.Detail      = Detail;
            this.Instance    = Instance;
            this.Extensions  = extensions;

        }

        #endregion


        #region (static) ForStatus (Status, Detail = null, Extensions = null)

        /// <summary>
        /// A problem with no semantics beyond its status code: type
        /// <see cref="AboutBlank"/>, left implicit, and as its title the status
        /// code's reason phrase, which Section 4.2.1 says the title SHOULD be.
        /// </summary>
        /// <param name="Status">The status code of the response that carries this problem.</param>
        /// <param name="Detail">A human-readable explanation of this occurrence.</param>
        /// <param name="Extensions">Extension members.</param>
        public static HTTPProblemDetails ForStatus(HTTPStatusCode  Status,
                                                   String?         Detail       = null,
                                                   JObject?        Extensions   = null)

            => new (Status:      Status,
                    Title:       Status.Name,
                    Detail:      Detail,
                    Extensions:  Extensions);

        #endregion

        #region (static) IsPreferredBy (Accept)

        /// <summary>
        /// Whether a request's Accept field prefers problem details over the
        /// <c>application/json</c> a server would otherwise answer an error with.
        ///
        /// It does when it names <c>application/problem+json</c> itself, with a
        /// quality above 0 and at least the quality it gives <c>application/json</c>.
        /// For that one, the most specific range applies: <c>application/json</c>,
        /// then <c>application/*</c>, then <c>*/*</c> (RFC 9110, Section 12.5.1).
        ///
        /// A wildcard alone does not ask for problem details. A client that lists
        /// <c>application/problem+json</c> next to <c>application/json</c>, as
        /// RFC 9457's first example does, gets them.
        /// </summary>
        /// <param name="Accept">The request's Accept field, if any.</param>
        public static Boolean IsPreferredBy(AcceptTypes? Accept)
        {

            if (Accept is null)
                return false;

            Double? problemQuality   = null;
            Double  jsonQuality      = 0;
            var     jsonSpecificity  = -1;

            foreach (var acceptType in Accept)
            {

                var mainType = acceptType.ContentType?.MediaMainType;
                var subType  = acceptType.ContentType?.MediaSubType;

                if (mainType is null || subType is null)
                    continue;

                // Media types are case-insensitive (RFC 9110, Section 8.3.1).
                var isApplication = mainType.Equals("application", StringComparison.OrdinalIgnoreCase);

                if (isApplication && subType.Equals("problem+json", StringComparison.OrdinalIgnoreCase))
                {
                    problemQuality = Math.Max(problemQuality ?? 0, acceptType.Quality);
                    continue;
                }

                var specificity = isApplication && subType.Equals("json", StringComparison.OrdinalIgnoreCase)  ? 2
                                : isApplication && subType == "*"                                                ? 1
                                : mainType == "*" && subType == "*"                                              ? 0
                                : -1;

                if (specificity > jsonSpecificity)
                {
                    jsonSpecificity = specificity;
                    jsonQuality     = acceptType.Quality;
                }

            }

            return problemQuality > 0 &&
                   problemQuality >= jsonQuality;

        }

        #endregion


        #region (static) TryParse (Text,   out ProblemDetails)

        /// <summary>
        /// Parse problem details from their JSON text.
        /// False when the text is not JSON, or not a JSON object.
        /// </summary>
        /// <param name="Text">The JSON text of a problem details object.</param>
        /// <param name="ProblemDetails">The parsed problem details.</param>
        public static Boolean TryParse(String                                         Text,
                                       [NotNullWhen(true)] out HTTPProblemDetails?   ProblemDetails)
        {

            ProblemDetails = null;

            JToken json;

            try
            {
                json = JToken.Parse(Text);
            }
            catch (JsonException)
            {
                return false;
            }

            if (json is not JObject jsonObject)
                return false;

            ProblemDetails = Parse(jsonObject);
            return true;

        }

        #endregion

        #region (static) Parse    (JSON)

        /// <summary>
        /// Parse problem details from a JSON object.
        ///
        /// Section 3.1: a member whose value has the wrong type "MUST be ignored --
        /// i.e., processing will continue as if the member had not been present".
        /// So a <c>"type": 42</c> leaves the type at <see cref="AboutBlank"/>, and
        /// is not kept as an extension either. A <c>status</c> that cannot be a
        /// status code, a fraction or a number outside 100–599 (RFC 9110,
        /// Section 15), is treated the same way.
        /// </summary>
        /// <param name="JSON">A problem details object.</param>
        public static HTTPProblemDetails Parse(JObject JSON)
        {

            String?          type        = null;
            HTTPStatusCode?  status      = null;
            String?          title       = null;
            String?          detail      = null;
            String?          instance    = null;
            var              extensions  = new JObject();

            foreach (var member in JSON.Properties())
            {
                switch (member.Name)
                {

                    case "type":
                        type      = AsString(member.Value);
                        break;

                    case "status":
                        status    = AsStatusCode(member.Value);
                        break;

                    case "title":
                        title     = AsString(member.Value);
                        break;

                    case "detail":
                        detail    = AsString(member.Value);
                        break;

                    case "instance":
                        instance  = AsString(member.Value);
                        break;

                    default:
                        extensions.Add(member.Name, member.Value.DeepClone());
                        break;

                }
            }

            return new (type,
                        status,
                        title,
                        detail,
                        instance,
                        extensions);

        }

        private static String? AsString(JToken Value)

            => Value.Type == JTokenType.String
                   ? Value.Value<String>()
                   : null;

        private static HTTPStatusCode? AsStatusCode(JToken Value)
        {

            // Read from the JSON text rather than the CLR value: an integer too
            // large for Int64 arrives as a BigInteger, which no conversion to
            // Double accepts, and it is a number all the same.
            if ((Value.Type != JTokenType.Integer &&
                 Value.Type != JTokenType.Float)  ||
                !Double.TryParse(Value.ToString(Formatting.None),
                                 NumberStyles.Float,
                                 CultureInfo.InvariantCulture,
                                 out var number))
            {
                return null;
            }

            if (number < 100 || number > 599 || number != Math.Floor(number))
                return null;

            HTTPStatusCode.TryParseUInt16((UInt16) number, out var statusCode);

            return statusCode;

        }

        #endregion


        #region ToJSON()

        /// <summary>
        /// The JSON object: the members Section 3.1 defines that are set, in the
        /// order of the schema in Appendix A, then the extensions.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Type     is not null)
                json.Add("type",      Type);

            if (Title    is not null)
                json.Add("title",     Title);

            if (Status   is not null)
                json.Add("status",    Status.Code);

            if (Detail   is not null)
                json.Add("detail",    Detail);

            if (Instance is not null)
                json.Add("instance",  Instance);

            foreach (var extension in Extensions.Properties())
                json.Add(extension.Name, extension.Value.DeepClone());

            return json;

        }

        #endregion

        #region ToUTF8Bytes()

        /// <summary>
        /// The content of an <c>application/problem+json</c> response.
        /// </summary>
        public Byte[] ToUTF8Bytes()

            => ToJSON().ToUTF8Bytes();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The JSON text.
        /// </summary>
        public override String ToString()

            => ToJSON().ToString(Formatting.None);

        #endregion

    }

}
