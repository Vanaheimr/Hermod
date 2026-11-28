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

    using System.Diagnostics.CodeAnalysis;


    /// <summary>
    /// What an HTTP message signature is computed over (RFC 9421, Section 2):
    /// a request's control data or a response's status code, the field lines of
    /// its header and trailer sections in the order they occur, and, for a
    /// response, the request it answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Version-neutral on purpose. HTTP/1.x builds one from the header as it is
    /// on the wire, HTTP/2 and HTTP/3 could from their pseudo-header fields: a
    /// signature over <c>@authority</c> means the same in all three (Section
    /// 2.2.3), and is what makes signing <c>Host</c> itself a bad idea
    /// (Section 7.2.4).
    /// </para>
    /// <para>
    /// The field lines are kept apart rather than combined. Section 2.1 combines
    /// them with ", " for the plain component value, Section 2.1.3 wraps each one
    /// separately for <c>bs</c>, and only the second can tell two lines from one
    /// line with a comma in it.
    /// </para>
    /// </remarks>
    public sealed class HTTPSignatureMessage
    {

        #region Properties

        /// <summary>Whether this is a request rather than a response.</summary>
        public Boolean                                         IsRequest        { get; }

        /// <summary>The method of a request, as it was sent: methods are case-sensitive.</summary>
        public String?                                         Method           { get; }

        /// <summary>The scheme of a request's target URI, in lower case, when it is known.</summary>
        public String?                                         Scheme           { get; }

        /// <summary>The authority of a request's target URI as it was given, before normalization.</summary>
        public String?                                         Authority        { get; }

        /// <summary>The request-target, as on the request line.</summary>
        public String?                                         RequestTarget    { get; }

        /// <summary>The status code of a response.</summary>
        public UInt16?                                         StatusCode       { get; }

        /// <summary>The header field lines, in order.</summary>
        public IReadOnlyList<KeyValuePair<String, String>>     HeaderFields     { get; }

        /// <summary>The trailer field lines, in order.</summary>
        public IReadOnlyList<KeyValuePair<String, String>>     TrailerFields    { get; }

        /// <summary>For a response, the request it answers, when that is known.</summary>
        public HTTPSignatureMessage?                           Request          { get; }

        #endregion

        #region Constructor(s)

        private HTTPSignatureMessage(Boolean                                          IsRequest,
                                     String?                                          Method,
                                     String?                                          Scheme,
                                     String?                                          Authority,
                                     String?                                          RequestTarget,
                                     UInt16?                                          StatusCode,
                                     IEnumerable<KeyValuePair<String, String>>        HeaderFields,
                                     IEnumerable<KeyValuePair<String, String>>?       TrailerFields,
                                     HTTPSignatureMessage?                            Request)
        {

            this.IsRequest      = IsRequest;
            this.Method         = Method;
            this.Scheme         = Scheme?.ToLowerInvariant();
            this.Authority      = Authority;
            this.RequestTarget  = RequestTarget;
            this.StatusCode     = StatusCode;
            this.HeaderFields   = [.. HeaderFields];
            this.TrailerFields  = [.. TrailerFields ?? []];
            this.Request        = Request;

        }

        #endregion


        #region (static) ForRequest (Method, Scheme, RequestTarget, HeaderFields, TrailerFields = null)

        /// <summary>
        /// A request. Its authority is the one an absolute-form request-target
        /// names, and otherwise that of its <c>Host</c> field (RFC 9112,
        /// Section 3.2).
        /// </summary>
        /// <param name="Method">The method.</param>
        /// <param name="Scheme">The scheme of the target URI, which a request in origin form does not carry: the connection's.</param>
        /// <param name="RequestTarget">The request-target, as on the request line.</param>
        /// <param name="HeaderFields">The header field lines, in order.</param>
        /// <param name="TrailerFields">The trailer field lines, in order.</param>
        public static HTTPSignatureMessage ForRequest(String                                       Method,
                                                      String?                                      Scheme,
                                                      String                                       RequestTarget,
                                                      IEnumerable<KeyValuePair<String, String>>    HeaderFields,
                                                      IEnumerable<KeyValuePair<String, String>>?   TrailerFields   = null)
        {

            var fields     = HeaderFields.ToArray();
            var authority  = (String?) null;

            if (TrySplitAbsoluteForm(RequestTarget, out var targetScheme, out var targetAuthority, out _))
            {
                Scheme     = targetScheme;
                authority  = targetAuthority;
            }
            else if (!RequestTarget.StartsWith('/') && RequestTarget != "*")
                authority  = RequestTarget;   // authority-form

            // Two Host lines name no authority (RFC 9112, Section 3.2).
            var hosts = fields.Where (field => field.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)).
                               Select(field => field.Value.Trim()).
                               ToArray();

            authority ??= hosts.Length == 1 ? hosts[0] : null;

            return new (true, Method, Scheme, authority, RequestTarget, null, fields, TrailerFields, null);

        }

        #endregion

        #region (static) ForResponse(StatusCode, HeaderFields, TrailerFields = null, Request = null)

        /// <summary>
        /// A response.
        /// </summary>
        /// <param name="StatusCode">The status code.</param>
        /// <param name="HeaderFields">The header field lines, in order.</param>
        /// <param name="TrailerFields">The trailer field lines, in order.</param>
        /// <param name="Request">The request it answers, for components with <c>req</c> (Section 2.4).</param>
        public static HTTPSignatureMessage ForResponse(UInt16                                       StatusCode,
                                                       IEnumerable<KeyValuePair<String, String>>    HeaderFields,
                                                       IEnumerable<KeyValuePair<String, String>>?   TrailerFields   = null,
                                                       HTTPSignatureMessage?                        Request         = null)
        {

            if (Request is not null && !Request.IsRequest)
                throw new ArgumentException("The message a response answers must be a request!", nameof(Request));

            return new (false, null, null, null, null, StatusCode, HeaderFields, TrailerFields, Request);

        }

        #endregion

        #region (static) TryParse(Header, Scheme, out Message, TrailerFields = null, Request = null)

        /// <summary>
        /// Read an HTTP/1.x header section: the request line or the status line,
        /// then one field line after the other. Lines end in CRLF or LF, and the
        /// section may or may not end in an empty line. An obs-fold is replaced
        /// with a single space (RFC 9421, Section 2.1, step 3), and leading and
        /// trailing whitespace is stripped from each value (step 2).
        /// </summary>
        /// <param name="Header">The header section.</param>
        /// <param name="Scheme">The scheme of a request's target URI where the request-target does not say: the connection's.</param>
        /// <param name="Message">The message, when this returns true.</param>
        /// <param name="TrailerFields">The trailer field lines, in order.</param>
        /// <param name="Request">For a response, the request it answers.</param>
        public static Boolean TryParse(String                                       Header,
                                       String?                                      Scheme,
                                       [NotNullWhen(true)] out HTTPSignatureMessage?  Message,
                                       IEnumerable<KeyValuePair<String, String>>?   TrailerFields   = null,
                                       HTTPSignatureMessage?                        Request         = null)
        {

            Message = null;

            var lines = Header.Replace("\r\n", "\n").Split('\n');

            if (lines.Length == 0 || lines[0].Length == 0)
                return false;

            var fields = new List<KeyValuePair<String, String>>();

            for (var i = 1; i < lines.Length; i++)
            {

                var line = lines[i];

                if (line.Length == 0)
                    break;

                // obs-fold (RFC 9112, Section 5.2)
                if (line[0] is ' ' or '\t')
                {

                    if (fields.Count == 0)
                        return false;

                    var last = fields[^1];
                    fields[^1] = new (last.Key, String.Concat(last.Value, " ", line.Trim(' ', '\t')).Trim(' ', '\t'));
                    continue;

                }

                var colon = line.IndexOf(':');

                if (colon <= 0)
                    return false;

                fields.Add(new (line[..colon], line[(colon + 1)..].Trim(' ', '\t')));

            }

            var startLine = lines[0].Split(' ', 3);

            if (startLine.Length < 2)
                return false;

            // A status line starts with the protocol version, a request line
            // with a method, which never contains a "/".
            if (startLine[0].StartsWith("HTTP/", StringComparison.Ordinal))
            {

                if (!UInt16.TryParse(startLine[1], out var statusCode) || startLine[1].Length != 3)
                    return false;

                Message = ForResponse(statusCode, fields, TrailerFields, Request);
                return true;

            }

            if (startLine.Length != 3)
                return false;

            Message = ForRequest(startLine[0], Scheme, startLine[1], fields, TrailerFields);
            return true;

        }

        #endregion


        #region GetFieldLines(Name, FromTrailers = false)

        /// <summary>
        /// The values of every line of the named field, in order: empty when
        /// there is none.
        /// </summary>
        /// <param name="Name">The field name, in any case.</param>
        /// <param name="FromTrailers">Whether to look in the trailer section rather than the header section.</param>
        public IReadOnlyList<String> GetFieldLines(String   Name,
                                                    Boolean  FromTrailers   = false)

            => [.. (FromTrailers ? TrailerFields : HeaderFields).
                       Where (field => field.Key.Equals(Name, StringComparison.OrdinalIgnoreCase)).
                       Select(field => field.Value)];

        #endregion


        #region (internal static) TrySplitAbsoluteForm(RequestTarget, out Scheme, out Authority, out PathAndQuery)

        /// <summary>
        /// Split a request-target in absolute form (RFC 9112, Section 3.2.2)
        /// into its scheme, its authority and the rest.
        /// </summary>
        internal static Boolean TrySplitAbsoluteForm(String                               RequestTarget,
                                                     [NotNullWhen(true)] out String?      Scheme,
                                                     [NotNullWhen(true)] out String?      Authority,
                                                     [NotNullWhen(true)] out String?      PathAndQuery)
        {

            Scheme        = null;
            Authority     = null;
            PathAndQuery  = null;

            var separator = RequestTarget.IndexOf("://", StringComparison.Ordinal);

            if (separator <= 0 || RequestTarget.StartsWith('/'))
                return false;

            var rest = RequestTarget[(separator + 3)..];
            var end  = rest.IndexOfAny(['/', '?', '#']);

            Scheme        = RequestTarget[..separator].ToLowerInvariant();
            Authority     = end < 0 ? rest : rest[..end];
            PathAndQuery  = end < 0 ? ""   : rest[end..];

            return true;

        }

        #endregion

    }

}
