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

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The unique identification of a HTTP Event Source.
    /// </summary>
    public readonly struct HTTPEventSource_Id : IId,
                                                IEquatable<HTTPEventSource_Id>,
                                                IComparable<HTTPEventSource_Id>
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
        public Boolean  IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean  IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the HTTP Event Source identification.
        /// </summary>
        public UInt64   Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP Event Source identification based on the given string.
        /// </summary>
        /// <param name="String">The string representation of the HTTP Event Source identification.</param>
        private HTTPEventSource_Id(String  String)
        {
            this.InternalId  = String;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a HTTP Event Source identification.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Event Source identification.</param>
        public static HTTPEventSource_Id Parse(String Text)
        {

            if (TryParse(Text, out var httpEventSourceId))
                return httpEventSourceId;

            throw new ArgumentException($"Invalid text representation of a HTTP Event Source identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a HTTP Event Source identification.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Event Source identification.</param>
        public static HTTPEventSource_Id? TryParse(String Text)
        {

            if (TryParse(Text, out var httpEventSourceId))
                return httpEventSourceId;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out HTTPEventSourceId)

        /// <summary>
        /// Try to parse the given string as a HTTP Event Source identification.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId">The parsed HTTP Event Source identification.</param>
        public static Boolean TryParse(String                                      Text,
                                       [NotNullWhen(true)] out HTTPEventSource_Id  HTTPEventSourceId)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    HTTPEventSourceId = new HTTPEventSource_Id(Text);
                    return true;
                }
                catch
                { }
            }

            HTTPEventSourceId = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this HTTP Event Source identification.
        /// </summary>
        public HTTPEventSource_Id Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">An HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPEventSource_Id HTTPEventSourceId1,
                                           HTTPEventSource_Id HTTPEventSourceId2)

            => HTTPEventSourceId1.Equals(HTTPEventSourceId2);

        #endregion

        #region Operator != (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">An HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPEventSource_Id HTTPEventSourceId1,
                                           HTTPEventSource_Id HTTPEventSourceId2)

            => !HTTPEventSourceId1.Equals(HTTPEventSourceId2);

        #endregion

        #region Operator <  (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">An HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPEventSource_Id HTTPEventSourceId1,
                                          HTTPEventSource_Id HTTPEventSourceId2)

            => HTTPEventSourceId1.CompareTo(HTTPEventSourceId2) < 0;

        #endregion

        #region Operator <= (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">An HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPEventSource_Id HTTPEventSourceId1,
                                           HTTPEventSource_Id HTTPEventSourceId2)

            => HTTPEventSourceId1.CompareTo(HTTPEventSourceId2) <= 0;

        #endregion

        #region Operator >  (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">An HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPEventSource_Id HTTPEventSourceId1,
                                          HTTPEventSource_Id HTTPEventSourceId2)

            => HTTPEventSourceId1.CompareTo(HTTPEventSourceId2) > 0;

        #endregion

        #region Operator >= (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">An HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPEventSource_Id HTTPEventSourceId1,
                                           HTTPEventSource_Id HTTPEventSourceId2)

            => HTTPEventSourceId1.CompareTo(HTTPEventSourceId2) >= 0;

        #endregion

        #endregion

        #region IComparable<HTTPEventSourceId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP Event Source identifications.
        /// </summary>
        /// <param name="Object">An HTTP Event Source identification to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPEventSource_Id httpEventSourceId
                   ? CompareTo(httpEventSourceId)
                   : throw new ArgumentException("The given object is not a HTTP Event Source identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPEventSourceId)

        /// <summary>
        /// Compares two HTTP Event Source identifications.
        /// </summary>
        /// <param name="HTTPEventSourceId">An HTTP Event Source identification to compare with.</param>
        public Int32 CompareTo(HTTPEventSource_Id HTTPEventSourceId)

            => String.Compare(InternalId,
                              HTTPEventSourceId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<HTTPEventSourceId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP Event Source identifications for equality.
        /// </summary>
        /// <param name="Object">An HTTP Event Source identification to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPEventSource_Id httpEventSourceId &&
                   Equals(httpEventSourceId);

        #endregion

        #region Equals(HTTPEventSourceId)

        /// <summary>
        /// Compares two HTTP Event Source identifications for equality.
        /// </summary>
        /// <param name="HTTPEventSourceId">An HTTP Event Source identification to compare with.</param>
        public Boolean Equals(HTTPEventSource_Id HTTPEventSourceId)

            => String.Equals(InternalId,
                             HTTPEventSourceId.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => InternalId?.ToLower().GetHashCode() ?? 0;

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
