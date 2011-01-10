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
using System.Linq;
using System.Reflection;

using de.ahzf.Hermod.HTTP.Common;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    public abstract class AHTTPServer<HTTPServiceType>
        where HTTPServiceType : IHTTPService, new()
    {

        #region Data

        protected readonly URLMapping _URLMapping;

        #endregion

        #region Properties

        #region ServerName

        public String ServerName { get; set; }

        #endregion

        #region HTTPSecurity

        private HTTPSecurity _HTTPSecurity;

        public HTTPSecurity HTTPSecurity
        {

            get
            {
                return _HTTPSecurity;
            }

            set
            {
                _HTTPSecurity = value;
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region AHTTPServer()

        public AHTTPServer()
        {
            _URLMapping = new URLMapping();
            ParseInterface();
        }

        #endregion

        #endregion


        #region RegisterService(myIHTTPService)

        public void RegisterService(IHTTPService myIHTTPService)
        {
        }

        #endregion

        #region ParseInterface

        private void ParseInterface()
        {

            #region Data

            HTTPMethod _HTTPMethod;
            String     _URITemplate;
            String     _Host = "*";

            #endregion

            #region Find the correct interface

            var _AllInterfaces = from _Interface
                                 in typeof(HTTPServiceType).GetInterfaces()
                                 where _Interface.GetInterfaces() != null
                                 where _Interface.GetInterfaces().Contains(typeof(IHTTPService))
                                 where _Interface.GetCustomAttributes(typeof(HTTPServiceAttribute), false) != null
                                 select _Interface;

            var _CurrentInterface = _AllInterfaces.FirstOrDefault();

            if (_CurrentInterface == null)
                throw new Exception("Could not find any valid interface having the HTTPService attribute!");

            if (_AllInterfaces.Count() > 1)
                throw new Exception("Multiple interfaces having the HTTPService attribute!");

            #endregion

            #region Get Host

            var _HTTPServiceAttribute = _CurrentInterface.GetCustomAttributes(typeof(HTTPServiceAttribute), false);
            if (_HTTPServiceAttribute != null && _HTTPServiceAttribute.Count() == 1)
            {
                _Host = (_HTTPServiceAttribute[0] as HTTPServiceAttribute).Host;
            }

            #endregion

            #region Check global Force-/NoAuthenticationAttribute

            var _GlobalNeedsExplicitAuthentication = false;

            if (_CurrentInterface.GetCustomAttributes(typeof(NoAuthenticationAttribute), false) != null)
                _GlobalNeedsExplicitAuthentication = false;

            if (_CurrentInterface.GetCustomAttributes(typeof(ForceAuthenticationAttribute), false) != null)
                _GlobalNeedsExplicitAuthentication = true;

            #endregion

            Boolean NeedsExplicitAuthentication = false;

            foreach (var _MethodInfo in _CurrentInterface.GetMethods())
            {

                _HTTPMethod                 = null;
                _URITemplate                = "";
                NeedsExplicitAuthentication = _GlobalNeedsExplicitAuthentication;

                foreach (var _Attribute in _MethodInfo.GetCustomAttributes(true))
                {

                    #region HTTPMappingAttribute

                    var _HTTPMappingAttribute = _Attribute as HTTPMappingAttribute;
                    if (_HTTPMappingAttribute != null)
                    {
                        _HTTPMethod  = _HTTPMappingAttribute.HTTPMethod;
                        _URITemplate = _HTTPMappingAttribute.UriTemplate;
                        continue;
                    }

                    #endregion

                    #region NoAuthentication

                    var _NoAuthenticationAttribute = _Attribute as NoAuthenticationAttribute;
                    if (_NoAuthenticationAttribute != null)
                    {
                        NeedsExplicitAuthentication = false;
                        continue;
                    }

                    #endregion

                    #region ForceAuthentication

                    var _ForceAuthenticationAttribute = _Attribute as ForceAuthenticationAttribute;
                    if (_ForceAuthenticationAttribute != null)
                    {
                        NeedsExplicitAuthentication = true;
                        continue;
                    }

                    #endregion

                    #region NeedsAuthentication

                    var _NeedsAuthenticationAttribute = _Attribute as NeedsAuthenticationAttribute;
                    if (_NeedsAuthenticationAttribute != null)
                    {
                        NeedsExplicitAuthentication = _NeedsAuthenticationAttribute.NeedsAuthentication;
                        continue;
                    }

                    #endregion

                }

                if (_HTTPMethod != null && _URITemplate != null && _URITemplate != "")
                    AddMethodCallback(_MethodInfo, _Host, _URITemplate, _HTTPMethod, null, NeedsExplicitAuthentication);

            }

        }

        #endregion

        #region AddMethodCallback(myHost, myURITemplate, myHTTPMethod, myMethodInfo, myHTTPContentType = null, myNeedsExplicitAuthentication = null)

        public void AddMethodCallback(MethodInfo myMethodInfo, String myHost, String myURITemplate, HTTPMethod myHTTPMethod, HTTPContentType myHTTPContentType = null, Boolean myNeedsExplicitAuthentication = false)
        {
            _URLMapping.AddHandler(myMethodInfo, myHost, myURITemplate, myHTTPMethod, myHTTPContentType, myNeedsExplicitAuthentication);
        }

        #endregion


    }

}
