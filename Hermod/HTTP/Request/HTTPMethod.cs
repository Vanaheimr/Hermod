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

using System.Diagnostics;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP method.
    /// </summary>
    [DebuggerDisplay("{DebugView}")]
    public sealed class HTTPMethod : IEquatable<HTTPMethod>,
                                     IComparable<HTTPMethod>,
                                     IComparable
    {

        #region Data

        /// <summary>
        /// The registry of all well-known and explicitly declared HTTP methods.
        ///
        /// Note: This registry is only ever grown by code explicitly calling Register(...).
        ///       Parse(...) and TryParse(...) never add to it, therefore a remote peer can
        ///       not grow it without any bound, no matter what it sends over the wire.
        ///
        ///       Unknown HTTP methods are still parsed successfully. As equality, hash code
        ///       and comparison are all based on the method name, such a transient HTTP
        ///       method is fully interchangeable with a registered one.
        /// </summary>
        private readonly static ConcurrentDictionary<String, HTTPMethod>  lookup           = new (StringComparer.Ordinal);

        /// <summary>
        /// A regular expression to validate the HTTP method name.
        /// RFC 9110 §3.1.1: "The method token is case-sensitive and SHOULD be in uppercase, yet is not required to be.
        /// The method token is a sequence of characters that does not include any control characters or separators.
        /// Its syntax is defined as a token in Section 3.2.6 which includes the following characters: !#$%&'*+-.^_`|~0-9A-Za-z".
        /// </summary>
        private static readonly Regex                                     httpMethodRegex  = new (@"\A[!#$%&'*+.^_`|~0-9A-Za-z-]+\z",
                                                                                                  RegexOptions.CultureInvariant |
                                                                                                  RegexOptions.Compiled);

        #endregion

        #region Properties

        /// <summary>
        /// The name of the HTTP method.
        /// </summary>
        public String   MethodName      { get; }

        /// <summary>
        /// Whether this HTTP method causes any changes or side-effects on the server-side.
        /// </summary>
        public Boolean  IsSafe          { get; }

        /// <summary>
        /// Whether this HTTP methods has side-effects for multiple identical requests
        /// other as for a single request.
        /// </summary>
        public Boolean  IsIdempotent    { get; }

        /// <summary>
        /// The optional description of this HTTP method.
        /// </summary>
        public String?  Description     { get; }


        /// <summary>
        /// Indicates whether this HTTP method is null or empty.
        /// </summary>
        public Boolean  IsNullOrEmpty
            => MethodName.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this HTTP method is NOT null or empty.
        /// </summary>
        public Boolean  IsNotNullOrEmpty
            => MethodName.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the HTTP method.
        /// </summary>
        public UInt64   Length
            => (UInt64) (MethodName?.Length ?? 0);


        /// <summary>
        /// The number of currently registered HTTP methods.
        /// </summary>
        public static Int32 RegisteredCount
            => lookup.Count;

        #endregion

        #region (private) Constructor(s)

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
            this.IsIdempotent  = IsSafe || IsIdempotent;
            this.Description   = Description;

        }

        #endregion


        #region (static) Register(MethodName, IsSafe = false, IsIdempotent = false, Description = null)

        /// <summary>
        /// Register a new HTTP method with the given parameters.
        ///
        /// Note: This is the only way to grow the registry of well-known HTTP methods and is
        ///       meant for code declaring its own methods. Never call it with data received
        ///       over the wire, use Parse(...)/TryParse(...) for that!
        ///
        ///       When the given method is already registered, the already registered method
        ///       is returned unchanged and the other parameters are ignored.
        /// </summary>
        /// <param name="MethodName">An HTTP method name.</param>
        /// <param name="IsSafe">The HTTP method does not cause any changes or side-effects on the server-side.</param>
        /// <param name="IsIdempotent">The HTTP methods has no side-effects for multiple identical requests other as for a single request.</param>
        /// <param name="Description">An optional description of this HTTP method.</param>
        public static HTTPMethod Register(String   MethodName,
                                          Boolean  IsSafe         = false,
                                          Boolean  IsIdempotent   = false,
                                          String?  Description    = null)
        {

            if (MethodName is null || !httpMethodRegex.IsMatch(MethodName))
                throw new ArgumentException($"Invalid HTTP method name: '{MethodName}'!",
                                            nameof(MethodName));

            return lookup.GetOrAdd(
                       MethodName,
                       static (methodName, parameters) => new HTTPMethod(
                                                              methodName,
                                                              parameters.IsSafe,
                                                              parameters.IsIdempotent,
                                                              parameters.Description
                                                          ),
                       (IsSafe, IsIdempotent, Description)
                   );

        }

        #endregion


        #region RFC 9110 - HTTP/1.1

        /// <summary>
        /// Establish a tunnel to the given resource.
        /// </summary>
        public static HTTPMethod  CONNECT           { get; }

            = Register(
                 "CONNECT",
                  Description:  "Establish a tunnel to the given resource"
              );


        /// <summary>
        /// Delete the given resource.
        /// </summary>
        public static HTTPMethod  DELETE            { get; }

            = Register(
                 "DELETE",
                  IsIdempotent:  true,
                  Description:  "Delete the given resource"
              );


        /// <summary>
        /// Retrieve the given resource.
        /// </summary>
        public static HTTPMethod  GET               { get; }

            = Register(
                 "GET",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Retrieve the given resource"
              );

        /// <summary>
        /// Return the headers of the given resource.
        /// </summary>
        public static HTTPMethod  HEAD              { get; }

            = Register(
                 "HEAD",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Return the headers of the given resource"
              );

        /// <summary>
        /// Return the allowed HTTP methods for the given resource.
        /// </summary>
        public static HTTPMethod  OPTIONS           { get; }

            = Register(
                 "OPTIONS",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Return the allowed HTTP methods for the given resource"
              );

        /// <summary>
        /// Create a new resource or update an existing resource with the given data.
        /// </summary>
        public static HTTPMethod  POST              { get; }

            = Register(
                 "POST",
                  Description:  "Create a new resource or update an existing resource with the given data"
              );


        /// <summary>
        /// Update an existing resource with the given data.
        /// </summary>
        public static HTTPMethod  PUT               { get; }

            = Register(
                 "PUT",
                  IsIdempotent:  true,
                  Description:  "Update an existing resource with the given data"
              );


        /// <summary>
        /// Request a response identical to that of a GET request, but without the response body.
        /// </summary>
        public static HTTPMethod  TRACE             { get; }

            = Register(
                 "TRACE",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Request a response identical to that of a GET request, but without the response body"
              );


        /// <summary>
        /// Request a response identical to that of a GET request, but without the response body.
        /// </summary>
        public static HTTPMethod  QUERY             { get; }

            = Register(
                 "QUERY",
                 IsIdempotent:  true,
                 IsSafe:        true,
                 Description:  "Request a response identical to that of a GET request, but without the response body"
              );

        #endregion

        #region RFC 4918 - WebDAV

        /// <summary>
        /// Copy the given resource to another location.
        /// </summary>
        public static HTTPMethod  COPY              { get; }

            = Register(
                 "COPY",
                 IsIdempotent:  true,
                 Description:  "Copy the given resource to another location"
              );


        /// <summary>
        /// Lock the given resource.
        /// </summary>
        public static HTTPMethod  LOCK              { get; }

            = Register(
                 "LOCK",
                  Description:  "Lock the given resource"
              );


        /// <summary>
        /// Create a new collection resource.
        /// </summary>
        public static HTTPMethod  MKCOL             { get; }

            = Register(
                 "MKCOL",
                  IsIdempotent:  true,
                  Description:  "Create a new collection resource"
              );


        /// <summary>
        /// Move the given resource to another location.
        /// </summary>
        public static HTTPMethod  MOVE              { get; }

            = Register(
                 "MOVE",
                  IsIdempotent:  true,
                  Description:  "Move the given resource to another location"
              );


        /// <summary>
        /// Retrieve properties of the given resource.
        /// </summary>
        public static HTTPMethod  PROPFIND          { get; }

            = Register(
                 "PROPFIND",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Retrieve properties of the given resource"
              );


        /// <summary>
        /// Update properties of the given resource.
        /// </summary>
        public static HTTPMethod  PROPPATCH         { get; }
            = Register(
                 "PROPPATCH",
                  IsIdempotent:  true,
                  Description:  "Update properties of the given resource"
              );


        /// <summary>
        /// Unlock the given resource.
        /// </summary>
        public static HTTPMethod  UNLOCK            { get; }

            = Register(
                 "UNLOCK",
                  IsIdempotent:  true,
                  Description:  "Unlock the given resource"
              );

        #endregion

        #region Additional methods

        /// <summary>
        /// Similar to SEARCH, searches for matching items, but might filter or sort those items differently.
        /// </summary>
        public static HTTPMethod  SEARCH            { get; }

            = Register(
                 "SEARCH",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Similar to SEARCH, searches for matching items, but might filter or sort those items differently"
              );


        /// <summary>
        /// Similar to GET, checks wether a resource exists, but only returns 'true' or 'false'.
        /// </summary>
        public static HTTPMethod  EXISTS            { get; }
            = Register(
                 "EXISTS",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Similar to GET, checks wether a resource exists, but only returns 'true' or 'false'"
              );


        /// <summary>
        /// Counts the number of elements in a resource collection.
        /// </summary>
        public static HTTPMethod  COUNT             { get; }

            = Register(
                 "COUNT",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Counts the number of elements in a resource collection"
              );


        /// <summary>
        /// Similar to GET, but with an additional filter methods within the http body.
        /// </summary>
        public static HTTPMethod  FILTER            { get; }

            = Register(
                 "FILTER",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Similar to GET, but with an additional filter methods within the http body"
              );


        /// <summary>
        /// Returns dynamic status information on a single resource or an entire resource collection.
        /// </summary>
        public static HTTPMethod  STATUS            { get; }

            = Register(
                 "STATUS",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Returns dynamic status information on a single resource or an entire resource collection"
              );


        /// <summary>
        /// Creates a new resource. Within a resource collection the unique
        /// identification of the new resource will be chosen by the server.
        /// </summary>
        public static HTTPMethod  CREATE            { get; }

            = Register(
                 "CREATE",
                  Description:  "Creates a new resource. Within a resource collection the unique identification of the new resource will be chosen by the server"
              );


        /// <summary>
        /// Adds a new resource to a resource collection. It will fail when
        /// a unique identification of the resource is missing or already
        /// exists on the server.
        /// </summary>
        public static HTTPMethod  ADD               { get; }

            = Register(
                 "ADD",
                  Description:  "Adds a new resource to a resource collection. It will fail when a unique identification of the resource is missing or already exists on the server"
              );


        /// <summary>
        /// Adds a new resource to a resource collection. The request will be silently
        /// ignored when the unique identification of the resource already exists on
        /// the server.
        /// </summary>
        public static HTTPMethod  ADDIFNOTEXISTS    { get; }

            = Register(
                 "ADDIFNOTEXISTS",
                  Description:  "Adds a new resource to a resource collection. The request will be silently ignored when the unique identification of the resource already exists on the server"
              );



        /// <summary>
        /// Patch the given resource.
        /// </summary>
        public static HTTPMethod  PATCH             { get; }

            = Register(
                 "PATCH",
                  Description:  "Patch the given resource"
              );


        /// <summary>
        /// Announce the given resource.
        /// </summary>
        public static HTTPMethod  ANNOUNCE          { get; }

            = Register(
                 "ANNOUNCE",
                  IsIdempotent:  true,
                  IsSafe:        true,
                  Description:  "Announce the given resource"
              );


        /// <summary>
        /// Traverse the given resource.
        /// </summary>
        public static HTTPMethod  TRAVERSE          { get; }

            = Register(
                 "TRAVERSE",
                  Description:  "Traverse the given resource"
              );


        /// <summary>
        /// Composes a new resource (e.g. send a html form to compose a new resource)
        /// </summary>
        public static HTTPMethod  COMPOSE           { get; }

            = Register(
                 "COMPOSE",
                  Description:  "Composes a new resource (e.g. send a html form to compose a new resource)"
              );


        /// <summary>
        /// SET the value of a resource (a replacement for PUT and POST)
        /// </summary>
        public static HTTPMethod  SET               { get; }

            = Register(
                 "SET",
                  Description:  "SET the value of a resource (a replacement for PUT and POST)"
              );


        /// <summary>
        /// RESET the value of a resource
        /// </summary>
        public static HTTPMethod  RESET             { get; }

            = Register(
                 "RESET",
                  Description:  "RESET the value of a resource"
              );


        /// <summary>
        /// Change the owner of a resource
        /// </summary>
        public static HTTPMethod  CHOWN             { get; }

            = Register(
                 "CHOWN",
                  Description:  "Change the owner of a resource"
              );


        /// <summary>
        /// Authenticate the given user/resource.
        /// </summary>
        public static HTTPMethod  AUTH              { get; }

            = Register(
                 "AUTH",
                  Description:  "Authenticate the given user/resource"
              );


        /// <summary>
        /// Deauthenticate the given user/resource.
        /// </summary>
        public static HTTPMethod  DEAUTH            { get; }

            = Register(
                 "DEAUTH",
                  Description:  "Deauthenticate the given user/resource"
              );


        /// <summary>
        /// Impersonate (become/switch to) the given user/resource.
        /// </summary>
        public static HTTPMethod  IMPERSONATE       { get; }

            = Register(
                 "IMPERSONATE",
                  Description:  "Impersonate (become/switch to) the given user/resource"
              );


        /// <summary>
        /// Depersonate (switch back) from the given user/resource.
        /// </summary>
        public static HTTPMethod  DEPERSONATE       { get; }

            = Register(
                 "DEPERSONATE",
                  Description:  "Depersonate (switch back) from the given user/resource"
              );


        /// <summary>
        /// Update a resource (a replacement for PUT)
        /// </summary>
        public static HTTPMethod  UPDATE            { get; }

            = Register(
                 "UPDATE",
                  IsIdempotent:  true,
                  Description:  "Update a resource (a replacement for PUT)"
              );


        /// <summary>
        /// Edits a resource, e.g. return a HTML page for editing.
        /// </summary>
        public static HTTPMethod  EDIT              { get; }

            = Register(
                 "EDIT",
                  Description:  "Edits a resource, e.g. return a HTML page for editing"
              );


        /// <summary>
        /// Monitors a resource or collection resource for modifications using an eventstream.
        /// </summary>
        public static HTTPMethod  MONITOR           { get; }

            = Register(
                 "MONITOR",
                  Description:  "Monitors a resource or collection resource for modifications using an eventstream"
              );


        /// <summary>
        /// Maps all elements of a collection resource and may reduce this to a second data structure.
        /// This can be implemented via two JavaScript functions within the HTTP body.
        /// </summary>
        public static HTTPMethod  MAPREDUCE         { get; }

            = Register(
                 "MAPREDUCE",
                  Description:  "Maps all elements of a collection resource and may reduce this to a second data structure. This can be implemented via two JavaScript functions within the HTTP body."
              );


        /// <summary>
        /// Subscribe an URI to receive notifications from this resource.
        /// </summary>
        public static HTTPMethod  SUBSCRIBE         { get; }

            = Register(
                 "SUBSCRIBE",
                  Description:  "Subscribe an URI to receive notifications from this resource"
              );


        /// <summary>
        /// Unsubscribe an URI to receive notifications from this resource.
        /// </summary>
        public static HTTPMethod  UNSUBSCRIBE       { get; }

            = Register(
                 "UNSUBSCRIBE",
                  Description:  "Unsubscribe an URI to receive notifications from this resource"
              );


        /// <summary>
        /// Notify a subscriber of an URI about notifications from a resource.
        /// </summary>
        public static HTTPMethod  NOTIFY            { get; }

            = Register(
                 "NOTIFY",
                  Description:  "Notify a subscriber of an URI about notifications from a resource"
              );


        /// <summary>
        /// Check a resource.
        /// </summary>
        public static HTTPMethod  CHECK             { get; }

            = Register(
                 "CHECK",
                  Description:  "Check a resource"
              );


        /// <summary>
        /// Clear a (collection) resource.
        /// </summary>
        public static HTTPMethod  CLEAR             { get; }

            = Register(
                 "CLEAR",
                  Description:  "Clear a (collection) resource"
              );


        /// <summary>
        /// Signup a resource.
        /// </summary>
        public static HTTPMethod  SIGNUP            { get; }

            = Register(
                 "SIGNUP",
                  Description:  "Signup a resource"
              );


        /// <summary>
        /// Validate a resource.
        /// </summary>
        public static HTTPMethod  VALIDATE          { get; }

            = Register(
                 "VALIDATE",
                  Description:  "Validate a resource"
              );


        /// <summary>
        /// Mirror a resource.
        /// </summary>
        public static HTTPMethod  MIRROR            { get; }

            = Register(
                 "MIRROR",
                  Description:  "Mirror a resource"
              );

        #endregion


        #region Parse    (Text)

        /// <summary>
        /// Parse the given string as a HTTP method.
        ///
        /// Note: This never adds an unknown HTTP method to the registry, use Register(...) for that.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP method.</param>
        public static HTTPMethod Parse(String? Text)
        {

            if (TryParse(Text, out var httpMethod, out _))
                return httpMethod;

            throw new ArgumentException($"Invalid text representation of a HTTP method: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a HTTP method.
        ///
        /// Note: This never adds an unknown HTTP method to the registry, use Register(...) for that.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP method.</param>
        public static HTTPMethod? TryParse(String? Text)
        {

            if (TryParse(Text, out var httpMethod, out _))
                return httpMethod;

            return null;

        }

        #endregion

        #region TryParse (Text, out HTTPMethod, out ErrorResponse)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Tries to find the appropriate HTTPMethod for the given string.
        ///
        /// Note: This never adds an unknown HTTP method to the registry, use Register(...) for that.
        ///       Therefore this is safe to call with anything received over the wire.
        /// </summary>
        /// <param name="Text">An HTTP method name.</param>
        /// <param name="HTTPMethod">The parsed HTTP method.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(String?                               Text,
                                       [NotNullWhen(true)]  out HTTPMethod?  HTTPMethod,
                                       [NotNullWhen(false)] out String?      ErrorResponse)
        {

            // Note: HTTP method names are case-sensitive, see RFC 9110 section 9.1,
            //       therefore they must not be normalized here.
            if (Text is null || !httpMethodRegex.IsMatch(Text))
            {
                HTTPMethod     = null;
                ErrorResponse  = $"Invalid HTTP method name: '{Text}'!";
                return false;
            }

            if (!lookup.TryGetValue(Text, out HTTPMethod))
                HTTPMethod = new HTTPMethod(Text);

            ErrorResponse = null;
            return true;

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">An HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPMethod? HTTPMethod1,
                                           HTTPMethod? HTTPMethod2)
        {

            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(HTTPMethod1, HTTPMethod2))
                return true;

            // If one is null, but not both, return false.
            if (HTTPMethod1 is null || HTTPMethod2 is null)
                return false;

            return HTTPMethod1.Equals(HTTPMethod2);

        }

        #endregion

        #region Operator != (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">An HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPMethod? HTTPMethod1,
                                           HTTPMethod? HTTPMethod2)

            => !(HTTPMethod1 == HTTPMethod2);

        #endregion

        #region Operator <  (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">An HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPMethod? HTTPMethod1,
                                          HTTPMethod? HTTPMethod2)

            => Compare(HTTPMethod1, HTTPMethod2) < 0;

        #endregion

        #region Operator <= (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">An HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPMethod? HTTPMethod1,
                                           HTTPMethod? HTTPMethod2)

            => Compare(HTTPMethod1, HTTPMethod2) <= 0;

        #endregion

        #region Operator >  (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">An HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPMethod? HTTPMethod1,
                                          HTTPMethod? HTTPMethod2)

            => Compare(HTTPMethod1, HTTPMethod2) > 0;

        #endregion

        #region Operator >= (HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod1">An HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPMethod? HTTPMethod1,
                                           HTTPMethod? HTTPMethod2)

            => Compare(HTTPMethod1, HTTPMethod2) >= 0;

        #endregion

        #endregion

        #region IComparable<HTTPMethod> Members

        #region (private static) Compare(HTTPMethod1, HTTPMethod2)

        /// <summary>
        /// Compare two HTTP methods, treating null as smaller than any HTTP method.
        /// This keeps all comparison operators consistent with CompareTo(null) == 1.
        /// </summary>
        /// <param name="HTTPMethod1">An HTTP method.</param>
        /// <param name="HTTPMethod2">Another HTTP method.</param>
        private static Int32 Compare(HTTPMethod? HTTPMethod1,
                                     HTTPMethod? HTTPMethod2)

            => ReferenceEquals(HTTPMethod1, HTTPMethod2)
                   ?  0
                   : HTTPMethod1 is null
                         ? -1
                         : HTTPMethod1.CompareTo(HTTPMethod2);

        #endregion

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP methods.
        /// </summary>
        /// <param name="Object">An HTTP method to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPMethod httpMethod
                   ? CompareTo(httpMethod)
                   : throw new ArgumentException("The given object is not a HTTP method!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPMethod)

        /// <summary>
        /// Compares two HTTP methods.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method to compare with.</param>
        public Int32 CompareTo(HTTPMethod? HTTPMethod)

            => HTTPMethod is null
                   ? 1
                   : StringComparer.Ordinal.Compare(
                         MethodName,
                         HTTPMethod.MethodName
                     );

        #endregion

        #endregion

        #region IEquatable<HTTPMethod> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP methods for equality.
        /// </summary>
        /// <param name="Object">An HTTP method to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPMethod httpMethod &&
                   Equals(httpMethod);

        #endregion

        #region Equals(HTTPMethod)

        /// <summary>
        /// Compares two HTTP methods for equality.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method to compare with.</param>
        public Boolean Equals(HTTPMethod? HTTPMethod)

            => HTTPMethod is not null &&

               String.Equals(MethodName,
                             HTTPMethod.MethodName,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => StringComparer.Ordinal.GetHashCode(MethodName);

        #endregion

        #region DebugView()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public String DebugView

            => String.Concat(

                   MethodName,

                   IsSafe
                       ? " (safe)"
                       : String.Empty,

                   IsIdempotent
                       ? " (idempotent)"
                       : String.Empty,

                   Description.IsNotNullOrEmpty()
                       ? $": '{Description}'"
                       : String.Empty

               );

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
