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
using System.Text;
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    /// <summary>
    /// An abstract HTTP header.
    /// </summary>
    public abstract class AHTTPHeader : IEnumerable<String>
    {

        #region Data

        /// <summary>
        /// All header fields.
        /// </summary>
        protected readonly IDictionary<String, Object> HeaderFields;

        protected readonly String[] _LineSeperator;
        protected readonly Char[]   _ColonSeperator;
        protected readonly Char[]   _SlashSeperator;
        protected readonly Char[]   _SpaceSeperator;
        protected readonly Char[]   _URLSeperator;

        #endregion

        #region Properties

        #region HTTPStatusCode

        /// <summary>
        /// The HTTP status code.
        /// </summary>
        public HTTPStatusCode HTTPStatusCode { get; protected set; }

        #endregion

        #region General header fields

        #region CacheControl

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

        #region ContentEncoding

        public Encoding ContentEncoding
        {
            get
            {
                return GetHeaderField<Encoding>("Content-Encoding");
            }
        }

        #endregion

        #region ContentLanguage

        public new List<String> ContentLanguage
        {
            get
            {
                return GetHeaderField<List<String>>(HTTPHeaderField.ContentLanguage);
            }
        }

        #endregion

        #region ContentLength

        public UInt64? ContentLength
        {
            get
            {
                return GetHeaderField<UInt64>(HTTPHeaderField.ContentLength);
            }
        }

        #endregion

        #region ContentLocation

        public String ContentLocation
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.ContentLocation);
            }
        }

        #endregion

        #region ContentMD5

        public String ContentMD5
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.ContentMD5);
            }
        }

        #endregion

        #region ContentRange

        public String ContentRange
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.ContentRange);
            }
        }

        #endregion

        #region ContentType

        public HTTPContentType ContentType
        {
            get
            {
                return GetHeaderField<HTTPContentType>("Content-Type");
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

        #region RAWHTTPHeader

        /// <summary>
        /// Return a string representation of this HTTPHeader.
        /// </summary>
        public String RAWHTTPHeader
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

        #endregion

        #region Constructor(s)

        #region AHTTPHeader()

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        public AHTTPHeader()
        {

            _LineSeperator  = new String[] { Environment.NewLine };
            _ColonSeperator = new Char[]   { ':' };
            _SlashSeperator = new Char[]   { '/' };
            _SpaceSeperator = new Char[]   { ' ' };
            _URLSeperator   = new Char[]   { '?', '!' };

            HeaderFields    = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

        }

        #endregion

        #endregion


        #region (protected) ParseHeader(HTTPHeaderLines)

        /// <summary>
        /// Parse an HTTP header.
        /// </summary>
        /// <param name="HTTPHeaderLines">An enumeration of strings.</param>
        protected void ParseHeader(IEnumerable<String> HTTPHeaderLines)
        {

            if (HTTPHeaderLines.Count() == 0)
            {
                this.HTTPStatusCode = HTTPStatusCode.BadRequest;
                return;
            }

            String[] _KeyValuePairs = null;

            foreach (var _Line in HTTPHeaderLines)
            {
                _KeyValuePairs = _Line.Split(_ColonSeperator, 2, StringSplitOptions.RemoveEmptyEntries);
                HeaderFields.Add(_KeyValuePairs[0].Trim(), _KeyValuePairs[1].Trim());
            }

            this.HTTPStatusCode = HTTPStatusCode.OK;

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


        #region GetEnumerator()

        /// <summary>
        /// Return an enumeration of all header lines.
        /// </summary>
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

        /// <summary>
        /// Return an enumeration of all header lines.
        /// </summary>
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

        #endregion

    }

}

