/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Text;
using System.Collections.Generic;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Hermod.HTTP;

#endregion

namespace eu.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// A HTTP service serving text.
    /// </summary>
    public class HTTPTestService_TEXT : AHTTPTestService
    {

        #region Constructor(s)

        #region HTTPTestService_TEXT()

        /// <summary>
        /// TEXT content representation.
        /// </summary>
        public HTTPTestService_TEXT()
            : base(HTTPContentType.TEXT_UTF8)
        { }

        #endregion

        #region HTTPTestService_TEXT(IHTTPConnection)

        /// <summary>
        /// TEXT content representation.
        /// </summary>
        /// <param name="IHTTPConnection">The http connection for this request.</param>
        public HTTPTestService_TEXT(IHTTPConnection IHTTPConnection)
            : base(IHTTPConnection, HTTPContentType.TEXT_UTF8)
        { }

        #endregion

        #endregion


        #region GetRoot()

        public override HTTPResponse GET_Root()
        {

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    ContentType    = HTTPContentType.HTML_UTF8,
                    Content        = "Hello World!".ToUTF8Bytes(),
                    CacheControl   = "no-cache"
                };

        }

        #endregion


        #region /HelloWorld

        #region HelloWorld_OPTIONS()

        public override HTTPResponse HelloWorld_OPTIONS()
        {

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    Allow          = new List<HTTPMethod> {
                                             HTTPMethod.OPTIONS,
                                             HTTPMethod.HEAD,
                                             HTTPMethod.GET
                                         },
                    CacheControl   = "no-cache"
                };

        }

        #endregion

        #region HelloWorld_HEAD()

        public override HTTPResponse HelloWorld_HEAD()
        {

            var _RequestHeader = IHTTPConnection.RequestHeader;

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    ContentType    = HTTPContentType.TEXT_UTF8,
                    Content        = "Hello world!".ToUTF8Bytes(),
                    CacheControl   = "no-cache"
                };

        }

        #endregion

        #region HelloWorld_GET()

        public override HTTPResponse HelloWorld_GET()
        {

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    ContentType    = HTTPContentType.TEXT_UTF8,
                    Content        = "Hello world!".ToUTF8Bytes(),
                    CacheControl   = "no-cache"
                };

        }

        #endregion

        #endregion

    }

}
