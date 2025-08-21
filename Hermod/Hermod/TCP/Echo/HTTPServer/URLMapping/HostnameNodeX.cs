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

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    /// <summary>
    /// A node which stores information for maintaining multiple http hostnames.
    /// </summary>
    public class HostnameNodeX : IEnumerable<URL_NodeX>
    {

        #region Data

        private        readonly  ConcurrentDictionary<HTTPPath, URL_NodeX>  urlNodes    = [];

        private static readonly  Char[]                                     separator   = [ ':' ];

        #endregion

        #region Properties

        /// <summary>
        /// The hosting HTTP API.
        /// </summary>
        public HTTPAPIX               HTTPAPI     { get; }

        /// <summary>
        /// The hostname for this http service.
        /// </summary>
        public HTTPHostname           Hostname    { get; }


        /// <summary>
        /// Return all defined URIs.
        /// </summary>
        public IEnumerable<HTTPPath>  URIs
            => urlNodes.Keys;

        /// <summary>
        /// Return all URI nodes.
        /// </summary>
        public IEnumerable<URL_NodeX>  URLNodes
            => urlNodes.Values;

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new hostname node.
        /// </summary>
        /// <param name="Hostname">The hostname(s) for this http service.</param>
        internal HostnameNodeX(HTTPAPIX      HTTPAPI,
                               HTTPHostname  Hostname)
        {

            #region Check Hostname

            var    hostHeader  = Hostname.ToString().Split(separator, StringSplitOptions.None).Select(v => v.Trim()).ToArray();
            UInt16 hostPort    = 80;

            // 1.2.3.4          => 1.2.3.4:80
            // 1.2.3.4:80       => ok
            // 1.2.3.4 : 80     => ok
            // 1.2.3.4:*        => ok
            // 1.2.3.4:a        => invalid
            // 1.2.3.4:80:      => ok
            // 1.2.3.4:80:0     => invalid

            // rfc 2616 - 3.2.2
            // If the port is empty or not given, port 80 is assumed.
            if (hostHeader.Length == 1)
                this.Hostname = HTTPHostname.Parse(Hostname + ":" + hostPort);

            else if ((hostHeader.Length == 2 && !UInt16.TryParse(hostHeader[1], out hostPort) && hostHeader[1] != "*") ||
                      hostHeader.Length  > 2)
                      throw new ArgumentException("Invalid Hostname!", nameof(Hostname));

            else
                this.Hostname = HTTPHostname.Parse(hostHeader[0] + ":" + hostHeader[1]);

            #endregion

            this.HTTPAPI   = HTTPAPI;

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPAPIX                     HTTPAPI,
                               HTTPDelegate                 HTTPDelegate,

                               HTTPPath?                    URLTemplate                 = null,
                               Boolean                      OpenEnd                     = false,
                               HTTPMethod?                  Method                      = null,
                               HTTPContentType?             HTTPContentType             = null,

                               HTTPAuthentication?          URLAuthentication           = null,
                               HTTPAuthentication?          HTTPMethodAuthentication    = null,
                               HTTPAuthentication?          ContentTypeAuthentication   = null,

                               OnHTTPRequestLogDelegate2?   HTTPRequestLogger           = null,
                               OnHTTPResponseLogDelegate2?  HTTPResponseLogger          = null,

                               HTTPDelegate?                DefaultErrorHandler         = null,
                               URLReplacement               AllowReplacement            = URLReplacement.Fail)

        {

            if (!URLTemplate.HasValue)
                URLTemplate = HTTPPath.Root;

            if (!urlNodes.TryGetValue(URLTemplate.Value, out var urlNode))
                urlNode = urlNodes.AddAndReturnValue(
                              URLTemplate.Value,
                              new URL_NodeX(
                                  HTTPAPI,
                                  URLTemplate.Value,
                                  OpenEnd,
                                  URLAuthentication
                              )
                          );

            urlNode.AddHandler(
                        HTTPAPI,
                        HTTPDelegate,

                        Method ?? HTTPMethod.GET,
                        HTTPContentType,

                        HTTPMethodAuthentication,
                        ContentTypeAuthentication,

                        HTTPRequestLogger,
                        HTTPResponseLogger,

                        DefaultErrorHandler,
                        AllowReplacement
                    );

        }

        #endregion


        #region Contains(HTTPPath)

        /// <summary>
        /// Determines whether the given HTTP path template is defined.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path template.</param>
        public Boolean Contains(HTTPPath HTTPPath)

            => urlNodes.ContainsKey(HTTPPath);

        #endregion

        #region Get     (HTTPPath)

        /// <summary>
        /// Return the URL node for the given HTTP path template.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path template.</param>
        public URL_NodeX? Get(HTTPPath HTTPPath)
        {

            if (urlNodes.TryGetValue(HTTPPath, out var urlNode))
                return urlNode;

            return null;

        }

        #endregion

        #region TryGet  (HTTPPath, out URLNode)

        /// <summary>
        /// Return the URL node for the given HTTP path template.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path template.</param>
        /// <param name="URLNode">The attached URL node.</param>
        public Boolean TryGet(HTTPPath HTTPPath, [NotNullWhen(true)] out URL_NodeX? URLNode)

            => urlNodes.TryGetValue(HTTPPath, out URLNode);

        #endregion


        #region IEnumerable<URINode> Members

        /// <summary>
        /// Return all URI nodes.
        /// </summary>
        public IEnumerator<URL_NodeX> GetEnumerator()
            => urlNodes.Values.GetEnumerator();

        /// <summary>
        /// Return all URI nodes.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
            => urlNodes.Values.GetEnumerator();

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Hostname.ToString();

        #endregion

    }

}
