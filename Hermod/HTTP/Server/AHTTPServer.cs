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
using de.ahzf.Hermod.Tools;
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    public abstract class AHTTPServer<HTTPServiceInterface>
        where HTTPServiceInterface : IHTTPService
    {

        #region Data

        protected readonly URLMapping _URLMapping;

        #endregion

        #region Properties

        #region Implementations

        /// <summary>
        /// The autodiscovered implementations of the HTTPServiceInterface.
        /// </summary>
        public IDictionary<HTTPContentType, HTTPServiceInterface> Implementations { get; private set; }

        #endregion

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
            _URLMapping     = new URLMapping();
            Implementations = new Dictionary<HTTPContentType, HTTPServiceInterface>();
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
            String     _EventIdentification;
            UInt32     _MaxNumberOfCachedEvents = 0;
            Boolean    _IsSharedEventSource = false;

            #endregion

            var HTTPServiceImplementations = new AutoDiscovery<HTTPServiceInterface>();

            if (HTTPServiceImplementations != null && HTTPServiceImplementations.Count >= 1)
                foreach (var _Implementation in HTTPServiceImplementations)
                {

                    #region Find the correct interface

                    //var _AllInterfaces = from _Interface
                    //                     in typeof(HTTPServiceInterface).GetInterfaces()
                    //                     where _Interface.GetInterfaces() != null
                    //                     where _Interface.GetInterfaces().Contains(typeof(IHTTPService))
                    //                     where _Interface.GetCustomAttributes(typeof(HTTPServiceAttribute), false) != null
                    //                     select _Interface;

                    //var _CurrentInterface = _AllInterfaces.FirstOrDefault();

                    //if (_CurrentInterface == null)
                    //    throw new Exception("Could not find any valid interface having the HTTPService attribute!");

                    //if (_AllInterfaces.Count() > 1)
                    //    throw new Exception("Multiple interfaces having the HTTPService attribute!");

                    #endregion

                    HTTPContentType _CurrentContentType = null;

                    var _CurrentInterface = typeof(HTTPServiceInterface);

                    if (_Implementation.HTTPContentTypes.Count() != 1)
                        throw new ApplicationException("Less than and more than one HTTPContentType is currently not supported!");

                    _CurrentContentType = _Implementation.HTTPContentTypes.First();

                    Implementations.Add(_CurrentContentType, _Implementation);

                    #region Get Host

                    var _HTTPServiceAttribute = _CurrentInterface.GetCustomAttributes(typeof(HTTPServiceAttribute), false);
                    //ToDo: _HTTPServiceAttribute.Count() == 1 might be a bug!!!
                    if (_HTTPServiceAttribute != null && _HTTPServiceAttribute.Count() == 1)
                        _Host = (_HTTPServiceAttribute[0] as HTTPServiceAttribute).Host;

                    #endregion

                    #region Check global Force-/NoAuthenticationAttribute

                    var _GlobalNeedsExplicitAuthentication = false;

                    if (_CurrentInterface.GetCustomAttributes(typeof(NoAuthenticationAttribute), false) != null)
                        _GlobalNeedsExplicitAuthentication = false;

                    if (_CurrentInterface.GetCustomAttributes(typeof(ForceAuthenticationAttribute), false) != null)
                        _GlobalNeedsExplicitAuthentication = true;

                    #endregion

                    #region Add method callbacks

                    Boolean NeedsExplicitAuthentication = false;

                    foreach (var _MethodInfo in _CurrentInterface.GetMethods())
                    {

                        _HTTPMethod          = null;
                        _EventIdentification = null;
                        _URITemplate         = "";
                        NeedsExplicitAuthentication = _GlobalNeedsExplicitAuthentication;

                        foreach (var _Attribute in _MethodInfo.GetCustomAttributes(true))
                        {

                            #region HTTPMappingAttribute

                            var _HTTPMappingAttribute = _Attribute as HTTPMappingAttribute;
                            if (_HTTPMappingAttribute != null)
                            {

                                if (_EventIdentification != null)
                                    throw new Exception("URI '" + _URITemplate + "' is already registered as HTTP event source!");

                                _HTTPMethod  = _HTTPMappingAttribute.HTTPMethod;
                                _URITemplate = _HTTPMappingAttribute.UriTemplate;
                                continue;

                            }

                            #endregion

                            #region HTTPEventMappingAttribute

                            var _HTTPEventMappingAttribute = _Attribute as HTTPEventMappingAttribute;
                            if (_HTTPEventMappingAttribute != null)
                            {
                                
                                if (_HTTPMethod != null)
                                    throw new Exception("URI '" + _URITemplate + "' is already registered as HTTP method!");

                                _EventIdentification     = _HTTPEventMappingAttribute.EventIdentification;
                                _URITemplate             = _HTTPEventMappingAttribute.UriTemplate;
                                _MaxNumberOfCachedEvents = _HTTPEventMappingAttribute.MaxNumberOfCachedEvents;
                                _IsSharedEventSource     = _HTTPEventMappingAttribute.IsSharedEventSource;
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
                            AddMethodCallback(_MethodInfo, _Host, _URITemplate, _HTTPMethod, _CurrentContentType, NeedsExplicitAuthentication);

                        else if (_EventIdentification != null && _URITemplate != null && _URITemplate != "")
                            AddEventSource(_MethodInfo, _Host, _URITemplate, _EventIdentification, _MaxNumberOfCachedEvents, _IsSharedEventSource, NeedsExplicitAuthentication);

                    }

                    #endregion

                }

            else
                throw new Exception("Could not find any valid implementation of the HTTP service interface '" + typeof(HTTPServiceInterface).ToString() +"'!");

        }

        #endregion

        #region AddMethodCallback(myHost, myURITemplate, myHTTPMethod, myMethodInfo, myHTTPContentType = null, myNeedsExplicitAuthentication = null)

        public void AddMethodCallback(MethodInfo myMethodInfo, String myHost, String myURITemplate, HTTPMethod myHTTPMethod, HTTPContentType myHTTPContentType = null, Boolean myNeedsExplicitAuthentication = false)
        {
            _URLMapping.AddHandler(myMethodInfo, myHost, myURITemplate, myHTTPMethod, myHTTPContentType, myNeedsExplicitAuthentication);
        }

        #endregion


        /// <summary>
        /// 
        /// </summary>
        /// <param name="myMethodInfo"></param>
        /// <param name="myHost"></param>
        /// <param name="myURITemplate"></param>
        /// <param name="myEventIdentification"></param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events (0 means infinite).</param>
        /// <param name="myIsSharedEventSource"></param>
        /// <param name="myNeedsExplicitAuthentication"></param>
        public void AddEventSource(MethodInfo myMethodInfo, String myHost, String myURITemplate, String myEventIdentification, UInt32 MaxNumberOfCachedEvents, Boolean myIsSharedEventSource = false, Boolean myNeedsExplicitAuthentication = false)
        {
            _URLMapping.AddEventSource(myMethodInfo, myHost, myURITemplate, myEventIdentification, MaxNumberOfCachedEvents, myIsSharedEventSource, myNeedsExplicitAuthentication);
        }

    }

}
