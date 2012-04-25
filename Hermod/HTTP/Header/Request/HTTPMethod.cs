/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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

using System;
using System.Linq;

#endregion

namespace de.ahzf.Hermod.HTTP
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

        DELETE,
        GET,
        HEAD,
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

        PATCH,
        TRAVERSE,
        FILTER

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
    public class HTTPMethod : IComparable, IComparable<HTTPMethod>, IEquatable<HTTPMethod>
    {

        #region Properties

        /// <summary>
        /// The name of the HTTP method.
        /// </summary>
        public String  MethodName   { get; private set; }
        
        /// <summary>
        /// IsSafe
        /// </summary>
        public Boolean IsSafe       { get; private set; }

        /// <summary>
        /// This HTTP methods has no side-effects for N > 0 identical
        /// requests, as it is the same as for a single request.
        /// </summary>
        public Boolean IsIdempotent { get; private set; }
        
        /// <summary>
        /// The description of this HTTP method.
        /// </summary>
        public String  Description  { get; private set; }

        #endregion

        #region Constructor(s)

        #region HTTPMethod(MethodName, IsSafe = false, IsIdempotent = false, Description = null)

        /// <summary>
        /// Creates a new HTTP method based on the given parameters.
        /// </summary>
        /// <param name="MethodName">The name of the HTTP method.</param>
        /// <param name="IsSafe"></param>
        /// <param name="IsIdempotent">This HTTP methods has no side-effects for N > 0 identical requests, as it is the same as for a single request.</param>
        /// <param name="Description">The description of this HTTP method.</param>
        public HTTPMethod(String MethodName, Boolean IsSafe = false, Boolean IsIdempotent = false, String Description = null)
        {
            this.MethodName    = MethodName;
            this.IsSafe        = IsSafe;
            this.IsIdempotent  = IsIdempotent;
            this.Description   = Description;
        }

        #endregion

        #endregion


        #region RFC 2616 - HTTP/1.1

        public static readonly HTTPMethod CONNECT       = new HTTPMethod("CONNECT");

        public static readonly HTTPMethod DELETE        = new HTTPMethod("DELETE",  IsIdempotent: true);
        public static readonly HTTPMethod GET           = new HTTPMethod("GET",     IsIdempotent: true, IsSafe: true);
        public static readonly HTTPMethod HEAD          = new HTTPMethod("HEAD",    IsIdempotent: true, IsSafe: true);
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

        public static readonly HTTPMethod PATCH         = new HTTPMethod("PATCH");
        public static readonly HTTPMethod TRAVERSE      = new HTTPMethod("TRAVERSE");
        public static readonly HTTPMethod FILTER        = new HTTPMethod("FILTER");

        #endregion


        #region Tools

        #region ParseEnum(myHTTPMethodsEnum)

        /// <summary>
        /// Tries to find the apropriate HTTPMethod for the given HTTPMethods.
        /// </summary>
        /// <param name="myCode">A HTTPMethod code as string</param>
        /// <returns>A HTTPMethod</returns>
        public static HTTPMethod ParseEnum(HTTPMethods myHTTPMethodsEnum)
        {

            var _HTTPMethod = (from   _FieldInfo in typeof(HTTPMethod).GetFields()
                               let    __HTTPMethod = _FieldInfo.GetValue(null) as HTTPMethod
                               where  __HTTPMethod != null
                               where  __HTTPMethod.MethodName == myHTTPMethodsEnum.ToString()
                               select __HTTPMethod).FirstOrDefault();

            if (_HTTPMethod != null)
                return _HTTPMethod;

            return null;

        }

        #endregion

        #region ParseString(myMethodname)

        /// <summary>
        /// Tries to find the apropriate HTTPMethod for the given HTTPMethods.
        /// </summary>
        /// <param name="myCode">A HTTPMethod code as string</param>
        /// <returns>A HTTPMethod</returns>
        public static HTTPMethod ParseString(String myMethodname)
        {

            var _HTTPMethod = (from   _FieldInfo in typeof(HTTPMethod).GetFields()
                               let    __HTTPMethod = _FieldInfo.GetValue(null) as HTTPMethod
                               where  __HTTPMethod != null
                               where  __HTTPMethod.MethodName == myMethodname
                               select __HTTPMethod).FirstOrDefault();

            if (_HTTPMethod != null)
                return _HTTPMethod;

            return null;

        }

        #endregion


        #region TryParseEnum(myCode, out myHTTPMethod)

        /// <summary>
        /// Tries to find the apropriate HTTPMethod for the given HTTPMethods.
        /// </summary>
        /// <param name="myCode">A HTTPMethod code</param>
        /// <param name="myHTTPMethod">The parsed HTTPMethod</param>
        /// <returns>true or false</returns>
        public static Boolean TryParseEnum(HTTPMethods myHTTPMethodsEnum, out HTTPMethod myHTTPMethod)
        {

            myHTTPMethod = (from   _FieldInfo in typeof(HTTPMethod).GetFields()
                            let    _HTTPMethod = _FieldInfo.GetValue(null) as HTTPMethod
                            where  _HTTPMethod != null
                            where  _HTTPMethod.MethodName == myHTTPMethodsEnum.ToString()
                            select _HTTPMethod).FirstOrDefault();

            if (myHTTPMethod != null)
                return true;

            myHTTPMethod = null;

            return false;

        }

        #endregion

        #region TryParseString(myMethodname, out myHTTPMethod)

        /// <summary>
        /// Tries to find the apropriate HTTPMethod for the given string.
        /// </summary>
        /// <param name="myMethodname">A HTTPMethod code</param>
        /// <param name="myHTTPStatusCode">The parsed HTTPMethod</param>
        /// <returns>true or false</returns>
        public static Boolean TryParseString(String myMethodname, out HTTPMethod myHTTPMethod)
        {

            myHTTPMethod = (from   _FieldInfo in typeof(HTTPMethod).GetFields()
                            let    _HTTPMethod = _FieldInfo.GetValue(null) as HTTPMethod
                            where  _HTTPMethod != null
                            where  _HTTPMethod.MethodName == myMethodname
                            select _HTTPMethod).FirstOrDefault();

            if (myHTTPMethod != null)
                return true;

            myHTTPMethod = null;

            return false;

        }

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (myHTTPMethod1, myHTTPMethod2)

        public static Boolean operator == (HTTPMethod myHTTPMethod1, HTTPMethod myHTTPMethod2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(myHTTPMethod1, myHTTPMethod2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) myHTTPMethod1 == null) || ((Object) myHTTPMethod2 == null))
                return false;

            return myHTTPMethod1.Equals(myHTTPMethod2);

        }

        #endregion

        #region Operator != (myHTTPMethod1, myHTTPMethod2)

        public static Boolean operator != (HTTPMethod myHTTPMethod1, HTTPMethod myHTTPMethod2)
        {
            return !(myHTTPMethod1 == myHTTPMethod2);
        }

        #endregion

        #endregion

        #region IComparable<HTTPMethod> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is an HTTPMethod.
            var HTTPMethod = Object as HTTPMethod;
            if ((Object) HTTPMethod == null)
                throw new ArgumentException("The given object is not a HTTPMethod!");

            return CompareTo(HTTPMethod);

        }

        #endregion

        #region CompareTo(HTTPMethod)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPMethod">An object to compare with.</param>
        public Int32 CompareTo(HTTPMethod HTTPMethod)
        {

            if ((Object) HTTPMethod == null)
                throw new ArgumentNullException("The given HTTPMethod must not be null!");

            return MethodName.CompareTo(HTTPMethod.MethodName);

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
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            // Check if the given object is an HTTPMethod.
            var HTTPMethod = Object as HTTPMethod;
            if ((Object) HTTPMethod == null)
                return false;

            return this.Equals(HTTPMethod);

        }

        #endregion

        #region Equals(HTTPMethod)

        /// <summary>
        /// Compares two HTTPMethod for equality.
        /// </summary>
        /// <param name="HTTPMethod">An HTTPMethod to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPMethod HTTPMethod)
        {

            if ((Object) HTTPMethod == null)
                return false;

            return MethodName == HTTPMethod.MethodName;

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            return MethodName.GetHashCode();
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {
            return MethodName;
        }

        #endregion

    }

}

