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

using System.Buffers.Text;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    /// <summary>
    /// The options for navigator.credentials.get(), serialized as the
    /// WebAuthn Level 3 JSON that PublicKeyCredential.parseRequestOptionsFromJSON()
    /// reads: binary fields are Base64URL without padding. Without
    /// allowCredentials the authenticator offers its discoverable credentials.
    /// https://w3c.github.io/webauthn/#dictdef-publickeycredentialrequestoptions
    /// </summary>
    public class PublicKeyCredentialRequestOptions(Byte[]                                       Challenge,
                                                   String?                                      RelyingPartyId     = null,
                                                   UserVerificationRequirement?                 UserVerification   = null,
                                                   TimeSpan?                                    Timeout            = null,
                                                   IEnumerable<PublicKeyCredentialDescriptor>?  AllowCredentials   = null,
                                                   IEnumerable<PublicKeyCredentialHint>?        Hints              = null,
                                                   AuthenticationExtensions?                    Extensions         = null)
    {

        #region Properties

        public Byte[]                                      Challenge           { get; } = Challenge;
        public String?                                     RelyingPartyId      { get; } = RelyingPartyId;
        public UserVerificationRequirement?                UserVerification    { get; } = UserVerification;
        public TimeSpan?                                   Timeout             { get; } = Timeout;
        public IEnumerable<PublicKeyCredentialDescriptor>  AllowCredentials    { get; } = AllowCredentials?.Distinct() ?? [];
        public IEnumerable<PublicKeyCredentialHint>        Hints               { get; } = Hints?.           Distinct() ?? [];
        public AuthenticationExtensions?                   Extensions          { get; } = Extensions;

        #endregion

        #region ToJSON()

        public JObject ToJSON()

            => JSONObject.Create(

                         new JProperty("challenge",          Base64Url.EncodeToString(Challenge)),

                   RelyingPartyId.IsNotNullOrEmpty()
                       ? new JProperty("rpId",               RelyingPartyId)
                       : null,

                   UserVerification.HasValue
                       ? new JProperty("userVerification",   UserVerification.Value.ToString())
                       : null,

                   Timeout.HasValue
                       ? new JProperty("timeout",            (UInt32) Timeout.Value.TotalMilliseconds)
                       : null,

                   AllowCredentials.Any()
                       ? new JProperty("allowCredentials",   new JArray(AllowCredentials.Select(descriptor => descriptor.ToJSON())))
                       : null,

                   Hints.Any()
                       ? new JProperty("hints",              new JArray(Hints.Select(hint => hint.ToString())))
                       : null,

                   Extensions is { Count: > 0 }
                       ? new JProperty("extensions",         Extensions.ToJSON())
                       : null

               );

        #endregion

    }

}
