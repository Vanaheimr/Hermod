/*
 * Copyright (c) 2014-2023 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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

using System;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The unique identification of a tag.
    /// </summary>
    public struct Tag_Id : IId,
                           IEquatable<Tag_Id>,
                           IComparable<Tag_Id>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

        //ToDo: Replace with better randomness!
        private static readonly Random _Random = new Random();

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the tag identification.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new tag identification based on the given string.
        /// </summary>
        /// <param name="String">The string representation of the tag identification.</param>
        private Tag_Id(String String)
        {
            InternalId = String;
        }

        #endregion


        #region (static) Parse(Text)

        /// <summary>
        /// Parse the given string as a tag identification.
        /// </summary>
        /// <param name="Text">A text representation of a tag identification.</param>
        public static Tag_Id Parse(String Text)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a tag identification must not be null or empty!");

            #endregion

            return new Tag_Id(Text);

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a tag identification.
        /// </summary>
        /// <param name="Text">A text representation of a tag identification.</param>
        public static Tag_Id? TryParse(String Text)
        {

            Tag_Id _TagId;

            if (TryParse(Text, out _TagId))
                return _TagId;

            return new Tag_Id?();

        }

        #endregion

        #region (static) TryParse(Text, out TagId)

        /// <summary>
        /// Try to parse the given string as a tag identification.
        /// </summary>
        /// <param name="Text">A text representation of a tag identification.</param>
        /// <param name="TagId">The parsed tag identification.</param>
        public static Boolean TryParse(String Text, out Tag_Id TagId)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a tag identification must not be null or empty!");

            #endregion

            try
            {
                TagId = new Tag_Id(Text);
                return true;
            }
            catch
            {
                TagId = default(Tag_Id);
                return false;
            }

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone a tag identification.
        /// </summary>

        public Tag_Id Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (TagId1, TagId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TagId1">A tag identification.</param>
        /// <param name="TagId2">Another tag identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (Tag_Id TagId1, Tag_Id TagId2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(TagId1, TagId2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) TagId1 == null) || ((Object) TagId2 == null))
                return false;

            return TagId1.Equals(TagId2);

        }

        #endregion

        #region Operator != (TagId1, TagId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TagId1">A tag identification.</param>
        /// <param name="TagId2">Another tag identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (Tag_Id TagId1, Tag_Id TagId2)
            => !(TagId1 == TagId2);

        #endregion

        #region Operator <  (TagId1, TagId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TagId1">A tag identification.</param>
        /// <param name="TagId2">Another tag identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (Tag_Id TagId1, Tag_Id TagId2)
        {

            if ((Object) TagId1 == null)
                throw new ArgumentNullException(nameof(TagId1), "The given TagId1 must not be null!");

            return TagId1.CompareTo(TagId2) < 0;

        }

        #endregion

        #region Operator <= (TagId1, TagId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TagId1">A tag identification.</param>
        /// <param name="TagId2">Another tag identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (Tag_Id TagId1, Tag_Id TagId2)
            => !(TagId1 > TagId2);

        #endregion

        #region Operator >  (TagId1, TagId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TagId1">A tag identification.</param>
        /// <param name="TagId2">Another tag identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (Tag_Id TagId1, Tag_Id TagId2)
        {

            if ((Object) TagId1 == null)
                throw new ArgumentNullException(nameof(TagId1), "The given TagId1 must not be null!");

            return TagId1.CompareTo(TagId2) > 0;

        }

        #endregion

        #region Operator >= (TagId1, TagId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TagId1">A tag identification.</param>
        /// <param name="TagId2">Another tag identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (Tag_Id TagId1, Tag_Id TagId2)
            => !(TagId1 < TagId2);

        #endregion

        #endregion

        #region IComparable<TagId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            if (!(Object is Tag_Id))
                throw new ArgumentException("The given object is not a tag identification!",
                                            nameof(Object));

            return CompareTo((Tag_Id) Object);

        }

        #endregion

        #region CompareTo(TagId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TagId">An object to compare with.</param>
        public Int32 CompareTo(Tag_Id TagId)
        {

            if ((Object) TagId == null)
                throw new ArgumentNullException(nameof(TagId),  "The given tag identification must not be null!");

            return String.Compare(InternalId, TagId.InternalId, StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<TagId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            if (!(Object is Tag_Id))
                return false;

            return Equals((Tag_Id) Object);

        }

        #endregion

        #region Equals(TagId)

        /// <summary>
        /// Compares two tag identifications for equality.
        /// </summary>
        /// <param name="TagId">An tag identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(Tag_Id TagId)
        {

            if ((Object) TagId == null)
                return false;

            return InternalId.Equals(TagId.InternalId);

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
            => InternalId.GetHashCode();

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
            => InternalId;

        #endregion

    }

}
