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
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IdentityModel.Tokens;

using de.ahzf.Hermod.Sockets.TCP;
using de.ahzf.Hermod.HTTP.Common;
using System.Reflection;
using System.IO;
using de.ahzf.Hermod.Tools;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// This handles incoming HTTP requests and maps them onto
    /// methods of HTTPServiceType.
    /// </summary>
    /// <typeparam name="HTTPServiceInterface">the instance</typeparam>
    public class HTTPConnection<HTTPServiceInterface> : ATCPConnection, IHTTPConnection
        where HTTPServiceInterface : IHTTPService
    {

        #region Data

        private HTTPServiceInterface _HTTPServiceInterface;

        #endregion

        #region Properties

        #region NewHTTPServiceHandler

        private de.ahzf.Hermod.HTTP.HTTPServer<HTTPServiceInterface>.NewHTTPServiceHandler _NewHTTPServiceHandler;

        public de.ahzf.Hermod.HTTP.HTTPServer<HTTPServiceInterface>.NewHTTPServiceHandler NewHTTPServiceHandler
        {

            get
            {
                return _NewHTTPServiceHandler;
            }

            set
            {
                _NewHTTPServiceHandler = value;
            }

        }

        #endregion

        /// <summary>
        /// The autodiscovered implementations of the HTTPServiceInterface.
        /// </summary>
        public IDictionary<HTTPContentType, HTTPServiceInterface> Implementations { get; set; }

        public HTTPRequestHeader  RequestHeader  { get; protected set; }

        public Byte[]             RequestBody    { get; protected set; }

        public HTTPResponseHeader ResponseHeader { get; protected set; }

        public NetworkStream      ResponseStream { get; protected set; }

        public String             ServerName     { get; set; }

        public HTTPSecurity       HTTPSecurity   { get; set; }

        public URLMapping         URLMapping     { get; set; }

        public String             ErrorReason    { get; set; }

        public Exception          LastException  { get; set; }

        #endregion


        #region Constructor(s)

        #region HTTPConnection()

        public HTTPConnection()
        { }

        #endregion

        #region HTTPConnection(myTCPClientConnection)

        /// <summary>
        /// Create a new HTTPConnection class using the given TcpClient class
        /// </summary>
        public HTTPConnection(TcpClient myTCPClientConnection)
            : base(myTCPClientConnection)
        {
            ResponseHeader = new HTTPResponseHeader();
            ResponseStream = myTCPClientConnection.GetStream();
        }

        #endregion

        #endregion

        
        #region ProcessHTTP()

        public void ProcessHTTP()
        {

            //Console.WriteLine("HTTPConnection from {0}, thread {1}", TCPClientConnection.Client.RemoteEndPoint, Thread.CurrentThread.ManagedThreadId);

            using (var _HTTPStream = TCPClientConnection.GetStream())
            {

                #region Get HTTP header and body

                var     _MemoryStream       = new MemoryStream();
                var     _Buffer             = new Byte[65535];
                Byte[]  _ByteArray          = null;
                Boolean _EndOfHTTPHeader    = false;
                long    _Length             = 0;
                long    _ReadPosition       = 6;

                try
                {

                    while (!_EndOfHTTPHeader || _HTTPStream.DataAvailable || !TCPClientConnection.Connected)
                    {

                        while (_HTTPStream.DataAvailable)
                        {
                            _MemoryStream.Write(_Buffer, 0, _HTTPStream.Read(_Buffer, 0, _Buffer.Length));
                        }

                        _Length = _MemoryStream.Length;

                        if (_Length > 4)
                        {

                            _ByteArray    = _MemoryStream.ToArray();
                            _ReadPosition = _ReadPosition - 3;
                        
                            while (_ReadPosition < _Length)
                            {

                                if (_ByteArray[_ReadPosition - 3] == 0x0d &&
                                    _ByteArray[_ReadPosition - 2] == 0x0a &&
                                    _ByteArray[_ReadPosition - 1] == 0x0d &&
                                    _ByteArray[_ReadPosition    ] == 0x0a)
                                {
                                    _EndOfHTTPHeader = true;
                                    break;
                                }

                                _ReadPosition++;

                            }

                            if (_EndOfHTTPHeader)
                                break;

                        }

                    }

                    if (_EndOfHTTPHeader == false)
                        throw new Exception("Protocol Error!");

                    // Create a new HTTPRequestHeader
                    var HeaderBytes = new Byte[_ReadPosition - 1];
                    Array.Copy(_ByteArray, 0, HeaderBytes, 0, _ReadPosition - 1);

                    RequestHeader = new HTTPRequestHeader(Encoding.UTF8.GetString(HeaderBytes));

                    // Copy only the number of bytes given within
                    // the HTTP header element 'ContentType'!
                    if (RequestHeader.ContentLength.HasValue)
                    {
                        RequestBody = new Byte[RequestHeader.ContentLength.Value];
                        Array.Copy(_ByteArray, _ReadPosition + 1, RequestBody, 0, (Int64) RequestHeader.ContentLength.Value);
                    }
                    else
                        RequestBody = new Byte[0];

                    // The parsing of the http header failed!
                    if (RequestHeader.HTTPStatusCode != HTTPStatusCode.OK)
                    {
                        SendErrorpage(RequestHeader.HTTPStatusCode,
                                    RequestHeader,
                                    RequestBody);
                    }

                    #endregion

                    var _ContentType = RequestHeader.GetBestMatchingAcceptHeader(Implementations.Keys.ToArray());
                    var _X = Implementations[_ContentType];

                    #region Invoke upper-layer protocol constructor

                    // Get constructor for HTTPServiceType
                    var _Type = _X.GetType().
                                GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                               null,
                                               new Type[] {
                                                   typeof(IHTTPConnection)
                                               },
                                               null);

                    if (_Type == null)
                        throw new ArgumentException("A appropriate constructor for type '" + typeof(HTTPServiceInterface).Name + "' could not be found!");


                    // Invoke constructor of HTTPServiceType
                    _HTTPServiceInterface = (HTTPServiceInterface) _Type.Invoke(new Object[] { this });

                    if (_HTTPServiceInterface == null)
                        throw new ArgumentException("A http connection of type '" + typeof(HTTPServiceInterface).Name + "' could not be created!");

                    if (NewHTTPServiceHandler != null)
                        NewHTTPServiceHandler(_HTTPServiceInterface);

                    #endregion

                    //ToDo: Add HTTP pipelining!


                    #region Get and check callback...

                    var _ParsedCallback = URLMapping.GetHandler(RequestHeader.Host,
                                                                RequestHeader.RawUrl,
                                                                RequestHeader.HTTPMethod, _ContentType);

                    if (_ParsedCallback == null || _ParsedCallback.Item1 == null)// || _ParsedCallback.Item1.MethodCallback == null)
                    {

                        SendErrorpage(HTTPStatusCode.InternalServerError,
                                      RequestHeader,
                                      RequestBody,
                                      ErrorReason: "Could not find a valid handler for URL: " + RequestHeader.RawUrl);

                        return;

                    }

                    #endregion

                    #region Check authentication

                    var IsAuthenticated = false;

                    //#region Check HTTPSecurity

                    //// the server switched on authentication AND the method does not explicit allow not authentication
                    //if (HTTPSecurity != null && !(parsedCallback.Item1.NeedsExplicitAuthentication.HasValue && !parsedCallback.Item1.NeedsExplicitAuthentication.Value))
                    //{

                    //    #region Authentication

                    //    if (HTTPSecurity.CredentialType == HttpClientCredentialType.Basic)
                    //    {

                    //        if (requestHeader.Authorization == null)
                    //        {

                    //            #region No authorisation info was sent

                    //            responseHeader = GetAuthenticationRequiredHeader();
                    //            responseHeaderBytes = responseHeader.ToBytes();

                    //            #endregion

                    //        }
                    //        else if (!Authorize(_HTTPWebContext.RequestHeader.Authorization))
                    //        {

                    //            #region Authorization failed

                    //            responseHeader = GetAuthenticationRequiredHeader();
                    //            responseHeaderBytes = responseHeader.ToBytes();

                    //            #endregion

                    //        }
                    //        else
                    //        {
                    //            authenticated = true;
                    //        }

                    //    }

                    //    else
                    //    {
                    //        responseBodyBytes = Encoding.UTF8.GetBytes("Authentication other than Basic currently not supported");
                    //        responseHeader = new HTTPResponseHeader() { HttpStatusCode = HTTPStatusCode.InternalServerError, ContentLength = responseBodyBytes.ULongLength() };
                    //        responseHeaderBytes = responseHeader.ToBytes();

                    //        Debug.WriteLine("!!!Authentication other than Basic currently not supported!!!");
                    //    }

                    //    #endregion

                    //}

                    //else if (parsedCallback.Item1.NeedsExplicitAuthentication.HasValue && parsedCallback.Item1.NeedsExplicitAuthentication.Value)
                    //{

                    //    #region The server does not have authentication but the Interface explicitly needs authentication

                    //    responseBodyBytes = Encoding.UTF8.GetBytes("Authentication not supported for this server!");
                    //    responseHeader = new HTTPResponseHeader() { HttpStatusCode = HTTPStatusCode.InternalServerError, ContentLength = responseBodyBytes.ULongLength() };
                    //    responseHeaderBytes = responseHeader.ToBytes();

                    //    #endregion

                    //    Debug.WriteLine("!!!Authentication not supported for this server!!!");

                    //}

                    //else
                    //    authenticated = true;

                    //#endregion

                    // HACK: authenticated = true!!!!!!!!!!!!!!
                    IsAuthenticated = true;

                    #endregion

                    #region Invoke callback within the upper-layer protocol

                    if (IsAuthenticated)
                    {

                        try
                        {

                            var _HTTPResponse = _ParsedCallback.Item1.Invoke(_HTTPServiceInterface, _ParsedCallback.Item2.ToArray()) as HTTPResponse;

                            if (_HTTPResponse == null || _HTTPResponse.ResponseHeader == null)
                            {

                                SendErrorpage(HTTPStatusCode.InternalServerError,
                                              RequestHeader,
                                              RequestBody,
                                              ErrorReason: "Could not invoke method for URL: " + RequestHeader.RawUrl);

                                return;

                            }

                            // If the ServerName had not been set, set it now!
                            if (_HTTPResponse.ResponseHeader.Server == null)
                                _HTTPResponse.ResponseHeader.Server = ServerName;

                            ResponseHeader = _HTTPResponse.ResponseHeader;

                            // If there is no client and server error...
                            if (!_HTTPResponse.ResponseHeader.HttpStatusCode.IsClientError &&
                                !_HTTPResponse.ResponseHeader.HttpStatusCode.IsServerError)
                                 WriteToResponseStream(_HTTPResponse);


                        }

                        catch (Exception e)
                        {

                            WriteToResponseStream(new HTTPResponse(
                    
                                new HTTPResponseHeader()
                                {
                                    HttpStatusCode = HTTPStatusCode.InternalServerError,
                                    CacheControl   = "no-cache",
                                    ContentType    = HTTPContentType.TEXT_UTF8
                                },

                                e.ToString().ToUTF8Bytes()

                            ));

                        }

                    }

                    #endregion

                }

                catch (Exception _Exception)
                {
                    LastException = _Exception;
                    ResponseHeader.HttpStatusCode = HTTPStatusCode.InternalServerError;
                    //ExceptionThrown(this, _Exception);
                }

                #region In case of errors => Send an errorpage!

                if (ResponseHeader.HttpStatusCode.IsClientError ||
                    ResponseHeader.HttpStatusCode.IsServerError)
                {

                    SendErrorpage(ResponseHeader.HttpStatusCode,
                                  RequestHeader,
                                  RequestBody,
                                  LastException: LastException);

                    return;

                }

                #endregion

            }

            return;

        }

        #endregion


        #region (private) WriteToResponseStream(myHTTPResponseHeader, myContent = null)

        private void WriteToResponseStream(HTTPResponseHeader myHTTPResponseHeader, Byte[] myContent = null)
        {

            if (myContent != null)
                myHTTPResponseHeader.ContentLength = (UInt64)myContent.LongLength;

            if (myHTTPResponseHeader != null)
                WriteToResponseStream(Encoding.UTF8.GetBytes(myHTTPResponseHeader.GetResponseHeader));

            if (myContent != null)
                WriteToResponseStream(myContent);

        }

        #endregion

        #region (private) WriteToResponseStream(myHTTPResponse, myReadTimeout = 1000)

        private void WriteToResponseStream(HTTPResponse myHTTPResponse, Int32 myReadTimeout = 1000)
        {

            if (myHTTPResponse.Content != null)
                ResponseHeader.ContentLength = (UInt64) myHTTPResponse.Content.LongLength;
            else if (myHTTPResponse.ContentStream != null)
                ResponseHeader.ContentLength = (UInt64) myHTTPResponse.ContentStream.Length;
            // else ContentLength will still be 0!

            WriteToResponseStream(Encoding.UTF8.GetBytes(ResponseHeader.GetResponseHeader));

            if (myHTTPResponse.Content != null)
                WriteToResponseStream(myHTTPResponse.Content);
            else if (myHTTPResponse.ContentStream != null)
                WriteToResponseStream(myHTTPResponse.ContentStream, myReadTimeout);

            if (myHTTPResponse.ResponseHeader.HttpStatusCode.IsClientError ||
                myHTTPResponse.ResponseHeader.HttpStatusCode.IsServerError)
                Console.WriteLine("HTTPStatusCode: " + myHTTPResponse.ResponseHeader.HttpStatusCode);

        }

        #endregion


        #region (private) GetAuthenticationRequiredHeader()

        private HTTPResponseHeader GetAuthenticationRequiredHeader()
        {
            return new HTTPResponseHeader()
            {
                HttpStatusCode = HTTPStatusCode.Unauthorized
            };
        }

        #endregion

        #region (private) Authorize(myHTTPCredentials)

        private Boolean Authorize(HTTPBasicAuthentication myHTTPCredentials)
        {

            try
            {
                HTTPSecurity.UserNamePasswordValidator.Validate(myHTTPCredentials.Username, myHTTPCredentials.Password);
                return true;
            }

            catch (SecurityTokenException ste)
            {
                Debug.WriteLine("Authorize failed with " + ste.ToString());
                return false;
            }

        }

        #endregion


        #region SendErrorpage(myHttpStatusCode, myRequestHeader, myRequestBody, ErrorReason = null, LastException = null)

        public void SendErrorpage(HTTPStatusCode myHttpStatusCode, HTTPRequestHeader myRequestHeader, Byte[] myRequestBody, String ErrorReason = null, Exception LastException = null)
        {

            if (ErrorReason != null && ErrorReason == null)
                this.ErrorReason = ErrorReason;

            if (LastException != null && LastException == null)
                this.LastException = LastException;

            ResponseHeader.HttpStatusCode = myHttpStatusCode;
            ResponseHeader.Server         = ServerName;

            #region Send a customized errorpage...

            var __ErrorHandler = URLMapping.GetErrorHandler("*",
                                                            RequestHeader.RawUrl,
                                                            RequestHeader.HTTPMethod,
                                                            null,
                                                            ResponseHeader.HttpStatusCode);

            if (__ErrorHandler != null && __ErrorHandler.Item1 != null)
            {

                var _ParamArray    = __ErrorHandler.Item2.ToArray();
                var _Parameters    = new Object[_ParamArray.Count() + 2];
                    _Parameters[0] = ErrorReason;
                    _Parameters[1] = LastException;
                Array.Copy(_ParamArray, 0, _Parameters, 2, _ParamArray.Count());

                var _Response      = __ErrorHandler.Item1.Invoke(_HTTPServiceInterface, _Parameters);
                var _HTTPResponse  = _Response as HTTPResponse;

                if (_Response == null || _HTTPResponse == null)
                {

                    SendErrorpage(HTTPStatusCode.InternalServerError,
                                  RequestHeader,
                                  RequestBody,
                                  ErrorReason: "Could not invoke errorpage for URL: " + RequestHeader.RawUrl);

                }

                WriteToResponseStream(_HTTPResponse);

            }

            #endregion

            #region ...or send a general XHTML errorpage

            else
            {

                #region Generate XHTML errorpage

                var _StringBuilder = new StringBuilder();

                _StringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" ?>");
                _StringBuilder.AppendLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">");
                _StringBuilder.AppendLine("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
                _StringBuilder.AppendLine("  <head>");
                _StringBuilder.Append    ("    <title>Error ").Append(myHttpStatusCode).AppendLine("</title>");
                _StringBuilder.AppendLine("  </head>");
                _StringBuilder.AppendLine("  <body>");

                _StringBuilder.Append    ("    <h1>Error ").Append(myHttpStatusCode).AppendLine("</h1>");
                _StringBuilder.AppendLine("    <p>");
                _StringBuilder.AppendLine("      The client reqest from '" + RemoteSocket + "' led to an error!<br /><br />");
                _StringBuilder.AppendLine("    </p>");

                _StringBuilder.AppendLine("    <h3>Raw request header:</h3>");
                _StringBuilder.AppendLine("    <pre>");
                _StringBuilder.AppendLine(RequestHeader.RAWHTTPHeader);
                _StringBuilder.AppendLine("    </pre>");

                if (LastException != null)
                {

                    _StringBuilder.AppendLine("    <h3>Exception:</h3>");
                    _StringBuilder.AppendLine("    <pre>");
                    _StringBuilder.AppendLine("Type:       " + LastException.GetType().FullName);
                    _StringBuilder.AppendLine("Source:     " + LastException.Source);
                    _StringBuilder.AppendLine("Message:    " + LastException.Message);

                    if (LastException.StackTrace != null)
                        _StringBuilder.AppendLine("StackTrace: " + LastException.StackTrace.ToString());

                    _StringBuilder.AppendLine("    </pre>");

                }

                if (ErrorReason != null)
                {
                    _StringBuilder.AppendLine("    <h3>Reason:</h3>");
                    _StringBuilder.AppendLine("    <pre>");
                    _StringBuilder.AppendLine(ErrorReason);
                    _StringBuilder.AppendLine("    </pre>");
                }

                _StringBuilder.AppendLine("  </body>");
                _StringBuilder.AppendLine("</html>");
                _StringBuilder.AppendLine();

                var _XHTMLErrorpage = Encoding.UTF8.GetBytes(_StringBuilder.ToString());

                #endregion

                ResponseHeader.ContentLength = (UInt64) _XHTMLErrorpage.LongCount();
                ResponseHeader.ContentType   = HTTPContentType.HTML_UTF8;

                WriteToResponseStream(
                    ResponseHeader,
                    _XHTMLErrorpage
                );

            }

            #endregion

        }

        #endregion


        #region Dispose

        public override void Dispose()
        {
            
            var _IDisposable = _HTTPServiceInterface as IDisposable;
            
            if (_IDisposable != null)
                _IDisposable.Dispose();

        }

        #endregion

    }


    #region HTTPConnection => HTTPConnection<DefaultHTTPService>

    public class HTTPConnection : HTTPConnection<DefaultHTTPService>
    {

        #region Constructor(s)

        #region HTTPConnection()

        public HTTPConnection()
        { }

        #endregion

        #region HTTPConnection(myTCPClientConnection)

        /// <summary>
        /// Create a new HTTPConnection class using the given TcpClient class
        /// </summary>
        public HTTPConnection(TcpClient myTCPClientConnection)
            : base(myTCPClientConnection)
        { }

        #endregion

        #endregion

    }

    #endregion

}
