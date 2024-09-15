/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A read-only HTTP response header.
    /// </summary>
    public partial class HTTPResponse : AHTTPPDU
    {

        /// <summary>
        /// A read-write HTTP response header.
        /// </summary>
        public class Builder : AHTTPPDUBuilder
        {

            #region Properties

            /// <summary>
            /// The optional HTTP sub protocol response, e.g. HTTP WebSocket.
            /// </summary>
            public Object?            SubprotocolResponse    { get; set; }

            /// <summary>
            /// The correlated HTTP request.
            /// </summary>
            public HTTPRequest?       HTTPRequest            { get; set; }

            /// <summary>
            /// The timestamp of the HTTP response.
            /// </summary>
            public DateTime           Timestamp              { get; set; }

            /// <summary>
            /// The cancellation token.
            /// </summary>
            public CancellationToken  CancellationToken      { get; set; }

            /// <summary>
            /// The runtime of the HTTP request/response pair.
            /// </summary>
            public TimeSpan?          Runtime                { get; set; }

            /// <summary>
            /// The entire HTTP header.
            /// </summary>
            public String             HTTPHeader

                => String.Concat(
                      (HTTPStatusCode ?? HTTPStatusCode.BadRequest).HTTPResponseString,
                       Environment.NewLine,
                       ConstructedHTTPHeader,
                       Environment.NewLine,
                       Environment.NewLine
                   );

            #endregion

            #region Response header fields

            #region Age

            /// <summary>
            /// Age
            /// </summary>
            public UInt64? Age
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.Age);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.Age, value);
                }

            }

            #endregion

            #region Allow

            /// <summary>
            /// Allow
            /// </summary>
            public IEnumerable<HTTPMethod> Allow
            {

                get
                {
                    return GetHeaderFields(HTTPResponseHeaderField.Allow);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.Allow, value);
                }

            }

            #endregion

            #region AcceptPatch

            /// <summary>
            /// Accept-Patch
            /// </summary>
            public IEnumerable<HTTPContentType> AcceptPatch
            {

                get
                {
                    return GetHeaderFields(HTTPResponseHeaderField.AcceptPatch);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.Allow, value);
                }

            }

            #endregion

            #region DAV

            /// <summary>
            /// DAV
            /// </summary>
            public String? DAV
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.DAV);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.DAV, value);
                }

            }

            #endregion

            #region ETag

            /// <summary>
            /// ETag
            /// </summary>
            public String? ETag
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.ETag);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.ETag, value);
                }

            }

            #endregion

            #region Expires

            /// <summary>
            /// Expires
            /// </summary>
            public String? Expires
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.Expires);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.Expires, value);
                }

            }

            #endregion

            #region Keep-Alive

            /// <summary>
            /// Keep-Alive
            /// </summary>
            public KeepAliveType? KeepAlive
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.KeepAlive);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.KeepAlive, value);
                }

            }

            #endregion

            #region Last-Modified

            /// <summary>
            /// Last-Modified
            /// </summary>
            public DateTime? LastModified
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.LastModified);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.LastModified, value);
                }

            }

            #endregion

            #region Location

            /// <summary>
            /// An absolute or relative HTTP Location.
            /// </summary>
            public Location? Location
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.Location);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.Location, value);
                }

            }

            #endregion

            #region X-LocationAfterAuth

            /// <summary>
            /// X-LocationAfterAuth
            /// </summary>
            public HTTPPath? XLocationAfterAuth
            {

                get
                {

                    var httpPath = GetHeaderField(HTTPResponseHeaderField.XLocationAfterAuth);

                    return httpPath is not null
                               ? HTTPPath.Parse(httpPath)
                               : null;

                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.XLocationAfterAuth, value);
                }

            }

            #endregion

            #region Proxy-Authenticate

            /// <summary>
            /// Proxy-Authenticate
            /// </summary>
            public String? ProxyAuthenticate
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.ProxyAuthenticate);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.ProxyAuthenticate, value);
                }

            }

            #endregion

            #region Retry-After

            /// <summary>
            /// Retry-After
            /// </summary>
            public String? RetryAfter
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.RetryAfter);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.RetryAfter, value);
                }

            }

            #endregion

            #region Server

            /// <summary>
            /// Server
            /// </summary>
            public String? Server
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.Server);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.Server, value);
                }

            }

            #endregion

            #region SetCookie

            /// <summary>
            /// Set-Cookie
            /// </summary>
            public HTTPCookies? SetCookie
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.SetCookie);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.SetCookie, value);
                }

            }

            #endregion

            #region Vary

            /// <summary>
            /// Vary
            /// </summary>
            public String? Vary
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.Vary);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.Vary, value);
                }

            }

            #endregion

            #region WWWAuthenticate

            /// <summary>
            /// WWW-Authenticate
            /// </summary>
            public WWWAuthenticate WWWAuthenticate
            {

                get
                {
                    return GetHeaderFields(HTTPResponseHeaderField.WWWAuthenticate);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.WWWAuthenticate, value);
                }

            }

            #endregion

            #region SecWebSocketAccept

            /// <summary>
            /// Sec-WebSocket-Accept
            /// </summary>
            public String? SecWebSocketAccept
            {

                get
                {
                    return GetHeaderField(HTTPResponseHeaderField.SecWebSocketAccept);
                }

                set
                {
                    SetHeaderField(HTTPResponseHeaderField.SecWebSocketAccept, value);
                }

            }

            #endregion

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new HTTP response.
            /// </summary>
            /// <param name="HTTPRequest">An optional HTTP request for this response.</param>
            public Builder(HTTPRequest? HTTPRequest = null)
            {

                this.HTTPRequest        = HTTPRequest;
                this.Timestamp          = Illias.Timestamp.Now;
                this.Date               = Illias.Timestamp.Now;
                this.ProtocolName       = "HTTP";
                this.ProtocolVersion    = new HTTPVersion(1, 1);
                this.CancellationToken  = HTTPRequest?.CancellationToken ?? CancellationToken.None;
                base.EventTrackingId    = HTTPRequest?.EventTrackingId   ?? EventTracking_Id.New;
                this.Runtime            = HTTPRequest is not null
                                              ? Illias.Timestamp.Now - HTTPRequest.Timestamp
                                              : TimeSpan.Zero;

            }

            #endregion


            #region Set(HeaderField, Value)

            /// <summary>
            /// Set a HTTP header field.
            /// A field value of NULL will remove the field from the header.
            /// </summary>
            /// <param name="HeaderField">The header field.</param>
            /// <param name="Value">The value. NULL will remove the field from the header.</param>
            public Builder Set(HTTPHeaderField HeaderField, Object Value)
            {

                base.SetHeaderField(HeaderField, Value);

                return this;

            }

            /// <summary>
            /// Set a HTTP header field.
            /// A field value of NULL will remove the field from the header.
            /// </summary>
            /// <param name="HeaderField">The header field.</param>
            /// <param name="Value">The value. NULL will remove the field from the header.</param>
            public Builder Set(String HeaderField, Object Value)
            {

                base.SetHeaderField(HeaderField, Value);

                return this;

            }

            #endregion


            #region (implicit operator) HTTPResponseBuilder => HTTPResponse

            /// <summary>
            /// An implicit conversion of a HTTPResponseBuilder into a HTTPResponse.
            /// </summary>
            /// <param name="HTTPResponseBuilder">A HTTP response builder.</param>
            public static implicit operator HTTPResponse(Builder HTTPResponseBuilder)
                => HTTPResponseBuilder.AsImmutable;

            #endregion


            #region Set non-http header fields

            #region SetHTTPStatusCode(HTTPStatusCode)

            /// <summary>
            /// Set the HTTP status code.
            /// </summary>
            /// <param name="HTTPStatusCode">A HTTP status code.</param>
            public Builder SetHTTPStatusCode(HTTPStatusCode HTTPStatusCode)
            {
                this.HTTPStatusCode = HTTPStatusCode;
                return this;
            }

            #endregion

            #region SetProtocolName(ProtocolName)

            /// <summary>
            /// Set the protocol name.
            /// </summary>
            /// <param name="ProtocolName">The protocol name.</param>
            public Builder SetProtocolName(String ProtocolName)
            {
                this.ProtocolName = ProtocolName;
                return this;
            }

            #endregion

            #region SetProtocolVersion(ProtocolVersion)

            /// <summary>
            /// Set the protocol version.
            /// </summary>
            /// <param name="ProtocolVersion">The protocol version.</param>
            public Builder SetProtocolVersion(HTTPVersion ProtocolVersion)
            {
                this.ProtocolVersion = ProtocolVersion;
                return this;
            }

            #endregion

            #region SetContent(...)

            #region SetContent(ByteArray)

            /// <summary>
            /// The HTTP content/body.
            /// </summary>
            /// <param name="ByteArray">The HTTP content/body.</param>
            public Builder SetContent(Byte[] ByteArray)
            {
                this.Content = ByteArray;
                return this;
            }

            #endregion

            #region SetContent(String)

            /// <summary>
            /// The HTTP content/body.
            /// </summary>
            /// <param name="String">The HTTP content/body.</param>
            public Builder SetContent(String String)
            {
                this.Content = String.ToUTF8Bytes();
                return this;
            }

            #endregion

            #endregion

            #region SetContentStream(ContentStream)

            /// <summary>
            /// The HTTP content/body as a stream.
            /// </summary>
            /// <param name="ContentStream">The HTTP content/body as a stream.</param>
            public Builder SetContent(Stream ContentStream)
            {
                this.ContentStream = ContentStream;
                return this;
            }

            #endregion

            #endregion

            #region Set general header fields

            #region SetCacheControl(CacheControl)

            /// <summary>
            /// Set the HTTP CacheControl.
            /// </summary>
            /// <param name="CacheControl">CacheControl.</param>
            public Builder SetCacheControl(String CacheControl)
            {
                this.CacheControl = CacheControl;
                return this;
            }

            #endregion

            #region SetConnection(Connection)

            /// <summary>
            /// Set the HTTP connection.
            /// </summary>
            /// <param name="Connection">A connection.</param>
            public Builder SetConnection(String Connection)
            {
                this.Connection = Connection;
                return this;
            }

            #endregion

            #region SetContentEncoding(ContentEncoding)

            /// <summary>
            /// Set the HTTP Content-Encoding.
            /// </summary>
            /// <param name="ContentEncoding">The encoding of the HTTP content/body.</param>
            public Builder SetContentEncoding(Encoding ContentEncoding)
            {
                this.ContentEncoding = ContentEncoding;
                return this;
            }

            #endregion

            #region SetContentLanguage(ContentLanguages)

            /// <summary>
            /// Set the HTTP Content-Languages.
            /// </summary>
            /// <param name="ContentLanguages">The languages of the HTTP content/body.</param>
            public Builder SetContentLanguage(String[] ContentLanguages)
            {
                this.ContentLanguage = ContentLanguages;
                return this;
            }

            #endregion

            #region SetContentLength(ContentLength)

            /// <summary>
            /// Set the HTTP Content-Length.
            /// </summary>
            /// <param name="ContentLength">The length of the HTTP content/body.</param>
            public Builder SetContentLength(UInt64? ContentLength)
            {
                this.ContentLength = ContentLength;
                return this;
            }

            #endregion

            #region SetContentLocation(ContentLocation)

            /// <summary>
            /// Set the HTTP ContentLocation.
            /// </summary>
            /// <param name="ContentLocation">ContentLocation.</param>
            public Builder SetContentLocation(String ContentLocation)
            {
                this.ContentLocation = ContentLocation;
                return this;
            }

            #endregion

            #region SetContentMD5(ContentMD5)

            /// <summary>
            /// Set the HTTP ContentMD5.
            /// </summary>
            /// <param name="ContentMD5">ContentMD5.</param>
            public Builder SetContentMD5(String ContentMD5)
            {
                this.ContentMD5 = ContentMD5;
                return this;
            }

            #endregion

            #region SetContentRange(ContentRange)

            /// <summary>
            /// Set the HTTP ContentRange.
            /// </summary>
            /// <param name="ContentRange">ContentRange.</param>
            public Builder SetContentRange(String ContentRange)
            {
                this.ContentRange = ContentRange;
                return this;
            }

            #endregion

            #region SetContentType(ContentType)

            /// <summary>
            /// Set the HTTP Content-Type.
            /// </summary>
            /// <param name="ContentType">The type of the HTTP content/body.</param>
            public Builder SetContentType(HTTPContentType ContentType)
            {
                this.ContentType = ContentType;
                return this;
            }

            #endregion

            #region SetDate(Date)

            /// <summary>
            /// Set the HTTP Date.
            /// </summary>
            /// <param name="Date">DateTime.</param>
            public Builder SetDate(DateTime Date)
            {
                this.Date = Date;
                return this;
            }

            #endregion

            #region SetVia(Via)

            /// <summary>
            /// Set the HTTP Via.
            /// </summary>
            /// <param name="Via">Via.</param>
            public Builder SetVia(String Via)
            {
                this.Via = Via;
                return this;
            }

            #endregion

            #endregion


            #region (static) ClientError        (Request, Configurator = null)

            /// <summary>
            /// Create a new 0-ClientError HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder ClientError(HTTPRequest       Request,
                                              Action<Builder>?  Configurator = null)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.ClientError
                               };

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 0-ClientError HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder ClientError(HTTPRequest             Request,
                                              Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.ClientError
                               };

                return Configurator is not null
                           ? Configurator(response)
                           : response;

            }

            #endregion


            #region (static) OK                 (Request, Configurator = null)

            /// <summary>
            /// Create a new 200-OK HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder OK(HTTPRequest       Request,
                                     Action<Builder>?  Configurator = null)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.OK
                               };

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 200-OK HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder OK(HTTPRequest             Request,
                                     Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.OK
                               };

                return Configurator is not null
                           ? Configurator(response)
                           : response;

            }

            #endregion

            #region (static) BadRequest         (Request, Configurator = null, CloseConnection = true)

            /// <summary>
            /// Create a new 400-BadRequest HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            /// <param name="CloseConnection">Whether to close to connection (default: true)</param>
            public static Builder BadRequest(HTTPRequest       Request,
                                             Action<Builder>?  Configurator      = null,
                                             Boolean?          CloseConnection   = true)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.BadRequest
                               };

                if (CloseConnection ?? true)
                    response.Connection = "close";

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 400-BadRequest HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            /// <param name="CloseConnection">Whether to close to connection (default: true)</param>
            public static Builder BadRequest(HTTPRequest             Request,
                                             Func<Builder, Builder>  Configurator,
                                             Boolean?                CloseConnection   = true)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.BadRequest
                               };

                if (CloseConnection ?? true)
                    response.Connection = "close";

                return Configurator is not null
                           ? Configurator(response)
                           : response;

            }

            #endregion

            #region (static) ServiceUnavailable (Request, Configurator = null)

            /// <summary>
            /// Create a new 503-ServiceUnavailable HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder ServiceUnavailable(HTTPRequest       Request,
                                                     Action<Builder>?  Configurator = null)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.ServiceUnavailable
                               };

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 503-ServiceUnavailable HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder ServiceUnavailable(HTTPRequest             Request,
                                                     Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.ServiceUnavailable
                               };

                return Configurator is not null
                           ? Configurator(response)
                           : response;

            }

            #endregion

            #region (static) FailedDependency   (Request, Configurator = null)

            /// <summary>
            /// Create a new 424-FailedDependency HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder FailedDependency(HTTPRequest       Request,
                                                   Action<Builder>?  Configurator = null)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.FailedDependency
                               };

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 424-FailedDependency HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder FailedDependency(HTTPRequest             Request,
                                                   Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.FailedDependency
                               };

                return Configurator is not null
                           ? Configurator(response)
                           : response;

            }

            #endregion

            #region (static) GatewayTimeout     (Request, Configurator = null)

            /// <summary>
            /// Create a new 504-GatewayTimeout HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder GatewayTimeout(HTTPRequest       Request,
                                                 Action<Builder>?  Configurator = null)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.GatewayTimeout
                               };

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 504-GatewayTimeout HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder GatewayTimeout(HTTPRequest             Request,
                                                 Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request) {
                                   HTTPStatusCode = HTTPStatusCode.GatewayTimeout
                               };

                return Configurator is not null
                           ? Configurator(response)
                           : response;

            }

            #endregion



            #region SetHeaderField(FieldName,   Value)

            /// <summary>
            /// Set a HTTP header field.
            /// A field value of NULL will remove the field from the header.
            /// </summary>
            /// <param name="FieldName">The name of the header field.</param>
            /// <param name="Value">The value. NULL will remove the field from the header.</param>
            public new Builder SetHeaderField(String   FieldName,
                                              Object?  Value)
            {

                base.SetHeaderField(FieldName,
                                    Value);

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
            public new Builder SetHeaderField(HTTPHeaderField  HeaderField,
                                              Object?          Value)
            {

                base.SetHeaderField(HeaderField,
                                    Value);

                return this;

            }


            /// <summary>
            /// Set a HTTP header field.
            /// A field value of NULL will remove the field from the header.
            /// </summary>
            /// <param name="HeaderField">The header field.</param>
            /// <param name="Value">The value. NULL will remove the field from the header.</param>
            public new Builder SetHeaderField<T>(HTTPHeaderField<T> HeaderField, Object? Value)
            {

                base.SetHeaderField(HeaderField,
                                    Value);

                return this;

            }

            #endregion


            #region RemoveHeaderField(FieldName)

            /// <summary>
            /// Remove a HTTP header field.
            /// </summary>
            /// <param name="FieldName">The name of the header field.</param>
            public new Builder RemoveHeaderField(String FieldName)
            {

                base.RemoveHeaderField(FieldName);

                return this;

            }

            #endregion

            #region RemoveHeaderField(FieldName)

            /// <summary>
            /// Remove a HTTP header field.
            /// </summary>
            /// <param name="HeaderField">The header field.</param>
            public new Builder RemoveHeaderField(HTTPHeaderField HeaderField)
            {

                base.RemoveHeaderField(HeaderField);

                return this;

            }

            #endregion


            #region PrepareImmutability()

            /// <summary>
            /// Prepares the immutability of an HTTP PDU, e.g. calculates
            /// and set the Content-Length header.
            /// </summary>
            protected override void PrepareImmutability()
            {
                base.PrepareImmutability();
            }

            #endregion

            #region AsImmutable

            /// <summary>
            /// Converts this HTTPResponseBuilder into an immutable HTTPResponse.
            /// </summary>
            public HTTPResponse AsImmutable
            {
                get
                {

                    // Update response timestamp
                    Date = Illias.Timestamp.Now;

                    PrepareImmutability();

                    if      (Content       is not null)
                        return Parse(HTTPHeader, Content,       HTTPRequest, SubprotocolResponse);

                    else if (ContentStream is not null)
                        return Parse(HTTPHeader, ContentStream, HTTPRequest, SubprotocolResponse);

                    else
                        return Parse(HTTPHeader,                HTTPRequest, SubprotocolResponse);

                }
            }

            #endregion


        }

    }

}
