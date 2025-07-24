/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Newtonsoft.Json.Linq;
using org.GraphDefined.Vanaheimr.Illias;
using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An abstract HTTP protocol data unit.
    /// An HTTP pdu has three parts:
    ///  - First a request/response specific first line
    ///  - A collection of key-value pairs of type &lt;string,object&gt;
    ///    for any kind of metadata
    ///  - A body hosting the transmitted content
    /// </summary>
    public abstract class AHTTPPDU : IEnumerable<KeyValuePair<String, Object>>,
                                     IDisposable
    {

        #region Data

        /// <summary>
        /// The collection of all HTTP headers.
        /// </summary>
        protected readonly Dictionary<String, Object?>  headerFields;
        protected readonly Dictionary<String, Object>   headerFieldsParsed;

        protected readonly static String[]  lineSeparator    = ["\n", "\r\n"];
        protected readonly static Char[]    colonSeparator   = [':'  ];
        protected readonly static Char[]    slashSeparator   = [ '/' ];
        protected readonly static Char[]    spaceSeparator   = [ ' ' ];
        protected readonly static Char[]    urlSeparator     = [ '?', '!'];
        protected readonly static Char[]    hashSeparator    = [ '#' ];

        /// <summary>
        /// The default size of the HTTP body receive buffer (==8 KByte).
        /// </summary>
        public const UInt32 DefaultHTTPBodyReceiveBufferSize  =    8 * 1024 * 1024;

        /// <summary>
        /// The maximum size of the HTTP body receive buffer (==1 MByte).
        /// </summary>
        public const UInt32 MaxHTTPBodyReceiveBufferSize      = 1024 * 1024 * 1024;

        #endregion

        #region Non-HTTP header fields

        /// <summary>
        /// The timestamp of the HTTP PDU generation.
        /// </summary>
        public DateTime                 Timestamp            { get; }

        /// <summary>
        /// The cancellation token.
        /// </summary>
        public CancellationToken        CancellationToken    { get; }

        /// <summary>
        /// An unique event tracking identification for correlating this request with other events.
        /// </summary>
        public EventTracking_Id         EventTrackingId      { get; }

        /// <summary>
        /// The remote TCP/IP socket.
        /// </summary>
        public HTTPSource               HTTPSource           { get; internal set; }

        /// <summary>
        /// The IP socket of the HTTP packet.
        /// </summary>
        public IPSocket                 RemoteSocket         { get; internal set; }

        /// <summary>
        /// An additional enumeration of IP addresses, when the message has been forwarded between HTTP servers.
        /// </summary>
        public IEnumerable<IIPAddress>  ForwardedFor
                   => HTTPSource.ForwardedFor;

        /// <summary>
        /// The local TCP/IP socket.
        /// </summary>
        public IPSocket                 LocalSocket          { get; internal set; }


        /// <summary>
        /// The RAW, unparsed and unverified HTTP header.
        /// </summary>
        public String                   RawHTTPHeader        { get; internal set; }

        /// <summary>
        /// The raw unparsed HTTP protocol data unit.
        /// </summary>
        public String                   RawPDU               { get; }

        /// <summary>
        /// The first line of a HTTP request or response.
        /// </summary>
        public String                   FirstPDULine         { get; }


        #region (protected) ConstructedHTTPHeader

        /// <summary>
        /// Return a text representation of this HTTP header.
        /// </summary>
        protected String ConstructedHTTPHeader
        {
            get
            {

                var sb = new StringBuilder();

                foreach (var headerField in headerFields)
                {

                    if (headerField.Key is null)
                        continue;

                    if (headerField.Value is null)
                        continue;

                    if      (headerField.Value is String              text)
                        sb.Append($"{headerField.Key}: {text}\r\n");

                    else if (headerField.Value is IEnumerable<String> listOfStrings)
                        sb.Append($"{headerField.Key}: {listOfStrings.AggregateCSV()}\r\n");

                    else if (headerField.Value is IEnumerable         listOfThings)
                    {

                        var list = new List<String>();

                        foreach (var thing in listOfThings)
                        {
                            if (thing is not null)
                            {

                                var thingText = thing?.ToString();

                                if (thingText is not null && thingText.IsNotNullOrEmpty())
                                    list.Add(thingText);

                            }
                        }

                        sb.Append($"{headerField.Key}: {list.AggregateCSV()}\r\n");

                    }

                    else
                        sb.Append($"{headerField.Key}: {headerField.Value}\r\n");

                }

                return sb.ToString().TrimEnd();

                //if (headerFields.Count > 0)
                //    return (from   keyValuePair in headerFields
                //            where  keyValuePair.Key   is not null
                //            where  keyValuePair.Value is not null
                //            select keyValuePair.Key.Trim() + ": " + keyValuePair.Value.ToString().Trim()).
                //            Aggregate((a, b) => a + "\r\n" + b).
                //            Trim();

                //return null;

            }
        }

        #endregion

        #region EntirePDU

        /// <summary>
        /// The entire HTTP protocol data unit.
        /// </summary>
        public String EntirePDU
        {
            get
            {

                TryReadHTTPBodyStream();

                if (httpBody?.Length > 0)
                    return RawHTTPHeader.Trim() + "\r\n\r\n" +
                           Encoding.UTF8.GetString(httpBody);

                //Note: Because of \n vs \r\n the content-length might be invalid when a PDU is loaded from disc!
                //0,
                //ContentLength.HasValue
                //    ? (Int32) ContentLength.Value//Math.Min(HTTPBody.Length, (Int32) ContentLength.Value)
                    //: HTTPBody.Length);

                return RawHTTPHeader;

            }
        }

        #endregion

        #endregion

        #region General header fields

        #region Cache-Control

        /// <summary>
        /// Cache Control
        /// </summary>
        public String? CacheControl

            => GetHeaderField(HTTPHeaderField.CacheControl);

        #endregion

        #region Connection

        /// <summary>
        /// Connection
        /// </summary>
        public ConnectionType? Connection

            => GetHeaderField(HTTPHeaderField.Connection);

        #endregion

        #region Upgrade

        /// <summary>
        /// Upgrade
        /// </summary>
        public String? Upgrade

            => GetHeaderField(HTTPHeaderField.Upgrade);

        #endregion

        #region Content-Encoding

        public Encoding? ContentEncoding

            => GetHeaderField<Encoding>(HTTPHeaderField.ContentEncoding);

        #endregion

        #region Content-Language

        public IEnumerable<String> ContentLanguage

            => GetHeaderFields(HTTPHeaderField.ContentLanguage) ?? Array.Empty<String>();

        #endregion

        #region Content-Length

        public UInt64? ContentLength

            => GetHeaderField(HTTPHeaderField.ContentLength);

        #endregion

        #region Content-Location

        public String? ContentLocation

            => GetHeaderField(HTTPHeaderField.ContentLocation);

        #endregion

        #region Content-MD5

        public String? ContentMD5

            => GetHeaderField(HTTPHeaderField.ContentMD5);

        #endregion

        #region Content-Range

        public String? ContentRange

            => GetHeaderField(HTTPHeaderField.ContentRange);

        #endregion

        #region Content-Type

        public HTTPContentType? ContentType

            => GetHeaderField(HTTPHeaderField.ContentType);

        #endregion

        #region Date

        public DateTime? Date

            => GetHeaderField(HTTPHeaderField.Date);

        #endregion

        #region Via

        public String? Via

            => GetHeaderField(HTTPHeaderField.Via);

        #endregion

        #region Sec-WebSocket-Version

        /// <summary>
        /// Sec-WebSocket-Version
        /// </summary>
        public String? SecWebSocketVersion

            => GetHeaderField(HTTPHeaderField.SecWebSocketVersion);

        #endregion

        #region Transfer-Encoding

        /// <summary>
        /// Transfer-Encoding
        /// </summary>
        public String? TransferEncoding

            => GetHeaderField(HTTPResponseHeaderField.TransferEncoding);

        /// <summary>
        /// Whether this HTTP PDU uses chunked transfer encoding.
        /// </summary>
        public Boolean IsChunkedTransferEncoding

            => TransferEncoding is not null &&
               TransferEncoding.Equals("chunked", StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Keep-Alive

        public KeepAliveType? KeepAlive
        {
            get
            {

                var connection = Connection;

                if (connection.HasValue && connection.Value == ConnectionType.KeepAlive)
                    return GetHeaderField(HTTPHeaderField.KeepAlive);

                return null;

            }
        }

        public Boolean IsKeepAlive
        {
            get
            {

                var connection = Connection;

                if (connection.HasValue && connection.Value == ConnectionType.KeepAlive)
                    return true;

                return false;

            }
        }

        #endregion

        #endregion

        #region HTTPBody

        private Byte[]? httpBody;

        /// <summary>
        /// The HTTP body/content as an array of bytes.
        /// </summary>
        public Byte[]? HTTPBody
        {
            get
            {
                TryReadHTTPBodyStream();
                return httpBody;
            }
        }

        internal void ResizeBody(Int32 NewSize)
        {
            Array.Resize(ref httpBody, NewSize);
        }

        #endregion

        #region HTTPBodyAsUTF8String

        /// <summary>
        /// Return the HTTP body/content as an UTF8 string.
        /// </summary>
        public String? HTTPBodyAsUTF8String
        {
            get
            {

                try
                {

                    TryReadHTTPBodyStream();

                    if (httpBody?.Length > 0)
                        return httpBody.ToUTF8String();

                }
                catch
                { }

                return null;

            }
        }

        #endregion

        #region HTTPBodyAsJSONObject

        /// <summary>
        /// Return the HTTP body/content as a JSON object.
        /// </summary>
        public JObject? HTTPBodyAsJSONObject
        {
            get
            {

                try
                {

                    TryReadHTTPBodyStream();

                    if (httpBody?.Length > 0)
                        return JObject.Parse(httpBody.ToUTF8String());

                }
                catch
                { }

                return null;

            }
        }

        #endregion

        #region HTTPBodyAsJSONArray

        /// <summary>
        /// Return the HTTP body/content as a JSON array.
        /// </summary>
        public JArray? HTTPBodyAsJSONArray
        {
            get
            {

                try
                {

                    TryReadHTTPBodyStream();

                    if (httpBody?.Length > 0)
                        return JArray.Parse(httpBody.ToUTF8String());

                }
                catch
                { }

                return null;

            }
        }

        #endregion

        #region HTTPBodyStream

        private Stream? httpBodyStream;

        /// <summary>
        /// The HTTP body as a stream of bytes.
        /// </summary>
        public Stream? HTTPBodyStream
        {
            get
            {
                return httpBodyStream;
            }
            set
            {
                httpBodyStream = value;
            }
        }

        #endregion

        #region HTTPBodyReceiveBufferSize

        /// <summary>
        /// The size of the HTTP body receive buffer.
        /// </summary>
        public UInt32  HTTPBodyReceiveBufferSize   { get; }

        #endregion

        #region Constructor(s)

        #region (protected) AHTTPPDU()

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        protected AHTTPPDU()
        {

            this.Timestamp                  = Illias.Timestamp.Now;
            this.headerFields               = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            this.headerFieldsParsed         = new Dictionary<String, Object> (StringComparer.OrdinalIgnoreCase);
            this.HTTPBodyReceiveBufferSize  = DefaultHTTPBodyReceiveBufferSize;
            this.RawHTTPHeader              = "";
            this.RawPDU                     = "";
            this.FirstPDULine               = "";
            this.EventTrackingId            = EventTracking_Id.New;
            //this.secWebSocketProtocol       = Array.Empty<String>();

        }

        #endregion

        #region (protected) AHTTPPDU(HTTPPDU)

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        /// <param name="HTTPPDU">Another HTTP PDU.</param>
        protected AHTTPPDU(AHTTPPDU HTTPPDU)

            : this()

        {

            this.Timestamp                  = HTTPPDU.Timestamp;
            this.HTTPSource                 = HTTPPDU.HTTPSource;
            this.RemoteSocket               = HTTPPDU.RemoteSocket;
            this.LocalSocket                = HTTPPDU.LocalSocket;
            this.RawHTTPHeader              = HTTPPDU.RawHTTPHeader;
            this.RawPDU                     = HTTPPDU.RawPDU;
            this.httpBody                   = HTTPPDU.HTTPBody;
            this.httpBodyStream             = HTTPPDU.HTTPBodyStream;
            this.HTTPBodyReceiveBufferSize  = DefaultHTTPBodyReceiveBufferSize;
            this.CancellationToken          = HTTPPDU.CancellationToken;
            this.EventTrackingId            = HTTPPDU.EventTrackingId;

            this.FirstPDULine               = HTTPPDU.FirstPDULine;

            if (HTTPPDU.headerFields is not null)
                foreach (var field in HTTPPDU.headerFields)
                    headerFields.Add(field.Key, field.Value);

        }

        #endregion

        #region (protected) AHTTPPDU(Timestamp, HTTPSource, LocalSocket, HTTPHeader, HTTPBody = null, HTTPBodyStream = null, CancellationToken = null, EventTrackingId = null)

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="HTTPSource">The HTTP source.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        protected AHTTPPDU(DateTime           Timestamp,
                           HTTPSource         HTTPSource,
                           IPSocket           LocalSocket,
                           IPSocket           RemoteSocket,
                           String             HTTPHeader,
                           Byte[]?            HTTPBody                    = null,
                           Stream?            HTTPBodyStream              = null,
                           UInt32?            HTTPBodyReceiveBufferSize   = DefaultHTTPBodyReceiveBufferSize,
                           EventTracking_Id?  EventTrackingId             = null,
                           CancellationToken  CancellationToken           = default)

            : this()

        {

            this.Timestamp                  = Timestamp;
            this.HTTPSource                 = HTTPSource;
            this.LocalSocket                = LocalSocket;
            this.RemoteSocket               = RemoteSocket;
            this.RawHTTPHeader              = HTTPHeader.Trim();
            this.httpBody                   = HTTPBody;
            this.httpBodyStream             = HTTPBodyStream;
            this.HTTPBodyReceiveBufferSize  = HTTPBodyReceiveBufferSize.HasValue
                                                  ? HTTPBodyReceiveBufferSize.Value < MaxHTTPBodyReceiveBufferSize
                                                        ? HTTPBodyReceiveBufferSize.Value
                                                        : DefaultHTTPBodyReceiveBufferSize
                                                  : DefaultHTTPBodyReceiveBufferSize;
            this.CancellationToken          = CancellationToken;
            this.EventTrackingId            = EventTrackingId ?? EventTracking_Id.New;

            #region Process first line...

            var allLines = this.RawHTTPHeader.Split(lineSeparator,
                                                    StringSplitOptions.RemoveEmptyEntries);

            if (allLines is null || allLines.Length < 2)
                throw new Exception("Bad request");

            FirstPDULine = allLines.First();

            if (FirstPDULine is null)
                throw new Exception("Bad request");

            #endregion

            #region ...process all other header lines

            foreach (var headerLine in allLines.Skip(1))
            {

                if (headerLine.IsNullOrEmpty())
                    break;

                var keyValuePair = headerLine.Split(colonSeparator, 2);

                // Not valid for every HTTP header... but at least for most...
                if (keyValuePair.Length == 1)
                    headerFields.Add(keyValuePair[0].Trim(), String.Empty);

                else
                {

                    var key = keyValuePair[0].Trim();

                    if (key.IsNotNullOrEmpty())
                    {

                        if (!headerFields.ContainsKey(key))
                            headerFields.Add(key, keyValuePair[1].Trim());

                        else
                        {

                            if (headerFields[key] is String existingValue)
                                headerFields[key] = new[] { existingValue, keyValuePair[1].Trim() };

                            else if (headerFields[key] is String[] existingValues)
                                headerFields[key] = existingValues.Append(keyValuePair[1].Trim()).ToArray();

                        }

                    }

                }

            }

            #endregion

        }

        #endregion

        #endregion


        #region TryGetHeaderField(FieldName)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <param name="FieldName">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        public Boolean TryGetHeaderField(String                           FieldName,
                                         [NotNullWhen(true)] out Object?  Value)

            => headerFields.TryGetValue(FieldName, out Value);

        #endregion

        #region TryGetHeaderField(HeaderField)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <param name="HeaderField">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        public Boolean TryGetHeaderField<T>(HTTPHeaderField<T>          HeaderField,
                                            [NotNullWhen(true)] out T?  Value)
        {

            if (headerFields.TryGetValue(HeaderField.Name, out var value) &&
                value is T valueT)
            {
                Value = valueT;
                return true;
            }

            Value = default;
            return false;

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
        public Boolean TryGet<T>(String Key, out T? Value)
        {

            if (headerFields.TryGetValue(Key, out var value))
            {

                if (value is T)
                {
                    Value = (T) value;
                    return true;
                }

                else if (typeof(T).Equals(typeof(Int32)))
                {
                    if (Int32.TryParse(value.ToString(), out var int32))
                    {
                        Value = (T) (Object) int32;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                }

                else if (typeof(T).Equals(typeof(UInt32)))
                {
                    if (UInt32.TryParse(value.ToString(), out var uInt32))
                    {
                        Value = (T) (Object) uInt32;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                }

                else if (typeof(T).Equals(typeof(Int64)))
                {
                    if (Int64.TryParse(value.ToString(), out var int64))
                    {
                        Value = (T) (Object) int64;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                }

                else if (typeof(T).Equals(typeof(UInt64)))
                {
                    if (UInt64.TryParse(value.ToString(), out var uInt64))
                    {
                        Value = (T) (Object) uInt64;
                        SetHeaderField(Key, Value);
                        return true;
                    }
                }

                else
                {
                    try
                    {
                        Value = (T) value;
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

        #region TryParseHeaderField(HeaderField, TryParser)

        /// <summary>
        /// Parse the given http header field.
        /// </summary>
        /// <param name="HeaderField">The key of the requested header field.</param>
        /// <param name="TryParser">The header field parser.</param>
        public TValue? TryParseHeaderField<TValue>(String             HeaderField,
                                                   TryParser<TValue>  TryParser)

            where TValue : struct

        {

            if (TryParser is not null &&
                headerFields.TryGetValue(HeaderField, out var value) &&
                value is String text &&
                TryParser(text, out var value2))
            {
                return value2;
            }

            return default;

        }


        ///// <summary>
        ///// Parse the given http header field.
        ///// </summary>
        ///// <param name="HeaderField">The key of the requested header field.</param>
        ///// <param name="TryParser">The header field parser.</param>
        //public TValue? TryParseHeaderField<TValue>(HTTPHeaderField<TValue>  HeaderField,
        //                                           TryParser<TValue>        TryParser)

        //    where TValue : struct

        //{

        //    if (TryParser is not null &&
        //        headerFields.TryGetValue(HeaderField.Name, out var value) &&
        //        value is String text &&
        //        TryParser(text, out var Value))
        //    {
        //        return Value;
        //    }

        //    return default;

        //}

        #endregion

        #region TryParseHeaderField(HeaderField, TryParser, Value)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <param name="HeaderField">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        public Boolean TryParseHeaderField<TValue>(String             HeaderField,
                                                   TryParser<TValue>  TryParser,
                                                   out TValue?        Value)
        {

            if (TryParser != null &&
                headerFields.TryGetValue(HeaderField, out var value) &&
                value is String text &&
                TryParser(text, out Value))
            {
                return true;
            }

            Value = default;
            return false;

        }


        ///// <summary>
        ///// Return a http header field.
        ///// </summary>
        ///// <param name="HeaderField">The key of the requested header field.</param>
        ///// <param name="Value">The value of the requested header field.</param>
        ///// <returns>True if the requested header exists; false otherwise.</returns>
        //public Boolean TryParseHeaderField<TValue>(HTTPHeaderField<TValue>  HeaderField,
        //                                           TryParser<TValue>        TryParser,
        //                                           out TValue?              Value)
        //{

        //    if (TryParser != null &&
        //        headerFields.TryGetValue(HeaderField.Name, out var value) &&
        //        value is String text &&
        //        TryParser(text, out Value))
        //    {
        //        return true;
        //    }

        //    Value = default;
        //    return false;

        //}

        #endregion


        #region GetHeaderField        (FieldName)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        public String? GetHeaderField(String FieldName)

            => headerFields.TryGetValue(FieldName, out var httpValue)
                   ? httpValue?.ToString()
                   : null;

        #endregion

        #region GetHeaderField<T>     (FieldName)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        public T? GetHeaderField<T>(String FieldName)

            => headerFields.TryGetValue(FieldName, out var httpValue)
                   ? httpValue is T valueT ? valueT : default
                   : default;

        #endregion

        #region GetHeaderStruct<T>    (FieldName, Parser)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="FieldName">The name of the header field.</param>
        public T? GetHeaderStruct<T>(String FieldName, Func<String, T?> Parser)
            where T: struct
        {

            if (headerFields.TryGetValue(FieldName, out var headerValue))
            {

                if (headerValue is T valueT)
                    return valueT;

                if (headerValue is String text)
                {
                    try
                    {
                        return Parser(text);
                    }
                    catch
                    { }
                }

            }

            return null;

        }

        #endregion

        #region GetHeaderField<T>     (FieldName, Parser)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="FieldName">The name of the header field.</param>
        public T? TryGetHeaderField<T>(String FieldName, Func<String, T?> Parser)
            where T: class
        {

            if (headerFields.TryGetValue(FieldName, out var headerValue))
            {

                if (headerValue is T valueT)
                    return valueT;

                if (headerValue is String text)
                {
                    try
                    {
                        return Parser(text);
                    }
                    catch
                    { }
                }

            }

            return default;

        }

        #endregion

        #region TryGetHeaderStruct<T> (FieldName, TryParser)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="FieldName">The name of the header field.</param>
        public T? TryGetHeaderStruct<T>(String FieldName, TryParser<T> TryParser)
            where T: struct
        {

            if (headerFields.TryGetValue(FieldName, out var headerValue))
            {

                if (headerValue is T valueT)
                    return valueT;

                if (headerValue is String text &&
                    TryParser(text, out var parsedValue))
                {
                    return parsedValue;
                }

            }

            return null;

        }

        #endregion

        #region TryGetHeaderField<T>  (FieldName, TryParser)

        /// <summary>
        /// Return the given HTTP header field.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="FieldName">The name of the header field.</param>
        public T? TryGetHeaderField<T>(String FieldName, TryParser<T> TryParser)
            where T: class
        {

            if (headerFields.TryGetValue(FieldName, out var headerValue))
            {

                if (headerValue is T valueT)
                    return valueT;

                if (headerValue is String text &&
                    TryParser(text, out var parsedValue))
                {
                    return parsedValue;
                }

            }

            return default;

        }

        #endregion


        #region GetHeaderField        (HeaderField)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The name of the header field.</param>
        public String? GetHeaderField(HTTPHeaderField HeaderField)

            => headerFields.TryGetValue(HeaderField.Name, out var httpValue)
                   ? httpValue?.ToString()
                   : null;

        #endregion

        #region GetHeaderField        (HeaderField)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        public T? GetHeaderField<T>(HTTPHeaderField<T> HeaderField)
        {

            if (headerFieldsParsed.TryGetValue(HeaderField.Name, out var parsedValues) &&
                parsedValues is T valuesT)
            {
                return valuesT;
            }

            if (headerFields.TryGetValue(HeaderField.Name, out var value) &&
                value is not null)
            {

                //if (value is String text &&
                //    HeaderField.StringParser is not null &&
                //    HeaderField.StringParser(text, out var valueT) &&
                //    valueT is not null)
                //{
                //    headerFieldsParsed.TryAdd(HeaderField.Name, valueT);
                //    return valueT;
                //}

                if (value is String text)
                {

                    if (typeof(T).Equals(typeof(String)))
                    {
                        return (T) value;
                    }

                    if (HeaderField.StringParser is not null           &&
                        HeaderField.StringParser(text, out var valueT) &&
                        valueT is not null)
                    {
                        headerFieldsParsed.Remove(HeaderField.Name);
                        headerFieldsParsed.TryAdd(HeaderField.Name, valueT);
                        return valueT;
                    }

                }

            }

            return default;

        }

        #endregion

        #region GetHeaderFields       (HeaderField, DefaultT = default)

        /// <summary>
        /// Return the value of the given HTTP header field.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        public T? GetHeaderFields<T>(HTTPHeaderField<T>  HeaderField,
                                     T?                  DefaultT   = default)
        {

            if (headerFieldsParsed.TryGetValue(HeaderField.Name, out var parsedValues) &&
                parsedValues is T valuesT)
            {
                return valuesT;
            }

            if (headerFields.TryGetValue(HeaderField.Name, out var value) &&
                value is not null)
            {

                if (value is String text &&
                    HeaderField.StringParser is not null)
                {

                    var text2 = text.Trim();

                    if (text2.IsNotNullOrEmpty() &&
                        HeaderField.StringParser(text2, out var listOfValues) &&
                        listOfValues is not null)
                    {
                        headerFieldsParsed.Add(HeaderField.Name, listOfValues);
                        return listOfValues;
                    }

                }

            }

            return DefaultT;

        }

        #endregion


        #region (protected internal) RemoveHeaderField(FieldName)

        /// <summary>
        /// Remove a HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        protected internal void RemoveHeaderField(String FieldName)
        {
            if (headerFields.ContainsKey(FieldName))
                headerFields.Remove(FieldName);
        }

        #endregion


        #region (protected internal) SetHeaderField(FieldName, Value)

        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        protected internal void SetHeaderField(String FieldName, Object Value)
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

        #endregion

        #region (protected internal) SetHeaderField(HeaderField, Value)

        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        protected internal void SetHeaderField<T>(HTTPHeaderField<T> HeaderField, T Value)
        {

            if (Value is not null)
            {

                if (headerFields.ContainsKey(HeaderField.Name))
                    headerFields[HeaderField.Name] = Value;
                else
                    headerFields.Add(HeaderField.Name, Value);

            }

            else
                if (headerFields.ContainsKey(HeaderField.Name))
                    headerFields.Remove(HeaderField.Name);


            //// New collection...
            //if (Value is not null)
            //{

            //    if (_HeaderFields2.ContainsKey(HeaderField))
            //        _HeaderFields2[HeaderField] = Value;
            //    else
            //        _HeaderFields2.Add(HeaderField, Value);

            //}

            //else
            //    if (_HeaderFields2.ContainsKey(HeaderField))
            //    _HeaderFields2.Remove(HeaderField);

        }

        #endregion


        #region IEnumerable<KeyValuePair<String, Object>> Members

        /// <summary>
        /// Return a HTTP header enumerator.
        /// </summary>
        public IEnumerator<KeyValuePair<String, Object>> GetEnumerator()
        {
            return headerFields.GetEnumerator();
        }

        /// <summary>
        /// Return a HTTP header enumerator.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return headerFields.GetEnumerator();
        }

        #endregion



        public Task<IEnumerable<(String, String)>>

            ReadAllChunks(Action<Byte[]>     OnChunkReceived,
                          CancellationToken  CancellationToken   = default)

        {

            if (httpBodyStream is ChunkedTransferEncodingStream chunkedStream)
                return chunkedStream.ReadAllChunks(
                           OnChunkReceived,
                           CancellationToken
                       );

            return Task.FromResult<IEnumerable<(String, String)>>([]);

        }


        #region TryReadHTTPBodyStream()

        public Boolean TryReadHTTPBodyStream()
        {

            lock (headerFields)
            {

                if (httpBody is not null)
                    return true;

                if (httpBodyStream is null ||
                   !ContentLength.HasValue ||
                    ContentLength.Value == 0)
                {
                    httpBody = Array.Empty<Byte>();
                    return true;
                }

                httpBody      ??= new Byte[(Int32) ContentLength.Value];
                var read        = 0;
                var position    = 0;
                var retry       = 0;
                var maxRetries  = 20;

                while (position < httpBody.Length && retry < maxRetries)
                {

                    try
                    {

                        read = httpBodyStream.Read(httpBody,
                                                   position,
                                                   httpBody.Length - position);

                        if (read == 0) {
                            Thread.Sleep(5);
                            retry++;
                            continue;
                        }

                        position += read;
                        retry     = 0;

                    }
                    catch (IOException ex)
                    {
                        // If the ReceiveTimeout is reached an IOException will be raised...
                        // with an InnerException of type SocketException and ErrorCode 10060

                        // If it's not the "expected" exception, let's not hide the error
                        if (ex.InnerException is not SocketException socketException || socketException.ErrorCode != 10060)
                            throw;

                        // If it is the receive timeout, then reading ended
                        break;

                    }
                    catch (Exception e)
                    {
                        DebugX.LogT($"{nameof(AHTTPPDU)} could not read HTTP body ({ContentLength.Value} bytes): {e.Message}");
                        return false;
                    }

                }


                if (position == httpBody.Length)
                    return true;

                else
                {
                    Array.Resize(ref httpBody, position);
                    return false;
                }

            }

        }

        #endregion


        #region NewContentStream()

        public MemoryStream NewContentStream()
        {

            var memoryStream = new MemoryStream();

            httpBodyStream = memoryStream;

            return memoryStream;

        }

        #endregion

        #region ContentStreamToArray(DataStream = null)

        public void ContentStreamToArray(Stream? DataStream = null)
        {

            if (DataStream is null)
                httpBody = HTTPBodyStream is null
                               ? Array.Empty<Byte>()
                               : httpBody = ((MemoryStream) HTTPBodyStream).ToArray();

            else
                httpBody = ((MemoryStream) DataStream).ToArray();

        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Release all resources, e.g. internal streams.
        /// </summary>
        public void Dispose()
        {

            httpBodyStream?.Dispose();
            httpBodyStream = null;

            GC.SuppressFinalize(this);

        }

        #endregion

    }

}

