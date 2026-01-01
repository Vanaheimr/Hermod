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

    // https://w3c.github.io/webauthn/#dictionary-credential-descriptor

    /// <summary>
    /// Beschreibung bereits vorhandener Credentials (um Duplikate zu verhindern)
    /// </summary>
    public class PublicKeyCredentialDescriptor(Byte[]                                Id,
                                               PublicKeyCredentialType               Type,
                                               IEnumerable<AuthenticatorTransport>?  Transports   = null)
    {

        /// <summary>
        /// The unique credential identification.
        /// </summary>
        public Byte[]                               Id            { get; } = Id;

        /// <summary>
        /// The type of the credential (default: "public-key").
        /// </summary>
        public PublicKeyCredentialType              Type          { get; } = Type;

        /// <summary>
        /// Optional transports for the credential, e.g. "ble", "hybrid", "internal", "nfc", "usb", ...
        /// </summary>
        public IEnumerable<AuthenticatorTransport>  Transports    { get; } = Transports?.Distinct() ?? [];


        public JObject ToJSON()

            => new (
                   new JProperty("id",           Convert.ToBase64String(Id)),
                   new JProperty("type",         Type.ToString()),
                   new JProperty("transports",   new JArray(Transports.Select(transport => transport.ToString())))
               );

    }

}
