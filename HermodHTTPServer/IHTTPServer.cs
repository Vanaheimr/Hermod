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

using eu.Vanaheimr.Hermod.Datastructures;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    public delegate void AccessLogDelegate(DateTime ServerTime, HTTPRequest Request, HTTPResponse HTTPResponse);
    public delegate void ErrorLogDelegate (DateTime ServerTime, HTTPRequest Request, HTTPResponse HTTPResponse, String Error = null, Exception LastException = null);

    /// <summary>
    /// The HTTP server interface.
    /// </summary>
    public interface IHTTPServer : ITCPServer
    {

        /// <summary>
        /// The HTTP server name.
        /// </summary>
        String ServerName { get; set; }

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        String DefaultServerName { get; }

        /// <summary>
        /// The URL mapping object.
        /// </summary>
        URLMapping URLMapping { get; }

        /// <summary>
        /// The HTTP security object.
        /// </summary>
        HTTPSecurity HTTPSecurity { get; set; }

        event AccessLogDelegate AccessLog;
        event ErrorLogDelegate  ErrorLog;

        void LogAccess(DateTime ServerTime, HTTPRequest Request, HTTPResponse HTTPResponse);
        void LogError (DateTime ServerTime, HTTPRequest Request, HTTPResponse HTTPResponse, String Error = null, Exception LastException = null);

    }

}
