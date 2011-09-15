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
using System.Web;
using System.Linq;
using System.Collections.Generic;
using System.Text;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A read-only HTTP request header.
    /// </summary>
    public class HTTPRequestHeader : AHTTPHeader
    {

        #region Properties

        #region Non-HTTP header fields

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
        public String Url { get; protected set; }

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

        protected List<AcceptType> _Accept;

        /// <summary>
        /// The http content types accepted by the client.
        /// </summary>
        public IEnumerable<AcceptType> Accept
        {

            get
            {

                _Accept = GetHeaderField<List<AcceptType>>("Accept");
                if (_Accept != null)
                    return _Accept;

                var _AcceptString = GetHeaderField<String>("Accept");
                    _Accept       = new List<AcceptType>();

                if (!_AcceptString.IsNullOrEmpty())
                {

                    if (_AcceptString.Contains(","))
                    {

                        UInt32 place = 0;

                        foreach (var acc in _AcceptString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            _Accept.Add(new AcceptType(acc.Trim(), place++));

                    }

                    else
                        _Accept.Add(new AcceptType(_AcceptString.Trim()));

                    SetHeaderField("Accept", _Accept);
                    return _Accept;

                }
                else
                    return new List<AcceptType>();

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

                _Authorization = new HTTPBasicAuthentication(GetHeaderField<String>("Authorization"));

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

        #region HTTPRequestHeader()

        /// <summary>
        /// Create a new http request header.
        /// </summary>
        public HTTPRequestHeader()
        {
            QueryString = new QueryString();
        }

        #endregion

        #region HTTPRequestHeader(HTTPHeader)

        /// <summary>
        /// Create a new http request header based on the given string representation.
        /// </summary>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPStatusCode">HTTPStatusCode.OK is the header could be parsed.</param>
        public HTTPRequestHeader(String HTTPHeader)
            : base()
        {

            #region Split the request into lines

            var _HTTPRequestLines = HTTPHeader.Split(_LineSeperator, StringSplitOptions.RemoveEmptyEntries);
            if (_HTTPRequestLines.Length == 0)
            {
                HTTPStatusCode = HTTPStatusCode.BadRequest;
                return;
            }

            #endregion

            #region Parse HTTPMethod (first line of the http request)

            var _HTTPMethodHeader = _HTTPRequestLines[0].Split(_SpaceSeperator, StringSplitOptions.RemoveEmptyEntries);

            // e.g: PROPFIND /file/file Name HTTP/1.1
            if (_HTTPMethodHeader.Length != 3)
            {
                HTTPStatusCode = HTTPStatusCode.BadRequest;
                return;
            }

            // Parse HTTP method
            // Propably not usefull to define here, as we can not send a response having an "Allow-header" here!
            HTTPMethod _HTTPMethod = null;
            if (!HTTPMethod.TryParseString(_HTTPMethodHeader[0], out _HTTPMethod))
            {
                HTTPStatusCode = HTTPStatusCode.MethodNotAllowed;
                return;
            }

            this.HTTPMethod = _HTTPMethod;

            #endregion

            #region Parse URL and QueryString (first line of the http request)

            var RawUrl     = _HTTPMethodHeader[1];
            var _ParsedURL = RawUrl.Split(_URLSeperator, 2, StringSplitOptions.RemoveEmptyEntries);            
            Url            = _ParsedURL[0];
            
            if (Url == "" || Url == null)
                Url = "/";

            // Parse QueryString after '?'
            if (RawUrl.IndexOf('?') > -1)
            {
                //var a = HttpUtility.ParseQueryString(_ParsedURL[1]);
                //foreach (var b in a.AllKeys)
                //    QueryString.Add(b, a[b]);
                QueryString = new QueryString(_ParsedURL[1]);
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

            #region Parse remaining header lines

            ParseHeader(_HTTPRequestLines.Skip(1));

            #endregion

            if (!HeaderFields.ContainsKey("Host"))
                HeaderFields.Add("Host", "*");

            this.HTTPStatusCode = HTTPStatusCode.OK;

        }

        #endregion

        #endregion


        #region GetBestMatchingAcceptHeader

        /// <summary>
        /// Will return the best matching content type OR the first given!
        /// </summary>
        /// <param name="myContentTypes"></param>
        /// <returns></returns>        
        public HTTPContentType GetBestMatchingAcceptHeader(params HTTPContentType[] myContentTypes)
        {

            UInt32 pos = 0;
            var _ListOfFoundAcceptHeaders = new List<AcceptType>();

            foreach (var _ContentType in myContentTypes)
            {

                var _AcceptType = new AcceptType(_ContentType.MediaType, pos++);
                    
                var _Match = Accept.ToList().Find(_AType => _AType.Equals(_AcceptType));

                if (_Match != null)
                {

                    if (_Match.ContentType.GetMediaSubType() == "*") // this was a * and we will set the quality to lowest
                        _AcceptType.Quality = 0;

                    _ListOfFoundAcceptHeaders.Add(_AcceptType);

                }

            }

            _ListOfFoundAcceptHeaders.Sort();

            if (!_ListOfFoundAcceptHeaders.IsNullOrEmpty())
                return _ListOfFoundAcceptHeaders.First().ContentType;
            else if (!myContentTypes.IsNullOrEmpty())
                return myContentTypes.First();
            else
                return null;

        }

        #endregion

    }

}
