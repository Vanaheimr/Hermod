/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Text;
using System.Collections;

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
    public abstract class AHTTPPDUBuilder : IEnumerable<KeyValuePair<String, Object?>>
    {

        #region Data

        /// <summary>
        /// The collection of all HTTP headers.
        /// </summary>
        protected readonly Dictionary<String, Object?> headerFields;

        #endregion

        #region Properties

        #region Non-HTTP header fields

        /// <summary>
        /// A unique identification for tracking related events.
        /// </summary>
        public EventTracking_Id?  EventTrackingId    { get; set; }

        /// <summary>
        /// The HTTP status code.
        /// </summary>
        public HTTPStatusCode?    HTTPStatusCode     { get; set; }

        /// <summary>
        /// The HTTP protocol name field.
        /// </summary>
        public String?            ProtocolName       { get; set; }

        /// <summary>
        /// The HTTP protocol version.
        /// </summary>
        public HTTPVersion?       ProtocolVersion    { get; set; }

        /// <summary>
        /// The HTTP body/content as a stream.
        /// </summary>
        public Stream?            ContentStream      { get; set; }


        private Byte[]? content;

        /// <summary>
        /// The HTTP body/content as an array of bytes.
        /// </summary>
        public Byte[]? Content
        {

            get
            {
                return content;
            }

            set
            {

                content        = value;
                ContentLength  = content is not null
                                     ? (UInt64) content.LongLength
                                     : 0;

            }

        }


        /// <summary>
        /// Return a string representation of this HTTPHeader.
        /// </summary>
        public String ConstructedHTTPHeader
        {
            get
            {

                var sb = new StringBuilder();

                foreach (var headerField in headerFields)
                {

                    if (headerField.Key == "Accept")
                    {

                        // Remove an empty "Accept" header...
                        if (headerField.Value is AcceptTypes acceptTypes)
                        {
                            if (!acceptTypes.Any())
                                continue;
                        }

                    }

                    if (headerField.Value is not null)
                    {

                        if (headerField.Value is HTTPCookies httpCookies)
                        {
                            foreach (var cookie in httpCookies)
                                sb.Append($"{headerField.Key}: {cookie}\r\n");
                            continue;
                        }

                        switch (headerField.Value)
                        {

                            case String text:
                                sb.Append($"{headerField.Key}: {text}\r\n");
                                break;

                            case Int32 number:
                                sb.Append($"{headerField.Key}: {number}\r\n");
                                break;

                            case DateTime dateTime:
                                sb.Append($"{headerField.Key}: {dateTime.ToString("r")}\r\n");
                                break;

                            //case String[] texts:
                            //    foreach (var text in texts)
                            //        HTTPHeader.Add(kvp.Key + ": " + text);
                            //    break;

                            case IEnumerable<String> texts:
                                if (texts.Any())
                                {

                                    var definedHeaderField = HTTPHeaderField.GetByName(headerField.Key);
                                    if (definedHeaderField is not null)
                                    {

                                        if (definedHeaderField.MultipleValuesAsList == true)
                                            sb.Append($"{headerField.Key}: {texts.AggregateWith(", ")}\r\n");

                                        else
                                            foreach (var text in texts)
                                                sb.Append($"{headerField.Key}: {text}\r\n");

                                    }

                                }
                                break;

                            case IHTTPAuthentication httpAuthentication:
                                sb.Append($"{headerField.Key}: {httpAuthentication.HTTPText}\r\n");
                                break;

                            case IEnumerable things:

                                var list = new List<String>();

                                foreach (var thing in things)
                                {

                                    var thingText = thing?.ToString();

                                    if (thingText is not null && thingText.IsNotNullOrEmpty())
                                        list.Add(thingText);

                                }

                                sb.Append($"{headerField.Key}: {list.AggregateWith(", ")}\r\n");
                                break;

                            default:
                                sb.Append($"{headerField.Key}: {headerField.Value}\r\n");
                                break;

                        }

                    }
                }

                return sb.ToString();

            }
        }

        #endregion

        #region General header fields

        #region Cache-Control

        public String? CacheControl
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

        public String? Connection
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

        #region Upgrade

        public String? Upgrade
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Upgrade);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Upgrade, value);
            }

        }

        #endregion

        #region Content-Encoding

        public Encoding? ContentEncoding
        {

            get
            {
                return GetHeaderField<Encoding>(HTTPHeaderField.ContentEncoding);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentEncoding, value);
            }

        }

        #endregion

        #region Content-Language

        public IEnumerable<String> ContentLanguage
        {

            get
            {
                return GetHeaderFields(HTTPHeaderField.ContentLanguage);
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
                return GetHeaderField(HTTPHeaderField.ContentLength);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLength, value);
            }

        }

        #endregion

        #region Content-Location

        public String? ContentLocation
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

        public String? ContentMD5
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

        public String? ContentRange
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

        public HTTPContentType? ContentType
        {

            get
            {

                return GetHeaderField(HTTPHeaderField.ContentType);
                //if (contentType is not null)
                //    return contentType;

                //var contentTypeString = GetHeaderField<String>(HTTPHeaderField.ContentType);
                //if (contentTypeString is not null)
                //{

                //    var contentTypeStringElements = contentTypeString.Split('/');

                //    if (contentTypeStringElements.Length == 2)
                //    {

                //        contentType = new HTTPContentType(contentTypeStringElements[0],
                //                                          contentTypeStringElements[1],
                //                                          "utf-8",
                //                                          null,
                //                                          null);

                //        SetHeaderField("Content-Type", contentType);

                //        return contentType;

                //    }

                //}

                //return null;

            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentType, value);
            }

        }

        #endregion

        #region Content-Disposition

        public String? ContentDisposition
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.ContentDisposition);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentDisposition, value);
            }

        }

        #endregion

        #region Date

        /// <summary>
        /// The date and time of the request/response in rfc1123 format.
        /// </summary>
        public DateTime? Date
        {

            get
            {
                return GetHeaderField<DateTime?>(HTTPHeaderField.Date);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Date, value);
            }

        }

        #endregion

        #region Via

        public String? Via
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

        public String? TransferEncoding
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

        #region SecWebSocketProtocol

        public IEnumerable<String> SecWebSocketProtocol
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.SecWebSocketProtocol);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.SecWebSocketProtocol, value);
            }

        }

        #endregion

        #region SecWebSocketVersion

        public String? SecWebSocketVersion
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.SecWebSocketVersion);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.SecWebSocketVersion, value);
            }

        }

        #endregion

        #region Trailer

        public String? Trailer
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Trailer);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Trailer, value);
            }

        }

        #endregion

        #endregion


        // CORS

        #region Access-Control-Allow-Origin

        /// <summary>
        /// Access-Control-Allow-Origin
        /// </summary>
        public String? AccessControlAllowOrigin
        {

            get
            {
                return GetHeaderField(HTTPResponseHeaderField.AccessControlAllowOrigin);
            }

            set
            {
                SetHeaderField(HTTPResponseHeaderField.AccessControlAllowOrigin, value);
            }

        }

        #endregion

        #region Access-Control-Allow-Methods

        /// <summary>
        /// Access-Control-Allow-Methods
        /// </summary>
        public IEnumerable<String> AccessControlAllowMethods
        {

            get
            {
                return GetHeaderFields(HTTPResponseHeaderField.AccessControlAllowMethods);
            }

            set
            {
                SetHeaderField(HTTPResponseHeaderField.AccessControlAllowMethods, value);
            }

        }

        #endregion

        #region Access-Control-Allow-Headers

        /// <summary>
        /// Access-Control-Allow-Headers
        /// </summary>
        public IEnumerable<String> AccessControlAllowHeaders
        {

            get
            {
                return GetHeaderFields(HTTPResponseHeaderField.AccessControlAllowHeaders);
            }

            set
            {
                SetHeaderField(HTTPResponseHeaderField.AccessControlAllowHeaders, value);
            }

        }

        #endregion

        #region Access-Control-Max-Age

        /// <summary>
        /// Access-Control-Max-Age
        /// </summary>
        public UInt64? AccessControlMaxAge
        {

            get
            {
                return GetHeaderField(HTTPResponseHeaderField.AccessControlMaxAge);
            }

            set
            {
                SetHeaderField(HTTPResponseHeaderField.AccessControlMaxAge, value);
            }

        }

        #endregion


        #region X_ExpectedTotalNumberOfItems

        public UInt64? X_ExpectedTotalNumberOfItems
        {

            get
            {
                return GetHeaderField(HTTPResponseHeaderField.X_ExpectedTotalNumberOfItems);
            }

            set
            {
                SetHeaderField(HTTPResponseHeaderField.X_ExpectedTotalNumberOfItems, value);
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
        public String? X_FrameOptions
        {

            get
            {
                return GetHeaderField(HTTPResponseHeaderField.X_FrameOptions);
            }

            set
            {
                SetHeaderField(HTTPResponseHeaderField.X_FrameOptions, value);
            }

        }

        #endregion

        #region Process-ID

        /// <summary>
        /// The unique identification of a server side process,
        /// e.g. used by the Hubject Open InterCharge Protocol.
        /// </summary>
        /// <example>4c1134cd-2ee7-49da-9952-0f53c5456d36</example>
        public String? ProcessID
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.ProcessID);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ProcessID, value);
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP header builder.
        /// </summary>
        public AHTTPPDUBuilder()
        {
            headerFields = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
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
            if (ContentLength is null || ContentLength == 0)
            {

                if (Content is not null)
                    ContentLength = (UInt64) Content.LongLength;

                else if (ContentStream is not null && ContentStream.CanSeek) // NetworkStreams are not seekable!
                    ContentLength = (UInt64) ContentStream.Length;

            }

            if (TransferEncoding == "chunked")
            {
                ContentLength = null;
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
        protected Boolean TryGetHeaderField(String FieldName, out Object? Value)

            => headerFields.TryGetValue(FieldName, out Value);

        #endregion

        #region (protected) TryGetHeaderField(HeaderField)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <param name="FieldName">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        protected Boolean TryGetHeaderField(HTTPHeaderField HeaderField, out Object? Value)

            => headerFields.TryGetValue(HeaderField.Name, out Value);

        #endregion

        #region TryGet<T>(Key)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <typeparam name="T">The type of the value of the requested header field.</typeparam>
        /// <param name="Key">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        public Boolean TryGet<T>(String Key, out T? Value)
        {

            if (headerFields.TryGetValue(Key, out var value))
            {

                if (value is T valueT)
                {
                    Value = valueT;
                    return true;
                }

                else if (typeof(T).Equals(typeof(Int32))  &&  Int32.TryParse(value?.ToString(), out var int32)) {
                    Value = (T) (Object) int32;
                    SetHeaderField(Key, Value);
                    return true;
                }

                else if (typeof(T).Equals(typeof(UInt32)) && UInt32.TryParse(value?.ToString(), out var uInt32)) {
                    Value = (T) (Object) uInt32;
                    SetHeaderField(Key, Value);
                    return true;
                }

                else if (typeof(T).Equals(typeof(Int64))  &&  Int64.TryParse(value?.ToString(), out var int64)) {
                    Value = (T) (Object) int64;
                    SetHeaderField(Key, Value);
                    return true;
                }

                else if (typeof(T).Equals(typeof(UInt64)) && UInt64.TryParse(value?.ToString(), out var uInt64)) {
                    Value = (T) (Object) uInt64;
                    SetHeaderField(Key, Value);
                    return true;
                }

                else
                {
                    try
                    {
                        Value = (T) (Object) value;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                    catch
                    {
                        Value = default;
                        return false;
                    }
                }

            }

            Value = default;
            return false;

        }

        #endregion


        #region (protected) GetHeaderField    (FieldName)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of a HTTP header field.</param>
        protected String? GetHeaderField(String FieldName)
        {

            if (headerFields.TryGetValue(FieldName, out var value))
                return value?.ToString();

            return null;

        }

        #endregion

        #region (protected) GetHeaderField<T> (FieldName, TryParser)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of a HTTP header field.</param>
        /// <param name="TryParser">A parser to parse the string value.</param>
        protected T? GetHeaderFieldy<T>(String        FieldName,
                                        TryParser<T>  TryParser)
        {

            if (headerFields.TryGetValue(FieldName, out var value) &&
                value is String valueString &&
                TryParser(valueString, out var valueT))
            {
                return valueT;
            }

            return default;

        }

        #endregion

        #region (protected) GetHeaderField    (HeaderField)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        protected String? GetHeaderField(HTTPHeaderField HeaderField)
        {

            if (headerFields.TryGetValue(HeaderField.Name, out var value))
                return value?.ToString();

            return null;

        }

        #endregion

        #region (protected) GetHeaderField<T> (HeaderField)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="HeaderField">The HTTP header field.</param>
        protected T? GetHeaderField<T>(HTTPHeaderField<T> HeaderField)
        {

            if (headerFields.TryGetValue(HeaderField.Name, out var value) &&
                value is not null)
            {

                if (value is T valueT)
                    return valueT;

                if (value is String &&
                    HeaderField.StringParser is not null &&
                    HeaderField.StringParser(value?.ToString() ?? String.Empty, out var valueT2))
                {
                    return valueT2;
                }

            }

            return default;

        }

        #endregion

        #region (protected) GetHeaderFields   (HeaderField)

        /// <summary>
        /// Return the values of the given HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        protected String[] GetHeaderFields(HTTPHeaderField HeaderField)
        {

            if (headerFields.TryGetValue(HeaderField.Name, out var valueOrValues))
            {

                if (valueOrValues is null)
                    return Array.Empty<String>();

                if (valueOrValues is String Text)
                    return new String[] { Text };

                if (valueOrValues is String[] Texts)
                    return Texts;

                var result = valueOrValues.ToString();

                return result is null
                           ? Array.Empty<String>()
                           : new[] { result };

            }

            return Array.Empty<String>();

        }

        #endregion

        #region (protected) GetHeaderFields<T>(HeaderField)

        /// <summary>
        /// Return the values of the given HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        protected T GetHeaderFields<T>(HTTPHeaderField<T> HeaderField,
                                       T                  DefaultValueT = default)
        {

            if (headerFields.TryGetValue(HeaderField.Name, out var values))
            {

                if (values is String Text)
                {
                    if (HeaderField.StringParser is not null &&
                        HeaderField.StringParser(Text, out var valuesT) &&
                        valuesT is not null)
                    {
                        return valuesT;
                    }
                }

                if (values is T listOfValues)
                    return listOfValues;

            }

            return DefaultValueT;

        }

        #endregion


        #region SetHeaderField(FieldName,   Value)

        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        public AHTTPPDUBuilder SetHeaderField(String   FieldName,
                                              Object?  Value)
        {

            FieldName = FieldName.Trim();

            if (FieldName.IsNotNullOrEmpty())
            {

                if (Value is not null)
                {
                    if (headerFields.ContainsKey(FieldName))
                        headerFields[FieldName] = Value;
                    else
                        headerFields.Add(FieldName, Value);
                }
                else
                    headerFields.Remove(FieldName);

            }

            return this;

        }

        #endregion

        #region SetHeaderField(HeaderField, Value)

        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="HeaderField">The header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        public AHTTPPDUBuilder SetHeaderField(HTTPHeaderField  HeaderField,
                                              Object?          Value)
        {

            if (Value is not null)
            {
                if (headerFields.ContainsKey(HeaderField.Name))
                    headerFields[HeaderField.Name] = Value;
                else
                    headerFields.Add(HeaderField.Name, Value);
            }

            else
                headerFields.Remove(HeaderField.Name);

            return this;

        }


        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="HeaderField">The header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        public AHTTPPDUBuilder SetHeaderField<T>(HTTPHeaderField<T> HeaderField, Object? Value)
        {

            if (Value is not null)
            {
                if (headerFields.ContainsKey(HeaderField.Name))
                    headerFields[HeaderField.Name] = Value;
                else
                    headerFields.Add(HeaderField.Name, Value);
            }

            else
                headerFields.Remove(HeaderField.Name);

            return this;

        }

        #endregion


        #region RemoveHeaderField(FieldName)

        /// <summary>
        /// Remove a HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        public AHTTPPDUBuilder RemoveHeaderField(String FieldName)
        {

            FieldName = FieldName.Trim();

            if (FieldName.IsNotNullOrEmpty())
                headerFields.Remove(FieldName);

            return this;

        }

        #endregion

        #region RemoveHeaderField(FieldName)

        /// <summary>
        /// Remove a HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The header field.</param>
        public AHTTPPDUBuilder RemoveHeaderField(HTTPHeaderField HeaderField)
        {

            headerFields.Remove(HeaderField.Name);

            return this;

        }

        #endregion


        #region GetEnumerator()

        /// <summary>
        /// Return an enumeration of all header lines.
        /// </summary>
        public IEnumerator<KeyValuePair<String, Object?>> GetEnumerator()
            => headerFields.GetEnumerator();

        /// <summary>
        /// Return an enumeration of all header lines.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
            => headerFields.GetEnumerator();

        #endregion


    }

}
