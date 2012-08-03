/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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

#endregion

namespace de.ahzf.Hermod.HTTP
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
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class OptionalAuthenticationAttribute : AuthenticationAttribute
    {

        /// <summary>
        /// Optional authentication possible.
        /// </summary>
        /// <param name="AuthenticationType">The authentication type(s).</param>
        /// <param name="Realm">The HTTP realm.</param>
        public OptionalAuthenticationAttribute(HTTPAuthenticationTypes AuthenticationType, String Realm)
            : base(AuthenticationType, Realm)
        { }

    }

    #endregion

    #region ForceAuthenticationAttribute

    /// <summary>
    /// HTTP authentication required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class ForceAuthenticationAttribute : AuthenticationAttribute
    {

        /// <summary>
        /// HTTP authentication required.
        /// </summary>
        public ForceAuthenticationAttribute()
            : base(HTTPAuthenticationTypes.Basic | HTTPAuthenticationTypes.Digest | HTTPAuthenticationTypes.Mutual)
        { }

        /// <summary>
        /// HTTP authentication required.
        /// </summary>
        /// <param name="Realm">The HTTP realm.</param>
        public ForceAuthenticationAttribute(String Realm)
            : base(HTTPAuthenticationTypes.Basic | HTTPAuthenticationTypes.Digest | HTTPAuthenticationTypes.Mutual, Realm)
        { }

        /// <summary>
        /// HTTP authentication required.
        /// </summary>
        /// <param name="AuthenticationType">The authentication type(s).</param>
        /// <param name="Realm">The HTTP realm.</param>
        public ForceAuthenticationAttribute(HTTPAuthenticationTypes AuthenticationType, String Realm)
            : base(AuthenticationType, Realm)
        { }

    }

    #endregion

}
