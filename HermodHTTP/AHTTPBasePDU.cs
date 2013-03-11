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
    public abstract class AHTTPBasePDU
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

        #region Non-HTTP header fields

        #region RawPDU

        /// <summary>
        /// The raw unparsed HTTP protocol data unit.
        /// </summary>
        public String RawPDU { get; set; }

        #endregion

        public String RawHTTPHeader { get; protected set; }

        protected String FirstPDULine { get; set; }

        #region HTTPStatusCode

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
                _HTTPStatusCode = value;
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

        #endregion

        #region Constructor(s)

        #region AHTTPHeader()

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        public AHTTPBasePDU()
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
        protected Boolean ParseHeader(String HTTPHeader)//, Boolean ExternalHeader)
        {

            this.HTTPStatusCode = HTTPStatusCode.BadRequest;

            RawHTTPHeader = HTTPHeader;

            var _HTTPHeaderLines = HTTPHeader.Split(_LineSeperator, StringSplitOptions.RemoveEmptyEntries);
            if (_HTTPHeaderLines.Length == 0)
            {
                HTTPStatusCode = HTTPStatusCode.BadRequest;
                return false;
            }

            FirstPDULine = _HTTPHeaderLines.First();

            String[] _KeyValuePairs = null;

            foreach (var _Line in _HTTPHeaderLines.Skip(1))
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

    }

}

