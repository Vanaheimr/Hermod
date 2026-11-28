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
    using System.Text;


    /// <summary>
    /// HTTP Message Signatures (RFC 9421): signing a message, verifying the
    /// signatures it carries, and the three fields that carry them -
    /// <c>Signature-Input</c>, <c>Signature</c> and <c>Accept-Signature</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A signature covers the components it names and nothing else. Which ones
    /// those must be is the application's to say (Section 3.2.1), and
    /// <see cref="HTTPSignatureVerificationOptions"/> is where it says it; what
    /// the RFC itself requires is checked whatever the options are.
    /// </para>
    /// <para>
    /// Nothing here touches the content. A signature that is to vouch for it
    /// covers a <c>Content-Digest</c> (RFC 9530; Section 7.2.8 of RFC 9421), and
    /// the digest is checked against the octets on its own.
    /// </para>
    /// </remarks>
    public static class HTTPMessageSignatures
    {

        #region Field names

        /// <summary>The signature parameters, by label (Section 4.1).</summary>
        public const String SignatureInputField   = "Signature-Input";

        /// <summary>The signature values, by label (Section 4.2).</summary>
        public const String SignatureField        = "Signature";

        /// <summary>The signatures asked for, by label (Section 5.1).</summary>
        public const String AcceptSignatureField  = "Accept-Signature";

        #endregion


        #region TrySign(Message, Label, Parameters, Key, out Signature, out Error, StructuredFieldTypes = null)

        /// <summary>
        /// Sign a message (Section 3.1): create the signature base for the given
        /// parameters and sign it with the given key.
        /// </summary>
        /// <param name="Message">The message, as it will be sent.</param>
        /// <param name="Label">The label, an sf-key, unique in the message.</param>
        /// <param name="Parameters">The covered components and the signature parameters.</param>
        /// <param name="Key">The key to sign with.</param>
        /// <param name="Signature">The signature, when this returns true.</param>
        /// <param name="Error">Why there is none, when this returns false.</param>
        /// <param name="StructuredFieldTypes">The Structured Field types for <c>sf</c> and <c>key</c>.</param>
        public static Boolean TrySign(HTTPSignatureMessage                                      Message,
                                      String                                                    Label,
                                      HTTPSignatureParameters                                   Parameters,
                                      HTTPSignatureKey                                          Key,
                                      [NotNullWhen(true)]  out HTTPMessageSignature?            Signature,
                                      [NotNullWhen(false)] out String?                          Error,
                                      IReadOnlyDictionary<String, HTTPStructuredFieldType>?     StructuredFieldTypes   = null)
        {

            Signature = null;

            if (!StructuredFields.IsKey(Label))
            {
                Error = $"'{Label}' is not a valid signature label: an sf-key is lower-case letters, digits, '_', '-', '.' and '*' (RFC 9421, Section 4)!";
                return false;
            }

            if (!Key.CanSign)
            {
                Error = $"The key{(Key.KeyId is null ? "" : $" '{Key.KeyId}'")} cannot sign!";
                return false;
            }

            if (Parameters.Algorithm is String algorithm && algorithm != Key.Algorithm)
            {
                Error = $"The parameter alg=\"{algorithm}\" names another algorithm than the key's, '{Key.Algorithm}' (RFC 9421, Section 3.2, step 6.5)!";
                return false;
            }

            if (!HTTPSignatureBase.TryCreate(Message, Parameters, out var signatureBase, out Error, StructuredFieldTypes))
                return false;

            Signature = new HTTPMessageSignature(
                            Label,
                            Parameters,
                            Key.Sign(Encoding.ASCII.GetBytes(signatureBase))
                        );

            return true;

        }

        #endregion

        #region TryParseSignatures(Message, out Signatures, out Error)

        /// <summary>
        /// Read the signatures a message carries: every line of
        /// <c>Signature-Input</c> and of <c>Signature</c>, in the header section
        /// and then in the trailer section (Sections 4.1 and 4.2).
        /// </summary>
        /// <remarks>
        /// A label must be in both fields, and occur once in each across all their
        /// lines. Each line is parsed as a Dictionary of its own, because a label
        /// on two lines would otherwise disappear into the last one, which is what
        /// RFC 9651, Section 4.2.2 does with a key that occurs twice.
        /// </remarks>
        /// <param name="Message">The message.</param>
        /// <param name="Signatures">The signatures, in the order of <c>Signature-Input</c>, when this returns true.</param>
        /// <param name="Error">Why they could not be read, when this returns false.</param>
        public static Boolean TryParseSignatures(HTTPSignatureMessage                                  Message,
                                                 [NotNullWhen(true)]  out IReadOnlyList<HTTPMessageSignature>?  Signatures,
                                                 [NotNullWhen(false)] out String?                      Error)
        {

            Signatures = null;

            if (!TryReadMembers(Message, SignatureInputField, out var inputs, out Error) ||
                !TryReadMembers(Message, SignatureField,      out var values, out Error))
                return false;

            var signatures = new List<HTTPMessageSignature>();

            foreach (var (label, input) in inputs)
            {

                var value = values.FirstOrDefault(member => member.Key == label);

                if (value.Key is null)
                {
                    Error = $"The signature '{label}' is in {SignatureInputField} and not in {SignatureField} (RFC 9421, Section 4)!";
                    return false;
                }

                if (value.Value is not SFItem item || item.Value.Type != SFType.ByteSequence)
                {
                    Error = $"The member '{label}' of {SignatureField} must be a Byte Sequence (RFC 9421, Section 4.2)!";
                    return false;
                }

                if (!HTTPSignatureParameters.TryParse(input, out var parameters, out var parameterError))
                {
                    Error = $"The member '{label}' of {SignatureInputField}: {parameterError}";
                    return false;
                }

                signatures.Add(new HTTPMessageSignature(label, parameters, item.Value.AsByteSequence));

            }

            foreach (var (label, _) in values)
            {
                if (!inputs.Any(member => member.Key == label))
                {
                    Error = $"The signature '{label}' is in {SignatureField} and not in {SignatureInputField} (RFC 9421, Section 4)!";
                    return false;
                }
            }

            Signatures = signatures;
            return true;

        }

        private static Boolean TryReadMembers(HTTPSignatureMessage                                              Message,
                                              String                                                            FieldName,
                                              [NotNullWhen(true)]  out List<KeyValuePair<String, SFMember>>?    Members,
                                              [NotNullWhen(false)] out String?                                  Error)
        {

            Members  = [];
            Error    = null;

            foreach (var line in Message.GetFieldLines(FieldName).Concat(Message.GetFieldLines(FieldName, FromTrailers: true)))
            {

                if (!StructuredFields.TryParseDictionary(line, out var dictionary))
                {
                    Members  = null;
                    Error    = $"{FieldName} is not a valid Dictionary (RFC 9421, Section 4)!";
                    return false;
                }

                foreach (var member in dictionary.Values)
                {

                    if (Members.Any(existing => existing.Key == member.Key))
                    {
                        Members  = null;
                        Error    = $"The label '{member.Key}' occurs twice in {FieldName} (RFC 9421, Section 4)!";
                        return false;
                    }

                    Members.Add(member);

                }

            }

            return true;

        }

        #endregion

        #region Verify(Message, Options)

        /// <summary>
        /// Verify a signature of the given message (Section 3.2): the one with
        /// the label the options name, or else the first that meets them.
        /// </summary>
        /// <param name="Message">The message, as it was received.</param>
        /// <param name="Options">What the application requires.</param>
        public static HTTPSignatureVerification Verify(HTTPSignatureMessage              Message,
                                                       HTTPSignatureVerificationOptions  Options)
        {

            if (Message.GetFieldLines(SignatureInputField).Count == 0 &&
                Message.GetFieldLines(SignatureField).     Count == 0 &&
                Message.GetFieldLines(SignatureInputField, FromTrailers: true).Count == 0 &&
                Message.GetFieldLines(SignatureField,      FromTrailers: true).Count == 0)
                return new (HTTPSignatureVerificationStatus.NotPresent, Error: "The message carries no signature.");

            if (!TryParseSignatures(Message, out var signatures, out var error))
                return new (HTTPSignatureVerificationStatus.Malformed, Error: error);

            var candidates = signatures.Where(signature => (Options.Label is null || signature.Label          == Options.Label) &&
                                                           (Options.Tag   is null || signature.Parameters.Tag == Options.Tag)).
                                        ToArray();

            if (candidates.Length == 0)
                return new (HTTPSignatureVerificationStatus.NoMatchingSignature,
                            Label: Options.Label,
                            Error: Options.Label is not null
                                       ? $"The message has no signature labeled '{Options.Label}'{(Options.Tag is null ? "" : $" and tagged '{Options.Tag}'")}."
                                       : $"The message has no signature tagged '{Options.Tag}'.");

            HTTPSignatureVerification? first = null;

            foreach (var candidate in candidates)
            {

                var result = VerifyOne(Message, candidate, Options);

                if (result.IsValid)
                    return result;

                first ??= result;

            }

            return first!;

        }

        private static HTTPSignatureVerification VerifyOne(HTTPSignatureMessage              Message,
                                                           HTTPMessageSignature              Signature,
                                                           HTTPSignatureVerificationOptions  Options)
        {

            var label       = Signature.Label;
            var parameters  = Signature.Parameters;

            HTTPSignatureVerification Fail(HTTPSignatureVerificationStatus Status, String Error, HTTPSignatureKey? Key = null)
                => new (Status, label, parameters, Key, Error);

            // Step 4: what the application requires of the parameters.
            foreach (var required in Options.RequiredComponents)
                if (!parameters.Components.Contains(required))
                    return Fail(HTTPSignatureVerificationStatus.InsufficientCoverage, $"The signature does not cover {required}.");

            var now      = Options.Now ?? DateTimeOffset.UtcNow;
            var created  = parameters.Created;
            var expires  = parameters.Expires;

            if (created is null && (Options.RequireCreated || Options.MaximumAge.HasValue))
                return Fail(HTTPSignatureVerificationStatus.CreatedMissing, "The signature has no 'created' parameter.");

            if (created > now + Options.ClockSkew)
                return Fail(HTTPSignatureVerificationStatus.CreatedInTheFuture, $"The signature was created at {created:u}, which is later than now.");

            if (created.HasValue && Options.MaximumAge.HasValue && created.Value + Options.MaximumAge.Value + Options.ClockSkew < now)
                return Fail(HTTPSignatureVerificationStatus.TooOld, $"The signature was created at {created:u}, longer ago than {Options.MaximumAge.Value}.");

            if (expires.HasValue && Options.RejectExpired && expires.Value + Options.ClockSkew < now)
                return Fail(HTTPSignatureVerificationStatus.Expired, $"The signature expired at {expires:u}.");

            // Step 5: the key.
            var key = Options.KeyResolver(parameters);

            if (key is null)
                return Fail(HTTPSignatureVerificationStatus.UnknownKey, parameters.KeyId is null
                                                                            ? "No key is known for the signature."
                                                                            : $"No key is known by the identifier '{parameters.KeyId}'.");

            // Step 6: the algorithm, from the key, which the alg parameter must agree with.
            if (!Options.AllowedAlgorithms.Contains(key.Algorithm))
                return Fail(HTTPSignatureVerificationStatus.AlgorithmNotAllowed, $"The algorithm '{key.Algorithm}' is not accepted.", key);

            if (parameters.Algorithm is String algorithm && algorithm != key.Algorithm)
                return Fail(HTTPSignatureVerificationStatus.AlgorithmMismatch, $"The signature says alg=\"{algorithm}\", and the key is for '{key.Algorithm}'.", key);

            // Step 7: the signature base, from the message as it was received.
            if (!HTTPSignatureBase.TryCreate(Message, parameters, out var signatureBase, out var baseError, Options.StructuredFieldTypes))
                return Fail(HTTPSignatureVerificationStatus.SignatureBaseError, baseError, key);

            // Step 8
            return key.Verify(Encoding.ASCII.GetBytes(signatureBase), Signature.Value)
                       ? new (HTTPSignatureVerificationStatus.Valid, label, parameters, key)
                       : Fail(HTTPSignatureVerificationStatus.Invalid, "The signature does not verify.", key);

        }

        #endregion


        #region TryParseAcceptSignature(FieldValue, out Requests, out Error)

        /// <summary>
        /// Read an <c>Accept-Signature</c> field (Section 5.1).
        /// </summary>
        /// <param name="FieldValue">The field value, its lines combined.</param>
        /// <param name="Requests">The signatures asked for, when this returns true.</param>
        /// <param name="Error">Why the field could not be read, when this returns false.</param>
        public static Boolean TryParseAcceptSignature(String                                                    FieldValue,
                                                      [NotNullWhen(true)]  out IReadOnlyList<HTTPSignatureRequest>?  Requests,
                                                      [NotNullWhen(false)] out String?                          Error)
        {

            Requests  = null;
            Error     = null;

            if (!StructuredFields.TryParseDictionary(FieldValue, out var dictionary))
            {
                Error = $"{AcceptSignatureField} is not a valid Dictionary (RFC 9421, Section 5.1)!";
                return false;
            }

            var requests = new List<HTTPSignatureRequest>();

            foreach (var (label, member) in dictionary.Values)
            {

                if (member is not SFInnerList innerList)
                {
                    Error = $"The member '{label}' of {AcceptSignatureField} must be an Inner List (RFC 9421, Section 5.1)!";
                    return false;
                }

                var components = new List<HTTPSignatureComponent>();

                foreach (var item in innerList.Items)
                {

                    if (!HTTPSignatureComponent.TryParse(item, out var component, out Error))
                        return false;

                    components.Add(component);

                }

                requests.Add(new HTTPSignatureRequest(label, components, new SFParameters(innerList.Parameters.Values)));

            }

            Requests = requests;
            return true;

        }

        #endregion

        #region TrySignAsRequested(Message, Request, Key, out Signature, out Error, Lifetime = null, Now = null)

        /// <summary>
        /// Create the signature an <c>Accept-Signature</c> member asks for
        /// (Section 5.2): with its label, covering exactly its components, and
        /// with every parameter it requests, or not at all.
        /// </summary>
        /// <param name="Message">The target message.</param>
        /// <param name="Request">The requested signature.</param>
        /// <param name="Key">The key to sign with.</param>
        /// <param name="Signature">The signature, when this returns true.</param>
        /// <param name="Error">Why the request cannot be fulfilled, when this returns false.</param>
        /// <param name="Lifetime">How long the signature is valid, for a requested <c>expires</c>.</param>
        /// <param name="Now">The creation time, now by default.</param>
        public static Boolean TrySignAsRequested(HTTPSignatureMessage                       Message,
                                                 HTTPSignatureRequest                       Request,
                                                 HTTPSignatureKey                           Key,
                                                 [NotNullWhen(true)]  out HTTPMessageSignature?  Signature,
                                                 [NotNullWhen(false)] out String?           Error,
                                                 TimeSpan?                                  Lifetime   = null,
                                                 DateTimeOffset?                            Now        = null)
        {

            Signature = null;

            var now         = Now ?? DateTimeOffset.UtcNow;
            var parameters  = new SFParameters();

            foreach (var (key, value) in Request.Parameters.Values)
            {

                switch (key)
                {

                    case HTTPSignatureParameters.CreatedParameter:
                        parameters.Set(key, SFBareItem.Integer(now.ToUnixTimeSeconds()));
                        break;

                    case HTTPSignatureParameters.ExpiresParameter:
                        if (!Lifetime.HasValue)
                        {
                            Error = "The signature is to expire, and no lifetime is given for it.";
                            return false;
                        }
                        parameters.Set(key, SFBareItem.Integer((now + Lifetime.Value).ToUnixTimeSeconds()));
                        break;

                    case HTTPSignatureParameters.NonceParameter:
                    case HTTPSignatureParameters.TagParameter:
                        if (value.Type != SFType.String)
                        {
                            Error = $"The requested '{key}' must be a String, not {value} (RFC 9421, Section 5.1)!";
                            return false;
                        }
                        parameters.Set(key, value);
                        break;

                    case HTTPSignatureParameters.AlgParameter:
                        if (value.Type != SFType.String || value.AsString != Key.Algorithm)
                        {
                            Error = $"The signature is asked for in {value}, and the key is for '{Key.Algorithm}'.";
                            return false;
                        }
                        parameters.Set(key, value);
                        break;

                    case HTTPSignatureParameters.KeyIdParameter:
                        if (value.Type != SFType.String || value.AsString != Key.KeyId)
                        {
                            Error = $"The signature is asked for with the key {value}, and the key is '{Key.KeyId}'.";
                            return false;
                        }
                        parameters.Set(key, value);
                        break;

                    default:
                        Error = $"The requested parameter '{key}' is not understood, so the request cannot be fulfilled (RFC 9421, Section 5.2, step 2.4).";
                        return false;

                }

            }

            return TrySign(Message,
                           Request.Label,
                           new HTTPSignatureParameters(Request.Components, parameters),
                           Key,
                           out Signature,
                           out Error);

        }

        #endregion

    }

}
