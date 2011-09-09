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
using System.Text;

using de.ahzf.Hermod.HTTP.Common;
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    #region IDefaultHTTPService

    /// <summary>
    /// The interface for the default http service.
    /// </summary>
    [HTTPService(Host: "localhost:8181")]
    public interface IDefaultHTTPService : IHTTPService
    {

        /// <summary>
        /// Get the http server root.
        /// </summary>
        /// <returns>Some HTML and JavaScript.</returns>
        [HTTPMapping(HTTPMethods.GET, "/"), NoAuthentication]
        HTTPResponse GetRoot();

        /// <summary>
        /// Get the raw http request header.
        /// </summary>
        /// <returns>Some plain text.</returns>
        [HTTPMapping(HTTPMethods.GET, "/raw")]
        HTTPResponse GetRAWRequestHeader();

    }

    #endregion

    #region DefaultHTTPService

    /// <summary>
    /// The default http service.
    /// </summary>
    public class DefaultHTTPService : IDefaultHTTPService
    {

        #region Properties

        public IHTTPConnection IHTTPConnection { get; set; }

        #endregion

        #region Constructor(s)

        #region DefaultHTTPService()

        /// <summary>
        /// Creates a new DefaultHTTPService.
        /// </summary>
        public DefaultHTTPService()
        { }

        #endregion

        #region DefaultHTTPService(myIHTTPConnection)

        /// <summary>
        /// Creates a new DefaultHTTPService.
        /// </summary>
        /// <param name="myIHTTPConnection">The http connection for this request.</param>
        public DefaultHTTPService(IHTTPConnection myIHTTPConnection)
        {
            IHTTPConnection = myIHTTPConnection;
        }

        #endregion

        #endregion


        #region HTTPContentTypes

        /// <summary>
        /// The supported HTTP ContentTypes.
        /// </summary>
        public IEnumerable<HTTPContentType> HTTPContentTypes
        {
            get
            {
                return new List<HTTPContentType>() { HTTPContentType.TEXT_UTF8 };
            }
        }

        #endregion


        #region (private) HTMLBuilder(myHeadline, myFunc)

        public String HTMLBuilder(String myHeadline, Action<StringBuilder> myFunc)
        {

            var _StringBuilder = new StringBuilder();

            _StringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            _StringBuilder.AppendLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.1//EN\" \"http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd\">");
            _StringBuilder.AppendLine("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
            _StringBuilder.AppendLine("<head>");
            _StringBuilder.AppendLine("<title>Hermod default HTTPService</title>");
            _StringBuilder.AppendLine("</head>");
            _StringBuilder.AppendLine("<body>");
            _StringBuilder.Append("<h2>").Append(myHeadline).AppendLine("</h2>");
            _StringBuilder.AppendLine("<table>");
            _StringBuilder.AppendLine("<tr>");
            _StringBuilder.AppendLine("<td style=\"width: 100px\">&nbsp;</td>");
            _StringBuilder.AppendLine("<td>");

            if (myFunc != null)
                myFunc(_StringBuilder);

            _StringBuilder.AppendLine("</td>");
            _StringBuilder.AppendLine("</tr>");
            _StringBuilder.AppendLine("</table>");
            _StringBuilder.AppendLine("</body>").AppendLine("</html>").AppendLine();

            return _StringBuilder.ToString();

        }

        #endregion


        #region GetRoot()

        public HTTPResponse GetRoot()
        {

            return new HTTPResponse(

                new HTTPResponseHeader()
                {
                    HttpStatusCode = HTTPStatusCode.OK,
                    CacheControl   = "no-cache",
                    ContentType    = HTTPContentType.HTML_UTF8
                },

                HTMLBuilder("Hello world!", _StringBuilder =>
                {

                    _StringBuilder.Append("<p><a href=\"/raw\">Look at your raw http request header!</a></p><br /><br />");

                }).ToUTF8Bytes()

            );

        }

        #endregion

        #region GetRAWRequestHeader()

        public HTTPResponse GetRAWRequestHeader()
        {

            return new HTTPResponse(

                new HTTPResponseHeader()
                {
                    HttpStatusCode = HTTPStatusCode.OK,
                    CacheControl   = "no-cache",
                    Connection     = "close",
                    ContentType    = HTTPContentType.TEXT_UTF8
                },

                Encoding.UTF8.GetBytes("Incoming http connection from '" + IHTTPConnection.RemoteSocket + "'" +
                                        Environment.NewLine + Environment.NewLine +
                                        IHTTPConnection.RequestHeader.RAWHTTPHeader +
                                        Environment.NewLine + Environment.NewLine +
                                        "Method => " + IHTTPConnection.RequestHeader.HTTPMethod + Environment.NewLine +
                                        "URL => " + IHTTPConnection.RequestHeader.Url + Environment.NewLine +
                                        "QueryString => " + IHTTPConnection.RequestHeader.QueryString + Environment.NewLine +
                                        "Protocol => " + IHTTPConnection.RequestHeader.ProtocolName + Environment.NewLine +
                                        "Version => " + IHTTPConnection.RequestHeader.ProtocolVersion + Environment.NewLine +
                                        Environment.NewLine + Environment.NewLine +
                                        IHTTPConnection.ResponseHeader.HttpStatusCode
                                        )

            );

        }

        #endregion

    }

    #endregion

}