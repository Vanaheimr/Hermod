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
    /// HTTP Message Signatures (RFC 9421) on HTTP/1.x requests and responses:
    /// a message's components as they are on the wire, signing a builder, and
    /// verifying what arrived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The components are read from <see cref="AHTTPPDU.RawHTTPHeader"/>: for
    /// a message that arrived the header as it came, for a builder the header as
    /// it will go out, since the immutable message a builder makes is parsed from
    /// that text. So a field on two lines is two lines, which <c>bs</c> needs,
    /// and a request-target is the one on the request line, which
    /// <c>@path</c> and <c>@query</c> need undecoded.
    /// </para>
    /// <para>
    /// The scheme is the one thing an HTTP/1.x request in origin form does not
    /// carry. A request in absolute form has its own; otherwise a request has
    /// the scheme of the client's <c>RemoteURL</c>, or of the connection it
    /// arrived on. A builder knows neither, so signing one takes the scheme as
    /// an argument, and without it <c>@scheme</c> and <c>@target-uri</c> cannot
    /// be derived.
    /// </para>
    /// </remarks>
    public static class HTTPMessageSignatureExtensions
    {

        #region ToSignatureMessage(this Request,  Scheme = null)

        /// <summary>
        /// The components of this request, as an HTTP message signature sees them.
        /// </summary>
        /// <param name="Request">The request.</param>
        /// <param name="Scheme">The scheme of its target URI, when it is not the one the request knows.</param>
        public static HTTPSignatureMessage ToSignatureMessage(this HTTPRequest  Request,
                                                             String?           Scheme   = null)
        {

            var scheme = Scheme
                      ?? Request.TargetScheme
                      ?? (Request.HTTPClient is not null
                              ? Request.HTTPClient.RemoteURL.Scheme?.EnforcesTLS == true ? "https" : "http"
                              : Request.HTTPServer is not null
                                    ? Request.ServerCertificate is not null ? "https" : "http"
                                    : null);

            return HTTPSignatureMessage.TryParse(Request.RawHTTPHeader,
                                                 scheme,
                                                 out var message,
                                                 Request.TrailingHeaders)
                       ? message
                       : throw new ArgumentException("The request's header section cannot be read!", nameof(Request));

        }

        #endregion

        #region ToSignatureMessage(this Response, RequestScheme = null)

        /// <summary>
        /// The components of this response, as an HTTP message signature sees
        /// them, with the request it answers for <c>req</c>.
        /// </summary>
        /// <param name="Response">The response.</param>
        /// <param name="RequestScheme">The scheme of the request's target URI, when it is not the one the request knows.</param>
        public static HTTPSignatureMessage ToSignatureMessage(this HTTPResponse  Response,
                                                             String?            RequestScheme   = null)

            => HTTPSignatureMessage.TryParse(Response.RawHTTPHeader,
                                             null,
                                             out var message,
                                             Response.TrailingHeaders,
                                             Response.HTTPRequest?.ToSignatureMessage(RequestScheme))
                   ? message
                   : throw new ArgumentException("The response's header section cannot be read!", nameof(Response));

        #endregion


        #region VerifySignature(this Request,  Options, Scheme        = null)

        /// <summary>
        /// Verify a signature on this request (RFC 9421, Section 3.2).
        /// </summary>
        /// <param name="Request">The request, as it arrived.</param>
        /// <param name="Options">What the application requires of the signature.</param>
        /// <param name="Scheme">The scheme of its target URI, when it is not the one the request knows.</param>
        public static HTTPSignatureVerification VerifySignature(this HTTPRequest                  Request,
                                                                HTTPSignatureVerificationOptions  Options,
                                                                String?                           Scheme   = null)

            => HTTPMessageSignatures.Verify(Request.ToSignatureMessage(Scheme), Options);

        #endregion

        #region VerifySignature(this Response, Options, RequestScheme = null)

        /// <summary>
        /// Verify a signature on this response (RFC 9421, Section 3.2).
        /// </summary>
        /// <param name="Response">The response, as it arrived.</param>
        /// <param name="Options">What the application requires of the signature.</param>
        /// <param name="RequestScheme">The scheme of the request's target URI, when it is not the one the request knows.</param>
        public static HTTPSignatureVerification VerifySignature(this HTTPResponse                 Response,
                                                                HTTPSignatureVerificationOptions  Options,
                                                                String?                           RequestScheme   = null)

            => HTTPMessageSignatures.Verify(Response.ToSignatureMessage(RequestScheme), Options);

        #endregion


        #region TrySign(this RequestBuilder,  Label, Parameters, Key, out Error, Scheme = null)

        /// <summary>
        /// Sign the request this builder will make, and add the signature to its
        /// <c>Signature-Input</c> and <c>Signature</c> fields, beside any that
        /// are there already (RFC 9421, Sections 3.1 and 4.3).
        /// </summary>
        /// <remarks>
        /// Sign last. A field set after this, or a covered one changed, is not
        /// what was signed; and a client sending the request adds only fields of
        /// its own (<c>TOTP</c>), never changing one the builder had.
        /// </remarks>
        /// <param name="Builder">The request builder.</param>
        /// <param name="Label">The signature's label.</param>
        /// <param name="Parameters">The covered components and the signature parameters.</param>
        /// <param name="Key">The key to sign with.</param>
        /// <param name="Error">Why the request was not signed, when this returns false.</param>
        /// <param name="Scheme">The scheme of the request's target URI, for <c>@scheme</c> and <c>@target-uri</c>.</param>
        public static Boolean TrySign(this HTTPRequest.Builder          Builder,
                                      String                            Label,
                                      HTTPSignatureParameters           Parameters,
                                      HTTPSignatureKey                  Key,
                                      [NotNullWhen(false)] out String?  Error,
                                      String?                           Scheme   = null)
        {

            if (!HTTPSignatureMessage.TryParse(Builder.AsImmutable.RawHTTPHeader, Scheme, out var message))
            {
                Error = "The request's header section cannot be read!";
                return false;
            }

            return TryAddSignature(Builder, message, Label, Parameters, Key, out Error);

        }

        #endregion

        #region TrySign(this ResponseBuilder, Label, Parameters, Key, out Error, RequestScheme = null)

        /// <summary>
        /// Sign the response this builder will make, and add the signature to its
        /// <c>Signature-Input</c> and <c>Signature</c> fields, beside any that
        /// are there already (RFC 9421, Sections 3.1 and 4.3). Components with
        /// <c>req</c> are taken from the builder's <c>HTTPRequest</c>.
        /// </summary>
        /// <remarks>
        /// A signed response is not compressed by the server: the
        /// <c>Content-Encoding</c>, <c>Content-Length</c> and digests a signature
        /// may cover would change under it. The server adds its own fields,
        /// <c>Date</c> where there is none, and leaves the builder's alone.
        /// </remarks>
        /// <param name="Builder">The response builder.</param>
        /// <param name="Label">The signature's label.</param>
        /// <param name="Parameters">The covered components and the signature parameters.</param>
        /// <param name="Key">The key to sign with.</param>
        /// <param name="Error">Why the response was not signed, when this returns false.</param>
        /// <param name="RequestScheme">The scheme of the request's target URI, when it is not the one the request knows.</param>
        public static Boolean TrySign(this HTTPResponse.Builder         Builder,
                                      String                            Label,
                                      HTTPSignatureParameters           Parameters,
                                      HTTPSignatureKey                  Key,
                                      [NotNullWhen(false)] out String?  Error,
                                      String?                           RequestScheme   = null)
        {

            var draft = Builder.AsImmutable;

            if (!HTTPSignatureMessage.TryParse(draft.RawHTTPHeader,
                                               null,
                                               out var message,
                                               draft.TrailingHeaders,
                                               Builder.HTTPRequest?.ToSignatureMessage(RequestScheme)))
            {
                Error = "The response's header section cannot be read!";
                return false;
            }

            return TryAddSignature(Builder, message, Label, Parameters, Key, out Error);

        }

        #endregion


        #region (private static) TryAddSignature(Builder, Message, Label, Parameters, Key, out Error)

        private static Boolean TryAddSignature(AHTTPPDUBuilder                   Builder,
                                               HTTPSignatureMessage              Message,
                                               String                            Label,
                                               HTTPSignatureParameters           Parameters,
                                               HTTPSignatureKey                  Key,
                                               [NotNullWhen(false)] out String?  Error)
        {

            var existingInput  = GetField(Builder, HTTPMessageSignatures.SignatureInputField);
            var existingValue  = GetField(Builder, HTTPMessageSignatures.SignatureField);

            if ((existingInput is not null && StructuredFields.TryParseDictionary(existingInput, out var inputs) && inputs.ContainsKey(Label)) ||
                (existingValue is not null && StructuredFields.TryParseDictionary(existingValue, out var values) && values.ContainsKey(Label)))
            {
                Error = $"The message already has a signature labeled '{Label}' (RFC 9421, Section 4)!";
                return false;
            }

            if (!HTTPMessageSignatures.TrySign(Message, Label, Parameters, Key, out var signature, out Error))
                return false;

            Builder.SetHeaderField(HTTPMessageSignatures.SignatureInputField,
                                   existingInput is null ? signature.SignatureInputMember : $"{existingInput}, {signature.SignatureInputMember}");

            Builder.SetHeaderField(HTTPMessageSignatures.SignatureField,
                                   existingValue is null ? signature.SignatureMember      : $"{existingValue}, {signature.SignatureMember}");

            return true;

        }

        private static String? GetField(AHTTPPDUBuilder Builder, String FieldName)
        {

            foreach (var (name, value) in Builder)
            {
                if (name.Equals(FieldName, StringComparison.OrdinalIgnoreCase))
                    return value switch {
                               null                  => null,
                               String          text  => text,
                               IEnumerable<String> lines => String.Join(", ", lines),
                               _                     => value.ToString()
                           };
            }

            return null;

        }

        #endregion

    }

}
