/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An abstract HTTP protocol data unit.
    /// A HTTP pdu has three parts:
    ///  - First a request/response specific first line
    ///  - A collection of key-value pairs of type &lt;string,object&gt;
    ///    for any kind of metadata
    ///  - A body hosting the transmitted content
    /// </summary>
    public abstract class AHTTPPDU : AHTTPBasePDU, IEnumerable<String>
    {

        #region Properties

        #region Non-HTTP header fields

        #region ConstructedHTTPHeader

        /// <summary>
        /// Return a string representation of this HTTPHeader.
        /// </summary>
        public String ConstructedHTTPHeader
        {
            get
            {

                if (HeaderFields.Count > 0)
                    return (from   _KeyValuePair in HeaderFields
                            where  _KeyValuePair.Key   != null
                            where  _KeyValuePair.Value != null
                            select _KeyValuePair.Key + ": " + _KeyValuePair.Value).
                            Aggregate((a, b) => a + Environment.NewLine + b);

                return null;

            }
        }

        #endregion

        #region ProtocolName

        /// <summary>
        /// The HTTP protocol name field.
        /// </summary>
        public String  ProtocolName    { get; protected set; }

        #endregion

        #region ProtocolVersion

        /// <summary>
        /// The HTTP protocol version.
        /// </summary>
        public HTTPVersion ProtocolVersion { get; protected set; }

        #endregion

        #region Content

        /// <summary>
        /// The HTTP body/content as an array of bytes.
        /// </summary>
        public Byte[] Content { get; protected set; }

        #endregion

        #region ContentStream

        /// <summary>
        /// The HTTP body/content as a stream.
        /// </summary>
        public Stream ContentStream { get; protected set; }

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
        }

        #endregion

        #region Connection

        public String Connection
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Connection);
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
        }

        #endregion

        #region Content-Language

        public List<String> ContentLanguage
        {
            get
            {
                return GetHeaderField<List<String>>(HTTPHeaderField.ContentLanguage);
            }
        }

        #endregion

        #region Content-Length

        public UInt64? ContentLength
        {
            get
            {
                return GetHeaderField<UInt64>(HTTPHeaderField.ContentLength);
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
        }

        #endregion

        #region Content-MD5

        public String ContentMD5
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.ContentMD5);
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
        }

        #endregion

        #region Content-Type

        public HTTPContentType ContentType
        {
            get
            {
             //   return GetHeaderField<HTTPContentType>("Content-Type");

                var _ContentType = GetHeaderField<HTTPContentType>("Content-Type");
                if (_ContentType != null)
                    return _ContentType;

                var _ContentTypeString = GetHeaderField<String>("Content-Type");
                if (_ContentTypeString != null)
                {
                    if (HTTPContentType.TryParseString(_ContentTypeString, out _ContentType))
                    {
                        SetHeaderField("Content-Type", _ContentType);
                        return _ContentType;
                    }
                }

                return null;

            }
        }

        #endregion

        #region Date

        public String Date
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Date);
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
        }

        #endregion

        #endregion

        #endregion

        #region Constructor(s)

        #region AHTTPHeader()

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        public AHTTPPDU()
        { }

        #endregion

        #endregion


        #region (private) ParseHeader(HTTPHeaderLines)

        /// <summary>
        /// Parse an HTTP header.
        /// </summary>
        /// <param name="HTTPHeaderLines">An enumeration of strings.</param>
        protected Boolean ParseHeader(IEnumerable<String> HTTPHeaderLines)
        {

            this.HTTPStatusCode = HTTPStatusCode.BadRequest;

            String[] _KeyValuePairs = null;

            foreach (var _Line in HTTPHeaderLines)
            {

                _KeyValuePairs = _Line.Split(_ColonSeperator, 2, StringSplitOptions.RemoveEmptyEntries);

                if (_KeyValuePairs.Length == 2)
                    HeaderFields.Add(_KeyValuePairs[0].Trim(), _KeyValuePairs[1].Trim());
                else
                    return false;

            }

            this.HTTPStatusCode = HTTPStatusCode.OK;
            return true;

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

            Object Value;
            if (HeaderFields.TryGetValue(HeaderField.Name, out Value))
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

        #region (protected) GetHeaderField_Int64(FieldName)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        protected Int64? GetHeaderField_Int64(String FieldName)
        {

            Object Value;
            if (HeaderFields.TryGetValue(FieldName, out Value))
            {

                if (Value is Int64?)
                    return (Int64?) Value;

                Int64 Int64Value;
                if (Int64.TryParse(Value.ToString(), out Int64Value))
                    return Int64Value;

            }

            return null;

        }

        #endregion

        #region (protected) GetHeaderField_UInt64(FieldName)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="FieldName">The name of the header field.</param>
        protected UInt64? GetHeaderField_UInt64(String FieldName)
        {

            Object Value;
            if (HeaderFields.TryGetValue(FieldName, out Value))
            {

                if (Value is UInt64?)
                    return (UInt64?) Value;

                UInt64 UInt64Value;
                if (UInt64.TryParse(Value.ToString(), out UInt64Value))
                    return UInt64Value;

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

            Object Value;
            if (HeaderFields.TryGetValue(HeaderField.Name, out Value))
            {

                if (Value is UInt64?)
                    return (UInt64?)Value;

                UInt64 UInt64Value;
                if (UInt64.TryParse(Value.ToString(), out UInt64Value))
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
        /// <param name="FieldName">The name of the header field.</param>
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


        public String EntirePDU
        {
            get
            {

                if (Content != null && Content.Length > 0)
                    return RawHTTPHeader + Environment.NewLine + Environment.NewLine +
                           Encoding.UTF8.GetString(Content, 0, Math.Min(Content.Length, (Int32) ContentLength.Value));

                return RawHTTPHeader;

            }
        }


        #region GetEnumerator()

        /// <summary>
        /// Return an enumeration of all header lines.
        /// </summary>
        public IEnumerator<String> GetEnumerator()
        {
            return (from HeaderField in HeaderFields select HeaderField.Key + ": " + HeaderField.Value.ToString()).GetEnumerator();
        }

        /// <summary>
        /// Return an enumeration of all header lines.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (from HeaderField in HeaderFields select HeaderField.Key + ": " + HeaderField.Value.ToString()).GetEnumerator();
        }

        #endregion

    }

}

