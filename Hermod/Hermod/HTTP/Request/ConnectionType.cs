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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for connection types.
    /// </summary>
    public static class ConnectionTypeExtensions
    {

        /// <summary>
        /// Indicates whether this connection type is null or empty.
        /// </summary>
        /// <param name="ConnectionType">A connection type.</param>
        public static Boolean IsNullOrEmpty(this ConnectionType? ConnectionType)
            => !ConnectionType.HasValue || ConnectionType.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this connection type is NOT null or empty.
        /// </summary>
        /// <param name="ConnectionType">A connection type.</param>
        public static Boolean IsNotNullOrEmpty(this ConnectionType? ConnectionType)
            => ConnectionType.HasValue && ConnectionType.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A HTTP connection type.
    /// </summary>
    public readonly struct ConnectionType : IId<ConnectionType>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this connection type is null or empty.
        /// </summary>
        public Boolean  IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this connection type is NOT null or empty.
        /// </summary>
        public Boolean  IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the connection type.
        /// </summary>
        public UInt64   Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new connection type based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a connection type.</param>
        private ConnectionType(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text as a connection type.
        /// </summary>
        /// <param name="Text">A text representation of a connection type.</param>
        public static ConnectionType Parse(String Text)
        {

            if (TryParse(Text, out var connectionType))
                return connectionType;

            throw new ArgumentException($"Invalid text representation of a connection type: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a connection type.
        /// </summary>
        /// <param name="Text">A text representation of a connection type.</param>
        public static ConnectionType? TryParse(String Text)
        {

            if (TryParse(Text, out var connectionType))
                return connectionType;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out ConnectionType)

        /// <summary>
        /// Try to parse the given text as a connection type.
        /// </summary>
        /// <param name="Text">A text representation of a connection type.</param>
        /// <param name="ConnectionType">The parsed connection type.</param>
        public static Boolean TryParse(String Text, out ConnectionType ConnectionType)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    ConnectionType = new ConnectionType(Text);
                    return true;
                }
                catch
                { }
            }

            ConnectionType = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this connection type.
        /// </summary>
        public ConnectionType Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static defaults

        /// <summary>
        /// Close the connection.
        /// </summary>
        public static ConnectionType  Close        { get; }
            = Parse("close");

        /// <summary>
        /// Keep the connection alive.
        /// </summary>
        public static ConnectionType  KeepAlive    { get; }
            = Parse("keep-alive");

        /// <summary>
        /// Upgrade the connection.
        /// </summary>
        public static ConnectionType  Upgrade      { get; }
            = Parse("upgrade");

        #endregion


        #region Operator overloading

        #region Operator == (ConnectionType1, ConnectionType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionType1">A connection type.</param>
        /// <param name="ConnectionType2">Another connection type.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator == (ConnectionType ConnectionType1,
                                           ConnectionType ConnectionType2)

            => ConnectionType1.Equals(ConnectionType2);

        #endregion

        #region Operator != (ConnectionType1, ConnectionType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionType1">A connection type.</param>
        /// <param name="ConnectionType2">Another connection type.</param>
        /// <returns>False if both match; True otherwise.</returns>
        public static Boolean operator != (ConnectionType ConnectionType1,
                                           ConnectionType ConnectionType2)

            => !ConnectionType1.Equals(ConnectionType2);

        #endregion

        #region Operator <  (ConnectionType1, ConnectionType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionType1">A connection type.</param>
        /// <param name="ConnectionType2">Another connection type.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator < (ConnectionType ConnectionType1,
                                          ConnectionType ConnectionType2)

            => ConnectionType1.CompareTo(ConnectionType2) < 0;

        #endregion

        #region Operator <= (ConnectionType1, ConnectionType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionType1">A connection type.</param>
        /// <param name="ConnectionType2">Another connection type.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator <= (ConnectionType ConnectionType1,
                                           ConnectionType ConnectionType2)

            => ConnectionType1.CompareTo(ConnectionType2) <= 0;

        #endregion

        #region Operator >  (ConnectionType1, ConnectionType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionType1">A connection type.</param>
        /// <param name="ConnectionType2">Another connection type.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator > (ConnectionType ConnectionType1,
                                          ConnectionType ConnectionType2)

            => ConnectionType1.CompareTo(ConnectionType2) > 0;

        #endregion

        #region Operator >= (ConnectionType1, ConnectionType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionType1">A connection type.</param>
        /// <param name="ConnectionType2">Another connection type.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator >= (ConnectionType ConnectionType1,
                                           ConnectionType ConnectionType2)

            => ConnectionType1.CompareTo(ConnectionType2) >= 0;

        #endregion

        #endregion

        #region IComparable<ConnectionType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two connection types.
        /// </summary>
        /// <param name="Object">A connection type to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is ConnectionType connectionType
                   ? CompareTo(connectionType)
                   : throw new ArgumentException("The given object is not a connection type!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(ConnectionType)

        /// <summary>
        /// Compares two connection types.
        /// </summary>
        /// <param name="ConnectionType">A connection type to compare with.</param>
        public Int32 CompareTo(ConnectionType ConnectionType)

            => String.Compare(InternalId,
                              ConnectionType.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<ConnectionType> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two connection types for equality.
        /// </summary>
        /// <param name="Object">A connection type to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is ConnectionType connectionType &&
                   Equals(connectionType);

        #endregion

        #region Equals(ConnectionType)

        /// <summary>
        /// Compares two connection types for equality.
        /// </summary>
        /// <param name="ConnectionType">A connection type to compare with.</param>
        public Boolean Equals(ConnectionType ConnectionType)

            => String.Equals(InternalId,
                             ConnectionType.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
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
