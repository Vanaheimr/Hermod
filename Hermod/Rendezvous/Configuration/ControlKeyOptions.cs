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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// One public key of the control endpoint, as it can be written into a
    /// configuration file:
    ///
    ///     {
    ///       "Id":              "operator-1",
    ///       "KeyType":         "Ed25519",
    ///       "PublicKey":       "&lt;base64&gt;",
    ///       "NotBefore":       "2026-01-01T00:00:00Z",
    ///       "NotAfter":        "2026-12-31T23:59:59Z",
    ///       "Description":     "The operator on duty",
    ///       "IsAdministrator": false,
    ///       "CreatedBy":       "achim"
    ///     }
    ///
    /// Only public keys ever appear here - a private key has no business
    /// being in a configuration file of a service.
    /// </summary>
    public sealed class ControlKeyOptions
    {

        #region Properties

        /// <summary>
        /// The unique identification of the key.
        /// </summary>
        public String            Id                { get; set; } = "";

        /// <summary>
        /// The type of the key: Ed25519, Ed448, MLDsa44, MLDsa65 or MLDsa87.
        /// </summary>
        public SignatureKeyType  KeyType           { get; set; } = SignatureKeyType.Ed25519;

        /// <summary>
        /// The base64 encoded raw public key.
        /// </summary>
        public String            PublicKey         { get; set; } = "";

        /// <summary>
        /// An optional timestamp before which the key is not valid.
        /// </summary>
        public DateTimeOffset?   NotBefore         { get; set; }

        /// <summary>
        /// An optional timestamp after which the key is not valid.
        /// </summary>
        public DateTimeOffset?   NotAfter          { get; set; }

        /// <summary>
        /// An optional description, e.g. who owns this key.
        /// </summary>
        public String?           Description       { get; set; }

        /// <summary>
        /// Whether this key may also close the rendezvous of somebody else.
        /// Give this to as few keys as possible.
        /// </summary>
        public Boolean           IsAdministrator   { get; set; }

        /// <summary>
        /// An optional timestamp when this key was configured.
        /// </summary>
        public DateTimeOffset?   Created           { get; set; }

        /// <summary>
        /// An optional note who configured this key.
        /// </summary>
        public String?           CreatedBy         { get; set; }

        #endregion


        #region TryToControlKey(out Key, out ErrorResponse)

        /// <summary>
        /// Try to turn this configuration into a control key.
        /// </summary>
        /// <param name="Key">The control key.</param>
        /// <param name="ErrorResponse">An error response, when the configuration is invalid.</param>
        public Boolean TryToControlKey([NotNullWhen(true)]  out ControlKey?  Key,
                                       [NotNullWhen(false)] out String?      ErrorResponse)
        {

            Key = null;

            if (String.IsNullOrWhiteSpace(Id))
            {
                ErrorResponse = "A control key must have an identification!";
                return false;
            }

            Byte[] publicKey;

            try
            {
                publicKey = Convert.FromBase64String(PublicKey);
            }
            catch (FormatException)
            {
                ErrorResponse = $"The public key of '{Id}' is not valid base64!";
                return false;
            }

            try
            {

                Key            = new ControlKey(Id, KeyType, publicKey, NotBefore, NotAfter, Description, IsAdministrator, Created, CreatedBy);
                ErrorResponse  = null;

                return true;

            }
            catch (ArgumentException e)
            {
                ErrorResponse = $"The control key '{Id}' is invalid: {e.Message}";
                return false;
            }

        }

        #endregion

        #region Validate(Path)

        /// <summary>
        /// Validate this configuration and return a human readable error message
        /// for every invalid value.
        /// </summary>
        /// <param name="Path">The configuration path, used within the error messages.</param>
        public IEnumerable<String> Validate(String Path)
        {

            if (!TryToControlKey(out _, out var errorResponse))
                yield return $"{Path}: {errorResponse}";

        }

        #endregion

    }

}
