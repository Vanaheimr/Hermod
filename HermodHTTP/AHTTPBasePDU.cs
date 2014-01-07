/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
    public abstract class AHTTPBasePDU
    {

        #region Data

        /// <summary>
        /// The collection of all HTTP headers.
        /// </summary>
        protected readonly IDictionary<String, Object> HeaderFields;

        protected readonly String[] _LineSeparator;
        protected readonly Char[]   _ColonSeparator;
        protected readonly Char[]   _SlashSeparator;
        protected readonly Char[]   _SpaceSeparator;
        protected readonly Char[]   _URLSeparator;
        protected readonly Char[]   _HashSeparator;

        #endregion

        #region Properties

        #region Non-HTTP header fields

        #region RawHTTPHeader

        /// <summary>
        /// The RAW, unparsed and unverified HTTP header.
        /// </summary>
        public String RawHTTPHeader { get; protected set; }

        #endregion

        #region RawPDU

        /// <summary>
        /// The raw unparsed HTTP protocol data unit.
        /// </summary>
        public String RawPDU { get; set; }

        #endregion

        #region FirstPDULine

        /// <summary>
        /// The first line of a HTTP request or response.
        /// </summary>
        protected String FirstPDULine { get; set; }

        #endregion

        #region HTTPStatusCode

        /// <summary>
        /// The HTTP status code.
        /// </summary>
        public HTTPStatusCode HTTPStatusCode { get; set; }

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

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        public AHTTPBasePDU()
        {

            _LineSeparator   = new String[] { Environment.NewLine };
            _ColonSeparator  = new Char[]   { ':' };
            _SlashSeparator  = new Char[]   { '/' };
            _SpaceSeparator  = new Char[]   { ' ' };
            _URLSeparator    = new Char[]   { '?', '!' };
            _HashSeparator   = new Char[]   { '#' };

            HeaderFields     = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

        }

        #endregion


        #region (protected) TryParseHeader(HTTPHeaderLines)

        /// <summary>
        /// Try to parse a HTTP header.
        /// </summary>
        /// <param name="HTTPHeader">An enumeration of strings.</param>
        protected Boolean TryParseHeader(String HTTPHeader)
        {

            this.HTTPStatusCode  = HTTPStatusCode.BadRequest;
            this.RawHTTPHeader   = HTTPHeader;

            try
            {

                var Lines = HTTPHeader.Split(_LineSeparator, StringSplitOptions.RemoveEmptyEntries);

                FirstPDULine = Lines.FirstOrDefault();
                if (FirstPDULine == null)
                    return false;

                String[] KeyValuePair = null;

                foreach (var Line in Lines.Skip(1))
                {

                    KeyValuePair = Line.Split(_ColonSeparator, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (KeyValuePair.Length == 2)
                        HeaderFields.Add(KeyValuePair[0].Trim(), KeyValuePair[1].Trim());
                    else
                        return false;

                }

            }
            catch (Exception)
            {
                return false;
            }

            this.HTTPStatusCode = HTTPStatusCode.OK;
            return true;

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


    }

}

