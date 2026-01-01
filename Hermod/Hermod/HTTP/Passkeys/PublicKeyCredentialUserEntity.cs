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

    // https://w3c.github.io/webauthn/#dictdef-publickeycredentialuserentity

    /// <summary>
    /// Informationen über den Benutzer, der den Passkey registriert
    /// </summary>
    public class PublicKeyCredentialUserEntity(Byte[]  Id,
                                               String  Name,
                                               String  DisplayName)

        : PublicKeyCredentialEntity(Name)

    {

        /// <summary>
        /// Eindeutige ID als Byte-Array
        /// </summary>
        public Byte[]  Id             { get; } = Id;

        /// <summary>
        /// Anzeigename (vollständiger Name, etc.)
        /// </summary>
        public String  DisplayName    { get; } = DisplayName;


        public JObject ToJSON()

            => new (
                   new JProperty("id",           Convert.ToBase64String(Id)),
                   new JProperty("name",         Name),
                   new JProperty("displayName",  DisplayName)
               );


    }

}
