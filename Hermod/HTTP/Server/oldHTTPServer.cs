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

using eu.Vanaheimr.Hermod.Sockets.TCP;
using System.Collections.Generic;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    //#region HTTPServer -> HTTPServer<DefaultHTTPService>

    ///// <summary>
    ///// A HTTP server serving a default HTTP service.
    ///// </summary>
    //public class HTTPServer : AHTTPServer, IHTTPServer
    //{

    //    #region Properties



    //    #endregion

    //    #region Constructor(s)

    //    #region HTTPServer(Port, NewHTTPConnectionHandler = null, AutoStart = false)

    //    /// <summary>
    //    /// Initialize the HTTPServer using IPAddress.Any and the given parameters.
    //    /// </summary>
    //    /// <param name="Port">The listening port</param>
    //    /// <param name="NewHTTPConnectionHandler">A delegate called for every new http connection.</param>
    //    /// <param name="Autostart">Autostart the http server.</param>
    //    public HTTPServer(IPPort Port, Boolean Autostart = false)
    //        : base(IPv4Address.Any, Port, Autostart)
    //    { }

    //    #endregion

    //    //#region HTTPServer(Port, NewHTTPConnectionHandler = null, AutoStart = false)

    //    ///// <summary>
    //    ///// Initialize the HTTPServer using IPAddress.Any and the given parameters.
    //    ///// </summary>
    //    ///// <param name="Port">The listening port</param>
    //    ///// <param name="NewHTTPConnectionHandler">A delegate called for every new http connection.</param>
    //    ///// <param name="Autostart">Autostart the http server.</param>
    //    //public HTTPServer(IPPort Port, NewHTTPServiceHandler NewHTTPConnectionHandler = null, Boolean Autostart = false)
    //    //    : this(IPv4Address.Any, Port, NewHTTPConnectionHandler, Autostart)
    //    //{ }

    //    //#endregion

    //    //#region HTTPServer(IIPAddress, Port, NewHTTPConnectionHandler = null, AutoStart = false)

    //    ///// <summary>
    //    ///// Initialize the HTTPServer using the given parameters.
    //    ///// </summary>
    //    ///// <param name="IIPAddress">The listening IP address(es)</param>
    //    ///// <param name="Port">The listening port</param>
    //    ///// <param name="NewHTTPConnectionHandler">A delegate called for every new http connection.</param>
    //    ///// <param name="Autostart">Autostart the http server.</param>
    //    //public HTTPServer(IIPAddress IIPAddress, IPPort Port, NewHTTPServiceHandler NewHTTPConnectionHandler = null, Boolean Autostart = false)
    //    //    : base(IIPAddress, Port, NewHTTPConnectionHandler, Autostart)
    //    //{ }

    //    //#endregion

    //    //#region HTTPServer(IPSocket, NewHTTPConnectionHandler = null, Autostart = false)

    //    ///// <summary>
    //    ///// Initialize the HTTPServer using the given parameters.
    //    ///// </summary>
    //    ///// <param name="IPSocket">The listening IPSocket.</param>
    //    ///// <param name="NewHTTPConnectionHandler">A delegate called for every new http connection.</param>
    //    ///// <param name="Autostart">Autostart the http server.</param></param>
    //    //public HTTPServer(IPSocket IPSocket, NewHTTPServiceHandler NewHTTPConnectionHandler = null, Boolean Autostart = false)
    //    //    : this(IPSocket.IPAddress, IPSocket.Port, NewHTTPConnectionHandler, Autostart)
    //    //{ }

    //    //#endregion

    //    #endregion

    //    #region ToString()

    //    /// <summary>
    //    /// Return a string represtentation of this object.
    //    /// </summary>
    //    public override String ToString()
    //    {

    //        var _Running = "";
    //        if (IsRunning) _Running = " (running)";

    //        return String.Concat(this.GetType().Name, " ", IPAddress.ToString(), ":", Port, _Running);

    //    }

    //    #endregion

    //}

    //#endregion

    //#region HTTPServer<DefaultHTTPService>

    ///// <summary>
    /////  This http server will listen on a tcp port and maps incoming urls
    /////  to methods of HTTPServiceInterface.
    ///// </summary>
    ///// <typeparam name="HTTPServiceInterface">A http service interface.</typeparam>
    //public class HTTPServer<HTTPServiceInterface> : HTTPServer
    //    where HTTPServiceInterface : class, IHTTPService
    //{

    //    #region Properties

    //    #region Implementations

    //    private readonly IDictionary<HTTPContentType, HTTPServiceInterface> _Implementations;

    //    /// <summary>
    //    /// The autodiscovered implementations of the HTTPServiceInterface.
    //    /// </summary>
    //    public IDictionary<HTTPContentType, HTTPServiceInterface> Implementations
    //    {
    //        get
    //        {
    //            return _Implementations;
    //        }
    //    }

    //    #endregion

    //    #endregion

    //    #region Events

    //    #region OnNewHTTPService

    //    /// <summary>
    //    /// A delegate definition for every incoming HTTP connection.
    //    /// </summary>
    //    /// <param name="HTTPServiceInterfaceType">The interface of the associated http connections.</param>
    //    public delegate void NewHTTPServiceHandler(HTTPServiceInterface HTTPServiceInterfaceType);

    //    /// <summary>
    //    /// An event called for every incoming HTTP connection.
    //    /// </summary>
    //    public event NewHTTPServiceHandler OnNewHTTPService;

    //    #endregion

    //    #endregion

    //    #region Constructor(s)

    //    //#region HTTPServer(NewHTTPConnectionHandler = null)

    //    ///// <summary>
    //    ///// Initialize the HTTPServer using IPAddress.Any, http port 80 and start the server.
    //    ///// </summary>
    //    ///// <param name="NewHTTPConnectionHandler">A delegate called for every new http connection.</param>
    //    //public HTTPServer(NewHTTPServiceHandler NewHTTPConnectionHandler = null)
    //    //    : this(IPv4Address.Any, IPPort.HTTP, NewHTTPConnectionHandler, true)
    //    //{ }

    //    //#endregion

    //    //#region HTTPServer(Port, NewHTTPServiceHandler = null, AutoStart = true)

    //    ///// <summary>
    //    ///// Initialize the HTTPServer using IPAddress.Any and the given parameters.
    //    ///// </summary>
    //    ///// <param name="Port">The listening port</param>
    //    ///// <param name="NewHTTPServiceHandler">A delegate called for every new http connection.</param>
    //    ///// <param name="Autostart">Autostart the http server.</param>
    //    //public HTTPServer(IPPort Port, NewHTTPServiceHandler NewHTTPServiceHandler = null, Boolean Autostart = true)
    //    //    : this(IPv4Address.Any, Port, NewHTTPServiceHandler, Autostart)
    //    //{ }

    //    //#endregion

    //    //#region HTTPServer(IIPAddress, Port, NewHTTPServiceHandler = null, AutoStart = true)

    //    ///// <summary>
    //    ///// Initialize the HTTPServer using the given parameters.
    //    ///// </summary>
    //    ///// <param name="IIPAddress">The listening IP address(es)</param>
    //    ///// <param name="Port">The listening port</param>
    //    ///// <param name="NewHTTPServiceHandler">A delegate called for every new http connection.</param>
    //    ///// <param name="Autostart">Autostart the http server.</param>
    //    //public HTTPServer(IIPAddress IIPAddress, IPPort Port, NewHTTPServiceHandler NewHTTPServiceHandler = null, Boolean Autostart = true)
    //    //{

    //    //    ServerName = _DefaultServerName;

    //    //    if (NewHTTPServiceHandler != null)
    //    //        OnNewHTTPService += NewHTTPServiceHandler;

    //    //    _TCPServer = new TCPServer<HTTPConnection<HTTPServiceInterface>>(
    //    //                         IIPAddress,
    //    //                         Port,
    //    //                         NewHTTPConnection =>
    //    //                             {

    //    //                                 NewHTTPConnection.HTTPServer            = this;
    //    //                                 NewHTTPConnection.ServerName            = ServerName;
    //    //                                 NewHTTPConnection.HTTPSecurity          = HTTPSecurity;
    //    //                                 NewHTTPConnection.NewHTTPServiceHandler = OnNewHTTPService;
    //    //                                 NewHTTPConnection.Implementations       = Implementations;

    //    //                                 try
    //    //                                 {
    //    //                                     NewHTTPConnection.ProcessHTTP();
    //    //                                 }
    //    //                                 catch (Exception Exception)
    //    //                                 {
    //    //                                     var OnExceptionOccured_Local = _OnExceptionOccured;
    //    //                                     if (OnExceptionOccured_Local != null)
    //    //                                         OnExceptionOccured_Local(this, Exception);
    //    //                                 }

    //    //                             },
    //    //                         // Don't do it now, do it a bit later...
    //    //                         Autostart: false,
    //    //                         ThreadDescription: "HTTPServer<" + typeof(HTTPServiceInterface).Name + ">");

    //    //    _TCPServer.OnStarted += (Sender, Timestamp) => {
    //    //        if (OnStarted != null)
    //    //            OnStarted(this, Timestamp);
    //    //        };

    //    //    if (Autostart)
    //    //        _TCPServer.Start();

    //    //}

    //    //#endregion

    //    //#region HTTPServer(IPSocket, NewHTTPServiceHandler = null, Autostart = true)

    //    ///// <summary>
    //    ///// Initialize the HTTPServer using the given parameters.
    //    ///// </summary>
    //    ///// <param name="IPSocket">The listening IPSocket.</param>
    //    ///// <param name="NewHTTPServiceHandler">A delegate called for every new http connection.</param>
    //    ///// <param name="Autostart">Autostart the http server.</param>
    //    //public HTTPServer(IPSocket IPSocket, NewHTTPServiceHandler NewHTTPServiceHandler = null, Boolean Autostart = true)
    //    //    : this(IPSocket.IPAddress, IPSocket.Port, NewHTTPServiceHandler, Autostart)
    //    //{ }

    //    //#endregion

    //    #endregion


    //    #region (private) ParseInterface

    //    /// <summary>
    //    /// Parses the given HTTP service interface and
    //    /// adds method callbacks and event sources.
    //    /// </summary>
    //    private void ParseInterface()
    //    {

    //        #region Data

    //        HTTPMethod _HTTPMethod;
    //        String     _URITemplate;
    //        String     _Host = "*";
    //        String     _EventIdentification;
    //        UInt32     _MaxNumberOfCachedEvents  = 100;
    //        TimeSpan   _RetryIntervall           = TimeSpan.FromSeconds(30);
    //        Boolean    _IsSharedEventSource      = false;

    //        #endregion

    //        var HTTPServiceInterfaceType = typeof(HTTPServiceInterface);
    //        var HTTPServiceDiscovery     = new AutoDiscovery<HTTPServiceInterface>();

    //        if (HTTPServiceDiscovery != null && HTTPServiceDiscovery.Count >= 1)
    //        {
    //            foreach (var HTTPServiceImplementation in HTTPServiceDiscovery)
    //            {

    //                #region Initial checks

    //                if (HTTPServiceImplementation.HTTPContentTypes == null)
    //                    throw new ApplicationException("Please define an associated HTTPContentType for the '" + HTTPServiceImplementation.GetType().FullName + "' HTTP service implementation!");

    //                if (HTTPServiceImplementation.HTTPContentTypes.Count() != 1)
    //                    throw new ApplicationException("Less than and more than one HTTPContentType is currently not supported!");

    //                #endregion

    //                #region Register associated content type(s)

    //                foreach (var AssociatedContentType in HTTPServiceImplementation.HTTPContentTypes)
    //                {
    //                    if (!(Implementations.ContainsKey(AssociatedContentType)))
    //                        Implementations.Add(AssociatedContentType, HTTPServiceImplementation);
    //                    else
    //                        throw new ApplicationException("The content type '" + AssociatedContentType + "' is already associated with an HTTP service implementation called '" + Implementations[AssociatedContentType].GetType().FullName + "'!");
    //                }

    //                #endregion

    //                #region Get HTTPServiceInterface Attributes

    //                #region HTTPServiceAttribute

    //                var _HTTPServiceAttribute = HTTPServiceInterfaceType.GetCustomAttributes(typeof(HTTPServiceAttribute), false);

    //                if (_HTTPServiceAttribute == null || _HTTPServiceAttribute.Count() != 1)
    //                    throw new ApplicationException("Invalid HTTPServiceAttributes found!");

    //                    _Host = (_HTTPServiceAttribute[0] as HTTPServiceAttribute).Host;

    //                #endregion

    //                #region Check global Force-/NoAuthenticationAttribute

    //                var _GlobalNeedsExplicitAuthentication = false;

    //                if (HTTPServiceInterfaceType.GetCustomAttributes(typeof(NoAuthenticationAttribute), false) != null)
    //                    _GlobalNeedsExplicitAuthentication = false;

    //                if (HTTPServiceInterfaceType.GetCustomAttributes(typeof(ForceAuthenticationAttribute), false) != null)
    //                    _GlobalNeedsExplicitAuthentication = true;

    //                #endregion

    //                #endregion

    //                #region Add method callbacks and event sources

    //                var NeedsExplicitAuthentication = false;

    //                foreach (var _MethodInfo in HTTPServiceInterfaceType.GetRecursiveInterfaces().SelectMany(CI => CI.GetMethods()))
    //                {

    //                    _HTTPMethod                 = null;
    //                    _EventIdentification        = null;
    //                    _URITemplate                = "";
    //                    NeedsExplicitAuthentication = _GlobalNeedsExplicitAuthentication;

    //                    foreach (var _Attribute in _MethodInfo.GetCustomAttributes(true))
    //                    {

    //                        #region HTTPMappingAttribute

    //                        var _HTTPMappingAttribute = _Attribute as HTTPMappingAttribute;
    //                        if (_HTTPMappingAttribute != null)
    //                        {

    //                            if (_EventIdentification != null)
    //                                throw new Exception("URI '" + _URITemplate + "' is already registered as HTTP event source!");

    //                            _HTTPMethod  = _HTTPMappingAttribute.HTTPMethod;
    //                            _URITemplate = _HTTPMappingAttribute.UriTemplate;
    //                            continue;

    //                        }

    //                        #endregion

    //                        #region HTTPEventMappingAttribute

    //                        var _HTTPEventMappingAttribute = _Attribute as HTTPEventMappingAttribute;
    //                        if (_HTTPEventMappingAttribute != null)
    //                        {

    //                            if (_HTTPMethod != null)
    //                                throw new Exception("URI '" + _URITemplate + "' is already registered as HTTP method!");

    //                            _HTTPMethod               = _HTTPEventMappingAttribute.HTTPMethod;
    //                            _EventIdentification      = _HTTPEventMappingAttribute.EventIdentification;
    //                            _URITemplate              = _HTTPEventMappingAttribute.UriTemplate;
    //                            _MaxNumberOfCachedEvents  = _HTTPEventMappingAttribute.MaxNumberOfCachedEvents;
    //                            _RetryIntervall           = _HTTPEventMappingAttribute.RetryIntervall;
    //                            _IsSharedEventSource      = _HTTPEventMappingAttribute.IsSharedEventSource;
    //                            continue;

    //                        }

    //                        #endregion

    //                        #region NoAuthentication

    //                        var _NoAuthenticationAttribute = _Attribute as NoAuthenticationAttribute;
    //                        if (_NoAuthenticationAttribute != null)
    //                        {
    //                            NeedsExplicitAuthentication = false;
    //                            continue;
    //                        }

    //                        #endregion

    //                        #region ForceAuthentication

    //                        var _ForceAuthenticationAttribute = _Attribute as ForceAuthenticationAttribute;
    //                        if (_ForceAuthenticationAttribute != null)
    //                        {
    //                            NeedsExplicitAuthentication = true;
    //                            continue;
    //                        }

    //                        #endregion

    //                    }

    //                    #region Add MethodCallback or EventSource

    //                    if (_HTTPMethod != null && _URITemplate != null && _URITemplate != "")
    //                        foreach (var AssociatedContentType in HTTPServiceImplementation.HTTPContentTypes)
    //                        {
    //                            if (AssociatedContentType != HTTPContentType.EVENTSTREAM)
    //                                AddMethodCallback(_MethodInfo, _Host, _URITemplate, _HTTPMethod, AssociatedContentType, NeedsExplicitAuthentication);

    //                            else
    //                                if (_EventIdentification != null)
    //                                    AddEventSource(_MethodInfo, _Host, _URITemplate, _HTTPMethod, _EventIdentification, _MaxNumberOfCachedEvents, _RetryIntervall, _IsSharedEventSource, NeedsExplicitAuthentication);
    //                        }

    //                    #endregion

    //                }

    //                #endregion

    //            }
    //        }
    //        else
    //            throw new Exception("Could not find any valid implementation of the HTTP service interface '" + typeof(HTTPServiceInterface).FullName.ToString() + "'!");

    //    }

    //    #endregion




    //    #region ToString()

    //    /// <summary>
    //    /// Return a string represtentation of this object.
    //    /// </summary>
    //    public override String ToString()
    //    {

    //        var _TypeName         = this.GetType().Name;
    //        var _GenericArguments = this.GetType().GetGenericArguments();
    //        var _GenericTypeName  = "";

    //        if (_GenericArguments.Length > 0)
    //        {
    //            _GenericTypeName  = String.Concat("<", _GenericArguments[0].Name, ">");
    //            _TypeName         = _TypeName.Remove(_TypeName.Length - 2);
    //        }

    //        var _Running = "";
    //        if (_TCPServer.IsRunning) _Running = " (running)";

    //        return String.Concat(_TypeName, _GenericTypeName, " at ", _TCPServer.IPAddress.ToString(), ":", _TCPServer.Port, _Running);

    //    }

    //    #endregion

    //    #region Dispose()

    //    /// <summary>
    //    /// Dispose this HTTP server.
    //    /// </summary>
    //    public void Dispose()
    //    {
    //        Shutdown();
    //    }

    //    #endregion

    //}

    //#endregion

}
