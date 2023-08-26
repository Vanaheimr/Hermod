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

using System.Collections;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A single HTTP cookie.
    /// </summary>
    public class HTTPCookie : IEnumerable<KeyValuePair<String, String>>,
                              IEquatable<HTTPCookie>,
                              IComparable<HTTPCookie>,
                              IComparable
    {

        #region Data

        /// <summary>
        /// The data stored within the cookie.
        /// </summary>
        private readonly Dictionary<String, String>  crumbs;

        /// <summary>
        /// The parts of the cookie.
        /// </summary>
        private readonly Dictionary<String, String>  parts;

        #endregion

        #region Properties

        /// <summary>
        /// The name of the cookie.
        /// </summary>
        public HTTPCookieName  Name        { get; }

        public String?         Path        { get; }

        public Boolean         Secure      { get; }

        public Boolean         HTTPOnly    { get; }

        public String?         SameSite    { get; }

        public DateTime?       Expires     { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP cookie.
        /// </summary>
        private HTTPCookie(HTTPCookieName                             Name,
                           IEnumerable<KeyValuePair<String, String>>  Crumbs,
                           IEnumerable<KeyValuePair<String, String>>  Parts)
        {

            this.Name    = Name;
            this.crumbs  = new Dictionary<String, String>();
            this.parts   = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            foreach (var crumb in Crumbs)
            {
                if (!this.crumbs.ContainsKey(crumb.Key))
                    this.crumbs.Add(crumb.Key, crumb.Value);
                else
                    this.crumbs[crumb.Key] = crumb.Value;
            }

            foreach (var part in Parts)
            {
                if (!this.parts.ContainsKey(part.Key))
                    this.parts.Add(part.Key, part.Value);
                else
                    this.parts[part.Key] = part.Value;
            }

            this.Path      = parts.ContainsKey("Path")     ? parts["Path"]                                      : null;
            this.Secure    = parts.ContainsKey("Secure");
            this.HTTPOnly  = parts.ContainsKey("HTTPOnly");
            this.SameSite  = parts.ContainsKey("SameSite") ? parts["SameSite"]                                  : null;
            this.Expires   = parts.ContainsKey("Expires")  ? DateTime.Parse(parts["Expires"]).ToUniversalTime() : null;

        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text as a single HTTP cookie.
        /// </summary>
        /// <param name="Text">A text representation of a single HTTP cookie.</param>
        public static HTTPCookie Parse(String Text)
        {

            if (TryParse(Text, out var httpCookie))
                return httpCookie!;

            throw new ArgumentException("The given JSON representation of a single HTTP cookie is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text, out HTTPCookie)

        /// <summary>
        /// Parse the given string as a single HTTP cookie.
        /// </summary>
        /// <param name="Text">A text representation of a single HTTP cookie.</param>
        /// <param name="HTTPCookie">The parsed HTTP cookie.</param>
        public static Boolean TryParse(String Text, out HTTPCookie? HTTPCookie)
        {

            if (Text.IsNotNullOrEmpty())
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
            {
                HTTPCookie = null;
                return false;
            }

            if (!HTTPCookieName.TryParse(Text[..Text.IndexOf('=')],
                                         out var cookieName))
            {
                HTTPCookie = null;
                return false;
            }

            var split2 = new[] { '=' };
            var parts  = new Dictionary<String, String>();

            Text.Split     (';').
                 SafeSelect(command => command.Split(split2, 2)).
                 ForEach   (tuple   => {
                               if (tuple[0].IsNotNullOrEmpty())
                               {
                                   if (tuple.Length == 1)
                                   {
                                       if (!parts.ContainsKey(tuple[0].Trim()))
                                           parts.Add(tuple[0].Trim(), "");
                                       else
                                           parts[tuple[0].Trim()] = "";
                                   }
                                   else if (tuple.Length == 2)
                                   {
                                       if (!parts.ContainsKey(tuple[0].Trim()))
                                           parts.Add(tuple[0].Trim(), tuple[1]);
                                       else
                                           parts[tuple[0].Trim()] = tuple[1];
                                   }
                               }
                          });


            var crumbs = new Dictionary<String, String>();

            parts[cookieName.ToString()].Split     (':').
                                         SafeSelect(command => command.Split(split2, 2)).
                                         ForEach   (tuple   => {
                                                       if (tuple[0].IsNotNullOrEmpty())
                                                       {
                                                           if (tuple.Length == 1)
                                                           {
                                                               if (!crumbs.ContainsKey(tuple[0].Trim()))
                                                                   crumbs.Add(tuple[0].Trim(), "");
                                                               else
                                                                   crumbs[tuple[0].Trim()] = "";
                                                           }
                                                           else if (tuple.Length == 2)
                                                           {
                                                               if (!crumbs.ContainsKey(tuple[0].Trim()))
                                                                   crumbs.Add(tuple[0].Trim(), tuple[1]);
                                                               else
                                                                   crumbs[tuple[0].Trim()] = tuple[1];
                                                           }
                                                       }
                                                  });

            parts.Remove(cookieName.ToString());


            HTTPCookie = new HTTPCookie(cookieName,
                                        crumbs,
                                        parts);

            return true;

        }

        #endregion


        #region this[Crumb]

        /// <summary>
        /// Return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        public String? this[String Crumb]
        {
            get
            {

                if (crumbs.TryGetValue(Crumb, out var value))
                    return value;

                return null;

            }
        }

        #endregion

        #region Get(Crumb)

        /// <summary>
        /// Return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        public String? Get(String Crumb)
        {

            if (crumbs.TryGetValue(Crumb, out var value))
                return value;

            return null;

        }

        #endregion

        #region TryGet(Crumb, out Value)

        /// <summary>
        /// Try to return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        /// <param name="Value">The value of the crumb.</param>
        public Boolean TryGet(String Crumb, out String? Value)

            => crumbs.TryGetValue(Crumb, out Value);

        #endregion


        #region GetEnumerator()

        public IEnumerator<KeyValuePair<String, String>> GetEnumerator()
            => crumbs.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => crumbs.GetEnumerator();

        #endregion


        #region Operator overloading

        #region Operator == (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPCookie HTTPCookie1,
                                           HTTPCookie HTTPCookie2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPCookie1, HTTPCookie2))
                return true;

            // If one is null, but not both, return false.
            if (HTTPCookie1 is null || HTTPCookie2 is null)
                return false;

            return HTTPCookie1.Equals(HTTPCookie2);

        }

        #endregion

        #region Operator != (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPCookie HTTPCookie1,
                                           HTTPCookie HTTPCookie2)

            => !(HTTPCookie1 == HTTPCookie2);

        #endregion

        #region Operator <  (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPCookie HTTPCookie1,
                                          HTTPCookie HTTPCookie2)
        {

            if (HTTPCookie1 is null)
                throw new ArgumentNullException("The given HTTPCookie1 must not be null!");

            return HTTPCookie1.CompareTo(HTTPCookie2) < 0;

        }

        #endregion

        #region Operator <= (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPCookie HTTPCookie1,
                                           HTTPCookie HTTPCookie2)

            => !(HTTPCookie1 > HTTPCookie2);

        #endregion

        #region Operator >  (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPCookie HTTPCookie1,
                                          HTTPCookie HTTPCookie2)
        {

            if (HTTPCookie1 is null)
                throw new ArgumentNullException("The given HTTPCookie1 must not be null!");

            return HTTPCookie1.CompareTo(HTTPCookie2) > 0;

        }

        #endregion

        #region Operator >= (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPCookie HTTPCookie1,
                                           HTTPCookie HTTPCookie2)

            => !(HTTPCookie1 < HTTPCookie2);

        #endregion

        #endregion

        #region IComparable<HTTPCookie> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP cookies.
        /// </summary>
        /// <param name="Object">A HTTP cookie to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPCookie httpCookie
                   ? CompareTo(httpCookie)
                   : throw new ArgumentException("The given object is not a HTTP cookie!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPCookie)

        /// <summary>
        /// Compares two HTTP cookies.
        /// </summary>
        /// <param name="HTTPCookie">A HTTP cookie to compare with.</param>
        public Int32 CompareTo(HTTPCookie? HTTPCookie)
        {

            if (HTTPCookie is null)
                throw new ArgumentNullException(nameof(HTTPCookie), "The given HTTP Cookie must not be null!");

            return String.Compare(ToString(),
                                  HTTPCookie.ToString(),
                                  StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPCookie> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP cookies for equality.
        /// </summary>
        /// <param name="Object">A HTTP cookie to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPCookie httpCookie &&
                   Equals(httpCookie);

        #endregion

        #region Equals(HTTPCookie)

        /// <summary>
        /// Compares two HTTP cookies for equality.
        /// </summary>
        /// <param name="HTTPCookie">A HTTP cookie to compare with.</param>
        public Boolean Equals(HTTPCookie? HTTPCookie)

            => HTTPCookie is not null &&
               String.Equals(ToString(),
                             HTTPCookie.ToString(),
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            unchecked
            {
                return Name.GetHashCode();
            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            var _crumbs = crumbs.Select(crumb => crumb.Value.IsNotNullOrEmpty() ? $"{crumb.Key}={crumb.Value}" : crumb.Key).
                                 AggregateWith(":");

            var _parts  = parts. Select(part  => part. Value.IsNotNullOrEmpty() ? $"{part.Key }={part. Value}" : part. Key).
                                 AggregateWith("; ");

             return $"{Name}={_crumbs}{(_parts.IsNotNullOrEmpty() ? "; " + _parts : "")}";

        }

        #endregion


    }

}
