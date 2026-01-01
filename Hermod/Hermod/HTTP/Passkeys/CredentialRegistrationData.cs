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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    public class CredentialRegistrationData(String                            Id,
                                            String                            RawId,
                                            String                            Type,
                                            AuthenticatorAttestationResponse  Response)
    {

        public String                            Id          { get; } = Id;
        public String                            RawId       { get; } = RawId;
        public String                            Type        { get; } = Type;
        public AuthenticatorAttestationResponse  Response    { get; } = Response;


        // {
        //     "id":    "ctiFJnbbigLN7My3VDyvmZWAoJ0Q6TyQAd0oGieXjTQ",
        //     "rawId": "ctiFJnbbigLN7My3VDyvmZWAoJ0Q6TyQAd0oGieXjTQ=",
        //     "type":  "public-key",
        //     "response": {
        //         "clientDataJSON":    "eyJ0eXBlIjoid2ViYXV0aG4uY3JlYXRlIiwiY2hhbGxlbmdlIjoicU1zbU84WVpJZVBqMjBELU1QSVJfVVR0dHMtTTN3cUctWEtFZ1h4bmFucyIsIm9yaWdpbiI6Imh0dHA6Ly9sb2NhbGhvc3Q6OTAwMCIsImNyb3NzT3JpZ2luIjpmYWxzZX0=",
        //         "attestationObject": "o2NmbXRkbm9uZWdhdHRTdG10oGhhdXRoRGF0YVikSZYN5YgOjGh0NBcPZHZgW4/krrmihjLHmVzzuoMdl2NFAAAAAAiYcFjK3EuBtuEw3lDcvpYAIHLYhSZ224oCzezMt1Q8r5mVgKCdEOk8kAHdKBonl400pQECAyYgASFYIA7O5OEOUJ0qVZ10C1es7irN1gOtY8pH1NqKoSrWl3bUIlggP8OlK5dOLp9XUOZsNFnTeQ5ZZLYLVrLhot2QTc2ecPs="
        //     }
        // }


        #region (static) TryParse(JSON, out CredentialRegistrationData, out ErrorResponse)

        /// <summary>
        /// Try to parse the given JSON representation of CredentialRegistrationData.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CredentialRegistrationData">The parsed CredentialRegistrationData.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                               JSON,
                                       [NotNullWhen(true)]  out CredentialRegistrationData?  CredentialRegistrationData,
                                       [NotNullWhen(false)] out String?                      ErrorResponse)
        {

            try
            {

                CredentialRegistrationData = null;

                #region Parse Id          [mandatory]

                if (!JSON.ParseMandatoryText("id",
                                             "identification",
                                             out String? id,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse RawId       [mandatory]

                if (!JSON.ParseMandatoryText("rawId",
                                             "raw identification",
                                             out String? rawId,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Type        [mandatory]

                if (!JSON.ParseMandatoryText("type",
                                             "type",
                                             out String? type,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Response    [mandatory]

                if (!JSON.ParseMandatoryJSON("response",
                                             "authenticator attestation response",
                                             AuthenticatorAttestationResponse.TryParse,
                                             out AuthenticatorAttestationResponse? response,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion


                CredentialRegistrationData = new CredentialRegistrationData(
                                                 id,
                                                 rawId,
                                                 type,
                                                 response
                                             );

                return true;

            }
            catch (Exception e)
            {
                CredentialRegistrationData  = null;
                ErrorResponse               = "The given JSON representation of CredentialRegistrationData is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        public JObject ToJSON()

            => new (
                   new JProperty("id",        Id),
                   new JProperty("rawId",     RawId),
                   new JProperty("type",      Type),
                   new JProperty("response",  Response.ToJSON())
               );

    }

}
