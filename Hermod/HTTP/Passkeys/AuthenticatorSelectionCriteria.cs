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

    /// <summary>
    /// Which authenticators may create the credential and how: attachment,
    /// whether the credential must be discoverable (a passkey) and whether
    /// the user must be verified.
    /// https://w3c.github.io/webauthn/#dictdef-authenticatorselectioncriteria
    /// </summary>
    public class AuthenticatorSelectionCriteria(AuthenticatorAttachment?      Attachment           = null,
                                                ResidentKeyRequirement?       ResidentKey          = null,
                                                Boolean?                      RequireResidentKey   = null,
                                                UserVerificationRequirement?  UserVerification     = null)
    {

        #region Properties

        public AuthenticatorAttachment?      Attachment            { get; } = Attachment;
        public ResidentKeyRequirement?       ResidentKey           { get; } = ResidentKey;
        public Boolean?                      RequireResidentKey    { get; } = RequireResidentKey;
        public UserVerificationRequirement?  UserVerification      { get; } = UserVerification;

        #endregion

        #region ToJSON()

        public JObject ToJSON()

            => JSONObject.Create(

                   Attachment.HasValue
                       ? new JProperty("authenticatorAttachment",  Attachment.Value.ToString())
                       : null,

                   ResidentKey.HasValue
                       ? new JProperty("residentKey",              ResidentKey.Value.ToString())
                       : null,

                   RequireResidentKey.HasValue
                       ? new JProperty("requireResidentKey",       RequireResidentKey.Value)
                       : null,

                   UserVerification.HasValue
                       ? new JProperty("userVerification",         UserVerification.Value.ToString())
                       : null

               );

        #endregion

    }

}
