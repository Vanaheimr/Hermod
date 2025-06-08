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
    /// Extension methods for file mappers.
    /// </summary>
    public static class FileMapperIdExtensions
    {

        /// <summary>
        /// Indicates whether this file mapper is null or empty.
        /// </summary>
        /// <param name="FileMapperId">A file mapper.</param>
        public static Boolean IsNullOrEmpty(this FileMapper_Id? FileMapperId)
            => !FileMapperId.HasValue || FileMapperId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this file mapper is null or empty.
        /// </summary>
        /// <param name="FileMapperId">A file mapper.</param>
        public static Boolean IsNotNullOrEmpty(this FileMapper_Id? FileMapperId)
            => FileMapperId.HasValue && FileMapperId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of a file mapper.
    /// </summary>
    public readonly struct FileMapper_Id : IId,
                                           IEquatable<FileMapper_Id>,
                                           IComparable<FileMapper_Id>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String  InternalId;

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
        /// The length of the file mapper identification.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique file mapper identification based on the given text representation.
        /// </summary>
        /// <param name="String">The text representation of the file mapper identification.</param>
        private FileMapper_Id(String String)
        {
            this.InternalId = String;
        }

        #endregion


        #region (static) Random  (Length = 40)

        /// <summary>
        /// Generate a new random file mapper identification.
        /// </summary>
        /// <param name="Length">The expected length of the random string.</param>
        public static FileMapper_Id Random(UInt16 Length   = 40)

            => new (RandomExtensions.RandomString(Length));

        #endregion

        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text representation of a file mapper identification.
        /// </summary>
        /// <param name="Text">A text representation of a file mapper identification.</param>
        public static FileMapper_Id Parse(String Text)
        {

            if (TryParse(Text, out FileMapper_Id securityTokenId))
                return securityTokenId;

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a file mapper identification must not be null or empty!");

            throw new ArgumentException("The given text representation of a file mapper identification is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text representation of a file mapper identification.
        /// </summary>
        /// <param name="Text">A text representation of a file mapper identification.</param>
        public static FileMapper_Id? TryParse(String Text)
        {

            if (TryParse(Text, out FileMapper_Id securityTokenId))
                return securityTokenId;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out FileMapperId)

        /// <summary>
        /// Try to parse the given text representation of a file mapper identification.
        /// </summary>
        /// <param name="Text">A text representation of a file mapper identification.</param>
        /// <param name="FileMapperId">The parsed file mapper identification.</param>
        public static Boolean TryParse(String Text, out FileMapper_Id FileMapperId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    FileMapperId = new FileMapper_Id(Text);
                    return true;
                }
                catch
                { }
            }

            FileMapperId = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this file mapper identification.
        /// </summary>
        public FileMapper_Id Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (FileMapperId1, FileMapperId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FileMapperId1">A file mapper identification.</param>
        /// <param name="FileMapperId2">Another file mapper identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (FileMapper_Id FileMapperId1,
                                           FileMapper_Id FileMapperId2)

            => FileMapperId1.Equals(FileMapperId2);

        #endregion

        #region Operator != (FileMapperId1, FileMapperId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FileMapperId1">A file mapper identification.</param>
        /// <param name="FileMapperId2">Another file mapper identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (FileMapper_Id FileMapperId1,
                                           FileMapper_Id FileMapperId2)

            => !FileMapperId1.Equals(FileMapperId2);

        #endregion

        #region Operator <  (FileMapperId1, FileMapperId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FileMapperId1">A file mapper identification.</param>
        /// <param name="FileMapperId2">Another file mapper identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (FileMapper_Id FileMapperId1,
                                          FileMapper_Id FileMapperId2)

            => FileMapperId1.CompareTo(FileMapperId2) < 0;

        #endregion

        #region Operator <= (FileMapperId1, FileMapperId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FileMapperId1">A file mapper identification.</param>
        /// <param name="FileMapperId2">Another file mapper identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (FileMapper_Id FileMapperId1,
                                           FileMapper_Id FileMapperId2)

            => FileMapperId1.CompareTo(FileMapperId2) <= 0;

        #endregion

        #region Operator >  (FileMapperId1, FileMapperId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FileMapperId1">A file mapper identification.</param>
        /// <param name="FileMapperId2">Another file mapper identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (FileMapper_Id FileMapperId1,
                                          FileMapper_Id FileMapperId2)

            => FileMapperId1.CompareTo(FileMapperId2) > 0;

        #endregion

        #region Operator >= (FileMapperId1, FileMapperId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FileMapperId1">A file mapper identification.</param>
        /// <param name="FileMapperId2">Another file mapper identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (FileMapper_Id FileMapperId1,
                                           FileMapper_Id FileMapperId2)

            => FileMapperId1.CompareTo(FileMapperId2) >= 0;

        #endregion

        #endregion

        #region IComparable<FileMapper_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is FileMapper_Id securityTokenId
                   ? CompareTo(securityTokenId)
                   : throw new ArgumentException("The given object is not a file mapper identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(FileMapper_Id)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FileMapper_Id">An object to compare with.</param>
        public Int32 CompareTo(FileMapper_Id FileMapper_Id)

            => String.Compare(InternalId,
                              FileMapper_Id.InternalId,
                              StringComparison.Ordinal);

        #endregion

        #endregion

        #region IEquatable<FileMapper_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is FileMapper_Id securityTokenId &&
                   Equals(securityTokenId);

        #endregion

        #region Equals(FileMapper_Id)

        /// <summary>
        /// Compares two file mapper identifications for equality.
        /// </summary>
        /// <param name="FileMapper_Id">An file mapper identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(FileMapper_Id FileMapper_Id)

            => String.Equals(InternalId,
                             FileMapper_Id.InternalId,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
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
