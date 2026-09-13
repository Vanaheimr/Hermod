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

using System.Collections;

using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    /// <summary>
    /// The client extension inputs of a ceremony, serialized as one JSON
    /// object with the extension identifiers as keys.
    /// https://w3c.github.io/webauthn/#webauthn-extensions
    /// </summary>
    public class AuthenticationExtensions(IEnumerable<AuthenticationExtension> Extensions) : IEnumerable<AuthenticationExtension>
    {

        #region Data

        private readonly List<AuthenticationExtension> extensions = Extensions.ToList();

        #endregion

        #region Properties

        public Int32  Count
            => extensions.Count;

        #endregion

        #region Constructor(s)

        public AuthenticationExtensions(params AuthenticationExtension[] Extensions)
            : this((IEnumerable<AuthenticationExtension>) Extensions)
        { }

        #endregion

        #region ToJSON()

        public JObject ToJSON()

            => new (extensions.Select(extension => new JProperty(extension.Name, extension.Value)));

        #endregion

        #region IEnumerable<AuthenticationExtension> Members

        public IEnumerator<AuthenticationExtension> GetEnumerator()
            => extensions.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => extensions.GetEnumerator();

        #endregion

    }

}
