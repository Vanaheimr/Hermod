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
using System.Linq;
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    /// <summary>
    /// A http request header.
    /// </summary>
    public abstract class AHTTPRequestHeader : IEnumerable<String>
    {

        #region Properties

        #region Non-http header fields

        /// <summary>
        /// The unparsed http header.
        /// </summary>
        public String RAWHTTPHeader { get; protected set; }

        /// <summary>
        /// All header fields.
        /// </summary>
        public IDictionary<String, Object> HeaderFields { get; private set; }


        public HTTPStatusCode HTTPStatusCode { get; protected set; }



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

                if (_AcceptString != null)
                {

                    if (_AcceptString.Contains(","))
                    {

                        UInt32 place = 0;

                        foreach (var acc in _AcceptString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            _AcceptList.Add(new AcceptType(acc.Trim(), place++));

                    }

                    else
                        _AcceptList.Add(new AcceptType(_AcceptString.Trim()));

                    SetHeaderField("Accept", _AcceptList);

                }

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

        #region AHTTPRequestHeader()

        /// <summary>
        /// Create a new empty http request header.
        /// </summary>
        public AHTTPRequestHeader()
        {
            QueryString  = new Dictionary<String, String>();
            HeaderFields = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #endregion


        #region (protected) SetHeaderField(Key, Value)

        /// <summary>
        /// Sets a http header field.
        /// </summary>
        /// <param name="Key">The key of the requested header field.</typeparam>
        /// <param name="Value">The value of the requested header field.</typeparam>
        protected void SetHeaderField(String Key, Object Value)
        {

            if (Value != null)
            {

                if (HeaderFields.ContainsKey(Key))
                    HeaderFields[Key] = Value;
                else
                    HeaderFields.Add(Key, Value);

            }

            else
                if (HeaderFields.ContainsKey(Key))
                    HeaderFields.Remove(Key);

        }

        #endregion


        #region TryGet(Key)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <param name="Key">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        public Boolean TryGet(String Key, out Object Value)
        {
            return HeaderFields.TryGetValue(Key, out Value);
        }

        #endregion

        #region TryGet<T>(Key)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <typeparam name="T">The type of the value of the requested header field.</typeparam>
        /// <param name="Key">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        public Boolean TryGet<T>(String Key, out T Value)
        {

            Object _Object;

            if (HeaderFields.TryGetValue(Key, out _Object))
            {

                if (_Object is T)
                {
                    Value = (T) _Object;
                    return true;
                }

                else if (typeof(T).Equals(typeof(Int32)))
                {
                    Int32 _Int32;
                    if (Int32.TryParse(_Object.ToString(), out _Int32))
                    {
                        Value = (T) (Object) _Int32;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                }

                else if (typeof(T).Equals(typeof(UInt32)))
                {
                    UInt32 _UInt32;
                    if (UInt32.TryParse(_Object.ToString(), out _UInt32))
                    {
                        Value = (T) (Object) _UInt32;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                }

                else if (typeof(T).Equals(typeof(Int64)))
                {
                    Int64 _Int64;
                    if (Int64.TryParse(_Object.ToString(), out _Int64))
                    {
                        Value = (T) (Object) _Int64;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                }

                else if (typeof(T).Equals(typeof(UInt64)))
                {
                    UInt64 _UInt64;
                    if (UInt64.TryParse(_Object.ToString(), out _UInt64))
                    {
                        Value = (T) (Object) _UInt64;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                }

                else
                {
                    try
                    {
                        Value = (T) (Object) _Object;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                    catch (Exception)
                    {
                        Value = default(T);
                        return false;
                    }                    
                }

            }


            Value = default(T);
            return false;

        }

        #endregion

        #region GetNullable<T>(Key)

        /// <summary>
        /// Return a http header field wrapped within a nullable.
        /// </summary>
        /// <typeparam name="T">The underlying value type of the System.Nullable<T> generic type.</typeparam>
        /// <param name="Key">The key of the requested header field.</param>
        public Nullable<T> GetNullable<T>(String Key)
            where T : struct
        {

            T Value;

            if (TryGet<T>(Key, out Value))
                return new Nullable<T>(Value);

            else
                return new Nullable<T>();

        }

        #endregion

        #region Get<T>(myKey)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <typeparam name="T">The type of the value of the requested header field.</typeparam>
        /// <param name="Key">The key of the requested header field.</typeparam>
        public T Get<T>(String Key)
            where T : class
        {

            T _Value;
            if (TryGet<T>(Key, out _Value))
                return _Value;

            return null;

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


        public IEnumerator<String> GetEnumerator()
        {
            foreach (var field in HeaderFields)
            {

                if (field.Key == "Accept")
                    yield return field.Key + ": " + (field.Value as List<AcceptType>).Select(at => at.ToString()).Aggregate((a, b) => a + ", " + b);

                else
                    yield return field.Key + ": " + field.Value.ToString();

            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {

            foreach (var field in HeaderFields)
            {

                if (field.Key == "Accept")
                    yield return field.Key + ": " + (field.Value as List<AcceptType>).Select(at => at.ToString()).Aggregate((a, b) => a + ", " + b);

                else
                    yield return field.Key + ": " + field.Value.ToString();

            }

        }

    }

}

