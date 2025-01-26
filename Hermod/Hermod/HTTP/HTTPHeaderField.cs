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

using System.Collections.Generic;
using System.Text;

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

        #region NullableInt64               (String, out Object)

        ///// <summary>
        ///// A delegate to parse a Int64? value from a string.
        ///// </summary>
        ///// <param name="String">The string to be parsed.</param>
        ///// <param name="Object">The parsed Int64? value.</param>
        //public static Boolean NullableInt64(String String, out Object? Object)
        //{

        //    if (Int64.TryParse(String, out var value))
        //    {
        //        Object = value;
        //        return true;
        //    }

        //    Object = null;
        //    return false;

        //}

        #endregion

        #region NullableUInt64              (String, out Integer)

        /// <summary>
        /// A delegate to parse a UInt64? value from a string.
        /// </summary>
        /// <param name="String">The string to be parsed.</param>
        /// <param name="Integer">The parsed UInt64 value.</param>
        public static Boolean NullableUInt64(String String, out UInt64? Integer)
        {

            if (UInt64.TryParse(String, out var uint64))
            {
                Integer = uint64;
                return true;
            }

            Integer = null;
            return false;

        }

        #endregion

        #region NullableTimeSpan            (String, out Integer)

        /// <summary>
        /// A delegate to parse a time span value from a string.
        /// </summary>
        /// <param name="String">The string to be parsed.</param>
        /// <param name="TimeSpan">The parsed time span value.</param>
        public static Boolean NullableTimeSpan(String String, out TimeSpan? TimeSpan)
        {

            if (System.TimeSpan.TryParse(String, out var timeSpan))
            {
                TimeSpan = timeSpan;
                return true;
            }

            TimeSpan = null;
            return false;

        }

        #endregion


        #region NullableListOfStrings       (String, out Strings)

        /// <summary>
        /// A delegate to parse a UInt64? value from a string.
        /// </summary>
        /// <param name="String">The string to be parsed.</param>
        /// <param name="Strings">The parsed list of strings.</param>
        public static Boolean NullableListOfStrings(String String, out IEnumerable<String> Strings)
        {

            var elements = String.Split(",");

            Strings = elements.Length == 0
                          ? Array.Empty<String>()
                          : elements.Where  (element => element is not null).
                                     Select (element => element.Trim()).
                                     Where  (element => element.IsNotNullOrEmpty()).
                                     ToArray();

            return true;

        }

        #endregion

        #region NullableHashSetOfStrings    (String, out Strings)

        /// <summary>
        /// A delegate to parse a hash set of strings from a string.
        /// </summary>
        /// <param name="String">The string to be parsed.</param>
        /// <param name="Strings">The parsed hash set of strings.</param>
        public static Boolean NullableHashSetOfStrings(String String, out IEnumerable<String> Strings)
        {

            var elements  = String.Split(",");
            var list      = new List<String>();

            if (elements.Length == 0)
            {
                Strings = Array.Empty<String>();
                return true;
            }

            foreach (var element in elements)
            {

                var element2 = element?.Trim();

                if (element2 is not null &&
                    element2.IsNotNullOrEmpty() &&
                    !list.Contains(element2))
                {
                    list.Add(element2);
                }

            }

            Strings = list.ToArray();
            return true;

        }

        #endregion

        #region NullableHashSet             (String, out Strings)

        /// <summary>
        /// A delegate to parse a hash set of strings from a string.
        /// </summary>
        /// <param name="String">The string to be parsed.</param>
        /// <param name="Strings">The parsed hash set of strings.</param>
        public static Boolean NullableHashSet<T>(String              String,
                                                 TryParser<T>        Parser,
                                                 out IEnumerable<T>  Strings)
        {

            var list      = new List<T>();
            var elements  = String.Split (",", StringSplitOptions.RemoveEmptyEntries).
                                   Select(element => element.Trim());

            foreach (var element in elements)
            {

                if (element is not null &&
                    element.IsNotNullOrEmpty() &&
                    Parser(element, out var TTT) &&
                    TTT is not null)
                {
                    list.Add(TTT);
                }

            }

            Strings = list.ToArray();
            return true;

        }

        #endregion

    }

    #endregion


    #region HTTPHeaderField

    public class HTTPHeaderField
    {

        #region Data

        private static readonly Dictionary<String, HTTPHeaderField> definedHTTPHeaderFields = new();

        #endregion

        #region Properties

        /// <summary>
        /// The name of this HTTP request field
        /// </summary>
        public String               Name                    { get; }

        /// <summary>
        /// The type of a HTTP header field.
        /// </summary>
        public HeaderFieldType      HeaderFieldType         { get; }

        /// <summary>
        /// Whether a header field has and end-to-end or
        /// an hop-to-hop semantic.
        /// </summary>
        public RequestPathSemantic  RequestPathSemantic     { get; }

        /// <summary>
        /// When set to true header fields having multiple values
        /// will be serialized as a comma separated list, otherwise
        /// as multiple lines.
        /// </summary>
        public Boolean?             MultipleValuesAsList    { get; }

        #endregion


        #region Constructor(s)

        public HTTPHeaderField(String               Name,
                               HeaderFieldType      HeaderFieldType,
                               RequestPathSemantic  RequestPathSemantic,
                               Boolean?             MultipleValuesAsList   = null)
        {

            #region Initial checks

            if (Name.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Name),  "The given name of the header field must not be null or its length zero!");

            #endregion

            this.Name                  = Name.Trim();
            this.HeaderFieldType       = HeaderFieldType;
            this.RequestPathSemantic   = RequestPathSemantic;
            this.MultipleValuesAsList  = MultipleValuesAsList;

            definedHTTPHeaderFields.TryAdd(this.Name, this);

        }

        #endregion


        #region GetByName(Name)

        /// <summary>
        /// Get the HTTP header field with the given name.
        /// </summary>
        /// <param name="Name">The name of the HTTP header field.</param>
        public static HTTPHeaderField? GetByName(String Name)
        {

            if (definedHTTPHeaderFields.TryGetValue(Name, out var headerField))
                return headerField;

            return null;

        }

        #endregion


        #region Well-known header fields

        #region Cache-Control

        /// <summary>
        /// The Cache-Control general-header field is used to specify
        /// directives that MUST be obeyed by all caching mechanisms
        /// along the request/response chain. The directives specify
        /// behavior intended to prevent caches from adversely
        /// interfering with the request or response.
        /// </summary>
        /// <example>Cache-Control: no-cache</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField<String> CacheControl = new ("Cache-Control",
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
        public static readonly HTTPHeaderField<ConnectionType> Connection = new ("Connection",
                                                                                 HeaderFieldType.General,
                                                                                 RequestPathSemantic.EndToEnd,
                                                                                 MultipleValuesAsList:   false,
                                                                                 StringParser:           ConnectionType.TryParse,
                                                                                 ValueSerializer:        ct => ct.ToString());

        #endregion

        #region Content-Encoding

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
        public static readonly HTTPHeaderField<Encoding> ContentEncoding = new ("Content-Encoding",
                                                                                HeaderFieldType.General,
                                                                                RequestPathSemantic.EndToEnd);

        #endregion

        #region Content-Language

        /// <summary>
        /// The Content-Language entity-header field describes the natural
        /// language(s) of the intended audience for the enclosed entity.
        /// </summary>
        /// <example>Content-Language: en, de</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField<IEnumerable<String>> ContentLanguage = new ("Content-Language",
                                                                                           HeaderFieldType.General,
                                                                                           RequestPathSemantic.EndToEnd,
                                                                                           StringParser: StringParsers.NullableListOfStrings);

        #endregion

        #region Content-Length

        /// <summary>
        /// The Content-Length entity-header field indicates the size of
        /// the entity-body, in decimal number of OCTETs, sent to the
        /// recipient or, in the case of the HEAD method, the size of the
        /// entity-body that would have been sent if the request had been
        /// a GET request.
        /// </summary>
        /// <example>Content-Length: 3495</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField<UInt64?> ContentLength = new ("Content-Length",
                                                                             HeaderFieldType.General,
                                                                             RequestPathSemantic.EndToEnd,
                                                                             StringParser: StringParsers.NullableUInt64);

        #endregion

        #region Content-Location

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
        public static readonly HTTPHeaderField<String> ContentLocation = new ("Content-Location",
                                                                              HeaderFieldType.General,
                                                                              RequestPathSemantic.EndToEnd);

        #endregion

        #region Content-MD5

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
        public static readonly HTTPHeaderField<String> ContentMD5 = new ("Content-MD5",
                                                                         HeaderFieldType.General,
                                                                         RequestPathSemantic.EndToEnd);

        #endregion

        #region Content-Range

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
        public static readonly HTTPHeaderField<String> ContentRange = new ("Content-Range",
                                                                           HeaderFieldType.General,
                                                                           RequestPathSemantic.EndToEnd);

        #endregion

        #region Content-Type

        /// <summary>
        /// The Content-Type entity-header field indicates the media
        /// type of the entity-body sent to the recipient or, in the
        /// case of the HEAD method, the media type that would have
        /// been sent had the request been a GET.
        /// </summary>
        /// <example>Content-Type: text/html; charset=ISO-8859-4</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        public static readonly HTTPHeaderField<HTTPContentType> ContentType = new ("Content-Type",
                                                                                   HeaderFieldType.General,
                                                                                   RequestPathSemantic.EndToEnd,
                                                                                   StringParser: HTTPContentType.TryParse);

        #endregion

        #region Content-Disposition

        /// <summary>
        /// The Content-Disposition response header field is used to convey
        /// additional information about how to process the response payload, and
        /// also can be used to attach additional metadata, such as the filename
        /// to use when saving the response payload locally.
        /// </summary>
        /// <example>Content-Disposition: attachment; filename="filename.jpg"</example>
        /// <seealso cref="https://tools.ietf.org/html/rfc6266"/>
        public static readonly HTTPHeaderField<String> ContentDisposition = new ("Content-Disposition",
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
        public static readonly HTTPHeaderField<DateTime?> Date = new ("Date",
                                                                      HeaderFieldType.General,
                                                                      RequestPathSemantic.EndToEnd,
                                                                      StringParser:     (String text, out DateTime? dt) => { if (DateTime.TryParse(text, out var dt2)) { dt = dt2; return true; } dt = null; return false; },
                                                                      ValueSerializer:  dateTime => dateTime.HasValue
                                                                                            ? dateTime.Value.ToUniversalTime().ToString("r")
                                                                                            : null);

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
        public static readonly HTTPHeaderField<String> Pragma = new ("Pragma",
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
        public static readonly HTTPHeaderField<String> Trailer = new ("Trailer",
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
        public static readonly HTTPHeaderField<String> Via = new ("Via",
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
        public static readonly HTTPHeaderField<String> TransferEncoding = new ("Transfer-Encoding",
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
        /// <example>Upgrade: websocket</example>
        /// <seealso cref="http://tools.ietf.org/html/rfc2616"/>
        /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9110.html#name-upgrade"/>
        public static readonly HTTPHeaderField<String> Upgrade = new ("Upgrade",
                                                                      HeaderFieldType.General,
                                                                      RequestPathSemantic.EndToEnd);

        #endregion


        // WebSocket

        #region Sec-WebSocket-Key

        /// <summary>
        /// Sec-Web-SocketKey.
        /// </summary>
        public static readonly HTTPHeaderField<String> SecWebSocketKey = new ("Sec-WebSocket-Key",
                                                                              HeaderFieldType.General,
                                                                              RequestPathSemantic.EndToEnd);

        #endregion

        #region Sec-WebSocket-Protocol

        /// <summary>
        /// Sec-Web-SocketProtocol within a HTTP request.
        /// </summary>
        public static readonly HTTPHeaderField<IEnumerable<String>> SecWebSocketProtocol_Request = new ("Sec-WebSocket-Protocol",
                                                                                                        HeaderFieldType.General,
                                                                                                        RequestPathSemantic.EndToEnd,
                                                                                                        MultipleValuesAsList:  true,
                                                                                                        StringParser:          StringParsers.NullableListOfStrings);

        /// <summary>
        /// Sec-Web-SocketProtocol within a HTTP response.
        /// </summary>
        public static readonly HTTPHeaderField<String> SecWebSocketProtocol_Response = new ("Sec-WebSocket-Protocol",
                                                                                            HeaderFieldType.General,
                                                                                            RequestPathSemantic.EndToEnd);

        #endregion

        #region Sec-WebSocket-Version

        /// <summary>
        /// Sec-WebSocket-Version.
        /// </summary>
        public static readonly HTTPHeaderField<String> SecWebSocketVersion = new ("Sec-WebSocket-Version",
                                                                                  HeaderFieldType.General,
                                                                                  RequestPathSemantic.EndToEnd);

        #endregion

        #region Sec-WebSocket-Accept

        /// <summary>
        /// Sec-WebSocket-Accept.
        /// </summary>
        public static readonly HTTPHeaderField<String> SecWebSocketAccept = new ("Sec-WebSocket-Accept",
                                                                                 HeaderFieldType.General,
                                                                                 RequestPathSemantic.EndToEnd);

        #endregion

        #endregion

        #region Additional header fields

        #region Process-ID

        /// <summary>
        /// The unique identification of a server side process,
        /// e.g. used by the Hubject Open InterCharge Protocol.
        /// </summary>
        /// <example>4c1134cd-2ee7-49da-9952-0f53c5456d36</example>
        public static readonly HTTPHeaderField<String> ProcessID = new ("Process-ID",
                                                                        HeaderFieldType.General,
                                                                        RequestPathSemantic.HopToHop);

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator == (HTTPHeaderField? AHTTPHeaderField1,
                                           HTTPHeaderField? AHTTPHeaderField2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(AHTTPHeaderField1, AHTTPHeaderField2))
                return true;

            // If one is null, but not both, return false.
            if (AHTTPHeaderField1 is null || AHTTPHeaderField2 is null)
                return false;

            return AHTTPHeaderField1.Equals(AHTTPHeaderField2);

        }

        #endregion

        #region Operator != (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator != (HTTPHeaderField? AHTTPHeaderField1,
                                           HTTPHeaderField? AHTTPHeaderField2)

            => !(AHTTPHeaderField1 == AHTTPHeaderField2);

        #endregion

        #region Operator <  (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator < (HTTPHeaderField? AHTTPHeaderField1,
                                          HTTPHeaderField? AHTTPHeaderField2)
        {

            if (AHTTPHeaderField1 is null)
                throw new ArgumentNullException(nameof(AHTTPHeaderField1), "The given AHTTPHeaderField1 must not be null!");

            if (AHTTPHeaderField2 is null)
                throw new ArgumentNullException(nameof(AHTTPHeaderField2), "The given AHTTPHeaderField2 must not be null!");

            return AHTTPHeaderField1.CompareTo(AHTTPHeaderField2) < 0;

        }

        #endregion

        #region Operator >  (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator > (HTTPHeaderField? AHTTPHeaderField1,
                                          HTTPHeaderField? AHTTPHeaderField2)
        {

            if (AHTTPHeaderField1 is null)
                throw new ArgumentNullException(nameof(AHTTPHeaderField1), "The given AHTTPHeaderField1 must not be null!");

            if (AHTTPHeaderField2 is null)
                throw new ArgumentNullException(nameof(AHTTPHeaderField2), "The given AHTTPHeaderField2 must not be null!");

            return AHTTPHeaderField1.CompareTo(AHTTPHeaderField2) > 0;

        }

        #endregion

        #region Operator <= (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator <= (HTTPHeaderField? AHTTPHeaderField1,
                                           HTTPHeaderField? AHTTPHeaderField2)

            => !(AHTTPHeaderField1 > AHTTPHeaderField2);

        #endregion

        #region Operator >= (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator >= (HTTPHeaderField? AHTTPHeaderField1,
                                           HTTPHeaderField? AHTTPHeaderField2)

            => !(AHTTPHeaderField1 < AHTTPHeaderField2);

        #endregion

        #endregion

        #region IComparable<HTTPHeaderField> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP header fields.
        /// </summary>
        /// <param name="Object">A HTTP header field to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPHeaderField httpHeaderField
                   ? CompareTo(httpHeaderField)
                   : throw new ArgumentException("The given object is not a http header field!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPHeaderField)

        /// <summary>
        /// Compares two HTTP header fields.
        /// </summary>
        /// <param name="HTTPHeaderField">A HTTP header field to compare with.</param>
        public Int32 CompareTo(HTTPHeaderField? HTTPHeaderField)
        {

            if (HTTPHeaderField is null)
                throw new ArgumentNullException(nameof(HTTPHeaderField),
                                                "The given HTTP header field must not be null!");

            return String.Compare(Name,
                                  HTTPHeaderField.Name,
                                  StringComparison.OrdinalIgnoreCase);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPHeaderField> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP header fields for equality.
        /// </summary>
        /// <param name="Object">A HTTP header field to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPHeaderField httpHeaderField &&
                   Equals(httpHeaderField);

        #endregion

        #region Equals(HTTPHeaderField)

        /// <summary>
        /// Compares two HTTP header fields for equality.
        /// </summary>
        /// <param name="HTTPHeaderField">A HTTP header field to compare with.</param>
        public Boolean Equals(HTTPHeaderField? HTTPHeaderField)

            => HTTPHeaderField is not null &&

               String.Equals(Name,
                             HTTPHeaderField.Name,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => Name.ToLower().GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Name;

        #endregion

    }

    #endregion

    #region HTTPHeaderField<T>

    /// <summary>
    /// Defines a field within the HTTP header.
    /// </summary>
    /// <seealso cref="http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html"/>
    /// <seealso cref="http://restpatterns.org"/>
    /// <seealso cref="http://en.wikipedia.org/wiki/List_of_HTTP_header_fields"/>
    /// <seealso cref="http://www.and.org/texts/server-http"/>
    /// <seealso cref="http://www.iana.org/assignments/message-headers/message-headers.xhtml"/>
    public class HTTPHeaderField<T> : HTTPHeaderField,
                                      IEquatable<HTTPHeaderField<T>>,
                                      IComparable<HTTPHeaderField<T>>,
                                      IComparable
    {

        #region Delegates

        /// <summary>
        /// A delegate definition to parse the value of the header field from a string.
        /// </summary>
        public delegate Boolean  StringParserDelegate(String arg1, out Object? arg2);

        /// <summary>
        /// A delegate definition to serialize the value of the header field to a string.
        /// </summary>
        public delegate String?  ValueSerializerDelegate<T2>(T2 arg1);

        #endregion

        #region Properties

        /// <summary>
        /// A delegate to parse the value of the header field from a string.
        /// </summary>
        public TryParser<T>?               StringParser       { get; }

        /// <summary>
        /// A delegate to parse a list of values of the header field from a string.
        /// </summary>
        public TryParser<IEnumerable<T>>?  StringsParser      { get; }

        /// <summary>
        /// A delegate to serialize the value of the header field to a string.
        /// </summary>
        public ValueSerializerDelegate<T>  ValueSerializer    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new HTTP header field.
        /// </summary>
        /// <param name="Name">The name of the HTTP header field.</param>
        /// <param name="HeaderFieldType">The type of the header field (general|request|response).</param>
        /// <param name="RequestPathSemantic">Whether a header field has and end-to-end or an hop-to-hop semantic.</param>
        /// <param name="MultipleValuesAsList">When set to true header fields having multiple values will be serialized as a comma separated list, otherwise as multiple lines.</param>
        /// <param name="StringParser">Parse this HTTPHeaderField from a string.</param>
        /// <param name="ValueSerializer">A delegate to serialize the value of the header field to a string.</param>
        public HTTPHeaderField(String                       Name,
                               HeaderFieldType              HeaderFieldType,
                               RequestPathSemantic          RequestPathSemantic,
                               Boolean?                     MultipleValuesAsList   = null,
                               TryParser<T>?                StringParser           = null,
                               ValueSerializerDelegate<T>?  ValueSerializer        = null)

            : base(Name,
                   HeaderFieldType,
                   RequestPathSemantic,
                   MultipleValuesAsList)

        {

            this.StringParser     = StringParser;

            if (this.StringParser is null &&
                typeof(T) == typeof(String))
            {
                this.StringParser = (String s, out T? s2) => { s2 = (T) (Object) s; return true; };
            }


            this.ValueSerializer  = ValueSerializer ?? ((valueT) => valueT?.ToString() ?? "");

        }

        #endregion


        #region Operator overloading

        #region Operator == (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator == (HTTPHeaderField<T>? AHTTPHeaderField1,
                                           HTTPHeaderField<T>? AHTTPHeaderField2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(AHTTPHeaderField1, AHTTPHeaderField2))
                return true;

            // If one is null, but not both, return false.
            if (AHTTPHeaderField1 is null || AHTTPHeaderField2 is null)
                return false;

            return AHTTPHeaderField1.Equals(AHTTPHeaderField2);

        }

        #endregion

        #region Operator != (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator != (HTTPHeaderField<T>? AHTTPHeaderField1,
                                           HTTPHeaderField<T>? AHTTPHeaderField2)

            => !(AHTTPHeaderField1 == AHTTPHeaderField2);

        #endregion

        #region Operator <  (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator < (HTTPHeaderField<T>? AHTTPHeaderField1,
                                          HTTPHeaderField<T>? AHTTPHeaderField2)
        {

            if (AHTTPHeaderField1 is null)
                throw new ArgumentNullException(nameof(AHTTPHeaderField1), "The given AHTTPHeaderField1 must not be null!");

            if (AHTTPHeaderField2 is null)
                throw new ArgumentNullException(nameof(AHTTPHeaderField2), "The given AHTTPHeaderField2 must not be null!");

            return AHTTPHeaderField1.CompareTo(AHTTPHeaderField2) < 0;

        }

        #endregion

        #region Operator >  (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator > (HTTPHeaderField<T>? AHTTPHeaderField1,
                                          HTTPHeaderField<T>? AHTTPHeaderField2)
        {

            if (AHTTPHeaderField1 is null)
                throw new ArgumentNullException(nameof(AHTTPHeaderField1), "The given AHTTPHeaderField1 must not be null!");

            if (AHTTPHeaderField2 is null)
                throw new ArgumentNullException(nameof(AHTTPHeaderField2), "The given AHTTPHeaderField2 must not be null!");

            return AHTTPHeaderField1.CompareTo(AHTTPHeaderField2) > 0;

        }

        #endregion

        #region Operator <= (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator <= (HTTPHeaderField<T>? AHTTPHeaderField1,
                                           HTTPHeaderField<T>? AHTTPHeaderField2)

            => !(AHTTPHeaderField1 > AHTTPHeaderField2);

        #endregion

        #region Operator >= (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator >= (HTTPHeaderField<T>? AHTTPHeaderField1,
                                           HTTPHeaderField<T>? AHTTPHeaderField2)

            => !(AHTTPHeaderField1 < AHTTPHeaderField2);

        #endregion

        #endregion

        #region IComparable<HTTPHeaderField> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP header fields.
        /// </summary>
        /// <param name="Object">A HTTP header field to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPHeaderField<T> httpHeaderField
                   ? CompareTo(httpHeaderField)
                   : throw new ArgumentException("The given object is not a http header field!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPHeaderField)

        /// <summary>
        /// Compares two HTTP header fields.
        /// </summary>
        /// <param name="HTTPHeaderField">A HTTP header field to compare with.</param>
        public Int32 CompareTo(HTTPHeaderField<T>? HTTPHeaderField)
        {

            if (HTTPHeaderField is null)
                throw new ArgumentNullException(nameof(HTTPHeaderField),
                                                "The given HTTP header field must not be null!");

            return String.Compare(Name,
                                  HTTPHeaderField.Name,
                                  StringComparison.OrdinalIgnoreCase);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPHeaderField> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP header fields for equality.
        /// </summary>
        /// <param name="Object">A HTTP header field to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPHeaderField<T> httpHeaderField &&
                   Equals(httpHeaderField);

        #endregion

        #region Equals(HTTPHeaderField)

        /// <summary>
        /// Compares two HTTP header fields for equality.
        /// </summary>
        /// <param name="HTTPHeaderField">A HTTP header field to compare with.</param>
        public Boolean Equals(HTTPHeaderField<T>? HTTPHeaderField)

            => HTTPHeaderField is not null &&

               String.Equals(Name,
                             HTTPHeaderField.Name,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => Name.ToLower().GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Name;

        #endregion

    }

    #endregion

}
