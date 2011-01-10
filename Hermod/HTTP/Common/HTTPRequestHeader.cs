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
using System.Web;
using System.Linq;
using System.Text;
using System.Net.Mime;
using System.Collections.Generic;
using System.Collections.Specialized;
using de.ahzf.Hermod;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    public class HTTPRequestHeader
    {

        #region Data

        private readonly String[] _LineSeperator;
        private readonly Char[]   _ColonSeperator;
        private readonly Char[]   _SlashSeperator;
        private readonly Char[]   _SpaceSeperator;
        private readonly Char[]   _URLSeperator;        

        #endregion

        #region Properties

        #region Non-http header fields

        /// <summary>
        /// The unparsed http header.
        /// </summary>
        public String RAWHTTPHeader { get; private set; }

        /// <summary>
        /// All header fields.
        /// </summary>
        public IDictionary<String, Object> HeaderFields { get; private set; }
        

        /// <summary>
        /// The http method.
        /// </summary>
        public HTTPMethod HTTPMethod { get; private set; }

        /// <summary>
        /// The unparsed http URL.
        /// </summary>
        public String RawUrl { get; private set; }

        /// <summary>
        /// The parsed minimal URL.
        /// </summary>
        public String Url { get; private set; }

        /// <summary>
        /// The parsed QueryString.
        /// </summary>
        public IDictionary<String, String> QueryString { get; private set; }

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
                return GetHeaderField<String>("Host");
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
                
                var _Accept = GetHeaderField<IEnumerable<AcceptType>>("Accept");
                if (_Accept != null)
                    return _Accept;

                var _AcceptString = GetHeaderField<String>("Accept");
                var _AcceptList   = new List<AcceptType>();

                if (_AcceptString.Contains(","))
                {
                    
                    UInt32 place = 0;

                    foreach (var acc in _AcceptString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        _AcceptList.Add(new AcceptType(acc.Trim(), place++));

                }

                else
                    _AcceptList.Add(new AcceptType(_AcceptString.Trim()));

                SetHeaderField("accept", _Accept);

                return _AcceptList;

            }

        }

        #endregion

        #region ContentLength

        public UInt64 ContentLength
        {

            get
            {

                UInt64 _ContentLength;
                if (!UInt64.TryParse(GetHeaderField<String>("Content-Length"), out _ContentLength))
                    return 0;

                return _ContentLength;

            }

        }

        #endregion

        #region ContentType

        public HTTPContentType ContentType
        {

            get
            {

                var _ContentType = GetHeaderField<HTTPContentType>("Content-Type");
                if (_ContentType != null)
                    return _ContentType;

                _ContentType = new HTTPContentType(GetHeaderField<String>("Content-Type"));

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

                var _Authorization = GetHeaderField<HTTPBasicAuthentication>("Authorization");
                if (_Authorization != null)
                    return _Authorization;

                _Authorization = new HTTPBasicAuthentication(GetHeaderField<String>("Authorization"));

                SetHeaderField("Authorization", _Authorization);

                return _Authorization;

            }

        }

        #endregion


        // Additional header infos

        #region KeepAlive

        public Boolean KeepAlive
        {

            get
            {
                
                var _Connection = GetHeaderField<String>("Connection");
                if (_Connection == null)
                    return false;

                return _Connection.ToLower().Contains("keep-alive");

            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPRequestHeader()

        public HTTPRequestHeader()
        {

            QueryString     = new Dictionary<String, String>();
            HeaderFields    = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

            _LineSeperator  = new String[] { Environment.NewLine };
            _ColonSeperator = new Char[]   { ':' };
            _SlashSeperator = new Char[]   { '/' };
            _SpaceSeperator = new Char[]   { ' ' };
            _URLSeperator   = new Char[]   { '?', '!' };

        }

        #endregion

        #region HTTPRequestHeader(myHTTPHeader, out myHTTPStatusCode)

        public HTTPRequestHeader(String myHTTPHeader, out HTTPStatusCode myHTTPStatusCode)
            : this()
        {

            #region Split the request into lines

            RAWHTTPHeader = myHTTPHeader;

            var _HTTPRequestLines = myHTTPHeader.Split(_LineSeperator, StringSplitOptions.RemoveEmptyEntries);
            if (_HTTPRequestLines.Length == 0)
            {
                myHTTPStatusCode = HTTPStatusCode.BadRequest;
                return;
            }

            #endregion

            #region Parse HTTPMethod (first line of the http request)

            var _HTTPMethodHeader = _HTTPRequestLines[0].Split(_SpaceSeperator, StringSplitOptions.RemoveEmptyEntries);

            // e.g: PROPFIND /file/file Name HTTP/1.1
            if (_HTTPMethodHeader.Length != 3)
            {
                myHTTPStatusCode = HTTPStatusCode.BadRequest;
                return;
            }

            // Parse HTTP method
            // Propably not usefull to define here, as we can not send a response having an "Allow-header" here!
            HTTPMethod _HTTPMethod = null;
            if (!HTTPMethod.TryParseString(_HTTPMethodHeader[0], out _HTTPMethod))
            {
                myHTTPStatusCode = HTTPStatusCode.MethodNotAllowed;
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

            foreach (var _Line in _HTTPRequestLines.Skip(1))
            {

                var _KeyValuePairs = _Line.Split(_ColonSeperator, 2, StringSplitOptions.RemoveEmptyEntries);

                if (_KeyValuePairs.Length == 2)
                    HeaderFields.Add(_KeyValuePairs[0].Trim(), _KeyValuePairs[1].Trim());

            }

            //_AcceptTypes.Sort();
            
            //AcceptTypes.Sort(new Comparison<AcceptType>((at1, at2) =>
            //{
            //    if (at1.Quality > at2.Quality) return 1;
            //    else if (at1.Quality == at2.Quality) return 0;
            //    else return -1;
            //    //if (at2.Quality > at1.Quality) return -1;
            //    //else return 1;
            //    //return at1.Quality.CompareTo(at2.Quality);
            //}
            //    ));

            #endregion

            if (!HeaderFields.ContainsKey("Host"))
                HeaderFields.Add("Host", "*");

            myHTTPStatusCode = HTTPStatusCode.OK;

        }

        #endregion

        #endregion


        #region GetHeaderField<T>(myKey)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <typeparam name="T">The type of the requested header field.</typeparam>
        /// <param name="myKey">The key of the requested header field.</typeparam>
        /// <returns></returns>
        public T GetHeaderField<T>(String myKey)
        {

            Object _Value = null;
            if (HeaderFields.TryGetValue(myKey, out _Value))
                if (_Value is T)
                    return (T) _Value;

            return default(T);

        }

        #endregion

        #region SetHeaderField(myKey, myValue)

        /// <summary>
        /// Sets a http header field.
        /// </summary>
        /// <param name="myKey">The key of the requested header field.</typeparam>
        /// <param name="myValue">The value of the requested header field.</typeparam>
        private void SetHeaderField(String myKey, Object myValue)
        {

            if (myValue != null)
            {

                if (HeaderFields.ContainsKey(myKey))
                    HeaderFields[myKey] = myValue;
                else
                    HeaderFields.Add(myKey, myValue);

            }

            else
                if (HeaderFields.ContainsKey(myKey))
                    HeaderFields.Remove(myKey);

        }

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

                var _AcceptType = new AcceptType(_ContentType.ToString(), pos++);

                var _Match = GetHeaderField<List<AcceptType>>("accept").Find(_AType => _AType.Equals(_AcceptType));

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
