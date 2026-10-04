/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.UnitTests
{
    class HTTPServerMultiTenancyTests
    {


        //            // HTTP methods...
        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2000",    // Check rfc!
        ////                                           "/test",
        //                                           HTTPDelegate: Request => {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder() {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content         = String.Concat("Hello world on port 2000 /!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = ConnectionType.Close
        //                                               };

        //                                           });

        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2002",    // Check rfc!
        ////                                           "/test",
        //                                           HTTPDelegate: Request => {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder() {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content         = String.Concat("Hello world on port 2002 /!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = ConnectionType.Close
        //                                               };

        //                                           });

        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2000",    // Check rfc!
        //                                           "/tests/{2000}",
        //                                           HTTPDelegate: Request =>
        //                                           {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder()
        //                                               {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content = String.Concat("Hello world on port 2000 /tests/{id} with id == " + Request.ParsedQueryParameters.FirstOrDefault() + "!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = ConnectionType.Close
        //                                               };

        //                                           });

        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2000",    // Check rfc!
        //                                           "/tests/{test}/ids/{id}",
        //                                           HTTPDelegate: Request =>
        //                                           {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder()
        //                                               {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content = String.Concat("Hello world on port 2000 /tests/{test}/ids/{id} with id == " + Request.ParsedQueryParameters.FirstOrDefault() + "!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = ConnectionType.Close
        //                                               };

        //                                           });

        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2002",    // Check rfc!
        //                                           "/test2002",
        //                                           HTTPDelegate: Request =>
        //                                           {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder()
        //                                               {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content         = String.Concat("Hello world on port 2002 /test2!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = ConnectionType.Close
        //                                               };

        //                                           });

        //_HTTPServer.OnNotification      += (ConnectionId, ServerTimestamp, Request) => {

        //    // Cache-control:max-age=300
        //    // Content-Encoding:gzip
        //    // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //    // Vary:User-Agent,Accept-Encoding

        //    return new HTTPResponseBuilder() {
        //        HTTPStatusCode  = HTTPStatusCode.OK,
        //        ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //        Content         = String.Concat(Request.Host, Environment.NewLine, "Hello world any port!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //        Server          = _HTTPServer.DefaultServerName,
        //        Connection      = ConnectionType.Close
        //    };

        //};

    }
}
