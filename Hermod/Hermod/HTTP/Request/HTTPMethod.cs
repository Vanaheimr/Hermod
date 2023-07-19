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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Supported HTTP methods, but only used for HTTP mapping attributes!
    /// Internally the HTTPMethod class is used.
    /// </summary>
    public enum HTTPMethods
    {

        UNKNOWN,

        #region RFC 2616 - HTTP/1.1

        CONNECT,

        /// <summary>
        /// Delete the given resource.
        /// </summary>
        DELETE,

        /// <summary>
        /// Return the given resource.
        /// </summary>
        GET,

        /// <summary>
        /// Return only the headers (not including the body) of the given resource.
        /// </summary>
        HEAD,

        /// <summary>
        /// Return a list of valid HTTP verbs for the given resource.
        /// </summary>
        OPTIONS,

        POST,
        PUT,
        TRACE,

        #endregion

        #region RFC 4918 - WebDAV

        COPY,
        LOCK,
        MKCOL,     
        MOVE,
        PROPFIND,
        PROPPATCH,
        UNLOCK,

        #endregion

        #region Additional methods

        /// <summary>
        /// Patch the given resource.
        /// </summary>
        PATCH,

        /// <summary>
        /// Traverse the given resource.
        /// </summary>
        TRAVERSE,
        
        /// <summary>
        /// Similar to a GET request, but with an additional filter methods within the http body.
        /// This can be implemented via a JavaScript function within the HTTP body.
        /// </summary>
        FILTER,

        /// <summary>
        /// Counts the number of elements which would be returned by a GET or FILTER request.
        /// </summary>
        COUNT,

        /// <summary>
        /// Composes a new resource (e.g. send a html form to compose a new resource).
        /// </summary>
        COMPOSE,

        /// <summary>
        /// Creates a new resource (a replacement for PUT and POST).
        /// </summary>
        CREATE,

        /// <summary>
        /// Add a new resource to a collection of resources (a replacement for PUT and POST).
        /// </summary>
        ADD,

        /// <summary>
        /// Update a resource (a replacement for PUT).
        /// </summary>
        UPDATE,

        /// <summary>
        /// Edits a resource, e.g. return a HTML page for editing.
        /// </summary>
        EDIT,

        /// <summary>
        /// Monitors a resource or collection resource for modifications using an eventstream.
        /// </summary>
        MONITOR,

        /// <summary>
        /// Maps all elements of a collection resource and may reduce this to a second data structure.
        /// This can be implemented via two JavaScript functions within the HTTP body.
        /// </summary>
        MAPREDUCE,

        /// <summary>
        /// Subscribe an URI to receive notifications from this resource.
        /// </summary>
        SUBSCRIBE,

        /// <summary>
        /// Unsubscribe an URI to receive notifications from this resource.
        /// </summary>
        UNSUBSCRIBE,

        /// <summary>
        /// Notify a subscriber of an URI about notifications from a resource.
        /// </summary>
        NOTIFY,

        /// <summary>
        /// Check a resource.
        /// </summary>
        CHECK

        #endregion

    }

