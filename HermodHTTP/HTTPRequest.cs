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
using System.Web;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using de.ahzf.Illias.Commons;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A read-only HTTP request header.
    /// </summary>
    public class HTTPRequest : AHTTPPDU
    {

        #region Properties

        #region Non-HTTP header fields

        public String EntireRequestHeader
        {
            get
            {
                return HTTPRequestLine + Environment.NewLine + ConstructedHTTPHeader;
            }
        }

        public String HTTPRequestLine
        {
            get
            {
                return HTTPMethod.ToString() + " " + this.UrlPath + " " + ProtocolName + "/" + ProtocolVersion;
            }
        }

        #region HTTPMethod

        /// <summary>
        /// The HTTP method.
        /// </summary>
        public HTTPMethod HTTPMethod { get; protected set; }

        #endregion

        #region Url

        /// <summary>
        /// The minimal URL (this means e.g. without the query string).
        /// </summary>
        public String UrlPath { get; protected set; }

        #endregion

        #region QueryString

        /// <summary>
        /// The HTTP query string.
        /// </summary>
        public QueryString QueryString { get; protected set; }

        #endregion

        #endregion

        #region Request header fields

        #region Accept

        protected AcceptTypes _Accept;

        /// <summary>
        /// The http content types accepted by the client.
        /// </summary>
        public AcceptTypes Accept
        {

            get
            {

                _Accept = GetHeaderField<AcceptTypes>("Accept");
                if (_Accept != null)
                    return _Accept;

                var _AcceptString = GetHeaderField<String>("Accept");

                if (!_AcceptString.IsNullOrEmpty())
                {
                    _Accept = new AcceptTypes(_AcceptString);
                    SetHeaderField("Accept", _Accept);
                    return _Accept;
                }

                else
                    return new AcceptTypes();

            }

        }

        #endregion

        #region Accept-Charset

        public String AcceptCharset
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.AcceptCharset);
            }
        }

        #endregion

        #region Accept-Encoding

        public String AcceptEncoding
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.AcceptEncoding);
            }
        }

        #endregion

        #region Accept-Language

        public String AcceptLanguage
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.AcceptLanguage);
            }
        }

        #endregion

        #region Accept-Ranges

        public String AcceptRanges
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.AcceptRanges);
            }
        }

        #endregion

        #region Authorization

        public HTTPBasicAuthentication Authorization
        {
            get
            {

                var _Authorization = GetHeaderField<HTTPBasicAuthentication>("Authorization");
                if (_Authorization != null)
                    return _Authorization;

                var _AuthString = GetHeaderField<String>("Authorization");

                if (_AuthString == null)
                    return null;

                _Authorization = new HTTPBasicAuthentication(_AuthString);

                SetHeaderField("Authorization", _Authorization);

                return _Authorization;

            }
        }

        #endregion

        #region Depth

        public String Depth
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Depth);
            }
        }

        #endregion

        #region Destination

        public String Destination
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Destination);
            }
        }

        #endregion

        #region Expect

        public String Expect
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Expect);
            }
        }

        #endregion

        #region From

        public String From
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.From);
            }
        }

        #endregion

        #region Host

        public String Host
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Host);
            }
        }

        #endregion

        #region If

        public String If
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.If);
            }
        }

        #endregion

        #region If-Match

        public String IfMatch
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfMatch);
            }
        }

        #endregion

        #region If-Modified-Since

        public String IfModifiedSince
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfModifiedSince);
            }
        }

        #endregion

        #region If-None-Match

        public String IfNoneMatch
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfNoneMatch);
            }
        }

        #endregion

        #region If-Range

        public String IfRange
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfRange);
            }
        }

        #endregion

        #region If-Unmodified-Since

        public String IfUnmodifiedSince
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfUnmodifiedSince);
            }
        }

        #endregion

        #region Lock-Token

        public String LockToken
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.LockToken);
            }
        }

        #endregion

        #region MaxForwards

        public UInt64? MaxForwards
        {
            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.MaxForwards);
            }
        }

        #endregion

        #region Overwrite

        public String Overwrite
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Overwrite);
            }
        }

        #endregion

        #region Proxy-Authorization

        public String ProxyAuthorization
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.ProxyAuthorization);
            }
        }

        #endregion

        #region Range

        public String Range
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Range);
            }
        }

        #endregion

        #region Referer

        public String Referer
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Referer);
            }
        }

        #endregion

        #region TE

        public String TE
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.TE);
            }
        }

        #endregion

        #region Timeout

        public UInt64? Timeout
        {
            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.Timeout);
            }
        }

        #endregion

        #region User-Agent

        public String UserAgent
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.UserAgent);
            }
        }

        #endregion

        #region Last-Event-Id

        public UInt64? LastEventId
        {
            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.LastEventId);
            }
        }

        #endregion

        #region Cookie

        public String Cookie
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Cookie);
            }
        }

        #endregion

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPRequest()

        /// <summary>
        /// Create a new http request header.
        /// </summary>
        public HTTPRequest()
        {
            QueryString = new QueryString();
        }

        #endregion

        #region HTTPRequest(HTTPHeader)

        /// <summary>
        /// Create a new http request header based on the given string representation.
        /// </summary>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPStatusCode">HTTPStatusCode.OK is the header could be parsed.</param>
        public HTTPRequest(String HTTPHeader)
        {

            if (!ParseHeader(HTTPHeader))
                return;

            #region Parse HTTPMethod (first line of the http request)

            var _HTTPMethodHeader = FirstPDULine.Split(_SpaceSeperator, StringSplitOptions.RemoveEmptyEntries);

            // e.g: PROPFIND /file/file Name HTTP/1.1
            if (_HTTPMethodHeader.Length != 3)
            {
                this.HTTPStatusCode = HTTPStatusCode.BadRequest;
                return;
            }

            // Parse HTTP method
            // Propably not usefull to define here, as we can not send a response having an "Allow-header" here!
            HTTPMethod _HTTPMethod = null;
            if (!HTTPMethod.TryParseString(_HTTPMethodHeader[0], out _HTTPMethod))
            {
                this.HTTPStatusCode = HTTPStatusCode.MethodNotAllowed;
                return;
            }

            this.HTTPMethod = _HTTPMethod;

            #endregion

            #region Parse URL and QueryString (first line of the http request)

            var RawUrl     = _HTTPMethodHeader[1];
            var _ParsedURL = RawUrl.Split(_URLSeperator, 2, StringSplitOptions.RemoveEmptyEntries);            
            UrlPath        = _ParsedURL[0];
            
            if (UrlPath == "" || UrlPath == null)
                UrlPath = "/";

            // Parse QueryString after '?'
            if (RawUrl.IndexOf('?') > -1)
            {
                //var a = HttpUtility.ParseQueryString(_ParsedURL[1]);
                //foreach (var b in a.AllKeys)
                //    QueryString.Add(b, a[b]);
                this.QueryString = new QueryString(_ParsedURL[1]);
            }

            #endregion

            #region Parse protocol name and -version (first line of the http request)

            var _ProtocolArray  = _HTTPMethodHeader[2].Split(_SlashSeperator, 2, StringSplitOptions.RemoveEmptyEntries);
            ProtocolName        = _ProtocolArray[0].ToUpper();

            if (ProtocolName.ToUpper() != "HTTP")
            {
                this.HTTPStatusCode = HTTPStatusCode.InternalServerError;
                return;
            }

            HTTPVersion _HTTPVersion = null;
            if (HTTPVersion.TryParseVersionString(_ProtocolArray[1], out _HTTPVersion))
                ProtocolVersion = _HTTPVersion;
            if (ProtocolVersion != HTTPVersion.HTTP_1_1)
            {
                this.HTTPStatusCode = HTTPStatusCode.HTTPVersionNotSupported;
                return;
            }

            #endregion

            if (!HeaderFields.ContainsKey("Host"))
                HeaderFields.Add("Host", "*");

            this.HTTPStatusCode = HTTPStatusCode.OK;

        }

        #endregion

        #endregion

    }

}
