/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The unique identification of an attached file.
    /// </summary>
    public readonly struct AttachedFile_Id : IId,
                                             IEquatable<AttachedFile_Id>,
                                             IComparable<AttachedFile_Id>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

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
        /// The length of the attached file identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new attached file identification based on the given string.
        /// </summary>
        private AttachedFile_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Random(Length)

        /// <summary>
        /// Create a new attached file identification.
        /// </summary>
        /// <param name="Length">The expected length of the attached file identification.</param>
        public static AttachedFile_Id Random(Byte Length = 15)

            => new (RandomExtensions.RandomString(Length).ToUpper());

        #endregion

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as an attached file identification.
        /// </summary>
        /// <param name="Text">A text representation of an attached file identification.</param>
        public static AttachedFile_Id Parse(String Text)
        {

            if (TryParse(Text, out AttachedFile_Id attachedFileId))
                return attachedFileId;

            throw new ArgumentException($"Invalid text representation of an attached file identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an attached file identification.
        /// </summary>
        /// <param name="Text">A text representation of an attached file identification.</param>
        public static AttachedFile_Id? TryParse(String Text)
        {

            if (TryParse(Text, out AttachedFile_Id attachedFileId))
                return attachedFileId;

            return null;

        }

        #endregion

        #region TryParse(Text, out AttachedFileId)

        /// <summary>
        /// Try to parse the given string as an attached file identification.
        /// </summary>
        /// <param name="Text">A text representation of an attached file identification.</param>
        /// <param name="AttachedFileId">The parsed attached file identification.</param>
        public static Boolean TryParse(String Text, out AttachedFile_Id AttachedFileId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    AttachedFileId = new AttachedFile_Id(Text);
                    return true;
                }
                catch
                { }
            }

            AttachedFileId = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this attached file identification.
        /// </summary>
        public AttachedFile_Id Clone

            => new AttachedFile_Id(
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (AttachedFileIdId1, AttachedFileIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileIdId1">An attached file identification.</param>
        /// <param name="AttachedFileIdId2">Another attached file identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (AttachedFile_Id AttachedFileIdId1,
                                           AttachedFile_Id AttachedFileIdId2)

            => AttachedFileIdId1.Equals(AttachedFileIdId2);

        #endregion

        #region Operator != (AttachedFileIdId1, AttachedFileIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileIdId1">An attached file identification.</param>
        /// <param name="AttachedFileIdId2">Another attached file identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (AttachedFile_Id AttachedFileIdId1,
                                           AttachedFile_Id AttachedFileIdId2)

            => !AttachedFileIdId1.Equals(AttachedFileIdId2);

        #endregion

        #region Operator <  (AttachedFileIdId1, AttachedFileIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileIdId1">An attached file identification.</param>
        /// <param name="AttachedFileIdId2">Another attached file identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (AttachedFile_Id AttachedFileIdId1,
                                          AttachedFile_Id AttachedFileIdId2)

            => AttachedFileIdId1.CompareTo(AttachedFileIdId2) < 0;

        #endregion

        #region Operator <= (AttachedFileIdId1, AttachedFileIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileIdId1">An attached file identification.</param>
        /// <param name="AttachedFileIdId2">Another attached file identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (AttachedFile_Id AttachedFileIdId1,
                                           AttachedFile_Id AttachedFileIdId2)

            => AttachedFileIdId1.CompareTo(AttachedFileIdId2) <= 0;

        #endregion

        #region Operator >  (AttachedFileIdId1, AttachedFileIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileIdId1">An attached file identification.</param>
        /// <param name="AttachedFileIdId2">Another attached file identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (AttachedFile_Id AttachedFileIdId1,
                                          AttachedFile_Id AttachedFileIdId2)

            => AttachedFileIdId1.CompareTo(AttachedFileIdId2) > 0;

        #endregion

        #region Operator >= (AttachedFileIdId1, AttachedFileIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileIdId1">An attached file identification.</param>
        /// <param name="AttachedFileIdId2">Another attached file identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (AttachedFile_Id AttachedFileIdId1,
                                           AttachedFile_Id AttachedFileIdId2)

            => AttachedFileIdId1.CompareTo(AttachedFileIdId2) >= 0;

        #endregion

        #endregion

        #region IComparable<AttachedFileId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is AttachedFile_Id attachedFileId
                   ? CompareTo(attachedFileId)
                   : throw new ArgumentException("The given object is not an attached file identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(AttachedFileId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileId">An object to compare with.</param>
        public Int32 CompareTo(AttachedFile_Id AttachedFileId)

            => String.Compare(InternalId,
                              AttachedFileId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<AttachedFileId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is AttachedFile_Id attachedFileId &&
                   Equals(attachedFileId);

        #endregion

        #region Equals(AttachedFileId)

        /// <summary>
        /// Compares two AttachedFileIds for equality.
        /// </summary>
        /// <param name="AttachedFileId">An attached file identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(AttachedFile_Id AttachedFileId)

            => String.Equals(InternalId,
                             AttachedFileId.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => InternalId?.GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
