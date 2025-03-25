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

    // https://w3c.github.io/webauthn/#webauthn-extensions


    /// <summary>
    /// An Authentication Extension
    /// </summary>
    public class AuthenticationExtension(String                      Name,
                                         Dictionary<String, Object>  Map)
    {

        /// <summary>
        /// An "entry key" identifying the extension.
        /// </summary>
        public String                      Name    { get; } = Name;

        /// <summary>
        /// Parameters of the extension.
        /// </summary>
        public Dictionary<String, Object>  Map     { get; } = Map;


        public JObject ToJSON()

            => JSONObject.Create(
                   Map.Select(extEntry => new JProperty(
                                              extEntry.Key,
                                              extEntry.Value
                                          ))
               );

    }

}
