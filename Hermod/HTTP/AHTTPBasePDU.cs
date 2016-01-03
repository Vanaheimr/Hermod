/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Collections.Generic;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
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
        internal protected readonly Dictionary<String, Object>  HeaderFields;

        protected readonly static String[] _LineSeparator   = new String[] { Environment.NewLine };
        protected readonly static Char[]   _ColonSeparator  = new Char[]   { ':' };
        protected readonly static Char[]   _SlashSeparator  = new Char[]   { '/' };
        protected readonly static Char[]   _SpaceSeparator  = new Char[]   { ' ' };
        protected readonly static Char[]   _URLSeparator    = new Char[]   { '?', '!' };
        protected readonly static Char[]   _HashSeparator   = new Char[]   { '#' };

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
            HeaderFields  = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion


        #region (protected) TryParseHeader(HTTPHeaderLines)

        /// <summary>
        /// Try to parse a HTTP header.
        /// </summary>
        /// <param name="HTTPHeader">An enumeration of strings.</param>
        protected Boolean TryParseHeader(String HTTPHeader)
        {

            this.RawHTTPHeader  = HTTPHeader.Trim();

            try
            {

                var Lines = HTTPHeader.Trim().Split(_LineSeparator, StringSplitOptions.RemoveEmptyEntries);

                #region Verify first line...

                FirstPDULine = Lines.FirstOrDefault();
                if (FirstPDULine == null)
                {
                    this.HTTPStatusCode = HTTPStatusCode.BadRequest;
                    return false;
                }

                //ToDo: Verify first line!

                #endregion

                #region Process all other header lines lazily...

                String[] KeyValuePair = null;

                foreach (var Line in Lines.Skip(1))
                {

                    KeyValuePair = Line.Split(_ColonSeparator, 2, StringSplitOptions.RemoveEmptyEntries);

                    // Not valid for every HTTP header... but at least for most...
                    if (KeyValuePair.Length == 1)
                        HeaderFields.Add(KeyValuePair[0].Trim(), String.Empty);

                    else // KeyValuePair.Length == 2
                        HeaderFields.Add(KeyValuePair[0].Trim(), KeyValuePair[1].Trim());

                }

                #endregion

            }
            catch (Exception)
            {
                this.HTTPStatusCode = HTTPStatusCode.BadRequest;
                return false;
            }

            #region Check Host header

            // rfc 2616 - Section 19.6.1.1
            // A client that sends an HTTP/1.1 request MUST send a Host header.

            // rfc 2616 - Section 14.23
            // All Internet-based HTTP/1.1 servers MUST respond with a 400 (Bad Request)
            // status code to any HTTP/1.1 request message which lacks a Host header field.

            // rfc 2616 - Section 5.2 The Resource Identified by a Request
            // 1. If Request-URI is an absoluteURI, the host is part of the Request-URI.
            //    Any Host header field value in the request MUST be ignored.
            // 2. If the Request-URI is not an absoluteURI, and the request includes a
            //    Host header field, the host is determined by the Host header field value.
            // 3. If the host as determined by rule 1 or 2 is not a valid host on the server,
            //    the response MUST be a 400 (Bad Request) error message. (Not valid for proxies?!)
            if (!HeaderFields.ContainsKey(HTTPHeaderField.Host.Name))
                throw new Exception("The HTTP PDU does not have a HOST header!");

            // rfc 2616 - 3.2.2
            // If the port is empty or not given, port 80 is assumed.
            var    HostHeader  = HeaderFields[HTTPHeaderField.Host.Name].ToString().
                                     Split(_ColonSeparator, StringSplitOptions.RemoveEmptyEntries).
                                     Select(v => v.Trim()).
                                     ToArray();

            UInt16 HostPort    = 80;

            if (HostHeader.Length == 1)
                HeaderFields[HTTPHeaderField.Host.Name] = HeaderFields[HTTPHeaderField.Host.Name].ToString();// + ":80"; ":80" will cause side effects!

            else if ((HostHeader.Length == 2 && !UInt16.TryParse(HostHeader[1], out HostPort)) || HostHeader.Length > 2)
            {
                this.HTTPStatusCode = HTTPStatusCode.BadRequest;
                return false;
            }

            #endregion

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

