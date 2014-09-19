///*
// * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
// * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
// *
// * Licensed under the Apache License, Version 2.0 (the "License");
// * you may not use this file except in compliance with the License.
// * You may obtain a copy of the License at
// *
// *     http://www.apache.org/licenses/LICENSE-2.0
// *
// * Unless required by applicable law or agreed to in writing, software
// * distributed under the License is distributed on an "AS IS" BASIS,
// * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// * See the License for the specific language governing permissions and
// * limitations under the License.
// */

//#region Usings

//using System;
//using System.Reflection;
//using System.Collections.Generic;

//using org.GraphDefined.Vanaheimr.Styx.Arrows;

//#endregion

//namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
//{

//    /// <summary>
//    /// The HTTP server interface.
//    /// </summary>
//    public interface IHTTPServer : IBoomerangSender<String, DateTime, HTTPRequest, HTTPResponse>
//    {

//        #region Properties

//        /// <summary>
//        /// The default HTTP server name, used whenever
//        /// no host-header had been given.
//        /// </summary>
//        String        DefaultServerName     { get; }

//        /// <summary>
//        /// The HTTP security object.
//        /// </summary>
//        HTTPSecurity  HTTPSecurity          { get; }

//        #endregion

//        #region Events

//        event BoomerangSenderHandler<String, DateTime, HTTPRequest, HTTPResponse> OnNotification;

//        /// <summary>
//        /// An event called whenever a request came in.
//        /// </summary>
//        event RequestLogHandler  RequestLog;

//        /// <summary>
//        /// An event called whenever a request could successfully be processed.
//        /// </summary>
//        event AccessLogHandler   AccessLog;

//        /// <summary>
//        /// An event called whenever a request resulted in an error.
//        /// </summary>
//        event ErrorLogHandler    ErrorLog;

//        #endregion


//        #region Add Method Callbacks

//        /// <summary>
//        /// Add a method callback for the given URI template.
//        /// </summary>
//        /// <param name="MethodHandler">The method to call.</param>
//        /// <param name="Host">The HTTP host.</param>
//        /// <param name="URITemplate">The URI template.</param>
//        /// <param name="HTTPMethod">The HTTP method.</param>
//        /// <param name="HTTPContentType">The HTTP content type.</param>
//        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
//        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
//        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
//        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
//        void AddMethodCallback(MethodInfo          MethodHandler,
//                               String              Host,
//                               String              URITemplate,
//                               HTTPMethod          HTTPMethod,
//                               HTTPContentType     HTTPContentType             = null,
//                               HTTPAuthentication  HostAuthentication          = null,
//                               Boolean             URIAuthentication           = false,
//                               Boolean             HTTPMethodAuthentication    = false,
//                               Boolean             ContentTypeAuthentication   = false);

//        #endregion

//        #region Get Method Callbacks

//        /// <summary>
//        /// Return the best matching method handler for the given parameters.
//        /// </summary>
//        Tuple<HTTPDelegate, IEnumerable<Object>> GetHandler(String           Host,
//                                                            String           URL               = "/",
//                                                            HTTPMethod       HTTPMethod        = null,
//                                                            HTTPContentType  HTTPContentType   = null);

//        #endregion


//        #region Add HTTP Server Sent Events

//        /// <summary>
//        /// Add a HTTP Sever Sent Events source.
//        /// </summary>
//        /// <param name="EventIdentification">The unique identification of the event source.</param>
//        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
//        /// <param name="RetryIntervall">The retry intervall.</param>
//        HTTPEventSource AddEventSource(String      EventIdentification,
//                                       UInt32      MaxNumberOfCachedEvents  = 100,
//                                       TimeSpan?   RetryIntervall           = null);

