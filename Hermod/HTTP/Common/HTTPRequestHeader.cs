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
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    /// <summary>
    /// A http request header.
    /// </summary>
    public class HTTPRequestHeader : AHTTPRequestHeader
    {

        #region Properties

        // http header fields

        #region Host

        /// <summary>
        /// The http host header field.
        /// </summary>
        public String Host
        {

            get
            {
                return Get<String>("Host");
            }

            set
            {
                SetHeaderField("Host", value);
            }

        }

        #endregion

        #region Accept

        /// <summary>
        /// The http content types accepted by the client.
        /// </summary>
        public IEnumerable<AcceptType> Accept
        {

            get
            {
                
                var _Accept = Get<IEnumerable<AcceptType>>("Accept");
                if (_Accept != null)
                    return _Accept;

                var _AcceptString = Get<String>("Accept");
                var _AcceptList   = new List<AcceptType>();

                if (_AcceptString.Contains(","))
                {
                    
                    UInt32 place = 0;

                    foreach (var acc in _AcceptString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        _AcceptList.Add(new AcceptType(acc.Trim(), place++));

                }

                else
                    _AcceptList.Add(new AcceptType(_AcceptString.Trim()));

                SetHeaderField("Accept", _AcceptList);

                return _AcceptList;

            }

        }

        #endregion

        #region ContentLength

        public UInt64? ContentLength
        {
            get
            {
                return GetNullable<UInt64>("Content-Length");
            }
        }

        #endregion

        #region ContentType

        public HTTPContentType ContentType
        {

            get
            {

                var _ContentType = Get<HTTPContentType>("Content-Type");
                if (_ContentType != null)
                    return _ContentType;

                _ContentType = new HTTPContentType(Get<String>("Content-Type"));

                SetHeaderField("Content-Type", _ContentType);

                return _ContentType;

            }

        }

        #endregion

        #region Authorization

        public HTTPBasicAuthentication Authorization
        {

            get
            {

                var _Authorization = Get<HTTPBasicAuthentication>("Authorization");
                if (_Authorization != null)
                    return _Authorization;

                _Authorization = new HTTPBasicAuthentication(Get<String>("Authorization"));

                SetHeaderField("Authorization", _Authorization);

                return _Authorization;

            }

        }

        #endregion

        #region LastEventId

        /// <summary>
        /// The last event id.
        /// </summary>
        public UInt64? LastEventId
        {
            
            get
            {
                return GetNullable<UInt64>("Last-Event-Id");
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

        #region KeepAlive

        public Boolean KeepAlive
        {

            get
            {

                String _Connection;
                if (TryGet<String>("Connection", out _Connection))
                    if (_Connection != null)
                        return _Connection.ToLower().Contains("keep-alive");
                
                return false;

            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPRequestHeader(HTTPHeader, out HTTPStatusCode)

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

    }

}

