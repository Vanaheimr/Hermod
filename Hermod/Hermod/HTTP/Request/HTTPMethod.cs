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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using static System.Net.Mime.MediaTypeNames;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for HTTP methods.
    /// </summary>
    public static class HTTPMethodExtensions
    {

        /// <summary>
        /// Indicates whether this HTTP method is null or empty.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        public static Boolean IsNullOrEmpty(this HTTPMethod? HTTPMethod)
            => !HTTPMethod.HasValue || HTTPMethod.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this HTTP method is NOT null or empty.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        public static Boolean IsNotNullOrEmpty(this HTTPMethod? HTTPMethod)
            => HTTPMethod.HasValue && HTTPMethod.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A HTTP method.
    /// </summary>
    public readonly struct HTTPMethod : IEquatable<HTTPMethod>,
                                        IComparable<HTTPMethod>,
                                        IComparable
    {

        #region Data

        private static readonly ConcurrentDictionary<String, HTTPMethod> httpMethods = new();

        #endregion

        #region Properties

        /// <summary>
        /// The name of the HTTP method.
        /// </summary>
        public String   MethodName      { get; }

        /// <summary>
        /// This HTTP method does not cause any changes or side-effects on the server-side.
        /// </summary>
        public Boolean  IsSafe          { get; }

        /// <summary>
        /// This HTTP methods has no side-effects for multiple identical requests other as for a single request.
        /// </summary>
        public Boolean  IsIdempotent    { get; }

        /// <summary>
        /// The optional description of this HTTP method.
        /// </summary>
        public String?  Description     { get; }


        /// <summary>
        /// Indicates whether this HTTP method is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => MethodName.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this HTTP method is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => MethodName.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the HTTP method.
        /// </summary>
        public UInt64 Length
            => (UInt64) (MethodName?.Length ?? 0);

        #endregion

        #region (private) Constructor(s)

        #region (static)  HTTPMethod()

        static HTTPMethod()
        {

            foreach (var fieldInfo in typeof(HTTPMethod).GetFields())
            {
                if (fieldInfo is not null)
                {

                    var reflected = fieldInfo.GetValue(null);

                    if (reflected is HTTPMethod httpMethod)
                        httpMethods.TryAdd(httpMethod.MethodName, httpMethod);

                }
            }

        }

        #endregion

        #region (private) HTTPMethod(MethodName, IsSafe = false, IsIdempotent = false, Description = null)

        /// <summary>
        /// Creates a new HTTP method based on the given parameters.
        /// </summary>
        /// <param name="MethodName">The name of the HTTP method.</param>
        /// <param name="IsSafe">The HTTP method does not cause any changes or side-effects on the server-side.</param>
        /// <param name="IsIdempotent">The HTTP methods has no side-effects for multiple identical requests other as for a single request.</param>
        /// <param name="Description">An optional description of this HTTP method.</param>
        private HTTPMethod(String   MethodName,
                           Boolean  IsSafe         = false,
                           Boolean  IsIdempotent   = false,
                           String?  Description    = null)
        {

            this.MethodName    = MethodName;
            this.IsSafe        = IsSafe;
            this.IsIdempotent  = IsIdempotent;
            this.Description   = Description;

            unchecked
            {

                hashCode = this.MethodName.  GetHashCode() *  7 ^
                           this.IsSafe.      GetHashCode() *  5 ^
                           this.IsIdempotent.GetHashCode() *  3 ^
                           this.Description?.GetHashCode() ?? 0;

            }

        }

        #endregion

        #endregion


        #region RFC 2616 - HTTP/1.1

        public static readonly HTTPMethod CONNECT       = Parse("CONNECT");

        /// <summary>
        /// Delete the given resource.
        /// </summary>
        public static readonly HTTPMethod DELETE        = Parse("DELETE",  IsIdempotent: true);

        /// <summary>
        /// Return the given resource.
        /// </summary>
        public static readonly HTTPMethod GET           = Parse("GET",     IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Return only the headers (not including the body) of the given resource.
        /// </summary>
        public static readonly HTTPMethod HEAD          = Parse("HEAD",    IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Return a list of valid HTTP verbs for the given resource.
        /// </summary>
        public static readonly HTTPMethod OPTIONS       = Parse("OPTIONS", IsIdempotent: true);

        public static readonly HTTPMethod POST          = Parse("POST");

        public static readonly HTTPMethod PUT           = Parse("PUT",     IsIdempotent: true);

        public static readonly HTTPMethod TRACE         = Parse("TRACE",   IsIdempotent: true);

        #endregion

        #region RFC 4918 - WebDAV

        public static readonly HTTPMethod COPY          = Parse("COPY");
        public static readonly HTTPMethod LOCK          = Parse("LOCK");
        public static readonly HTTPMethod MKCOL         = Parse("MKCOL");
        public static readonly HTTPMethod MOVE          = Parse("MOVE");
        public static readonly HTTPMethod PROPFIND      = Parse("PROPFIND");
        public static readonly HTTPMethod PROPPATCH     = Parse("PROPPATCH");
        public static readonly HTTPMethod UNLOCK        = Parse("UNLOCK");

        #endregion

        #region Additional methods

        /// <summary>
        /// Similar to SEARCH, searches for matching items, but might filter or sort those items differently.
        /// </summary>
        public static readonly HTTPMethod SEARCH          = Parse("SEARCH",   IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Similar to GET, checks wether a resource exists, but only returns 'true' or 'false'.
        /// </summary>
        public static readonly HTTPMethod EXISTS          = Parse("EXISTS",   IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Counts the number of elements in a resource collection.
        /// </summary>
        public static readonly HTTPMethod COUNT           = Parse("COUNT",    IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Similar to GET, but with an additional filter methods within the http body.
        /// </summary>
        public static readonly HTTPMethod FILTER          = Parse("FILTER",   IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Returns dynamic status information on a single resource or an entire resource collection.
        /// </summary>
        public static readonly HTTPMethod STATUS          = Parse("STATUS",   IsIdempotent: true, IsSafe: true);


        /// <summary>
        /// Creates a new resource. Within a resource collection the unique
        /// identification of the new resource will be chosen by the server.
        /// </summary>
        public static readonly HTTPMethod CREATE          = Parse("CREATE");

        /// <summary>
        /// Adds a new resource to a resource collection. It will fail when
        /// a unique identification of the resource is missing or already
        /// exists on the server.
        /// </summary>
        public static readonly HTTPMethod ADD             = Parse("ADD");

        /// <summary>
        /// Adds a new resource to a resource collection. The request will be silently
        /// ignored when the unique identification of the resource already exists on
        /// the server.
        /// </summary>
        public static readonly HTTPMethod ADDIFNOTEXISTS  = Parse("ADDIFNOTEXISTS");



        /// <summary>
        /// Patch the given resource.
        /// </summary>
        public static readonly HTTPMethod PATCH           = Parse("PATCH");

        /// <summary>
        /// Announce the given resource.
        /// </summary>
        public static readonly HTTPMethod ANNOUNCE        = Parse("ANNOUNCE", IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Traverse the given resource.
        /// </summary>
        public static readonly HTTPMethod TRAVERSE        = Parse("TRAVERSE");

        /// <summary>
        /// Query a resource.
        /// </summary>
        public static readonly HTTPMethod QUERY           = Parse("QUERY");

        /// <summary>
        /// Composes a new resource (e.g. send a html form to compose a new resource)
        /// </summary>
        public static readonly HTTPMethod COMPOSE         = Parse("COMPOSE");

        /// <summary>
        /// SET the value of a resource (a replacement for PUT and POST)
        /// </summary>
        public static readonly HTTPMethod SET             = Parse("SET");

        /// <summary>
        /// RESET the value of a resource
        /// </summary>
        public static readonly HTTPMethod RESET           = Parse("RESET");

        /// <summary>
        /// Change the owner of a resource
        /// </summary>
        public static readonly HTTPMethod CHOWN           = Parse("CHOWN");

        /// <summary>
        /// Authenticate the given user/resource.
        /// </summary>
        public static readonly HTTPMethod AUTH            = Parse("AUTH");

        /// <summary>
        /// Deauthenticate the given user/resource.
        /// </summary>
        public static readonly HTTPMethod DEAUTH          = Parse("DEAUTH");

        /// <summary>
        /// Impersonate (become/switch to) the given user/resource.
        /// </summary>
        public static readonly HTTPMethod IMPERSONATE     = Parse("IMPERSONATE");

        /// <summary>
        /// Depersonate (switch back) from the given user/resource.
        /// </summary>
        public static readonly HTTPMethod DEPERSONATE     = Parse("DEPERSONATE");

        /// <summary>
        /// Update a resource (a replacement for PUT)
        /// </summary>
        public static readonly HTTPMethod UPDATE          = Parse("UPDATE");

        /// <summary>
        /// Edits a resource, e.g. return a HTML page for editing.
        /// </summary>
        public static readonly HTTPMethod EDIT            = Parse("EDIT");

        /// <summary>
        /// Monitors a resource or collection resource for modifications using an eventstream.
        /// </summary>
        public static readonly HTTPMethod MONITOR         = Parse("MONITOR");

        /// <summary>
        /// Maps all elements of a collection resource and may reduce this to a second data structure.
        /// This can be implemented via two JavaScript functions within the HTTP body.
        /// </summary>
        public static readonly HTTPMethod MAPREDUCE       = Parse("MAPREDUCE");

        /// <summary>
        /// Subscribe an URI to receive notifications from this resource.
        /// </summary>
        public static readonly HTTPMethod SUBSCRIBE       = Parse("SUBSCRIBE");

        /// <summary>
        /// Unsubscribe an URI to receive notifications from this resource.
        /// </summary>
        public static readonly HTTPMethod UNSUBSCRIBE     = Parse("UNSUBSCRIBE");

        /// <summary>
        /// Notify a subscriber of an URI about notifications from a resource.
        /// </summary>
        public static readonly HTTPMethod NOTIFY          = Parse("NOTIFY");

        /// <summary>
        /// Check a resource.
        /// </summary>
        public static readonly HTTPMethod CHECK           = Parse("CHECK");

        /// <summary>
        /// Clear a (collection) resource.
        /// </summary>
        public static readonly HTTPMethod CLEAR           = Parse("CLEAR");

        /// <summary>
        /// Signup a resource.
        /// </summary>
        public static readonly HTTPMethod SIGNUP          = Parse("SIGNUP");

        /// <summary>
        /// Validate a resource.
        /// </summary>
        public static readonly HTTPMethod VALIDATE        = Parse("VALIDATE");

        /// <summary>
        /// Mirror a resource.
        /// </summary>
        public static readonly HTTPMethod MIRROR          = Parse("MIRROR");

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as a HTTP method.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP method.</param>
        /// <param name="IsSafe">The HTTP method does not cause any changes or side-effects on the server-side.</param>
        /// <param name="IsIdempotent">The HTTP methods has no side-effects for multiple identical requests other as for a single request.</param>
        /// <param name="Description">An optional description of this HTTP method.</param>
        public static HTTPMethod Parse(String   Text,
                                       Boolean  IsSafe         = false,
                                       Boolean  IsIdempotent   = false,
                                       String?  Description    = null)
        {

            if (TryParse(Text,
                         out var httpMethod,
                         IsSafe,
                         IsIdempotent,
                         Description))
            {
                return httpMethod;
            }

            throw new ArgumentException($"Invalid text representation of a HTTP method: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a HTTP method.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP method.</param>
        /// <param name="IsSafe">The HTTP method does not cause any changes or side-effects on the server-side.</param>
        /// <param name="IsIdempotent">The HTTP methods has no side-effects for multiple identical requests other as for a single request.</param>
        /// <param name="Description">An optional description of this HTTP method.</param>
        public static HTTPMethod? TryParse(String   Text,
                                           Boolean  IsSafe,
                                           Boolean  IsIdempotent   = false,
                                           String?  Description    = null)
        {

            if (TryParse(Text,
                         out var httpMethod,
                         IsSafe,
                         IsIdempotent,
                         Description))
            {
                return httpMethod;
            }

            return null;

        }

        #endregion

        #region TryParse(Text, out HTTPMethod)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Tries to find the appropriate HTTPMethod for the given string.
        /// </summary>
        /// <param name="Text">A HTTP method name.</param>
        /// <param name="HTTPMethod">The parsed HTTP method.</param>
        public static Boolean TryParse(String          Text,
                                       out HTTPMethod  HTTPMethod)

            => TryParse(Text,
                        out HTTPMethod,
                        false,
                        false,
                        null);


        /// <summary>
        /// Tries to find the appropriate HTTPMethod for the given string.
        /// </summary>
        /// <param name="Text">A HTTP method name.</param>
        /// <param name="HTTPMethod">The parsed HTTP method.</param>
        /// <param name="IsSafe">The HTTP method does not cause any changes or side-effects on the server-side.</param>
        /// <param name="IsIdempotent">The HTTP methods has no side-effects for multiple identical requests other as for a single request.</param>
        /// <param name="Description">An optional description of this HTTP method.</param>
        public static Boolean TryParse(String          Text,
                                       out HTTPMethod  HTTPMethod,
                                       Boolean         IsSafe         = false,
                                       Boolean         IsIdempotent   = false,
                                       String?         Description    = null)
        {

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
            {
                HTTPMethod = default;
                return false;
            }

            if (httpMethods.TryGetValue(Text, out var httpMethod))
                HTTPMethod = httpMethod;

            else
            {

                HTTPMethod = new HTTPMethod(
                                 Text,
                                 IsSafe,
                                 IsIdempotent,
                                 Description
                             );

                httpMethods.TryAdd(Text,
                                   HTTPMethod);

            }

            return true;

        }

        #endregion


        #region (static) Register   (MethodName, IsSafe = false, IsIdempotent = false, Description = null)

        /// <summary>
        /// 
        /// </summary>
        /// <param name="MethodName">A HTTP method name.</param>
        /// <param name="IsSafe">The HTTP method does not cause any changes or side-effects on the server-side.</param>
        /// <param name="IsIdempotent">The HTTP methods has no side-effects for multiple identical requests other as for a single request.</param>
        /// <param name="Description">An optional description of this HTTP method.</param>
        public static HTTPMethod? Register(String   MethodName,
                                           Boolean  IsSafe         = false,
                                           Boolean  IsIdempotent   = false,
                                           String?  Description    = null)
        {

            MethodName = MethodName.Trim();

            if (MethodName.IsNullOrEmpty())
                return null;

            var httpMethod = new HTTPMethod(
                                     MethodName,
                                     IsSafe,
                                     IsIdempotent,
                                     Description
                                 );

            if (httpMethods.TryAdd(MethodName,
                                   httpMethod))
            {
                return httpMethod;
            }

            return null;

        }

        #endregion

        #region (static) TryRegister(MethodName, IsSafe = false, IsIdempotent = false, Description = null)

        /// <summary>
        /// 
        /// </summary>
        /// <param name="MethodName">A HTTP method name.</param>
        /// <param name="IsSafe">The HTTP method does not cause any changes or side-effects on the server-side.</param>
        /// <param name="IsIdempotent">The HTTP methods has no side-effects for multiple identical requests other as for a single request.</param>
        /// <param name="Description">An optional description of this HTTP method.</param>
        public static Boolean TryRegister(String   MethodName,
                                          Boolean  IsSafe         = false,
                                          Boolean  IsIdempotent   = false,
                                          String?  Description    = null)

            => httpMethods.TryAdd(MethodName,
                                  new HTTPMethod(
                                      MethodName,
                                      IsSafe,
                                      IsIdempotent,
                                      Description
                                  ));

        #endregion


        #region Operator overloading

        #region Operator == (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">A HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPMethod HTTPMethod1,
                                           HTTPMethod HTTPMethod2)

            => HTTPMethod1.Equals(HTTPMethod2);

        #endregion

        #region Operator != (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">A HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPMethod HTTPMethod1,
                                           HTTPMethod HTTPMethod2)

            => !HTTPMethod1.Equals(HTTPMethod2);

        #endregion

        #region Operator <  (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">A HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPMethod HTTPMethod1,
                                          HTTPMethod HTTPMethod2)

            => HTTPMethod1.CompareTo(HTTPMethod2) < 0;

        #endregion

        #region Operator <= (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">A HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPMethod HTTPMethod1,
                                           HTTPMethod HTTPMethod2)

            => HTTPMethod1.CompareTo(HTTPMethod2) <= 0;

        #endregion

        #region Operator >  (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">A HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPMethod HTTPMethod1,
                                          HTTPMethod HTTPMethod2)

            => HTTPMethod1.CompareTo(HTTPMethod2) > 0;

        #endregion

        #region Operator >= (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">A HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPMethod HTTPMethod1,
                                           HTTPMethod HTTPMethod2)

            => HTTPMethod1.CompareTo(HTTPMethod2) >= 0;

        #endregion

        #endregion

        #region IComparable<HTTPMethod> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPMethod httpMethod
                   ? CompareTo(httpMethod)
                   : throw new ArgumentException("The given object is not a HTTP method!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPMethod)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod">An object to compare with.</param>
        public Int32 CompareTo(HTTPMethod HTTPMethod)
        {

            var c = String.Compare(MethodName,
                                   HTTPMethod.MethodName,
                                   StringComparison.OrdinalIgnoreCase);

            if (c == 0)
                c = IsSafe.      CompareTo(HTTPMethod.IsSafe);

            if (c == 0)
                c = IsIdempotent.CompareTo(HTTPMethod.IsIdempotent);

            if (c == 0 && Description is not null && HTTPMethod.Description is not null)
                Description.     CompareTo(HTTPMethod.Description);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<HTTPMethod> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object? Object)

            => Object is HTTPMethod httpMethod &&
                   Equals(httpMethod);

        #endregion

        #region Equals(HTTPMethod)

        /// <summary>
        /// Compares two HTTPMethod for equality.
        /// </summary>
        /// <param name="HTTPMethod">A HTTPMethod to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPMethod HTTPMethod)

            => String.Equals(MethodName,
                             HTTPMethod.MethodName,
                             StringComparison.OrdinalIgnoreCase) &&

               IsSafe.      Equals(HTTPMethod.IsSafe)       &&
               IsIdempotent.Equals(HTTPMethod.IsIdempotent) &&

             ((Description is null     && HTTPMethod.Description is null) ||
              (Description is not null && HTTPMethod.Description is not null && Description.Equals(HTTPMethod.Description)));

        #endregion

        #endregion

        #region (override) GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => MethodName;

        #endregion


    }

}
