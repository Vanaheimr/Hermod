/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An abstract HTTP protocol data unit builder.
    /// A HTTP pdu has three parts:
    ///  - First a request/response specific first line
    ///  - A collection of key-value pairs of type &lt;string,object&gt;
    ///    for any kind of metadata
    ///  - A body hosting the transmitted content
    /// </summary>
    public abstract class AHTTPPDUBuilder : IEnumerable<KeyValuePair<String, Object>>, INotifyPropertyChanged
    {

        #region Data

        /// <summary>
        /// The collection of all HTTP headers.
        /// </summary>
        protected readonly IDictionary<String, Object> HeaderFields;

        #endregion

        #region Properties

        #region Non-HTTP header fields

        #region EventTrackingId

        private EventTracking_Id _EventTrackingId;

        /// <summary>
        /// A unique identification for tracking related events.
        /// </summary>
        public EventTracking_Id EventTrackingId
        {

            get
            {
                return _EventTrackingId;
            }

            set
            {
                _EventTrackingId = value;
            }

        }

        #endregion

        #region ConstructedHTTPHeader

        /// <summary>
        /// Return a string representation of this HTTPHeader.
        /// </summary>
        public String ConstructedHTTPHeader
        {
            get
            {

                return (from   _KeyValuePair in HeaderFields
                        where  _KeyValuePair.Key   != null
                        where  _KeyValuePair.Value != null
                        where  !String.IsNullOrEmpty(_KeyValuePair.Value.ToString())
                        select _KeyValuePair.Key + ": " + _KeyValuePair.Value).
                        AggregateOrDefault((a, b) => a + Environment.NewLine + b, String.Empty);

            }
        }

        #endregion

        #region HTTPStatusCode

        /// <summary>
        /// The HTTP status code.
        /// </summary>
        protected HTTPStatusCode _HTTPStatusCode;

        /// <summary>
        /// The HTTP status code.
        /// </summary>
        public HTTPStatusCode HTTPStatusCode
        {

            get
            {
                return _HTTPStatusCode;
            }

            set
            {
                SetProperty(ref _HTTPStatusCode, value, "HTTPStatusCode");
            }

        }

        #endregion

        #region ProtocolName

        private String _ProtocolName;

        /// <summary>
        /// The HTTP protocol name field.
        /// </summary>
        public String ProtocolName
        {

            get
            {
                return _ProtocolName;
            }

            set
            {
                SetProperty(ref _ProtocolName, value, "ProtocolName");
            }

        }

        #endregion

        #region ProtocolVersion

        private HTTPVersion _ProtocolVersion;

        /// <summary>
        /// The HTTP protocol version.
        /// </summary>
        public HTTPVersion ProtocolVersion
        {

            get
            {
                return _ProtocolVersion;
            }

            set
            {
                SetProperty(ref _ProtocolVersion, value, "ProtocolVersion");
            }

        }

        #endregion

        #region Content

        private Byte[] _Content;

        /// <summary>
        /// The HTTP body/content as an array of bytes.
        /// </summary>
        public Byte[] Content
        {

            get
            {
                return _Content;
            }

            set
            {
                SetProperty(ref _Content, value, "Content");
                ContentLength = _Content != null ? (UInt64) _Content.LongLength : 0;
            }

        }

        #endregion

        #region ContentStream

        private Stream _ContentStream;

        /// <summary>
        /// The HTTP body/content as a stream.
        /// </summary>
        public Stream ContentStream
        {

            get
            {
                return _ContentStream;
            }

            set
            {
                SetProperty(ref _ContentStream, value, "ContentStream");
                // Setting the Content-Length might break lazyness!
                //ContentLength = (UInt64) _ContentStream.Length;
            }

        }

        #endregion

        #endregion

        #region General header fields

        #region Cache-Control

        public String CacheControl
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.CacheControl);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.CacheControl, value);
            }

        }

        #endregion

        #region Connection

        public String Connection
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Connection);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Connection, value);
            }

        }

        #endregion

        #region Content-Encoding

        public Encoding ContentEncoding
        {

            get
            {
                return GetHeaderField<Encoding>("Content-Encoding");
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentEncoding, value);
            }

        }

        #endregion

        #region Content-Language

        public List<String> ContentLanguage
        {

            get
            {
                return GetHeaderField<List<String>>(HTTPHeaderField.ContentLanguage);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLanguage, value);
            }

        }

        #endregion

        #region Content-Length

        public UInt64? ContentLength
        {

            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.ContentLength);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLength, value);
            }

        }

        #endregion

        #region Content-Location

        public String ContentLocation
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.ContentLocation);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLocation, value);
            }

        }

        #endregion

        #region Content-MD5

        public String ContentMD5
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.ContentMD5);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentMD5, value);
            }

        }

        #endregion

        #region Content-Range

        public String ContentRange
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.ContentRange);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentRange, value);
            }

        }

        #endregion

        #region Content-Type

        public HTTPContentType ContentType
        {

            get
            {

                var _ContentType = GetHeaderField<HTTPContentType>("Content-Type");
                if (_ContentType != null)
                    return _ContentType;

                var _ContentTypeString = GetHeaderField<String>("Content-Type");
                if (_ContentTypeString != null)
                {

                    var __ContentTypeString = _ContentTypeString.Split('/');

                    if (__ContentTypeString.Length == 2)
                    {

                        _ContentType = new HTTPContentType(__ContentTypeString[0],
                                                           __ContentTypeString[1],
                                                           "utf-8",
                                                           null,
                                                           null);

                        SetHeaderField("Content-Type", _ContentType);

                        return _ContentType;

                    }

                }

                return null;

            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentType, value);
            }

        }

        #endregion

        #region Date

        /// <summary>
        /// The date and time of the request/response in rfc1123 format.
        /// </summary>
        public DateTime Date
        {

            get
            {
                return DateTime.Parse(GetHeaderField(HTTPHeaderField.Date));
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Date, value.ToUniversalTime().ToString("r"));
            }

        }

        #endregion

        #region Via

        public String Via
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Via);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Via, value);
            }

        }

        #endregion

        #region Transfer-Encoding

        public String TransferEncoding
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.TransferEncoding);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.TransferEncoding, value);
            }

        }

        #endregion

        #endregion

        #region CORS

        #region Access-Control-Allow-Origin

        public String AccessControlAllowOrigin
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.AccessControlAllowOrigin);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.AccessControlAllowOrigin, value);
            }

        }

        #endregion

        #region Access-Control-Allow-Methods

        public String AccessControlAllowMethods
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.AccessControlAllowMethods);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.AccessControlAllowMethods, value);
            }

        }

        #endregion

        #region Access-Control-Allow-Headers

        public String AccessControlAllowHeaders
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.AccessControlAllowHeaders);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.AccessControlAllowHeaders, value);
            }

        }

        #endregion

        #region Access-Control-Max-Age

        public UInt64? AccessControlMaxAge
        {

            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.AccessControlMaxAge);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.AccessControlMaxAge, value);
            }

        }

        #endregion

        #endregion

        #region X_ExpectedTotalNumberOfItems

        public UInt64? X_ExpectedTotalNumberOfItems
        {

            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.X_ExpectedTotalNumberOfItems);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.X_ExpectedTotalNumberOfItems, value);
            }

        }

        #endregion

        #region X_FrameOptions

        /// <summary>
        /// The X-Frame-Options HTTP response header can be used to indicate whether or not a browser
        /// should be allowed to render a page in a &lt;frame&gt;, &lt;iframe&gt; or &lt;object&gt;.
        /// Sites can use this to avoid clickjacking attacks, by ensuring that their content is not
        /// embedded into other sites.
        /// </summary>
        /// <example>DENY, SAMEORIGIN, ALLOW-FROM https://example.com</example>
        public String X_FrameOptions
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.X_FrameOptions);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.X_FrameOptions, value);
            }

        }

        #endregion

        #endregion

        #region Events

        #region PropertyChanged

        /// <summary>
        /// Raise an event whenever a property is changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #endregion

        #region Constructor(s)

        #region AHTTPPDUBuilder()

        /// <summary>
        /// Create a new HTTP header builder.
        /// </summary>
        public AHTTPPDUBuilder()
        {

            this.HeaderFields  = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

            this.Date          = DateTime.UtcNow;

        }

        #endregion

        #endregion


        #region (protected) SetProperty<T>(ref Field, NewValue, PropertyName)

        /// <summary>
        /// Change a property value and raises an PropertyChanged event.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="Field">The internal field.</param>
        /// <param name="NewValue">The new value of the property.</param>
        /// <param name="PropertyName">The name of the property.</param>
        protected void SetProperty<T>(ref T Field, T NewValue, String PropertyName)
        {

            if (!EqualityComparer<T>.Default.Equals(Field, NewValue))
            {

                Field = NewValue;

                // Take a copy of the handler for concurrency issues!
                PropertyChanged?.Invoke(this,
                                        new PropertyChangedEventArgs(PropertyName));

            }

        }

        #endregion



        #region (protected) PrepareImmutability()

        /// <summary>
        /// Prepares the immutability of an HTTP PDU, e.g. calculates
        /// and set the Content-Length header.
        /// </summary>
        protected virtual void PrepareImmutability()
        {

            // Set the Content-Length if it was not set before
            if (ContentLength == null || ContentLength == 0)
            {

                if (Content != null)
                    ContentLength = (UInt64) Content.LongLength;

                else if (ContentStream != null &&
                         ContentStream.CanSeek) // NetworkStreams are not seekable!
                    ContentLength = (UInt64) ContentStream.Length;

            }

        }

        #endregion



        #region (protected) TryGetHeaderField(FieldName)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <param name="FieldName">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        protected Boolean TryGetHeaderField(String FieldName, out Object Value)
        {
            return HeaderFields.TryGetValue(FieldName, out Value);
        }

        #endregion

        #region (protected) TryGetHeaderField(HeaderField)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <param name="FieldName">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        protected Boolean TryGetHeaderField(HTTPHeaderField HeaderField, out Object Value)
        {
            return HeaderFields.TryGetValue(HeaderField.Name, out Value);
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



        #region (protected) GetHeaderField(FieldName)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        protected String GetHeaderField(String FieldName)
        {

            Object Value;
            if (HeaderFields.TryGetValue(FieldName, out Value))
                return Value.ToString();

            return null;

        }

        #endregion

        #region (protected) GetHeaderField<T>(FieldName)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="FieldName">The name of the header field.</param>
        protected T GetHeaderField<T>(String FieldName)
        {

            Object Value;
            if (HeaderFields.TryGetValue(FieldName, out Value))
                if (Value is T)
                    return (T) Value;

            return default(T);

        }

        #endregion

        #region (protected) GetHeaderField(HeaderField)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        protected String GetHeaderField(HTTPHeaderField HeaderField)
        {

            if (HeaderFields.TryGetValue(HeaderField.Name, out Object Value))
                return Value.ToString();

            return null;

        }

        #endregion

        #region (protected) GetHeaderField<T>(HeaderField)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="HeaderField">The HTTP header field.</param>
        protected T GetHeaderField<T>(HTTPHeaderField HeaderField)
        {

            Object Value;
            if (HeaderFields.TryGetValue(HeaderField.Name, out Value))
                if (Value is String)
                {
                    if (HeaderField.Type == typeof(String))
                        return (T) Value;
                    else
                    {
                        Object Value2 = null;
                        if (HeaderField.StringParser(Value.ToString(), out Value2))
                            return (T) Value2;
                    }
                }
                else
                    return (T) Value;

            return default(T);

        }

        #endregion

        #region (protected) GetHeaderField_Int64(HeaderField)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        protected Int64? GetHeaderField_Int64(HTTPHeaderField HeaderField)
        {

            if (HeaderFields.TryGetValue(HeaderField.Name, out Object Value))
            {

                if (Value is Int64?)
                    return (Int64?) Value;

                if (Int64.TryParse(Value.ToString(), out Int64 Int64Value))
                    return Int64Value;

            }

            return null;

        }

        #endregion

        #region (protected) GetHeaderField_UInt64(HeaderField)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="HeaderField">The HTTP header field.</param>
        protected UInt64? GetHeaderField_UInt64(HTTPHeaderField HeaderField)
        {

            if (HeaderFields.TryGetValue(HeaderField.Name, out Object Value))
            {

                if (Value is UInt64?)
                    return (UInt64?)Value;

                if (UInt64.TryParse(Value.ToString(), out UInt64 UInt64Value))
                    return UInt64Value;

            }

            return null;

        }

        #endregion


        #region (protected) SetHeaderField(FieldName, Value)

        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        protected void SetHeaderField(String FieldName, Object Value)
        {

            if (Value != null)
            {

                if (HeaderFields.ContainsKey(FieldName))
                    HeaderFields[FieldName] = Value;
                else
                    HeaderFields.Add(FieldName, Value);

            }

            else
                if (HeaderFields.ContainsKey(FieldName))
                    HeaderFields.Remove(FieldName);

        }

        #endregion

        #region (protected) SetHeaderField(HeaderField, Value)

        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="HeaderField">The header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        protected void SetHeaderField(HTTPHeaderField HeaderField, Object Value)
        {

            if (Value != null)
            {

                if (HeaderFields.ContainsKey(HeaderField.Name))
                    HeaderFields[HeaderField.Name] = Value;
                else
                    HeaderFields.Add(HeaderField.Name, Value);

            }

            else
                if (HeaderFields.ContainsKey(HeaderField.Name))
                    HeaderFields.Remove(HeaderField.Name);

        }

        #endregion

        #region (protected) RemoveHeaderField(FieldName)

        /// <summary>
        /// Remove a HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        protected void RemoveHeaderField(String FieldName)
        {
            if (HeaderFields.ContainsKey(FieldName))
                HeaderFields.Remove(FieldName);
        }

        #endregion


        #region GetEnumerator()

        /// <summary>
        /// Return an enumeration of all header lines.
        /// </summary>
        public IEnumerator<KeyValuePair<String, Object>> GetEnumerator()
        {
            return HeaderFields.GetEnumerator();
        }

        /// <summary>
        /// Return an enumeration of all header lines.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return HeaderFields.GetEnumerator();
        }

        #endregion

    }

}

