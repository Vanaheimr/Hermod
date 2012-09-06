/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Reflection;
using System.Collections.Concurrent;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A node which stores information for maintaining multiple http hosts.
    /// </summary>
    public class HostNode
    {

        #region Properties

        /// <summary>
        /// The hostname for this (virtual) http service.
        /// </summary>
        public String Hostname { get; private set; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit host authentication.
        /// </summary>
        public Boolean HostAuthentication { get; private set; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public MethodInfo HostErrorHandler { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public ConcurrentDictionary<HTTPStatusCode, MethodInfo> HostErrorHandlers { get; private set; }

        /// <summary>
        /// A mapping from URLs to URLNodes.
        /// </summary>
        public ConcurrentDictionary<String, URLNode> URLNodes { get; private set; }

        #endregion

        #region Constructor(s)

        #region HostNode(myHostname, myHostAuthentication = false, myHostError = null)

        /// <summary>
        /// Creates a new HostNode.
        /// </summary>
        /// <param name="Hostname">The hostname for this (virtual) http service.</param>
        /// <param name="HostAuthentication">This and all subordinated nodes demand an explicit host authentication.</param>
        /// <param name="HostErrorHandler">A general error handling method.</param>
        public HostNode(String Hostname, Boolean HostAuthentication = false, MethodInfo HostErrorHandler = null)
        {
            this.Hostname            = Hostname;
            this.HostAuthentication  = HostAuthentication;
            this.URLNodes            = new ConcurrentDictionary<String, URLNode>();
            this.HostErrorHandler    = HostErrorHandler;
            this.HostErrorHandlers   = new ConcurrentDictionary<HTTPStatusCode, MethodInfo>();
        }

        #endregion

        #endregion

        #region ToString()

        public override String ToString()
        {

            var _HostAuthentication = "";
            if (HostAuthentication)
                _HostAuthentication = " (auth)";

            var _HostErrorHandler = "";
            if (HostErrorHandler != null)
                _HostErrorHandler = " (errhdl)";

            return String.Concat(Hostname, _HostAuthentication, _HostErrorHandler);

        }

        #endregion

    }

}
