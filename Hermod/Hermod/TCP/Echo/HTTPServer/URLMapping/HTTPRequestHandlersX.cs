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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    public class HTTPRequestHandlersX(HTTPAPIX                                   HTTPAPI,
                                      HTTPDelegate?                              RequestHandler,
                                      OnHTTPRequestLogDelegate2?                 HTTPRequestLogger,
                                      OnHTTPResponseLogDelegate2?                HTTPResponseLogger,
                                      HTTPDelegate?                              DefaultErrorHandler,
                                      Dictionary<HTTPStatusCode, HTTPDelegate>?  ErrorHandlers)
    {

        #region Properties

        public HTTPAPIX                                   HTTPAPI                { get; } = HTTPAPI;
        public HTTPDelegate?                              RequestHandler         { get; } = RequestHandler;
        public OnHTTPRequestLogDelegate2?                 HTTPRequestLogger      { get; } = HTTPRequestLogger;
        public OnHTTPResponseLogDelegate2?                HTTPResponseLogger     { get; } = HTTPResponseLogger;
        public HTTPDelegate?                              DefaultErrorHandler    { get; } = DefaultErrorHandler;
        public Dictionary<HTTPStatusCode, HTTPDelegate>?  ErrorHandlers          { get; } = ErrorHandlers;

        #endregion

        public static HTTPRequestHandlersX FromURLNode(URL_NodeX URLNode)

            => new (URLNode.HTTPAPI,
                    URLNode.RequestHandler,
                    URLNode.HTTPRequestLogger,
                    URLNode.HTTPResponseLogger,
                    URLNode.DefaultErrorHandler,
                    URLNode.ErrorHandlers);

        public static HTTPRequestHandlersX FromMethodNode(HTTPMethodNodeX MethodNode)

            => new (MethodNode.HTTPAPI,
                    MethodNode.RequestHandler,
                    MethodNode.HTTPRequestLogger,
                    MethodNode.HTTPResponseLogger,
                    MethodNode.DefaultErrorHandler,
                    MethodNode.ErrorHandlers);

        public static HTTPRequestHandlersX FromContentTypeNode(ContentTypeNodeX ContentTypeNode)

            => new (ContentTypeNode.HTTPAPI,
                    ContentTypeNode.RequestHandler,
                    ContentTypeNode.HTTPRequestLogger,
                    ContentTypeNode.HTTPResponseLogger,
                    ContentTypeNode.DefaultErrorHandler,
                    ContentTypeNode.ErrorHandlers);

    }

}
