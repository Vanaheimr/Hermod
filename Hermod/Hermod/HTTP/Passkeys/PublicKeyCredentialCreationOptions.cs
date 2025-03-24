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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{


    public class PublicKeyCredentialCreationOptions(Byte[]                                Challenge,
                                                    PublicKeyCredentialRpEntity           Rp,
                                                    PublicKeyCredentialUserEntity         User,
                                                    List<PublicKeyCredentialParameters>   PubKeyCredParams,
                                                    TimeSpan                              Timeout,
                                                    String                                Attestation,
                                                    List<PublicKeyCredentialDescriptor>?  ExcludeCredentials   = null)
    {

        public Byte[]                               Challenge             { get; } = Challenge;
        public PublicKeyCredentialRpEntity          Rp                    { get; } = Rp;
        public PublicKeyCredentialUserEntity        User                  { get; } = User;
        public List<PublicKeyCredentialParameters>  PubKeyCredParams      { get; } = PubKeyCredParams;
        public TimeSpan                             Timeout               { get; } = Timeout;
        public String                               Attestation           { get; } = Attestation;
        public List<PublicKeyCredentialDescriptor>  ExcludeCredentials    { get; } = ExcludeCredentials ?? [];


        public JObject ToJSON()

            => JSONObject.Create(

                         new JProperty("challenge",            Convert.ToBase64String(Challenge)),
                         new JProperty("rp",                   Rp.  ToJSON()),
                         new JProperty("user",                 User.ToJSON()),
                         new JProperty("pubKeyCredParams",     new JArray(PubKeyCredParams.Select(xx => xx.ToJSON()))),
                         new JProperty("timeout",              (Int32) Timeout.TotalMilliseconds),
                         new JProperty("attestation",          Attestation),

                   ExcludeCredentials.Count > 0
                       ? new JProperty("excludeCredentials",   new JArray(ExcludeCredentials.Select(ec => ec.ToJSON())))
                       : null

               );


    }

}
