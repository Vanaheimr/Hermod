/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// HTTP status codes as defined within RFC 2616 (Hypertext Transfer Protocol v1.1) and other resources.
    /// http://www.iana.org/assignments/http-status-codes/http-status-codes.xhtml
    /// http://en.wikipedia.org/wiki/List_of_HTTP_status_codes
    /// </summary>
    public class HTTPStatusCode : IEquatable<HTTPStatusCode>,
                                  IComparable<HTTPStatusCode>,
                                  IComparable
    {

        #region Properties

        /// <summary>
        /// The code of this HTTP status code
        /// </summary>
        public UInt32 Code          { get; }

        /// <summary>
        /// The name of this HTTP status code
        /// </summary>
        public String Name          { get; }

        /// <summary>
        /// A description of this HTTP status code
        /// </summary>
        public String Description   { get; }


        /// <summary>
        /// Is informational.
        /// </summary>
        public Boolean IsInformational
            => Code >= 100 && Code < 200;

        /// <summary>
        /// Is successful.
        /// </summary>
        public Boolean IsSuccessful
            => Code >= 200 && Code < 300;

        /// <summary>
        /// Is redirection.
        /// </summary>
        public Boolean IsRedirection
            => Code >= 300 && Code < 400;

        /// <summary>
        /// Is client error.
        /// </summary>
        public Boolean IsClientError
            => Code >= 400 && Code < 500;

        /// <summary>
        /// Is server error.
        /// </summary>
        public Boolean IsServerError
            => Code >= 500 && Code < 600;



        /// <summary>
        /// SimpleString.
        /// </summary>
        public String SimpleString
            => String.Concat(Code, " ", Name);

        /// <summary>
        /// Create a status code line from a HTTPStatusCode.
        /// </summary>
        public String HTTPResponseString
            => String.Concat("HTTP/1.1 ", Code, " ", Name);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new HTTP status coded based on a code, name and an optional description.
        /// </summary>
        /// <param name="Code">The code of the HTTP status code</param>
        /// <param name="Name">The name of the HTTP status code</param>
        /// <param name="Description">An optional description of the HTTP status code</param>
        public HTTPStatusCode(UInt32  Code,
                              String  Name,
                              String  Description = null)
        {

            this.Code         = Code;
            this.Name         = Name;
            this.Description  = Description;

        }

        #endregion


        public static readonly HTTPStatusCode ClientError = new HTTPStatusCode(
                                              0,
                                              "ClientError",
                                              "Indicates that the client can not process or send this request.");

        #region 1xx Informational

        /*
         This class of status code indicates a provisional response,
         consisting only of the Status-Line and optional headers, and is
         terminated by an empty line. There are no required headers for this
         class of status code.
        */

        /// <summary>
        /// Indicates that the client can continue with its request.
        /// </summary>
        public static readonly HTTPStatusCode Continue = new HTTPStatusCode(
                                              100,
                                              "Continue",
                                              "Indicates that the client can continue with its request.");

        /// <summary>
        /// Indicates that the protocol version or protocol is being changed.
        /// </summary>
        public static readonly HTTPStatusCode SwitchingProtocols = new HTTPStatusCode(
                                              101,
                                              "Switching Protocols",
                                              "Indicates that the protocol version or protocol is being changed.");

        #endregion

        #region 2xx Successful

        /*
         This class of status code indicates that the client's request was
         successfully received, understood, and accepted.
        */

        /// <summary>
        /// Indicates that the request succeeded and that the requested information
        /// is in the response.
        /// </summary>
        public static readonly HTTPStatusCode OK = new HTTPStatusCode(
                                              200,
                                              "OK",
                                              "Indicates that the request succeeded and that the requested information is in the response.");

        /// <summary>
        /// The request has been fulfilled and resulted in a new resource being created.
        /// The newly created resource can be referenced by the URI(s) returned in the Location header field.
        /// </summary>
        public static readonly HTTPStatusCode Created = new HTTPStatusCode(
                                              201,
                                              "Created",
                                              "The request has been fulfilled and resulted in a new resource being created. " +
                                              "The newly created resource can be referenced by the URI(s) returned in the Location header field.");

        /// <summary>
        /// The request has been accepted for processing, but the processing has
        /// not been completed. The request might or might not eventually be acted
        /// upon, as it might be disallowed when processing actually takes place.
        /// </summary>
        public static readonly HTTPStatusCode Accepted = new HTTPStatusCode(
                                              202,
                                              "Accepted",
                                              "The request has been accepted for processing, but the processing has not been completed. " +
                                              "The request might or might not eventually be acted upon, as it might be disallowed when processing actually takes place.");

        /// <summary>
        /// The returned metainformation in the entity-header is not the
        /// definitive set as available from the origin server, but is gathered
        /// from a local or a third-party copy. The set presented MAY be a subset
        /// or superset of the original version. Use of this response code is not
        /// required and is only appropriate when the response would otherwise be
        /// 200 (OK).
        /// </summary>
        public static readonly HTTPStatusCode NonAuthoritativeInformation = new HTTPStatusCode(
                                              203,
                                              "Non-Authoritative Information",
                                              "The returned metainformation in the entity-header is not the definitive set as available " +
                                              "from the origin server, but is gathered from a local or a third-party copy. The set " +
                                              "presented MAY be a subset or superset of the original version. Use of this response code " +
                                              "is not required and is only appropriate when the response would otherwise be 200 (OK).");

        /// <summary>
        /// The server has fulfilled the request but does not need to return an
        /// entity-body, and might want to return updated metainformation.
        /// </summary>
        public static readonly HTTPStatusCode NoContent = new HTTPStatusCode(
                                              204,
                                              "No Content",
                                              "The server has fulfilled the request but does not need to return an entity-body, " +
                                              "and might want to return updated metainformation.");

        /// <summary>
        /// The server has fulfilled the request and the user agent SHOULD reset
        /// the document view which caused the request to be sent. The response
        /// MUST NOT include an entity.
        /// </summary>
        public static readonly HTTPStatusCode ResetContent = new HTTPStatusCode(
                                              205,
                                              "Reset Content",
                                              "The server has fulfilled the request and the user agent SHOULD reset the document " +
                                              "view which caused the request to be sent. The response MUST NOT include an entity.");

        /// <summary>
        /// The server has fulfilled the partial GET request for the resource.
        /// The request MUST have included a Range header field indicating the
        /// desired range, and MAY have included an If-Range header field to
        /// make the request conditional.
        /// </summary>
        public static readonly HTTPStatusCode PartialContent = new HTTPStatusCode(
                                              206,
                                              "Partial Content",
                                              "The server has fulfilled the partial GET request for the resource. " +
                                              "The request MUST have included a Range header field indicating the desired range, " +
                                              "and MAY have included an If-Range header field to make the request conditional.");

        /// <summary>
        /// The response is a MultiStatus response as requested by a WebDAV PROPFIND
        /// request that contains the content of a requested destination.
        /// </summary>
        public static readonly HTTPStatusCode MultiStatus = new HTTPStatusCode(
                                              207,
                                              "Multi-Status",
                                              "The response is a MultiStatus response as requested by a WebDAV PROPFIND " +
                                              "request that contains the content of a requested destination.");

        #endregion

        #region 3xx Redirection

        /*
         This class of status code indicates that further action needs to be
         taken by the user agent in order to fulfill the request.
        */

        /// <summary>
        /// The requested resource corresponds to any one of a set of representations,
        /// each with its own specific location. The client can select a preferred
        /// representation and redirect its request to that location.
        /// </summary>
        public static readonly HTTPStatusCode MultipleChoices = new HTTPStatusCode(
                                              300,
                                              "Multiple Choices",
                                              "The requested resource corresponds to any one of a set of representations, " +
                                              "each with its own specific location. The client can select a preferred " +
                                              "representation and redirect its request to that location.");

        /// <summary>
        /// The requested resource has been assigned a new permanent URI and any
        /// future references to this resource SHOULD use one of the returned URIs
        /// provided in the location field of the response header.
        /// </summary>
        public static readonly HTTPStatusCode MovedPermanently = new HTTPStatusCode(
                                              301,
                                              "Moved Permanently",
                                              "The requested resource has been assigned a new permanent URI and any " +
                                              "future references to this resource SHOULD use one of the returned URIs "+
                                              "provided in the location field of the response header.");

        /// <summary>
        /// The requested resource resides temporarily under a different URI
        /// provided in the location field of the response header.
        /// </summary>
        public static readonly HTTPStatusCode Found = new HTTPStatusCode(
                                              302,
                                              "Found",
                                              "The requested resource resides temporarily under a different URI " +
                                              "provided in the location field of the response header.");

        /// <summary>
        /// The response to the request can be found under a different URI and
        /// SHOULD be retrieved using a GET method on that resource.
        /// </summary>
        public static readonly HTTPStatusCode SeeOther = new HTTPStatusCode(
                                              303,
                                              "See Other",
                                              "The response to the request can be found under a different URI and " +
                                              "SHOULD be retrieved using a GET method on that resource.");
        
        /// <summary>
        /// If the client has performed a conditional GET request and access is
        /// allowed, but the document has not been modified, the server SHOULD
        /// respond with this status code and MUST NOT send a message-body.
        /// </summary>
        public static readonly HTTPStatusCode NotModified = new HTTPStatusCode(
                                              304,
                                              "Not Modified",
                                              "If the client has performed a conditional GET request and access is " +
                                              "allowed, but the document has not been modified, the server SHOULD " +
                                              "respond with this status code and MUST NOT send a message-body.");
        
        /// <summary>
        /// The requested resource MUST be accessed through the proxy given by
        /// the Location field.
        /// </summary>
        public static readonly HTTPStatusCode UseProxy = new HTTPStatusCode(
                                              305,
                                              "Use Proxy",
                                              "The requested resource MUST be accessed through the proxy given by " +
                                              "the Location field.");
        
        /// <summary>
        /// Switch Proxy
        /// </summary>
        public static readonly HTTPStatusCode SwitchProxy = new HTTPStatusCode(
                                              306,
                                              "Switch Proxy");
        
        /// <summary>
        /// The requested resource resides temporarily under a different URI.
        /// Since the redirection MAY be altered on occasion, the client SHOULD
        /// continue to use the Request-URI for future requests.
        /// </summary>
        public static readonly HTTPStatusCode TemporaryRedirect = new HTTPStatusCode(
                                              307,
                                              "Temporary Redirect",
                                              "The requested resource resides temporarily under a different URI. " +
                                              "Since the redirection MAY be altered on occasion, the client SHOULD " +
                                              "continue to use the Request-URI for future requests.");

        #endregion

        #region 4xx Client Error

        /*
         The 4xx class of status code is intended for cases in which the
         client seems to have erred. Except when responding to a HEAD request,
         the server SHOULD include an entity containing an explanation of the
         error situation, and whether it is a temporary or permanent
         condition. These status codes are applicable to any request method.
         User agents SHOULD display any included entity to the user.
        */

        /// <summary>
        /// The request could not be understood by the server due to malformed
        /// syntax. The client SHOULD NOT repeat the request without modifications.
        /// </summary>
        public static readonly HTTPStatusCode BadRequest = new HTTPStatusCode(
                                              400,
                                              "Bad Request",
                                              "The request could not be understood by the server due to malformed " +
                                              "syntax. The client SHOULD NOT repeat the request without modifications.");

        /// <summary>
        /// The request requires user authentication. The response MUST include a
        /// WWW-Authenticate header field containing a challenge applicable to
        /// the requested resource. If the request already included Authorization
        /// credentials, then the 401 response indicates that authorization has
        /// been refused for those credentials.
        /// </summary>
        public static readonly HTTPStatusCode Unauthorized = new HTTPStatusCode(
                                              401,
                                              "Unauthorized",
                                              "The request requires user authentication. The response MUST include a " +
                                              "WWW-Authenticate header field containing a challenge applicable to " +
                                              "the requested resource. If the request already included Authorization " +
                                              "credentials, then the 401 response indicates that authorization has " +
                                              "been refused for those credentials.");

        /// <summary>
        /// This code is reserved for future use.
        /// </summary>
        public static readonly HTTPStatusCode PaymentRequired = new HTTPStatusCode(
                                              402,
                                              "Payment Required",
                                              "This code is reserved for future use.");

        /// <summary>
        /// The server understood the request, but is refusing to fulfill it.
        /// Authorization will not help and the request SHOULD NOT be repeated.
        /// </summary>
        public static readonly HTTPStatusCode Forbidden = new HTTPStatusCode(
                                              403,
                                              "Forbidden",
                                              "The server understood the request, but is refusing to fulfill it. " +
                                              "Authorization will not help and the request SHOULD NOT be repeated.");

        /// <summary>
        /// The server has not found anything matching the Request-URI.
        /// No indication is given of whether the condition is temporary or permanent.
        /// </summary>
        public static readonly HTTPStatusCode NotFound = new HTTPStatusCode(
                                              404,
                                              "Not Found",
                                              "The server has not found anything matching the Request-URI. " +
                                              "No indication is given of whether the condition is temporary or permanent.");

        /// <summary>
        /// The method specified in the Request-Line is not allowed for the
        /// resource identified by the Request-URI. The response MUST include an
        /// Allow header containing a list of valid methods for the requested resource.
        /// </summary>
        public static readonly HTTPStatusCode MethodNotAllowed = new HTTPStatusCode(
                                              405,
                                              "Method Not Allowed",
                                              "The method specified in the Request-Line is not allowed for the " +
                                              "resource identified by the Request-URI. The response MUST include an " +
                                              "Allow header containing a list of valid methods for the requested resource.");

        /// <summary>
        /// The client has indicated with Accept headers that it will not
        /// accept any of the available representations of the resource.
        /// </summary>
        public static readonly HTTPStatusCode NotAcceptable = new HTTPStatusCode(
                                              406,
                                              "Not Acceptable",
                                              "The client has indicated with Accept headers that it will not " +
                                              "accept any of the available representations of the resource.");

        /// <summary>
        /// This code is similar to 401 (Unauthorized), but indicates that the
        /// client must first authenticate itself with the proxy. The proxy MUST
        /// return a Proxy-Authenticate header field containing a challenge
        /// applicable to the proxy for the requested resource.
        /// </summary>
        public static readonly HTTPStatusCode ProxyAuthenticationRequired   = new HTTPStatusCode(
                                              407,
                                              "Proxy Authentication Required",
                                              "This code is similar to 401 (Unauthorized), but indicates that the " +
                                              "client must first authenticate itself with the proxy. The proxy MUST " +
                                              "return a Proxy-Authenticate header field containing a challenge " +
                                              "applicable to the proxy for the requested resource.");

        /// <summary>
        /// The client did not produce a request within the time that the server
        /// was prepared to wait. The client MAY repeat the request without
        /// modifications at any later time.
        /// </summary>
        public static readonly HTTPStatusCode RequestTimeout = new HTTPStatusCode(
                                              408,
                                              "Request Timeout",
                                              "The client did not produce a request within the time that the server " +
                                              "was prepared to wait. The client MAY repeat the request without " +
                                              "modifications at any later time.");

        /// <summary>
        /// The request could not be completed due to a conflict with the current
        /// state of the resource. This code is only allowed in situations where
        /// it is expected that the user might be able to resolve the conflict
        /// and resubmit the request.
        /// </summary>
        public static readonly HTTPStatusCode Conflict = new HTTPStatusCode(
                                              409,
                                              "Conflict",
                                              "The request could not be completed due to a conflict with the current " +
                                              "state of the resource. This code is only allowed in situations where " +
                                              "it is expected that the user might be able to resolve the conflict " +
                                              "and resubmit the request.");

        /// <summary>
        /// The requested resource is no longer available at the server and
        /// no forwarding address is known. This condition is expected to be
        /// considered permanent.
        /// </summary>
        public static readonly HTTPStatusCode Gone = new HTTPStatusCode(
                                              410,
                                              "Gone",
                                              "The requested resource is no longer available at the server and " +
                                              "no forwarding address is known. This condition is expected to be " +
                                              "considered permanent.");

        /// <summary>
        /// The server refuses to accept the request without a
        /// content-length header.
        /// </summary>
        public static readonly HTTPStatusCode LengthRequired = new HTTPStatusCode(
                                              411,
                                              "Length Required",
                                              "The server refuses to accept the request without a content-length header.");

        /// <summary>
        /// The precondition given in one or more of the request-header
        /// fields evaluated to false when it was tested on the server.
        /// </summary>
        public static readonly HTTPStatusCode PreconditionFailed = new HTTPStatusCode(
                                              412,
                                              "Precondition Failed",
                                              "The precondition given in one or more of the request-header fields " +
                                              "evaluated to false when it was tested on the server.");

        /// <summary>
        /// The server is refusing to process a request because the request
        /// entity is larger than the server is willing or able to process. The
        /// server MAY close the connection to prevent the client from continuing
        /// the request.
        /// </summary>
        public static readonly HTTPStatusCode RequestEntityTooLarge = new HTTPStatusCode(
                                              413,
                                              "Request Entity Too Large",
                                              "The server is refusing to process a request because the request " +
                                              "entity is larger than the server is willing or able to process. The " +
                                              "server MAY close the connection to prevent the client from continuing " +
                                              "the request.");

        /// <summary>
        /// The server is refusing to service the request because the
        /// Request-URI is longer than the server is willing to interpret.
        /// </summary>
        public static readonly HTTPStatusCode RequestURITooLong = new HTTPStatusCode(
                                              414,
                                              "Request-URI Too Long",
                                              "The server is refusing to service the request because the Request-URI " +
                                              "is longer than the server is willing to interpret.");

        /// <summary>
        /// The server is refusing to service the request because the entity of
        /// the request is in a format not supported by the requested resource
        /// for the requested method.
        /// </summary>
        public static readonly HTTPStatusCode UnsupportedMediaType = new HTTPStatusCode(
                                              415,
                                              "Unsupported Media Type",
                                              "The server is refusing to service the request because the entity of " +
                                              "the request is in a format not supported by the requested resource " +
                                              "for the requested method.");

        /// <summary>
        /// A server SHOULD return a response with this status code if a request
        /// included a Range request-header field, and none of the range-specifier
        /// values in this field overlap the current extent of the selected
        /// resource, and the request did not include an If-Range request-header field.
        /// </summary>
        public static readonly HTTPStatusCode RequestedRangeNotSatisfiable = new HTTPStatusCode(
                                              416,
                                              "Requested Range Not Satisfiable",
                                              "A server SHOULD return a response with this status code if a request " +
                                              "included a Range request-header field, and none of the range-specifier " +
                                              "values in this field overlap the current extent of the selected " +
                                              "resource, and the request did not include an If-Range request-header field.");

        /// <summary>
        /// The expectation given in an Expect request-header field could not be met
        /// by this server, or, if the server is a proxy, the server has unambiguous
        /// evidence that the request could not be met by the next-hop server.
        /// </summary>
        public static readonly HTTPStatusCode ExpectationFailed = new HTTPStatusCode(
                                              417,
                                              "Expectation Failed",
                                              "The expectation given in an Expect request-header field could not be met " +
                                              "by this server, or, if the server is a proxy, the server has unambiguous " +
                                              "evidence that the request could not be met by the next-hop server.");

        /// <summary>
        /// Unfortunately this coffee machine is out of coffee.
        /// See RFC 2324 for details!
        /// </summary>
        public static readonly HTTPStatusCode ImATeapot = new HTTPStatusCode(
                                              418,
                                              "I'm a teapot",
                                              "Unfortunately this coffee machine is out of coffee." + 
                                              "See RFC 2324 for details!");

        /// <summary>
        /// The server understands the media type of the request entity,
        /// but was unable to process the contained instructions.
        /// </summary>
        public static readonly HTTPStatusCode UnprocessableEntity = new HTTPStatusCode(
                                              422,
                                              "Unprocessable Entity",
                                              "The server understands the media type of the request entity, " +
                                              "but was unable to process the contained instructions.");

        /// <summary>
        /// The requested resource is currently locked.
        /// The lock must be released or proper identification
        /// given before the method can be applied.
        /// </summary>
        public static readonly HTTPStatusCode Locked = new HTTPStatusCode(
                                              423,
                                              "Locked",
                                              "The requested resource is currently locked. " +
                                              "The lock must be released or proper identification " +
                                              "given before the method can be applied.");
        
        /// <summary>
        /// The method could not be performed on the resource because the
        /// requested action depended on another action and that other
        /// action failed.
        /// </summary>
        public static readonly HTTPStatusCode FailedDependency = new HTTPStatusCode(
                                              424,
                                              "Failed Dependency",
                                              "The method could not be performed on the resource because " +
                                              "the requested action depended on another action and that other " +
                                              "action failed.");

        /// <summary>
        /// No code
        /// </summary>
        public static readonly HTTPStatusCode NoCode = new HTTPStatusCode(
                                              425,
                                              "No code");

        /// <summary>
        /// The requested resource can only be retrieved using SSL.
        /// The server is willing to upgrade the current connection to SSL,
        /// but your client doesn't support it. Either upgrade your client,
        /// or try requesting the page using https://
        /// </summary>
        public static readonly HTTPStatusCode UpgradeRequired = new HTTPStatusCode(
                                              426,
                                              "Upgrade Required",
                                              "The requested resource can only be retrieved using SSL. " +
                                              "The server is willing to upgrade the current connection to " +
                                              "SSL, but your client doesn't support it. Either upgrade your " +
                                              "client, or try requesting the page using https:// ");


        /// <summary>
        /// The 428 status code indicates that the origin server requires the
        /// request to be conditional.
        /// Its typical use is to avoid the "lost update" problem, where a client
        /// GETs a resource's state, modifies it, and PUTs it back to the server,
        /// when meanwhile a third party has modified the state on the server,
        /// leading to a conflict.  By requiring requests to be conditional, the
        /// server can assure that clients are working with the correct copies.
        /// Responses using this status code SHOULD explain how to resubmit the
        /// request successfully.
        /// For example: This request is required to be conditional;
        /// try using the 'If-Match'-header.
        /// </summary>
        public static readonly HTTPStatusCode PreconditionRequired = new HTTPStatusCode(
                                              428,
                                              "Precondition Required",
                                              "The 428 status code indicates that the origin server requires the " +
                                              "request to be conditional. " + Environment.NewLine +
                                              "Its typical use is to avoid the 'lost update' problem, where a client " +
                                              "GETs a resource's state, modifies it, and PUTs it back to the server, " +
                                              "when meanwhile a third party has modified the state on the server, " +
                                              "leading to a conflict.  By requiring requests to be conditional, the " +
                                              "server can assure that clients are working with the correct copies. " +
                                              "Responses using this status code SHOULD explain how to resubmit the " +
                                              "request successfully. " + Environment.NewLine +
                                              "For example: This request is required to be " +
                                              "conditional; try using the 'If-Match'-header.");

        #endregion

        #region 5xx Server Error

        /*
         The 5xx class of status code is intended for cases in
         which the server is aware that it has erred or is incapable of
         performing the request.
        */

        /// <summary>
        /// The server encountered an unexpected condition which
        /// prevented it from fulfilling the request.
        /// </summary>
        public static readonly HTTPStatusCode InternalServerError = new HTTPStatusCode(
                                              500,
                                              "Internal Server Error",
                                              "The server encountered an unexpected condition which prevented " +
                                              "it from fulfilling the request.");

        /// <summary>
        /// The server does not support the functionality required to fulfill the
        /// request. This is the appropriate response when the server does not
        /// recognize the request method and is not capable of supporting it for
        /// any resource.
        /// </summary>
        public static readonly HTTPStatusCode NotImplemented = new HTTPStatusCode(
                                              501,
                                              "Not Implemented",
                                              "The server does not support the functionality required to fulfill the " +
                                              "request. This is the appropriate response when the server does not " +
                                              "recognize the request method and is not capable of supporting it for " +
                                              "any resource.");
        
        /// <summary>
        /// The server, while acting as a gateway or proxy, received an invalid
        /// response from the upstream server it accessed in attempting to
        /// fulfill the request.
        /// </summary>
        public static readonly HTTPStatusCode BadGateway = new HTTPStatusCode(
                                              502,
                                              "Bad Gateway",
                                              "The server, while acting as a gateway or proxy, received an invalid " +
                                              "response from the upstream server it accessed in attempting to " +
                                              "fulfill the request.");
        
        /// <summary>
        /// The server is currently unable to handle the request due to a
        /// temporary overloading or maintenance of the server. The implication
        /// is that this is a temporary condition which will be alleviated after
        /// some delay. If known, the length of the delay MAY be indicated in a
        /// Retry-After header. If no Retry-After is given, the client SHOULD
        /// handle the response as it would for a 500 response.
        /// </summary>
        public static readonly HTTPStatusCode ServiceUnavailable = new HTTPStatusCode(
                                              503,
                                              "Service Unavailable",
                                              "The server is currently unable to handle the request due to a " +
                                              "temporary overloading or maintenance of the server. The implication " +
                                              "is that this is a temporary condition which will be alleviated after " +
                                              "some delay. If known, the length of the delay MAY be indicated in a " +
                                              "Retry-After header. If no Retry-After is given, the client SHOULD " +
                                              "handle the response as it would for a 500 response.");
        
        /// <summary>
        /// The server, while acting as a gateway or proxy, did not receive a
        /// timely response from the upstream server specified or some other
        /// auxiliary server (e.g. DNS) it needed to access in attempting to
        /// complete the request.
        /// </summary>
        public static readonly HTTPStatusCode GatewayTimeout = new HTTPStatusCode(
                                              504,
                                              "Gateway Timeout",
                                              "The server, while acting as a gateway or proxy, did not receive a " +
                                              "timely response from the upstream server specified or some other " +
                                              "auxiliary server (e.g. DNS) it needed to access in attempting to " +
                                              "complete the request.");

        /// <summary>
        /// The server does not support, or refuses to support, the HTTP
        /// protocol version that was used in the request message.
        /// </summary>
        public static readonly HTTPStatusCode HTTPVersionNotSupported = new HTTPStatusCode(
                                              505,
                                              "HTTP Version Not Supported",
                                              "The server does not support, or refuses to support, the HTTP protocol " +
                                              "version that was used in the request message.");

        /// <summary>
        /// Transparent content negotiation for the request results in a circular reference.
        /// </summary>
        public static readonly HTTPStatusCode VariantAlsoNegotiates = new HTTPStatusCode(
                                              506,
                                              "Variant Also Negotiates",
                                              "Transparent content negotiation for the request results in a " +
                                              "circular reference.");

        /// <summary>
        /// Indicates a InsufficientStorage of a WebDAV upload request.
        /// </summary>
        public static readonly HTTPStatusCode InsufficientStorage = new HTTPStatusCode(
                                              507,
                                              "Insufficient Storage",
                                              "Indicates a InsufficientStorage of a WebDAV upload request.");

        /// <summary>
        /// Bandwidth Limit Exceeded
        /// </summary>
        public static readonly HTTPStatusCode BandwidthLimitExceeded = new HTTPStatusCode(
                                              509,
                                              "Bandwidth Limit Exceeded");

        /// <summary>
        /// Further extensions to the request are required for the server to fulfill it.
        /// </summary>
        public static readonly HTTPStatusCode NotExtended = new HTTPStatusCode(
                                              510,
                                              "Not Extended",
                                              "Further extensions to the request are required for the server to " +
                                              "fulfill it.");

        #endregion


        #region ParseInt(myCode)

        /// <summary>
        /// Tries to find the appropriate HTTPStatusCode for the given integer
        /// or returns HTTPStatusCode.BadRequest.
        /// </summary>
        /// <param name="myCode">A HTTPStatusCode code</param>
        /// <returns>A HTTPStatusCode</returns>
        public static HTTPStatusCode ParseInt(UInt16 myCode)
        {

            var myHTTPStatusCode = (from   _FieldInfo in typeof(HTTPStatusCode).GetFields()
                                    let    _HTTPStatusCode = _FieldInfo.GetValue(null) as HTTPStatusCode
                                    where  _HTTPStatusCode != null
                                    where  _HTTPStatusCode.Code == myCode
                                    select _HTTPStatusCode).FirstOrDefault();

            if (myHTTPStatusCode != null)
                return myHTTPStatusCode;

            return HTTPStatusCode.BadRequest;

        }

        #endregion

        #region ParseString(myCode)

        /// <summary>
        /// Tries to find the appropriate HTTPStatusCode for the given string
        /// or returns HTTPStatusCode.BadRequest.
        /// </summary>
        /// <param name="myCode">A HTTPStatusCode code as string</param>
        /// <returns>A HTTPStatusCode</returns>
        public static HTTPStatusCode ParseString(String myCode)
        {

            if (UInt32.TryParse(myCode, out UInt32 _Code))
            {

                var myHTTPStatusCode = (from   _FieldInfo in typeof(HTTPStatusCode).GetFields()
                                        let    _HTTPStatusCode = _FieldInfo.GetValue(null) as HTTPStatusCode
                                        where  _HTTPStatusCode != null
                                        where  _HTTPStatusCode.Code == _Code
                                        select _HTTPStatusCode).FirstOrDefault();

                if (myHTTPStatusCode != null)
                    return myHTTPStatusCode;

            }

            return HTTPStatusCode.BadRequest;

        }

        #endregion

        #region TryParseInt(myCode, out myHTTPStatusCode)

        /// <summary>
        /// Tries to find the appropriate HTTPStatusCode for the given integer.
        /// </summary>
        /// <param name="myCode">A HTTPStatusCode code</param>
        /// <param name="myHTTPStatusCode">The parsed HTTPStatusCode</param>
        /// <returns>true or false</returns>
        public static Boolean TryParseInt(UInt16 myCode, out HTTPStatusCode myHTTPStatusCode)
        {

            myHTTPStatusCode = (from _FieldInfo in typeof(HTTPStatusCode).GetFields()
                                let _HTTPStatusCode = _FieldInfo.GetValue(null) as HTTPStatusCode
                                where _HTTPStatusCode != null
                                where _HTTPStatusCode.Code == myCode
                                select _HTTPStatusCode).FirstOrDefault();

            if (myHTTPStatusCode != null)
                return true;

            myHTTPStatusCode = HTTPStatusCode.BadRequest;
            return false;

        }

        #endregion

        #region TryParseString(myCode, out myHTTPStatusCode)

        /// <summary>
        /// Tries to find the appropriate HTTPStatusCode for the given string.
        /// </summary>
        /// <param name="myCode">A HTTPStatusCode code</param>
        /// <param name="myHTTPStatusCode">The parsed HTTPStatusCode</param>
        /// <returns>true or false</returns>
        public static Boolean TryParseString(String myCode, out HTTPStatusCode myHTTPStatusCode)
        {

            if (UInt32.TryParse(myCode, out UInt32 _Code))
            {

                myHTTPStatusCode = (from   _FieldInfo in typeof(HTTPStatusCode).GetFields()
                                    let    _HTTPStatusCode = _FieldInfo.GetValue(null) as HTTPStatusCode
                                    where  _HTTPStatusCode != null
                                    where  _HTTPStatusCode.Code == _Code
                                    select _HTTPStatusCode).FirstOrDefault();

                if (myHTTPStatusCode != null)
                    return true;

            }

            myHTTPStatusCode = HTTPStatusCode.BadRequest;
            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (myHTTPStatusCode1, myHTTPStatusCode2)

        public static Boolean operator == (HTTPStatusCode myHTTPStatusCode1, HTTPStatusCode myHTTPStatusCode2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(myHTTPStatusCode1, myHTTPStatusCode2))
                return true;

            // If one is null, but not both, return false.
            if ((myHTTPStatusCode1 is null) || (myHTTPStatusCode2 is null))
                return false;

            return myHTTPStatusCode1.Equals(myHTTPStatusCode2);

        }

        #endregion

        #region Operator != (myHTTPStatusCode1, myHTTPStatusCode2)

        public static Boolean operator != (HTTPStatusCode myHTTPStatusCode1, HTTPStatusCode myHTTPStatusCode2)
            => !(myHTTPStatusCode1 == myHTTPStatusCode2);

        #endregion

        #region Operator <  (myHTTPStatusCode1, myHTTPStatusCode2)

        public static Boolean operator < (HTTPStatusCode myHTTPStatusCode1, HTTPStatusCode myHTTPStatusCode2)
        {

            // Check if myHTTPStatusCode1 is null
            if (myHTTPStatusCode1 is null)
                throw new ArgumentNullException("Parameter myHTTPStatusCode1 must not be null!");

            // Check if myHTTPStatusCode2 is null
            if (myHTTPStatusCode2 is null)
                throw new ArgumentNullException("Parameter myHTTPStatusCode2 must not be null!");

            return myHTTPStatusCode1.CompareTo(myHTTPStatusCode2) < 0;

        }

        #endregion

        #region Operator >  (myHTTPStatusCode1, myHTTPStatusCode2)

        public static Boolean operator > (HTTPStatusCode myHTTPStatusCode1, HTTPStatusCode myHTTPStatusCode2)
        {

            // Check if myHTTPStatusCode1 is null
            if (myHTTPStatusCode1 is null)
                throw new ArgumentNullException("Parameter myHTTPStatusCode1 must not be null!");

            // Check if myHTTPStatusCode2 is null
            if (myHTTPStatusCode2 is null)
                throw new ArgumentNullException("Parameter myHTTPStatusCode2 must not be null!");

            return myHTTPStatusCode1.CompareTo(myHTTPStatusCode2) > 0;

        }

        #endregion

        #region Operator <= (myHTTPStatusCode1, myHTTPStatusCode2)

        public static Boolean operator <= (HTTPStatusCode myHTTPStatusCode1, HTTPStatusCode myHTTPStatusCode2)
            => !(myHTTPStatusCode1 > myHTTPStatusCode2);

        #endregion

        #region Operator >= (myHTTPStatusCode1, myHTTPStatusCode2)

        public static Boolean operator >= (HTTPStatusCode myHTTPStatusCode1, HTTPStatusCode myHTTPStatusCode2)
            => !(myHTTPStatusCode1 < myHTTPStatusCode2);

        #endregion

        #endregion

        #region IComparable<HTTPStatusCode> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            if (!(Object is HTTPStatusCode HTTPStatusCode))
                throw new ArgumentException("The given object is not a HTTPStatusCode!");

            return CompareTo(HTTPStatusCode);

        }

        #endregion

        #region CompareTo(HTTPStatusCode)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPStatusCode">An object to compare with.</param>
        public Int32 CompareTo(HTTPStatusCode HTTPStatusCode)
        {

            if (HTTPStatusCode is null)
                throw new ArgumentNullException("The given HTTPStatusCode must not be null!");

            return Code.CompareTo(HTTPStatusCode.Code);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPStatusCode> Members

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

            if (!(Object is HTTPStatusCode HTTPStatusCode))
                return false;

            return Equals(HTTPStatusCode);

        }

        #endregion

        #region Equals(HTTPStatusCode)

        /// <summary>
        /// Compares two HTTPStatusCodes for equality.
        /// </summary>
        /// <param name="HTTPStatusCode">A HTTPStatusCode to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPStatusCode HTTPStatusCode)
        {

            if (HTTPStatusCode is null)
                return false;

            return Code == HTTPStatusCode.Code;

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
            => Code.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => String.Format("{0} - {1}", Code, Name);

        #endregion

    }

}
