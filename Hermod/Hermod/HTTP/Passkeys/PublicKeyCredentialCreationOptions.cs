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

    // https://w3c.github.io/webauthn/#dictdef-publickeycredentialcreationoptions

    public class PublicKeyCredentialCreationOptions(PublicKeyCredentialRpEntity                  RelyingParty,
                                                    PublicKeyCredentialUserEntity                User,
                                                    Byte[]                                       Challenge,
                                                    IEnumerable<PublicKeyCredentialParameters>   PubKeyCredParams,

                                                    TimeSpan?                                    Timeout              = null,
                                                    IEnumerable<PublicKeyCredentialDescriptor>?  ExcludeCredentials   = null,
                                                    IEnumerable<PublicKeyCredentialHint>?        Hints                = null,
                                                    AttestationConveyancePreference?             Attestation          = null,
                                                    IEnumerable<String>?                         AttestationFormats   = null,
                                                    IEnumerable<String>?                         Extensions           = null)
    {

        public PublicKeyCredentialRpEntity                 RelyingParty          { get; } = RelyingParty;
        public PublicKeyCredentialUserEntity               User                  { get; } = User;
        public Byte[]                                      Challenge             { get; } = Challenge;
        public IEnumerable<PublicKeyCredentialParameters>  PubKeyCredParams      { get; } = PubKeyCredParams.   Distinct();

        public TimeSpan?                                   Timeout               { get; } = Timeout;
        public IEnumerable<PublicKeyCredentialDescriptor>  ExcludeCredentials    { get; } = ExcludeCredentials?.Distinct() ?? [];
        public IEnumerable<PublicKeyCredentialHint>        Hints                 { get; } = Hints?.             Distinct() ?? [];
        public AttestationConveyancePreference?            Attestation           { get; } = Attestation;
        public IEnumerable<String>                         AttestationFormats    { get; } = AttestationFormats?.Distinct() ?? [];

        public IEnumerable<String>                         Extensions            { get; } = Extensions?.        Distinct() ?? [];


        public JObject ToJSON()

            => JSONObject.Create(

                         new JProperty("rp",                   RelyingParty.  ToJSON()),
                         new JProperty("user",                 User.ToJSON()),
                         new JProperty("challenge",            Convert.ToBase64String(Challenge)),
                         new JProperty("pubKeyCredParams",     new JArray(PubKeyCredParams.Select(publicKeyCredentialParameters => publicKeyCredentialParameters.ToJSON()))),

                   Timeout.HasValue
                       ? new JProperty("timeout",              (Int32) Timeout.Value.TotalMilliseconds)
                       : null,

                   ExcludeCredentials.Any()
                       ? new JProperty("excludeCredentials",   new JArray(ExcludeCredentials.Select(publicKeyCredentialDescriptor => publicKeyCredentialDescriptor.ToJSON())))
                       : null,

                   Hints.             Any()
                       ? new JProperty("hints",                new JArray(Hints.             Select(publicKeyCredentialHint       => publicKeyCredentialHint.      ToString())))
                       : null,

                   Attestation.HasValue
                       ? new JProperty("attestation",          Attestation.Value.ToString())
                       : null

               );


    }

}
