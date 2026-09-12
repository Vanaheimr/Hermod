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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    /// <summary>
    /// A credential type together with a signature algorithm the relying
    /// party accepts, listed within pubKeyCredParams in order of preference.
    /// https://w3c.github.io/webauthn/#dictdef-publickeycredentialparameters
    /// </summary>
    public class PublicKeyCredentialParameters(PublicKeyCredentialType   Type,
                                               COSEAlgorithmIdentifiers  Alg)
    {

        #region Properties

        public PublicKeyCredentialType   Type    { get; } = Type;
        public COSEAlgorithmIdentifiers  Alg     { get; } = Alg;

        #endregion

        #region ToJSON()

        public JObject ToJSON()

            => new (
                   new JProperty("type",  Type.ToString()),
                   new JProperty("alg",   (Int32) Alg)
               );

        #endregion

    }

}