//        /// <summary>
//        /// Add a method call back for the given URI template and
//        /// add a HTTP Sever Sent Events source.
//        /// </summary>
//        /// <param name="MethodInfo">The method to call.</param>
//        /// <param name="Host">The HTTP host.</param>
//        /// <param name="URITemplate">The URI template.</param>
//        /// <param name="HTTPMethod">The HTTP method.</param>
//        /// <param name="EventIdentification">The unique identification of the event source.</param>
//        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
//        /// <param name="RetryIntervall">The retry intervall.</param>
//        /// <param name="IsSharedEventSource">Whether this event source will be shared.</param>
//        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
//        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
//        HTTPEventSource AddEventSource(MethodInfo          MethodInfo,
//                                       String              Host,
//                                       String              URITemplate,
//                                       HTTPMethod          HTTPMethod,
//                                       String              EventIdentification,
//                                       UInt32              MaxNumberOfCachedEvents  = 100,
//                                       TimeSpan?           RetryIntervall           = null,
//                                       Boolean             IsSharedEventSource      = false,
//                                       HTTPAuthentication  HostAuthentication       = null,
//                                       Boolean             URIAuthentication        = false);

//        /// <summary>
//        /// Add an HTTP event source method handler for the given URI template.
//        /// </summary>
//        /// <param name="MethodInfo">The method to call.</param>
//        /// <param name="Host">The HTTP host.</param>
//        /// <param name="URITemplate">The URI template.</param>
//        /// <param name="HTTPMethod">The HTTP method.</param>
//        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
//        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
//        void AddEventSourceHandler    (MethodInfo          MethodInfo,
//                                       String              Host,
//                                       String              URITemplate,
//                                       HTTPMethod          HTTPMethod,
//                                       HTTPAuthentication  HostAuthentication  = null,
//                                       Boolean             URIAuthentication   = false);

//        #endregion

//        #region Get HTTP Server Sent Events

//        /// <summary>
//        /// Return the event source identified by the given event source identification.
//        /// </summary>
//        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
//        HTTPEventSource GetEventSource(String EventSourceIdentification);

//        /// <summary>
//        /// Return the event source identified by the given event source identification.
//        /// </summary>
//        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
//        /// <param name="EventSource">The event source.</param>
//        Boolean TryGetEventSource(String EventSourceIdentification, out HTTPEventSource EventSource);

//        /// <summary>
//        /// An enumeration of all event sources.
//        /// </summary>
//        IEnumerable<HTTPEventSource> GetEventSources(Func<HTTPEventSource, Boolean> EventSourceSelector = null);

//        #endregion


//        #region ErrorHandling

//        /// <summary>
//        /// Return the best matching error handler for the given parameters.
//        /// </summary>
//        Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String           Host,
//                                                               String           URL, 
//                                                               HTTPMethod       HTTPMethod       = null,
//                                                               HTTPContentType  HTTPContentType  = null,
//                                                               HTTPStatusCode   HTTPStatusCode   = null);

//        #endregion

//        #region Logging

//        /// <summary>
//        /// Log an incoming request.
//        /// </summary>
//        /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
//        /// <param name="Request">The incoming request.</param>
//        void LogRequest(DateTime ServerTimestamp, HTTPRequest Request);

//        /// <summary>
//        /// Log an successful request processing.
//        /// </summary>
//        /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
//        /// <param name="Request">The incoming request.</param>
//        /// <param name="Response">The outgoing response.</param>
//        void LogAccess(DateTime ServerTimestamp, HTTPRequest Request, HTTPResponse Response);

//        /// <summary>
//        /// Log an error during request processing.
//        /// </summary>
//        /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
//        /// <param name="Request">The incoming request.</param>
//        /// <param name="HTTPResponse">The outgoing response.</param>
//        /// <param name="Error">The occured error.</param>
//        /// <param name="LastException">The last occured exception.</param>
//        void LogError (DateTime ServerTimestamp, HTTPRequest Request, HTTPResponse HTTPResponse, String Error = null, Exception LastException = null);

//        #endregion

//    }

//}
