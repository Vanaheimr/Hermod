/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Security.Authentication;
using System.Reflection;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class IHTTPServerExtensions
    {

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        public static HTTPEventSource<JObject> AddJSONEventSource(this IHTTPServer                 HTTPServer,
                                                                  HTTPAPI                          HTTPAPI,
                                                                  HTTPEventSource_Id               EventIdentification,
                                                                  UInt32                           MaxNumberOfCachedEvents      = 500,
                                                                  TimeSpan?                        RetryIntervall               = null,
                                                                  Boolean                          EnableLogging                = true,
                                                                  String?                          LogfilePath                  = null,
                                                                  String?                          LogfilePrefix                = null,
                                                                  Func<String, DateTime, String>?  LogfileName                  = null,
                                                                  String?                          LogfileReloadSearchPattern   = null)


            => HTTPServer.AddEventSource(
                              EventIdentification,
                              HTTPAPI,
                              MaxNumberOfCachedEvents,
                              RetryIntervall,
                              data => data.ToString(Newtonsoft.Json.Formatting.None),
                              JObject.Parse,
                              EnableLogging,
                              LogfilePath,
                              LogfilePrefix,
                              LogfileName,
                              LogfileReloadSearchPattern
                          );



        /// <summary>
        /// Add a HTTP Sever Sent Events source and a method call back for the given URL template.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="IncludeFilterAtRuntime">Include this events within the HTTP SSE output. Can e.g. be used to filter events by HTTP users.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public static HTTPEventSource<JObject> AddJSONEventSource(this IHTTPServer                    HTTPServer,
                                                                  HTTPEventSource_Id                  EventIdentification,
                                                                  HTTPAPI                             HTTPAPI,
                                                                  HTTPPath                            URLTemplate,

                                                                  UInt32                              MaxNumberOfCachedEvents      = 500,
                                                                  Func<HTTPEvent<JObject>, Boolean>?  IncludeFilterAtRuntime       = null,
                                                                  TimeSpan?                           RetryIntervall               = null,
                                                                  Boolean                             EnableLogging                = false,
                                                                  String?                             LogfilePath                  = null,
                                                                  String?                             LogfilePrefix                = null,
                                                                  Func<String, DateTime, String>?     LogfileName                  = null,
                                                                  String?                             LogfileReloadSearchPattern   = null,

                                                                  HTTPHostname?                       Hostname                     = null,
                                                                  HTTPMethod?                         HTTPMethod                   = null,
                                                                  HTTPContentType?                    HTTPContentType              = null,

                                                                  Boolean                             RequireAuthentication        = false,
                                                                  HTTPAuthentication?                 URIAuthentication            = null,
                                                                  HTTPAuthentication?                 HTTPMethodAuthentication     = null,

                                                                  HTTPDelegate?                       DefaultErrorHandler          = null)

            => HTTPServer.AddEventSource(
                              EventIdentification,
                              HTTPAPI,
                              URLTemplate,

                              MaxNumberOfCachedEvents,
                              IncludeFilterAtRuntime,
                              RetryIntervall,
                              data => data.ToString(Newtonsoft.Json.Formatting.None),
                              JObject.Parse,
                              EnableLogging,
                              LogfilePath,
                              LogfilePrefix,
                              LogfileName,
                              LogfileReloadSearchPattern,

                              Hostname,
                              HTTPMethod,
                              HTTPContentType,

                              RequireAuthentication,
                              URIAuthentication,
                              HTTPMethodAuthentication,

                              DefaultErrorHandler
                          );

    }



    public interface IHTTPServer : IDisposable
    {

        /// <summary>
        /// The default HTTP servername, used whenever no HTTP "Host"-header had been given.
        /// </summary>
        String                                                     DefaultServerName             { get; }

        /// <summary>
        /// An associated HTTP security object.
        /// </summary>
        HTTPSecurity?                                              HTTPSecurity                  { get; }

        /// <summary>
        /// The optional delegate to select a TLS server certificate.
        /// </summary>
        ServerCertificateSelectorDelegate?                         ServerCertificateSelector     { get; }

        /// <summary>
        /// The optional delegate to verify the TLS client certificate used for authentication.
        /// </summary>
        RemoteTLSClientCertificateValidationHandler<IHTTPServer>?  ClientCertificateValidator    { get; }

        /// <summary>
        /// The optional delegate to select the TLS client certificate used for authentication.
        /// </summary>
        LocalCertificateSelectionHandler?                          LocalCertificateSelector     { get; }

        /// <summary>
        /// The TLS protocol(s) allowed for this connection.
        /// </summary>
        SslProtocols                                               AllowedTLSProtocols           { get; }

        /// <summary>
        /// Is the server already started?
        /// </summary>
        Boolean                                                    IsStarted                     { get; }

        /// <summary>
        /// The current number of attached TCP clients.
        /// </summary>
        UInt64                                                     NumberOfClients               { get; }

        DNSClient                                                  DNSClient                     { get; }



        void AddMethodCallback(HTTPAPI                       HTTPAPI,
                               HTTPHostname                  Hostname,
                               HTTPMethod                    HTTPMethod,
                               HTTPPath                      URLTemplate,
                               HTTPContentType?              HTTPContentType             = null,
                               HTTPAuthentication?           URIAuthentication           = null,
                               HTTPAuthentication?           HTTPMethodAuthentication    = null,
                               HTTPAuthentication?           ContentTypeAuthentication   = null,
                               HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                               HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                               HTTPDelegate?                 DefaultErrorHandler         = null,
                               HTTPDelegate?                 HTTPDelegate                = null,
                               URLReplacement                AllowReplacement            = URLReplacement.Fail);

        void AddMethodCallback(HTTPAPI                       HTTPAPI,
                               HTTPHostname                  Hostname,
                               HTTPMethod                    HTTPMethod,
                               IEnumerable<HTTPPath>         URLTemplates,
                               HTTPContentType?              HTTPContentType             = null,
                               HTTPAuthentication?           URIAuthentication           = null,
                               HTTPAuthentication?           HTTPMethodAuthentication    = null,
                               HTTPAuthentication?           ContentTypeAuthentication   = null,
                               HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                               HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                               HTTPDelegate?                 DefaultErrorHandler         = null,
                               HTTPDelegate?                 HTTPDelegate                = null,
                               URLReplacement                AllowReplacement            = URLReplacement.Fail);

        void AddMethodCallback(HTTPAPI                       HTTPAPI,
                               HTTPHostname                  Hostname,
                               HTTPMethod                    HTTPMethod,
                               HTTPPath                      URLTemplate,
                               IEnumerable<HTTPContentType>  HTTPContentTypes,
                               HTTPAuthentication?           URIAuthentication           = null,
                               HTTPAuthentication?           HTTPMethodAuthentication    = null,
                               HTTPAuthentication?           ContentTypeAuthentication   = null,
                               HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                               HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                               HTTPDelegate?                 DefaultErrorHandler         = null,
                               HTTPDelegate?                 HTTPDelegate                = null,
                               URLReplacement                AllowReplacement            = URLReplacement.Fail);

        void AddMethodCallback(HTTPAPI                       HTTPAPI,
                               HTTPHostname                  Hostname,
                               HTTPMethod                    HTTPMethod,
                               IEnumerable<HTTPPath>         URLTemplates,
                               IEnumerable<HTTPContentType>  HTTPContentTypes,
                               HTTPAuthentication?           URIAuthentication           = null,
                               HTTPAuthentication?           HTTPMethodAuthentication    = null,
                               HTTPAuthentication?           ContentTypeAuthentication   = null,
                               HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                               HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                               HTTPDelegate?                 DefaultErrorHandler         = null,
                               HTTPDelegate?                 HTTPDelegate                = null,
                               URLReplacement                AllowReplacement            = URLReplacement.Fail);

        IHTTPServer AttachTCPPorts  (params IPPort[]   Ports);
        IHTTPServer AttachTCPSockets(params IPSocket[] Sockets);
        IHTTPServer DetachTCPPorts  (params IPPort[]   Ports);



        #region HTTP Server Sent Events

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        HTTPEventSource<TData> AddEventSource<TData>(HTTPEventSource_Id               EventIdentification,
                                                     HTTPAPI                          HTTPAPI,
                                                     UInt32                           MaxNumberOfCachedEvents      = 500,
                                                     TimeSpan?                        RetryIntervall               = null,
                                                     Func<TData, String>?             DataSerializer               = null,
                                                     Func<String, TData>?             DataDeserializer             = null,
                                                     Boolean                          EnableLogging                = true,
                                                     String?                          LogfilePath                  = null,
                                                     String?                          LogfilePrefix                = null,
                                                     Func<String, DateTime, String>?  LogfileName                  = null,
                                                     String?                          LogfileReloadSearchPattern   = null);

        /// <summary>
        /// Add a HTTP Sever Sent Events source and a method call back for the given URL template.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="IncludeFilterAtRuntime">Include this events within the HTTP SSE output. Can e.g. be used to filter events by HTTP users.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        HTTPEventSource<TData> AddEventSource<TData>(HTTPEventSource_Id                EventIdentification,
                                                     HTTPAPI                           HTTPAPI,
                                                     HTTPPath                          URLTemplate,

                                                     UInt32                            MaxNumberOfCachedEvents      = 500,
                                                     Func<HTTPEvent<TData>, Boolean>?  IncludeFilterAtRuntime       = null,
                                                     TimeSpan?                         RetryIntervall               = null,
                                                     Func<TData, String>?              DataSerializer               = null,
                                                     Func<String, TData>?              DataDeserializer             = null,
                                                     Boolean                           EnableLogging                = false,
                                                     String?                           LogfilePath                  = null,
                                                     String?                           LogfilePrefix                = null,
                                                     Func<String, DateTime, String>?   LogfileName                  = null,
                                                     String?                           LogfileReloadSearchPattern   = null,

                                                     HTTPHostname?                     Hostname                     = null,
                                                     HTTPMethod?                       HTTPMethod                   = null,
                                                     HTTPContentType?                  HTTPContentType              = null,

                                                     Boolean                           RequireAuthentication        = false,
                                                     HTTPAuthentication?               URIAuthentication            = null,
                                                     HTTPAuthentication?               HTTPMethodAuthentication     = null,

                                                     HTTPDelegate?                     DefaultErrorHandler          = null);

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        IHTTPEventSource? Get(HTTPEventSource_Id EventSourceIdentification);

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        IHTTPEventSource<TData>? Get<TData>(HTTPEventSource_Id EventSourceIdentification);



        /// <summary>
        /// Try to return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        Boolean TryGet(HTTPEventSource_Id     EventSourceIdentification,
                       out IHTTPEventSource?  EventSource);

        /// <summary>
        /// Try to return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        Boolean TryGet<TData>(HTTPEventSource_Id            EventSourceIdentification,
                              out IHTTPEventSource<TData>?  EventSource);


        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        IEnumerable<IHTTPEventSource>        EventSources       (Func<IHTTPEventSource, Boolean>?  IncludeEventSource   = null);

        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        IEnumerable<IHTTPEventSource<TData>> EventSources<TData>(Func<IHTTPEventSource, Boolean>?  IncludeEventSource   = null);

        #endregion


        void Redirect(HTTPAPI          HTTPAPI,
                      HTTPHostname     Hostname,
                      HTTPMethod       HTTPMethod,
                      HTTPPath         URLTemplate,
                      HTTPContentType  HTTPContentType,
                      HTTPPath         URITarget);
        void Redirect(HTTPAPI          HTTPAPI,
                      HTTPMethod       HTTPMethod,
                      HTTPPath         URLTemplate,
                      HTTPContentType  HTTPContentType,
                      HTTPPath         URITarget);

        void AddFilter(HTTPFilter1Delegate Filter);
        void AddFilter(HTTPFilter2Delegate Filter);

        void Rewrite  (HTTPRewrite1Delegate Rewrite);
        void Rewrite  (HTTPRewrite2Delegate Rewrite);

        Task<HTTPResponse> InvokeRequestHandle(HTTPRequest Request);
        Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String            Host,
                                                               String            URL,
                                                               HTTPMethod?       HTTPMethod        = null,
                                                               HTTPContentType?  HTTPContentType   = null,
                                                               HTTPStatusCode?   HTTPStatusCode    = null);

        Boolean Start();
        Boolean Start(TimeSpan Delay, Boolean InBackground = true);
        Boolean Shutdown(String?  Message   = null,
                         Boolean  Wait      = true);

    }

}
