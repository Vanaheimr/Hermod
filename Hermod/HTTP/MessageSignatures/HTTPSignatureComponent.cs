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
    /// A component identifier of an HTTP message signature (RFC 9421, Section 2):
    /// a component name - a field name in lower case, or a derived component
    /// such as <c>@method</c> - and the parameters that say where its value is
    /// taken from and how it is canonicalized.
    /// </summary>
    /// <remarks>
    /// Two identifiers are the same when their names are and their parameters
    /// are the same set, in whatever order. The order is kept all the same,
    /// because it is part of what was signed: Section 2 lets a verifier treat
    /// <c>"foo";bar;baz</c> and <c>"foo";baz;bar</c> as one identifier, and does
    /// not let it rewrite either into the other.
    /// </remarks>
    public sealed class HTTPSignatureComponent : IEquatable<HTTPSignatureComponent>
    {

        #region Well-known names and parameters

        /// <summary>The method of a request (Section 2.2.1).</summary>
        public const String MethodName          = "@method";

        /// <summary>The target URI of a request (Section 2.2.2).</summary>
        public const String TargetURIName       = "@target-uri";

        /// <summary>The authority of a request's target URI (Section 2.2.3).</summary>
        public const String AuthorityName       = "@authority";

        /// <summary>The scheme of a request's target URI (Section 2.2.4).</summary>
        public const String SchemeName          = "@scheme";

        /// <summary>The request-target, as on the request line (Section 2.2.5).</summary>
        public const String RequestTargetName   = "@request-target";

        /// <summary>The absolute path of a request's target URI (Section 2.2.6).</summary>
        public const String PathName            = "@path";

        /// <summary>The query of a request's target URI, with its "?" (Section 2.2.7).</summary>
        public const String QueryName           = "@query";

        /// <summary>One named query parameter (Section 2.2.8).</summary>
        public const String QueryParamName      = "@query-param";

        /// <summary>The status code of a response (Section 2.2.9).</summary>
        public const String StatusName          = "@status";

        /// <summary>The signature parameters, always the last line and never covered (Section 2.3).</summary>
        public const String SignatureParamsName = "@signature-params";


        /// <summary>The field in its strict Structured Field serialization (Section 2.1.1).</summary>
        public const String SFParameter   = "sf";

        /// <summary>One member of a Dictionary field (Section 2.1.2).</summary>
        public const String KeyParameter  = "key";

        /// <summary>Each field line wrapped as a Byte Sequence (Section 2.1.3).</summary>
        public const String BSParameter   = "bs";

        /// <summary>From the request this response answers (Section 2.4).</summary>
        public const String ReqParameter  = "req";

        /// <summary>From the trailer section (Section 2.1.4).</summary>
        public const String TRParameter   = "tr";

        /// <summary>The name of a query parameter (Section 2.2.8).</summary>
        public const String NameParameter = "name";


        /// <summary>
        /// The derived components Section 2.2 defines, and so the ones a
        /// verifier here understands. Any other name that starts with "@" is an
        /// error (Section 2.5, step 2.5).
        /// </summary>
        public static readonly IReadOnlySet<String> DerivedComponentNames = new HashSet<String>(StringComparer.Ordinal) {
                                                                                MethodName,
                                                                                TargetURIName,
                                                                                AuthorityName,
                                                                                SchemeName,
                                                                                RequestTargetName,
                                                                                PathName,
                                                                                QueryName,
                                                                                QueryParamName,
                                                                                StatusName
                                                                            };

        #endregion

        #region Properties

        /// <summary>
        /// The component name: a field name in lower case, or a derived
        /// component name, which starts with "@".
        /// </summary>
        public String        Name          { get; }

        /// <summary>
        /// The component parameters, in the order they were given.
        /// </summary>
        public SFParameters  Parameters    { get; }

        /// <summary>
        /// Whether this is a derived component rather than an HTTP field.
        /// </summary>
        public Boolean       IsDerived

            => Name.StartsWith('@');

        /// <summary>
        /// Whether the value is taken from the request a response answers.
        /// </summary>
        public Boolean       FromRequest

            => IsTrue(ReqParameter);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a component identifier.
        /// </summary>
        /// <param name="Name">A field name, which is lower-cased, or a derived component name.</param>
        /// <param name="Parameters">The component parameters, if any.</param>
        public HTTPSignatureComponent(String         Name,
                                      SFParameters?  Parameters   = null)
        {

            if (String.IsNullOrEmpty(Name))
                throw new ArgumentException("A component name must not be empty!", nameof(Name));

            this.Name        = Name.StartsWith('@')
                                   ? Name
                                   : Name.ToLowerInvariant();

            this.Parameters  = new SFParameters(Parameters?.Values);

        }

        #endregion


        #region Factories

        /// <summary>The HTTP field of the given name (Section 2.1).</summary>
        public static HTTPSignatureComponent Field(String FieldName)
            => new (FieldName.StartsWith('@')
                        ? throw new ArgumentException($"'{FieldName}' is not a field name!", nameof(FieldName))
                        : FieldName);

        /// <summary><c>@method</c> (Section 2.2.1).</summary>
        public static HTTPSignatureComponent Method          => new (MethodName);

        /// <summary><c>@target-uri</c> (Section 2.2.2).</summary>
        public static HTTPSignatureComponent TargetURI       => new (TargetURIName);

        /// <summary><c>@authority</c> (Section 2.2.3).</summary>
        public static HTTPSignatureComponent Authority       => new (AuthorityName);

        /// <summary><c>@scheme</c> (Section 2.2.4).</summary>
        public static HTTPSignatureComponent Scheme          => new (SchemeName);

        /// <summary><c>@request-target</c> (Section 2.2.5).</summary>
        public static HTTPSignatureComponent RequestTarget   => new (RequestTargetName);

        /// <summary><c>@path</c> (Section 2.2.6).</summary>
        public static HTTPSignatureComponent Path            => new (PathName);

        /// <summary><c>@query</c> (Section 2.2.7).</summary>
        public static HTTPSignatureComponent Query           => new (QueryName);

        /// <summary><c>@status</c> (Section 2.2.9).</summary>
        public static HTTPSignatureComponent Status          => new (StatusName);

        /// <summary><c>@query-param;name="…"</c> (Section 2.2.8), with the name as it is encoded in the query.</summary>
        public static HTTPSignatureComponent QueryParam(String EncodedName)
            => new (QueryParamName, new SFParameters([ new (NameParameter, SFBareItem.String(EncodedName)) ]));


        /// <summary>This component, taken from the request the response answers (Section 2.4).</summary>
        public HTTPSignatureComponent FromTheRequest()
            => With(ReqParameter, SFBareItem.Boolean(true));

        /// <summary>This field, from the trailer section (Section 2.1.4).</summary>
        public HTTPSignatureComponent FromTrailers()
            => With(TRParameter, SFBareItem.Boolean(true));

        /// <summary>This field, in its strict Structured Field serialization (Section 2.1.1).</summary>
        public HTTPSignatureComponent AsStructuredField()
            => With(SFParameter, SFBareItem.Boolean(true));

        /// <summary>This field, with each field line wrapped as a Byte Sequence (Section 2.1.3).</summary>
        public HTTPSignatureComponent AsBinaryWrapped()
            => With(BSParameter, SFBareItem.Boolean(true));

        /// <summary>One member of this Dictionary field (Section 2.1.2).</summary>
        public HTTPSignatureComponent Member(String DictionaryKey)
            => With(KeyParameter, SFBareItem.String(DictionaryKey));

        private HTTPSignatureComponent With(String ParameterKey, SFBareItem Value)
            => new (Name, new SFParameters(Parameters.Values).Set(ParameterKey, Value));

        #endregion


        #region (internal) IsTrue(Parameter)

        /// <summary>
        /// Whether the given flag is present and true.
        /// </summary>
        internal Boolean IsTrue(String Parameter)

            => Parameters.TryGet(Parameter, out var value) &&
               value.Type == SFType.Boolean &&
               value.AsBoolean;

        #endregion


        #region ToSFItem() / TryParse(Item, out Component, out Error)

        /// <summary>
        /// This identifier as the String Item it is serialized as.
        /// </summary>
        public SFItem ToSFItem()

            => new (SFBareItem.String(Name), new SFParameters(Parameters.Values));


        /// <summary>
        /// Read a component identifier from an Inner List's item. Its value must
        /// be a String, and a field name in it must already be lower case
        /// (Section 2.1): this does not lower-case what a signer sent, because
        /// the identifier is part of what was signed.
        /// </summary>
        /// <param name="Item">The item.</param>
        /// <param name="Component">The component identifier, when this returns true.</param>
        /// <param name="Error">Why not, when this returns false.</param>
        public static Boolean TryParse(SFItem                                    Item,
                                       [NotNullWhen(true)]  out HTTPSignatureComponent?  Component,
                                       [NotNullWhen(false)] out String?                  Error)
        {

            Component  = null;
            Error      = null;

            if (Item.Value.Type != SFType.String)
            {
                Error = $"A component identifier must be a String, not the {Item.Value.Type} {Item.Value}!";
                return false;
            }

            var name = Item.Value.AsString;

            if (name.Length == 0)
            {
                Error = "A component name must not be empty!";
                return false;
            }

            if (!name.StartsWith('@') && name != name.ToLowerInvariant())
            {
                Error = $"The field name '{name}' must be in lower case in a component identifier (RFC 9421, Section 2.1)!";
                return false;
            }

            Component = new HTTPSignatureComponent(name, Item.Parameters);
            return true;

        }

        #endregion

        #region ToString()

        /// <summary>
        /// The serialized identifier, as it starts a line of the signature base:
        /// the name as an sf-string, then its parameters.
        /// </summary>
        public override String ToString()

            => ToSFItem().ToString();

        #endregion


        #region Equals(Other) / GetHashCode()

        /// <summary>
        /// Whether both identifiers name the same component with the same set of
        /// parameters, in whatever order (Section 2).
        /// </summary>
        public Boolean Equals(HTTPSignatureComponent? Other)
        {

            if (Other is null)
                return false;

            if (!String.Equals(Name, Other.Name, StringComparison.Ordinal) ||
                Parameters.Count != Other.Parameters.Count)
                return false;

            foreach (var (key, value) in Parameters.Values)
            {
                if (!Other.Parameters.TryGet(key, out var otherValue) ||
                    otherValue.Type != value.Type ||
                    otherValue.ToString() != value.ToString())
                    return false;
            }

            return true;

        }

        public override Boolean Equals(Object? Object)
            => Equals(Object as HTTPSignatureComponent);

        public override Int32 GetHashCode()
        {

            var hash = Name.GetHashCode(StringComparison.Ordinal);

            // The order of the parameters must not change the hash.
            foreach (var (key, value) in Parameters.Values)
                hash ^= HashCode.Combine(key, value.ToString());

            return hash;

        }

        #endregion

    }

}
