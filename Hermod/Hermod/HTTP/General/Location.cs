/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for HTTP locations.
    /// </summary>
    public static class LocationExtensions
    {

        /// <summary>
        /// Indicates whether this HTTP location is null or empty.
        /// </summary>
        /// <param name="Location">A HTTP location.</param>
        public static Boolean IsNullOrEmpty(this Location? Location)
            => !Location.HasValue || Location.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this HTTP location is null or empty.
        /// </summary>
        /// <param name="Location">A HTTP location.</param>
        public static Boolean IsNotNullOrEmpty(this Location? Location)
            => Location.HasValue && Location.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A absolute or relative HTTP Location.
    /// </summary>
    public readonly struct Location
    {

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => URL.IsNullOrEmpty() && Path.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => URL.IsNotNullOrEmpty() && Path.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the location.
        /// </summary>
        public UInt64 Length
            => URL?.Length ?? Path?.Length ?? 0;

        public URL?       URL     { get; }

        public HTTPPath?  Path    { get; }

        #endregion

        #region Constructor(s)

        #region Location(URL)

        /// <summary>
        /// Create a new absolute HTTP Location based on the given HTTP URL.
        /// </summary>
        /// <param name="URL">A HTTP URL.</param>
        public Location(URL URL)
        {
            this.URL = URL;
        }

        #endregion

        #region Location(Path)

        /// <summary>
        /// Create a new relative HTTP Location based on the given HTTP path.
        /// </summary>
        /// <param name="Path">A HTTP path.</param>
        public Location(HTTPPath Path)
        {
            this.Path = Path;
        }

        #endregion

        #endregion


        #region (static) From(URL)

        /// <summary>
        /// Convert the given HTTP URL into a HTTP Location.
        /// </summary>
        /// <param name="Path">A HTTP URL.</param>
        public static Location From(URL URL)

            => new (URL);

        #endregion

        #region (static) From(Path)

        /// <summary>
        /// Convert the given HTTP Path into a HTTP Location.
        /// </summary>
        /// <param name="Path">A HTTP Path.</param>
        public static Location From(HTTPPath Path)

            => new(Path);

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a HTTP Location.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Location.</param>
        public static Location Parse(String Text)
        {

            if (TryParse(Text, out var location))
                return location;

            throw new ArgumentException("The given text representation of a HTTP Location is invalid: " + Text,
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a HTTP Location.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Location.</param>
        public static Location? TryParse(String Text)
        {

            if (TryParse(Text, out var location))
                return location;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out URL)

        /// <summary>
        /// Try to parse the given text as a HTTP Location.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Location.</param>
        /// <param name="URL">The parsed HTTP Location.</param>
        public static Boolean TryParse(String Text, out Location Location)
        {

            Text      = Text.Trim();
            Location  = default;

            if (Text.IsNotNullOrEmpty())
            {
                try
                {

                    if (Text.Contains("://") &&
                        HTTP.URL.TryParse(Text, out var url))
                    {
                        Location = new Location(url);
                        return true;
                    }

                    if (HTTPPath.TryParse(Text, out var path))
                    {
                        Location = new Location(path);
                        return true;
                    }

                }
                catch
                { }
            }

            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this HTTP Location.
        /// </summary>
        public Location Clone

            => URL.HasValue
                   ? new (URL.  Value)
                   : new (Path!.Value);

        #endregion


        #region Operator overloading

        #region Operator == (Location1, Location2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Location1">A HTTP location.</param>
        /// <param name="Location2">Another HTTP location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (Location Location1,
                                           Location Location2)

            => Location1.Equals(Location2);

        #endregion

        #region Operator != (Location1, Location2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Location1">A HTTP location.</param>
        /// <param name="Location2">Another HTTP location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (Location Location1,
                                           Location Location2)

            => !(Location1 == Location2);

        #endregion

        #region Operator <  (Location1, Location2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Location1">A HTTP location.</param>
        /// <param name="Location2">Another HTTP location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (Location Location1,
                                          Location Location2)

            => Location1.CompareTo(Location2) < 0;

        #endregion

        #region Operator <= (Location1, Location2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Location1">A HTTP location.</param>
        /// <param name="Location2">Another HTTP location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (Location Location1,
                                           Location Location2)

            => !(Location1 > Location2);

        #endregion

        #region Operator >  (Location1, Location2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Location1">A HTTP location.</param>
        /// <param name="Location2">Another HTTP location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (Location Location1,
                                          Location Location2)

            => Location1.CompareTo(Location2) > 0;

        #endregion

        #region Operator >= (Location1, Location2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Location1">A HTTP location.</param>
        /// <param name="Location2">Another HTTP location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (Location Location1,
                                           Location Location2)

            => !(Location1 < Location2);

        #endregion

        #endregion

        #region IComparable<Location> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP locations for equality.
        /// </summary>
        /// <param name="Object">A HTTP location to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is Location url
                   ? CompareTo(url)
                   : throw new ArgumentException("The given object is not an HTTP location!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(Location)

        /// <summary>
        /// Compares two HTTP locations for equality.
        /// </summary>
        /// <param name="Location">A HTTP location to compare with.</param>
        public Int32 CompareTo(Location Location)

            => URL.HasValue && Location.URL.HasValue
                   ? URL.Value.CompareTo(Location.URL. Value)
                   : Path.HasValue && Location.Path.HasValue
                         ? Path.Value.CompareTo(Location.Path.Value)
                         : 0;

        #endregion

        #endregion

        #region IEquatable<Location> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP locations for equality.
        /// </summary>
        /// <param name="Object">A HTTP location to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is Location url &&
                   Equals(url);

        #endregion

        #region Equals(Location)

        /// <summary>
        /// Compares two HTTP locations for equality.
        /// </summary>
        /// <param name="Location">A HTTP location to compare with.</param>
        public Boolean Equals(Location Location)

            => URL.HasValue && Location.URL.HasValue
                   ? URL.Value.Equals(Location.URL. Value)
                   : Path.HasValue && Location.Path.HasValue
                         ? Path.Value.Equals(Location.Path.Value)
                         : true;

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => URL?. GetHashCode() ??
               Path?.GetHashCode() ??
               0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => URL?. ToString() ??
               Path?.ToString() ??
               String.Empty;

        #endregion

    }

}
