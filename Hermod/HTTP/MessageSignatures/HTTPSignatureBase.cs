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
    using System.Globalization;
    using System.Text;


    /// <summary>
    /// The signature base of an HTTP message signature (RFC 9421, Section 2.5):
    /// one line per covered component, its identifier, ": " and its
    /// canonicalized value, and the signature parameters as the last line,
    /// without a newline after it.
    /// </summary>
    /// <remarks>
    /// Every error fails the whole base, and there is no base without one: a
    /// component that cannot be derived, a field that is not there, a parameter
    /// that is unknown or does not apply, an identifier that occurs twice. A
    /// signer that skipped such a component would sign less than it was asked
    /// to, and a verifier that skipped it would accept less than it asked for.
    /// </remarks>
    public static class HTTPSignatureBase
    {

        #region Data

        /// <summary>
        /// The fields this stack knows to be Structured Fields, by the type their
        /// definitions give them: the ones RFC 9421 itself defines, the digests of
        /// RFC 9530, and the others Hermod reads with <see cref="StructuredFields"/>.
        /// A field whose type is not known cannot be signed with <c>sf</c> or
        /// <c>key</c> (Section 2.1.1).
        /// </summary>
        public static readonly IReadOnlyDictionary<String, HTTPStructuredFieldType> KnownStructuredFields

            = new Dictionary<String, HTTPStructuredFieldType>(StringComparer.OrdinalIgnoreCase) {
                  { "signature-input",       HTTPStructuredFieldType.Dictionary },   // RFC 9421
                  { "signature",             HTTPStructuredFieldType.Dictionary },
                  { "accept-signature",      HTTPStructuredFieldType.Dictionary },
                  { "content-digest",        HTTPStructuredFieldType.Dictionary },   // RFC 9530
                  { "repr-digest",           HTTPStructuredFieldType.Dictionary },
                  { "want-content-digest",   HTTPStructuredFieldType.Dictionary },
                  { "want-repr-digest",      HTTPStructuredFieldType.Dictionary },
                  { "client-cert",           HTTPStructuredFieldType.Item       },   // RFC 9440
                  { "client-cert-chain",     HTTPStructuredFieldType.List       },
                  { "priority",              HTTPStructuredFieldType.Dictionary },   // RFC 9218
                  { "cache-status",          HTTPStructuredFieldType.List       },   // RFC 9211
                  { "proxy-status",          HTTPStructuredFieldType.List       },   // RFC 9209
                  { "cdn-cache-control",     HTTPStructuredFieldType.Dictionary }    // RFC 9213
              };

        #endregion


        #region TryCreate(Message, Parameters, out SignatureBase, out Error, StructuredFieldTypes = null)

        /// <summary>
        /// Create the signature base of the given message for the given
        /// signature parameters (Section 2.5).
        /// </summary>
        /// <param name="Message">The target message.</param>
        /// <param name="Parameters">The covered components and the signature parameters.</param>
        /// <param name="SignatureBase">The signature base, when this returns true.</param>
        /// <param name="Error">Why there is none, when this returns false.</param>
        /// <param name="StructuredFieldTypes">The Structured Field types to go by for <c>sf</c> and <c>key</c>, <see cref="KnownStructuredFields"/> by default.</param>
        public static Boolean TryCreate(HTTPSignatureMessage                                      Message,
                                        HTTPSignatureParameters                                   Parameters,
                                        [NotNullWhen(true)]  out String?                          SignatureBase,
                                        [NotNullWhen(false)] out String?                          Error,
                                        IReadOnlyDictionary<String, HTTPStructuredFieldType>?     StructuredFieldTypes   = null)
        {

            SignatureBase  = null;
            Error          = null;

            var output     = new StringBuilder();
            var covered    = new HashSet<HTTPSignatureComponent>();

            foreach (var component in Parameters.Components)
            {

                // Section 2.3: never one of the covered components, itself included.
                if (component.Name == HTTPSignatureComponent.SignatureParamsName)
                {
                    Error = "@signature-params must not be a covered component (RFC 9421, Section 2.3)!";
                    return false;
                }

                // Step 2.1
                if (!covered.Add(component))
                {
                    Error = $"The component {component} is covered twice (RFC 9421, Section 2.5)!";
                    return false;
                }

                // Step 2.5
                if (!TryGetComponentValue(Message, component, out var value, out Error, StructuredFieldTypes))
                    return false;

                // Section 2: never a newline in a component value.
                if (value.Contains('\n') || value.Contains('\r'))
                {
                    Error = $"The value of {component} contains a line break (RFC 9421, Section 2)!";
                    return false;
                }

                // Steps 2.2 to 2.7
                output.Append(component).Append(": ").Append(value).Append('\n');

            }

            // Step 3
            output.Append('"').Append(HTTPSignatureComponent.SignatureParamsName).Append("\": ").Append(Parameters);

            var signatureBase = output.ToString();

            // Step 4
            foreach (var c in signatureBase)
            {
                if (c > 0x7E || (c < 0x20 && c != '\n'))
                {
                    Error = $"The signature base contains the character U+{(Int32) c:X4}, which is not printable ASCII (RFC 9421, Section 2.5, step 4)!";
                    return false;
                }
            }

            SignatureBase = signatureBase;
            return true;

        }

        #endregion

        #region TryGetComponentValue(Message, Component, out Value, out Error, StructuredFieldTypes = null)

        /// <summary>
        /// The canonicalized value of one component of the given message
        /// (Sections 2.1, 2.2 and 2.4).
        /// </summary>
        /// <param name="Message">The target message.</param>
        /// <param name="Component">The component identifier.</param>
        /// <param name="Value">The component value, when this returns true.</param>
        /// <param name="Error">Why there is none, when this returns false.</param>
        /// <param name="StructuredFieldTypes">The Structured Field types to go by for <c>sf</c> and <c>key</c>, <see cref="KnownStructuredFields"/> by default.</param>
        public static Boolean TryGetComponentValue(HTTPSignatureMessage                                      Message,
                                                   HTTPSignatureComponent                                    Component,
                                                   [NotNullWhen(true)]  out String?                          Value,
                                                   [NotNullWhen(false)] out String?                          Error,
                                                   IReadOnlyDictionary<String, HTTPStructuredFieldType>?     StructuredFieldTypes   = null)
        {

            Value  = null;
            Error  = null;

            #region The parameters: each one known, each one applicable, no two incompatible

            foreach (var (key, parameter) in Component.Parameters.Values)
            {

                switch (key)
                {

                    case HTTPSignatureComponent.ReqParameter:
                        if (!IsTrueFlag(parameter, key, out Error))
                            return false;
                        break;

                    case HTTPSignatureComponent.SFParameter:
                    case HTTPSignatureComponent.BSParameter:
                    case HTTPSignatureComponent.TRParameter:
                        if (Component.IsDerived)
                        {
                            Error = $"The parameter '{key}' does not apply to the derived component {Component.Name} (RFC 9421, Section 2.5)!";
                            return false;
                        }
                        if (!IsTrueFlag(parameter, key, out Error))
                            return false;
                        break;

                    case HTTPSignatureComponent.KeyParameter:
                        if (Component.IsDerived)
                        {
                            Error = $"The parameter 'key' does not apply to the derived component {Component.Name} (RFC 9421, Section 2.5)!";
                            return false;
                        }
                        if (parameter.Type != SFType.String)
                        {
                            Error = $"The parameter 'key' must be a String, not {parameter} (RFC 9421, Section 2.1.2)!";
                            return false;
                        }
                        break;

                    case HTTPSignatureComponent.NameParameter:
                        if (Component.Name != HTTPSignatureComponent.QueryParamName)
                        {
                            Error = $"The parameter 'name' applies to @query-param only, not to {Component.Name} (RFC 9421, Section 2.5)!";
                            return false;
                        }
                        if (parameter.Type != SFType.String)
                        {
                            Error = $"The parameter 'name' must be a String, not {parameter} (RFC 9421, Section 2.2.8)!";
                            return false;
                        }
                        break;

                    default:
                        Error = $"The component parameter '{key}' of {Component.Name} is not understood (RFC 9421, Section 2.5)!";
                        return false;

                }

            }

            if (Component.IsTrue(HTTPSignatureComponent.BSParameter) &&
               (Component.IsTrue(HTTPSignatureComponent.SFParameter) || Component.Parameters.ContainsKey(HTTPSignatureComponent.KeyParameter)))
            {
                Error = $"'bs' is incompatible with 'sf' and 'key' in {Component} (RFC 9421, Section 2.1)!";
                return false;
            }

            #endregion

            #region The context: the target message, or with 'req' the request it answers

            var context = Message;

            if (Component.FromRequest)
            {

                if (Message.IsRequest)
                {
                    Error = $"'req' must not be used in a signature on a request, as in {Component} (RFC 9421, Section 2.4)!";
                    return false;
                }

                if (Message.Request is null)
                {
                    Error = $"{Component} is taken from the request this response answers, and that request is not known!";
                    return false;
                }

                context = Message.Request;

            }

            #endregion

            return Component.IsDerived
                       ? TryDerive        (context, Component, out Value, out Error)
                       : TryGetFieldValue (context, Component, out Value, out Error, StructuredFieldTypes ?? KnownStructuredFields);

        }

        #endregion


        #region (private static) IsTrueFlag(Parameter, Key, out Error)

        private static Boolean IsTrueFlag(SFBareItem                        Parameter,
                                          String                            Key,
                                          [NotNullWhen(false)] out String?  Error)
        {

            Error = null;

            if (Parameter.Type == SFType.Boolean && Parameter.AsBoolean)
                return true;

            Error = $"The component parameter '{Key}' is a flag and must be true, not {Parameter} (RFC 9421, Section 2.1)!";
            return false;

        }

        #endregion

        #region (private static) TryGetFieldValue(Context, Component, out Value, out Error, StructuredFieldTypes)

        private static Boolean TryGetFieldValue(HTTPSignatureMessage                                  Context,
                                                HTTPSignatureComponent                                Component,
                                                [NotNullWhen(true)]  out String?                      Value,
                                                [NotNullWhen(false)] out String?                      Error,
                                                IReadOnlyDictionary<String, HTTPStructuredFieldType>  StructuredFieldTypes)
        {

            Value  = null;
            Error  = null;

            var fromTrailers  = Component.IsTrue(HTTPSignatureComponent.TRParameter);
            var lines         = Context.GetFieldLines(Component.Name, fromTrailers).
                                        Select(line => line.Trim(' ', '\t')).
                                        ToArray();

            if (lines.Length == 0)
            {
                Error = $"The {(fromTrailers ? "trailer" : "header")} field '{Component.Name}' is not in the {(Context.IsRequest ? "request" : "response")} (RFC 9421, Section 2.5)!";
                return false;
            }

            #region bs: each line a Byte Sequence, then the List of them (Section 2.1.3)

            if (Component.IsTrue(HTTPSignatureComponent.BSParameter))
            {

                Value = String.Join(", ", lines.Select(line => SFBareItem.ByteSequence(Encoding.UTF8.GetBytes(line)).ToString()));
                return true;

            }

            #endregion

            var combined  = String.Join(", ", lines);
            var hasKey    = Component.Parameters.TryGet(HTTPSignatureComponent.KeyParameter, out var dictionaryKey);
            var strict    = Component.IsTrue(HTTPSignatureComponent.SFParameter);

            if (!hasKey && !strict)
            {
                Value = combined;
                return true;
            }

            if (!StructuredFieldTypes.TryGetValue(Component.Name, out var type))
            {
                Error = $"The field '{Component.Name}' is not known to be a Structured Field, so {Component} cannot be derived (RFC 9421, Section 2.1.1)!";
                return false;
            }

            #region key: one member of a Dictionary (Section 2.1.2)

            if (hasKey)
            {

                if (type != HTTPStructuredFieldType.Dictionary)
                {
                    Error = $"The field '{Component.Name}' is a {type}, not a Dictionary, so {Component} cannot be derived (RFC 9421, Section 2.1.2)!";
                    return false;
                }

                if (!StructuredFields.TryParseDictionary(combined, out var dictionary))
                {
                    Error = $"The field '{Component.Name}' is not a valid Dictionary (RFC 9421, Section 2.5)!";
                    return false;
                }

                if (!dictionary.TryGet(dictionaryKey.AsString, out var member) || member is null)
                {
                    Error = $"The Dictionary field '{Component.Name}' has no member '{dictionaryKey.AsString}' (RFC 9421, Section 2.1.2)!";
                    return false;
                }

                Value = member.ToString()!;
                return true;

            }

            #endregion

            #region sf: the strict serialization of the whole field (Section 2.1.1)

            switch (type)
            {

                case HTTPStructuredFieldType.List:
                    if (StructuredFields.TryParseList(combined, out var members))
                    {
                        Value = StructuredFields.Serialize(members);
                        return true;
                    }
                    break;

                case HTTPStructuredFieldType.Dictionary:
                    if (StructuredFields.TryParseDictionary(combined, out var dictionary))
                    {
                        Value = dictionary.ToString();
                        return true;
                    }
                    break;

                case HTTPStructuredFieldType.Item:
                    if (lines.Length == 1 && StructuredFields.TryParseItem(combined, out var item))
                    {
                        Value = item.ToString();
                        return true;
                    }
                    break;

            }

            Error = $"The field '{Component.Name}' is not a valid {type} (RFC 9421, Section 2.5)!";
            return false;

            #endregion

        }

        #endregion

        #region (private static) TryDerive(Context, Component, out Value, out Error)

        private static Boolean TryDerive(HTTPSignatureMessage              Context,
                                         HTTPSignatureComponent            Component,
                                         [NotNullWhen(true)]  out String?  Value,
                                         [NotNullWhen(false)] out String?  Error)
        {

            Value  = null;
            Error  = null;

            if (!HTTPSignatureComponent.DerivedComponentNames.Contains(Component.Name))
            {
                Error = $"The derived component {Component.Name} is not understood (RFC 9421, Section 2.5)!";
                return false;
            }

            #region @status: a response's only

            if (Component.Name == HTTPSignatureComponent.StatusName)
            {

                if (Context.IsRequest || !Context.StatusCode.HasValue)
                {
                    Error = "@status must not be used in a request (RFC 9421, Section 2.2.9)!";
                    return false;
                }

                Value = Context.StatusCode.Value.ToString("D3", CultureInfo.InvariantCulture);
                return true;

            }

            #endregion

            #region Every other one a request's

            if (!Context.IsRequest || Context.RequestTarget is null || Context.Method is null)
            {
                Error = $"{Component.Name} is derived from a request, and the {(Component.FromRequest ? "request" : "target message")} is a response (RFC 9421, Section 2.2)!";
                return false;
            }

            var target = Context.RequestTarget;
            String? path;
            String? query;

            if (HTTPSignatureMessage.TrySplitAbsoluteForm(target, out _, out _, out var pathAndQuery))
                SplitPathAndQuery(pathAndQuery, out path, out query);

            else if (target.StartsWith('/'))
                SplitPathAndQuery(target,       out path, out query);

            else
            {
                path   = null;   // the asterisk form and the authority form have no path
                query  = null;
            }

            switch (Component.Name)
            {

                case HTTPSignatureComponent.MethodName:
                    Value = Context.Method;
                    break;

                case HTTPSignatureComponent.RequestTargetName:
                    Value = target;
                    break;

                case HTTPSignatureComponent.SchemeName:
                    if (Context.Scheme is null)
                    {
                        Error = "The scheme of the request's target URI is not known, so @scheme cannot be derived!";
                        return false;
                    }
                    Value = Context.Scheme;
                    break;

                case HTTPSignatureComponent.AuthorityName:
                    if (!TryNormalizeAuthority(Context.Authority, Context.Scheme, out Value))
                    {
                        Error = "The request names no single authority, so @authority cannot be derived (RFC 9421, Section 2.2.3)!";
                        return false;
                    }
                    break;

                case HTTPSignatureComponent.TargetURIName:
                    if (target.Contains("://", StringComparison.Ordinal) && !target.StartsWith('/'))
                        Value = target;
                    else if (Context.Scheme is null || Context.Authority is null)
                    {
                        Error = "The scheme or the authority of the request's target URI is not known, so @target-uri cannot be derived!";
                        return false;
                    }
                    else
                        Value = $"{Context.Scheme}://{Context.Authority}{(path is null ? "" : target)}";
                    break;

                case HTTPSignatureComponent.PathName:
                    if (path is null)
                    {
                        Error = $"The request-target '{target}' has no path, so @path cannot be derived (RFC 9421, Section 2.2.6)!";
                        return false;
                    }
                    Value = path.Length == 0 ? "/" : path;
                    break;

                case HTTPSignatureComponent.QueryName:
                    Value = "?" + (query ?? "");
                    break;

                case HTTPSignatureComponent.QueryParamName:
                    if (!TryGetQueryParameter(query, Component, out Value, out Error))
                        return false;
                    break;

            }

            #endregion

            // Section 2.2: printable characters and spaces, no whitespace at
            // either end.
            if (Value is null ||
                Value.Any(c => c < 0x20 || c > 0x7E) ||
               (Value.Length > 0 && (Value[0] == ' ' || Value[^1] == ' ')))
            {
                Error = $"The value of {Component} is not printable ASCII without whitespace at either end (RFC 9421, Section 2.2)!";
                Value = null;
                return false;
            }

            return true;

        }

        #endregion

        #region (private static) SplitPathAndQuery(PathAndQuery, out Path, out Query)

        private static void SplitPathAndQuery(String PathAndQuery, out String Path, out String? Query)
        {

            var fragment = PathAndQuery.IndexOf('#');

            if (fragment >= 0)
                PathAndQuery = PathAndQuery[..fragment];

            var question = PathAndQuery.IndexOf('?');

            Path   = question < 0 ? PathAndQuery : PathAndQuery[..question];
            Query  = question < 0 ? null         : PathAndQuery[(question + 1)..];

        }

        #endregion

        #region (private static) TryNormalizeAuthority(Authority, Scheme, out Normalized)

        /// <summary>
        /// RFC 9110, Section 4.2.3: the host in lower case, and the port left out
        /// where it is the scheme's default or empty.
        /// </summary>
        private static Boolean TryNormalizeAuthority(String?                          Authority,
                                                     String?                          Scheme,
                                                     [NotNullWhen(true)] out String?  Normalized)
        {

            Normalized = null;

            if (String.IsNullOrWhiteSpace(Authority))
                return false;

            var authority  = Authority.Trim();
            var host       = authority;
            var port       = (String?) null;

            // An IPv6 literal has colons of its own, and its port follows the "]".
            var portColon  = -1;

            if (authority.StartsWith('['))
            {
                var close = authority.IndexOf(']');
                if (close >= 0 && close + 1 < authority.Length && authority[close + 1] == ':')
                    portColon = close + 1;
            }
            else
                portColon = authority.LastIndexOf(':');

            if (portColon >= 0)
            {
                host  = authority[..portColon];
                port  = authority[(portColon + 1)..];
            }

            host = host.ToLowerInvariant();

            if (String.IsNullOrEmpty(port) ||
               (port == "80"  && Scheme == "http") ||
               (port == "443" && Scheme == "https"))
                Normalized = host;

            else
                Normalized = $"{host}:{port}";

            return true;

        }

        #endregion

        #region (private static) TryGetQueryParameter(Query, Component, out Value, out Error)

        /// <summary>
        /// Section 2.2.8: the query parsed as application/x-www-form-urlencoded
        /// (WHATWG URL, Section 5.1), each name and value then percent-encoded
        /// again - with a space as "%20", as every example of the section has it.
        /// </summary>
        private static Boolean TryGetQueryParameter(String?                           Query,
                                                    HTTPSignatureComponent            Component,
                                                    [NotNullWhen(true)]  out String?  Value,
                                                    [NotNullWhen(false)] out String?  Error)
        {

            Value  = null;
            Error  = null;

            if (!Component.Parameters.TryGet(HTTPSignatureComponent.NameParameter, out var nameParameter))
            {
                Error = "@query-param requires the parameter 'name' (RFC 9421, Section 2.2.8)!";
                return false;
            }

            var name     = nameParameter.AsString;
            var matches  = new List<String>();

            foreach (var sequence in (Query ?? "").Split('&'))
            {

                if (sequence.Length == 0)
                    continue;

                var equals        = sequence.IndexOf('=');
                var rawName       = equals < 0 ? sequence : sequence[..equals];
                var rawValue      = equals < 0 ? ""       : sequence[(equals + 1)..];

                if (Encode(Decode(rawName)) == name)
                    matches.Add(Encode(Decode(rawValue)));

            }

            if (matches.Count == 0)
            {
                Error = $"The query has no parameter named '{name}' (RFC 9421, Section 2.2.8)!";
                return false;
            }

            if (matches.Count > 1)
            {
                Error = $"The query has the parameter '{name}' more than once, and then it must not be covered (RFC 9421, Section 2.2.8)!";
                return false;
            }

            Value = matches[0];
            return true;

        }

        /// <summary>
        /// WHATWG URL, Section 5.1, steps 3.4 and 3.5: "+" is a space, then the
        /// percent-decoded octets read as UTF-8.
        /// </summary>
        private static String Decode(String Text)
        {

            var octets = new List<Byte>();
            var text   = Text.Replace('+', ' ');
            var i      = 0;

            while (i < text.Length)
            {

                if (text[i] == '%'          &&
                    i + 2 < text.Length         &&
                    Uri.IsHexDigit(text[i + 1]) &&
                    Uri.IsHexDigit(text[i + 2]))
                {
                    octets.Add(Convert.ToByte(text.Substring(i + 1, 2), 16));
                    i += 3;
                    continue;
                }

                // Up to the next "%", as one run, so that a surrogate pair is
                // encoded as the one character it is.
                var next = text.IndexOf('%', i + 1);
                var run  = next < 0 ? text[i..] : text[i..next];

                octets.AddRange(Encoding.UTF8.GetBytes(run));
                i += run.Length;

            }

            return Encoding.UTF8.GetString([.. octets]);

        }

        /// <summary>
        /// WHATWG URL, Section 5.2: UTF-8, and everything but ASCII letters, digits
        /// and "*-._" percent-encoded in upper-case hexadecimal.
        /// </summary>
        private static String Encode(String Text)
        {

            var encoded = new StringBuilder();

            foreach (var octet in Encoding.UTF8.GetBytes(Text))
            {

                if (octet is >= (Byte) 'a' and <= (Byte) 'z' or
                             >= (Byte) 'A' and <= (Byte) 'Z' or
                             >= (Byte) '0' and <= (Byte) '9' or
                             (Byte) '*' or (Byte) '-' or (Byte) '.' or (Byte) '_')
                    encoded.Append((Char) octet);

                else
                    encoded.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));

            }

            return encoded.ToString();

        }

        #endregion

    }

}
