/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using de.ahzf.Hermod.HTTP;

#endregion

namespace de.ahzf.Hermod.UnitTests
{

    /// <summary>
    /// A REST service serving HTML.
    /// </summary>
    public class RESTService_HTML : AHTTPService, IRESTService
    {

        #region Properties

        #region HTTPContentTypes

        /// <summary>
        /// Returns an enumeration of all associated content types.
        /// </summary>
        public IEnumerable<HTTPContentType> HTTPContentTypes
        {
            get
            {
                return new HTTPContentType[1] { HTTPContentType.HTML_UTF8 };
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region RESTService_HTML()

        /// <summary>
        /// Creates a new RESTService_HTML.
        /// </summary>
        public RESTService_HTML()
        { }

        #endregion

        #region RESTService_HTML(myIHTTPConnection)

        /// <summary>
        /// Creates a new RESTService_HTML.
        /// </summary>
        /// <param name="myIHTTPConnection">The http connection for this request.</param>
        public RESTService_HTML(IHTTPConnection myIHTTPConnection)
            : base(myIHTTPConnection, "HermodDemo.resources.")
        {
            this.CallingAssembly = Assembly.GetExecutingAssembly();
        }

        #endregion

        #endregion


        #region (private) HTML5Builder(Headline, Action)

        private String HTML5Builder(String Headline, Action<StringBuilder> Action)
        {

            var _StringBuilder = new StringBuilder();

            _StringBuilder.AppendLine("<!DOCTYPE html>");
            _StringBuilder.AppendLine("<html>");
            _StringBuilder.AppendLine("<head>");
            _StringBuilder.AppendLine("<title>Hermod HTTP Server</title>");
            _StringBuilder.AppendLine("</head>");
            _StringBuilder.AppendLine("<body>");
            _StringBuilder.Append("<h2>").Append(Headline).AppendLine("</h2>");
            _StringBuilder.AppendLine("<table>");
            _StringBuilder.AppendLine("<tr>");
            _StringBuilder.AppendLine("<td style=\"width: 100px\"> </td>");
            _StringBuilder.AppendLine("<td>");

            Action(_StringBuilder);

            _StringBuilder.AppendLine("</td>");
            _StringBuilder.AppendLine("</tr>");
            _StringBuilder.AppendLine("</table>");
            _StringBuilder.AppendLine("</body>").AppendLine("</html>").AppendLine();

            return _StringBuilder.ToString();

        }

        #endregion


        #region GetRoot()

        public HTTPResponseHeader GetRoot()
        {

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    ContentType    = HTTPContentType.HTML_UTF8,
                    Content        = HTML5Builder("Hello World!", _StringBuilder => {
                                         _StringBuilder.Append("<p><a href=\"/robots.txt\">Look at the '/robots.txt'!</a></p><br /><br />");
                                         _StringBuilder.Append("<p><a href=\"/raw\">Look at your raw http request header!</a></p><br /><br />");
                                     }).ToUTF8Bytes(),
                    CacheControl   = "no-cache"
                };

        }

        #endregion


        #region /HelloWorld

        #region HelloWorld_OPTIONS()

        public HTTPResponseHeader HelloWorld_OPTIONS()
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

        public HTTPResponseHeader HelloWorld_HEAD()
        {

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    ContentType    = HTTPContentType.TEXT_UTF8,
                    Content        = Encoding.UTF8.GetBytes("Hello world!"),
                    CacheControl   = "no-cache"
                };

        }

        #endregion

        #region HelloWorld_GET()

        public HTTPResponseHeader HelloWorld_GET()
        {

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    ContentType    = HTTPContentType.HTML_UTF8,
                    Content        = HTML5Builder("Hello world!", sb => sb.AppendLine("Hello world!")).ToUTF8Bytes(),
                    CacheControl   = "no-cache"
                };

        }

        #endregion

        #endregion

    }

}
