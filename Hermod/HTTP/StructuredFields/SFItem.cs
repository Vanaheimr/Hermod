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
    /// A bare item with its parameters (RFC 9651, Section 3.3):
    ///
    ///    sf-item = bare-item parameters
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9651.html#section-3.3"/>
    public sealed class SFItem : SFMember
    {

        #region Properties

        /// <summary>
        /// The bare item.
        /// </summary>
        public SFBareItem Value { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new item.
        /// </summary>
        /// <param name="Value">The bare item.</param>
        /// <param name="Parameters">Its parameters, if any.</param>
        public SFItem(SFBareItem     Value,
                      SFParameters?  Parameters   = null)

            : base(Parameters)

        {
            this.Value = Value;
        }

        #endregion


        #region ToString()

        /// <summary>
        /// Serialize this item (RFC 9651, Section 4.1.3).
        /// </summary>
        public override String ToString()

            => $"{Value}{Parameters}";

        #endregion

    }

}
