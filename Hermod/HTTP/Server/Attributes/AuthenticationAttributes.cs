/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
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

    #region NeedsAuthenticationAttribute

    /// <summary>
    /// If set to True, this methods of a web interface definition needs authentication. If the server does not provide any, an exception will be thrown.
    /// If set to False, no authentication is required even if the server expect one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class NeedsAuthenticationAttribute : Attribute
    {

        private Boolean _NeedsAuthentication;
        public Boolean NeedsAuthentication
        {
            get { return _NeedsAuthentication; }
        }

        /// <summary>
        /// If set to True, this methods of a web interface definition needs authentication. If the server does not provide any, an exception will be thrown.
        /// If set to False, no authentication is required even if the server expect one.
        /// </summary>
        /// <param name="needsAuthentication">If set to True, this methods of a web interface definition needs authentication. If the server does not provide any, an exception will be thrown. If set to False, no authentication is required even if the server expect one.</param>
        public NeedsAuthenticationAttribute(Boolean needsAuthentication)
        {
            _NeedsAuthentication = needsAuthentication;
        }

    }

    #endregion

    #region NoAuthenticationAttribute

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class NoAuthenticationAttribute : Attribute
    {
        public NoAuthenticationAttribute()
        {
        }
    }

    #endregion

    #region ForceAuthenticationAttribute

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class ForceAuthenticationAttribute : Attribute
    {
        public ForceAuthenticationAttribute()
        {
        }
    }

    #endregion

}
