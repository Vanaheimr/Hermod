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
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    /// <summary>
    /// A http request header.
    /// </summary>
    public class HTTPRequestHeader_RW : AHTTPRequestHeader
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


        #region SetHeaderField(Key, Value)

        /// <summary>
        /// Sets a http header field.
        /// </summary>
        /// <param name="Key">The key of the requested header field.</typeparam>
        /// <param name="Value">The value of the requested header field.</typeparam>
        public new void SetHeaderField(String Key, Object Value)
        {
            base.SetHeaderField(Key, Value);
        }

        #endregion

    }

}

