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
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using System.Net.Sockets;

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
    public abstract class AHTTPPDU : IEnumerable<KeyValuePair<String, Object>>,
                                     IDisposable
    {

        #region Data

        /// <summary>
        /// The collection of all HTTP headers.
        /// </summary>
        protected readonly Dictionary<String,          Object>  _HeaderFields;
        protected readonly Dictionary<HTTPHeaderField, Object>  _HeaderFields2;

        protected readonly static String[] _LineSeparator   = new String[] { "\n", "\r\n" };
        protected readonly static Char[]   _ColonSeparator  = new Char[]   { ':' };
        protected readonly static Char[]   _SlashSeparator  = new Char[]   { '/' };
        protected readonly static Char[]   _SpaceSeparator  = new Char[]   { ' ' };
        protected readonly static Char[]   _URLSeparator    = new Char[]   { '?', '!' };
        protected readonly static Char[]   _HashSeparator   = new Char[]   { '#' };

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

        #region Timestamp

        /// <summary>
        /// The timestamp of the HTTP request generation.
        /// </summary>
        public DateTime Timestamp { get; }

        #endregion

        #region CancellationToken

        /// <summary>
        /// The cancellation token.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        #endregion

        #region EventTrackingId

        /// <summary>
        /// An unique event tracking identification for correlating this request with other events.
        /// </summary>
        public EventTracking_Id EventTrackingId { get; }

        #endregion

        #region RemoteSocket

        /// <summary>
        /// The remote TCP/IP socket.
        /// </summary>
        public IPSocket RemoteSocket { get; }

        #endregion

        #region LocalSocket

        protected readonly IPSocket _LocalSocket;

        /// <summary>
        /// The local TCP/IP socket.
        /// </summary>
        public IPSocket LocalSocket
        {
            get
            {
                return _LocalSocket;
            }
        }

        #endregion


        #region RawHTTPHeader

        /// <summary>
        /// The RAW, unparsed and unverified HTTP header.
        /// </summary>
        public String RawHTTPHeader { get; }

        #endregion

        #region RawPDU

        /// <summary>
        /// The raw unparsed HTTP protocol data unit.
        /// </summary>
        public String RawPDU { get; }

        #endregion

        #region FirstPDULine

        /// <summary>
        /// The first line of a HTTP request or response.
        /// </summary>
        public String FirstPDULine { get; }

        #endregion


        #region (protected) ConstructedHTTPHeader

        /// <summary>
        /// Return a string representation of this HTTPHeader.
        /// </summary>
        protected String ConstructedHTTPHeader
        {
            get
            {

                if (_HeaderFields.Count > 0)
                    return (from   _KeyValuePair in _HeaderFields
                            where  _KeyValuePair.Key   != null
                            where  _KeyValuePair.Value != null
                            select _KeyValuePair.Key.Trim() + ": " + _KeyValuePair.Value.ToString().Trim()).
                            Aggregate((a, b) => a + "\r\n" + b).
                            Trim();

                return null;

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

                if (HTTPBody?.Length > 0)
                    return RawHTTPHeader.Trim() + "\r\n\r\n" +
                           Encoding.UTF8.GetString(HTTPBody);

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

                if (!_HeaderFields.ContainsKey(HTTPHeaderField.ContentLength.Name))
                    return new Nullable<UInt64>();

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

        #region HTTPBody

        private Byte[] _HTTPBody;

        /// <summary>
        /// The HTTP body/content as an array of bytes.
        /// </summary>
        public Byte[] HTTPBody
        {
            get
            {

                if (_HTTPBody == null)
                    TryReadHTTPBodyStream();

                return _HTTPBody;

            }
        }

        internal void ResizeBody(Int32 NewSize)
        {
            Array.Resize(ref _HTTPBody, NewSize);
        }

        #endregion

        #region HTTPBodyAsUTF8String

        /// <summary>
        /// The HTTP body/content as an UTF8 string.
        /// </summary>
        public String HTTPBodyAsUTF8String
        {
            get
            {

                try
                {

                    if (_HTTPBody == null)
                        TryReadHTTPBodyStream();

                    return _HTTPBody.ToUTF8String();

                }
                catch (Exception)
                {
                    return null;
                }

            }
        }

        #endregion

        #region HTTPBodyStream

        private Stream _HTTPBodyStream;

        /// <summary>
        /// The HTTP body as a stream of bytes.
        /// </summary>
        public Stream HTTPBodyStream
        {
            get
            {
                return _HTTPBodyStream;
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

        #region AHTTPPDU()

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        public AHTTPPDU()
        {

            this.Timestamp                  = DateTime.UtcNow;
            this._HeaderFields              = new Dictionary<String,          Object>(StringComparer.OrdinalIgnoreCase);
            this._HeaderFields2             = new Dictionary<HTTPHeaderField, Object>();
            this.HTTPBodyReceiveBufferSize  = DefaultHTTPBodyReceiveBufferSize;

        }

        #endregion

        #region AHTTPPDU(Timestamp, RemoteSocket, LocalSocket, HTTPHeader, HTTPBody = null, HTTPBodyStream = null, CancellationToken = null, EventTrackingId = null)

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        public AHTTPPDU(DateTime            Timestamp,
                        IPSocket            RemoteSocket,
                        IPSocket            LocalSocket,
                        String              HTTPHeader,
                        Byte[]              HTTPBody                    = null,
                        Stream              HTTPBodyStream              = null,
                        UInt32              HTTPBodyReceiveBufferSize   = DefaultHTTPBodyReceiveBufferSize,
                        CancellationToken?  CancellationToken           = null,
                        EventTracking_Id    EventTrackingId             = null)

            : this()

        {

            this.Timestamp                  = Timestamp;
            this.RemoteSocket               = RemoteSocket;
            this._LocalSocket               = LocalSocket;
            this.RawHTTPHeader              = HTTPHeader.Trim();
            this._HTTPBody                  = HTTPBody;
            this._HTTPBodyStream            = HTTPBodyStream;
            this.HTTPBodyReceiveBufferSize  = HTTPBodyReceiveBufferSize < MaxHTTPBodyReceiveBufferSize
                                                  ? HTTPBodyReceiveBufferSize
                                                  : DefaultHTTPBodyReceiveBufferSize;
            this.CancellationToken          = CancellationToken.HasValue ? CancellationToken.Value : new CancellationTokenSource().Token;
            this.EventTrackingId            = EventTrackingId;

            #region Process first line...

            var AllLines = HTTPHeader.Trim().Split(_LineSeparator, StringSplitOptions.RemoveEmptyEntries);

            FirstPDULine = AllLines.FirstOrDefault();
            if (FirstPDULine == null)
                throw new Exception("Bad request");

            #endregion

            #region ...process all other header lines

            String[] KeyValuePair = null;

            foreach (var Line in AllLines.Skip(1))
            {

                KeyValuePair = Line.Split(_ColonSeparator, 2, StringSplitOptions.RemoveEmptyEntries);

                // Not valid for every HTTP header... but at least for most...
                if (KeyValuePair.Length == 1)
                    _HeaderFields.Add(KeyValuePair[0].Trim(), String.Empty);

                else // KeyValuePair.Length == 2
                    _HeaderFields.Add(KeyValuePair[0].Trim(), KeyValuePair[1].Trim());

            }

            #endregion

        }

        #endregion

        #region AHTTPPDU(HTTPPDU)

        /// <summary>
        /// Creates a new HTTP header.
        /// </summary>
        /// <param name="HTTPPDU">Another HTTP PDU.</param>
        public AHTTPPDU(AHTTPPDU  HTTPPDU)

            : this()

        {

            this.Timestamp                  = HTTPPDU?.Timestamp         ?? DateTime.UtcNow;
            this.RemoteSocket               = HTTPPDU?.RemoteSocket;
            this._LocalSocket               = HTTPPDU?.LocalSocket;
            this.RawHTTPHeader              = HTTPPDU?.RawHTTPHeader;
            this.RawPDU                     = HTTPPDU?.RawPDU;
            this._HTTPBody                  = HTTPPDU?.HTTPBody;
            this._HTTPBodyStream            = HTTPPDU?.HTTPBodyStream;
            this.HTTPBodyReceiveBufferSize  = DefaultHTTPBodyReceiveBufferSize;
            this.CancellationToken          = HTTPPDU?.CancellationToken ?? new CancellationTokenSource().Token;
            this.EventTrackingId            = HTTPPDU?.EventTrackingId;

            this.FirstPDULine               = HTTPPDU?.FirstPDULine;

            if (HTTPPDU._HeaderFields != null)
                foreach (var field in HTTPPDU._HeaderFields)
                    _HeaderFields.Add(field.Key, field.Value);

            if (HTTPPDU._HeaderFields2 != null)
                foreach (var field in HTTPPDU._HeaderFields2)
                    _HeaderFields2.Add(field.Key, field.Value);

        }

        #endregion

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
            return _HeaderFields.TryGetValue(FieldName, out Value);
        }

        #endregion

        #region (protected) TryGetHeaderField(HeaderField)

        /// <summary>
        /// Return a http header field.
        /// </summary>
        /// <param name="HeaderField">The key of the requested header field.</param>
        /// <param name="Value">The value of the requested header field.</param>
        /// <returns>True if the requested header exists; false otherwise.</returns>
        protected Boolean TryGetHeaderField(HTTPHeaderField HeaderField, out Object Value)
        {
            return _HeaderFields.TryGetValue(HeaderField.Name, out Value);
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

            if (_HeaderFields.TryGetValue(Key, out _Object))
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
            if (_HeaderFields.TryGetValue(FieldName, out Value))
                return Value.ToString();

            return String.Empty;

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
            if (_HeaderFields.TryGetValue(FieldName, out Value))
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

            if (_HeaderFields.TryGetValue(HeaderField.Name, out Object Value))
                return Value.ToString();

            return String.Empty;

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
            if (_HeaderFields.TryGetValue(HeaderField.Name, out Value))
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
            if (_HeaderFields.TryGetValue(FieldName, out Value))
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
            if (_HeaderFields.TryGetValue(FieldName, out Value))
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
            if (_HeaderFields.TryGetValue(HeaderField.Name, out Value))
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


        #region (protected) RemoveHeaderField(FieldName)

        /// <summary>
        /// Remove a HTTP header field.
        /// </summary>
        /// <param name="FieldName">The name of the header field.</param>
        protected void RemoveHeaderField(String FieldName)
        {
            if (_HeaderFields.ContainsKey(FieldName))
                _HeaderFields.Remove(FieldName);
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

                if (_HeaderFields.ContainsKey(FieldName))
                    _HeaderFields[FieldName] = Value;
                else
                    _HeaderFields.Add(FieldName, Value);

            }

            else
                if (_HeaderFields.ContainsKey(FieldName))
                    _HeaderFields.Remove(FieldName);

        }

        #endregion

        #region (protected) SetHeaderField(HeaderField, Value)

        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="HeaderField">The HTTP header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        protected void SetHeaderField(HTTPHeaderField HeaderField, Object Value)
        {

            if (Value != null)
            {

                if (_HeaderFields.ContainsKey(HeaderField.Name))
                    _HeaderFields[HeaderField.Name] = Value;
                else
                    _HeaderFields.Add(HeaderField.Name, Value);

            }

            else
                if (_HeaderFields.ContainsKey(HeaderField.Name))
                    _HeaderFields.Remove(HeaderField.Name);


            // New collection...
            if (Value != null)
            {

                if (_HeaderFields2.ContainsKey(HeaderField))
                    _HeaderFields2[HeaderField] = Value;
                else
                    _HeaderFields2.Add(HeaderField, Value);

            }

            else
                if (_HeaderFields2.ContainsKey(HeaderField))
                _HeaderFields2.Remove(HeaderField);

        }

        #endregion


        #region IEnumerable<KeyValuePair<String, Object>> Members

        /// <summary>
        /// Return a HTTP header enumerator.
        /// </summary>
        public IEnumerator<KeyValuePair<String, Object>> GetEnumerator()
        {
            return _HeaderFields.GetEnumerator();
        }

        /// <summary>
        /// Return a HTTP header enumerator.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _HeaderFields.GetEnumerator();
        }

        #endregion


        public Boolean TryReadHTTPBodyStream()
        {

            if (_HTTPBody != null)
                return true;

            if (!ContentLength.HasValue ||
                 ContentLength.Value == 0)
            {
                _HTTPBody = new Byte[0];
                return true;
            }

               _HTTPBody   = new Byte[(Int32) ContentLength.Value];
            var Buffer     = new Byte[16*1024*1024]; //ToDo: Make the HTTP Body read buffer more flexible!
            var Read       = 0;
            var Position   = 0;

            do
            {

                try
                {

                    Read = _HTTPBodyStream.Read(Buffer, 0, Buffer.Length);

                    if (Read > 0)
                    {

                        if (Position + Read <= _HTTPBody.Length)
                        {
                            Array.Copy(Buffer, 0, _HTTPBody, Position, Read);
                            Position += Read;
                        }

                        else
                        {
                            Array.Copy(Buffer, 0, _HTTPBody, Position, _HTTPBody.Length - Position);
                            Position += Read;
                        }

                    }

                    if (Position >= _HTTPBody.Length)
                    {
                        this._HTTPBody = HTTPBody;
                        return true;
                    }

                    Thread.Sleep(10);

                }
                catch (IOException ex)
                {

                    // If the ReceiveTimeout is reached an IOException will be raised...
                    // with an InnerException of type SocketException and ErrorCode 10060
                    var socketExept = ex.InnerException as SocketException;

                    // If it's not the "expected" exception, let's not hide the error
                    if (socketExept == null || socketExept.ErrorCode != 10060)
                        throw;

                    // If it is the receive timeout, then reading ended
                    break;

                }
                catch (Exception e)
                {
                    DebugX.LogT(nameof(AHTTPPDU) + " could not read HTTP body (" + ContentLength.Value + " bytes): " + e.Message);
                    return false;
                }

            }
            while (Read > 0);

            Array.Resize(ref _HTTPBody, Position);
            return false;

        }


        #region NewContentStream()

        public MemoryStream NewContentStream()
        {

            var _MemoryStream = new MemoryStream();

            _HTTPBodyStream = _MemoryStream;

            return _MemoryStream;

        }

        #endregion

        #region ContentStreamToArray(DataStream = null)

        public void ContentStreamToArray(Stream DataStream = null)
        {

            if (DataStream == null)
                _HTTPBody = ((MemoryStream) HTTPBodyStream).ToArray();
            else
                _HTTPBody = ((MemoryStream) DataStream).ToArray();

        }

        #endregion


        public void Dispose()
        { }

    }

}

