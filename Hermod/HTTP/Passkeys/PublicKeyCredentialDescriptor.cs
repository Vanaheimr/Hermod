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
    /// A reference to an existing credential, used within excludeCredentials
    /// and allowCredentials.
    /// https://w3c.github.io/webauthn/#dictionary-credential-descriptor
    /// </summary>
    public class PublicKeyCredentialDescriptor(Byte[]                                Id,
                                               PublicKeyCredentialType               Type,
                                               IEnumerable<AuthenticatorTransport>?  Transports   = null)
    {

        #region Properties

        public Byte[]                               Id            { get; } = Id;
        public PublicKeyCredentialType              Type          { get; } = Type;
        public IEnumerable<AuthenticatorTransport>  Transports    { get; } = Transports?.Distinct() ?? [];

        #endregion

        #region ToJSON()

        public JObject ToJSON()

            => JSONObject.Create(

                         new JProperty("id",          Base64Url.EncodeToString(Id)),
                         new JProperty("type",        Type.ToString()),

                   Transports.Any()
                       ? new JProperty("transports",  new JArray(Transports.Select(transport => transport.ToString())))
                       : null

               );

        #endregion

    }

}
