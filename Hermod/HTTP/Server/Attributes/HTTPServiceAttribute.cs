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
using System.Net;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = true)]
    public class HTTPServiceAttribute : Attribute
    {

        #region Data

        private const String _DefaultHost = "*";

        #endregion

        #region Properties

        #region IPAddress

        private IPAddress _IPAddress;

        public IPAddress IPAddress
        {
            get
            {
                return _IPAddress;
            }
        }

        #endregion

        #region Host
        
        private String _Host;

        public String Host
        {
            get
            {
                return _Host;
            }
        }

        #endregion

        #region HostAuthentication

        private Boolean _HostAuthentication;

        public Boolean HostAuthentication
        {
            get
            {
                return _HostAuthentication;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        // Optional parameters on attributes seem to lead
        // to compilation errors on Microsoft .NET 4.0!

        #region HTTPServiceAttribute()

        public HTTPServiceAttribute()
        {
            _IPAddress           = IPAddress.Any;
            _Host                = _DefaultHost;
            _HostAuthentication = false;
        }

        #endregion

        #region HTTPServiceAttribute(HostAuthentication)

        public HTTPServiceAttribute(Boolean HostAuthentication)
        {
            _IPAddress          = IPAddress.Any;
            _Host               = _DefaultHost;
            _HostAuthentication = HostAuthentication;
        }

        #endregion

        #region HTTPServiceAttribute(Host, HostAuthentication)

        public HTTPServiceAttribute(String Host, Boolean HostAuthentication)
        {
            _IPAddress          = IPAddress.Any;
            _Host               = Host;
            _HostAuthentication = HostAuthentication;
        }

        #endregion

        #region HTTPServiceAttribute(Host)

        public HTTPServiceAttribute(String Host)
        {
            _IPAddress           = IPAddress.Any;
            _Host                = Host;
            _HostAuthentication = false;
        }

        #endregion

        #region HTTPServiceAttribute(IPAddress, Host, HostAuthentication)

        public HTTPServiceAttribute(IPAddress IPAddress, String Host, Boolean HostAuthentication)
        {
            _IPAddress          = IPAddress;
            _Host               = Host;
            _HostAuthentication = HostAuthentication;
        }

        #endregion

        #endregion

    }

}
