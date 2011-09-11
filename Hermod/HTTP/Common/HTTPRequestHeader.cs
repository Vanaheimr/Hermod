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

namespace de.ahzf.Hermod.HTTP.Common
{

    #region HTTPRequestHeader

    /// <summary>
    /// A read-only HTTP request header.
    /// </summary>
    public class HTTPRequestHeader : AHTTPHeader
    {

        #region Properties

        #region Non-HTTP header fields

        /// <summary>
        /// The http method.
        /// </summary>
        public HTTPMethod HTTPMethod { get; protected set; }

        /// <summary>
        /// The unparsed http URL.
        /// </summary>
        public String RawUrl { get; protected set; }

        /// <summary>
        /// The parsed minimal URL.
        /// </summary>
        public String Url { get; protected set; }

        /// <summary>
        /// The parsed QueryString.
        /// </summary>
        public IDictionary<String, String> QueryString { get; protected set; }

        /// <summary>
        /// Optional SVNParameters.
        /// </summary>
        public String SVNParameters { get; protected set; }

        /// <summary>
        /// The http protocol field.
        /// </summary>
        public String Protocol { get; protected set; }

        /// <summary>
        /// The http protocol name field.
        /// </summary>
        public String ProtocolName { get; protected set; }

        /// <summary>
        /// The http protocol version.
        /// </summary>
        public Version ProtocolVersion { get; protected set; }

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

        public String MaxForwards
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.MaxForwards);
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

