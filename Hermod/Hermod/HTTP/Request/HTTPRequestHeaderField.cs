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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    #region HTTPRequestHeaderField

    public class HTTPRequestHeaderField : HTTPHeaderField
    {

        #region Constructor(s)

        /// <summary>
        /// Creates a new HTTP request header field.
        /// </summary>
        /// <param name="Name">The name of the HTTP request header field.</param>
        /// <param name="RequestPathSemantic">Whether a header field has and end-to-end or an hop-to-hop semantic.</param>
        /// <param name="MultipleValuesAsList">When set to true header fields having multiple values will be serialized as a comma separated list, otherwise as multiple lines.</param>
        public HTTPRequestHeaderField(String               Name,
                                      RequestPathSemantic  RequestPathSemantic,
                                      Boolean?             MultipleValuesAsList   = null)

            : base(Name,
                   HeaderFieldType.Request,
                   RequestPathSemantic,
                   MultipleValuesAsList)

        { }

        #endregion


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
        public static readonly HTTPRequestHeaderField<AcceptTypes> Accept = new ("Accept",
                                                                                 RequestPathSemantic.EndToEnd,
                                                                                 StringParser: AcceptTypes.TryParse);

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
        public static readonly HTTPRequestHeaderField AcceptCharset = new ("Accept-Charset",
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
        public static readonly HTTPRequestHeaderField AcceptEncoding = new ("Accept-Encoding",
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
        public static readonly HTTPRequestHeaderField AcceptLanguage = new ("Accept-Language",
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
        public static readonly HTTPRequestHeaderField AcceptRanges = new ("Accept-Ranges",
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
        public static readonly HTTPRequestHeaderField<IHTTPAuthentication> Authorization = new ("Authorization",
                                                                                                RequestPathSemantic.EndToEnd,
                                                                                                StringParser: HTTPAuthenticationExtensions.TryParse);

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
        public static readonly HTTPRequestHeaderField Depth = new ("Depth",
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
        public static readonly HTTPRequestHeaderField Destination = new ("Destination",
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
        public static readonly HTTPRequestHeaderField Expect = new ("Expect",
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
        public static readonly HTTPRequestHeaderField From  = new ("From",
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
        public static readonly HTTPRequestHeaderField<HTTPHostname> Host = new ("Host",
                                                                                RequestPathSemantic.EndToEnd,
                                                                                StringParser: HTTPHostname.TryParse);

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
        public static readonly HTTPRequestHeaderField If = new ("If",
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
        public static readonly HTTPRequestHeaderField IfMatch = new ("If-Match",
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
        public static readonly HTTPRequestHeaderField IfModifiedSince = new ("If-Modified-Since",
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
        public static readonly HTTPRequestHeaderField IfNoneMatch = new ("If-None-Match",
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
        public static readonly HTTPRequestHeaderField IfRange = new ("If-Range",
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
        public static readonly HTTPRequestHeaderField IfUnmodifiedSince = new ("If-Unmodified-Since",
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
        public static readonly HTTPRequestHeaderField LockToken = new ("Lock-Token",
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
        public static readonly HTTPRequestHeaderField<UInt64?> MaxForwards = new ("Max-Forwards",
                                                                                  RequestPathSemantic.EndToEnd,
                                                                                  StringParser: StringParsers.NullableUInt64);

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
        public static readonly HTTPRequestHeaderField Overwrite = new ("Overwrite",
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
        public static readonly HTTPRequestHeaderField ProxyAuthorization = new ("Proxy-Authorization",
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
        public static readonly HTTPRequestHeaderField Range = new ("Range",
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
        public static readonly HTTPRequestHeaderField Referer = new ("Referer",
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
        public static readonly HTTPRequestHeaderField TE = new ("TE",
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
        public static readonly HTTPRequestHeaderField<TimeSpan?> Timeout = new ("Timeout",
                                                                                RequestPathSemantic.EndToEnd,
                                                                                StringParser: StringParsers.NullableTimeSpan);

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
        public static readonly HTTPRequestHeaderField UserAgent = new ("User-Agent",
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
        public static readonly HTTPRequestHeaderField<UInt64?> LastEventId = new ("Last-Event-Id",
                                                                                  RequestPathSemantic.EndToEnd,
                                                                                  StringParser: StringParsers.NullableUInt64);

        #endregion

        #region Cookie

        /// <summary>
        /// Send a HTTP cookie.
        /// In fact this can be a collection of cookies separated by "; ".
        /// </summary>
        /// <example>Cookie: UserID=JohnDoe; Max-Age=3600; Version=1</example>
        /// <seealso cref="http://en.wikipedia.org/wiki/HTTP_cookie"/>
        public static readonly HTTPRequestHeaderField<HTTPCookies> Cookie = new ("Cookie",
                                                                                 RequestPathSemantic.EndToEnd,
                                                                                 StringParser: HTTPCookies.TryParse);

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
        public static readonly HTTPRequestHeaderField<Boolean> DNT = new ("DNT",
                                                                          RequestPathSemantic.EndToEnd,
                                                                          StringParser: (String s, out Boolean b) => { b = s.Trim() == "1"; return true; });

        #endregion


        // Non-standard request header fields

        #region X-Real-IP

        /// <summary>
        /// Intermediary HTTP proxies might include this field to
        /// indicate the real IP address of the HTTP client.
        /// </summary>
        /// <example>X-Real-IP: 95.91.73.30</example>
        public static readonly HTTPRequestHeaderField<IIPAddress> X_Real_IP = new ("X-Real-IP",
                                                                                   RequestPathSemantic.HopToHop,
                                                                                   StringParser: IPAddress.TryParse);

        #endregion

        #region X-Forwarded-For

        /// <summary>
        /// Intermediary HTTP proxies might include this field to
        /// indicate the real IP address of the HTTP client.
        /// </summary>
        /// <example>X-Forwarded-For: 95.91.73.30</example>
        public static readonly HTTPRequestHeaderField<IEnumerable<IIPAddress>> X_Forwarded_For = new ("X-Forwarded-For",
                                                                                                      RequestPathSemantic.HopToHop,
                                                                                                      MultipleValuesAsList:  true,
                                                                                                      StringParser:         (String s, out IEnumerable<IIPAddress>? o) => StringParsers.NullableHashSet(s, IPAddress.TryParse, out o));

        #endregion

        #region API_Key

        /// <summary>
        /// An API key for authentication.
        /// </summary>
        /// <example>API-Key: vfsf87wefh8743tzfgw9f489fh9fgs9z9z237hd208du79ehcv86egfsrf</example>
        public static readonly HTTPRequestHeaderField<APIKey_Id> API_Key = new ("API-Key",
                                                                                RequestPathSemantic.EndToEnd,
                                                                                StringParser: APIKey_Id.TryParse);

        #endregion

        #region X_ClientId

        /// <summary>
        /// A client identification for better logging.
        /// </summary>
        /// <example>X-ClientId: Hello world</example>
        public static readonly HTTPRequestHeaderField X_ClientId = new ("X-ClientId",
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
        public static readonly HTTPRequestHeaderField<Boolean> X_Portal = new ("X-Portal",
                                                                               RequestPathSemantic.EndToEnd,
                                                                               StringParser: (String s, out Boolean b) => { b = s.Trim().ToLower() == "true"; return true; });

        #endregion


    }

    #endregion

    #region HTTPRequestHeaderField<T>

    public class HTTPRequestHeaderField<T> : HTTPHeaderField<T>
    {

        #region Constructor(s)

        /// <summary>
        /// Creates a new HTTP request header field.
        /// </summary>
        /// <param name="Name">The name of the HTTP request header field.</param>
        /// <param name="RequestPathSemantic">Whether a header field has and end-to-end or an hop-to-hop semantic.</param>
        /// <param name="MultipleValuesAsList">When set to true header fields having multiple values will be serialized as a comma separated list, otherwise as multiple lines.</param>
        /// <param name="StringParser">Parse this HTTPHeaderField from a string.</param>
        /// <param name="ValueSerializer">A delegate to serialize the value of the header field to a string.</param>
        public HTTPRequestHeaderField(String                       Name,
                                      RequestPathSemantic          RequestPathSemantic,
                                      Boolean?                     MultipleValuesAsList   = null,
                                      TryParser<T>?                StringParser           = null,
                                      ValueSerializerDelegate<T>?  ValueSerializer        = null)

            : base(Name,
                   HeaderFieldType.Request,
                   RequestPathSemantic,
                   MultipleValuesAsList,
                   StringParser,
                   ValueSerializer)

        { }

        #endregion

    }

    #endregion

}
