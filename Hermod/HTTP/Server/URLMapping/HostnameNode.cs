/*
 * Copyright (c) 2010-2020, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Collections;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A node which stores information for maintaining multiple http hostnames.
    /// </summary>
    public class HostnameNode : IEnumerable<URL_Node>
    {

        #region Data

        /// <summary>
        /// A mapping from URIs to URINodes.
        /// </summary>
        private readonly Dictionary<HTTPPath, URL_Node> _URINodes;

        #endregion

        #region Properties

        /// <summary>
        /// The hostname for this (virtual) http service.
        /// </summary>
        public HTTPHostname  Hostname    { get; }


        /// <summary>
        /// Return all defined URIs.
        /// </summary>
        public IEnumerable<HTTPPath> URIs
            => _URINodes.Keys;

        /// <summary>
        /// Return all URI nodes.
        /// </summary>
        public IEnumerable<URL_Node> URLNodes
            => _URINodes.Values;

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new hostname node.
        /// </summary>
        /// <param name="Hostname">The hostname(s) for this (virtual) http service.</param>
        internal HostnameNode(HTTPHostname Hostname)
        {

            #region Check Hostname

            if (Hostname == null)
                throw new ArgumentNullException(nameof(Hostname), "The given HTTP hostname must not be null!");

            var    HostHeader  = Hostname.ToString().Split(new Char[1] { ':' }, StringSplitOptions.None).Select(v => v.Trim()).ToArray();
            UInt16 HostPort    = 80;

            // 1.2.3.4          => 1.2.3.4:80
            // 1.2.3.4:80       => ok
            // 1.2.3.4 : 80     => ok
            // 1.2.3.4:*        => ok
            // 1.2.3.4:a        => invalid
            // 1.2.3.4:80:      => ok
            // 1.2.3.4:80:0     => invalid

            // rfc 2616 - 3.2.2
            // If the port is empty or not given, port 80 is assumed.
            if (HostHeader.Length == 1)
                this.Hostname = HTTPHostname.Parse(Hostname + ":" + HostPort);

            else if ((HostHeader.Length == 2 && (!UInt16.TryParse(HostHeader[1], out HostPort) && HostHeader[1] != "*")) ||
                      HostHeader.Length  > 2)
                      throw new ArgumentException("Invalid Hostname!", nameof(Hostname));

            else
                this.Hostname = HTTPHostname.Parse(HostHeader[0] + ":" + HostHeader[1]);

            #endregion

            this._URINodes  = new Dictionary<HTTPPath, URL_Node>();

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPDelegate              HTTPDelegate,

                               HTTPPath?                 URLTemplate                 = null,
                               HTTPMethod?               Method                      = null,
                               HTTPContentType           HTTPContentType             = null,

                               HTTPAuthentication        URIAuthentication           = null,
                               HTTPAuthentication        HTTPMethodAuthentication    = null,
                               HTTPAuthentication        ContentTypeAuthentication   = null,

                               HTTPRequestLogHandler     HTTPRequestLogger           = null,
                               HTTPResponseLogHandler    HTTPResponseLogger          = null,

                               HTTPDelegate              DefaultErrorHandler         = null,
                               URLReplacement            AllowReplacement            = URLReplacement.Fail)

        {

            lock (_URINodes)
            {

                if (!URLTemplate.HasValue)
                    URLTemplate = HTTPPath.Parse("/");

                if (!_URINodes.TryGetValue(URLTemplate.Value, out URL_Node _URINode))
                {

                    _URINode = _URINodes.AddAndReturnValue(URLTemplate.Value,
                                                           new URL_Node(URLTemplate.Value,
                                                                       URIAuthentication));

                }

                _URINode.AddHandler(HTTPDelegate,

                                    Method ?? HTTPMethod.GET,
                                    HTTPContentType,

                                    HTTPMethodAuthentication,
                                    ContentTypeAuthentication,

                                    HTTPRequestLogger,
                                    HTTPResponseLogger,

                                    DefaultErrorHandler,
                                    AllowReplacement);

            }

        }

        #endregion


        #region Contains(URITemplate)

        /// <summary>
        /// Determines whether the given URI template is defined.
        /// </summary>
        /// <param name="URITemplate">An URI template.</param>
        public Boolean Contains(HTTPPath URITemplate)

            => _URINodes.ContainsKey(URITemplate);

        #endregion

        #region Get     (URITemplate)

        /// <summary>
        /// Return the URI node for the given URI template.
        /// </summary>
        /// <param name="URITemplate">An URI template.</param>
        public URL_Node Get(HTTPPath URITemplate)
        {

            if (_URINodes.TryGetValue(URITemplate, out URL_Node uriNode))
                return uriNode;

            return null;

        }

        #endregion

        #region TryGet  (URITemplate, out URINode)

        /// <summary>
        /// Return the URI node for the given URI template.
        /// </summary>
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="URINode">The attached URI node.</param>
        public Boolean TryGet(HTTPPath URITemplate, out URL_Node URINode)

            => _URINodes.TryGetValue(URITemplate, out URINode);

        #endregion


        #region IEnumerable<URINode> members

        /// <summary>
        /// Return all URI nodes.
        /// </summary>
        public IEnumerator<URL_Node> GetEnumerator()
            => _URINodes.Values.GetEnumerator();

        /// <summary>
        /// Return all URI nodes.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
            => _URINodes.Values.GetEnumerator();

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
