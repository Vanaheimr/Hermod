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


    // Liste der unterstützten kryptografischen Algorithmen
    public class PublicKeyCredentialParameters(PublicKeyCredentialType   Type,
                                               COSEAlgorithmIdentifiers  Alg)
    {

        /// <summary>
        /// Meist "public-key"
        /// </summary>
        public PublicKeyCredentialType   Type    { get; } = Type;

        /// <summary>
        /// COSE-Algorithmus-ID, z.B. -7 für ES256
        /// </summary>
        public COSEAlgorithmIdentifiers  Alg     { get; } = Alg;


        public JObject ToJSON()

            => new (
                   new JProperty("type",  Type.ToString()),
                   new JProperty("alg",   Alg. ToString())
               );

    }

}
