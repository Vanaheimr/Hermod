/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Concurrent;
using static org.GraphDefined.Vanaheimr.Hermod.HTTPTest.HTTPTestServerX;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    /// <summary>
    /// A URL node which stores some child nodes and a callback
    /// </summary>
    public class HTTPAPIX
    {

        #region Properties

        public HTTPTestServerX?              HTTPTestServer      { get; internal set; }

        /// <summary>
        /// The HTTP hostname of this HTTP API.
        /// </summary>
        public IEnumerable<HTTPHostname>     Hostnames           { get; }

        /// <summary>
        /// The HTTP root path of this HTTP API.
        /// </summary>
        public HTTPPath                      RootPath            { get; }

        /// <summary>
        /// The HTTP content types served by this HTTP API.
        /// </summary>
        public IEnumerable<HTTPContentType>  HTTPContentTypes    { get; }

        /// <summary>
        /// An optional description of this HTTP API.
        /// </summary>
        public I18NString?                   Description         { get; }

        #endregion

        #region Constructor(s)

        public HTTPAPIX(HTTPTestServerX?               HTTPTestServer     = null,
                        IEnumerable<HTTPHostname>?     Hostnames          = null,
                        HTTPPath?                      RootPath           = null,
                        IEnumerable<HTTPContentType>?  HTTPContentTypes   = null,
                        I18NString?                    Description        = null)
        {

            this.Hostnames         = Hostnames?.       Distinct() ?? [];
            this.RootPath          = RootPath                     ?? HTTPPath.Root;
            this.HTTPContentTypes  = HTTPContentTypes?.Distinct() ?? [];
            this.Description       = Description                  ?? I18NString.Empty;
            this.HTTPTestServer    = HTTPTestServer;

        }

        #endregion


        private readonly ConcurrentDictionary<String, RouteNode2> routeNodes = [];

        #region (internal) AddHandler(HTTPDelegate, Hostname = "*", URLTemplate = "/", HTTPMethod = null, HTTPContentType = null, HostAuthentication = null, URLAuthentication = null, HTTPMethodAuthentication = null, ContentTypeAuthentication = null, DefaultErrorHandler = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="HTTPDelegate">A delegate called for each incoming HTTP request.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public void AddHandler(HTTPPath                                   URLTemplate,
                               HTTPDelegate                               HTTPDelegate,

                               HTTPMethod?                                HTTPMethod                  = null,
                               HTTPContentType?                           HTTPContentType             = null,
                            //   Boolean                                    OpenEnd                     = false,

                               HTTPAuthentication?                        URLAuthentication           = null,
                               HTTPAuthentication?                        HTTPMethodAuthentication    = null,
                               HTTPAuthentication?                        ContentTypeAuthentication   = null,

                               OnHTTPRequestLogDelegate?                  HTTPRequestLogger           = null,
                               OnHTTPResponseLogDelegate?                 HTTPResponseLogger          = null,

                               HTTPDelegate?                              DefaultErrorHandler         = null,
                               Dictionary<HTTPStatusCode, HTTPDelegate>?  ErrorHandlers               = null,
                               URLReplacement                             AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial Checks

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate), "The given parameter must not be null!");

            if (HTTPMethod is null && HTTPContentType is not null)
                throw new ArgumentException("If HTTPMethod is null the HTTPContentType must also be null!");

            #endregion

            var requestHandle = new HTTPRequestHandleX(
                                    this,
                                    HTTPDelegate,
                                    HTTPRequestLogger,
                                    HTTPResponseLogger,
                                    DefaultErrorHandler,
                                    ErrorHandlers
                                );

            var segments      = URLTemplate.ToString().Trim('/').Split('/');

            var routeNode1    = routeNodes.GetOrAdd(
                                    segments[0],
                                    hh => {
                                              if (hh.StartsWith('{') && hh.EndsWith('}'))
                                              {

                                                   var paramName = hh[1..^1];

                                                   if (("/" + hh) == URLTemplate.ToString())
                                                   {
                                                       return RouteNode2.ForCatchRestOfPath(
                                                                  "/" + hh,
                                                                  paramName,
                                                                  requestHandle
                                                              );
                                                   }

                                                   return RouteNode2.ForParameter(
                                                              "/" + hh,
                                                              paramName,
                                                              requestHandle
                                                          );
                                              }
                                              else
                                                  return RouteNode2.FromPath("/" + segments[0], HTTPPath.Root.ToString(), requestHandle);
                                          }
                                );

            foreach (var segment in segments.Skip(1))
            {

                var routeNode2 = routeNode1.Children.GetOrAdd(
                                     segment,
                                     ss => {

                                               if (ss.StartsWith('{') && ss.EndsWith('}'))
                                               {

                                                   var paramName = ss[1..^1];

                                                   if ((routeNode1.FullPath + "/" + ss) == URLTemplate.ToString())
                                                   {
                                                       return RouteNode2.ForCatchRestOfPath(
                                                                  routeNode1.FullPath + "/" + ss,
                                                                  paramName,
                                                                  requestHandle
                                                              );
                                                   }

                                                   return RouteNode2.ForParameter(
                                                              routeNode1.FullPath + "/" + ss,
                                                              paramName,
                                                              requestHandle
                                                          );

                                               }

                                         return RouteNode2.FromPath(
                                                    routeNode1.FullPath + "/" + ss,
                                                    "/" + ss,
                                                    requestHandle
                                                );

                                            }
                                 );

                routeNode1 = routeNode2;

            }

        }

        #endregion


        internal (HTTPRequestHandleX?, Dictionary<String, String>)

            GetRequestHandle(HTTPHostname                               Host,
                             HTTPPath                                   Path,
                             out String?                                ErrorResponse,
                             HTTPMethod?                                HTTPMethod                    = null,
                             Func<HTTPContentType[], HTTPContentType>?  HTTPContentTypeSelector       = null,
                             Action<IEnumerable<String>>?               ParsedURLParametersDelegate   = null)

        {

            var segments    = Path.ToString().Trim('/').Split('/');
            var parameters  = new Dictionary<String, String>();
            ErrorResponse   = null;

            if (!routeNodes.TryGetValue(segments[0], out var routeNode))
            {

                var parameterCatcher = routeNodes.Values.FirstOrDefault(routeNode => routeNode.ParamName is not null);
                if (parameterCatcher is not null && parameterCatcher.ParamName is not null)
                {

                    parameters.Add(
                        parameterCatcher.ParamName,
                        parameterCatcher.CatchRestOfPath
                            ? Path.ToString().TrimStart('/')
                            : segments[0]
                    );

                    return (parameterCatcher.RequestHandle, parameters);

                }
                else
                {
                    ErrorResponse = $"Unknown path {Path}!";
                    return (null, parameters);
                }

            }
            else
            {
                if (routeNode.ParamName is not null)
                {
                    parameters.Add(
                        routeNode.ParamName,
                        routeNode.CatchRestOfPath
                            ? segments.Skip(1).AggregateWith('/')
                            : segments[0]
                    );
                }
            }

            if (segments.Length > 1)
                for (var i = 1; i < segments.Length; i++)
                {

                    if (!routeNode.Children.TryGetValue(segments[i], out var routeNode2))
                    {

                        var parameterCatcher = routeNode.Children.Values.FirstOrDefault(routeNode => routeNode.ParamName is not null);
                        if (parameterCatcher is not null && parameterCatcher.ParamName is not null)
                        {

                            parameters.Add(
                                parameterCatcher.ParamName,
                                parameterCatcher.CatchRestOfPath
                                    ? segments.Skip(i).AggregateWith('/')
                                    : segments[i]
                            );

                            return (parameterCatcher.RequestHandle, parameters);

                        }
                        else
                        {
                            ErrorResponse = $"Unknown path {Path}!";
                            return (null, parameters);
                        }

                    }

                    routeNode = routeNode2;

                }

            ErrorResponse = "error!";
            return (null, parameters);

        }


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   Hostnames.Any()
                       ? $" ({Hostnames.AggregateCSV()})"
                       : String.Empty,

                   RootPath,

                   HTTPContentTypes.Any()
                       ? $" ({HTTPContentTypes.AggregateCSV()})"
                       : String.Empty

               );

        #endregion

    }

}
