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
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using eu.Vanaheimr.Illias.Commons;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An abstract generic HTTP server.
    /// </summary>
    /// <typeparam name="HTTPServiceInterface">An interface inheriting from IHTTPService and defining URLMappings.</typeparam>
    public abstract class AHTTPServer<HTTPServiceInterface>
        where HTTPServiceInterface : class, IHTTPService
    {

        #region Properties

        /// <summary>
        /// The autodiscovered implementations of the HTTPServiceInterface.
        /// </summary>
        public IDictionary<HTTPContentType, HTTPServiceInterface> Implementations { get; private set; }

        /// <summary>
        /// The HTTP server name.
        /// </summary>
        public String ServerName { get; set; }

        /// <summary>
        /// An associated HTTP security object.
        /// </summary>
        public HTTPSecurity HTTPSecurity { get; set; }

        /// <summary>
        /// A mapping from URLTemplates onto C# methods.
        /// </summary>
        public URLMapping URLMapping { get; protected set; }

        #region CallingAssemblies

        private List<Assembly> _CallingAssemblies;

        public List<Assembly> CallingAssemblies
        {

            get
            {
                return _CallingAssemblies;
            }

            set
            {

                if (value == null)
                    return;

                this._CallingAssemblies = value;

                // Add Hermod to the list of assemblies
                this._CallingAssemblies.Add(Assembly.GetExecutingAssembly());

                this.AllResources = _CallingAssemblies.

                                        SelectMany(v => v.GetManifestResourceNames().

                                        Select(v2 => new {
                                            Assembly  = v,
                                            Ressource = v2
                                        })).

                                        ToDictionary(w => w.Ressource,
                                        w => w.Assembly);

            }

        }

        #endregion

        public IDictionary<String, Assembly> AllResources { get; private set; }

        #endregion

        #region Events

        public event AccessLogDelegate AccessLog;
        public event ErrorLogDelegate  ErrorLog;

        #endregion

        #region Constructor(s)

        #region AHTTPServer()

        /// <summary>
        /// Creates a new abstract HTTP server.
        /// </summary>
        public AHTTPServer()
        {

            this.URLMapping       = new URLMapping();
            this.Implementations  = new Dictionary<HTTPContentType, HTTPServiceInterface>();

            ParseInterface();

        }

        #endregion

        #endregion


        #region (private) ParseInterface

        /// <summary>
        /// Parses the given HTTP service interface and
        /// adds method callbacks and event sources.
        /// </summary>
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

            var HTTPServiceInterfaceType = typeof(HTTPServiceInterface);
            var HTTPServiceDiscovery     = new AutoDiscovery<HTTPServiceInterface>();

            if (HTTPServiceDiscovery != null && HTTPServiceDiscovery.Count >= 1)
            {
                foreach (var HTTPServiceImplementation in HTTPServiceDiscovery)
                {

                    #region Initial checks

                    if (HTTPServiceImplementation.HTTPContentTypes == null)
                        throw new ApplicationException("Please define an associated HTTPContentType for the '" + HTTPServiceImplementation.GetType().FullName + "' HTTP service implementation!");

                    if (HTTPServiceImplementation.HTTPContentTypes.Count() != 1)
                        throw new ApplicationException("Less than and more than one HTTPContentType is currently not supported!");

                    #endregion

                    #region Register associated content type(s)

                    foreach (var AssociatedContentType in HTTPServiceImplementation.HTTPContentTypes)
                    {
                        if (!(Implementations.ContainsKey(AssociatedContentType)))
                            Implementations.Add(AssociatedContentType, HTTPServiceImplementation);
                        else
                            throw new ApplicationException("The content type '" + AssociatedContentType + "' is already associated with an HTTP service implementation called '" + Implementations[AssociatedContentType].GetType().FullName + "'!");
                    }

                    #endregion

                    #region Get HTTPServiceInterface Attributes

                    #region HTTPServiceAttribute

                    var _HTTPServiceAttribute = HTTPServiceInterfaceType.GetCustomAttributes(typeof(HTTPServiceAttribute), false);

                    if (_HTTPServiceAttribute == null || _HTTPServiceAttribute.Count() != 1)
                        throw new ApplicationException("Invalid HTTPServiceAttributes found!");

                        _Host = (_HTTPServiceAttribute[0] as HTTPServiceAttribute).Host;

                    #endregion

                    #region Check global Force-/NoAuthenticationAttribute

                    var _GlobalNeedsExplicitAuthentication = false;

                    if (HTTPServiceInterfaceType.GetCustomAttributes(typeof(NoAuthenticationAttribute), false) != null)
                        _GlobalNeedsExplicitAuthentication = false;

                    if (HTTPServiceInterfaceType.GetCustomAttributes(typeof(ForceAuthenticationAttribute), false) != null)
                        _GlobalNeedsExplicitAuthentication = true;

                    #endregion

                    #endregion

                    #region Add method callbacks and event sources

                    var NeedsExplicitAuthentication = false;

                    foreach (var _MethodInfo in HTTPServiceInterfaceType.GetRecursiveInterfaces().SelectMany(CI => CI.GetMethods()))
                    {

                        _HTTPMethod                 = null;
                        _EventIdentification        = null;
                        _URITemplate                = "";
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

                                _HTTPMethod              = _HTTPEventMappingAttribute.HTTPMethod;
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

                        }

                        #region Add MethodCallback or EventSource

                        if (_HTTPMethod != null && _URITemplate != null && _URITemplate != "")
                            foreach (var AssociatedContentType in HTTPServiceImplementation.HTTPContentTypes)
                            {
                                if (AssociatedContentType != HTTPContentType.EVENTSTREAM)
                                    AddMethodCallback(_MethodInfo, _Host, _URITemplate, _HTTPMethod, AssociatedContentType, NeedsExplicitAuthentication);

                                else
                                    if (_EventIdentification != null)
                                        AddEventSource(_MethodInfo, _Host, _URITemplate, _HTTPMethod, _EventIdentification, _MaxNumberOfCachedEvents, _IsSharedEventSource, NeedsExplicitAuthentication);
                            }

                        #endregion

                    }

                    #endregion

                }
            }
            else
                throw new Exception("Could not find any valid implementation of the HTTP service interface '" + typeof(HTTPServiceInterface).FullName.ToString() + "'!");

        }

        #endregion


        public void LogAccess(DateTime ServerTime, HTTPRequest Request, HTTPResponse HTTPResponse)
        {
            if (AccessLog != null)
                AccessLog(ServerTime, Request, HTTPResponse);
        }

        public void LogError(DateTime ServerTime, HTTPRequest Request, HTTPResponse HTTPResponse, String Error = null, Exception LastException = null)
        {
            if (ErrorLog != null)
                ErrorLog(ServerTime, Request, HTTPResponse, Error, LastException);
        }


        #region AddMethodCallback(Host, URITemplate, HTTPMethod, MethodInfo, HTTPContentType = null, NeedsExplicitAuthentication = false)

        /// <summary>
        /// 
        /// </summary>
        /// <param name="MethodInfo"></param>
        /// <param name="Host"></param>
        /// <param name="URITemplate"></param>
        /// <param name="HTTPMethod"></param>
        /// <param name="HTTPContentType"></param>
        /// <param name="NeedsExplicitAuthentication"></param>
        public void AddMethodCallback(MethodInfo       MethodInfo,
                                      String           Host,
                                      String           URITemplate,
                                      HTTPMethod       HTTPMethod,
                                      HTTPContentType  HTTPContentType             = null,
                                      Boolean          NeedsExplicitAuthentication = false)
        {
            URLMapping.AddHandler(MethodInfo, Host, URITemplate, HTTPMethod, HTTPContentType, NeedsExplicitAuthentication);
        }

        #endregion

        #region AddEventSource(MethodInfo, Host, URITemplate, HTTPMethod, EventIdentification, MaxNumberOfCachedEvents, IsSharedEventSource = false, NeedsExplicitAuthentication = false)

        /// <summary>
        /// 
        /// </summary>
        /// <param name="MethodInfo"></param>
        /// <param name="Host"></param>
        /// <param name="HTTPMethod">The HTTP methods to use.</param>
        /// <param name="URITemplate"></param>
        /// <param name="EventIdentification"></param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events (0 means infinite).</param>
        /// <param name="IsSharedEventSource"></param>
        /// <param name="NeedsExplicitAuthentication"></param>
        public void AddEventSource(MethodInfo  MethodInfo,
                                   String      Host,
                                   String      URITemplate,
                                   HTTPMethod  HTTPMethod,
                                   String      EventIdentification,
                                   UInt32      MaxNumberOfCachedEvents,
                                   Boolean     IsSharedEventSource          = false,
                                   Boolean     NeedsExplicitAuthentication  = false)
        {
            URLMapping.AddEventSource(MethodInfo, Host, URITemplate, HTTPMethod, EventIdentification, MaxNumberOfCachedEvents, IsSharedEventSource, NeedsExplicitAuthentication);
        }

        #endregion

    }

}
