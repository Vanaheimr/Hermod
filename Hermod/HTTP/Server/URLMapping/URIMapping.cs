/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
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

        #region (internal) AddHandler(HTTPDelegate, Hostname = "*", URITemplate = "/", HTTPMethod = null, HTTPContentType = null, HostAuthentication = null, URIAuthentication = null, HTTPMethodAuthentication = null, ContentTypeAuthentication = null, DefaultErrorHandler = null)

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
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        internal void AddHandler(HTTPDelegate        HTTPDelegate,

                                 String              Hostname                    = "*",
                                 String              URITemplate                 = "/",
                                 HTTPMethod          HTTPMethod                  = null,
                                 HTTPContentType     HTTPContentType             = null,

                                 HTTPAuthentication  HostAuthentication          = null,
                                 HTTPAuthentication  URIAuthentication           = null,
                                 HTTPAuthentication  HTTPMethodAuthentication    = null,
                                 HTTPAuthentication  ContentTypeAuthentication   = null,

                                 HTTPDelegate        DefaultErrorHandler         = null,
                                 Boolean             AllowReplacement            = false)

        {

            lock (Lock)
            {

                #region Initial Checks

                if (HTTPDelegate == null)
                    throw new ArgumentNullException("HTTPDelegate", "The given parameter must not be null!");

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
                    _HostnameNode = new HostnameNode(Hostname, HostAuthentication, HTTPDelegate, DefaultErrorHandler);
                    _HostnameNodes.Add(Hostname, _HostnameNode);
                }

                #endregion

                _HostnameNode.AddHandler(HTTPDelegate,

                                         URITemplate,
                                         HTTPMethod,
                                         HTTPContentType,

                                         URIAuthentication,
                                         HTTPMethodAuthentication,
                                         ContentTypeAuthentication,

                                         DefaultErrorHandler,
                                         AllowReplacement);

            }

        }

        #endregion

        #region (internal) AddHandler(HTTPDelegate, Hostname = "*", URITemplate = "/", HTTPMethod = null, HTTPContentType = null, HostAuthentication = null, URIAuthentication = null, HTTPMethodAuthentication = null, ContentTypeAuthentication = null, DefaultErrorHandler = null)

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
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        internal void ReplaceHandler(HTTPDelegate        HTTPDelegate,

                                     String              Hostname                    = "*",
                                     String              URITemplate                 = "/",
                                     HTTPMethod          HTTPMethod                  = null,
                                     HTTPContentType     HTTPContentType             = null,

                                     HTTPAuthentication  HostAuthentication          = null,
                                     HTTPAuthentication  URIAuthentication           = null,
                                     HTTPAuthentication  HTTPMethodAuthentication    = null,
                                     HTTPAuthentication  ContentTypeAuthentication   = null,

                                     HTTPDelegate        DefaultErrorHandler         = null)

        {

            lock (Lock)
            {

                #region Initial Checks

                if (HTTPDelegate == null)
                    throw new ArgumentNullException("HTTPDelegate", "The given parameter must not be null!");

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
                    _HostnameNode = new HostnameNode(Hostname, HostAuthentication, HTTPDelegate, DefaultErrorHandler);
                    _HostnameNodes.Add(Hostname, _HostnameNode);
                }

                #endregion

                _HostnameNode.AddHandler(HTTPDelegate,

                                         URITemplate,
                                         HTTPMethod,
                                         HTTPContentType,

                                         URIAuthentication,
                                         HTTPMethodAuthentication,
                                         ContentTypeAuthentication,

                                         DefaultErrorHandler);

            }

        }

        #endregion

        #region (internal) InvokeHandler(HTTPRequest)

        /// <summary>
        /// Invoke the best matching method handler for the given parameters.
        /// </summary>
        /// <param name="HTTPRequest">A HTTP request.</param>
        internal HTTPResponse InvokeHandler(HTTPRequest HTTPRequest)
        {

            return GetHandler((HTTPRequest.Host.IsNullOrEmpty()) ? "*" : HTTPRequest.Host,
                              (HTTPRequest.URI. IsNullOrEmpty()) ? "/" : HTTPRequest.URI,
                               HTTPRequest.HTTPMethod,
                               AvailableContentTypes => HTTPRequest.Accept.BestMatchingContentType(AvailableContentTypes),
                               ParsedURIParameters   => HTTPRequest.ParsedURIParameters = ParsedURIParameters.ToArray())

                               (HTTPRequest);

        }

        #endregion

        #region (internal) GetHandler(Host = "*", URL = "/", HTTPMethod = HTTPMethod.GET, HTTPContentTypeSelector = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        internal HTTPDelegate GetHandler(String                                    Host,
                                         String                                    URI,
                                         HTTPMethod                                HTTPMethod                   = null,
                                         Func<HTTPContentType[], HTTPContentType>  HTTPContentTypeSelector      = null,
                                         Action<IEnumerable<String>>               ParsedURIParametersDelegate  = null)
        {

            Host                     = Host.IsNullOrEmpty()            ? "*"                            : Host;
            URI                      = URI. IsNullOrEmpty()            ? "/"                            : URI;
            HTTPMethod               = HTTPMethod              == null ? HTTPMethod.GET                 : HTTPMethod;
            HTTPContentTypeSelector  = HTTPContentTypeSelector == null ? v => HTTPContentType.HTML_UTF8 : HTTPContentTypeSelector;

            lock (Lock)
            {

                #region Get HostNode or "*" or fail

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
                                        Match   = _RegexTupel.Regex.Match(URI)
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

                #region ...or return HostNode

                if (!_Matches.Any())
                {

                    if (_HostNode.RequestHandler != null)
                        return _HostNode.RequestHandler;

                    return null;

                }

                #endregion


                HTTPMethodNode  _HTTPMethodNode       = null;
                ContentTypeNode _HTTPContentTypeNode  = null;

                // Caused e.g. by the naming of the variables within the
                // URI templates, there could be multiple matches!
                //foreach (var _Match in _Matches)
                //{

                // Use best matching URL Handler!
                var _Match2 = _Matches.First();

                    #region Copy MethodHandler Parameters

                    var _Parameters = new List<String>();
                    for (var i = 1; i < _Match2.Match.Groups.Count; i++)
                        _Parameters.Add(_Match2.Match.Groups[i].Value.RemoveLastSlash());

                    var ParsedURIParametersDelegateLocal = ParsedURIParametersDelegate;
                    if (ParsedURIParametersDelegateLocal != null)
                        ParsedURIParametersDelegate(_Parameters);

                    #endregion

                    // If HTTPMethod was found...
                    if (_Match2.URLNode.HTTPMethods.TryGetValue(HTTPMethod, out _HTTPMethodNode))
                    {

                        // Get HTTPContentTypeNode
                        if (!_HTTPMethodNode.HTTPContentTypes.TryGetValue(HTTPContentTypeSelector(_HTTPMethodNode.HTTPContentTypes.Keys.ToArray()), out _HTTPContentTypeNode))
                            return _HTTPMethodNode.RequestHandler;

                        return _HTTPContentTypeNode.RequestHandler;

                    }

                //}

                // No HTTPMethod was found => return best matching URL Handler
                return _Match2.URLNode.RequestHandler;

                //return GetErrorHandler(Host, URL, HTTPMethod, HTTPContentType, HTTPStatusCode.BadRequest);

            }

        }

        #endregion


        // HTTP Server Sent Events

        #region (internal) AddEventSource(EventIdentification, MaxNumberOfCachedEvents = 500, RetryIntervall = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        internal HTTPEventSource AddEventSource(String     EventIdentification,
                                                UInt32     MaxNumberOfCachedEvents  = 500,
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

        #region (internal) AddEventSource(MethodInfo, Host, URITemplate, EventIdentification, MaxNumberOfCachedEvents = 500, RetryIntervall = null, IsSharedEventSource = false, HostAuthentication = false, URIAuthentication = false)

        /// <summary>
        /// Add a method call back for the given URI template and
        /// add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="HTTPDelegate">The method to call.</param>
        /// <param name="Host">The HTTP host.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="IsSharedEventSource">Whether this event source will be shared.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        internal HTTPEventSource AddEventSource(String              EventIdentification,
                                                UInt32              MaxNumberOfCachedEvents     = 500,
                                                TimeSpan?           RetryIntervall              = null,

                                                String              Hostname                    = "*",
                                                String              URITemplate                 = "/",
                                                HTTPMethod          HTTPMethod                  = null,

                                                HTTPAuthentication  HostAuthentication          = null,
                                                HTTPAuthentication  URIAuthentication           = null,
                                                HTTPAuthentication  HTTPMethodAuthentication    = null,

                                                HTTPDelegate        DefaultErrorHandler         = null)

        {

            lock (Lock)
            {

                #region Get or Create Event Source

                HTTPEventSource _HTTPEventSource;

                if (!_EventSources.TryGetValue(EventIdentification, out _HTTPEventSource))
                    _HTTPEventSource = _EventSources.AddAndReturnValue(EventIdentification,
                                                                       new HTTPEventSource(EventIdentification, MaxNumberOfCachedEvents, RetryIntervall));

                #endregion

                #region Define HTTP Delegate

                HTTPDelegate _HTTPDelegate = HTTPRequest =>
                {

                    var _LastEventId        = 0UL;
                    var _Client_LastEventId = 0UL;
                    var _EventSource        = GetEventSource(EventIdentification);

                    if (HTTPRequest.TryGet<UInt64>("Last-Event-Id", out _Client_LastEventId))
                        _LastEventId = _Client_LastEventId;

                    var _HTTPEvents      = (from   _HTTPEvent
                                            in     _EventSource.GetAllEventsGreater(_LastEventId)
                                            where  _HTTPEvent != null
                                            select _HTTPEvent.ToString())
                                           .ToArray(); // For thread safety!

                    // Transform HTTP events into an UTF8 string
                    var _ResourceContent = String.Empty;

                    if (_HTTPEvents.Length > 0)
                        _ResourceContent = Environment.NewLine + _HTTPEvents.Aggregate((a, b) => a + Environment.NewLine + b) + Environment.NewLine;

                    else
                        _ResourceContent += Environment.NewLine + "retry: " + ((UInt32) _EventSource.RetryIntervall.TotalMilliseconds) + Environment.NewLine + Environment.NewLine;

                    return new HTTPResponseBuilder() {
                        HTTPStatusCode  = HTTPStatusCode.OK,
                        ContentType     = HTTPContentType.EVENTSTREAM,
                        CacheControl    = "no-cache",
                        Connection      = "keep-alive",
                        KeepAlive       = new KeepAliveType(TimeSpan.FromSeconds(2*_EventSource.RetryIntervall.TotalSeconds)),
                        Content         = _ResourceContent.ToUTF8Bytes()
                    };

                };

                #endregion

                AddHandler(_HTTPDelegate,
                           Hostname,
                           URITemplate,
                           (HTTPMethod == null) ? HTTPMethod.GET : HTTPMethod,
                           HTTPContentType.EVENTSTREAM,

                           HostAuthentication,
                           URIAuthentication,
                           HTTPMethodAuthentication,
                           null,

                           DefaultErrorHandler);

                return _HTTPEventSource;

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

            HTTPEventSource _HTTPEventSource;

            if (_EventSources.TryGetValue(EventSourceIdentification, out _HTTPEventSource))
                return _HTTPEventSource;

            return null;

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
