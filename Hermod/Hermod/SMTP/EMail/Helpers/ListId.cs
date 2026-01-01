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

using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A unique mailing list identification.
    /// </summary>
    public class ListId : IEquatable<ListId>,
                          IComparable<ListId>,
                          IComparable
    {

        #region Public constants

        /// <summary>
        /// A regular expression to validate a List-Id e-mail header field.
        /// Mailinglist &lt;list@example.org&gt;
        /// </summary>
        public static readonly Regex ListIdRegularExpression = new ("^([^<]+)<([^>]+)>$");

        #endregion

        #region Properties

        /// <summary>
        /// The name of the mailing list.
        /// </summary>
        public String  Name              { get; }

        /// <summary>
        /// The unique identification of the mailing list.
        /// </summary>
        public String  Identification    { get; }


        /// <summary>
        /// The string value of a mailing list identification.
        /// </summary>
        public String  Value
            => $"{Name} <{Identification}>";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique mailing list identification.
        /// </summary>
        /// <param name="Name">The name of the mailing list.</param>
        /// <param name="Identification">The unique identification of the mailing list.</param>
        private ListId(String  Name,
                       String  Identification)
        {

            this.Name           = Name;
            this.Identification = Identification;

        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse a mailing list identification from a string.
        /// </summary>
        /// <param name="Text">A string representation of a mailing list identification.</param>
        public static ListId? Parse(String Text)
        {

            if (TryParse(Text, out var listId))
                return listId;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out ListId)

        /// <summary>
        /// Try to parse a mailing list identification from a string.
        /// </summary>
        /// <param name="Text">A string representation of a mailing list identification.</param>
        /// <param name="ListId">The parsed e-mail message identification.</param>
        public static Boolean TryParse(String                           Text,
                                       [NotNullWhen(true)] out ListId?  ListId)
        {

            var r = SimpleEMailAddress.SimpleEMail_RegEx.Match(Text.Trim());

            if (r.Success)
            {

                ListId = new ListId(
                             r.Groups[0].Value,
                             r.Groups[1].Value
                         );

                return true;

            }

            ListId = null;
            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (ListId ListId1,
                                           ListId ListId2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(ListId1, ListId2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) ListId1 is null) || ((Object) ListId2 is null))
                return false;

            return ListId1.Equals(ListId2);

        }

        #endregion

        #region Operator != (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (ListId ListId1, ListId ListId2)
        {
            return !(ListId1 == ListId2);
        }

        #endregion

        #region Operator <  (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (ListId ListId1, ListId ListId2)
        {
            return ListId1.Value.CompareTo(ListId2.Value) < 0;
        }

        #endregion

        #region Operator <= (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (ListId ListId1, ListId ListId2)
        {
            return !(ListId1 > ListId2);
        }

        #endregion

        #region Operator >  (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >(ListId ListId1, ListId ListId2)
        {
            return ListId1.Value.CompareTo(ListId2.Value) > 0;
        }

        #endregion

        #region Operator >= (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (ListId ListId1, ListId ListId2)
        {
            return !(ListId1 < ListId2);
        }

        #endregion

        #endregion

        #region IComparable<ListId> Member

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">A mailing list identification to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is ListId listId
                   ? CompareTo(listId)
                   : throw new ArgumentException("The given object is not a mailing list identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(ListId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId">A mailing list identification to compare with.</param>
        public Int32 CompareTo(ListId? ListId)
        {

            if (ListId is null)
                throw new ArgumentNullException(nameof(ListId), "The given mailing list identification must not be null!");

            return Value.CompareTo(ListId.Value);

        }

        #endregion

        #endregion

        #region IEquatable<ListId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">A mailing list identification to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is ListId listId &&
                   Equals(listId);

        #endregion

        #region Equals(ListId)

        /// <summary>
        /// Compares two ListIds for equality.
        /// </summary>
        /// <param name="ListId">A mailing list identification to compare with.</param>
        public Boolean Equals(ListId? ListId)

            => ListId is not null &&

               String.Equals(Value,
                             ListId.Value,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => Value.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a formatted string representation of this object.
        /// </summary>
        /// <returns>A formatted string representation of this object.</returns>
        public override String ToString()

            => Value;

        #endregion

    }

}
