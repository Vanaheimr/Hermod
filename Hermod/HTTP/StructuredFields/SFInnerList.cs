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

    using org.GraphDefined.Vanaheimr.Illias;

    /// <summary>
    /// An array of items, standing where a single item could stand
    /// (RFC 9651, Section 3.1.1):
    ///
    ///    inner-list = "(" *SP [ sf-item *( 1*SP sf-item ) *SP ] ")" parameters
    ///
    /// The items inside carry their own parameters, and so does the list.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9651.html#section-3.1.1"/>
    public sealed class SFInnerList : SFMember
    {

        #region Properties

        /// <summary>
        /// The items of this inner list.
        /// </summary>
        public IReadOnlyList<SFItem> Items { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new inner list.
        /// </summary>
        /// <param name="Items">The items.</param>
        /// <param name="Parameters">The parameters of the list itself.</param>
        public SFInnerList(IEnumerable<SFItem>  Items,
                           SFParameters?        Parameters   = null)

            : base(Parameters)

        {
            this.Items = [.. Items];
        }

        #endregion


        #region ToString()

        /// <summary>
        /// Serialize this inner list (RFC 9651, Section 4.1.1.1).
        /// </summary>
        public override String ToString()

            => $"({Items.Select(item => item.ToString()).AggregateWith(" ")}){Parameters}";

        #endregion

    }

}
