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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    /// <summary>
    /// The user a credential is created for. The id is the user handle: an
    /// opaque identifier the authenticator returns with every assertion,
    /// never the e-mail address.
    /// https://w3c.github.io/webauthn/#dictdef-publickeycredentialuserentity
    /// </summary>
    public class PublicKeyCredentialUserEntity(Byte[]  Id,
                                               String  Name,
                                               String  DisplayName)

        : PublicKeyCredentialEntity(Name)

    {

        #region Properties

        public Byte[]  Id             { get; } = Id;
        public String  DisplayName    { get; } = DisplayName;

        #endregion

        #region ToJSON()

        public JObject ToJSON()

            => new (
                   new JProperty("id",           Base64Url.EncodeToString(Id)),
                   new JProperty("name",         Name),
                   new JProperty("displayName",  DisplayName)
               );

        #endregion

    }

}
