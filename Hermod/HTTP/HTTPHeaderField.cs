/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    #region (enum)  HeaderFieldType

    /// <summary>
    /// The type of a HTTP header field.
    /// </summary>
    public enum HeaderFieldType
    {
        General,
        Request,
        Response
    }

    #endregion

    #region (enum)  RequestPathSemantic

    /// <summary>
    /// Whether a header field has and end-to-end or
    /// an hop-to-hop semantic.
    /// </summary>
    public enum RequestPathSemantic
    {
        EndToEnd,
        HopToHop,
        both
    }

    #endregion

    #region (class) Common string parsers

    /// <summary>
    /// A collection of delegates to parse the value of the header field from a string.
    /// </summary>
    public static class StringParsers
    {

        #region NullableUInt64(String, out Object)

        /// <summary>
        /// A delegate to parse a UInt64? value from a string.
        /// </summary>
        /// <param name="String">The string to be parsed.</param>
        /// <param name="Object">The parsed UInt64? value.</param>
        /// <returns>True if the value could be parsed; False otherwise.</returns>
        public static Boolean NullableUInt64(String String, out Object Object)
        {

            UInt64 Value;

            if (UInt64.TryParse(String, out Value))
            {
                Object = Value;
                return true;
            }

            Object = null;
            return false;

        }

        #endregion

    }

    #endregion

    #region HTTPHeaderField

    /// <summary>
    /// Defines a field within the HTTP header.
    /// </summary>
    /// <seealso cref="http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html"/>
    /// <seealso cref="http://restpatterns.org"/>
    /// <seealso cref="http://en.wikipedia.org/wiki/List_of_HTTP_header_fields"/>
    /// <seealso cref="http://www.and.org/texts/server-http"/>
    /// <seealso cref="http://www.iana.org/assignments/message-headers/message-headers.xhtml"/>
    public class HTTPHeaderField : IEquatable<HTTPHeaderField>, IComparable<HTTPHeaderField>, IComparable
    {

        #region Properties

        #region Name

        /// <summary>
        /// The name of this HTTP request field
        /// </summary>
        public String Name { get; }

        #endregion

        #region Type

        /// <summary>
        /// The C# type of this HTTP header field.
        /// </summary>
        public Type Type { get; }

        #endregion

        #region HeaderFieldType

        /// <summary>
        /// The type of a HTTP header field.
        /// </summary>
        public HeaderFieldType HeaderFieldType { get; }

        #endregion

        #region RequestPathSemantic

        /// <summary>
        /// Whether a header field has and end-to-end or
        /// an hop-to-hop semantic.
        /// </summary>
        public RequestPathSemantic RequestPathSemantic { get; }

        #endregion

        #region StringParser

        /// <summary>
        /// A delegate definition to parse the value of the header field from a string.
        /// </summary>
        public delegate Boolean StringParserDelegate(String arg1, out Object arg2);

        /// <summary>
        /// A delegate to parse the value of the header field from a string.
        /// </summary>
        public StringParserDelegate StringParser { get; }

        #endregion

        #region ValueSerializer

        /// <summary>
        /// A delegate definition to serialize the value of the header field to a string.
        /// </summary>
        public delegate String ValueSerializerDelegate(Object arg1);

        /// <summary>
        /// A delegate to serialize the value of the header field to a string.
        /// </summary>
        public ValueSerializerDelegate ValueSerializer { get; }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new HTTP header field.
        /// </summary>
        /// <param name="Name">The name of the HTTP header field.</param>
        /// <param name="Type">The type of the HTTP header field value.</param>
        /// <param name="HeaderFieldType">The type of the header field (general|request|response).</param>
        /// <param name="RequestPathSemantic">Whether a header field has and end-to-end or an hop-to-hop semantic.</param>
        /// <param name="StringParser">Parse this HTTPHeaderField from a string.</param>
        /// <param name="ValueSerializer">A delegate to serialize the value of the header field to a string.</param>
        public HTTPHeaderField(String                   Name,
                               Type                     Type,
                               HeaderFieldType          HeaderFieldType,
                               RequestPathSemantic      RequestPathSemantic,
                               StringParserDelegate     StringParser    = null,
                               ValueSerializerDelegate  ValueSerializer = null)

        {

            #region Initial checks

            if (Name.IsNullOrEmpty())
                throw new ArgumentNullException("Name",  "The given name of the header field must not be null or its length zero!");

            if (Type == null)
                throw new ArgumentNullException("Type",  "The given type of the header field value must not be null or its length zero!");

            #endregion

            this.Name                 = Name;
            this.Type                 = Type;
            this.HeaderFieldType      = HeaderFieldType;
            this.RequestPathSemantic  = RequestPathSemantic;
            this.StringParser         = StringParser    ?? ((String s, out Object o) => { o = s; return true; });
            this.ValueSerializer      = ValueSerializer ?? ((o)                      => o == null ? "" : o.ToString());

        }

        #endregion


        #region General header fields

        #region CacheControl

        /// <summary>
        /// The Cache-Control general-header field is used to specify
        /// directives that MUST be obeyed by all caching mechanisms
        /// along the request/response chain. The directives specify
        /// behavior intended to prevent caches from adversely
        /// interfering with the request or response.
        /// </summary>
        /// <example>Cache-Control: no-cache</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField CacheControl = new HTTPHeaderField("Cache-Control",
                                                                                  typeof(String),
                                                                                  HeaderFieldType.General,
                                                                                  RequestPathSemantic.HopToHop);

        #endregion

        #region Connection

        /// <summary>
        /// The Connection general-header field allows the sender
        /// to specify options that are desired for that particular
        /// connection and MUST NOT be communicated by proxies over
        /// further connections.
        /// HTTP/1.1 applications that do not support persistent
        /// connections MUST include the "close" connection option
        /// in every message.
        /// </summary>
        /// <example>Connection: close</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Connection = new HTTPHeaderField("Connection",
                                                                                typeof(String),
                                                                                HeaderFieldType.General,
                                                                                RequestPathSemantic.EndToEnd);

        #endregion

        #region ContentEncoding

        /// <summary>
        /// The Content-Encoding entity-header field is used as a modifier
        /// to the media-type. When present, its value indicates what
        /// additional content codings have been applied to the entity-body,
        /// and thus what decoding mechanisms must be applied in order to
        /// obtain the media-type referenced by the Content-Type header
        /// field.
        /// If the content-encoding of an entity in a request message is not
        /// acceptable to the origin server, the server SHOULD respond with
        /// a status code of 415 (Unsupported Media Type).
        /// </summary>
        /// <example>Content-Encoding: gzip</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ContentEncoding = new HTTPHeaderField("Content-Encoding",
                                                                                     typeof(String),
                                                                                     HeaderFieldType.General,
                                                                                     RequestPathSemantic.EndToEnd);

        #endregion

        #region ContentLanguage

        /// <summary>
        /// The Content-Language entity-header field describes the natural
        /// language(s) of the intended audience for the enclosed entity.
        /// </summary>
        /// <example>Content-Language: en, de</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ContentLanguage = new HTTPHeaderField("Content-Language",
                                                                                     typeof(String),
                                                                                     HeaderFieldType.General,
                                                                                     RequestPathSemantic.EndToEnd);

        #endregion

        #region ContentLength

        /// <summary>
        /// The Content-Length entity-header field indicates the size of
        /// the entity-body, in decimal number of OCTETs, sent to the
        /// recipient or, in the case of the HEAD method, the size of the
        /// entity-body that would have been sent if the request had been
        /// a GET request.
        /// </summary>
        /// <example>Content-Length: 3495</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ContentLength = new HTTPHeaderField("Content-Length",
                                                                                   typeof(UInt64?),
                                                                                   HeaderFieldType.General,
                                                                                   RequestPathSemantic.EndToEnd,
                                                                                   StringParsers.NullableUInt64);

        #endregion

        #region ContentLocation

        /// <summary>
        /// The Content-Location entity-header field MAY be used to supply
        /// the resource location for the entity enclosed in the message
        /// when that entity is accessible from a location separate from
        /// the requested resource's URI.
        /// If the Content-Location is a relative URI, the relative URI is
        /// interpreted relative to the Request-URI. 
        /// </summary>
        /// <example>Content-Location: ../test.html</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ContentLocation = new HTTPHeaderField("Content-Location",
                                                                                     typeof(String),
                                                                                     HeaderFieldType.General,
                                                                                     RequestPathSemantic.EndToEnd);

        #endregion

        #region ContentMD5

        /// <summary>
        /// The Content-MD5 entity-header field, is an MD5 digest of the
        /// entity-body for the purpose of providing an end-to-end
        /// message integrity check (MIC) of the entity-body.
        /// Note: a MIC is good for detecting accidental modification
        /// of the entity-body in transit, but is not proof against
        /// malicious attacks.
        /// </summary>
        /// <example>Content-MD5: Q2hlY2sgSW50ZWdyaXR5IQ==</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        /// <seealso cref="http://tools.ietf.org/html/rfc1864"/>
        public static readonly HTTPHeaderField ContentMD5 = new HTTPHeaderField("Content-MD5",
                                                                                typeof(String),
                                                                                HeaderFieldType.General,
                                                                                RequestPathSemantic.EndToEnd);

        #endregion

        #region ContentRange

        /// <summary>
        /// The Content-Range entity-header is sent with a partial
        /// entity-body to specify where in the full entity-body the
        /// partial body should be applied.
        /// The header SHOULD indicate the total length of the full
        /// entity-body, unless this length is unknown or difficult
        /// to determine. The asterisk "*" character means that the
        /// instance-length is unknown at the time when the response
        /// was generated.
        /// Partinal content replies must be sent using the response
        /// code 206 (Partial content). When an HTTP message includes
        /// the content of multiple ranges, these are transmitted as a
        /// multipart message using the media type "multipart/byteranges".
        /// Syntactically invalid content-range reqeuests, SHOULD be
        /// treated as if the invalid Range header field did not exist.
        /// Normally, this means return a 200 response containing the
        /// full entity.
        /// If the server receives a request (other than one including
        /// an If-Range request-header field) with an unsatisfiable
        /// Range request-header field, it SHOULD return a response
        /// code of 416 (Requested range not satisfiable).
        /// </summary>
        /// <example>Content-Range: bytes 21010-47021/47022</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ContentRange = new HTTPHeaderField("Content-Range",
                                                                                  typeof(String),
                                                                                  HeaderFieldType.General,
                                                                                  RequestPathSemantic.EndToEnd);

        #endregion

        #region ContentType

        /// <summary>
        /// The Content-Type entity-header field indicates the media
        /// type of the entity-body sent to the recipient or, in the
        /// case of the HEAD method, the media type that would have
        /// been sent had the request been a GET.
        /// </summary>
        /// <example>Content-Type: text/html; charset=ISO-8859-4</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ContentType = new HTTPHeaderField("Content-Type",
                                                                                 typeof(HTTPContentType),
                                                                                 HeaderFieldType.General,
                                                                                 RequestPathSemantic.EndToEnd);

        #endregion

        #region ContentDisposition

        /// <summary>
        /// The Content-Disposition response header field is used to convey
        /// additional information about how to process the response payload, and
        /// also can be used to attach additional metadata, such as the filename
        /// to use when saving the response payload locally.
        /// </summary>
        /// <example>Content-Disposition: attachment; filename="filename.jpg"</example>
        /// <seealso cref="https://tools.ietf.org/html/rfc6266"/>
        public static readonly HTTPHeaderField ContentDisposition = new HTTPHeaderField("Content-Disposition",
                                                                                        typeof(String),
                                                                                        HeaderFieldType.General,
                                                                                        RequestPathSemantic.EndToEnd);

        #endregion

        #region Date

        /// <summary>
        /// The Date general-header field represents the date and time
        /// at which the message was originated, having the same semantics
        /// as orig-date in RFC 822. The field value is an HTTP-date, as
        /// described in section 3.3.1; it MUST be sent in RFC 1123 [8]-date
        /// format.
        /// 
        /// Origin servers MUST include a Date header field in all responses,
        /// except in these cases:
        /// 
        /// If the response status code is 100 (Continue) or 101 (Switching
        /// Protocols), the response MAY include a Date header field, at the
        /// server's option.
        /// 
        /// If the response status code conveys a server error, e.g. 500
        /// (Internal Server Error) or 503 (Service Unavailable), and it is
        /// inconvenient or impossible to generate a valid Date.
        /// 
        /// If the server does not have a clock that can provide a reasonable
        /// approximation of the current time, its responses MUST NOT include
        /// a Date header field. In this case, the rules in section 14.18.1
        /// MUST be followed.
        /// 
        /// A received message that does not have a Date header field MUST be
        /// assigned one by the recipient if the message will be cached by that
        /// recipient or gatewayed via a protocol which requires a Date. An HTTP
        /// implementation without a clock MUST NOT cache responses without
        /// revalidating them on every use. An HTTP cache, especially a shared
        /// cache, SHOULD use a mechanism, such as NTP [28], to synchronize its
        /// clock with a reliable external standard.
        /// 
        /// Clients SHOULD only send a Date header field in messages that include
        /// an entity-body, as in the case of the PUT and POST requests, and even
        /// then it is optional. A client without a clock MUST NOT send a Date
        /// header field in a request.
        /// 
        /// The HTTP-date sent in a Date header SHOULD NOT represent a date and
        /// time subsequent to the generation of the message. It SHOULD represent
        /// the best available approximation of the date and time of message
        /// generation, unless the implementation has no means of generating a
        /// reasonably accurate date and time. In theory, the date ought to
        /// represent the moment just before the entity is generated. In practice,
        /// the date can be generated at any time during the message origination
        /// without affecting its semantic value.
        /// 
        /// Clockless Origin Server Operation
        /// Some origin server implementations might not have a clock available.
        /// An origin server without a clock MUST NOT assign Expires or Last-Modified
        /// values to a response, unless these values were associated with the
        /// resource by a system or user with a reliable clock. It MAY assign an
        /// Expires value that is known, at or before server configuration time,
        /// to be in the past (this allows "pre-expiration" of responses without
        /// storing separate Expires values for each resource).
        /// </summary>
        /// <example>Date: Tue, 15 Nov 1994 08:12:31 GMT</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Date = new HTTPHeaderField("Date",
                                                                          typeof(DateTime),
                                                                          HeaderFieldType.General,
                                                                          RequestPathSemantic.EndToEnd);

        #endregion

        #region Pragma

        /// <summary>
        /// The Pragma general-header field is used to include implementation-
        /// specific directives that might apply to any recipient along the
        /// request/response chain. All pragma directives specify optional
        /// behavior from the viewpoint of the protocol; however, some systems
        /// MAY require that behavior be consistent with the directives.
        /// 
        /// When the no-cache directive is present in a request message, an
        /// application SHOULD forward the request toward the origin server
        /// even if it has a cached copy of what is being requested. This
        /// pragma directive has the same semantics as the no-cache cache-
        /// directive (see section 14.9) and is defined here for backward
        /// compatibility with HTTP/1.0. Clients SHOULD include both header
        /// fields when a no-cache request is sent to a server not known to
        /// be HTTP/1.1 compliant. 
        /// 
        /// Pragma directives MUST be passed through by a proxy or gateway
        /// application, regardless of their significance to that application,
        /// since the directives might be applicable to all recipients along
        /// the request/response chain. It is not possible to specify a pragma
        /// for a specific recipient; however, any pragma directive not relevant
        /// to a recipient SHOULD be ignored by that recipient.
        /// 
        /// HTTP/1.1 caches SHOULD treat "Pragma: no-cache" as if the client
        /// had sent "Cache-Control: no-cache". No new Pragma directives will
        /// be defined in HTTP. 
        /// </summary>
        /// <example>Pragma: no-cache</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Pragma = new HTTPHeaderField("Pragma",
                                                                            typeof(String),
                                                                            HeaderFieldType.General,
                                                                            RequestPathSemantic.both);

        #endregion

        #region Trailer

        /// <summary>
        /// The Trailer general field value indicates that the given
        /// set of header fields is present in the trailer of a
        /// message encoded with chunked transfer-coding.
        /// 
        /// An HTTP/1.1 message SHOULD include a Trailer header field
        /// in a message using chunked transfer-coding with a non-empty
        /// trailer. Doing so allows the recipient to know which header
        /// fields to expect in the trailer.
        /// 
        /// If no Trailer header field is present, the trailer SHOULD NOT
        /// include any header fields. See section 3.6.1 for restrictions
        /// on the use of trailer fields in a "chunked" transfer-coding.
        /// 
        /// Message header fields listed in the Trailer header field MUST
        /// NOT include the following header fields:
        ///   - Transfer-Encoding
        ///   - Content-Length
        ///   - Trailer
        /// </summary>
        /// <example>Trailer : Max-Forwards</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Trailer = new HTTPHeaderField("Trailer",
                                                                             typeof(String),
                                                                             HeaderFieldType.General,
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

        #region Via

        /// <summary>
        /// The Via general-header field MUST be used by gateways
        /// and proxies to indicate the intermediate protocols and
        /// recipients between the user agent and the server on
        /// requests, and between the origin server and the client
        /// on responses.
        /// </summary>
        /// <example>Via: 1.0 fred, 1.1 nowhere.com (Apache/1.1)</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Via = new HTTPHeaderField("Via",
                                                                         typeof(String),
                                                                         HeaderFieldType.General,
                                                                         RequestPathSemantic.HopToHop);

        #endregion

        #region Transfer-Encoding

        /// <summary>
        /// The Transfer-Encoding general-header field indicates what (if any)
        /// type of transformation has been applied to the message body in
        /// order to safely transfer it between the sender and the recipient.
        /// This differs from the content-coding in that the transfer-coding
        /// is a property of the message, not of the entity. 
        /// 
        /// If multiple encodings have been applied to an entity, the transfer-
        /// codings MUST be listed in the order in which they were applied.
        /// Additional information about the encoding parameters MAY be
        /// provided by other entity-header fields not defined by this
        /// specification. 
        /// </summary>
        /// <example>Transfer-Encoding: chunked</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField TransferEncoding = new HTTPHeaderField("Transfer-Encoding",
                                                                                      typeof(String),
                                                                                      HeaderFieldType.General,
                                                                                      RequestPathSemantic.EndToEnd);

        #endregion

        #region Upgrade

        /// <summary>
        /// The Upgrade general-header allows the client to specify what
        /// additional communication protocols it supports and would like
        /// to use if the server finds it appropriate to switch protocols.
        /// The server MUST use the Upgrade header field within a 101
        /// (Switching Protocols) response to indicate which protocol(s)
        /// are being switched.
        /// 
        /// The Upgrade header field is intended to provide a simple
        /// mechanism for transition from HTTP/1.1 to some other,
        /// incompatible protocol. It does so by allowing the client to
        /// advertise its desire to use another protocol, such as a later
        /// version of HTTP with a higher major version number, even though
        /// the current request has been made using HTTP/1.1. This eases
        /// the difficult transition between incompatible protocols by
        /// allowing the client to initiate a request in the more commonly
        /// supported protocol while indicating to the server that it would
        /// like to use a "better" protocol if available (where "better"
        /// is determined by the server, possibly according to the nature
        /// of the method and/or resource being requested).
        /// 
        /// The Upgrade header field only applies to switching application-
        /// layer protocols upon the existing transport-layer connection.
        /// Upgrade cannot be used to insist on a protocol change; its
        /// acceptance and use by the server is optional. The capabilities
        /// and nature of the application-layer communication after the
        /// protocol change is entirely dependent upon the new protocol
        /// chosen, although the first action after changing the protocol
        /// MUST be a response to the initial HTTP request containing the
        /// Upgrade header field.
        /// 
        /// The Upgrade header field only applies to the immediate
        /// connection. Therefore, the upgrade keyword MUST be supplied
        /// within a Connection header field (section 14.10) whenever
        /// Upgrade is present in an HTTP/1.1 message.
        /// 
        /// The Upgrade header field cannot be used to indicate a switch
        /// to a protocol on a different connection. For that purpose, it
        /// is more appropriate to use a 301, 302, 303, or 305 redirection
        /// response.
        /// 
        /// This specification only defines the protocol name "HTTP" for
        /// use by the family of Hypertext Transfer Protocols, as defined
        /// by the HTTP version rules of section 3.1 and future updates
        /// to this specification. Any token can be used as a protocol
        /// name; however, it will only be useful if both the client and
        /// server associate the name with the same protocol.
        /// </summary>
        /// <example>Upgrade: HTTP/2.0, SHTTP/1.3, IRC/6.9, RTA/x11</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Upgrade = new HTTPHeaderField("Upgrade",
                                                                             typeof(String),
                                                                             HeaderFieldType.General,
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

        #region SecWebSocketKey

        /// <summary>
        /// Sec-Web-SocketKey.
        /// </summary>
        public static readonly HTTPHeaderField SecWebSocketKey = new HTTPHeaderField("Sec-Web-SocketKey",
                                                                                     typeof(String),
                                                                                     HeaderFieldType.General,
                                                                                     RequestPathSemantic.EndToEnd);

        #endregion

        #region SecWebSocketProtocol

        /// <summary>
        /// Sec-Web-SocketProtocol.
        /// </summary>
        public static readonly HTTPHeaderField SecWebSocketProtocol = new HTTPHeaderField("Sec-Web-SocketProtocol",
                                                                                          typeof(String),
                                                                                          HeaderFieldType.General,
                                                                                          RequestPathSemantic.EndToEnd);

        #endregion

        #region SecWebSocketVersion

        /// <summary>
        /// Sec-WebSocket-Version.
        /// </summary>
        public static readonly HTTPHeaderField SecWebSocketVersion = new HTTPHeaderField("Sec-WebSocket-Version",
                                                                                         typeof(String),
                                                                                         HeaderFieldType.General,
                                                                                         RequestPathSemantic.EndToEnd);

        #endregion

        #region SecWebSocketAccept

        /// <summary>
        /// Sec-WebSocket-Accept.
        /// </summary>
        public static readonly HTTPHeaderField SecWebSocketAccept = new HTTPHeaderField("Sec-WebSocket-Accept",
                                                                                        typeof(String),
                                                                                        HeaderFieldType.General,
                                                                                        RequestPathSemantic.EndToEnd);

        #endregion

        #endregion

        #region Request header fields

        #region Accept

        /// <summary>
        /// The Accept request-header field can be used to specify certain
        /// media types which are acceptable for the response. Accept
        /// headers can be used to indicate that the request is specifically
        /// limited to a small set of desired types, as in the case of a
        /// request for an in-line image.
        /// 
        /// The asterisk '*' character is used to group media types into
        /// ranges, with '*/*' indicating all media types and 'type/*'
        /// indicating all subtypes of that type. The media-range MAY include
        /// media type parameters that are applicable to that range.
        /// 
        /// Each media-range MAY be followed by one or more accept-params,
        /// beginning with the 'q' parameter for indicating a relative quality
        /// factor. The first 'q' parameter (if any) separates the media-range
        /// parameter(s) from the accept-params. Quality factors allow the user
        /// or user agent to indicate the relative degree of preference for
        /// that media-range, using the qvalue scale from 0 to 1 (section 3.9).
        /// The default value is q=1.
        /// 
        /// Note: Use of the 'q' parameter name to separate media type
        /// parameters from Accept extension parameters is due to historical
        /// practice. Although this prevents any media type parameter named
        /// 'q' from being used with a media range, such an event is believed
        /// to be unlikely given the lack of any 'q' parameters in the IANA
        /// media type registry and the rare usage of any media type parameters
        /// in Accept. Future media types are discouraged from registering any
        /// parameter named 'q'.
        /// 
        /// The example Accept: audio/*; q=0.2, audio/basic
        /// SHOULD be interpreted as 'I prefer audio/basic, but send me any
        /// audio type if it is the best available after an 80% mark-down in
        /// quality.'
        /// 
        /// If no Accept header field is present, then it is assumed that the
        /// client accepts all media types. If an Accept header field is present,
        /// and if the server cannot send a response which is acceptable
        /// according to the combined Accept field value, then the server SHOULD
        /// send a 406 (not acceptable) response.
        /// 
        /// A more elaborate example is:
        ///    Accept: text/plain; q=0.5, text/html,
        ///            text/x-dvi; q=0.8, text/x-c
        /// Verbally, this would be interpreted as 'text/html and text/x-c are
        /// the preferred media types, but if they do not exist, then send the
        /// text/x-dvi entity, and if that does not exist, send the text/plain
        /// entity.'
        /// 
        /// Media ranges can be overridden by more specific media ranges or
        /// specific media types. If more than one media range applies to a
        /// given type, the most specific reference has precedence. For example,
        /// 
        ///    Accept: text/*, text/html, text/html;level=1, */*
        ///    have the following precedence:
        ///       1) text/html;level=1
        ///       2) text/html
        ///       3) text/*
        ///       4) */*
        ///       
        /// The media type quality factor associated with a given type is
        /// determined by finding the media range with the highest precedence
        /// which matches that type. For example, 
        /// 
        ///    Accept: text/*;q=0.3, text/html;q=0.7, text/html;level=1,
        ///            text/html;level=2;q=0.4, */*;q=0.5
        ///            
        /// would cause the following values to be associated:
        /// 
        ///    text/html;level=1         = 1
        ///    text/html                 = 0.7
        ///    text/plain                = 0.3
        ///    image/jpeg                = 0.5
        ///    text/html;level=2         = 0.4
        ///    text/html;level=3         = 0.7
        ///    
        /// Note: A user agent might be provided with a default set of
        /// quality values for certain media ranges. However, unless the
        /// user agent is a closed system which cannot interact with other
        /// rendering agents, this default set ought to be configurable by
        /// the user.
        /// </summary>
        /// <example>Accept: text/plain; q=0.5, text/html</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Accept = new HTTPHeaderField("Accept",
                                                                            typeof(AcceptType),
                                                                            HeaderFieldType.Request,
                                                                            RequestPathSemantic.EndToEnd);

        #endregion

        #region Accept-Charset

        /// <summary>
        /// The Accept-Charset request-header field can be used to indicate
        /// what character sets are acceptable for the response. This field
        /// allows clients capable of understanding more comprehensive or
        /// special- purpose character sets to signal that capability to a
        /// server which is capable of representing documents in those
        /// character sets.
        /// 
        /// Character set values are described in section 3.4. Each charset
        /// MAY be given an associated quality value which represents the
        /// user's preference for that charset. The default value is q=1.
        /// 
        /// An example is Accept-Charset: iso-8859-5, unicode-1-1;q=0.8
        /// 
        /// The special value '*', if present in the Accept-Charset field,
        /// matches every character set (including ISO-8859-1) which is not
        /// mentioned elsewhere in the Accept-Charset field. If no '*' is
        /// present in an Accept-Charset field, then all character sets not
        /// explicitly mentioned get a quality value of 0, except for
        /// ISO-8859-1, which gets a quality value of 1 if not explicitly
        /// mentioned.
        /// 
        /// If no Accept-Charset header is present, the default is that any
        /// character set is acceptable. If an Accept-Charset header is
        /// present, and if the server cannot send a response which is
        /// acceptable according to the Accept-Charset header, then the
        /// server SHOULD send an error response with the 406 (not
        /// acceptable) status code, though the sending of an unacceptable
        /// response is also allowed.
        /// </summary>
        /// <example>Accept-Charset: iso-8859-5, unicode-1-1;q=0.8</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField AcceptCharset = new HTTPHeaderField("Accept-Charset",
                                                                                   typeof(String),
                                                                                   HeaderFieldType.Response,
                                                                                   RequestPathSemantic.EndToEnd);

        #endregion

        #region Accept-Encoding

        /// <summary>
        /// The Accept-Encoding request-header field is similar to Accept,
        /// but restricts the content-codings (section 3.5) that are
        /// acceptable in the response.
        /// 
        /// A server tests whether a content-coding is acceptable, according
        /// to an Accept-Encoding field, using these rules:
        /// 
        /// If the content-coding is one of the content-codings listed in
        /// the Accept-Encoding field, then it is acceptable, unless it is
        /// accompanied by a qvalue of 0. (As defined in section 3.9, a
        /// qvalue of 0 means "not acceptable.")
        /// 
        /// The special "*" symbol in an Accept-Encoding field matches any
        /// available content-coding not explicitly listed in the header field.
        /// 
        /// If multiple content-codings are acceptable, then the acceptable
        /// content-coding with the highest non-zero qvalue is preferred.
        /// 
        /// The "identity" content-coding is always acceptable, unless
        /// specifically refused because the Accept-Encoding field includes
        /// "identity;q=0", or because the field includes "*;q=0" and does
        /// not explicitly include the "identity" content-coding. If the
        /// Accept-Encoding field-value is empty, then only the "identity"
        /// encoding is acceptable.
        /// 
        /// If an Accept-Encoding field is present in a request, and if the
        /// server cannot send a response which is acceptable according to
        /// the Accept-Encoding header, then the server SHOULD send an error
        /// response with the 406 (Not Acceptable) status code.
        /// 
        /// If no Accept-Encoding field is present in a request, the server
        /// MAY assume that the client will accept any content coding. In
        /// this case, if "identity" is one of the available content-codings,
        /// then the server SHOULD use the "identity" content-coding, unless
        /// it has additional information that a different content-coding is
        /// meaningful to the client.
        /// 
        /// Note: If the request does not include an Accept-Encoding field, and
        /// if the "identity" content-coding is unavailable, then content-codings
        /// commonly understood by HTTP/1.0 clients (i.e., "gzip" and "compress")
        /// are preferred; some older clients improperly display messages sent
        /// with other content-codings. The server might also make this decision
        /// based on information about the particular user-agent or client.
        /// </summary>
        /// <example>Accept-Encoding: compress, gzip</example>
        /// <example>Accept-Encoding:</example>
        /// <example>Accept-Encoding: *</example>
        /// <example>Accept-Encoding: compress;q=0.5, gzip;q=1.0</example>
        /// <example>Accept-Encoding: gzip;q=1.0, identity; q=0.5, *;q=0</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField AcceptEncoding = new HTTPHeaderField("Accept-Encoding",
                                                                                   typeof(String),
                                                                                   HeaderFieldType.Response,
                                                                                   RequestPathSemantic.EndToEnd);

        #endregion

        #region Accept-Language

        /// <summary>
        /// The Accept-Language request-header field is similar to Accept,
        /// but restricts the set of natural languages that are preferred
        /// as a response to the request. Language tags are defined in
        /// section 3.10.
        /// 
        /// Each language-range MAY be given an associated quality value
        /// which represents an estimate of the user's preference for the
        /// languages specified by that range. The quality value defaults
        /// to "q=1". For example,
        ///    Accept-Language: da, en-gb;q=0.8, en;q=0.7
        /// would mean: "I prefer Danish, but will accept British English
        /// and other types of English." A language-range matches a
        /// language-tag if it exactly equals the tag, or if it exactly
        /// equals a prefix of the tag such that the first tag character
        /// following the prefix is "-". The special range "*", if present
        /// in the Accept-Language field, matches every tag not matched by
        /// any other range present in the Accept-Language field.
        /// 
        /// Note: This use of a prefix matching rule does not imply that
        /// language tags are assigned to languages in such a way that it
        /// is always true that if a user understands a language with a
        /// certain tag, then this user will also understand all languages
        /// with tags for which this tag is a prefix. The prefix rule simply
        /// allows the use of prefix tags if this is the case.
        /// 
        /// The language quality factor assigned to a language-tag by the
        /// Accept-Language field is the quality value of the longest
        /// language-range in the field that matches the language-tag. If
        /// no language-range in the field matches the tag, the language
        /// quality factor assigned is 0. If no Accept-Language header is
        /// present in the request, the server SHOULD assume that all
        /// languages are equally acceptable. If an Accept-Language header
        /// is present, then all languages which are assigned a quality
        /// factor greater than 0 are acceptable.
        /// 
        /// It might be contrary to the privacy expectations of the user
        /// to send an Accept-Language header with the complete linguistic
        /// preferences of the user in every request. For a discussion of
        /// this issue, see section 15.1.4.
        /// 
        /// As intelligibility is highly dependent on the individual user,
        /// it is recommended that client applications make the choice of
        /// linguistic preference available to the user. If the choice is
        /// not made available, then the Accept-Language header field MUST
        /// NOT be given in the request.
        /// 
        /// Note: When making the choice of linguistic preference available
        /// to the user, we remind implementors of the fact that users are
        /// not familiar with the details of language matching as described
        /// above, and should provide appropriate guidance. As an example,
        /// users might assume that on selecting "en-gb", they will be served
        /// any kind of English document if British English is not available.
        /// A user agent might suggest in such a case to add "en" to get the
        /// best matching behavior.
        /// </summary>
        /// <example>Accept-Language: da, en-gb;q=0.8, en;q=0.7</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField AcceptLanguage = new HTTPHeaderField("Accept-Language",
                                                                                    typeof(String),
                                                                                    HeaderFieldType.Request,
                                                                                    RequestPathSemantic.EndToEnd);

        #endregion

        #region Accept-Ranges

        /// <summary>
        /// The Accept-Ranges response-header field allows the server to
        /// indicate its acceptance of range requests for a resource:
        /// 
        /// Origin servers that accept byte-range requests MAY send
        ///    Accept-Ranges: bytes
        /// but are not required to do so. Clients MAY generate byte-range
        /// requests without having received this header for the resource
        /// involved. Range units are defined in section 3.12.
        /// 
        /// Servers that do not accept any kind of range request for a
        /// resource MAY send
        ///    Accept-Ranges: none
        /// to advise the client not to attempt a range request.
        /// </summary>
        /// <example>Accept-Ranges: bytes</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField AcceptRanges = new HTTPHeaderField("Accept-Ranges",
                                                                                  typeof(String),
                                                                                  HeaderFieldType.Request,
                                                                                  RequestPathSemantic.EndToEnd);

        #endregion

        #region Authorization

        /// <summary>
        /// A user agent that wishes to authenticate itself with a
        /// server--usually, but not necessarily, after receiving a 401
        /// response--does so by including an Authorization request-header
        /// field with the request. The Authorization field value consists
        /// of credentials containing the authentication information of
        /// the user agent for the realm of the resource being requested.
        /// 
        /// HTTP access authentication is described in "HTTP Authentication:
        /// Basic and Digest Access Authentication" [43]. If a request is
        /// authenticated and a realm specified, the same credentials SHOULD
        /// be valid for all other requests within this realm (assuming that
        /// the authentication scheme itself does not require otherwise, such
        /// as credentials that vary according to a challenge value or using
        /// synchronized clocks).
        /// 
        /// When a shared cache (see section 13.7) receives a request
        /// containing an Authorization field, it MUST NOT return the
        /// corresponding response as a reply to any other request, unless
        /// one of the following specific exceptions holds:
        /// 
        /// If the response includes the "s-maxage" cache-control directive,
        /// the cache MAY use that response in replying to a subsequent
        /// request. But (if the specified maximum age has passed) a proxy
        /// cache MUST first revalidate it with the origin server, using the
        /// request-headers from the new request to allow the origin server
        /// to authenticate the new request. (This is the defined behavior
        /// for s-maxage.) If the response includes "s-maxage=0", the proxy
        /// MUST always revalidate it before re-using it.
        /// 
        /// If the response includes the "must-revalidate" cache-control
        /// directive, the cache MAY use that response in replying to a
        /// subsequent request. But if the response is stale, all caches
        /// MUST first revalidate it with the origin server, using the
        /// request-headers from the new request to allow the origin
        /// server to authenticate the new request.
        /// 
        /// If the response includes the "public" cache-control directive,
        /// it MAY be returned in reply to any subsequent request.
        /// </summary>
        /// <example>Authorization: Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Authorization = new HTTPHeaderField("Authorization",
                                                                                   typeof(String),
                                                                                   HeaderFieldType.Request,
                                                                                   RequestPathSemantic.EndToEnd);

        #endregion

        #region Depth

        /// <summary>
        /// The Depth request header is used with methods executed on
        /// resources that could potentially have internal members to
        /// indicate whether the method is to be applied only to the
        /// resource ("Depth: 0"), to the resource and its internal
        /// members only ("Depth: 1"), or the resource and all its
        /// members ("Depth: infinity").
        /// 
        /// The Depth header is only supported if a method's definition
        /// explicitly provides for such support.
        /// 
        /// The following rules are the default behavior for any method
        /// that supports the Depth header. A method may override these
        /// defaults by defining different behavior in its definition.
        /// 
        /// Methods that support the Depth header may choose not to
        /// support all of the header's values and may define, on a
        /// case-by-case basis, the behavior of the method if a Depth
        /// header is not present. For example, the MOVE method only
        /// supports "Depth: infinity", and if a Depth header is not
        /// present, it will act as if a "Depth: infinity" header had
        /// been applied.
        /// 
        /// Clients MUST NOT rely upon methods executing on members of
        /// their hierarchies in any particular order or on the execution
        /// being atomic unless the particular method explicitly provides
        /// such guarantees.
        /// 
        /// Upon execution, a method with a Depth header will perform as
        /// much of its assigned task as possible and then return a
        /// response specifying what it was able to accomplish and what
        /// it failed to do.
        /// 
        /// So, for example, an attempt to COPY a hierarchy may result
        /// in some of the members being copied and some not.
        /// 
        /// By default, the Depth header does not interact with other
        /// headers. That is, each header on a request with a Depth
        /// header MUST be applied only to the Request-URI if it applies
        /// to any resource, unless specific Depth behavior is defined
        /// for that header.
        /// 
        /// If a source or destination resource within the scope of the
        /// Depth header is locked in such a way as to prevent the
        /// successful execution of the method, then the lock token for
        /// that resource MUST be submitted with the request in the If
        /// request header.
        /// 
        /// The Depth header only specifies the behavior of the method
        /// with regards to internal members. If a resource does not
        /// have internal members, then the Depth header MUST be ignored.
        /// </summary>
        /// <example>Depth: 0</example>
        /// <example>Depth: 1</example>
        /// <example>Depth: infinity</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Depth = new HTTPHeaderField("Depth",
                                                                           typeof(String),
                                                                           HeaderFieldType.Request,
                                                                           RequestPathSemantic.EndToEnd);

        #endregion

        #region Destination

        /// <summary>
        /// The Destination request header specifies the URI that
        /// identifies a destination resource for methods such as
        /// COPY and MOVE, which take two URIs as parameters.
        /// 
        /// If the Destination value is an absolute-URI (Section 4.3
        /// of [RFC3986]), it may name a different server (or different
        /// port or scheme). If the source server cannot attempt a copy
        /// to the remote server, it MUST fail the request. Note that
        /// copying and moving resources to remote servers is not fully
        /// defined in this specification (e.g., specific error conditions).
        /// 
        /// If the Destination value is too long or otherwise unacceptable,
        /// the server SHOULD return 400 (Bad Request), ideally with helpful
        /// information in an error body.
        /// </summary>
        /// <example>Destination : index-old.html</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Destination = new HTTPHeaderField("Destination",
                                                                                 typeof(String),
                                                                                 HeaderFieldType.Request,
                                                                                 RequestPathSemantic.EndToEnd);

        #endregion

        #region Expect

        /// <summary>
        /// The Expect request-header field is used to indicate that
        /// particular server behaviors are required by the client.
        /// 
        /// A server that does not understand or is unable to comply
        /// with any of the expectation values in the Expect field of
        /// a request MUST respond with appropriate error status. The
        /// server MUST respond with a 417 (Expectation Failed) status
        /// if any of the expectations cannot be met or, if there are
        /// other problems with the request, some other 4xx status.
        /// 
        /// This header field is defined with extensible syntax to
        /// allow for future extensions. If a server receives a request
        /// containing an Expect field that includes an expectation-
        /// extension that it does not support, it MUST respond with
        /// a 417 (Expectation Failed) status.
        /// 
        /// Comparison of expectation values is case-insensitive for
        /// unquoted tokens (including the 100-continue token), and
        /// is case-sensitive for quoted-string expectation-extensions.
        /// 
        /// The Expect mechanism is hop-by-hop: that is, an HTTP/1.1
        /// proxy MUST return a 417 (Expectation Failed) status if it
        /// receives a request with an expectation that it cannot meet.
        /// However, the Expect request-header itself is end-to-end;
        /// it MUST be forwarded if the request is forwarded.
        /// 
        /// Many older HTTP/1.0 and HTTP/1.1 applications do not
        /// understand the Expect header.
        /// 
        /// See section 8.2.3 for the use of the 100 (continue) status.
        /// </summary>
        /// <example>Expect: 100-continue</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Expect = new HTTPHeaderField("Expect",
                                                                            typeof(String),
                                                                            HeaderFieldType.Request,
                                                                            RequestPathSemantic.EndToEnd);

        #endregion

        #region From 

        /// <summary> 
        /// The From request-header field, if given, SHOULD contain an 
        /// Internet e-mail address for the human user who controls the 
        /// requesting user agent. The address SHOULD be machine-usable, 
        /// as defined by "mailbox" in RFC 822 [9] as updated by 
        /// RFC 1123 [8]: 
        ///  
        /// This header field MAY be used for logging purposes and as a 
        /// means for identifying the source of invalid or unwanted 
        /// requests. It SHOULD NOT be used as an insecure form of 
        /// access protection. The interpretation of this field is that 
        /// the request is being performed on behalf of the person given, 
        /// who accepts responsibility for the method performed. In 
        /// particular, robot agents SHOULD include this header so that 
        /// the person responsible for running the robot can be contacted 
        /// if problems occur on the receiving end. 
        ///  
        /// The Internet e-mail address in this field MAY be separate 
        /// from the Internet host which issued the request. For example, 
        /// when a request is passed through a proxy the original issuer's 
        /// address SHOULD be used. 
        ///  
        /// The client SHOULD NOT send the From header field without the 
        /// user's approval, as it might conflict with the user's privacy 
        /// interests or their site's security policy. It is strongly 
        /// recommended that the user be able to disable, enable, and 
        /// modify the value of this field at any time prior to a request. 
        /// </summary> 
        /// <example>From: webmaster@w3.org</example> 
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/> 
        public static readonly HTTPHeaderField From  = new HTTPHeaderField("From",
                                                                           typeof(String),
                                                                           HeaderFieldType.Request,
                                                                           RequestPathSemantic.EndToEnd);


        #endregion 

        #region Host

        /// <summary>
        /// The Host request-header field specifies the Internet host
        /// and port number of the resource being requested, as obtained
        /// from the original URI given by the user or referring
        /// resource (generally an HTTP URL, as described in section
        /// 3.2.2). The Host field value MUST represent the naming
        /// authority of the origin server or gateway given by the
        /// original URL. This allows the origin server or gateway to
        /// differentiate between internally-ambiguous URLs, such as the
        /// root "/" URL of a server for multiple host names on a single
        /// IP address. 
        /// 
        /// A "host" without any trailing port information implies the
        /// default port for the service requested (e.g., "80" for an
        /// HTTP URL). For example, a request on the origin server for
        /// &lt;http://www.w3.org/pub/WWW/&gt; would properly include:
        /// 
        /// GET /pub/WWW/ HTTP/1.1
        /// Host: www.w3.org
        /// 
        /// A client MUST include a Host header field in all HTTP/1.1
        /// request messages . If the requested URI does not include
        /// an Internet host name for the service being requested,
        /// then the Host header field MUST be given with an empty
        /// value. An HTTP/1.1 proxy MUST ensure that any request
        /// message it forwards does contain an appropriate Host
        /// header field that identifies the service being requested
        /// by the proxy. All Internet-based HTTP/1.1 servers MUST
        /// respond with a 400 (Bad Request) status code to any
        /// HTTP/1.1 request message which lacks a Host header field.
        /// 
        /// See sections 5.2 and 19.6.1.1 for other requirements
        /// relating to Host.
        /// </summary>
        /// <example>Host: www.w3.org</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Host = new HTTPHeaderField("Host",
                                                                          typeof(String),
                                                                          HeaderFieldType.Request,
                                                                          RequestPathSemantic.EndToEnd);

        #endregion

        #region If

        /// <summary>
        /// The If request header is intended to have similar functionality
        /// to the If-Match header defined in Section 14.24 of [RFC2616].
        /// However, the If header handles any state token as well as ETags.
        /// A typical example of a state token is a lock token, and lock
        /// tokens are the only state tokens defined in this specification.
        /// 
        /// Purpose
        /// The If header has two distinct purposes:
        /// 
        /// The first purpose is to make a request conditional by supplying
        /// a series of state lists with conditions that match tokens and
        /// ETags to a specific resource. If this header is evaluated and
        /// all state lists fail, then the request MUST fail with a 412
        /// (Precondition Failed) status. On the other hand, the request
        /// can succeed only if one of the described state lists succeeds.
        /// The success criteria for state lists and matching functions are
        /// defined in Sections 10.4.3 and 10.4.4.
        /// Additionally, the mere fact that a state token appears in an If
        /// header means that it has been "submitted" with the request. In
        /// general, this is used to indicate that the client has knowledge
        /// of that state token. The semantics for submitting a state token
        /// depend on its type (for lock tokens, please refer to Section 6).
        /// Note that these two purposes need to be treated distinctly: a
        /// state token counts as being submitted independently of whether
        /// the server actually has evaluated the state list it appears in,
        /// and also independently of whether or not the condition it
        /// expressed was found to be true.
        /// 
        /// Syntax
        /// If = "If" ":" ( 1*No-tag-list | 1*Tagged-list )
        /// 
        /// No-tag-list = List
        /// Tagged-list = Resource-Tag 1*List
        /// 
        /// List = "(" 1*Condition ")"
        /// Condition = ["Not"] (State-token | "[" entity-tag "]")
        /// ; entity-tag: see Section 3.11 of [RFC2616]
        /// ; No LWS allowed between "[", entity-tag and "]"
        /// 
        /// State-token = Coded-URL
        /// 
        /// Resource-Tag = "&lt;" Simple-ref "&gt;" 
        /// ; Simple-ref: see Section 8.3
        /// ; No LWS allowed in Resource-TagThe syntax distinguishes
        /// between untagged lists ("No-tag-list") and tagged lists
        /// ("Tagged-list"). Untagged lists apply to the resource
        /// identified by the Request-URI, while tagged lists apply to
        /// the resource identified by the preceding Resource-Tag.
        /// 
        /// A Resource-Tag applies to all subsequent Lists, up to the
        /// next Resource-Tag.
        /// 
        /// Note that the two list types cannot be mixed within an If header.
        /// This is not a functional restriction because the No-tag-list
        /// syntax is just a shorthand notation for a Tagged-list production
        /// with a Resource-Tag referring to the Request-URI.
        /// 
        /// Each List consists of one or more Conditions. Each Condition is
        /// defined in terms of an entity-tag or state-token, potentially
        /// negated by the prefix "Not".
        /// 
        /// Note that the If header syntax does not allow multiple instances of
        /// If headers in a single request. However, the HTTP header syntax
        /// allows extending single header values across multiple lines, by
        /// inserting a line break followed by whitespace (see [RFC2616],
        /// Section 4.2).
        /// 
        /// List Evaluation
        /// A Condition that consists of a single entity-tag or state-token
        /// evaluates to true if the resource matches the described state
        /// (where the individual matching functions are defined below in
        /// Section 10.4.4). Prefixing it with "Not" reverses the result of
        /// the evaluation (thus, the "Not" applies only to the subsequent
        /// entity-tag or state-token).
        /// 
        /// Each List production describes a series of conditions. The whole
        /// list evaluates to true if and only if each condition evaluates to
        /// true (that is, the list represents a logical conjunction of
        /// Conditions).
        /// 
        /// Each No-tag-list and Tagged-list production may contain one or
        /// more Lists. They evaluate to true if and only if any of the
        /// contained lists evaluates to true (that is, if there's more than
        /// one List, that List sequence represents a logical disjunction of
        /// the Lists).
        /// 
        /// Finally, the whole If header evaluates to true if and only if at
        /// least one of the No-tag-list or Tagged-list productions evaluates
        /// to true. If the header evaluates to false, the server MUST reject
        /// the request with a 412 (Precondition Failed) status. Otherwise,
        /// execution of the request can proceed as if the header wasn't
        /// present.
        /// 
        /// Matching State Tokens and ETags
        /// When performing If header processing, the definition of a matching
        /// state token or entity tag is as follows:
        /// 
        /// Identifying a resource: The resource is identified by the URI along
        /// with the token, in tagged list production, or by the Request-URI in
        /// untagged list production.
        /// 
        /// Matching entity tag: Where the entity tag matches an entity tag
        /// associated with the identified resource. Servers MUST use either
        /// the weak or the strong comparison function defined in Section 13.3.3
        /// of [RFC2616].
        /// 
        /// Matching state token: Where there is an exact match between the
        /// state token in the If header and any state token on the identified
        /// resource. A lock state token is considered to match if the resource
        /// is anywhere in the scope of the lock.
        /// 
        /// Handling unmapped URLs: For both ETags and state tokens, treat as
        /// if the URL identified a resource that exists but does not have the
        /// specified state.
        /// 
        /// If Header and Non-DAV-Aware Proxies
        /// Non-DAV-aware proxies will not honor the If header, since they will
        /// not understand the If header, and HTTP requires non-understood
        /// headers to be ignored. When communicating with HTTP/1.1 proxies,
        /// the client MUST use the "Cache-Control: no-cache" request header
        /// so as to prevent the proxy from improperly trying to service the
        /// request from its cache. When dealing with HTTP/1.0 proxies, the
        /// "Pragma: no-cache" request header MUST be used for the same reason.
        /// 
        /// Because in general clients may not be able to reliably detect
        /// non-DAV-aware intermediates, they are advised to always prevent
        /// caching using the request directives mentioned above.
        /// 
        /// Example - No-tag Production
        /// If: (&lt;urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2&gt; 
        /// (["I am an ETag"])
        /// (["I am another ETag"])The previous header would require that the
        /// resource identified in the Request-URI be locked with the specified
        /// lock token and be in the state identified by the "I am an ETag"
        /// ETag or in the state identified by the second ETag "I am another
        /// ETag".
        /// 
        /// To put the matter more plainly one can think of the previous If
        /// header as expressing the condition below:
        /// ( 
        ///   is-locked-with(urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2) AND
        ///   matches-etag("I am an ETag")
        /// )
        /// OR
        /// (
        ///   matches-etag("I am another ETag")
        /// )Example - Using "Not" with No-tag Production
        /// If: (Not &lt;urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2&gt; 
        /// &lt;urn:uuid:58f202ac-22cf-11d1-b12d-002035b29092&gt;)
        /// This If header requires that the resource must not be locked with a
        /// lock having the lock token urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2
        /// and must be locked by a lock with the lock token
        /// urn:uuid:58f202ac-22cf-11d1-b12d-002035b29092.
        /// 
        /// Example - Causing a Condition to Always Evaluate to True
        /// There may be cases where a client wishes to submit state tokens, but
        /// doesn't want the request to fail just because the state token isn't
        /// current anymore. One simple way to do this is to include a Condition
        /// that is known to always evaluate to true, such as in:
        /// 
        /// If: (&lt;urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2&gt;)
        /// (Not <DAV:no-lock>)"DAV:no-lock" is known to never represent a
        /// current lock token. Lock tokens are assigned by the server, following
        /// the uniqueness requirements described in Section 6.5, therefore cannot
        /// use the "DAV:" scheme. Thus, by applying "Not" to a state token that
        /// is known not to be current, the Condition always evaluates to true.
        /// Consequently, the whole If header will always evaluate to true, and
        /// the lock token urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2 will be
        /// submitted in any case.
        /// 
        /// Example - Tagged List If Header in COPY
        /// Request: 
        /// 
        /// COPY /resource1 HTTP/1.1 
        /// Host: www.example.com 
        /// Destination: /resource2 
        /// If: &lt;/resource1&gt;
        /// (&lt;urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2&gt;
        /// [W/"A weak ETag"]) (["strong ETag"])
        /// 
        /// In this example, http://www.example.com/resource1 is being copied to
        /// http://www.example.com/resource2. When the method is first applied to
        /// http://www.example.com/resource1, resource1 must be in the state
        /// specified by "(&lt;urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2&gt;
        /// [W/"A weak ETag"]) (["strong ETag"])". That is, either it must be
        /// locked with a lock token of "urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2"
        /// and have a weak entity tag W/"A weak ETag" or it must have a strong
        /// entity tag "strong ETag".
        /// 
        /// Example - Matching Lock Tokens with Collection Locks
        /// DELETE /specs/rfc2518.txt HTTP/1.1 
        /// Host: www.example.com 
        /// If: &lt;http://www.example.com/specs/ &gt;
        /// (&lt;urn:uuid:181d4fae-7d8c-11d0-a765-00a0c91e6bf2&gt;)For this example,
        /// the lock token must be compared to the identified resource, which is the
        /// 'specs' collection identified by the URL in the tagged list production.
        /// If the 'specs' collection is not locked by a lock with the specified
        /// lock token, the request MUST fail. Otherwise, this request could succeed,
        /// because the If header evaluates to true, and because the lock token for
        /// the lock affecting the affected resource has been submitted.
        /// 
        /// Example - Matching ETags on Unmapped URLs
        /// Consider a collection "/specs" that does not contain the member
        /// "/specs/rfc2518.doc". In this case, the If header
        /// 
        /// If: &lt;/specs/rfc2518.doc&gt; (["4217"])will evaluate to false (the URI
        /// isn't mapped, thus the resource identified by the URI doesn't have an
        /// entity matching the ETag "4217").
        /// 
        /// On the other hand, an If header of
        /// 
        /// If: &lt;/specs/rfc2518.doc&gt; (Not ["4217"]) will consequently evaluate
        /// to true.
        /// 
        /// Note that, as defined above in Section 10.4.4, the same considerations
        /// apply to matching state tokens.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField If = new HTTPHeaderField("If",
                                                                        typeof(String),
                                                                        HeaderFieldType.Request,
                                                                        RequestPathSemantic.EndToEnd);

        #endregion

        #region If-Match

        /// <summary>
        /// The If-Match request-header field is used with a method to make
        /// it conditional. A client that has one or more entities previously
        /// obtained from the resource can verify that one of those entities
        /// is current by including a list of their associated entity tags in
        /// the If-Match header field. Entity tags are defined in section
        /// 3.11. The purpose of this feature is to allow efficient updates
        /// of cached information with a minimum amount of transaction
        /// overhead. It is also used, on updating requests, to prevent
        /// inadvertent modification of the wrong version of a resource.
        /// As a special case, the value "*" matches any current entity of
        /// the resource.
        /// 
        /// If any of the entity tags match the entity tag of the entity that
        /// would have been returned in the response to a similar GET request
        /// (without the If-Match header) on that resource, or if "*" is given
        /// and any current entity exists for that resource, then the server
        /// MAY perform the requested method as if the If-Match header field
        /// did not exist.
        /// 
        /// A server MUST use the strong comparison function (see section
        /// 13.3.3) to compare the entity tags in If-Match.
        /// 
        /// If none of the entity tags match, or if "*" is given and no current
        /// entity exists, the server MUST NOT perform the requested method,
        /// and MUST return a 412 (Precondition Failed) response. This behavior
        /// is most useful when the client wants to prevent an updating method,
        /// such as PUT, from modifying a resource that has changed since the
        /// client last retrieved it.
        /// 
        /// If the request would, without the If-Match header field, result in
        /// anything other than a 2xx or 412 status, then the If-Match header
        /// MUST be ignored. 
        /// 
        /// The meaning of "If-Match: *" is that the method SHOULD be performed
        /// if the representation selected by the origin server (or by a cache,
        /// possibly using the Vary mechanism, see section 14.44) exists, and
        /// MUST NOT be performed if the representation does not exist.
        /// 
        /// A request intended to update a resource (e.g., a PUT) MAY include
        /// an If-Match header field to signal that the request method MUST NOT
        /// be applied if the entity corresponding to the If-Match value (a
        /// single entity tag) is no longer a representation of that resource.
        /// This allows the user to indicate that they do not wish the request
        /// to be successful if the resource has been changed without their
        /// knowledge.
        /// 
        /// The result of a request having both an If-Match header field and
        /// either an If-None-Match or an If-Modified-Since header fields is
        /// undefined by this specification. 
        /// </summary>
        /// <example>If-Match: "xyzzy"</example>
        /// <example>If-Match: "xyzzy", "r2d2xxxx", "c3piozzzz"</example>
        /// <example>If-Match: *</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField IfMatch = new HTTPHeaderField("If-Match",
                                                                             typeof(String),
                                                                             HeaderFieldType.Request,
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

        #region If-Modified-Since

        /// <summary>
        /// The If-Modified-Since request-header field is used with a method
        /// to make it conditional: if the requested variant has not been
        /// modified since the time specified in this field, an entity will
        /// not be returned from the server; instead, a 304 (not modified)
        /// response will be returned without any message-body.
        /// 
        /// If-Modified-Since = "If-Modified-Since" ":" HTTP-date
        /// An example of the field is: 
        /// 
        /// If-Modified-Since: Sat, 29 Oct 1994 19:43:31 GMT
        /// A GET method with an If-Modified-Since header and no Range header
        /// requests that the identified entity be transferred only if it has
        /// been modified since the date given by the If-Modified-Since
        /// header. The algorithm for determining this includes the following
        /// cases: 
        /// 
        /// If the request would normally result in anything other than a 200
        /// (OK) status, or if the passed If-Modified-Since date is invalid,
        /// the response is exactly the same as for a normal GET. A date which
        /// is later than the server's current time is invalid.
        /// 
        /// If the variant has been modified since the If-Modified-Since date,
        /// the response is exactly the same as for a normal GET.
        /// 
        /// If the variant has not been modified since a valid If-Modified-Since
        /// date, the server SHOULD return a 304 (Not Modified) response.
        /// 
        /// The purpose of this feature is to allow efficient updates of cached
        /// information with a minimum amount of transaction overhead.
        /// 
        /// Note: The Range request-header field modifies the meaning of
        /// If-Modified-Since; see section 14.35 for full details.
        /// 
        /// Note: If-Modified-Since times are interpreted by the server, whose
        /// clock might not be synchronized with the client.
        /// 
        /// Note: When handling an If-Modified-Since header field, some servers
        /// will use an exact date comparison function, rather than a less-than
        /// function, for deciding whether to send a 304 (Not Modified) response.
        /// To get best results when sending an If-Modified-Since header field
        /// for cache validation, clients are advised to use the exact date
        /// string received in a previous Last-Modified header field whenever
        /// possible.
        /// 
        /// Note: If a client uses an arbitrary date in the If-Modified-Since
        /// header instead of a date taken from the Last-Modified header for
        /// the same request, the client should be aware of the fact that this
        /// date is interpreted in the server's understanding of time. The
        /// client should consider unsynchronized clocks and rounding problems
        /// due to the different encodings of time between the client and
        /// server. This includes the possibility of race conditions if the
        /// document has changed between the time it was first requested and
        /// the If-Modified-Since date of a subsequent request, and the
        /// possibility of clock-skew-related problems if the If-Modified-Since
        /// date is derived from the client's clock without correction to the
        /// server's clock. Corrections for different time bases between
        /// client and server are at best approximate due to network latency.
        /// 
        /// The result of a request having both an If-Modified-Since header
        /// field and either an If-Match or an If-Unmodified-Since header
        /// fields is undefined by this specification.
        /// </summary>
        /// <example>If-Modified-Since: Sat, 29 Oct 1994 19:43:31 GMT</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField IfModifiedSince = new HTTPHeaderField("If-Modified-Since",
                                                                                     typeof(String),
                                                                                     HeaderFieldType.Request,
                                                                                     RequestPathSemantic.EndToEnd);

        #endregion

        #region If-None-Match

        /// <summary>
        /// The If-None-Match request-header field is used with a method to
        /// make it conditional. A client that has one or more entities
        /// previously obtained from the resource can verify that none of
        /// those entities is current by including a list of their associated
        /// entity tags in the If-None-Match header field. The purpose of
        /// this feature is to allow efficient updates of cached information
        /// with a minimum amount of transaction overhead. It is also used
        /// to prevent a method (e.g. PUT) from inadvertently modifying an
        /// existing resource when the client believes that the resource
        /// does not exist.
        /// 
        /// As a special case, the value "*" matches any current entity of
        /// the resource.
        /// 
        /// If any of the entity tags match the entity tag of the entity
        /// that would have been returned in the response to a similar GET
        /// request (without the If-None-Match header) on that resource,
        /// or if "*" is given and any current entity exists for that
        /// resource, then the server MUST NOT perform the requested method,
        /// unless required to do so because the resource's modification
        /// date fails to match that supplied in an If-Modified-Since header
        /// field in the request. Instead, if the request method was GET or
        /// HEAD, the server SHOULD respond with a 304 (Not Modified)
        /// response, including the cache- related header fields
        /// (particularly ETag) of one of the entities that matched. For all
        /// other request methods, the server MUST respond with a status of
        /// 412 (Precondition Failed).
        /// 
        /// See section 13.3.3 for rules on how to determine if two entities
        /// tags match. The weak comparison function can only be used with
        /// GET or HEAD requests.
        /// 
        /// If none of the entity tags match, then the server MAY perform
        /// the requested method as if the If-None-Match header field did
        /// not exist, but MUST also ignore any If-Modified-Since header
        /// field(s) in the request. That is, if no entity tags match,
        /// then the server MUST NOT return a 304 (Not Modified) response.
        /// 
        /// If the request would, without the If-None-Match header field,
        /// result in anything other than a 2xx or 304 status, then the
        /// If-None-Match header MUST be ignored. (See section 13.3.4 for
        /// a discussion of server behavior when both If-Modified-Since
        /// and If-None-Match appear in the same request.)
        /// 
        /// The meaning of "If-None-Match: *" is that the method MUST NOT
        /// be performed if the representation selected by the origin
        /// server (or by a cache, possibly using the Vary mechanism, see
        /// section 14.44) exists, and SHOULD be performed if the
        /// representation does not exist. This feature is intended to be
        /// useful in preventing races between PUT operations.
        /// 
        /// The result of a request having both an If-None-Match header
        /// field and either an If-Match or an If-Unmodified-Since header
        /// fields is undefined by this specification.
        /// </summary>
        /// <example>If-None-Match: "xyzzy"</example>
        /// <example>If-None-Match: W/"xyzzy"</example>
        /// <example>If-None-Match: "xyzzy", "r2d2xxxx", "c3piozzzz"</example>
        /// <example>If-None-Match: W/"xyzzy", W/"r2d2xxxx", W/"c3piozzzz"</example>
        /// <example>If-None-Match: *</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField IfNoneMatch = new HTTPHeaderField("If-None-Match",
                                                                                 typeof(String),
                                                                                 HeaderFieldType.Request,
                                                                                 RequestPathSemantic.EndToEnd);

        #endregion

        #region If-Range

        /// <summary>
        /// If a client has a partial copy of an entity in its cache, and wishes
        /// to have an up-to-date copy of the entire entity in its cache, it could
        /// use the Range request-header with a conditional GET (using either or
        /// both of If-Unmodified-Since and If-Match.) However, if the condition
        /// fails because the entity has been modified, the client would then
        /// have to make a second request to obtain the entire current entity-body.
        /// 
        /// The If-Range header allows a client to "short-circuit" the second
        /// request. Informally, its meaning is `if the entity is unchanged, send
        /// me the part(s) that I am missing; otherwise, send me the entire new
        /// entity'.
        /// 
        ///    If-Range = "If-Range" ":" ( entity-tag | HTTP-date )
        ///    
        /// If the client has no entity tag for an entity, but does have a
        /// Last-Modified date, it MAY use that date in an If-Range header. (The
        /// server can distinguish between a valid HTTP-date and any form of
        /// entity-tag by examining no more than two characters.) The If-Range
        /// header SHOULD only be used together with a Range header, and MUST be
        /// ignored if the request does not include a Range header, or if the
        /// server does not support the sub-range operation.
        /// 
        /// If the entity tag given in the If-Range header matches the current
        /// entity tag for the entity, then the server SHOULD provide the
        /// specified sub-range of the entity using a 206 (Partial content)
        /// response. If the entity tag does not match, then the server SHOULD
        /// return the entire entity using a 200 (OK) response.
        /// </summary>
        /// <example>If-Range: "737060cd8c284d8af7ad3082f209582d"</example>
        /// <example>If-Range: Sat, 29 Oct 1994 19:43:31 GMT</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField IfRange = new HTTPHeaderField("If-Range",
                                                                             typeof(String),
                                                                             HeaderFieldType.Request,
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

        #region If-Unmodified-Since

        /// <summary>
        /// The If-Unmodified-Since request-header field is used with a method
        /// to make it conditional. If the requested resource has not been
        /// modified since the time specified in this field, the server SHOULD
        /// perform the requested operation as if the If-Unmodified-Since
        /// header were not present.
        /// 
        /// If the requested variant has been modified since the specified time,
        /// the server MUST NOT perform the requested operation, and MUST return
        /// a 412 (Precondition Failed).
        /// 
        /// If the request normally (i.e., without the If-Unmodified-Since
        /// header) would result in anything other than a 2xx or 412 status,
        /// the If-Unmodified-Since header SHOULD be ignored.
        /// 
        /// If the specified date is invalid, the header is ignored.
        /// 
        /// The result of a request having both an If-Unmodified-Since header
        /// field and either an If-None-Match or an If-Modified-Since header
        /// fields is undefined by this specification.
        /// </summary>
        /// <example>If-Unmodified-Since: Sat, 29 Oct 1994 19:43:31 GMT</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField IfUnmodifiedSince = new HTTPHeaderField("If-Unmodified-Since",
                                                                                       typeof(String),
                                                                                       HeaderFieldType.Request,
                                                                                       RequestPathSemantic.EndToEnd);

        #endregion

        #region Lock-Token

        /// <summary>
        /// The Lock-Token request header is used with the UNLOCK method
        /// to identify the lock to be removed. The lock token in the
        /// Lock-Token request header MUST identify a lock that contains
        /// the resource identified by Request-URI as a member.
        /// 
        /// The Lock-Token response header is used with the LOCK method
        /// to indicate the lock token created as a result of a successful
        /// LOCK request to create a new lock.
        /// </summary>
        /// <example>Lock-Token: Coded-URL</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField LockToken = new HTTPHeaderField("Lock-Token",
                                                                               typeof(String),
                                                                               HeaderFieldType.Request,
                                                                               RequestPathSemantic.EndToEnd);

        #endregion

        #region Max-Forwards

        /// <summary>
        /// The Max-Forwards request-header field provides a mechanism
        /// with the TRACE (section 9.8) and OPTIONS (section 9.2)
        /// methods to limit the number of proxies or gateways that can
        /// forward the request to the next inbound server. This can be
        /// useful when the client is attempting to trace a request
        /// chain which appears to be failing or looping in mid-chain.
        /// 
        /// The Max-Forwards value is a decimal integer indicating the
        /// remaining number of times this request message may be
        /// forwarded.
        /// 
        /// Each proxy or gateway recipient of a TRACE or OPTIONS request
        /// containing a Max-Forwards header field MUST check and update
        /// its value prior to forwarding the request. If the received
        /// value is zero (0), the recipient MUST NOT forward the request;
        /// instead, it MUST respond as the final recipient. If the
        /// received Max-Forwards value is greater than zero, then the
        /// forwarded message MUST contain an updated Max-Forwards field
        /// with a value decremented by one (1).
        /// 
        /// The Max-Forwards header field MAY be ignored for all other
        /// methods defined by this specification and for any extension
        /// methods for which it is not explicitly referred to as part of
        /// that method definition.
        /// </summary>
        /// <example>Max-Forwards: 10</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField MaxForwards = new HTTPHeaderField("Max-Forwards",
                                                                                 typeof(UInt64?),
                                                                                 HeaderFieldType.Request,
                                                                                 RequestPathSemantic.EndToEnd,
                                                                                 StringParsers.NullableUInt64);

        #endregion

        #region Overwrite

        /// <summary>
        /// The Overwrite request header specifies whether the server should
        /// overwrite a resource mapped to the destination URL during a COPY
        /// or MOVE. A value of "F" states that the server must not perform
        /// the COPY or MOVE operation if the destination URL does map to a
        /// resource. If the overwrite header is not included in a COPY or
        /// MOVE request, then the resource MUST treat the request as if it
        /// has an overwrite header of value "T". While the Overwrite header
        /// appears to duplicate the functionality of using an "If-Match: *"
        /// header (see [RFC2616]), If-Match applies only to the Request-URI,
        /// and not to the Destination of a COPY or MOVE.
        /// 
        /// If a COPY or MOVE is not performed due to the value of the Overwrite
        /// header, the method MUST fail with a 412 (Precondition Failed) status
        /// code. The server MUST do authorization checks before checking this
        /// or any conditional header.
        /// 
        /// All DAV-compliant resources MUST support the Overwrite header.
        /// </summary>
        /// <example>Overwrite: T</example>
        /// <example>Overwrite: F</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Overwrite = new HTTPHeaderField("Overwrite",
                                                                               typeof(String),
                                                                               HeaderFieldType.Request,
                                                                               RequestPathSemantic.EndToEnd);

        #endregion

        #region Proxy-Authorization

        /// <summary>
        /// The Proxy-Authorization request-header field allows the client
        /// to identify itself (or its user) to a proxy which requires
        /// authentication. The Proxy-Authorization field value consists
        /// of credentials containing the authentication information of
        /// the user agent for the proxy and/or realm of the resource
        /// being requested.
        /// 
        /// The HTTP access authentication process is described in "HTTP
        /// Authentication: Basic and Digest Access Authentication" [43].
        /// Unlike Authorization, the Proxy-Authorization header field
        /// applies only to the next outbound proxy that demanded
        /// authentication using the Proxy- Authenticate field. When
        /// multiple proxies are used in a chain, the Proxy-Authorization
        /// header field is consumed by the first outbound proxy that was
        /// expecting to receive credentials. A proxy MAY relay the
        /// credentials from the client request to the next proxy if that
        /// is the mechanism by which the proxies cooperatively
        /// authenticate a given request.
        /// </summary>
        /// <example>Proxy-Authorization: Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ProxyAuthorization = new HTTPHeaderField("Proxy-Authorization",
                                                                                        typeof(String),
                                                                                        HeaderFieldType.Request,
                                                                                        RequestPathSemantic.EndToEnd);

        #endregion

        #region Range

        /// <summary>
        /// Since all HTTP entities are represented in HTTP messages as
        /// sequences of bytes, the concept of a byte range is meaningful
        /// for any HTTP entity. (However, not all clients and servers
        /// need to support byte- range operations.)
        /// 
        /// Byte range specifications in HTTP apply to the sequence of
        /// bytes in the entity-body (not necessarily the same as the
        /// message-body).
        /// 
        /// A byte range operation MAY specify a single range of bytes,
        /// or a set of ranges within a single entity.
        /// 
        /// The first-byte-pos value in a byte-range-spec gives the
        /// byte-offset of the first byte in a range. The last-byte-pos
        /// value gives the byte-offset of the last byte in the range;
        /// that is, the byte positions specified are inclusive. Byte
        /// offsets start at zero.
        /// 
        /// If the last-byte-pos value is present, it MUST be greater
        /// than or equal to the first-byte-pos in that byte-range-spec,
        /// or the byte- range-spec is syntactically invalid. The
        /// recipient of a byte-range- set that includes one or more
        /// syntactically invalid byte-range-spec values MUST ignore the
        /// header field that includes that byte-range- set.
        /// 
        /// If the last-byte-pos value is absent, or if the value is
        /// greater than or equal to the current length of the entity-body,
        /// last-byte-pos is taken to be equal to one less than the current
        /// length of the entity- body in bytes.
        /// 
        /// By its choice of last-byte-pos, a client can limit the number
        /// of bytes retrieved without knowing the size of the entity.
        /// 
        /// A suffix-byte-range-spec is used to specify the suffix of the
        /// entity-body, of a length given by the suffix-length value. (That
        /// is, this form specifies the last N bytes of an entity-body.) If
        /// the entity is shorter than the specified suffix-length, the
        /// entire entity-body is used.
        /// 
        /// If a syntactically valid byte-range-set includes at least one
        /// byte- range-spec whose first-byte-pos is less than the current
        /// length of the entity-body, or at least one suffix-byte-range-spec
        /// with a non- zero suffix-length, then the byte-range-set is
        /// satisfiable. Otherwise, the byte-range-set is unsatisfiable.
        /// If the byte-range-set is unsatisfiable, the server SHOULD return
        /// a response with a status of 416 (Requested range not satisfiable).
        /// Otherwise, the server SHOULD return a response with a status of
        /// 206 (Partial Content) containing the satisfiable ranges of the
        /// entity-body.
        /// 
        /// Examples of byte-ranges-specifier values (assuming an entity-body
        /// of length 10000):
        /// 
        /// The first 500 bytes (byte offsets 0-499, inclusive):
        /// bytes=0-499
        /// 
        /// The second 500 bytes (byte offsets 500-999, inclusive):
        /// bytes=500-999
        /// 
        /// The final 500 bytes (byte offsets 9500-9999, inclusive):
        /// bytes=-500
        /// bytes=9500-
        /// 
        /// The first and last bytes only (bytes 0 and 9999):
        /// bytes=0-0,-1
        /// 
        /// Several legal but not canonical specifications of the second
        /// 500 bytes (byte offsets 500-999, inclusive):
        /// bytes=500-600,601-999
        /// bytes=500-700,601-999
        /// 
        /// HTTP retrieval requests using conditional or unconditional GET
        /// methods MAY request one or more sub-ranges of the entity, instead
        /// of the entire entity, using the Range request header, which
        /// applies to the entity returned as the result of the request:
        /// 
        /// A server MAY ignore the Range header. However, HTTP/1.1 origin
        /// servers and intermediate caches ought to support byte ranges when
        /// possible, since Range supports efficient recovery from partially
        /// failed transfers, and supports efficient partial retrieval of
        /// large entities.
        /// 
        /// If the server supports the Range header and the specified range or
        /// ranges are appropriate for the entity:
        /// 
        /// The presence of a Range header in an unconditional GET modifies what
        /// is returned if the GET is otherwise successful. In other words, the
        /// response carries a status code of 206 (Partial Content) instead of
        /// 200 (OK).
        /// 
        /// The presence of a Range header in a conditional GET (a request using
        /// one or both of If-Modified-Since and If-None-Match, or one or both
        /// of If-Unmodified-Since and If-Match) modifies what is returned if
        /// the GET is otherwise successful and the condition is true. It does
        /// not affect the 304 (Not Modified) response returned if the
        /// conditional is false.
        /// 
        /// In some cases, it might be more appropriate to use the If-Range
        /// header (see section 14.27) in addition to the Range header.
        /// 
        /// If a proxy that supports ranges receives a Range request, forwards
        /// the request to an inbound server, and receives an entire entity in
        /// reply, it SHOULD only return the requested range to its client. It
        /// SHOULD store the entire received response in its cache if that is
        /// consistent with its cache allocation policies.
        /// </summary>
        /// <example>Range: bytes=500-999</example>
        /// <example>Range: bytes=500-600,601-999</example>
        /// <example>Range: bytes=500-</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Range = new HTTPHeaderField("Range",
                                                                           typeof(String),
                                                                           HeaderFieldType.Request,
                                                                           RequestPathSemantic.EndToEnd);

        #endregion

        #region Referer

        /// <summary>
        /// The Referer[sic] request-header field allows the client to specify,
        /// for the server's benefit, the address (URI) of the resource from
        /// which the Request-URI was obtained (the "referrer", although the
        /// header field is misspelled.) The Referer request-header allows a
        /// server to generate lists of back-links to resources for interest,
        /// logging, optimized caching, etc. It also allows obsolete or
        /// mistyped links to be traced for maintenance. The Referer field
        /// MUST NOT be sent if the Request-URI was obtained from a source
        /// that does not have its own URI, such as input from the user
        /// keyboard.
        /// 
        /// If the field value is a relative URI, it SHOULD be interpreted
        /// relative to the Request-URI. The URI MUST NOT include a fragment.
        /// See section 15.1.3 for security considerations.
        /// </summary>
        /// <example>Referer: DataSources/Overview.html</example>
        /// <example>Referer: http://www.w3.org/hypertext/DataSources/Overview.html </example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Referer = new HTTPHeaderField("Referer",
                                                                             typeof(String),
                                                                             HeaderFieldType.Request,
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

        #region TE

        /// <summary>
        /// The TE request-header field indicates what extension transfer-codings
        /// it is willing to accept in the response and whether or not it is
        /// willing to accept trailer fields in a chunked transfer-coding. Its
        /// value may consist of the keyword "trailers" and/or a comma-separated
        /// list of extension transfer-coding names with optional accept
        /// parameters (as described in section 3.6).
        /// 
        /// The presence of the keyword "trailers" indicates that the client is
        /// willing to accept trailer fields in a chunked transfer-coding, as
        /// defined in section 3.6.1. This keyword is reserved for use with
        /// transfer-coding values even though it does not itself represent a
        /// transfer-coding.
        /// 
        /// The TE header field only applies to the immediate connection.
        /// Therefore, the keyword MUST be supplied within a Connection header
        /// field (section 14.10) whenever TE is present in an HTTP/1.1 message.
        /// 
        /// A server tests whether a transfer-coding is acceptable, according to
        /// a TE field, using these rules:
        /// 
        /// The "chunked" transfer-coding is always acceptable. If the keyword
        /// "trailers" is listed, the client indicates that it is willing to
        /// accept trailer fields in the chunked response on behalf of itself
        /// and any downstream clients. The implication is that, if given, the
        /// client is stating that either all downstream clients are willing to
        /// accept trailer fields in the forwarded response, or that it will
        /// attempt to buffer the response on behalf of downstream recipients.
        /// 
        /// Note: HTTP/1.1 does not define any means to limit the size of a
        /// chunked response such that a client can be assured of buffering
        /// the entire response.
        /// 
        /// If the transfer-coding being tested is one of the transfer-codings
        /// listed in the TE field, then it is acceptable unless it is accompanied
        /// by a qvalue of 0. (As defined in section 3.9, a qvalue of 0 means
        /// "not acceptable.")
        /// 
        /// If multiple transfer-codings are acceptable, then the acceptable
        /// transfer-coding with the highest non-zero qvalue is preferred. The
        /// "chunked" transfer-coding always has a qvalue of 1.
        /// 
        /// If the TE field-value is empty or if no TE field is present, the only
        /// transfer-coding is "chunked". A message with no transfer-coding is
        /// always acceptable.
        /// </summary>
        /// <example>TE: deflate</example>
        /// <example>TE: trailers, deflate;q=0.5</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField TE = new HTTPHeaderField("TE",
                                                                        typeof(String),
                                                                        HeaderFieldType.Request,
                                                                        RequestPathSemantic.EndToEnd);

        #endregion

        #region Timeout

        /// <summary>
        /// Clients MAY include Timeout request headers in their LOCK requests.
        /// However, the server is not required to honor or even consider these
        /// requests. Clients MUST NOT submit a Timeout request header with any
        /// method other than a LOCK method.
        /// 
        /// The "Second" TimeType specifies the number of seconds that will
        /// elapse between granting of the lock at the server, and the automatic
        /// removal of the lock. The timeout value for TimeType "Second" MUST
        /// NOT be greater than 2^32-1.
        /// 
        /// See Section 6.6 for a description of lock timeout behavior.
        /// </summary>
        /// <example>Timeout: 120</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Timeout = new HTTPHeaderField("Timeout",
                                                                             typeof(UInt64?),
                                                                             HeaderFieldType.Request,
                                                                             RequestPathSemantic.EndToEnd,
                                                                             StringParsers.NullableUInt64);

        #endregion

        #region User-Agent

        /// <summary>
        /// The User-Agent request-header field contains information about
        /// the user agent originating the request. This is for statistical
        /// purposes, the tracing of protocol violations, and automated
        /// recognition of user agents for the sake of tailoring responses
        /// to avoid particular user agent limitations. User agents SHOULD
        /// include this field with requests. The field can contain multiple
        /// product tokens (section 3.8) and comments identifying the agent
        /// and any subproducts which form a significant part of the user
        /// agent. By convention, the product tokens are listed in order of
        /// their significance for identifying the application. 
        /// </summary>
        /// <example>User-Agent: CERN-LineMode/2.15 libwww/2.17b3</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField UserAgent = new HTTPHeaderField("User-Agent",
                                                                               typeof(UInt64?),
                                                                               HeaderFieldType.Request,
                                                                               RequestPathSemantic.EndToEnd);

        #endregion

        #region Last-Event-Id

        /// <summary>
        /// This specification defines an API for opening an HTTP connection
        /// for receiving push notifications from a server in the form of DOM
        /// events. The API is designed such that it can be extended to work
        /// with other push notification schemes such as Push SMS.
        /// </summary>
        /// <example>Last-Event-Id: 123</example>
        /// <seealso cref="http://dev.w3.org/html5/eventsource/"/>
        public static readonly HTTPHeaderField LastEventId = new HTTPHeaderField("Last-Event-Id",
                                                                                 typeof(UInt64?),
                                                                                 HeaderFieldType.Request,
                                                                                 RequestPathSemantic.EndToEnd,
                                                                                 StringParsers.NullableUInt64);

        #endregion

        #region Cookie

        /// <summary>
        /// Send a HTTP cookie.
        /// </summary>
        /// <example>Cookie: UserID=JohnDoe; Max-Age=3600; Version=1</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/HTTP_cookie"/>
        public static readonly HTTPHeaderField Cookie = new HTTPHeaderField("Cookie",
                                                                            typeof(String),
                                                                            HeaderFieldType.Request,
                                                                            RequestPathSemantic.EndToEnd);

        #endregion

        #region DNT

        /// <summary>
        /// With Do Not Track: A user enables Do Not Track in her web browser.
        /// She navigates a sequence of popular websites, many of which
        /// incorporate content from a major advertising network.  The
        /// advertising network delivers advertisements, but refrains from THIRD-
        /// PARTY TRACKING of the user.
        /// </summary>
        /// <example>DNT: 1</example>
        /// <seealso cref="https://tools.ietf.org/html/draft-mayer-do-not-track-00"/>
        public static readonly HTTPHeaderField DNT = new HTTPHeaderField("DNT",
                                                                         typeof(Boolean),
                                                                         HeaderFieldType.Request,
                                                                         RequestPathSemantic.EndToEnd);

        #endregion

        #endregion

        #region Non-standard request header fields

        #region X-Real-IP

        /// <summary>
        /// Intermediary HTTP proxies might include this field to
        /// indicate the real IP address of the HTTP client.
        /// </summary>
        /// <example>X-Real-IP: 95.91.73.30</example>
        public static readonly HTTPHeaderField X_Real_IP = new HTTPHeaderField("X-Real-IP",
                                                                               typeof(IIPAddress),
                                                                               HeaderFieldType.Request,
                                                                               RequestPathSemantic.HopToHop);

        #endregion

        #region X-Forwarded-For

        /// <summary>
        /// Intermediary HTTP proxies might include this field to
        /// indicate the real IP address of the HTTP client.
        /// </summary>
        /// <example>X-Forwarded-For: 95.91.73.30</example>
        public static readonly HTTPHeaderField X_Forwarded_For = new HTTPHeaderField("X-Forwarded-For",
                                                                                     typeof(IIPAddress),
                                                                                     HeaderFieldType.Request,
                                                                                     RequestPathSemantic.HopToHop);

        #endregion

        #region API_Key

        /// <summary>
        /// An API key for authentication.
        /// </summary>
        /// <example>API-Key: vfsf87wefh8743tzfgw9f489fh9fgs9z9z237hd208du79ehcv86egfsrf</example>
        public static readonly HTTPHeaderField API_Key = new HTTPHeaderField("API-Key",
                                                                             typeof(APIKey_Id),
                                                                             HeaderFieldType.Request,
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

        #region X-ExpectedTotalNumberOfItems

        /// <summary>
        /// The expected total number of items within a resource collection.
        /// </summary>
        /// <example>X-ExpectedTotalNumberOfItems: 42</example>
        public static readonly HTTPHeaderField X_ExpectedTotalNumberOfItems = new HTTPHeaderField("X-ExpectedTotalNumberOfItems",
                                                                                                  typeof(UInt64),
                                                                                                  HeaderFieldType.Response,
                                                                                                  RequestPathSemantic.EndToEnd);

        #endregion

        #region X-Frame-Options

        /// <summary>
        /// The X-Frame-Options HTTP response header can be used to indicate whether or not a browser
        /// should be allowed to render a page in a &lt;frame&gt;, &lt;iframe&gt; or &lt;object&gt;.
        /// Sites can use this to avoid clickjacking attacks, by ensuring that their content is not
        /// embedded into other sites.
        /// </summary>
        /// <example>DENY, SAMEORIGIN, ALLOW-FROM https://example.com</example>
        public static readonly HTTPHeaderField X_FrameOptions = new HTTPHeaderField("X-Frame-Options",
                                                                                    typeof(String),
                                                                                    HeaderFieldType.Response,
                                                                                    RequestPathSemantic.EndToEnd);

        #endregion

        #region X-Portal

        /// <summary>
        /// This is a non-standard HTTP header to idicate that the intended
        /// HTTP portal is calling. By this a special HTTP content type processing
        /// might be implemented, which is different from the processing of other
        /// HTTP client requests.
        /// </summary>
        /// <example>X-Portal: true</example>
        public static readonly HTTPHeaderField X_Portal = new HTTPHeaderField("X-Portal",
                                                                              typeof(Boolean),
                                                                              HeaderFieldType.Request,
                                                                              RequestPathSemantic.EndToEnd);

        #endregion

        #endregion


        #region Response header fields

        #region Age

        /// <summary>
        /// The Age response-header field conveys the sender's estimate of
        /// the amount of time since the response (or its revalidation)
        /// was generated at the origin server. A cached response is
        /// "fresh" if its age does not exceed its freshness lifetime. Age
        /// values are calculated as specified in section 13.2.3.
        /// 
        /// Age values are non-negative decimal integers, representing
        /// time in seconds.
        /// 
        /// If a cache receives a value larger than the largest positive
        /// integer it can represent, or if any of its age calculations
        /// overflows, it MUST transmit an Age header with a value of
        /// 2147483648 (2^31). An HTTP/1.1 server that includes a cache
        /// MUST include an Age header field in every response generated
        /// from its own cache. Caches SHOULD use an arithmetic type of
        /// at least 31 bits of range.
        /// </summary>
        /// <example>Age: 1234</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Age = new HTTPHeaderField("Age",
                                                                         typeof(UInt64?),
                                                                         HeaderFieldType.Response,
                                                                         RequestPathSemantic.EndToEnd,
                                                                         StringParsers.NullableUInt64);

        #endregion

        #region Allow

        /// <summary>
        /// The Allow entity-header field lists the set of methods
        /// supported by the resource identified by the Request-URI.
        /// The purpose of this field is strictly to inform the
        /// recipient of valid methods associated with the resource.
        /// An Allow header field MUST be present in a 405 (Method
        /// Not Allowed) response.
        /// 
        /// This field cannot prevent a client from trying other
        /// methods. However, the indications given by the Allow
        /// header field value SHOULD be followed. The actual set of
        /// allowed methods is defined by the origin server at the
        /// time of each request.
        /// 
        /// The Allow header field MAY be provided with a PUT request
        /// to recommend the methods to be supported by the new or
        /// modified resource. The server is not required to support
        /// these methods and SHOULD include an Allow header in the
        /// response giving the actual supported methods.
        /// 
        /// A proxy MUST NOT modify the Allow header field even if it
        /// does not understand all the methods specified, since the
        /// user agent might have other means of communicating with
        /// the origin server.
        /// </summary>
        /// <example>Allow: GET, HEAD, PUT</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Allow = new HTTPHeaderField("Allow",
                                                                           typeof(String),
                                                                           HeaderFieldType.Response,
                                                                           RequestPathSemantic.EndToEnd);

        #endregion

        #region  Accept-Patch

        public static readonly HTTPHeaderField AcceptPatch = new HTTPHeaderField("Accept-Patch",
                                                                                 typeof(String),
                                                                                 HeaderFieldType.Response,
                                                                                 RequestPathSemantic.EndToEnd);

        #endregion

        #region DAV

        /// <summary>
        /// This general-header appearing in the response indicates that
        /// the resource supports the DAV schema and protocol as specified.
        /// All DAV-compliant resources MUST return the DAV header with
        /// compliance-class "1" on all OPTIONS responses. In cases where
        /// WebDAV is only supported in part of the server namespace, an
        /// OPTIONS request to non-WebDAV resources (including "/") SHOULD
        /// NOT advertise WebDAV support.
        /// 
        /// The value is a comma-separated list of all compliance class
        /// identifiers that the resource supports. Class identifiers may
        /// be Coded-URLs or tokens (as defined by [RFC2616]). Identifiers
        /// can appear in any order. Identifiers that are standardized
        /// through the IETF RFC process are tokens, but other identifiers
        /// SHOULD be Coded-URLs to encourage uniqueness.
        /// 
        /// A resource must show class 1 compliance if it shows class 2 or
        /// 3 compliance. In general, support for one compliance class does
        /// not entail support for any other, and in particular, support for
        /// compliance class 3 does not require support for compliance class
        /// 2. Please refer to Section 18 for more details on compliance
        /// classes defined in this specification.
        /// 
        /// Note that many WebDAV servers do not advertise WebDAV support
        /// in response to "OPTIONS *".
        /// 
        /// As a request header, this header allows the client to advertise
        /// compliance with named features when the server needs that
        /// information. Clients SHOULD NOT send this header unless a
        /// standards track specification requires it. Any extension that
        /// makes use of this as a request header will need to carefully
        /// consider caching implications.
        /// </summary>
        /// <example>DAV : 1</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField DAV = new HTTPHeaderField("DAV",
                                                                         typeof(String),
                                                                         HeaderFieldType.Response,
                                                                         RequestPathSemantic.EndToEnd);

        #endregion

        #region ETag

        /// <summary>
        /// The ETag response-header field provides the current value
        /// of the entity tag for the requested variant. The headers
        /// used with entity tags are described in sections 14.24,
        /// 14.26 and 14.44. The entity tag MAY be used for comparison
        /// with other entities from the same resource (see section 13.3.3). 
        /// </summary>
        /// <example>ETag: "737060cd8c284d8af7ad3082f209582d"</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ETag = new HTTPHeaderField("ETag",
                                                                          typeof(String),
                                                                          HeaderFieldType.Response,
                                                                          RequestPathSemantic.EndToEnd);

        #endregion

        #region Expires

        /// <summary>
        /// The Expires entity-header field gives the date/time after
        /// which the response is considered stale. A stale cache entry
        /// may not normally be returned by a cache (either a proxy
        /// cache or a user agent cache) unless it is first validated
        /// with the origin server (or with an intermediate cache that
        /// has a fresh copy of the entity). See section 13.2 for
        /// further discussion of the expiration model.
        /// 
        /// The presence of an Expires field does not imply that the
        /// original resource will change or cease to exist at, before,
        /// or after that time.
        /// 
        /// The format is an absolute date and time as defined by HTTP-
        /// date in section 3.3.1; it MUST be in RFC 1123 date format.
        /// 
        /// If a response includes a Cache-Control field with the
        /// max-age directive (see section 14.9.3), that directive
        /// overrides the Expires field.
        /// 
        /// HTTP/1.1 clients and caches MUST treat other invalid date
        /// formats, especially including the value "0", as in the past
        /// (i.e., "already expired").
        /// 
        /// To mark a response as "already expired," an origin server
        /// sends an Expires date that is equal to the Date header
        /// value. (See the rules for expiration calculations in
        /// section 13.2.4.)
        /// 
        /// To mark a response as "never expires," an origin server
        /// sends an Expires date approximately one year from the time
        /// the response is sent. HTTP/1.1 servers SHOULD NOT send
        /// Expires dates more than one year in the future.
        /// 
        /// The presence of an Expires header field with a date value of
        /// some time in the future on a response that otherwise would
        /// by default be non-cacheable indicates that the response is
        /// cacheable, unless indicated otherwise by a Cache-Control
        /// header field (section 14.9).
        /// </summary>
        /// <example>Expires: Thu, 01 Dec 1994 16:00:00 GMT</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Expires = new HTTPHeaderField("Expires",
                                                                             typeof(String),
                                                                             HeaderFieldType.Response,
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

        #region Last-Modified

        /// <summary>
        /// The Keep-Alive general-header field may be used to include diagnostic information and
        /// other optional parameters associated with the "keep-alive" keyword of the Connection
        /// header field (Section 10.9). This Keep-Alive field must only be used when the
        /// "keep-alive" keyword is present (Section 10.9.1).
        /// 
        /// The Keep-Alive header field and the additional information it provides are optional
        /// and do not need to be present to indicate a persistent connection has been established.
        /// The semantics of the Connection header field prevent the Keep-Alive field from being
        /// accidentally forwarded to downstream connections.
        /// 
        /// HTTP/1.1 defines semantics for the optional "timeout" and "max" parameters on responses;
        /// other parameters may be added and the field may also be used on request messages. The
        /// "timeout" parameter allows the server to indicate, for diagnostic purposes only, the
        /// amount of time in seconds it is currently allowing between when the response was
        /// generated and when the next request is received from the client (i.e., the request
        /// timeout limit). Similarly, the "max" parameter allows the server to indicate the
        /// maximum additional requests that it will allow on the current persistent connection.
        /// 
        /// For example, the server may respond to a request for a persistent connection with
        /// 
        ///   Connection: Keep-Alive
        ///   Keep-Alive: timeout=10, max=5
        /// 
        /// to indicate that the server has selected (perhaps dynamically) a maximum of 5 requests,
        /// but will timeout the connection if the next request is not received within 10 seconds.
        /// Although these parameters have no affect on the operational requirements of the
        /// connection, they are sometimes useful for testing functionality and monitoring server
        /// behavior.
        /// </summary>
        /// <example>Keep-Alive: timeout=10, max=5</example>
        /// <seealso cref="http://www.w3.org/Protocols/HTTP/1.1/draft-ietf-http-v11-spec-01.html"/>
        public static readonly HTTPHeaderField KeepAlive = new HTTPHeaderField("Keep-Alive",
                                                                               typeof(KeepAliveType),
                                                                               HeaderFieldType.Response,
                                                                               RequestPathSemantic.HopToHop);

        #endregion

        #region Last-Modified

        /// <summary>
        /// The Last-Modified entity-header field indicates the date
        /// and time at which the origin server believes the variant
        /// was last modified.
        /// 
        /// The exact meaning of this header field depends on the
        /// implementation of the origin server and the nature of
        /// the original resource. For files, it may be just the
        /// file system last-modified time. For entities with
        /// dynamically included parts, it may be the most recent
        /// of the set of last-modify times for its component parts.
        /// For database gateways, it may be the last-update time
        /// stamp of the record. For virtual objects, it may be the
        /// last time the internal state changed.
        /// 
        /// An origin server MUST NOT send a Last-Modified date which
        /// is later than the server's time of message origination.
        /// In such cases, where the resource's last modification
        /// would indicate some time in the future, the server MUST
        /// replace that date with the message origination date.
        /// 
        /// An origin server SHOULD obtain the Last-Modified value of
        /// the entity as close as possible to the time that it
        /// generates the Date value of its response. This allows a
        /// recipient to make an accurate assessment of the entity's
        /// modification time, especially if the entity changes near
        /// the time that the response is generated.
        /// 
        /// HTTP/1.1 servers SHOULD send Last-Modified whenever feasible.
        /// </summary>
        /// <example>Last-Modified: Tue, 15 Nov 1994 12:45:26 GMT</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField LastModified = new HTTPHeaderField("Last-Modified",
                                                                                  typeof(String),
                                                                                  HeaderFieldType.Response,
                                                                                  RequestPathSemantic.EndToEnd);

        #endregion

        #region Location

        /// <summary>
        /// The Location response-header field is used to redirect the
        /// recipient to a location other than the Request-URI for
        /// completion of the request or identification of a new
        /// resource. For 201 (Created) responses, the Location is that
        /// of the new resource which was created by the request. For
        /// 3xx responses, the location SHOULD indicate the server's
        /// preferred URI for automatic redirection to the resource.
        /// The field value consists of a single absolute URI.
        /// 
        /// Note: The Content-Location header field (section 14.14)
        /// differs from Location in that the Content-Location identifies
        /// the original location of the entity enclosed in the request.
        /// It is therefore possible for a response to contain header
        /// fields for both Location and Content-Location. Also see
        /// section 13.10 for cache requirements of some methods.
        /// </summary>
        /// <example>Location: http://www.w3.org/pub/WWW/People.html </example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Location = new HTTPHeaderField("Location",
                                                                              typeof(String),
                                                                              HeaderFieldType.Response,
                                                                              RequestPathSemantic.EndToEnd);

        #endregion

        #region Proxy-Authenticate

        /// <summary>
        /// The Proxy-Authenticate response-header field MUST be included
        /// as part of a 407 (Proxy Authentication Required) response. The
        /// field value consists of a challenge that indicates the
        /// authentication scheme and parameters applicable to the proxy
        /// for this Request-URI.
        /// 
        /// The HTTP access authentication process is described in "HTTP
        /// Authentication: Basic and Digest Access Authentication" [43].
        /// Unlike WWW-Authenticate, the Proxy-Authenticate header field
        /// applies only to the current connection and SHOULD NOT be passed
        /// on to downstream clients. However, an intermediate proxy might
        /// need to obtain its own credentials by requesting them from the
        /// downstream client, which in some circumstances will appear as
        /// if the proxy is forwarding the Proxy-Authenticate header field.
        /// </summary>
        /// <example>Proxy-Authenticate: Basic</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField ProxyAuthenticate = new HTTPHeaderField("Proxy-Authenticate",
                                                                                       typeof(String),
                                                                                       HeaderFieldType.Response,
                                                                                       RequestPathSemantic.HopToHop);

        #endregion

        #region Retry-After

        /// <summary>
        /// The Retry-After response-header field can be used with a
        /// 503 (Service Unavailable) response to indicate how long
        /// the service is expected to be unavailable to the requesting
        /// client. This field MAY also be used with any 3xx (Redirection)
        /// response to indicate the minimum time the user-agent is asked
        /// wait before issuing the redirected request. The value of this
        /// field can be either an HTTP-date or an integer number of
        /// seconds (in decimal) after the time of the response. 
        /// </summary>
        /// <example>Retry-After: Fri, 31 Dec 1999 23:59:59 GMT</example>
        /// <example>Retry-After: 120</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField RetryAfter = new HTTPHeaderField("Retry-After",
                                                                                typeof(String),
                                                                                HeaderFieldType.Response,
                                                                                RequestPathSemantic.EndToEnd);

        #endregion

        #region Server

        /// <summary>
        /// The Server response-header field contains information about
        /// the software used by the origin server to handle the request.
        /// The field can contain multiple product tokens (section 3.8)
        /// and comments identifying the server and any significant
        /// subproducts. The product tokens are listed in order of their
        /// significance for identifying the application.
        /// 
        /// If the response is being forwarded through a proxy, the proxy
        /// application MUST NOT modify the Server response-header. Instead,
        /// it SHOULD include a Via field (as described in section 14.45).
        /// 
        /// Note: Revealing the specific software version of the server
        /// might allow the server machine to become more vulnerable to
        /// attacks against software that is known to contain security
        /// holes. Server implementors are encouraged to make this field
        /// a configurable option.
        /// </summary>
        /// <example>Server: CERN/3.0 libwww/2.17</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Server = new HTTPHeaderField("Server",
                                                                            typeof(String),
                                                                            HeaderFieldType.Response,
                                                                            RequestPathSemantic.EndToEnd);

        #endregion

        #region Vary

        /// <summary>
        /// The Vary field value indicates the set of request-header
        /// fields that fully determines, while the response is fresh,
        /// whether a cache is permitted to use the response to reply
        /// to a subsequent request without revalidation. For uncacheable
        /// or stale responses, the Vary field value advises the user
        /// agent about the criteria that were used to select the
        /// representation. A Vary field value of "*" implies that a
        /// cache cannot determine from the request headers of a
        /// subsequent request whether this response is the appropriate
        /// representation. See section 13.6 for use of the Vary header
        /// field by caches.
        /// 
        /// An HTTP/1.1 server SHOULD include a Vary header field with
        /// any cacheable response that is subject to server-driven
        /// negotiation. Doing so allows a cache to properly interpret
        /// future requests on that resource and informs the user agent
        /// about the presence of negotiation on that resource. A server
        /// MAY include a Vary header field with a non-cacheable response
        /// that is subject to server-driven negotiation, since this might
        /// provide the user agent with useful information about the
        /// dimensions over which the response varies at the time of
        /// the response.
        /// 
        /// A Vary field value consisting of a list of field-names signals
        /// that the representation selected for the response is based on
        /// a selection algorithm which considers ONLY the listed request-
        /// header field values in selecting the most appropriate
        /// representation. A cache MAY assume that the same selection will
        /// be made for future requests with the same values for the listed
        /// field names, for the duration of time for which the response is
        /// fresh.
        /// 
        /// The field-names given are not limited to the set of standard
        /// request-header fields defined by this specification. Field
        /// names are case-insensitive.
        /// 
        /// A Vary field value of '*' signals that unspecified parameters
        /// not limited to the request-headers (e.g., the network address
        /// of the client), play a role in the selection of the response
        /// representation. The "*" value MUST NOT be generated by a proxy
        /// server; it may only be generated by an origin server.
        /// </summary>
        /// <example>Vary: *</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField Vary = new HTTPHeaderField("Vary",
                                                                          typeof(String),
                                                                          HeaderFieldType.Response,
                                                                          RequestPathSemantic.EndToEnd);

        #endregion

        #region WWW-Authenticate

        /// <summary>
        /// The WWW-Authenticate response-header field MUST be included
        /// in 401 (Unauthorized) response messages. The field value
        /// consists of at least one challenge that indicates the
        /// authentication scheme(s) and parameters applicable to the
        /// Request-URI.
        /// 
        /// The HTTP access authentication process is described in 'HTTP
        /// Authentication: Basic and Digest Access Authentication' [43].
        /// User agents are advised to take special care in parsing the
        /// WWW-Authenticate field value as it might contain more than
        /// one challenge, or if more than one WWW-Authenticate header
        /// field is provided, the contents of a challenge itself can
        /// contain a comma-separated list of authentication parameters. 
        /// </summary>
        /// <example>WWW-Authenticate: Basic</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField WWWAuthenticate = new HTTPHeaderField("WWW-Authenticate",
                                                                                     typeof(String),
                                                                                     HeaderFieldType.Response,
                                                                                     RequestPathSemantic.EndToEnd);

        #endregion

        #region Refresh

        /// <summary>
        /// Used in redirection, or when a new resource has been created. This
        /// refresh redirects after 5 seconds. This is a proprietary, non-standard
        /// header extension introduced by Netscape and supported by most web browsers.
        /// </summary>
        /// <example>Refresh: 5; url=http://www.w3.org/pub/WWW/People.html </example>
        /// <seealso cref="http://en.wikipedia.org/wiki/List_of_HTTP_header_fields"/>
        public static readonly HTTPHeaderField Refresh = new HTTPHeaderField("Refresh",
                                                                             typeof(String),
                                                                             HeaderFieldType.Response,
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

        #region Set-Cookie

        /// <summary>
        /// Set a HTTP cookie.
        /// </summary>
        /// <example>Set-Cookie: UserID=JohnDoe; Max-Age=3600; Version=1</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/HTTP_cookie"/>
        public static readonly HTTPHeaderField SetCookie = new HTTPHeaderField("Set-Cookie",
                                                                               typeof(String),
                                                                               HeaderFieldType.Response,
                                                                               RequestPathSemantic.EndToEnd);

        #endregion

        #region CORS

        #region Access-Control-Allow-Origin

        /// <summary>
        /// Access-Control-Allow-Origin.
        /// </summary>
        /// <example>Access-Control-Allow-Origin: *</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/Cross-origin_resource_sharing"/>
        public static readonly HTTPHeaderField AccessControlAllowOrigin = new HTTPHeaderField("Access-Control-Allow-Origin",
                                                                                              typeof(String),
                                                                                              HeaderFieldType.Response,
                                                                                              RequestPathSemantic.EndToEnd);

        #endregion

        #region Access-Control-Allow-Methods

        /// <summary>
        /// Access-Control-Allow-Methods.
        /// </summary>
        /// <example>Access-Control-Allow-Methods: GET, PUT, POST, DELETE</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/Cross-origin_resource_sharing"/>
        public static readonly HTTPHeaderField AccessControlAllowMethods = new HTTPHeaderField("Access-Control-Allow-Methods",
                                                                                               typeof(String),
                                                                                               HeaderFieldType.Response,
                                                                                               RequestPathSemantic.EndToEnd);

        #endregion

        #region Access-Control-Allow-Headers

        /// <summary>
        /// Access-Control-Allow-Headers.
        /// </summary>
        /// <example>Access-Control-Allow-Headers: Content-Type</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/Cross-origin_resource_sharing"/>
        public static readonly HTTPHeaderField AccessControlAllowHeaders = new HTTPHeaderField("Access-Control-Allow-Headers",
                                                                                               typeof(String),
                                                                                               HeaderFieldType.Response,
                                                                                               RequestPathSemantic.EndToEnd);

        #endregion

        #region Access-Control-Max-Age

        /// <summary>
        /// The Access-Control-Max-Age response header indicates how long the results
        /// of a preflight request (that is the information contained in the
        /// Access-Control-Allow-Methods and Access-Control-Allow-Headers headers)
        /// can be cached.
        /// </summary>
        /// <example>Access-Control-Max-Age: delta-seconds</example>
        /// <seealso cref="https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Access-Control-Max-Age"/>
        public static readonly HTTPHeaderField AccessControlMaxAge = new HTTPHeaderField("Access-Control-Max-Age",
                                                                                         typeof(Int64),
                                                                                         HeaderFieldType.Response,
                                                                                         RequestPathSemantic.EndToEnd);

        #endregion

        #endregion

        #endregion

        #region Non-standard response header fields

        #region XLocationAfterAuth

        /// <summary>
        /// Stores the original HTTP path and redirects to it after authentication.
        /// </summary>
        public static readonly HTTPHeaderField XLocationAfterAuth = new HTTPHeaderField("X-LocationAfterAuth",
                                                                                        typeof(String),
                                                                                        HeaderFieldType.Response,
                                                                                        RequestPathSemantic.EndToEnd);

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator == (HTTPHeaderField AHTTPHeaderField1, HTTPHeaderField AHTTPHeaderField2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(AHTTPHeaderField1, AHTTPHeaderField2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) AHTTPHeaderField1 == null) || ((Object) AHTTPHeaderField2 == null))
                return false;

            return AHTTPHeaderField1.Equals(AHTTPHeaderField2);

        }

        #endregion

        #region Operator != (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator != (HTTPHeaderField AHTTPHeaderField1, HTTPHeaderField AHTTPHeaderField2)
        {
            return !(AHTTPHeaderField1 == AHTTPHeaderField2);
        }

        #endregion

        #region Operator <  (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator < (HTTPHeaderField AHTTPHeaderField1, HTTPHeaderField AHTTPHeaderField2)
        {

            // Check if AHTTPHeaderField1 is null
            if ((Object) AHTTPHeaderField1 == null)
                throw new ArgumentNullException("Parameter AHTTPHeaderField1 must not be null!");

            // Check if AHTTPHeaderField2 is null
            if ((Object) AHTTPHeaderField2 == null)
                throw new ArgumentNullException("Parameter AHTTPHeaderField2 must not be null!");

            return AHTTPHeaderField1.CompareTo(AHTTPHeaderField2) < 0;

        }

        #endregion

        #region Operator >  (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator > (HTTPHeaderField AHTTPHeaderField1, HTTPHeaderField AHTTPHeaderField2)
        {

            // Check if AHTTPHeaderField1 is null
            if ((Object) AHTTPHeaderField1 == null)
                throw new ArgumentNullException("Parameter AHTTPHeaderField1 must not be null!");

            // Check if AHTTPHeaderField2 is null
            if ((Object) AHTTPHeaderField2 == null)
                throw new ArgumentNullException("Parameter AHTTPHeaderField2 must not be null!");

            return AHTTPHeaderField1.CompareTo(AHTTPHeaderField2) > 0;

        }

        #endregion

        #region Operator <= (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator <= (HTTPHeaderField AHTTPHeaderField1, HTTPHeaderField AHTTPHeaderField2)
        {
            return !(AHTTPHeaderField1 > AHTTPHeaderField2);
        }

        #endregion

        #region Operator >= (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator >= (HTTPHeaderField AHTTPHeaderField1, HTTPHeaderField AHTTPHeaderField2)
        {
            return !(AHTTPHeaderField1 < AHTTPHeaderField2);
        }

        #endregion

        #endregion

        #region IComparable<AHTTPHeaderField> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is an AHTTPHeaderField.
            var HTTPRequestField = Object as HTTPHeaderField;
            if ((Object) HTTPRequestField == null)
                throw new ArgumentException("The given object is not a AHTTPHeaderField!");

            return CompareTo(HTTPRequestField);

        }

        #endregion

        #region CompareTo(AHTTPHeaderField)

        public Int32 CompareTo(HTTPHeaderField AHTTPHeaderField)
        {

            if ((Object) AHTTPHeaderField == null)
                throw new ArgumentNullException("The given AHTTPHeaderField must not be null!");

            return Name.CompareTo(AHTTPHeaderField.Name);

        }

        #endregion

        #endregion

        #region IEquatable<AHTTPHeaderField> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            // Check if the given object is an AHTTPHeaderField.
            var HTTPRequestField = Object as HTTPHeaderField;
            if ((Object) HTTPRequestField == null)
                return false;

            return this.Equals(HTTPRequestField);

        }

        #endregion

        #region Equals(AHTTPHeaderField)

        /// <summary>
        /// Compares two AHTTPHeaderFields for equality.
        /// </summary>
        /// <param name="AHTTPHeaderField">An AHTTPHeaderField to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPHeaderField AHTTPHeaderField)
        {

            if ((Object) AHTTPHeaderField == null)
                return false;

            return Name == AHTTPHeaderField.Name;

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            return Name.GetHashCode();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {
            return Name;
        }

        #endregion

    }

    #endregion

}
