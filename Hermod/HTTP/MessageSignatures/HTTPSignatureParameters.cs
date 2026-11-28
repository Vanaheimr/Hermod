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
    /// The signature parameters of one HTTP message signature (RFC 9421,
    /// Section 2.3): the ordered set of covered components and the ordered set
    /// of metadata parameters - when it was created, when it expires, the key,
    /// the algorithm, a nonce, a tag.
    /// </summary>
    /// <remarks>
    /// The serialization of this, an Inner List with parameters, is both the
    /// last line of the signature base and the member value of
    /// <c>Signature-Input</c> (Section 4.1). A verifier rebuilds the base from
    /// what it parsed, so whatever order the signer chose has to survive, and so
    /// do parameters this implementation does not know: they were signed too.
    /// </remarks>
    public sealed class HTTPSignatureParameters
    {

        #region Well-known parameters

        /// <summary>Creation time, a UNIX timestamp (Integer).</summary>
        public const String CreatedParameter  = "created";

        /// <summary>Expiration time, a UNIX timestamp (Integer).</summary>
        public const String ExpiresParameter  = "expires";

        /// <summary>A random unique value (String).</summary>
        public const String NonceParameter    = "nonce";

        /// <summary>The algorithm, from the HTTP Signature Algorithms registry (String).</summary>
        public const String AlgParameter      = "alg";

        /// <summary>The identifier of the key material (String).</summary>
        public const String KeyIdParameter    = "keyid";

        /// <summary>An application-specific tag (String).</summary>
        public const String TagParameter      = "tag";

        #endregion

        #region Properties

        /// <summary>
        /// The covered components, in the order they are signed.
        /// </summary>
        public IReadOnlyList<HTTPSignatureComponent>  Components    { get; }

        /// <summary>
        /// The metadata parameters, in the order they are serialized.
        /// </summary>
        public SFParameters                           Parameters    { get; }


        /// <summary>The creation time, if given.</summary>
        public DateTimeOffset?  Created

            => Parameters.TryGet(CreatedParameter, out var value) && value.Type == SFType.Integer
                   ? DateTimeOffset.FromUnixTimeSeconds(value.AsInteger)
                   : null;

        /// <summary>The expiration time, if given.</summary>
        public DateTimeOffset?  Expires

            => Parameters.TryGet(ExpiresParameter, out var value) && value.Type == SFType.Integer
                   ? DateTimeOffset.FromUnixTimeSeconds(value.AsInteger)
                   : null;

        /// <summary>The nonce, if given.</summary>
        public String?          Nonce

            => GetString(NonceParameter);

        /// <summary>The algorithm, if given.</summary>
        public String?          Algorithm

            => GetString(AlgParameter);

        /// <summary>The key identifier, if given.</summary>
        public String?          KeyId

            => GetString(KeyIdParameter);

        /// <summary>The tag, if given.</summary>
        public String?          Tag

            => GetString(TagParameter);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Signature parameters with the metadata parameters given as they are,
        /// in their order.
        /// </summary>
        /// <param name="Components">The covered components, in the order to sign them.</param>
        /// <param name="Parameters">The metadata parameters.</param>
        public HTTPSignatureParameters(IEnumerable<HTTPSignatureComponent>  Components,
                                       SFParameters?                        Parameters   = null)
        {

            this.Components  = [.. Components];
            this.Parameters  = new SFParameters(Parameters?.Values);

        }


        /// <summary>
        /// Signature parameters from the well-known metadata parameters, in the
        /// order created, expires, keyid, alg, nonce, tag, of which those that
        /// are null are left out.
        /// </summary>
        /// <param name="Components">The covered components, in the order to sign them.</param>
        /// <param name="Created">The creation time; sub-second precision is dropped.</param>
        /// <param name="Expires">The expiration time; sub-second precision is dropped.</param>
        /// <param name="KeyId">The identifier of the key material.</param>
        /// <param name="Algorithm">The algorithm, when it is to be named.</param>
        /// <param name="Nonce">A nonce.</param>
        /// <param name="Tag">An application-specific tag.</param>
        public HTTPSignatureParameters(IEnumerable<HTTPSignatureComponent>  Components,
                                       DateTimeOffset?                      Created,
                                       DateTimeOffset?                      Expires     = null,
                                       String?                              KeyId       = null,
                                       String?                              Algorithm   = null,
                                       String?                              Nonce       = null,
                                       String?                              Tag         = null)

            : this(Components)

        {

            if (Created.  HasValue) Parameters.Set(CreatedParameter, SFBareItem.Integer(Created.Value.ToUnixTimeSeconds()));
            if (Expires.  HasValue) Parameters.Set(ExpiresParameter, SFBareItem.Integer(Expires.Value.ToUnixTimeSeconds()));
            if (KeyId     is not null) Parameters.Set(KeyIdParameter,   SFBareItem.String(KeyId));
            if (Algorithm is not null) Parameters.Set(AlgParameter,     SFBareItem.String(Algorithm));
            if (Nonce     is not null) Parameters.Set(NonceParameter,   SFBareItem.String(Nonce));
            if (Tag       is not null) Parameters.Set(TagParameter,     SFBareItem.String(Tag));

        }

        #endregion


        #region (private) GetString(Parameter)

        private String? GetString(String Parameter)

            => Parameters.TryGet(Parameter, out var value) && value.Type == SFType.String
                   ? value.AsString
                   : null;

        #endregion


        #region ToInnerList() / TryParse(Member, out SignatureParameters, out Error)

        /// <summary>
        /// These parameters as the Inner List they are serialized as.
        /// </summary>
        public SFInnerList ToInnerList()

            => new (Components.Select(component => component.ToSFItem()),
                    new SFParameters(Parameters.Values));


        /// <summary>
        /// Read signature parameters from a member of <c>Signature-Input</c> or of
        /// <c>Accept-Signature</c>: an Inner List of component identifiers, with
        /// the metadata parameters on it.
        /// </summary>
        /// <param name="Member">The member value.</param>
        /// <param name="SignatureParameters">The parameters, when this returns true.</param>
        /// <param name="Error">Why not, when this returns false.</param>
        public static Boolean TryParse(SFMember                                    Member,
                                       [NotNullWhen(true)]  out HTTPSignatureParameters?  SignatureParameters,
                                       [NotNullWhen(false)] out String?                   Error)
        {

            SignatureParameters  = null;
            Error                = null;

            if (Member is not SFInnerList innerList)
            {
                Error = "Signature parameters must be an Inner List!";
                return false;
            }

            var components = new List<HTTPSignatureComponent>();

            foreach (var item in innerList.Items)
            {

                if (!HTTPSignatureComponent.TryParse(item, out var component, out Error))
                    return false;

                components.Add(component);

            }

            foreach (var (key, value) in innerList.Parameters.Values)
            {

                var expected = key switch {
                                   CreatedParameter or ExpiresParameter                                  => SFType.Integer,
                                   NonceParameter or AlgParameter or KeyIdParameter or TagParameter      => SFType.String,
                                   _                                                                     => (SFType?) null
                               };

                if (expected.HasValue && value.Type != expected.Value)
                {
                    Error = $"The signature parameter '{key}' must be {(expected.Value == SFType.Integer ? "an Integer" : "a String")}, not {value}!";
                    return false;
                }

            }

            SignatureParameters = new HTTPSignatureParameters(components, innerList.Parameters);
            return true;

        }

        #endregion

        #region ToString()

        /// <summary>
        /// The serialized signature parameters (Section 2.3): the value of the
        /// <c>@signature-params</c> line, and of the member of
        /// <c>Signature-Input</c>.
        /// </summary>
        public override String ToString()

            => ToInnerList().ToString();

        #endregion

    }

}
