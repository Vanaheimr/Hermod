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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    // https://w3c.github.io/webauthn/#dictdef-publickeycredentialrequestoptions

    /// <summary>
    /// Options for creating a new PublicKeyCredential.
    /// </summary>
    /// <param name="Challenge">The challenge that will be used for signing.</param>
    /// <param name="RelyingPartyId">An optional relying party identification (e.g. the domainname).</param>
    /// <param name="UserVerification"></param>
    /// <param name="Timeout">An optional timeout for the operation.</param>
    /// <param name="AllowCredentials">An optional enumeration of allowed credentials when the user is already known.</param>
    /// <param name="Hints"></param>
    /// <param name="Extensions"></param>
    public class PublicKeyCredentialRequestOptions(Byte[]                                       Challenge,
                                                   String?                                      RelyingPartyId     = null,
                                                   UserVerificationRequirement?                 UserVerification   = null,
                                                   TimeSpan?                                    Timeout            = null,
                                                   IEnumerable<PublicKeyCredentialDescriptor>?  AllowCredentials   = null,
                                                   IEnumerable<PublicKeyCredentialHint>?        Hints              = null,
                                                   IEnumerable<String>?                         Extensions         = null)
    {

        public Byte[]                                      Challenge           { get; } = Challenge;
        public String?                                     RelyingPartyId      { get; } = RelyingPartyId;
        public UserVerificationRequirement?                UserVerification    { get; } = UserVerification;
        public TimeSpan?                                   Timeout             { get; } = Timeout;
        public IEnumerable<PublicKeyCredentialDescriptor>  AllowCredentials    { get; } = AllowCredentials?.Distinct() ?? [];
        public IEnumerable<PublicKeyCredentialHint>        Hints               { get; } = Hints?.           Distinct() ?? [];
        public IEnumerable<String>                         Extensions          { get; } = Extensions?.      Distinct() ?? [];


        public JObject ToJSON()

            => JSONObject.Create(

                         new JProperty("challenge",          Challenge.ToBase64()),

                   RelyingPartyId.IsNotNullOrEmpty()
                       ? new JProperty("rpId",               RelyingPartyId)
                       : null,

                   UserVerification.IsNotNullOrEmpty()
                       ? new JProperty("userVerification",   UserVerification)
                       : null,

                   Timeout.HasValue
                       ? new JProperty("timeout",            (UInt32) Timeout.Value.TotalMilliseconds)
                       : null,

                   AllowCredentials.Any()
                       ? new JProperty("allowCredentials",   new JArray(AllowCredentials.Select(publicKeyCredentialDescriptor => publicKeyCredentialDescriptor.ToJSON())))
                       : null,

                   Hints.           Any()
                       ? new JProperty("hints",              new JArray(Hints.           Select(hint                          => hint.                         ToString())))
                       : null,

                   Extensions.      Any()
                       ? new JProperty("extensions",         new JArray(Extensions.      Select(extension                     => extension.                    ToString())))
                       : null

               );

    }

}
