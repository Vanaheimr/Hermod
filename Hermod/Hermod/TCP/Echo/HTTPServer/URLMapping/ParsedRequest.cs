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
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using static org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPServer;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{
    public class ParsedRequest
    {

        public HTTPRequestHandlersX?       RequestHandlers    { get; }
        public Dictionary<String, String>  Parameters         { get; }
        public String?                     ErrorResponse      { get; }

        private ParsedRequest(HTTPRequestHandlersX?       RequestHandlers,
                              Dictionary<String, String>  Parameters,
                              String?                     ErrorResponse   = null)
        {

            this.RequestHandlers  = RequestHandlers;
            this.Parameters       = Parameters;
            this.ErrorResponse    = ErrorResponse;

        }

        public static ParsedRequest Parsed(HTTPRequestHandlersX?       RequestHandler,
                                           Dictionary<String, String>  Parameters)

            => new (
                   RequestHandler,
                   Parameters
               );


        public static ParsedRequest Error(String ErrorResponse)

            => new (
                   null,
                   [],
                   ErrorResponse
               );


    }

}
