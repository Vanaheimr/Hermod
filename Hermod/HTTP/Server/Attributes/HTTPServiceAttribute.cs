/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Net;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = true)]
    public class HTTPServiceAttribute : Attribute
    {

        #region Data

        private const String _DefaultHost = "*";

        #endregion

        #region Properties

        public IPAddress IPAddress          { get; private set; }

        public String    Host               { get; private set; }

        public Boolean   HostAuthentication { get; private set; }

        #endregion

        #region Constructor(s)

        // Optional parameters on attributes seem to lead
        // to compilation errors on Microsoft .NET 4.0!

        #region HTTPServiceAttribute()

        public HTTPServiceAttribute()
        {
            this.IPAddress          = IPAddress.Any;
            this.Host               = _DefaultHost;
            this.HostAuthentication = false;
        }

        #endregion

        #region HTTPServiceAttribute(HostAuthentication)

        public HTTPServiceAttribute(Boolean HostAuthentication)
        {
            this.IPAddress          = IPAddress.Any;
            this.Host               = _DefaultHost;
            this.HostAuthentication = HostAuthentication;
        }

        #endregion

        #region HTTPServiceAttribute(Host, HostAuthentication)

        public HTTPServiceAttribute(String Host, Boolean HostAuthentication)
        {
            this.IPAddress          = IPAddress.Any;
            this.Host               = Host;
            this.HostAuthentication = HostAuthentication;
        }

        #endregion

        #region HTTPServiceAttribute(Host)

        public HTTPServiceAttribute(String Host)
        {
            this.IPAddress          = IPAddress.Any;
            this.Host               = Host;
            this.HostAuthentication = false;
        }

        #endregion

        #region HTTPServiceAttribute(IPAddress, Host, HostAuthentication)

        public HTTPServiceAttribute(IPAddress IPAddress, String Host, Boolean HostAuthentication)
        {
            this.IPAddress          = IPAddress;
            this.Host               = Host;
            this.HostAuthentication = HostAuthentication;
        }

        #endregion

        #endregion

    }

}
