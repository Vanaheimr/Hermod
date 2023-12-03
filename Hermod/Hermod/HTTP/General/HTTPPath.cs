/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System.Globalization;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for HTTP paths.
    /// </summary>
    public static class HTTPPathExtensions
    {

        /// <summary>
        /// Indicates whether this HTTP path is null or empty.
        /// </summary>
        /// <param name="HTTPPath">A HTTP path.</param>
        public static Boolean IsNullOrEmpty(this HTTPPath? HTTPPath)
            => !HTTPPath.HasValue || HTTPPath.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this HTTP path is null or empty.
        /// </summary>
        /// <param name="HTTPPath">A HTTP path.</param>
        public static Boolean IsNotNullOrEmpty(this HTTPPath? HTTPPath)
            => HTTPPath.HasValue && HTTPPath.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The path of a HTTP path.
    /// </summary>
    public readonly struct HTTPPath : IEquatable<HTTPPath>,
                                      IComparable<HTTPPath>,
                                      IComparable
    {

        #region Data

        /// <summary>
        /// The internal identifier.
        /// </summary>
        private readonly String  InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether the HTTP path is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether the HTTP path is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the HTTP path.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP path
        /// </summary>
        /// <param name="Path">The HTTP path.</param>
        private HTTPPath(String Path)
        {
            this.InternalId = Path;
        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text representation of a HTTP path.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP path.</param>
        public static HTTPPath Parse(String Text)
        {

            if (TryParse(Text, out var httpPath))
                return httpPath;

            throw new ArgumentException($"Invalid text representation of a HTTP path: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text representation of a HTTP path.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP path.</param>
        public static HTTPPath? TryParse(String Text)
        {

            if (TryParse(Text, out var httpPath))
                return httpPath;

            return null;

        }

        #endregion

        #region TryParse(Text, out URI)

        /// <summary>
        /// Try to parse the given text representation of a HTTP path.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP path.</param>
        /// <param name="HTTPPath">The parsed HTTP path.</param>
        public static Boolean TryParse(String Text, out HTTPPath HTTPPath)
        {

            Text = Text.Trim().Replace("//", "/");

            if (!Text.StartsWith("/"))
                Text = "/" + Text;

            try
            {
                HTTPPath = new HTTPPath(Text);
                return true;
            }
            catch
            { }

            HTTPPath = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this object.
        /// </summary>
        public HTTPPath Clone

            => new (InternalId);

        #endregion


        /// <summary>
        /// / (ROOT)
        /// </summary>
        public static HTTPPath  Root    { get; }
            = new ("/");


        public Boolean Contains(String Text)
            => InternalId.Contains(Text);


        public HTTPPath Substring(Int32 StartIndex)
            => Parse(InternalId.Substring(StartIndex));

        public HTTPPath Substring(Int32 StartIndex, Int32 EndIndex)
            => Parse(InternalId.Substring(StartIndex, EndIndex));


        public Int32 IndexOf(char value, int startIndex, int count)
            => InternalId.IndexOf(value, startIndex, count);

        public Int32 IndexOf(char value, int startIndex)
            => InternalId.IndexOf(value, startIndex);

        public Int32 IndexOf(String value)
            => InternalId.IndexOf(value);

        public Int32 IndexOf(String value, int startIndex)
            => InternalId.IndexOf(value, startIndex);

        public Int32 IndexOf(String value, int startIndex, int count)
            => InternalId.IndexOf(value, startIndex, count);

        public Int32 IndexOf(String value, StringComparison comparisonType)
            => InternalId.IndexOf(value, comparisonType);

        public Int32 IndexOf(String value, int startIndex, StringComparison comparisonType)
            => InternalId.IndexOf(value, startIndex, comparisonType);

        public Int32 IndexOf(char value)
            => InternalId.IndexOf(value);


        public Int32 LastIndexOf(char value, int startIndex, int count)
            => InternalId.LastIndexOf(value, startIndex, count);

        public Int32 LastIndexOf(char value, int startIndex)
            => InternalId.LastIndexOf(value, startIndex);

        public Int32 LastIndexOf(String value)
            => InternalId.LastIndexOf(value);

        public Int32 LastIndexOf(String value, int startIndex)
            => InternalId.LastIndexOf(value, startIndex);

        public Int32 LastIndexOf(String value, int startIndex, int count)
            => InternalId.LastIndexOf(value, startIndex, count);

        public Int32 LastIndexOf(String value, StringComparison comparisonType)
            => InternalId.LastIndexOf(value, comparisonType);

        public Int32 LastIndexOf(String value, int startIndex, StringComparison comparisonType)
            => InternalId.LastIndexOf(value, startIndex, comparisonType);

        public Int32 LastIndexOf(char value)
            => InternalId.LastIndexOf(value);


        public Boolean StartsWith(String value, StringComparison comparisonType)
            => InternalId.StartsWith(value, comparisonType);

        public Boolean StartsWith(String value, bool ignoreCase, CultureInfo culture)
            => InternalId.StartsWith(value, ignoreCase, culture);

        public Boolean StartsWith(String value)
            => InternalId.StartsWith(value);

        public Boolean StartsWith(HTTPPath value)
            => InternalId.StartsWith(value.ToString());


        public Boolean EndsWith(String value)
            => InternalId.EndsWith(value);

        public Boolean EndsWith(String value, StringComparison comparisonType)
            => InternalId.EndsWith(value, comparisonType);

        public Boolean EndsWith(String value, bool ignoreCase, CultureInfo culture)
            => InternalId.EndsWith(value, ignoreCase, culture);

        public Boolean EndsWith(HTTPPath value)
            => InternalId.EndsWith(value.ToString());


        #region Operator overloading

        #region Operator == (HTTPPath1, HTTPPath2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPPath HTTPPath1,
                                           HTTPPath HTTPPath2)

            => HTTPPath1.Equals(HTTPPath2);

        #endregion

        #region Operator == (HTTPPath1, Text)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="Text">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPPath HTTPPath1,
                                           String   Text)

            => String.Equals(HTTPPath1.InternalId,
                             Text,
                             StringComparison.Ordinal);

        #endregion

        #region Operator != (HTTPPath1, HTTPPath2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPPath HTTPPath1,
                                           HTTPPath HTTPPath2)

            => !HTTPPath1.Equals(HTTPPath2);

        #endregion

        #region Operator != (HTTPPath1, Text)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="Text">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPPath HTTPPath1,
                                           String   Text)

            => !String.Equals(HTTPPath1.InternalId,
                              Text,
                              StringComparison.Ordinal);

        #endregion

        #region Operator <  (HTTPPath1, HTTPPath2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPPath HTTPPath1,
                                          HTTPPath HTTPPath2)

            => HTTPPath1.CompareTo(HTTPPath2) < 0;

        #endregion

        #region Operator <= (HTTPPath1, HTTPPath2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPPath HTTPPath1,
                                           HTTPPath HTTPPath2)

            => HTTPPath1.CompareTo(HTTPPath2) <= 0;

        #endregion

        #region Operator >  (HTTPPath1, HTTPPath2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPPath HTTPPath1, HTTPPath HTTPPath2)

            => HTTPPath1.CompareTo(HTTPPath2) > 0;

        #endregion

        #region Operator >= (HTTPPath1, HTTPPath2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPPath HTTPPath1, HTTPPath HTTPPath2)

            => HTTPPath1.CompareTo(HTTPPath2) >= 0;

        #endregion


        #region Operator +  (HTTPPath1, Text)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="Text">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath HTTPPath1,
                                           String   Text)
        {

            if (HTTPPath1.EndsWith("/") && Text.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + Text.Substring(1));

            if (!HTTPPath1.EndsWith("/") && Text.StartsWith("/") ||
                 HTTPPath1.EndsWith("/") && !Text.StartsWith("/"))
            {
                return Text.IsNotNullOrEmpty()
                           ? Parse(HTTPPath1.ToString() + Text)
                           : HTTPPath1;
            }

            return Text.IsNotNullOrEmpty()
                       ? Parse(HTTPPath1.ToString() + "/" + Text)
                       : HTTPPath1;

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="Text">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath? HTTPPath1, String Text)
        {

            if (!HTTPPath1.HasValue)
                return Parse(Text);

            if (HTTPPath1.Value.EndsWith("/") && Text.StartsWith("/"))
                return Parse(HTTPPath1.Value.ToString() + Text.Substring(1));

            if (!HTTPPath1.Value.EndsWith("/") &&  Text.StartsWith("/") ||
                 HTTPPath1.Value.EndsWith("/") && !Text.StartsWith("/"))
            {
                return Text.IsNotNullOrEmpty()
                           ? Parse(HTTPPath1.Value.ToString() + Text)
                           : HTTPPath1.Value;
            }

            return Text.IsNotNullOrEmpty()
                       ? Parse(HTTPPath1.Value.ToString() + "/" + Text)
                       : HTTPPath1.Value;

        }

        #endregion

        #region Operator +  (Hostname,  HTTPPath)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname">Another HTTP path.</param>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <returns>true|false</returns>
        public static String operator + (HTTPHostname Hostname,
                                         HTTPPath     HTTPPath1)
        {

            if (Hostname.ToString().EndsWith("/") && HTTPPath1.StartsWith("/"))
                return Hostname.ToString().Substring(1) + HTTPPath1.ToString();

            if (!Hostname.ToString().EndsWith("/") &&  HTTPPath1.StartsWith("/") ||
                 Hostname.ToString().EndsWith("/") && !HTTPPath1.StartsWith("/"))
                return Hostname + HTTPPath1.ToString();

            return Hostname + "/" + HTTPPath1.ToString();

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Hostname">Another HTTP path.</param>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <returns>true|false</returns>
        public static String operator + (HTTPHostname Hostname, HTTPPath? HTTPPath1)
        {

            if (!HTTPPath1.HasValue)
                return Hostname.ToString();

            if (Hostname.ToString().EndsWith("/") && HTTPPath1.Value.StartsWith("/"))
                return Hostname.ToString().Substring(1) + HTTPPath1.ToString();

            if (!Hostname.ToString().EndsWith("/") &&  HTTPPath1.Value.StartsWith("/") ||
                 Hostname.ToString().EndsWith("/") && !HTTPPath1.Value.StartsWith("/"))
                return Hostname + HTTPPath1.ToString();

            return Hostname + "/" + HTTPPath1.ToString();

        }

        #endregion

        #region Operator +  (Text,      HTTPPath)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Text">Another HTTP path.</param>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <returns>true|false</returns>
        public static String operator + (String   Text,
                                         HTTPPath HTTPPath1)
        {

            if (Text.EndsWith("/") && HTTPPath1.StartsWith("/"))
                return Text.Substring(1) + HTTPPath1.ToString();

            if (!Text.EndsWith("/") &&  HTTPPath1.StartsWith("/") ||
                 Text.EndsWith("/") && !HTTPPath1.StartsWith("/"))
                return Text + HTTPPath1.ToString();

            return Text + "/" + HTTPPath1.ToString();

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Text">Another HTTP path.</param>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <returns>true|false</returns>
        public static String operator + (String Text, HTTPPath? HTTPPath1)
        {

            if (!HTTPPath1.HasValue)
                return Text;

            if (Text.EndsWith("/") && HTTPPath1.Value.StartsWith("/"))
                return Text.Substring(1) + HTTPPath1.ToString();

            if (!Text.EndsWith("/") &&  HTTPPath1.Value.StartsWith("/") ||
                 Text.EndsWith("/") && !HTTPPath1.Value.StartsWith("/"))
                return Text + HTTPPath1.ToString();

            return Text + "/" + HTTPPath1.ToString();

        }

        #endregion

        #region Operator +  (HTTPPath1, HTTPPath2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath HTTPPath1,
                                           HTTPPath HTTPPath2)
        {

            if (HTTPPath1.EndsWith("/") && HTTPPath2.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + HTTPPath2.Substring(1).ToString());

            if (!HTTPPath1.EndsWith("/") &&  HTTPPath2.StartsWith("/") ||
                 HTTPPath1.EndsWith("/") && !HTTPPath2.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + HTTPPath2.ToString());

            return Parse(HTTPPath1.ToString() + "/" + HTTPPath2.ToString());

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath? HTTPPath1, HTTPPath HTTPPath2)
        {

            if (!HTTPPath1.HasValue)
                return HTTPPath2;

            if (HTTPPath1.Value.EndsWith("/") && HTTPPath2.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + HTTPPath2.Substring(1).ToString());

            if (!HTTPPath1.Value.EndsWith("/") &&  HTTPPath2.StartsWith("/") ||
                 HTTPPath1.Value.EndsWith("/") && !HTTPPath2.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + HTTPPath2.ToString());

            return Parse(HTTPPath1.ToString() + "/" + HTTPPath2.ToString());

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath HTTPPath1, HTTPPath? HTTPPath2)
        {

            if (!HTTPPath2.HasValue)
                return HTTPPath1;

            if (HTTPPath1.EndsWith("/") && HTTPPath2.Value.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + HTTPPath2.Value.Substring(1).ToString());

            if (!HTTPPath1.EndsWith("/") &&  HTTPPath2.Value.StartsWith("/") ||
                 HTTPPath1.EndsWith("/") && !HTTPPath2.Value.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + HTTPPath2.ToString());

            return Parse(HTTPPath1.ToString() + "/" + HTTPPath2.ToString());

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath1">A HTTP path.</param>
        /// <param name="HTTPPath2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath? HTTPPath1, HTTPPath? HTTPPath2)
        {

            if (!HTTPPath1.HasValue && !HTTPPath2.HasValue)
                return HTTPPath.Parse("/");

            if (!HTTPPath1.HasValue)
                return HTTPPath2.Value;

            if (!HTTPPath2.HasValue)
                return HTTPPath1.Value;

            if (HTTPPath1.Value.EndsWith("/") && HTTPPath2.Value.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + HTTPPath2.Value.Substring(1).ToString());

            if (!HTTPPath1.Value.EndsWith("/") &&  HTTPPath2.Value.StartsWith("/") ||
                 HTTPPath1.Value.EndsWith("/") && !HTTPPath2.Value.StartsWith("/"))
                return Parse(HTTPPath1.ToString() + HTTPPath2.ToString());

            return Parse(HTTPPath1.ToString() + "/" + HTTPPath2.ToString());

        }

        #endregion

        #endregion

        #region IComparable<HTTPPath> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPPath httpPath
                   ? CompareTo(httpPath)
                   : throw new ArgumentException("The given object is not a HTTP path!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPPath)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPPath">An object to compare with.</param>
        public Int32 CompareTo(HTTPPath HTTPPath)

            => String.Compare(InternalId,
                              HTTPPath.InternalId,
                              StringComparison.Ordinal);

        #endregion

        #endregion

        #region IEquatable<HTTPPath> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object? Object)

            => Object is HTTPPath httpPath &&
                   Equals(httpPath);

        #endregion

        #region Equals(HTTPPath)

        /// <summary>
        /// Compares two HTTPPaths for equality.
        /// </summary>
        /// <param name="HTTPPath">A HTTPPath to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPPath HTTPPath)

            => String.Equals(InternalId,
                             HTTPPath.InternalId,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
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
