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

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    public enum HeaderFieldType
    {
        General,
        Request,
        Response
    }


    /// <summary>
    /// Defines a field within the HTTP header.
    /// </summary>
    public class HTTPHeaderField : IEquatable<HTTPHeaderField>, IComparable<HTTPHeaderField>, IComparable
    {

        #region Properties

        #region Name

        /// <summary>
        /// The name of this HTTP request field
        /// </summary>
        public String Name { get; private set; }

        #endregion

        #region Description

        /// <summary>
        /// A description of this HTTP request field
        /// </summary>
        public String Description { get; private set; }

        #endregion

        #region Example

        /// <summary>
        /// An usage example for this HTTP request field
        /// </summary>
        public String Example { get; private set; }

        #endregion

        #region SeeAlso

        /// <summary>
        /// An additional source of information, e.g. the defining request-for-comment.
        /// </summary>
        public Uri SeeAlso { get; private set; }

        #endregion

        #region Type

        /// <summary>
        /// The C# type of this HTTP header field.
        /// </summary>
        public Type Type { get; private set; }

        #endregion

        #region StringParser

        public delegate Boolean StringParserDelegate(String arg1, out Object arg2);

        /// <summary>
        /// Parse this HTTPHeaderField from a string.
        /// </summary>
        public StringParserDelegate StringParser { get; private set; }

        #endregion

        #region ValueSerializer

        /// <summary>
        /// A delegate to serialize the value of the header field to a string.
        /// </summary>
        public Func<Object, String> ValueSerializer { get; private set; }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPHeaderField(Name, Description = null, Example = null, SeeAlso = null)

        /// <summary>
        /// Creates a new HTTP header field.
        /// </summary>
        /// <param name="Name">The name of the HTTP header field</param>
        /// <param name="ValueSerializer">A delegate to serialize the value of the header field to a string.</param>
        /// <param name="Description">A description of the HTTP request header field</param>
        /// <param name="Example">An usage example</param>
        /// <param name="SeeAlso">An additional source of information, e.g. the defining request-for-comment</param>
        public HTTPHeaderField(String               Name,
                               HeaderFieldType      HeaderFieldType,
                               String               Description     = null,
                               String               Example         = null,
                               Uri                  SeeAlso         = null,
                               Type                 Type            = null,
                               StringParserDelegate StringParser    = null,
                               Func<Object, String> ValueSerializer = null)

        {

            #region Initial checks

            if (Name == null && Name == "")
                throw new ArgumentNullException("The given Name must not be null or its length zero!");

            #endregion

            this.Name            = Name;
            this.ValueSerializer = ValueSerializer;
            this.Description     = Description;
            this.Example         = Example;
            this.SeeAlso         = SeeAlso;
            this.Type            = Type;
            this.StringParser    = StringParser;
            this.ValueSerializer = ValueSerializer;

        }

        #endregion

        #endregion


        #region General header fields

        // http://restpatterns.org

        #region CacheControl

        /// <summary>
        /// The Cache-Control general-header field is used to specify
        /// directives that MUST be obeyed by all caching mechanisms
        /// along the request/response chain. The directives specify
        /// behavior intended to prevent caches from adversely
        /// interfering with the request or response.
        /// </summary>
        public static readonly HTTPHeaderField CacheControl = new HTTPHeaderField(
                                                      "Cache-Control",
                                                      HeaderFieldType.General,
                                                      "The Connection general-header field allows the sender to " +
                                                      "specify options that are desired for that particular " +
                                                      "connection and MUST NOT be communicated by proxies over " +
                                                      "further connections.",
                                                      "Cache-Control: no-cache",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));
        
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
        public static readonly HTTPHeaderField Connection = new HTTPHeaderField(
                                                      "Connection",
                                                      HeaderFieldType.General,
                                                      "The Cache-Control general-header field is used to specify " +
                                                      "directives that MUST be obeyed by all caching mechanisms " +
                                                      "along the request/response chain. The directives specify " +
                                                      "behavior intended to prevent caches from adversely " +
                                                      "interfering with the request or response. " +
                                                      "HTTP/1.1 applications that do not support persistent " +
                                                      "connections MUST include the \"close\" connection option " +
                                                      "in every message.",
                                                      "Connection: close",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));
        
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
        public static readonly HTTPHeaderField ContentEncoding = new HTTPHeaderField(
                                                      "Content-Encoding",
                                                      HeaderFieldType.General,
                                                      "The Content-Encoding entity-header field is used as a " +
                                                      "modifier to the media-type. When present, its value " +
                                                      "indicates what additional content codings have been " +
                                                      "applied to the entity-body, and thus what decoding " +
                                                      "mechanisms must be applied in order to obtain the " +
                                                      "media-type referenced by the Content-Type header field. " +
                                                      "If the content-encoding of an entity in a request message " +
                                                      "is not acceptable to the origin server, the server SHOULD " +
                                                      "respond with a status code of 415 (Unsupported Media Type).",
                                                      "Content-Encoding: gzip",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

        #endregion

        #region ContentLanguage

        /// <summary>
        /// The Content-Language entity-header field describes the natural
        /// language(s) of the intended audience for the enclosed entity.
        /// </summary>
        public static readonly HTTPHeaderField ContentLanguage = new HTTPHeaderField(
                                                      "Content-Language",
                                                      HeaderFieldType.General,
                                                      "The Content-Language entity-header field describes the " +
                                                      "natural language(s) of the intended audience for the " +
                                                      "enclosed entity.",
                                                      "Content-Language: en, de",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

        #endregion

        #region ContentLength

        /// <summary>
        /// The Content-Length entity-header field indicates the size of
        /// the entity-body, in decimal number of OCTETs, sent to the
        /// recipient or, in the case of the HEAD method, the size of the
        /// entity-body that would have been sent if the request had been
        /// a GET request.
        /// </summary>
        public static readonly HTTPHeaderField ContentLength = new HTTPHeaderField(
                                                      "Content-Length",
                                                      HeaderFieldType.General,
                                                      "The Content-Length entity-header field indicates the size of " +
                                                      "the entity-body, in decimal number of OCTETs, sent to the " +
                                                      "recipient or, in the case of the HEAD method, the size of the " +
                                                      "entity-body that would have been sent if the request had been " +
                                                      "a GET request.",
                                                      "Content-Length: 3495",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"),
                                                      typeof(UInt64?),
                                                      (String s, out Object o) => { UInt64 Value; if (UInt64.TryParse(s, out Value)) { o = Value; return true; } o = null; return false; });

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
        public static readonly HTTPHeaderField ContentLocation = new HTTPHeaderField(
                                                      "Content-Location",
                                                      HeaderFieldType.General,
                                                      "The Content-Location entity-header field MAY be used to supply " +
                                                      "the resource location for the entity enclosed in the message " +
                                                      "when that entity is accessible from a location separate from " +
                                                      "the requested resource's URI. " +
                                                      "If the Content-Location is a relative URI, the relative URI " + 
                                                      "is interpreted relative to the Request-URI.",
                                                      "Content-Location: ../test.html",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));
        
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
        public static readonly HTTPHeaderField ContentMD5 = new HTTPHeaderField(
                                                      "Content-MD5",
                                                      HeaderFieldType.General,
                                                      "The Content-MD5 entity-header field, is an MD5 digest of the " +
                                                      "entity-body for the purpose of providing an end-to-end " +
                                                      "message integrity check (MIC) of the entity-body. " +
                                                      "Note: a MIC is good for detecting accidental modification " +
                                                      "of the entity-body in transit, but is not proof against " +
                                                      "malicious attacks.",
                                                      "MD5-Digest: <base64 of 128 bit MD5 digest as per RFC 1864>",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField ContentRange = new HTTPHeaderField(
                                                      "Content-Range",
                                                      HeaderFieldType.General,
                                                      "The Content-Range entity-header is sent with a partial " +
                                                      "entity-body to specify where in the full entity-body the " +
                                                      "partial body should be applied. " +
                                                      "The header SHOULD indicate the total length of the full " +
                                                      "entity-body, unless this length is unknown or difficult " +
                                                      "to determine. The asterisk \"*\" character means that the " +
                                                      "instance-length is unknown at the time when the response " +
                                                      "was generated. " +
                                                      "Partinal content replies must be sent using the response " +
                                                      "code 206 (Partial content). When an HTTP message includes " +
                                                      "the content of multiple ranges, these are transmitted as a " +
                                                      "multipart message using the media type \"multipart/byteranges\". " +
                                                      "Syntactically invalid content-range reqeuests, SHOULD be " +
                                                      "treated as if the invalid Range header field did not exist. " +
                                                      "Normally, this means return a 200 response containing the " +
                                                      "full entity. " +
                                                      "If the server receives a request (other than one including " +
                                                      "an If-Range request-header field) with an unsatisfiable " +
                                                      "Range request-header field, it SHOULD return a response " +
                                                      "code of 416 (Requested range not satisfiable).",
                                                      "Content-Range: bytes 21010-47021/47022",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

        #endregion

        #region ContentType

        /// <summary>
        /// The Content-Type entity-header field indicates the media
        /// type of the entity-body sent to the recipient or, in the
        /// case of the HEAD method, the media type that would have
        /// been sent had the request been a GET.
        /// </summary>         
        public static readonly HTTPHeaderField ContentType = new HTTPHeaderField(
                                                      "Content-Type",
                                                      HeaderFieldType.General,
                                                      "The Content-Type entity-header field indicates the media type " +
                                                      "of the entity-body sent to the recipient or, in the case of the " +
                                                      "HEAD method, the media type that would have been sent had the " +
                                                      "request been a GET.",
                                                      "Content-Type: text/html; charset=ISO-8859-4",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Date = new HTTPHeaderField(
                                                      "Date",
                                                      HeaderFieldType.General,
                                                      "The Date general-header field represents the date and time " +
                                                      "at which the message was originated, having the same semantics " +
                                                      "as orig-date in RFC 822. The field value is an HTTP-date, as " +
                                                      "described in section 3.3.1; it MUST be sent in RFC 1123 [8]-date " +
                                                      "format." +
                                                      Environment.NewLine +
                                                      "Origin servers MUST include a Date header field in all responses, " +
                                                      "except in these cases:" +
                                                      Environment.NewLine +
                                                      "If the response status code is 100 (Continue) or 101 (Switching " +
                                                      "Protocols), the response MAY include a Date header field, at the " +
                                                      "server's option." +
                                                      Environment.NewLine +
                                                      "If the response status code conveys a server error, e.g. 500 " +
                                                      "(Internal Server Error) or 503 (Service Unavailable), and it is " +
                                                      "inconvenient or impossible to generate a valid Date. " +
                                                      Environment.NewLine +
                                                      "If the server does not have a clock that can provide a reasonable " +
                                                      "approximation of the current time, its responses MUST NOT include " +
                                                      "a Date header field. In this case, the rules in section 14.18.1 " +
                                                      "MUST be followed." +
                                                      Environment.NewLine +
                                                      "A received message that does not have a Date header field MUST be " +
                                                      "assigned one by the recipient if the message will be cached by that " +
                                                      "recipient or gatewayed via a protocol which requires a Date. An HTTP " +
                                                      "implementation without a clock MUST NOT cache responses without " +
                                                      "revalidating them on every use. An HTTP cache, especially a shared " +
                                                      "cache, SHOULD use a mechanism, such as NTP [28], to synchronize its " +
                                                      "clock with a reliable external standard. " +
                                                      Environment.NewLine +
                                                      "Clients SHOULD only send a Date header field in messages that include " +
                                                      "an entity-body, as in the case of the PUT and POST requests, and even " +
                                                      "then it is optional. A client without a clock MUST NOT send a Date " +
                                                      "header field in a request." +
                                                      Environment.NewLine +
                                                      "The HTTP-date sent in a Date header SHOULD NOT represent a date and " +
                                                      "time subsequent to the generation of the message. It SHOULD represent " +
                                                      "the best available approximation of the date and time of message " +
                                                      "generation, unless the implementation has no means of generating a " +
                                                      "reasonably accurate date and time. In theory, the date ought to " +
                                                      "represent the moment just before the entity is generated. In practice, " +
                                                      "the date can be generated at any time during the message origination " +
                                                      "without affecting its semantic value." +
                                                      Environment.NewLine +
                                                      "Clockless Origin Server Operation " + Environment.NewLine +
                                                      "Some origin server implementations might not have a clock available. " +
                                                      "An origin server without a clock MUST NOT assign Expires or Last-Modified " +
                                                      "values to a response, unless these values were associated with the " +
                                                      "resource by a system or user with a reliable clock. It MAY assign an " +
                                                      "Expires value that is known, at or before server configuration time, " +
                                                      "to be in the past (this allows 'pre-expiration' of responses without " +
                                                      "storing separate Expires values for each resource).",
                                                      "Date: Tue, 15 Nov 1994 08:12:31 GMT",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Pragma = new HTTPHeaderField(
                                                      "Pragma",
                                                      HeaderFieldType.General,
                                                      "The Pragma general-header field is used to include implementation-" +
                                                      "specific directives that might apply to any recipient along the " +
                                                      "request/response chain. All pragma directives specify optional " +
                                                      "behavior from the viewpoint of the protocol; however, some systems " +
                                                      "MAY require that behavior be consistent with the directives." +
                                                      Environment.NewLine +
                                                      "When the no-cache directive is present in a request message, an " +
                                                      "application SHOULD forward the request toward the origin server " +
                                                      "even if it has a cached copy of what is being requested. This " +
                                                      "pragma directive has the same semantics as the no-cache cache-" +
                                                      "directive (see section 14.9) and is defined here for backward " +
                                                      "compatibility with HTTP/1.0. Clients SHOULD include both header " +
                                                      "fields when a no-cache request is sent to a server not known to " +
                                                      "be HTTP/1.1 compliant." +
                                                      Environment.NewLine +
                                                      "Pragma directives MUST be passed through by a proxy or gateway " +
                                                      "application, regardless of their significance to that application, " +
                                                      "since the directives might be applicable to all recipients along " +
                                                      "the request/response chain. It is not possible to specify a pragma " +
                                                      "for a specific recipient; however, any pragma directive not relevant " +
                                                      "to a recipient SHOULD be ignored by that recipient." +
                                                      Environment.NewLine +
                                                      "HTTP/1.1 caches SHOULD treat \"Pragma: no-cache\" as if the client " +
                                                      "had sent \"Cache-Control: no-cache\". No new Pragma directives will " +
                                                      "be defined in HTTP.",
                                                      "Pragma: no-cache",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Trailer = new HTTPHeaderField(
                                                      "Trailer",
                                                      HeaderFieldType.General,
                                                      "The Trailer general field value indicates that the given " +
                                                      "set of header fields is present in the trailer of a " +
                                                      "message encoded with chunked transfer-coding." +
                                                      Environment.NewLine +
                                                      "An HTTP/1.1 message SHOULD include a Trailer header field " +
                                                      "in a message using chunked transfer-coding with a non-empty " +
                                                      "trailer. Doing so allows the recipient to know which header " +
                                                      "fields to expect in the trailer." +
                                                      Environment.NewLine +
                                                      "If no Trailer header field is present, the trailer SHOULD NOT " +
                                                      "include any header fields. See section 3.6.1 for restrictions " +
                                                      "on the use of trailer fields in a 'chunked' transfer-coding." +
                                                      Environment.NewLine +
                                                      "Message header fields listed in the Trailer header field MUST " +
                                                      "NOT include the following header fields:" + Environment.NewLine +
                                                      "  - Transfer-Encoding " + Environment.NewLine +
                                                      "  - Content-Length " + Environment.NewLine +
                                                      "  - Trailer",
                                                      "Trailer : Max-Forwards",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

        #endregion

        #region Via

        /// <summary>
        /// The Via general-header field MUST be used by gateways
        /// and proxies to indicate the intermediate protocols and
        /// recipients between the user agent and the server on
        /// requests, and between the origin server and the client
        /// on responses.
        /// </summary>         
        public static readonly HTTPHeaderField Via = new HTTPHeaderField(
                                                      "Via",
                                                      HeaderFieldType.General,
                                                      "The Via general-header field MUST be used by gateways and " +
                                                      "proxies to indicate the intermediate protocols and recipients " +
                                                      "between the user agent and the server on requests, and between " +
                                                      "the origin server and the client on responses.",
                                                      "Via: 1.0 fred, 1.1 nowhere.com (Apache/1.1)",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField TransferEncoding = new HTTPHeaderField(
                                                      "Transfer-Encoding",
                                                      HeaderFieldType.General,
                                                      "The Transfer-Encoding general-header field indicates what (if any) " +
                                                      "type of transformation has been applied to the message body in " +
                                                      "order to safely transfer it between the sender and the recipient. " +
                                                      "This differs from the content-coding in that the transfer-coding " +
                                                      "is a property of the message, not of the entity." +
                                                      Environment.NewLine +
                                                      "If multiple encodings have been applied to an entity, the transfer- " +
                                                      "codings MUST be listed in the order in which they were applied. " +
                                                      "Additional information about the encoding parameters MAY be " +
                                                      "provided by other entity-header fields not defined by this " +
                                                      "specification.",
                                                      "Transfer-Encoding: chunked",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Upgrade = new HTTPHeaderField(
                                                      "Upgrade",
                                                      HeaderFieldType.General,
                                                      "The Upgrade general-header allows the client to specify what " +
                                                      "additional communication protocols it supports and would like " +
                                                      "to use if the server finds it appropriate to switch protocols. " +
                                                      "The server MUST use the Upgrade header field within a 101 " +
                                                      "(Switching Protocols) response to indicate which protocol(s) " +
                                                      "are being switched." +
                                                      Environment.NewLine +
                                                      "The Upgrade header field is intended to provide a simple " +
                                                      "mechanism for transition from HTTP/1.1 to some other, " +
                                                      "incompatible protocol. It does so by allowing the client to " +
                                                      "advertise its desire to use another protocol, such as a later " +
                                                      "version of HTTP with a higher major version number, even though " +
                                                      "the current request has been made using HTTP/1.1. This eases " +
                                                      "the difficult transition between incompatible protocols by " +
                                                      "allowing the client to initiate a request in the more commonly " +
                                                      "supported protocol while indicating to the server that it would " +
                                                      "like to use a 'better' protocol if available (where 'better' " +
                                                      "is determined by the server, possibly according to the nature " +
                                                      "of the method and/or resource being requested)." +
                                                      Environment.NewLine +
                                                      "The Upgrade header field only applies to switching application- " +
                                                      "layer protocols upon the existing transport-layer connection. " +
                                                      "Upgrade cannot be used to insist on a protocol change; its " +
                                                      "acceptance and use by the server is optional. The capabilities " +
                                                      "and nature of the application-layer communication after the " +
                                                      "protocol change is entirely dependent upon the new protocol " +
                                                      "chosen, although the first action after changing the protocol " +
                                                      "MUST be a response to the initial HTTP request containing the " +
                                                      "Upgrade header field." +
                                                      Environment.NewLine +
                                                      "The Upgrade header field only applies to the immediate " +
                                                      "connection. Therefore, the upgrade keyword MUST be supplied " +
                                                      "within a Connection header field (section 14.10) whenever " +
                                                      "Upgrade is present in an HTTP/1.1 message." +
                                                      Environment.NewLine +
                                                      "The Upgrade header field cannot be used to indicate a switch " +
                                                      "to a protocol on a different connection. For that purpose, it " +
                                                      "is more appropriate to use a 301, 302, 303, or 305 redirection " +
                                                      "response." +
                                                      Environment.NewLine +
                                                      "This specification only defines the protocol name 'HTTP' for " +
                                                      "use by the family of Hypertext Transfer Protocols, as defined " +
                                                      "by the HTTP version rules of section 3.1 and future updates " +
                                                      "to this specification. Any token can be used as a protocol " +
                                                      "name; however, it will only be useful if both the client and " +
                                                      "server associate the name with the same protocol.",
                                                      "Upgrade: HTTP/2.0, SHTTP/1.3, IRC/6.9, RTA/x11",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Age = new HTTPHeaderField(
                                                      "Age",
                                                      HeaderFieldType.Response,
                                                      "The Age response-header field conveys the sender's estimate of " +
                                                      "the amount of time since the response (or its revalidation) " +
                                                      "was generated at the origin server. A cached response is " +
                                                      "'fresh' if its age does not exceed its freshness lifetime. Age " +
                                                      "values are calculated as specified in section 13.2.3. " +
                                                      Environment.NewLine +
                                                      "Age values are non-negative decimal integers, representing " +
                                                      "time in seconds." +
                                                      Environment.NewLine +
                                                      "If a cache receives a value larger than the largest positive " +
                                                      "integer it can represent, or if any of its age calculations " +
                                                      "overflows, it MUST transmit an Age header with a value of " +
                                                      "2147483648 (2^31). An HTTP/1.1 server that includes a cache " +
                                                      "MUST include an Age header field in every response generated " +
                                                      "from its own cache. Caches SHOULD use an arithmetic type of " +
                                                      "at least 31 bits of range.",
                                                      "Age: 1234",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Allow = new HTTPHeaderField(
                                                      "Allow",
                                                      HeaderFieldType.Response,
                                                      "The Allow entity-header field lists the set of methods " +
                                                      "supported by the resource identified by the Request-URI. " +
                                                      "The purpose of this field is strictly to inform the " +
                                                      "recipient of valid methods associated with the resource. " +
                                                      "An Allow header field MUST be present in a 405 (Method " +
                                                      "Not Allowed) response." +
                                                      Environment.NewLine +
                                                      "This field cannot prevent a client from trying other " +
                                                      "methods. However, the indications given by the Allow " +
                                                      "header field value SHOULD be followed. The actual set of " +
                                                      "allowed methods is defined by the origin server at the " +
                                                      "time of each request." +
                                                      Environment.NewLine +
                                                      "The Allow header field MAY be provided with a PUT request " +
                                                      "to recommend the methods to be supported by the new or " +
                                                      "modified resource. The server is not required to support " +
                                                      "these methods and SHOULD include an Allow header in the " +
                                                      "response giving the actual supported methods." +
                                                      Environment.NewLine +
                                                      "A proxy MUST NOT modify the Allow header field even if it " +
                                                      "does not understand all the methods specified, since the " +
                                                      "user agent might have other means of communicating with " +
                                                      "the origin server.",
                                                      "Allow: GET, HEAD, PUT",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField DAV = new HTTPHeaderField(
                                                      "DAV",
                                                      HeaderFieldType.Response,
                                                      "This general-header appearing in the response indicates that " +
                                                      "the resource supports the DAV schema and protocol as specified. " +
                                                      "All DAV-compliant resources MUST return the DAV header with " +
                                                      "compliance-class '1' on all OPTIONS responses. In cases where " +
                                                      "WebDAV is only supported in part of the server namespace, an " +
                                                      "OPTIONS request to non-WebDAV resources (including '/') SHOULD " +
                                                      "NOT advertise WebDAV support. " +
                                                      Environment.NewLine +
                                                      "The value is a comma-separated list of all compliance class " +
                                                      "identifiers that the resource supports. Class identifiers may " +
                                                      "be Coded-URLs or tokens (as defined by [RFC2616]). Identifiers " +
                                                      "can appear in any order. Identifiers that are standardized " +
                                                      "through the IETF RFC process are tokens, but other identifiers " +
                                                      "SHOULD be Coded-URLs to encourage uniqueness." +
                                                      Environment.NewLine +
                                                      "A resource must show class 1 compliance if it shows class 2 or " +
                                                      "3 compliance. In general, support for one compliance class does " +
                                                      "not entail support for any other, and in particular, support for " +
                                                      "compliance class 3 does not require support for compliance class " +
                                                      "2. Please refer to Section 18 for more details on compliance " +
                                                      "classes defined in this specification." +
                                                      Environment.NewLine +
                                                      "Note that many WebDAV servers do not advertise WebDAV support " +
                                                      "in response to 'OPTIONS *'." +
                                                      Environment.NewLine +
                                                      "As a request header, this header allows the client to advertise " +
                                                      "compliance with named features when the server needs that " +
                                                      "information. Clients SHOULD NOT send this header unless a " +
                                                      "standards track specification requires it. Any extension that " +
                                                      "makes use of this as a request header will need to carefully " +
                                                      "consider caching implications.",
                                                      "DAV : 1",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

        #endregion

        #region ETag

        /// <summary>
        /// The ETag response-header field provides the current value
        /// of the entity tag for the requested variant. The headers
        /// used with entity tags are described in sections 14.24,
        /// 14.26 and 14.44. The entity tag MAY be used for comparison
        /// with other entities from the same resource (see section 13.3.3). 
        /// </summary>         
        public static readonly HTTPHeaderField ETag = new HTTPHeaderField(
                                                      "ETag",
                                                      HeaderFieldType.Response,
                                                      "The ETag response-header field provides the current value " +
                                                      "of the entity tag for the requested variant. The headers " +
                                                      "used with entity tags are described in sections 14.24, " +
                                                      "14.26 and 14.44. The entity tag MAY be used for comparison " +
                                                      "with other entities from the same resource (see section 13.3.3).",
                                                      "ETag: 'xyzzy'",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Expires = new HTTPHeaderField(
                                                      "Expires",
                                                      HeaderFieldType.Response,
                                                      "The Expires entity-header field gives the date/time after " +
                                                      "which the response is considered stale. A stale cache entry " +
                                                      "may not normally be returned by a cache (either a proxy " +
                                                      "cache or a user agent cache) unless it is first validated " +
                                                      "with the origin server (or with an intermediate cache that " +
                                                      "has a fresh copy of the entity). See section 13.2 for " +
                                                      "further discussion of the expiration model." +
                                                      Environment.NewLine +
                                                      "The presence of an Expires field does not imply that the " +
                                                      "original resource will change or cease to exist at, before, " +
                                                      "or after that time." +
                                                      Environment.NewLine +
                                                      "The format is an absolute date and time as defined by HTTP- " +
                                                      "date in section 3.3.1; it MUST be in RFC 1123 date format." +
                                                      Environment.NewLine +
                                                      "If a response includes a Cache-Control field with the " +
                                                      "max-age directive (see section 14.9.3), that directive " +
                                                      "overrides the Expires field." +
                                                      Environment.NewLine +
                                                      "HTTP/1.1 clients and caches MUST treat other invalid date " +
                                                      "formats, especially including the value '0', as in the past " +
                                                      "(i.e., 'already expired')." +
                                                      Environment.NewLine +
                                                      "To mark a response as 'already expired', an origin server " +
                                                      "sends an Expires date that is equal to the Date header " +
                                                      "value. (See the rules for expiration calculations in " +
                                                      "section 13.2.4.)" +
                                                      Environment.NewLine +
                                                      "To mark a response as 'never expires', an origin server " +
                                                      "sends an Expires date approximately one year from the time " +
                                                      "the response is sent. HTTP/1.1 servers SHOULD NOT send " +
                                                      "Expires dates more than one year in the future." +
                                                      Environment.NewLine +
                                                      "The presence of an Expires header field with a date value of " +
                                                      "some time in the future on a response that otherwise would " +
                                                      "by default be non-cacheable indicates that the response is " +
                                                      "cacheable, unless indicated otherwise by a Cache-Control " +
                                                      "header field (section 14.9).",
                                                      "Expires: Thu, 01 Dec 1994 16:00:00 GMT",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField LastModified = new HTTPHeaderField(
                                                      "Last-Modified",
                                                      HeaderFieldType.Response,
                                                      "The Last-Modified entity-header field indicates the date " +
                                                      "and time at which the origin server believes the variant " +
                                                      "was last modified." +
                                                      Environment.NewLine +
                                                      "The exact meaning of this header field depends on the " +
                                                      "implementation of the origin server and the nature of " +
                                                      "the original resource. For files, it may be just the " +
                                                      "file system last-modified time. For entities with " +
                                                      "dynamically included parts, it may be the most recent " +
                                                      "of the set of last-modify times for its component parts. " +
                                                      "For database gateways, it may be the last-update time " +
                                                      "stamp of the record. For virtual objects, it may be the " +
                                                      "last time the internal state changed." +
                                                      Environment.NewLine +
                                                      "An origin server MUST NOT send a Last-Modified date which " +
                                                      "is later than the server's time of message origination. " +
                                                      "In such cases, where the resource's last modification " +
                                                      "would indicate some time in the future, the server MUST " +
                                                      "replace that date with the message origination date." +
                                                      Environment.NewLine +
                                                      "An origin server SHOULD obtain the Last-Modified value of " +
                                                      "the entity as close as possible to the time that it " +
                                                      "generates the Date value of its response. This allows a " +
                                                      "recipient to make an accurate assessment of the entity's " +
                                                      "modification time, especially if the entity changes near " +
                                                      "the time that the response is generated." +
                                                      Environment.NewLine +
                                                      "HTTP/1.1 servers SHOULD send Last-Modified whenever feasible.",
                                                      "Last-Modified: Tue, 15 Nov 1994 12:45:26 GMT",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Location = new HTTPHeaderField(
                                                      "Location",
                                                      HeaderFieldType.Response,
                                                      "The Location response-header field is used to redirect the " +
                                                      "recipient to a location other than the Request-URI for " +
                                                      "completion of the request or identification of a new " +
                                                      "resource. For 201 (Created) responses, the Location is that " +
                                                      "of the new resource which was created by the request. For " +
                                                      "3xx responses, the location SHOULD indicate the server's " +
                                                      "preferred URI for automatic redirection to the resource. " +
                                                      "The field value consists of a single absolute URI." +
                                                      Environment.NewLine +
                                                      "Note: The Content-Location header field (section 14.14) " +
                                                      "differs from Location in that the Content-Location identifies " +
                                                      "the original location of the entity enclosed in the request. " +
                                                      "It is therefore possible for a response to contain header " +
                                                      "fields for both Location and Content-Location. Also see " +
                                                      "section 13.10 for cache requirements of some methods.",
                                                      "Location: http://www.w3.org/pub/WWW/People.html",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField ProxyAuthenticate = new HTTPHeaderField(
                                                      "Proxy-Authenticate",
                                                      HeaderFieldType.Response,
                                                      "The Proxy-Authenticate response-header field MUST be included " +
                                                      "as part of a 407 (Proxy Authentication Required) response. The " +
                                                      "field value consists of a challenge that indicates the " +
                                                      "authentication scheme and parameters applicable to the proxy " +
                                                      "for this Request-URI." +
                                                      Environment.NewLine +
                                                      "The HTTP access authentication process is described in 'HTTP " +
                                                      "Authentication: Basic and Digest Access Authentication' [43]. " +
                                                      "Unlike WWW-Authenticate, the Proxy-Authenticate header field " +
                                                      "applies only to the current connection and SHOULD NOT be passed " +
                                                      "on to downstream clients. However, an intermediate proxy might " +
                                                      "need to obtain its own credentials by requesting them from the " +
                                                      "downstream client, which in some circumstances will appear as " +
                                                      "if the proxy is forwarding the Proxy-Authenticate header field.",
                                                      "Proxy-Authenticate: Basic",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField RetryAfter = new HTTPHeaderField(
                                                      "Retry-After",
                                                      HeaderFieldType.Response,
                                                      "The Retry-After response-header field can be used with a " +
                                                      "503 (Service Unavailable) response to indicate how long " +
                                                      "the service is expected to be unavailable to the requesting " +
                                                      "client. This field MAY also be used with any 3xx (Redirection) " +
                                                      "response to indicate the minimum time the user-agent is asked " +
                                                      "wait before issuing the redirected request. The value of this " +
                                                      "field can be either an HTTP-date or an integer number of " +
                                                      "seconds (in decimal) after the time of the response.",
                                                      "Retry-After: Fri, 31 Dec 1999 23:59:59 GMT" + Environment.NewLine + "Retry-After: 120",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Server = new HTTPHeaderField(
                                                      "Server",
                                                      HeaderFieldType.Response,
                                                      "The Server response-header field contains information about " +
                                                      "the software used by the origin server to handle the request. " +
                                                      "The field can contain multiple product tokens (section 3.8) " +
                                                      "and comments identifying the server and any significant " +
                                                      "subproducts. The product tokens are listed in order of their " +
                                                      "significance for identifying the application." +
                                                      Environment.NewLine +
                                                      "If the response is being forwarded through a proxy, the proxy " +
                                                      "application MUST NOT modify the Server response-header. Instead, " +
                                                      "it SHOULD include a Via field (as described in section 14.45)." +
                                                      Environment.NewLine +
                                                      "Note: Revealing the specific software version of the server " +
                                                      "might allow the server machine to become more vulnerable to " +
                                                      "attacks against software that is known to contain security " +
                                                      "holes. Server implementors are encouraged to make this field " +
                                                      "a configurable option.",
                                                      "Server: CERN/3.0 libwww/2.17",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField Vary = new HTTPHeaderField(
                                                      "Vary",
                                                      HeaderFieldType.Response,
                                                      "The Vary field value indicates the set of request-header " +
                                                      "fields that fully determines, while the response is fresh, " +
                                                      "whether a cache is permitted to use the response to reply " +
                                                      "to a subsequent request without revalidation. For uncacheable " +
                                                      "or stale responses, the Vary field value advises the user " +
                                                      "agent about the criteria that were used to select the " +
                                                      "representation. A Vary field value of '*' implies that a " +
                                                      "cache cannot determine from the request headers of a " +
                                                      "subsequent request whether this response is the appropriate " +
                                                      "representation. See section 13.6 for use of the Vary header " +
                                                      "field by caches." +
                                                      Environment.NewLine +
                                                      "An HTTP/1.1 server SHOULD include a Vary header field with " +
                                                      "any cacheable response that is subject to server-driven " +
                                                      "negotiation. Doing so allows a cache to properly interpret " +
                                                      "future requests on that resource and informs the user agent " +
                                                      "about the presence of negotiation on that resource. A server " +
                                                      "MAY include a Vary header field with a non-cacheable response " +
                                                      "that is subject to server-driven negotiation, since this might " +
                                                      "provide the user agent with useful information about the " +
                                                      "dimensions over which the response varies at the time of " +
                                                      "the response." +
                                                      Environment.NewLine +
                                                      "A Vary field value consisting of a list of field-names signals " +
                                                      "that the representation selected for the response is based on " +
                                                      "a selection algorithm which considers ONLY the listed request- " +
                                                      "header field values in selecting the most appropriate " +
                                                      "representation. A cache MAY assume that the same selection will " +
                                                      "be made for future requests with the same values for the listed " +
                                                      "field names, for the duration of time for which the response is " +
                                                      "fresh." +
                                                      Environment.NewLine +
                                                      "The field-names given are not limited to the set of standard " +
                                                      "request-header fields defined by this specification. Field " +
                                                      "names are case-insensitive." +
                                                      Environment.NewLine +
                                                      "A Vary field value of '*' signals that unspecified parameters " +
                                                      "not limited to the request-headers (e.g., the network address " +
                                                      "of the client), play a role in the selection of the response " +
                                                      "representation. The '*' value MUST NOT be generated by a proxy " +
                                                      "server; it may only be generated by an origin server.",
                                                      "Vary: *",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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
        public static readonly HTTPHeaderField WWWAuthenticate = new HTTPHeaderField(
                                                      "WWW-Authenticate",
                                                      HeaderFieldType.Response,
                                                      "The WWW-Authenticate response-header field MUST be included " +
                                                      "in 401 (Unauthorized) response messages. The field value " +
                                                      "consists of at least one challenge that indicates the " +
                                                      "authentication scheme(s) and parameters applicable to the " +
                                                      "Request-URI." +
                                                      Environment.NewLine +
                                                      "The HTTP access authentication process is described in 'HTTP " +
                                                      "Authentication: Basic and Digest Access Authentication' [43]. " +
                                                      "User agents are advised to take special care in parsing the " +
                                                      "WWW-Authenticate field value as it might contain more than " +
                                                      "one challenge, or if more than one WWW-Authenticate header " +
                                                      "field is provided, the contents of a challenge itself can " +
                                                      "contain a comma-separated list of authentication parameters.",
                                                      "WWW-Authenticate: Basic",
                                                      new Uri("http://tools.ietf.org/html/rfc2616"));

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

        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {
            return Name;
        }

        #endregion

    }

}
