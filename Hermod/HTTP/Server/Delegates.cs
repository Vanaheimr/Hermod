/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The delegate for the HTTP request log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="HTTPServer">The sending HTTP server.</param>
    /// <param name="Request">The incoming request.</param>
    public delegate Task RequestLogHandler(DateTime     Timestamp,
                                           IHTTPServer  HTTPServer,
                                           HTTPRequest  Request);

    /// <summary>
    /// The delegate for the HTTP request log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="HTTPAPI">The sending HTTP API.</param>
    /// <param name="Request">The incoming request.</param>
    public delegate Task HTTPRequestLogHandler(DateTime     Timestamp,
                                               HTTPAPI      HTTPAPI,
                                               HTTPRequest  Request);


    /// <summary>
    /// The delegate for the HTTP access log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="HTTPServer">The sending HTTP server.</param>
    /// <param name="Request">The incoming request.</param>
    /// <param name="Response">The outgoing response.</param>
    public delegate Task AccessLogHandler(DateTime      Timestamp,
                                          IHTTPServer   HTTPServer,
                                          HTTPRequest   Request,
                                          HTTPResponse  Response);

    /// <summary>
    /// The delegate for the HTTP access log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="HTTPAPI">The sending HTTP API.</param>
    /// <param name="Request">The incoming request.</param>
    /// <param name="Response">The outgoing response.</param>
    public delegate Task HTTPResponseLogHandler(DateTime      Timestamp,
                                                HTTPAPI       HTTPAPI,
                                                HTTPRequest   Request,
                                                HTTPResponse  Response);


    /// <summary>
    /// The delegate for the HTTP error log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="HTTPServer">The sending HTTP server.</param>
    /// <param name="Request">The incoming request.</param>
    /// <param name="Response">The outgoing response.</param>
    /// <param name="Error">The occured error.</param>
    /// <param name="LastException">The last occured exception.</param>
    public delegate Task ErrorLogHandler(DateTime      Timestamp,
                                         IHTTPServer   HTTPServer,
                                         HTTPRequest   Request,
                                         HTTPResponse  Response,
                                         String        Error          = null,
                                         Exception     LastException  = null);

    /// <summary>
    /// The delegate for the HTTP error log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="HTTPAPI">The sending HTTP API.</param>
    /// <param name="Request">The incoming request.</param>
    /// <param name="Response">The outgoing response.</param>
    /// <param name="Error">The occured error.</param>
    /// <param name="LastException">The last occured exception.</param>
    public delegate Task HTTPErrorLogHandler(DateTime      Timestamp,
                                             HTTPAPI       HTTPAPI,
                                             HTTPRequest   Request,
                                             HTTPResponse  Response,
                                             String        Error          = null,
                                             Exception     LastException  = null);





    /// <summary>
    /// The delegate for logging the HTTP request send by a HTTP client.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the outgoing HTTP request.</param>
    /// <param name="HTTPClient">The HTTP client sending the HTTP request.</param>
    /// <param name="Request">The outgoing HTTP request.</param>
    public delegate Task ClientRequestLogHandler(DateTime     Timestamp,
                                                 HTTPClient   HTTPClient,
                                                 HTTPRequest  Request);


    /// <summary>
    /// The delegate for logging the HTTP response received by a HTTP client.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming HTTP response.</param>
    /// <param name="HTTPClient">The HTTP client receiving the HTTP request.</param>
    /// <param name="Request">The outgoing HTTP request.</param>
    /// <param name="Response">The incoming HTTP response.</param>
    public delegate Task ClientResponseLogHandler(DateTime      Timestamp,
                                                  HTTPClient    HTTPClient,
                                                  HTTPRequest   Request,
                                                  HTTPResponse  Response);


    /// <summary>
    /// A HTTP delegate.
    /// </summary>
    /// <param name="Request">The HTTP request.</param>
    /// <returns>A HTTP response task.</returns>
    public delegate Task<HTTPResponse> HTTPDelegate(HTTPRequest Request);


    /// <summary>
    /// A HTTP delegate for HTTP authentication.
    /// </summary>
    /// <param name="Request">The HTTP request.</param>
    public delegate Boolean HTTPAuthentication(HTTPRequest Request);

}
