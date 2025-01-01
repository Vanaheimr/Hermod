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

            if (!Text.StartsWith('/'))
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
            => Parse(InternalId[StartIndex..]);

        public HTTPPath Substring(Int32 StartIndex, Int32 EndIndex)
            => Parse(InternalId.Substring(StartIndex, EndIndex));


        public Int32 IndexOf(Char value, Int32 StartIndex, Int32 Count)
            => InternalId.IndexOf(value, StartIndex, Count);

        public Int32 IndexOf(Char Value, Int32 StartIndex)
            => InternalId.IndexOf(Value, StartIndex);

        public Int32 IndexOf(String Value)
            => InternalId.IndexOf(Value);

        public Int32 IndexOf(String Value, Int32 StartIndex)
            => InternalId.IndexOf(Value, StartIndex);

        public Int32 IndexOf(String Value, Int32 StartIndex, Int32 Count)
            => InternalId.IndexOf(Value, StartIndex, Count);

        public Int32 IndexOf(String Value, StringComparison ComparisonType)
            => InternalId.IndexOf(Value, ComparisonType);

        public Int32 IndexOf(String Value, Int32 StartIndex, StringComparison ComparisonType)
            => InternalId.IndexOf(Value, StartIndex, ComparisonType);

        public Int32 IndexOf(Char Value)
            => InternalId.IndexOf(Value);


        public Int32 LastIndexOf(Char Value, Int32 StartIndex, Int32 Count)
            => InternalId.LastIndexOf(Value, StartIndex, Count);

        public Int32 LastIndexOf(Char Value, Int32 StartIndex)
            => InternalId.LastIndexOf(Value, StartIndex);

        public Int32 LastIndexOf(String Value)
            => InternalId.LastIndexOf(Value);

        public Int32 LastIndexOf(String Value, Int32 StartIndex)
            => InternalId.LastIndexOf(Value, StartIndex);

        public Int32 LastIndexOf(String Value, Int32 StartIndex, Int32 Count)
            => InternalId.LastIndexOf(Value, StartIndex, Count);

        public Int32 LastIndexOf(String Value, StringComparison ComparisonType)
            => InternalId.LastIndexOf(Value, ComparisonType);

        public Int32 LastIndexOf(String Value, Int32 StartIndex, StringComparison ComparisonType)
            => InternalId.LastIndexOf(Value, StartIndex, ComparisonType);

        public Int32 LastIndexOf(Char Value)
            => InternalId.LastIndexOf(Value);


        public Boolean StartsWith(String Value, StringComparison ComparisonType)
            => InternalId.StartsWith(Value, ComparisonType);

        public Boolean StartsWith(String Value, Boolean IgnoreCase, CultureInfo Culture)
            => InternalId.StartsWith(Value, IgnoreCase, Culture);

        public Boolean StartsWith(String Value)
            => InternalId.StartsWith(Value);

        public Boolean StartsWith(HTTPPath Value)
            => InternalId.StartsWith(Value.ToString());


        public Boolean EndsWith(String Value)
            => InternalId.EndsWith(Value);

        public Boolean EndsWith(String Value, StringComparison ComparisonType)
            => InternalId.EndsWith(Value, ComparisonType);

        public Boolean EndsWith(String Value, Boolean IgnoreCase, CultureInfo Culture)
            => InternalId.EndsWith(Value, IgnoreCase, Culture);

        public Boolean EndsWith(HTTPPath Value)
            => InternalId.EndsWith(Value.ToString());


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


        #region Operator +  (Path, Text)

        /// <summary>
        /// Combines a HTTP path and a string.
        /// </summary>
        /// <param name="Path">A HTTP path.</param>
        /// <param name="Text">Another HTTP path.</param>
        public static HTTPPath operator + (HTTPPath  Path,
                                           String    Text)
        {

            if (Path.EndsWith("/") && Text.StartsWith('/'))
                return Parse(Path.ToString() + Text[1..]);

            if (!Path.EndsWith("/") &&  Text.StartsWith('/') ||
                 Path.EndsWith("/") && !Text.StartsWith('/'))
            {
                return Text.IsNotNullOrEmpty()
                           ? Parse(Path.ToString() + Text)
                           : Path;
            }

            return Text.IsNotNullOrEmpty()
                       ? Parse(Path.ToString() + "/" + Text)
                       : Path;

        }

        /// <summary>
        /// Combines a HTTP path and a string.
        /// </summary>
        /// <param name="Path">A HTTP path.</param>
        /// <param name="Text">Another HTTP path.</param>
        public static HTTPPath operator + (HTTPPath?  Path,
                                           String     Text)
        {

            if (!Path.HasValue)
                return Parse(Text);

            if (Path.Value.EndsWith("/") && Text.StartsWith('/'))
                return Parse(Path.Value.ToString() + Text[1..]);

            if (!Path.Value.EndsWith("/") &&  Text.StartsWith('/') ||
                 Path.Value.EndsWith("/") && !Text.StartsWith('/'))
            {
                return Text.IsNotNullOrEmpty()
                           ? Parse(Path.Value.ToString() + Text)
                           : Path.Value;
            }

            return Text.IsNotNullOrEmpty()
                       ? Parse(Path.Value.ToString() + "/" + Text)
                       : Path.Value;

        }

        #endregion

        #region Operator +  (Hostname,  Path)

        /// <summary>
        /// Combines a HTTP hostname and HTTP path.
        /// </summary>
        /// <param name="Hostname">Another HTTP path.</param>
        /// <param name="Path">A HTTP path.</param>
        public static String operator + (HTTPHostname  Hostname,
                                         HTTPPath      Path)
        {

            if (Hostname.ToString().EndsWith('/') && Path.StartsWith("/"))
                return Hostname.ToString()[1..] + Path.ToString();

            if (!Hostname.ToString().EndsWith('/') &&  Path.StartsWith("/") ||
                 Hostname.ToString().EndsWith('/') && !Path.StartsWith("/"))
                return Hostname + Path.ToString();

            return $"{Hostname}/{Path}";

        }

        /// <summary>
        /// Combines a HTTP hostname and HTTP path.
        /// </summary>
        /// <param name="Hostname">Another HTTP path.</param>
        /// <param name="Path">A HTTP path.</param>
        public static String operator + (HTTPHostname  Hostname,
                                         HTTPPath?     Path)
        {

            if (!Path.HasValue)
                return Hostname.ToString();

            if (Hostname.ToString().EndsWith('/') && Path.Value.StartsWith("/"))
                return Hostname.ToString()[1..] + Path.ToString();

            if (!Hostname.ToString().EndsWith('/') &&  Path.Value.StartsWith("/") ||
                 Hostname.ToString().EndsWith('/') && !Path.Value.StartsWith("/"))
                return Hostname + Path.ToString();

            return $"{Hostname}/{Path}";

        }

        #endregion

        #region Operator +  (Text,      Path)

        /// <summary>
        /// Combines a string and HTTP path.
        /// </summary>
        /// <param name="Text">Another HTTP path.</param>
        /// <param name="Path">A HTTP path.</param>
        public static String operator + (String    Text,
                                         HTTPPath  Path)
        {

            if (Text.EndsWith('/') && Path.StartsWith("/"))
                return Text[1..] + Path.ToString();

            if (!Text.EndsWith('/') &&  Path.StartsWith("/") ||
                 Text.EndsWith('/') && !Path.StartsWith("/"))
                return Text + Path.ToString();

            return $"{Text}/{Path}";

        }

        /// <summary>
        /// Combines a string and HTTP path.
        /// </summary>
        /// <param name="Text">Another HTTP path.</param>
        /// <param name="Path">A HTTP path.</param>
        public static String operator + (String     Text,
                                         HTTPPath?  Path)
        {

            if (!Path.HasValue)
                return Text;

            if (Text.EndsWith('/') && Path.Value.StartsWith("/"))
                return Text[1..] + Path.ToString();

            if (!Text.EndsWith('/') &&  Path.Value.StartsWith("/") ||
                 Text.EndsWith('/') && !Path.Value.StartsWith("/"))
                return Text + Path.ToString();

            return $"{Text}/{Path}";

        }

        #endregion

        #region Operator +  (Path1,     Path2)

        /// <summary>
        /// Combines two instances of this object.
        /// </summary>
        /// <param name="Path1">A HTTP path.</param>
        /// <param name="Path2">Another HTTP path.</param>
        public static HTTPPath operator + (HTTPPath  Path1,
                                           HTTPPath  Path2)
        {

            if (Path1.EndsWith("/") && Path2.StartsWith("/"))
                return Parse(Path1.ToString() + Path2.Substring(1).ToString());

            if (!Path1.EndsWith("/") &&  Path2.StartsWith("/") ||
                 Path1.EndsWith("/") && !Path2.StartsWith("/"))
                return Parse(Path1.ToString() + Path2.ToString());

            return Parse($"{Path1}/{Path2}");

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Path1">A HTTP path.</param>
        /// <param name="Path2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath?  Path1,
                                           HTTPPath   Path2)
        {

            if (!Path1.HasValue)
                return Path2;

            if (Path1.Value.EndsWith("/") && Path2.StartsWith("/"))
                return Parse(Path1.ToString() + Path2.ToString()[1..]);

            if (!Path1.Value.EndsWith("/") &&  Path2.StartsWith("/") ||
                 Path1.Value.EndsWith("/") && !Path2.StartsWith("/"))
                return Parse(Path1.ToString() + Path2.ToString());

            return Parse($"{Path1}/{Path2}");

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Path1">A HTTP path.</param>
        /// <param name="Path2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath   Path1,
                                           HTTPPath?  Path2)
        {

            if (!Path2.HasValue)
                return Path1;

            if (Path1.EndsWith("/") && Path2.Value.StartsWith("/"))
                return Parse(Path1.ToString() + Path2.Value.ToString()[1..]);

            if (!Path1.EndsWith("/") &&  Path2.Value.StartsWith("/") ||
                 Path1.EndsWith("/") && !Path2.Value.StartsWith("/"))
                return Parse(Path1.ToString() + Path2.ToString());

            return Parse($"{Path1}/{Path2}");

        }

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Path1">A HTTP path.</param>
        /// <param name="Path2">Another HTTP path.</param>
        /// <returns>true|false</returns>
        public static HTTPPath operator + (HTTPPath?  Path1,
                                           HTTPPath?  Path2)
        {

            if (Path1.HasValue && Path2.HasValue)
            {

                if (Path1.Value.EndsWith("/") && Path2.Value.StartsWith("/"))
                    return Parse(Path1.ToString() + Path2.Value.ToString()[1..]);

                if (!Path1.Value.EndsWith("/") &&  Path2.Value.StartsWith("/") ||
                     Path1.Value.EndsWith("/") && !Path2.Value.StartsWith("/"))
                    return Parse(Path1.ToString() + Path2.ToString());

                return Parse($"{Path1}/{Path2}");

            }

            if (Path1.HasValue)
                return Path1.Value;

            if (Path2.HasValue)
                return Path2.Value;

            return Root;

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
