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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A delegate used whenever a HTTP request was received.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the request.</param>
    /// <param name="HTTPServer">The HTTP server.</param>
    /// <param name="Request">The HTTP request.</param>
    public delegate void OnHTTPRequestDelegate(DateTime Timestamp, HTTPServer HTTPServer, HTTPRequest Request);


    /// <summary>
    /// A delegate used whenever a HTTP response was sent.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the response.</param>
    /// <param name="HTTPServer">The HTTP server.</param>
    /// <param name="Request">The HTTP request.</param>
    /// <param name="Response">The HTTP response.</param>
    public delegate void OnHTTPResponseDelegate(DateTime Timestamp, HTTPServer HTTPServer, HTTPRequest Request, HTTPResponse Response);

}