/*
 * Currently this will not work really great with method attributes :(!
 * 

    //Safe Methods

    // Implementors should be aware that the software represents the user in their interactions over the Internet, and should be careful to allow the
    // user to be aware of any actions they might take which may have an unexpected significance to themselves or others.

    // In particular, the convention has been established that the GET and HEAD methods SHOULD NOT have the significance of taking an action other than
    // retrieval. These methods ought to be considered "safe". This allows user agents to represent other methods, such as POST, PUT and DELETE, in a
    // special way, so that the user is made aware of the fact that a possibly unsafe action is being requested.

    // Naturally, it is not possible to ensure that the server does not generate side-effects as a result of performing a GET request; in fact, some
    // dynamic resources consider that a feature. The important distinction here is that the user did not request the side-effects, so therefore
    // cannot be held accountable for them.


    //Idempotent Methods

    // Methods can also have the property of "idempotence" in that (aside from error or expiration issues) the side-effects of N > 0 identical requests is the same as for a single request.
    // The methods GET, HEAD, PUT and DELETE share this property. Also, the methods OPTIONS and TRACE SHOULD NOT have side effects, and so are inherently idempotent.

    //However, it is possible that a sequence of several requests is non-idempotent, even if all of the methods executed in that sequence are idempotent.
    //(A sequence is idempotent if a single execution of the entire sequence always yields a result that is not changed by a reexecution of all, or part, of that sequence.)
    //For example, a sequence is non-idempotent if its result depends on a value that is later modified in the same sequence.

    //A sequence that never has side effects is idempotent, by definition (provided that no concurrent operations are being executed on the same set of resources).
*/

    /// <summary>
    /// HTTP methods
    /// </summary>
    public readonly struct HTTPMethod : IEquatable<HTTPMethod>,
                                        IComparable<HTTPMethod>,
                                        IComparable
    {

        #region Properties

        /// <summary>
        /// The name of the HTTP method.
        /// </summary>
        public String   MethodName      { get; }

        /// <summary>
        /// IsSafe
        /// </summary>
        public Boolean  IsSafe          { get; }

        /// <summary>
        /// This HTTP methods has no side-effects for N > 0 identical
        /// requests, as it is the same as for a single request.
        /// </summary>
        public Boolean  IsIdempotent    { get; }

        /// <summary>
        /// The description of this HTTP method.
        /// </summary>
        public String?  Description     { get; }


        /// <summary>
        /// Indicates whether this HTTP method is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty

            => MethodName.IsNullOrEmpty();

        /// <summary>
        /// The length of the HTTP method.
        /// </summary>
        public UInt64 Length

            => (UInt64) (MethodName?.Length ?? 0);

        #endregion

        #region (private) Constructor(s)

        /// <summary>
        /// Creates a new HTTP method based on the given parameters.
        /// </summary>
        /// <param name="MethodName">The name of the HTTP method.</param>
        /// <param name="IsSafe"></param>
        /// <param name="IsIdempotent">This HTTP methods has no side-effects for N > 0 identical requests, as it is the same as for a single request.</param>
        /// <param name="Description">The description of this HTTP method.</param>
        public HTTPMethod(String   MethodName,
                          Boolean  IsSafe         = false,
                          Boolean  IsIdempotent   = false,
                          String?  Description    = null)
        {

            this.MethodName    = MethodName;
            this.IsSafe        = IsSafe;
            this.IsIdempotent  = IsIdempotent;
            this.Description   = Description;

        }

        #endregion


        #region RFC 2616 - HTTP/1.1

        public static readonly HTTPMethod CONNECT       = new HTTPMethod("CONNECT");

        /// <summary>
        /// Delete the given resource.
        /// </summary>
        public static readonly HTTPMethod DELETE        = new HTTPMethod("DELETE",  IsIdempotent: true);

        /// <summary>
        /// Return the given resource.
        /// </summary>
        public static readonly HTTPMethod GET           = new HTTPMethod("GET",     IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Return only the headers (not including the body) of the given resource.
        /// </summary>
        public static readonly HTTPMethod HEAD          = new HTTPMethod("HEAD",    IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Return a list of valid HTTP verbs for the given resource.
        /// </summary>
        public static readonly HTTPMethod OPTIONS       = new HTTPMethod("OPTIONS", IsIdempotent: true);

        public static readonly HTTPMethod POST          = new HTTPMethod("POST");

        public static readonly HTTPMethod PUT           = new HTTPMethod("PUT",     IsIdempotent: true);

        public static readonly HTTPMethod TRACE         = new HTTPMethod("TRACE",   IsIdempotent: true);

        #endregion

        #region RFC 4918 - WebDAV

        public static readonly HTTPMethod COPY          = new HTTPMethod("COPY");
        public static readonly HTTPMethod LOCK          = new HTTPMethod("LOCK");
        public static readonly HTTPMethod MKCOL         = new HTTPMethod("MKCOL");
        public static readonly HTTPMethod MOVE          = new HTTPMethod("MOVE");
        public static readonly HTTPMethod PROPFIND      = new HTTPMethod("PROPFIND");
        public static readonly HTTPMethod PROPPATCH     = new HTTPMethod("PROPPATCH");
        public static readonly HTTPMethod UNLOCK        = new HTTPMethod("UNLOCK");

        #endregion

        #region Additional methods

        /// <summary>
        /// Similar to SEARCH, searches for matching items, but might filter or sort those items differently.
        /// </summary>
        public static readonly HTTPMethod SEARCH          = new ("SEARCH",   IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Similar to GET, checks wether a resource exists, but only returns 'true' or 'false'.
        /// </summary>
        public static readonly HTTPMethod EXISTS          = new ("EXISTS",   IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Counts the number of elements in a resource collection.
        /// </summary>
        public static readonly HTTPMethod COUNT           = new ("COUNT",    IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Similar to GET, but with an additional filter methods within the http body.
        /// </summary>
        public static readonly HTTPMethod FILTER          = new ("FILTER",   IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Returns dynamic status information on a single resource or an entire resource collection.
        /// </summary>
        public static readonly HTTPMethod STATUS          = new ("STATUS",   IsIdempotent: true, IsSafe: true);


        /// <summary>
        /// Creates a new resource. Within a resource collection the unique
        /// identification of the new resource will be chosen by the server.
        /// </summary>
        public static readonly HTTPMethod CREATE          = new ("CREATE");

        /// <summary>
        /// Adds a new resource to a resource collection. It will fail when
        /// a unique identification of the resource is missing or already
        /// exists on the server.
        /// </summary>
        public static readonly HTTPMethod ADD             = new ("ADD");

        /// <summary>
        /// Adds a new resource to a resource collection. The request will be silently
        /// ignored when the unique identification of the resource already exists on
        /// the server.
        /// </summary>
        public static readonly HTTPMethod ADDIFNOTEXISTS  = new ("ADDIFNOTEXISTS");



        /// <summary>
        /// Patch the given resource.
        /// </summary>
        public static readonly HTTPMethod PATCH           = new ("PATCH");

        /// <summary>
        /// Announce the given resource.
        /// </summary>
        public static readonly HTTPMethod ANNOUNCE        = new ("ANNOUNCE", IsIdempotent: true, IsSafe: true);

        /// <summary>
        /// Traverse the given resource.
        /// </summary>
        public static readonly HTTPMethod TRAVERSE        = new ("TRAVERSE");

        /// <summary>
        /// Query a resource.
        /// </summary>
        public static readonly HTTPMethod QUERY           = new ("QUERY");

        /// <summary>
        /// Composes a new resource (e.g. send a html form to compose a new resource)
        /// </summary>
        public static readonly HTTPMethod COMPOSE         = new ("COMPOSE");

        /// <summary>
        /// SET the value of a resource (a replacement for PUT and POST)
        /// </summary>
        public static readonly HTTPMethod SET             = new ("SET");

        /// <summary>
        /// Change the owner of a resource
        /// </summary>
        public static readonly HTTPMethod CHOWN           = new ("CHOWN");

        /// <summary>
        /// Authenticate the given user/resource.
        /// </summary>
        public static readonly HTTPMethod AUTH            = new ("AUTH");

        /// <summary>
        /// Deauthenticate the given user/resource.
        /// </summary>
        public static readonly HTTPMethod DEAUTH          = new ("DEAUTH");

        /// <summary>
        /// Impersonate (become/switch to) the given user/resource.
        /// </summary>
        public static readonly HTTPMethod IMPERSONATE     = new ("IMPERSONATE");

        /// <summary>
        /// Depersonate (switch back) from the given user/resource.
        /// </summary>
        public static readonly HTTPMethod DEPERSONATE     = new ("DEPERSONATE");

        /// <summary>
        /// Update a resource (a replacement for PUT)
        /// </summary>
        public static readonly HTTPMethod UPDATE          = new ("UPDATE");

        /// <summary>
        /// Edits a resource, e.g. return a HTML page for editing.
        /// </summary>
        public static readonly HTTPMethod EDIT            = new ("EDIT");

        /// <summary>
        /// Monitors a resource or collection resource for modifications using an eventstream.
        /// </summary>
        public static readonly HTTPMethod MONITOR         = new ("MONITOR");

        /// <summary>
        /// Maps all elements of a collection resource and may reduce this to a second data structure.
        /// This can be implemented via two JavaScript functions within the HTTP body.
        /// </summary>
        public static readonly HTTPMethod MAPREDUCE       = new ("MAPREDUCE");

        /// <summary>
        /// Subscribe an URI to receive notifications from this resource.
        /// </summary>
        public static readonly HTTPMethod SUBSCRIBE       = new ("SUBSCRIBE");

        /// <summary>
        /// Unsubscribe an URI to receive notifications from this resource.
        /// </summary>
        public static readonly HTTPMethod UNSUBSCRIBE     = new ("UNSUBSCRIBE");

        /// <summary>
        /// Notify a subscriber of an URI about notifications from a resource.
        /// </summary>
        public static readonly HTTPMethod NOTIFY          = new ("NOTIFY");

        /// <summary>
        /// Check a resource.
        /// </summary>
        public static readonly HTTPMethod CHECK           = new ("CHECK");

        /// <summary>
        /// Clear a (collection) resource.
        /// </summary>
        public static readonly HTTPMethod CLEAR           = new ("CLEAR");

        /// <summary>
        /// Signup a resource.
        /// </summary>
        public static readonly HTTPMethod SIGNUP          = new ("SIGNUP");

        /// <summary>
        /// Validate a resource.
        /// </summary>
        public static readonly HTTPMethod VALIDATE        = new ("VALIDATE");

        #endregion

 
        #region Parse   (Text)

        /// <summary>
        /// Tries to find the appropriate HTTP method for the given string representation.
        /// </summary>
        /// <param name="Text">A string representation of a HTTP method.</param>
        /// <returns>A HTTP method.</returns>
        public static HTTPMethod Parse(String Text)

            => (from   fieldInfo in typeof(HTTPMethod).GetFields()
                let    httpMethod = (HTTPMethod) fieldInfo.GetValue(null)
                where  httpMethod.MethodName == Text

                select httpMethod).FirstOrDefault();

        #endregion

        #region Parse   (HTTPMethodEnum)

        /// <summary>
        /// Tries to find the appropriate HTTP method for the given HTTP methods.
        /// </summary>
        /// <param name="HTTPMethodEnum">A HTTP method code as string</param>
        /// <returns>A HTTP method</returns>
        public static HTTPMethod Parse(HTTPMethods HTTPMethodEnum)

            => (from   fieldInfo in typeof(HTTPMethod).GetFields()
                let    httpMethod = (HTTPMethod) fieldInfo.GetValue(null)
                where  httpMethod.MethodName == HTTPMethodEnum.ToString()
                select httpMethod).FirstOrDefault();

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a HTTP method.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP method.</param>
        public static HTTPMethod? TryParse(String Text)
        {

            if (TryParse(Text, out var httpMethod))
                return httpMethod!;

            return null;

        }

        #endregion

        #region TryParse(HTTPMethodEnum)

        /// <summary>
        /// Try to parse the given text as a HTTP method.
        /// </summary>
        /// <param name="HTTPMethodEnum">A HTTP method.</param>
        public static HTTPMethod? TryParse(HTTPMethods HTTPMethodEnum)
        {

            if (TryParse(HTTPMethodEnum, out var httpMethod))
                return httpMethod!;

            return null;

        }

        #endregion

        #region TryParse(Text,           out HTTPMethod)

        /// <summary>
        /// Tries to find the appropriate HTTPMethod for the given string.
        /// </summary>
        /// <param name="Text">A HTTP method name.</param>
        /// <param name="HTTPMethod">The parsed HTTP method.</param>
        /// <returns>true or false</returns>
        public static Boolean TryParse(String Text, out HTTPMethod HTTPMethod)
        {

            HTTPMethod = (from   fieldInfo in typeof(HTTPMethod).GetFields()
                          let    httpMethod = (HTTPMethod) fieldInfo.GetValue(null)
                          where  httpMethod.MethodName == Text
                          select httpMethod).FirstOrDefault();

            if (HTTPMethod.MethodName.IsNullOrEmpty())
                HTTPMethod = new HTTPMethod(Text);

            return true;

        }

        #endregion

        #region TryParse(HTTPMethodEnum, out HTTPMethod)

        /// <summary>
        /// Tries to find the appropriate HTTPMethod for the given HTTPMethods.
        /// </summary>
        /// <param name="HTTPMethodEnum">A HTTP method.</param>
        /// <param name="HTTPMethod">The parsed HTTP method.</param>
        /// <returns>true or false</returns>
        public static Boolean TryParse(HTTPMethods HTTPMethodEnum, out HTTPMethod HTTPMethod)
        {

            HTTPMethod = (from   fieldInfo in typeof(HTTPMethod).GetFields()
                          let    httpMethod = (HTTPMethod) fieldInfo.GetValue(null)
                          where  httpMethod.MethodName == HTTPMethodEnum.ToString()
                          select httpMethod).FirstOrDefault();

            if (HTTPMethod.MethodName.IsNullOrEmpty())
                HTTPMethod = new HTTPMethod(HTTPMethodEnum.ToString());

            return true;

        }

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
        public Int32 CompareTo(Object Object)

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

            => String.Compare(MethodName,
                              HTTPMethod.MethodName,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<HTTPMethod> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

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
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => MethodName.GetHashCode();

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
