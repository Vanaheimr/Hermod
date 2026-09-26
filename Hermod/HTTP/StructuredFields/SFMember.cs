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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// What may stand in a list, or on the right-hand side of a dictionary
    /// entry (RFC 9651, Sections 3.1 and 3.2): an <see cref="SFItem"/> or an
    /// <see cref="SFInnerList"/>. Both carry parameters of their own.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9651.html#section-3.1"/>
    public abstract class SFMember
    {

        /// <summary>
        /// The parameters of this member.
        /// </summary>
        public SFParameters Parameters { get; }

        /// <summary>
        /// Create a new member.
        /// </summary>
        protected SFMember(SFParameters? Parameters = null)
        {
            this.Parameters = Parameters ?? SFParameters.Empty;
        }

    }

}
