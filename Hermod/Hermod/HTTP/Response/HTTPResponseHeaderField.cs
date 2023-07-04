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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    #region HTTPResponseHeaderField

    public class HTTPResponseHeaderField : HTTPHeaderField
    {

        #region Constructor(s)

        public HTTPResponseHeaderField(String               Name,
                                       RequestPathSemantic  RequestPathSemantic)

            : base(Name,
                   HeaderFieldType.Response,
                   RequestPathSemantic)

        { }

        #endregion


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
        public static readonly HTTPResponseHeaderField<UInt64?> Age = new ("Age",
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
        public static readonly HTTPResponseHeaderField<IEnumerable<HTTPMethod>> Allow = new ("Allow",
                                                                                             RequestPathSemantic.EndToEnd,
                                                                                             (String s, out IEnumerable<HTTPMethod>? o) => StringParsers.NullableHashSet(s, HTTPMethod.TryParse, out o));

        #endregion

        #region  Accept-Patch

        /// <summary>
        /// Accept-Patch
        /// </summary>
        public static readonly HTTPResponseHeaderField<IEnumerable<HTTPContentType>> AcceptPatch = new ("Accept-Patch",
                                                                                                        RequestPathSemantic.EndToEnd,
                                                                                                        (String s, out IEnumerable<HTTPContentType>? o) => StringParsers.NullableHashSet(s, HTTPContentType.TryParse, out o));

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
        public static readonly HTTPResponseHeaderField DAV = new ("DAV",
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
        public static readonly HTTPResponseHeaderField ETag = new ("ETag",
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
        public static readonly HTTPResponseHeaderField Expires = new ("Expires",
                                                                      RequestPathSemantic.EndToEnd);

        #endregion

        #region Keep-Alive

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
        public static readonly HTTPResponseHeaderField<KeepAliveType> KeepAlive = new ("Keep-Alive",
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
        public static readonly HTTPResponseHeaderField<DateTime> LastModified = new ("Last-Modified",
                                                                                     RequestPathSemantic.EndToEnd,
                                                                                     DateTime.TryParse,
                                                                                     dateTime => dateTime.ToIso8601());

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
        public static readonly HTTPResponseHeaderField<Location> Location = new ("Location",
                                                                                 RequestPathSemantic.EndToEnd,
                                                                                 HTTP.Location.TryParse);

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
        public static readonly HTTPResponseHeaderField ProxyAuthenticate = new ("Proxy-Authenticate",
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
        public static readonly HTTPResponseHeaderField RetryAfter = new ("Retry-After",
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
        public static readonly HTTPResponseHeaderField Server = new ("Server",
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
        public static readonly HTTPResponseHeaderField Vary = new ("Vary",
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
        public static readonly HTTPResponseHeaderField WWWAuthenticate = new ("WWW-Authenticate",
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
        public static readonly HTTPResponseHeaderField Refresh = new ("Refresh",
                                                                      RequestPathSemantic.EndToEnd);

        #endregion

        #region Set-Cookie

        /// <summary>
        /// Set a HTTP cookie.
        /// </summary>
        /// <example>Set-Cookie: UserID=JohnDoe; Max-Age=3600; Version=1</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/HTTP_cookie"/>
        public static readonly HTTPResponseHeaderField<HTTPCookies?> SetCookie = new ("Set-Cookie",
                                                                                      RequestPathSemantic.EndToEnd,
                                                                                      HTTPCookies.TryParse);

        #endregion


        // CORS

        #region Access-Control-Allow-Origin

        /// <summary>
        /// Access-Control-Allow-Origin.
        /// </summary>
        /// <example>Access-Control-Allow-Origin: *</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/Cross-origin_resource_sharing"/>
        public static readonly HTTPResponseHeaderField AccessControlAllowOrigin = new ("Access-Control-Allow-Origin",
                                                                                       RequestPathSemantic.EndToEnd);

        #endregion

        #region Access-Control-Allow-Methods

        /// <summary>
        /// Access-Control-Allow-Methods.
        /// </summary>
        /// <example>Access-Control-Allow-Methods: GET, PUT, POST, DELETE</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/Cross-origin_resource_sharing"/>
        public static readonly HTTPResponseHeaderField<IEnumerable<String>> AccessControlAllowMethods = new ("Access-Control-Allow-Methods",
                                                                                                             RequestPathSemantic.EndToEnd,
                                                                                                             StringParsers.NullableHashSetOfStrings);

        #endregion

        #region Access-Control-Allow-Headers

        /// <summary>
        /// Access-Control-Allow-Headers.
        /// </summary>
        /// <example>Access-Control-Allow-Headers: Content-Type</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/Cross-origin_resource_sharing"/>
        public static readonly HTTPResponseHeaderField<IEnumerable<String>> AccessControlAllowHeaders = new ("Access-Control-Allow-Headers",
                                                                                                             RequestPathSemantic.EndToEnd,
                                                                                                             StringParsers.NullableHashSetOfStrings);

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
        public static readonly HTTPResponseHeaderField<UInt64?> AccessControlMaxAge = new ("Access-Control-Max-Age",
                                                                                           RequestPathSemantic.EndToEnd,
                                                                                           StringParsers.NullableUInt64);

        #endregion


        // Non-standard response header fields

        #region XLocationAfterAuth

        /// <summary>
        /// Stores the original HTTP path and redirects to it after authentication.
        /// </summary>
        public static readonly HTTPResponseHeaderField XLocationAfterAuth = new ("X-LocationAfterAuth",
                                                                                 RequestPathSemantic.EndToEnd);

        #endregion

        #region X-ExpectedTotalNumberOfItems

        /// <summary>
        /// The expected total number of items within a resource collection.
        /// </summary>
        /// <example>X-ExpectedTotalNumberOfItems: 42</example>
        public static readonly HTTPResponseHeaderField<UInt64?> X_ExpectedTotalNumberOfItems = new ("X-ExpectedTotalNumberOfItems",
                                                                                                    RequestPathSemantic.EndToEnd,
                                                                                                    StringParsers.NullableUInt64);

        #endregion

        #region X-Frame-Options

        /// <summary>
        /// The X-Frame-Options HTTP response header can be used to indicate whether or not a browser
        /// should be allowed to render a page in a &lt;frame&gt;, &lt;iframe&gt; or &lt;object&gt;.
        /// Sites can use this to avoid clickjacking attacks, by ensuring that their content is not
        /// embedded into other sites.
        /// </summary>
        /// <example>DENY, SAMEORIGIN, ALLOW-FROM https://example.com</example>
        public static readonly HTTPResponseHeaderField X_FrameOptions = new ("X-Frame-Options",
                                                                             RequestPathSemantic.EndToEnd);

        #endregion

    }

    #endregion

    #region HTTPResponseHeaderField<T>

    public class HTTPResponseHeaderField<T> : HTTPHeaderField<T>
    {

        #region Constructor(s)

        public HTTPResponseHeaderField(String                       Name,
                                       RequestPathSemantic          RequestPathSemantic,
                                       TryParser<T>?                StringParser      = null,
                                       ValueSerializerDelegate<T>?  ValueSerializer   = null)

            : base(Name,
                   HeaderFieldType.Response,
                   RequestPathSemantic,
                   StringParser,
                   ValueSerializer)

        { }

        #endregion

    }

    #endregion

}
