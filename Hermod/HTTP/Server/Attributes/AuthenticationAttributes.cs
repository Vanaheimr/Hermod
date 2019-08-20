/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    #region AuthenticationAttribute

    /// <summary>
    /// The generic HTTP authentication attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class AuthenticationAttribute : Attribute
    {

        #region Properties

        /// <summary>
        /// The authentication type(s).
        /// </summary>
        public HTTPAuthenticationTypes AuthenticationType { get; private set; }

        /// <summary>
        /// The HTTP realm.
        /// </summary>
        public String                  Realm              { get; private set; }

        #endregion

        #region (internal) AuthenticationAttribute()

        /// <summary>
        /// The generic HTTP authentication attribute.
        /// </summary>
        internal AuthenticationAttribute()
        {
            this.AuthenticationType = HTTPAuthenticationTypes.Basic | HTTPAuthenticationTypes.Digest | HTTPAuthenticationTypes.Mutual;
            this.Realm              = String.Empty;
        }

        #endregion

        #region (internal) AuthenticationAttribute(AuthenticationType)

        /// <summary>
        /// The generic HTTP authentication attribute.
        /// </summary>
        /// <param name="AuthenticationType">The authentication type(s).</param>
        internal AuthenticationAttribute(HTTPAuthenticationTypes AuthenticationType)
        {
            this.AuthenticationType = AuthenticationType;
            this.Realm              = String.Empty;
        }

        #endregion

        #region (internal) AuthenticationAttribute(AuthenticationType, Realm)

        /// <summary>
        /// The generic HTTP authentication attribute including a realm.
        /// </summary>
        /// <param name="AuthenticationType">The authentication type(s).</param>
        /// <param name="Realm">The HTTP realm.</param>
        internal AuthenticationAttribute(HTTPAuthenticationTypes AuthenticationType, String Realm)
        {
            this.AuthenticationType = AuthenticationType;
            this.Realm = Realm;
        }

        #endregion

    }

    #endregion

    #region NoAuthenticationAttribute

    /// <summary>
    /// No HTTP authentication required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class NoAuthenticationAttribute : AuthenticationAttribute
    {

        /// <summary>
        /// No HTTP authentication required.
        /// </summary>
        public NoAuthenticationAttribute()
            : base(HTTPAuthenticationTypes.None)
        { }

    }

    #endregion

    #region OptionalAuthenticationAttribute

    /// <summary>
    /// Optional authentication possible.
    /// </summary>
    /// <seealso cref="http://tools.ietf.org/html/draft-oiwa-httpbis-auth-extension-00#section-3"/>
    /// <remarks>Servers MAY send HTTP successful responses (response code 200, 206 and others) containing the Optional-WWW-Authenticate header as a replacement of a 401 response when it is an authentication-initializing response.  The Optional-WWW-Authenticate header MUST NOT be contained in 401 responses.</remarks>
    /// <example>HTTP/1.1 200 OK\r\nOptional-WWW-Authenticate: Basic realm="xxxx"</example>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class OptionalAuthenticationAttribute : AuthenticationAttribute
    {

        #region OptionalAuthenticationAttribute()

        /// <summary>
        /// Optional authentication possible.
        /// </summary>
        public OptionalAuthenticationAttribute()
            : base()
        { }

        #endregion

        #region OptionalAuthenticationAttribute(AuthenticationType)

        /// <summary>
        /// Optional authentication possible.
        /// </summary>
        /// <param name="AuthenticationType">The authentication type(s).</param>
        public OptionalAuthenticationAttribute(HTTPAuthenticationTypes AuthenticationType)
            : base(AuthenticationType)
        { }

        #endregion

        #region OptionalAuthenticationAttribute(AuthenticationType, Realm)

        /// <summary>
        /// Optional authentication possible.
        /// </summary>
        /// <param name="AuthenticationType">The authentication type(s).</param>
        /// <param name="Realm">The HTTP realm.</param>
        public OptionalAuthenticationAttribute(HTTPAuthenticationTypes AuthenticationType, String Realm)
            : base(AuthenticationType, Realm)
        { }

        #endregion

    }

    #endregion

    #region ForceAuthenticationAttribute

    /// <summary>
    /// HTTP authentication required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class ForceAuthenticationAttribute : AuthenticationAttribute
    {

        #region ForceAuthenticationAttribute()

        /// <summary>
        /// HTTP authentication required.
        /// </summary>
        public ForceAuthenticationAttribute()
            : base()
        { }

        #endregion

        #region ForceAuthenticationAttribute(Realm)

        /// <summary>
        /// HTTP authentication required.
        /// </summary>
        /// <param name="Realm">The HTTP realm.</param>
        public ForceAuthenticationAttribute(String Realm)
            : base(HTTPAuthenticationTypes.Basic | HTTPAuthenticationTypes.Digest | HTTPAuthenticationTypes.Mutual, Realm)
        { }

        #endregion

        #region ForceAuthenticationAttribute(AuthenticationType, Realm)

        /// <summary>
        /// HTTP authentication required.
        /// </summary>
        /// <param name="AuthenticationType">The authentication type(s).</param>
        /// <param name="Realm">The HTTP realm.</param>
        public ForceAuthenticationAttribute(HTTPAuthenticationTypes AuthenticationType, String Realm)
            : base(AuthenticationType, Realm)
        { }

        #endregion

    }

    #endregion

}