        public String Timeout
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Timeout);
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

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPRequestHeader()

        /// <summary>
        /// Create a new http request header.
        /// </summary>
        public HTTPRequestHeader()
        { }

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

            HTTPMethod = _HTTPMethod;

            #endregion

            #region Parse URL and QueryString (first line of the http request)

            RawUrl         = _HTTPMethodHeader[1];
            var _ParsedURL = RawUrl.Split(_URLSeperator, 2, StringSplitOptions.RemoveEmptyEntries);            
            Url            = _ParsedURL[0];
            
            if (Url == "" || Url == null)
                Url = "/";

            // Parse QueryString after '?'
            if (RawUrl.IndexOf('?') > -1)
            {
                var a = HttpUtility.ParseQueryString(_ParsedURL[1]);
                foreach (var b in a.AllKeys)
                    QueryString.Add(b, a[b]);
            }

            // Parse SVNParameters after '!'
            if (RawUrl.IndexOf('!') > -1)
                SVNParameters   = _ParsedURL[1];

            #endregion

            #region Parse protocol name and -version (first line of the http request)

            var _ProtocolArray  = _HTTPMethodHeader[2].Split(_SlashSeperator, 2, StringSplitOptions.RemoveEmptyEntries);
            ProtocolName        = _ProtocolArray[0].ToUpper();
            ProtocolVersion     = new Version(_ProtocolArray[1]);

            #endregion

            #region Parse all other Header information

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

    #endregion

    #region HTTPRequestHeader_RW

    /// <summary>
    /// A read-write HTTP request header.
    /// </summary>
    public class HTTPRequestHeader_RW : HTTPRequestHeader
    {

        #region Properties

        #region Non-http header fields

        /// <summary>
        /// The http method.
        /// </summary>
        public new HTTPMethod HTTPMethod
        {

            get
            {
                return base.HTTPMethod;
            }

            set
            {
                base.HTTPMethod = value;
            }

        }

        /// <summary>
        /// The parsed minimal URL.
        /// </summary>
        public String Url { get; private set; }

        /// <summary>
        /// Optional SVNParameters.
        /// </summary>
        public String SVNParameters { get; set; }

        /// <summary>
        /// The http protocol field.
        /// </summary>
        public String Protocol { get; set; }

        /// <summary>
        /// The http protocol name field.
        /// </summary>
        public String ProtocolName { get; set; }

        /// <summary>
        /// The http protocol version.
        /// </summary>
        public Version ProtocolVersion { get; set; }

        #endregion

        #region General header fields

        #region CacheControl

        public new String CacheControl
        {

            get
            {
                return base.CacheControl;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.CacheControl, value);
            }

        }

        #endregion

        #region Connection

        public new String Connection
        {

            get
            {
                return base.Connection;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Connection, value);
            }

        }

        #endregion

        #region ContentEncoding

        public new Encoding ContentEncoding
        {

            get
            {
                return base.ContentEncoding;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentEncoding, value);
            }

        }

        #endregion

        #region ContentLanguage

        public new List<String> ContentLanguage
        {

            get
            {
                return base.ContentLanguage;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLanguage, value);
            }

        }

        #endregion

        #region ContentLength

        public new UInt64? ContentLength
        {

            get
            {
                return base.ContentLength;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLength, value);
            }

        }

        #endregion

        #region ContentLocation

        public new String ContentLocation
        {

            get
            {
                return base.ContentLocation;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLocation, value);
            }

        }

        #endregion

        #region ContentMD5

        public new String ContentMD5
        {

            get
            {
                return base.ContentMD5;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentMD5, value);
            }

        }

        #endregion

        #region ContentRange

        public new String ContentRange
        {

            get
            {
                return base.ContentRange;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentRange, value);
            }

        }

        #endregion

        #region ContentType

        public new HTTPContentType ContentType
        {

            get
            {
                return base.ContentType;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentType, value);
            }

        }

        #endregion

        #region Date

        public new String Date
        {

            get
            {
                return base.Date;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Date, value);
            }

        }

        #endregion

        #region Via

        public new String Via
        {

            get
            {
                return base.Via;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Via, value);
            }

        }

        #endregion

        #endregion

        #region Request header fields

        #region Accept

        /// <summary>
        /// The http content types accepted by the client.
        /// </summary>
        public new List<AcceptType> Accept
        {

            get
            {
                return base._Accept;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Accept, value);
                base._Accept = value;
            }

        }

        #endregion


        #region Host

        public new String Host
        {

            get
            {
                return base.Host;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Host, value);
            }

        }

        #endregion


        #region LastEventId

        public new UInt64? LastEventId
        {
            
            get
            {
                return base.LastEventId;
            }

            set
            {
                if (value != null && value.HasValue)
                    SetHeaderField("Last-Event-Id", value.Value);
                else
                    throw new Exception("Could not set the HTTP request header 'Last-Event-Id' field!");
            }

        }

        #endregion

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPRequestHeader_RW()

        /// <summary>
        /// Create a new http request header.
        /// </summary>
        public HTTPRequestHeader_RW()
            : base()
        {
            this.RawUrl          = "/";
            this.ProtocolName    = "HTTP";
            this.ProtocolVersion = new Version(1, 1);
        }

        #endregion

        #endregion

        #region Set General header fields

        #region SetCacheControl(CacheControl)

        /// <summary>
        /// Set the HTTP CacheControl.
        /// </summary>
        /// <param name="CacheControl">CacheControl.</param>
        public HTTPRequestHeader_RW SetCacheControl(String CacheControl)
        {
            this.CacheControl = CacheControl;
            return this;
        }

        #endregion

        #region SetConnection(Connection)

        /// <summary>
        /// Set the HTTP connection.
        /// </summary>
        /// <param name="Connection">A connection.</param>
        public HTTPRequestHeader_RW SetConnection(String Connection)
        {
            this.Connection = Connection;
            return this;
        }

        #endregion

        #region SetContentEncoding(ContentEncoding)

        /// <summary>
        /// Set the HTTP Content-Encoding.
        /// </summary>
        /// <param name="ContentEncoding">The encoding of the HTTP content/body.</param>
        public HTTPRequestHeader_RW SetContentEncoding(Encoding ContentEncoding)
        {
            this.ContentEncoding = ContentEncoding;
            return this;
        }

        #endregion

        #region SetContentLanguage(ContentLanguages)

        /// <summary>
        /// Set the HTTP Content-Languages.
        /// </summary>
        /// <param name="ContentLanguages">The languages of the HTTP content/body.</param>
        public HTTPRequestHeader_RW SetContentLanguage(List<String> ContentLanguages)
        {
            this.ContentLanguage = ContentLanguages;
            return this;
        }

        #endregion

        #region SetContentLength(ContentLength)

        /// <summary>
        /// Set the HTTP Content-Length.
        /// </summary>
        /// <param name="ContentLength">The length of the HTTP content/body.</param>
        public HTTPRequestHeader_RW SetContentLength(UInt64? ContentLength)
        {
            this.ContentLength = ContentLength;
            return this;
        }

        #endregion

        #region SetContentLocation(ContentLocation)

        /// <summary>
        /// Set the HTTP ContentLocation.
        /// </summary>
        /// <param name="ContentLocation">ContentLocation.</param>
        public HTTPRequestHeader_RW SetContentLocation(String ContentLocation)
        {
            this.ContentLocation = ContentLocation;
            return this;
        }

        #endregion

        #region SetContentMD5(ContentMD5)

        /// <summary>
        /// Set the HTTP ContentMD5.
        /// </summary>
        /// <param name="ContentMD5">ContentMD5.</param>
        public HTTPRequestHeader_RW SetContentMD5(String ContentMD5)
        {
            this.ContentMD5 = ContentMD5;
            return this;
        }

        #endregion

        #region SetContentRange(ContentRange)

        /// <summary>
        /// Set the HTTP ContentRange.
        /// </summary>
        /// <param name="ContentRange">ContentRange.</param>
        public HTTPRequestHeader_RW SetContentRange(String ContentRange)
        {
            this.ContentRange = ContentRange;
            return this;
        }

        #endregion

        #region SetContentType(ContentType)

        /// <summary>
        /// Set the HTTP Content-Type.
        /// </summary>
        /// <param name="ContentType">The type of the HTTP content/body.</param>
        public HTTPRequestHeader_RW SetContentType(HTTPContentType ContentType)
        {
            this.ContentType = ContentType;
            return this;
        }

        #endregion

        #region SetDate(DateTime)

        /// <summary>
        /// Set the HTTP Date.
        /// </summary>
        /// <param name="DateTime">DateTime.</param>
        public HTTPRequestHeader_RW SetVia(DateTime DateTime)
        {
            this.Date = DateTime.ToString();
            return this;
        }

        #endregion

        #region SetVia(Via)

        /// <summary>
        /// Set the HTTP Via.
        /// </summary>
        /// <param name="Via">Via.</param>
        public HTTPRequestHeader_RW SetVia(String Via)
        {
            this.Via = Via;
            return this;
        }

        #endregion

        #endregion

    }

    #endregion

}
