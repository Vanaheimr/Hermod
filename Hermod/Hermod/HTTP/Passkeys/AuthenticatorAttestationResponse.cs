/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{


    public class AuthenticatorAttestationResponse
    {

        #region Properties

        public String   ClientDataJSON               { get; }

        public String   AttestationObject            { get; }


        public JObject  ParsedClientData             { get; }
        public JObject  ParsedAttestation            { get; }
        public JObject  ParsedAttestationAuthData    { get; }

        #endregion

        #region Constructor(s)

        public AuthenticatorAttestationResponse(String  ClientDataJSON,
                                                String  AttestationObject)
        {

            this.ClientDataJSON     = ClientDataJSON;
            this.AttestationObject  = AttestationObject;


            // {
            //     "type":                          "webauthn.create",
            //     "challenge":                     "Fz-M6t4Z1B_Sa4CPwUnp7sG19QcU-6vWtH9LHA4Avzo",
            //     "origin":                        "http://localhost:9000",
            //     "crossOrigin":                    false,
            //     "other_keys_can_be_added_here":  "do not compare clientDataJSON against a template. See https://goo.gl/yabPex"
            // }

            try
            {
                this.ParsedClientData  = JObject.Parse(ClientDataJSON.FromBASE64().ToUTF8String());
            }
            catch (Exception e)
            {
                throw new ArgumentException("The given clientDataJSON is invalid!", nameof(ClientDataJSON), e);
            }


            // {
            //   "fmt":       "none",
            //   "attStmt":    {},
            //   "authData":  "SZYN5YgOjGh0NBcPZHZgW4/krrmihjLHmVzzuoMdl2NFAAAAAAiYcFjK3EuBtuEw3lDcvpYAIFLeZFGCOZoHAucai/RhLAnzTZmZtMEuzkkWbf7Y9T1xpQECAyYgASFYIGzKF7OK0p5zYmN9Vwm6XuL/FNxnn/qKLQLic7ABFnMVIlggR2wLMO1W5gPr+YO8koTtSCrMpU6MEDecc7sPAcmnayg="
            // }

            try
            {
                this.ParsedAttestation = CborToJsonConverter.CBOR2JSON(AttestationObject.FromBASE64()) ?? [];
            }
            catch (Exception e)
            {
                throw new ArgumentException("The given AttestationObject is invalid!", nameof(AttestationObject), e);
            }




            // {
            //   "rpIdHash": "49960de5880e8c687434170f6476605b8fe4aeb9a28632c7995cf3ba831d9763",        // A SHA‑256 hash of the relying party identifier
            //   "flags": 69,                                                                           // ToDO: Decode flags
            //   "signCount": 0,                                                                        // A big‑endian unsigned integer
            //   "attestedCredentialData": {                                                            // only present when the attested flag is set!
            //     "aaguid":       "08987058cadc4b81b6e130de50dcbe96",                                  // In many authenticators this is all zeros!
            //     "credentialId": "52de645182399a0702e71a8bf4612c09f34d9999b4c12ece49166dfed8f53d71",  // The unique identifier for the credential
            //     "credentialPublicKey": {
            //        "1":  2,                                                                          // "kty" => EC2
            //        "3": -7,                                                                          // "alg" => ES256
            //       "-1":  1,                                                                          // "crv" => P-256
            //       "-2": "6cca17b38ad29e7362637d5709ba5ee2ff14dc679ffa8a2d02e273b001167315",          // Elliptic curve public key x-coordinate
            //       "-3": "476c0b30ed56e603ebf983bc9284ed482acca54e8c10379c73bb0f01c9a76b28"           // Elliptic curve public key y-coordinate
            //     }
            //   }
            // }

            try
            {

                var authData = this.ParsedAttestation["authData"]?.Value<Byte[]>() ?? [];

                // Parse the authenticator data per WebAuthn spec.
                // authData layout:
                //   - rpIdHash (32 bytes)
                //   - flags (1 byte)
                //   - signCount (4 bytes)
                //   - If (flags & 0x40 != 0) then attestedCredentialData follows.
                var rpIdHash   = new Byte[32];
                Buffer.BlockCopy(authData, 0, rpIdHash, 0, 32);
                var flags      = authData[32];
                var signCount  = (UInt32) (authData[33] << 24 | authData[34] << 16 | authData[35] << 8 | authData[36]);

                JObject? attestedCredentialData = null;
                if ((flags & 0x40) != 0)
                {

                    int offset = 37;

                    // AAGUID (16 bytes)
                    var aaguid = new Byte[16];
                    Buffer.BlockCopy(authData, offset, aaguid, 0, 16);
                    offset += 16;

                    // Credential ID length (2 bytes, big-endian)
                    var credIdLen = (UInt16) (authData[offset] << 8 | authData[offset + 1]);
                    offset += 2;

                    // Credential ID (credIdLen bytes)
                    var credentialId = new Byte[credIdLen];
                    Buffer.BlockCopy(authData, offset, credentialId, 0, credIdLen);
                    offset += credIdLen;

                    // The remainder is the credential public key (CBOR encoded)
                    var publicKeyLen = authData.Length - offset;
                    var credentialPublicKeyCbor = new Byte[publicKeyLen];
                    Buffer.BlockCopy(authData, offset, credentialPublicKeyCbor, 0, publicKeyLen);

                    // Decode the public key CBOR.
                    // (This example uses a helper method to decode the CBOR into a JObject.)
                    var credentialPublicKey = CborToJsonConverter.DecodeCborMap(credentialPublicKeyCbor);

                    attestedCredentialData = new JObject {
                                                 { "aaguid",               BitConverter.ToString(aaguid).      Replace("-", "").ToLower() },
                                                 { "credentialId",         BitConverter.ToString(credentialId).Replace("-", "").ToLower() },
                                                 { "credentialPublicKey",  credentialPublicKey }
                                             };

                }

                this.ParsedAttestationAuthData = new JObject {
                                                     { "rpIdHash",   BitConverter.ToString(rpIdHash).Replace("-", "").ToLower() },
                                                     { "flags",      flags },
                                                     { "signCount",  signCount }
                                                 };

                if (attestedCredentialData is not null)
                    this.ParsedAttestationAuthData["attestedCredentialData"] = attestedCredentialData;

            }
            catch (Exception e)
            {
                throw new ArgumentException("The given AttestationObject.AuthData is invalid!", nameof(AttestationObject), e);
            }

        }

        #endregion


        #region (static) TryParse(JSON, out AuthenticatorAttestationResponse, out ErrorResponse)

        /// <summary>
        /// Try to parse the given JSON representation of an AuthenticatorAttestationResponse.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="AuthenticatorAttestationResponse">The parsed AuthenticatorAttestationResponse.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                                     JSON,
                                       [NotNullWhen(true)]  out AuthenticatorAttestationResponse?  AuthenticatorAttestationResponse,
                                       [NotNullWhen(false)] out String?                            ErrorResponse)
        {

            try
            {

                AuthenticatorAttestationResponse = null;

                // {
                //     "clientDataJSON":    "eyJ0eXBlIjoid2ViYXV0aG4uY3JlYXRlIiwiY2hhbGxlbmdlIjoicU1zbU84WVpJZVBqMjBELU1QSVJfVVR0dHMtTTN3cUctWEtFZ1h4bmFucyIsIm9yaWdpbiI6Imh0dHA6Ly9sb2NhbGhvc3Q6OTAwMCIsImNyb3NzT3JpZ2luIjpmYWxzZX0=",
                //     "attestationObject": "o2NmbXRkbm9uZWdhdHRTdG10oGhhdXRoRGF0YVikSZYN5YgOjGh0NBcPZHZgW4/krrmihjLHmVzzuoMdl2NFAAAAAAiYcFjK3EuBtuEw3lDcvpYAIHLYhSZ224oCzezMt1Q8r5mVgKCdEOk8kAHdKBonl400pQECAyYgASFYIA7O5OEOUJ0qVZ10C1es7irN1gOtY8pH1NqKoSrWl3bUIlggP8OlK5dOLp9XUOZsNFnTeQ5ZZLYLVrLhot2QTc2ecPs="
                // }

                #region Parse ClientDataJSON          [mandatory]

                if (!JSON.ParseMandatoryText("clientDataJSON",
                                             "clientDataJSON",
                                             out String? clientDataJSON,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse AttestationObject       [mandatory]

                if (!JSON.ParseMandatoryText("attestationObject",
                                             "attestationObject",
                                             out String? attestationObject,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion


                AuthenticatorAttestationResponse = new AuthenticatorAttestationResponse(
                                                       clientDataJSON,
                                                       attestationObject
                                                   );

                return true;

            }
            catch (Exception e)
            {
                AuthenticatorAttestationResponse = null;
                ErrorResponse                    = "The given JSON representation of an AuthenticatorAttestationResponse is invalid: " + e.Message;
                return false;
            }

        }

        #endregion


        public JObject ToJSON()

            => new (
                   new JProperty("clientDataJSON",     ClientDataJSON),
                   new JProperty("attestationObject",  AttestationObject)
               );

    }

}
