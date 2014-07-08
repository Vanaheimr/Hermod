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
    /// A mapping tree from URI templates onto C# methods.
    /// </summary>
    public class URIMapping
    {

        #region Data

        private readonly static Object                                 Lock = new Object();
        private readonly        Dictionary<String, HostnameNode>       _HostnameNodes;
        private readonly        Dictionary<String, HTTPEventSource>    _EventSources;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new URI mapping tree.
        /// </summary>
        internal URIMapping()
        {
            _HostnameNodes  = new Dictionary<String, HostnameNode>();
            _EventSources   = new Dictionary<String, HTTPEventSource>();
        }

        #endregion


        // Method Callbacks

        #region (internal) AddHandler(MethodHandler, Hostnames, URITemplate, HTTPMethod = null, HTTPContentType = null, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="MethodHandler">The method to call.</param>
        /// <param name="Hosts">The HTTP hosts.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        internal void AddHandler(MethodInfo           MethodHandler,
                                 IEnumerable<String>  Hostnames,
                                 String               URITemplate,
                                 HTTPMethod           HTTPMethod                  = null,
                                 HTTPContentType      HTTPContentType             = null,
                                 HTTPAuthentication   HostAuthentication          = null,
                                 Boolean              URIAuthentication           = false,
                                 Boolean              HTTPMethodAuthentication    = false,
                                 Boolean              ContentTypeAuthentication   = false)

        {

            lock (Lock)
            {
                foreach (var Hostname in Hostnames)
                    AddHandler(MethodHandler,
                               Hostname,
                               URITemplate,
                               HTTPMethod,
                               HTTPContentType,
                               HostAuthentication,
                               URIAuthentication,
                               HTTPMethodAuthentication,
                               ContentTypeAuthentication);
             
            }

        }

        #endregion

        #region (internal) AddHandler(MethodHandler, Hostname, URITemplate, HTTPMethod = null, HTTPContentType = null, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="HTTPDelegate">A delegate called for each incoming HTTP request.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        internal void AddHandler(MethodInfo          MethodHandler,
                                 String              Hostname,
                                 String              URITemplate,
                                 HTTPMethod          HTTPMethod                  = null,
                                 HTTPContentType     HTTPContentType             = null,
                                 HTTPAuthentication  HostAuthentication          = null,
                                 Boolean             URIAuthentication           = false,
                                 Boolean             HTTPMethodAuthentication    = false,
                                 Boolean             ContentTypeAuthentication   = false)

        {

            lock (Lock)
            {

                #region Initial Checks

                if (MethodHandler == null)
                    throw new ArgumentNullException("MethodHandler", "The MethodHandler must not be null!");

                if (Hostname.IsNullOrEmpty())
                    Hostname = "*";

                URITemplate.FailIfNullOrEmpty();

                if (HTTPMethod == null && HTTPContentType != null)
                    throw new ArgumentNullException("If HTTPMethod is null the HTTPContentType must also be null!");

                #endregion

                #region AddOrGet HostnameNode

                HostnameNode _HostnameNode = null;
                if (!_HostnameNodes.TryGetValue(Hostname, out _HostnameNode))
                {
                    // Note: Use _.Hostname as the input is sanitized!
                    _HostnameNode = _HostnameNodes.AddAndReturnValue(_ => _.Hostname,
                                                                     new HostnameNode(Hostname, HostAuthentication));
                }

                #endregion

                #region AddOrUpdate URINode

                URINode _URINode = null;
                if (!_HostnameNode.URINodes.TryGetValue(URITemplate, out _URINode))
                {

                    if (HTTPMethod == null)
                        _URINode = new URINode(URITemplate, MethodHandler, URIAuthentication);

                    else
                        _URINode = new URINode(URITemplate, null, URIAuthentication);

                    _HostnameNode.URINodes.Add(URITemplate, _URINode);

                }

                if (HTTPMethod == null)
                    return;

                #endregion

                #region AddOrUpdate HTTPMethodNode

                HTTPMethodNode _HTTPMethodNode = null;
                if (!_URINode.HTTPMethods.TryGetValue(HTTPMethod, out _HTTPMethodNode))
                {

                    if (HTTPContentType == null)
                        _HTTPMethodNode = new HTTPMethodNode(HTTPMethod, MethodHandler, HTTPMethodAuthentication);

                    else
                        _HTTPMethodNode = new HTTPMethodNode(HTTPMethod, null, HTTPMethodAuthentication, HandleContentTypes: true);

                }

                _URINode.HTTPMethods.Add(HTTPMethod, _HTTPMethodNode);

                if (HTTPContentType == null)
                    return;

                #endregion

                #region AddOrUpdate ContentTypeNode

                ContentTypeNode _ContentTypeNode = null;
                if (!_HTTPMethodNode.HTTPContentTypes.TryGetValue(HTTPContentType, out _ContentTypeNode))
                    _ContentTypeNode = new ContentTypeNode(HTTPContentType, MethodHandler, ContentTypeAuthentication);

                _HTTPMethodNode.HTTPContentTypes.Add(HTTPContentType, _ContentTypeNode);

                #endregion

            }

        }

        #endregion

        #region (internal) AddHandler(MethodHandler, Hostname, URITemplate, HTTPMethod = null, HTTPContentType = null, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="HTTPDelegate">A delegate called for each incoming HTTP request.</param>
        /// <param name="Host">The HTTP host.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        internal void AddHandler(HTTPDelegate        HTTPDelegate,
                                 String              Hostname                    = "*",
                                 String              URITemplate                 = "/",
                                 HTTPMethod          HTTPMethod                  = null,
                                 HTTPContentType     HTTPContentType             = null,
                                 HTTPAuthentication  HostAuthentication          = null,
                                 Boolean             URIAuthentication           = false,
                                 Boolean             HTTPMethodAuthentication    = false,
                                 Boolean             ContentTypeAuthentication   = false)

        {

            lock (Lock)
            {

                #region Initial Checks

                if (HTTPDelegate == null)
                    throw new ArgumentNullException("HTTPDelegate", "The HTTPDelegate must not be null!");

                if (Hostname.IsNullOrEmpty())
                    Hostname = "*";

                if (URITemplate.IsNullOrEmpty())
                    URITemplate = "/";

                if (HTTPMethod == null && HTTPContentType != null)
                    throw new ArgumentNullException("If HTTPMethod is null the HTTPContentType must also be null!");

                #endregion

                #region AddOrUpdate HostNode

                HostnameNode _HostnameNode = null;
                if (!_HostnameNodes.TryGetValue(Hostname, out _HostnameNode))
                {
                    _HostnameNode = new HostnameNode(Hostname, HostAuthentication, HTTPDelegate);
                    _HostnameNodes.Add(Hostname, _HostnameNode);
                }

                #endregion

                #region AddOrUpdate URINode

                //URINode _URINode = null;
                //if (!_HostnameNode.URINodes.TryGetValue(URITemplate, out _URINode))
                //{

                //    if (HTTPMethod == null)
                //        _URINode = new URINode(URITemplate, HTTPDelegate, URIAuthentication);

                //    else
                //        _URINode = new URINode(URITemplate, null, URIAuthentication);

                //}

                //_HostnameNode.URINodes.Add(URITemplate, _URINode);

                //if (HTTPMethod == null)
                //    return;

                #endregion

                //#region AddOrUpdate HTTPMethodNode

                //HTTPMethodNode _HTTPMethodNode = null;
                //if (!_URINode.HTTPMethods.TryGetValue(HTTPMethod, out _HTTPMethodNode))
                //{

                //    if (HTTPContentType == null)
                //        _HTTPMethodNode = new HTTPMethodNode(HTTPMethod, HTTPDelegate, HTTPMethodAuthentication);

                //    else
                //        _HTTPMethodNode = new HTTPMethodNode(HTTPMethod, null, HTTPMethodAuthentication, HandleContentTypes: true);

                //}

                //_URINode.HTTPMethods.Add(HTTPMethod, _HTTPMethodNode);

                //if (HTTPContentType == null)
                //    return;

                //#endregion

                //#region AddOrUpdate ContentTypeNode

                //ContentTypeNode _ContentTypeNode = null;
                //if (!_HTTPMethodNode.HTTPContentTypes.TryGetValue(HTTPContentType, out _ContentTypeNode))
                //    _ContentTypeNode = new ContentTypeNode(HTTPContentType, HTTPDelegate, ContentTypeAuthentication);

                //_HTTPMethodNode.HTTPContentTypes.Add(HTTPContentType, _ContentTypeNode);

                //#endregion

            }

        }

        #endregion

        #region (internal) GetHandler(Host = "*", URL = "/", HTTPMethod = null, HTTPContentType = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        internal Tuple<HTTPDelegate, IEnumerable<Object>> GetHandler(String           Host              = "*",
                                                                     String           URL               = "/",
                                                                     HTTPMethod       HTTPMethod        = null,
                                                                     HTTPContentType  HTTPContentType   = null)
        {

            lock (Lock)
            {

                #region Set defaults...

                if (Host.IsNullOrEmpty())
                    Host = "*";

                if (URL.IsNullOrEmpty())
                    URL = "/";

                #endregion

                #region Get HostNode

                HostnameNode _HostNode = null;
                if (!_HostnameNodes.TryGetValue(Host, out _HostNode))
                    if (!_HostnameNodes.TryGetValue("*", out _HostNode))
                        return null;
                        //return GetErrorHandler(Host, URL, HTTPMethod, HTTPContentType, HTTPStatusCode.BadRequest);

                #endregion

                #region Try to find the best matching URLNode...

                var _RegexList    = from   __URLNode
                                    in     _HostNode.URINodes.Values
                                    select new {
                                        URLNode = __URLNode,
                                        Regex   = __URLNode.URIRegex
                                    };

                var _AllTemplates = from   _RegexTupel
                                    in     _RegexList
                                    select new {
                                        URLNode = _RegexTupel.URLNode,
                                        Match   = _RegexTupel.Regex.Match(URL)
                                    };

                var _Matches      = from    _Match
                                    in      _AllTemplates
                                    where   _Match.Match.Success
                                    orderby 100*_Match.URLNode.SortLength +
                                                _Match.URLNode.ParameterCount
                                            descending
                                    select  new {
                                        URLNode = _Match.URLNode,
                                        Match   = _Match.Match
                                    };

                #endregion

                #region If nothing found...

                if (!_Matches.Any())
                {

                    if (_HostNode.RequestHandler != null)
                        return new Tuple<HTTPDelegate, IEnumerable<Object>>(_HostNode.RequestHandler, new Object[0]);

                    return null;

                }

                #endregion

                #region ...else

                var _BestMatch    = _Matches.First();

                //Console.WriteLine(_BestMatch.URLNode.URLTemplate);
                //_Matches.ForEach(_m => { if (_m.Match.Success) Console.WriteLine("Match Length:'" + _m.Match.Length + "'+" + _m.URLNode.ParameterCount + " Groups[1]:'" + _m.Match.Groups[1] + "' URLTemplate:'" + _m.URLNode.URLTemplate + "'"); });

                // Copy MethodHandler Parameters
                var _Parameters = new List<Object>();
                for (var i=1; i< _BestMatch.Match.Groups.Count; i++)
                    _Parameters.Add(_BestMatch.Match.Groups[i].Value);

                // If no HTTPMethod was given => return best matching URL MethodHandler
                if (HTTPMethod == null)
                    //return new Tuple<MethodInfo, IEnumerable<Object>>(_BestMatch.URLNode.MethodHandler, _Parameters);
                    return null;

                #endregion

                #region Get HTTPMethodNode

                HTTPMethodNode _HTTPMethodNode = null;

                // If no HTTPMethod was found => return best matching URL MethodHandler
                if (!_BestMatch.URLNode.HTTPMethods.TryGetValue(HTTPMethod, out _HTTPMethodNode))
                    //return new Tuple<MethodInfo, IEnumerable<Object>>(_BestMatch.URLNode.MethodHandler, _Parameters);
                    return null;

                // If no HTTPContentType was given => return HTTPMethod MethodHandler
                if (HTTPContentType == null)
                    //return new Tuple<MethodInfo, IEnumerable<Object>>(_HTTPMethodNode.MethodHandler, _Parameters);
                    return null;

                #endregion

                #region Get ContentTypeNode

                ContentTypeNode _ContentTypeNode = null;

                if (_HTTPMethodNode.HTTPContentTypes.TryGetValue(HTTPContentType, out _ContentTypeNode))
                    //return new Tuple<MethodInfo, IEnumerable<Object>>(_ContentTypeNode.MethodHandler, _Parameters);
                    return null;

                #endregion

                //return GetErrorHandler(Host, URL, HTTPMethod, HTTPContentType, HTTPStatusCode.BadRequest);
                return null;

            }

        }

        #endregion


        // HTTP Server Sent Events

        #region (internal) AddEventSource(EventIdentification, MaxNumberOfCachedEvents = 100, RetryIntervall = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        internal HTTPEventSource AddEventSource(String     EventIdentification,
                                                UInt32     MaxNumberOfCachedEvents  = 100,
                                                TimeSpan?  RetryIntervall           = null)
        {

            lock (Lock)
            {

                if (_EventSources.ContainsKey(EventIdentification))
                    throw new ArgumentException("Duplicate event identification!");

                return _EventSources.AddAndReturnValue(EventIdentification,
                                                       new HTTPEventSource(EventIdentification, MaxNumberOfCachedEvents, RetryIntervall));

            }

        }

        #endregion

        #region (internal) AddEventSource(MethodInfo, Host, URITemplate, EventIdentification, MaxNumberOfCachedEvents = 100, RetryIntervall = null, IsSharedEventSource = false, HostAuthentication = false, URIAuthentication = false)

        /// <summary>
        /// Add a method call back for the given URI template and
        /// add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="MethodInfo">The method to call.</param>
        /// <param name="Host">The HTTP host.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="IsSharedEventSource">Whether this event source will be shared.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        internal HTTPEventSource AddEventSource(MethodInfo          MethodInfo,
                                                String              Host,
                                                String              URITemplate,
                                                HTTPMethod          HTTPMethod,
                                                String              EventIdentification,
                                                UInt32              MaxNumberOfCachedEvents  = 100,
                                                TimeSpan?           RetryIntervall           = null,
                                                Boolean             IsSharedEventSource      = false,
                                                HTTPAuthentication  HostAuthentication       = null,
                                                Boolean             URIAuthentication        = false)

        {

            lock (Lock)
            {

                HTTPEventSource _HTTPEventSource;

                if (IsSharedEventSource)
                {

                    if (!_EventSources.TryGetValue(EventIdentification, out _HTTPEventSource))
                        _HTTPEventSource = _EventSources.AddAndReturnValue(EventIdentification,
                                                                           new HTTPEventSource(EventIdentification, MaxNumberOfCachedEvents, RetryIntervall));

                }

                else
                {

                    if (_EventSources.ContainsKey(EventIdentification))
                        throw new ArgumentException("Duplicate event identification!");

                    _HTTPEventSource = _EventSources.AddAndReturnValue(EventIdentification,
                                                                       new HTTPEventSource(EventIdentification, MaxNumberOfCachedEvents, RetryIntervall));

                }

                AddHandler(MethodInfo, Host, URITemplate, HTTPMethod, HTTPContentType.EVENTSTREAM, HostAuthentication, URIAuthentication);

                return _HTTPEventSource;

            }

        }

        #endregion

        #region (internal) AddEventSourceHandler(MethodInfo, Host, URITemplate, HTTPMethod, HostAuthentication = false, URIAuthentication = false)

        /// <summary>
        /// Add an HTTP event source method handler for the given URI template.
        /// </summary>
        /// <param name="MethodInfo">The method to call.</param>
        /// <param name="Host">The HTTP host.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        internal void AddEventSourceHandler(MethodInfo          MethodInfo,
                                            String              Host,
                                            String              URITemplate,
                                            HTTPMethod          HTTPMethod,
                                            HTTPAuthentication  HostAuthentication  = null,
                                            Boolean             URIAuthentication   = false)

        {

            lock (Lock)
            {

                AddHandler(MethodInfo,
                           Host,
                           URITemplate,
                           HTTPMethod,
                           HTTPContentType.EVENTSTREAM,
                           HostAuthentication,
                           URIAuthentication);

            }

        }

        #endregion


        #region (internal) GetEventSource(EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        internal HTTPEventSource GetEventSource(String EventSourceIdentification)
        {

            EventSourceIdentification.FailIfNullOrEmpty();

            lock (Lock)
            {

                HTTPEventSource _HTTPEventSource;

                if (_EventSources.TryGetValue(EventSourceIdentification, out _HTTPEventSource))
                    return _HTTPEventSource;

                return null;

            }

        }

        #endregion

        #region (internal) TryGetEventSource(EventSourceIdentification, EventSource)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        internal Boolean TryGetEventSource(String EventSourceIdentification, out HTTPEventSource EventSource)
        {

            EventSourceIdentification.FailIfNullOrEmpty();

            lock (Lock)
            {
                return _EventSources.TryGetValue(EventSourceIdentification, out EventSource);
            }

        }

        #endregion

        #region (internal) GetEventSources(EventSourceSelector = null)

        /// <summary>
        /// An enumeration of all event sources.
        /// </summary>
        internal IEnumerable<HTTPEventSource> GetEventSources(Func<HTTPEventSource, Boolean> EventSourceSelector = null)
        {

            lock (Lock)
            {

                if (EventSourceSelector == null)
                    foreach (var EventSource in _EventSources.Values)
                        yield return EventSource;

                else
                    foreach (var EventSource in _EventSources.Values)
                        if (EventSourceSelector(EventSource))
                            yield return EventSource;

            }

        }

        #endregion


        // Error Handling

        #region (internal) GetErrorHandler(Host, URL, HTTPMethod = null, HTTPContentType = null, HTTPStatusCode = null)

        /// <summary>
        /// Return the best matching error handler for the given parameters.
        /// </summary>
        internal Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String           Host,
                                                                        String           URL, 
                                                                        HTTPMethod       HTTPMethod       = null,
                                                                        HTTPContentType  HTTPContentType  = null,
                                                                        HTTPStatusCode   HTTPStatusCode   = null)

        {

            lock (Lock)
            {
                return null;
            }

        }

        #endregion

    }

}
