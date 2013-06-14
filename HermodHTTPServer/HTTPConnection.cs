/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Reflection;
using System.Diagnostics;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IdentityModel.Tokens;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    internal class StreamHelper
    {

        public NetworkStream  NetworkStream  { get; private set; }
        public MemoryStream   MemoryStream   { get; private set; }
        public Byte[]         Buffer         { get; private set; }
        public AutoResetEvent DataAvailable  { get; private set; }
        public Int32 ReadPosition { get; set; }

        public StreamHelper(NetworkStream NewStream, Int32 BufferSize)
        {
            NetworkStream = NewStream;
            MemoryStream  = new MemoryStream();
            Buffer        = new Byte[BufferSize];
            DataAvailable = new AutoResetEvent(false);
        }

    }

    /// <summary>
    /// This handles incoming HTTP requests and maps them onto
    /// methods of HTTPServiceType.
    /// </summary>
    /// <typeparam name="HTTPServiceInterface">the instance</typeparam>
    public class HTTPConnection<HTTPServiceInterface> : ATCPConnection, IHTTPConnection
        where HTTPServiceInterface : class, IHTTPService
    {

        #region Data

        private HTTPServiceInterface _HTTPServiceInterface;

        #endregion

        #region Properties

        #region NewHTTPServiceHandler

        private eu.Vanaheimr.Hermod.HTTP.HTTPServer<HTTPServiceInterface>.NewHTTPServiceHandler _NewHTTPServiceHandler;

        public eu.Vanaheimr.Hermod.HTTP.HTTPServer<HTTPServiceInterface>.NewHTTPServiceHandler NewHTTPServiceHandler
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

        public HTTPRequest    RequestHeader   { get; protected set; }

        public Byte[]         RequestBody     { get; protected set; }

        public HTTPResponse   ResponseHeader  { get; protected set; }

        public NetworkStream  ResponseStream  { get; protected set; }

        public String         ServerName      { get; set; }

        public HTTPSecurity   HTTPSecurity    { get; set; }

        public URLMapping     URLMapping      { get; set; }

        public String         ErrorReason     { get; set; }

        public Exception      LastException   { get; set; }

        public IHTTPServer    HTTPServer      { get; set; }

        #endregion

        #region Constructor(s)

        #region HTTPConnection()

        public HTTPConnection()
        { }

        #endregion

        #region HTTPConnection(TCPClientConnection)

        /// <summary>
        /// Create a new HTTPConnection class using the given TcpClient class
        /// </summary>
        public HTTPConnection(TcpClient TCPClientConnection)
            : base(TCPClientConnection)
        {
            ResponseHeader = null;
            ResponseStream = TCPClientConnection.GetStream();
        }

        #endregion

        #endregion


        #region ProcessHTTP_new()

        public void ProcessHTTP_new()
        {

            using (var _HTTPStream = Stream)
            {

                var helper = new StreamHelper(_HTTPStream, 65535);

                Debug.WriteLine("New connection " + RemoteHost + ":" + RemotePort + " @ " + Thread.CurrentThread.ManagedThreadId);

                helper.NetworkStream.BeginRead(helper.Buffer, 0, helper.Buffer.Length, StreamReadCallback, helper);

                //Int32 ReadPosition = 0;

                while (IsConnected)
                {
                    Thread.Sleep(1);
                    //helper.DataAvailable.WaitOne();
                };

                Debug.WriteLine("Closing connection " + RemoteHost + ":" + RemotePort + " @ " + Thread.CurrentThread.ManagedThreadId);
                Close();

            }

        }

        #endregion

        #region (private) StreamReadCallback(...)

        private void StreamReadCallback(IAsyncResult ar)
        {

            var helper = (StreamHelper) ar.AsyncState;
            int bytesRead;

            try
            {

                bytesRead = helper.NetworkStream.EndRead(ar);

                //helper.MemoryStream.Position = helper.MemoryStream.Length;
                //helper.MemoryStream.Write(helper.Buffer, 0, bytesRead);
                Debug.WriteLine("Read " + bytesRead + " bytes @ " + Thread.CurrentThread.ManagedThreadId);
                //helper.DataAvailable.Set();

                //helper.MemoryStream.Position = helper.ReadPosition;


                //if (bytesRead > 4)
                //{

                //    var ReadPos = 0;

                //    do
                //    {

                //        if (helper.Buffer[ReadPos] == 0x0d)
                //        {
                //            if (helper.Buffer[ReadPos+1] == 0x0a)
                //            {
                //                if (helper.Buffer[ReadPos+2] == 0x0d)
                //                {
                //                    if (helper.Buffer[ReadPos+3] == 0x0a)
                //                    {
                //                        var Command = new Byte[helper.ReadPosition - 1];
                //                        helper.MemoryStream.Read(Command, 0, helper.ReadPosition - 1);
                                        
                //                    }
                //                }
                //            }
                //        }

                //        helper.ReadPosition++;

                //    }
                //    while (helper.ReadPosition < helper.MemoryStream.Length - 4);
                    

            }
            catch
            {
                //An error has occured when reading
                Debug.WriteLine("An error has occured when reading @ " + Thread.CurrentThread.ManagedThreadId);
                return;
            }

            // The connection has been closed.
            if (bytesRead == 0)
            {
                Debug.WriteLine("Zero bytes when reading @ " + Thread.CurrentThread.ManagedThreadId);
                return;
            }

            // Restart reading from the network stream
            helper.NetworkStream.BeginRead(helper.Buffer, 0, helper.Buffer.Length, StreamReadCallback, helper);
            
        }

        #endregion

        //private void ProcessHTTP1()
        //{

        //        #region Get HTTP header and body

        //        var     _MemoryStream       = new MemoryStream();
        //        var     _Buffer             = new Byte[65535];
        //        Byte[]  _ByteArray          = null;
        //        Boolean _EndOfHTTPHeader    = false;
        //        long    _Length             = 0;
        //        long    _ReadPosition       = 6;

        //        try
        //        {

        //            // Create a new HTTPRequestHeader
        //            var HeaderBytes = new Byte[_ReadPosition - 1];
        //            Array.Copy(_ByteArray, 0, HeaderBytes, 0, _ReadPosition - 1);

        //            InHTTPRequest = new HTTPRequest(HeaderBytes.ToUTF8String());

        //            // The parsing of the http header failed!
        //            if (InHTTPRequest.HTTPStatusCode != HTTPStatusCode.OK)
        //            {
        //                SendErrorpage(InHTTPRequest.HTTPStatusCode, InHTTPRequest);
        //                return;
        //            }

        //            // Copy only the number of bytes given within
        //            // the HTTP header element 'ContentType'!
        //            if (InHTTPRequest.ContentLength.HasValue)
        //            {
        //                RequestBody = new Byte[InHTTPRequest.ContentLength.Value];
        //                Array.Copy(_ByteArray, _ReadPosition + 1, RequestBody, 0, (Int64) InHTTPRequest.ContentLength.Value);
        //            }
        //            else
        //                RequestBody = new Byte[0];

        //            #endregion

        //            var BestContentType = InHTTPRequest.Accept.BestMatchingContentType(Implementations.Keys.ToArray());
        //            var BestImpl        = Implementations[BestContentType];

        //            #region Invoke upper-layer protocol constructor

        //            // Get constructor for HTTPServiceType
        //            var _Type = BestImpl.GetType().
        //                        GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        //                                       null,
        //                                       new Type[] {
        //                                           typeof(IHTTPConnection)
        //                                       },
        //                                       null);

        //            if (_Type == null)
        //                throw new ArgumentException("A appropriate constructor for type '" + typeof(HTTPServiceInterface).Name + "' could not be found!");


        //            // Invoke constructor of HTTPServiceType
        //            _HTTPServiceInterface = _Type.Invoke(new Object[] { this }) as HTTPServiceInterface;

        //            if (_HTTPServiceInterface == null)
        //                throw new ArgumentException("A http connection of type '" + typeof(HTTPServiceInterface).Name + "' could not be created!");

        //            if (NewHTTPServiceHandler != null)
        //                NewHTTPServiceHandler(_HTTPServiceInterface);

        //            #endregion

        //            //ToDo: Add HTTP pipelining!

        //            #region Get and check callback...

        //            var _ParsedCallback = URLMapping.GetHandler(InHTTPRequest.Host,
        //                                                        InHTTPRequest.UrlPath,
        //                                                        InHTTPRequest.HTTPMethod,
        //                                                        BestContentType);

        //            if (_ParsedCallback == null || _ParsedCallback.Item1 == null)// || _ParsedCallback.Item1.MethodCallback == null)
        //            {

        //                SendErrorpage(HTTPStatusCode.InternalServerError,
        //                              InHTTPRequest,
        //                              ErrorReason: "Could not find a valid handler for URL: " + InHTTPRequest.UrlPath);

        //                return;

        //            }

        //            #endregion

        //            #region Check authentication

        //            var IsAuthenticated = false;

        //            #region Check HTTPSecurity

        //            var _AuthenticationAttribute      = _ParsedCallback.Item1.GetCustomAttributes(typeof(AuthenticationAttribute),      false);
        //            var _ForceAuthenticationAttribute = _ParsedCallback.Item1.GetCustomAttributes(typeof(ForceAuthenticationAttribute), false);

        //            // the server switched on authentication AND the method does not explicit allow not authentication
        //            //if (HTTPSecurity != null && !(_ParsedCallback.Item1.GetCustomAttributes(NeedsExplicitAuthenticationAttribute) .NeedsExplicitAuthentication.HasValue && !_ParsedCallback.Item1.NeedsExplicitAuthentication.Value))
        //            //{

        //            //    #region Authentication

        //            //    if (HTTPSecurity.CredentialType == HttpClientCredentialType.Basic)
        //            //    {

        //            //        if (requestHeader.Authorization == null)
        //            //        {

        //            //            #region No authorisation info was sent

        //            //            responseHeader = GetAuthenticationRequiredHeader();
        //            //            responseHeaderBytes = responseHeader.ToBytes();

        //            //            #endregion

        //            //        }
        //            //        else if (!Authorize(_HTTPWebContext.RequestHeader.Authorization))
        //            //        {

        //            //            #region Authorization failed

        //            //            responseHeader = GetAuthenticationRequiredHeader();
        //            //            responseHeaderBytes = responseHeader.ToBytes();

        //            //            #endregion

        //            //        }
        //            //        else
        //            //        {
        //            //            authenticated = true;
        //            //        }

        //            //    }

        //            //    else
        //            //    {
        //            //        responseBodyBytes = Encoding.UTF8.GetBytes("Authentication other than Basic currently not supported");
        //            //        responseHeader = new HTTPResponseBuilderHeader() { HttpStatusCode = HTTPStatusCode.InternalServerError, ContentLength = responseBodyBytes.ULongLength() };
        //            //        responseHeaderBytes = responseHeader.ToBytes();

        //            //        Debug.WriteLine("!!!Authentication other than Basic currently not supported!!!");
        //            //    }

        //            //    #endregion

        //            //}

        //            //else if (parsedCallback.Item1.NeedsExplicitAuthentication.HasValue && parsedCallback.Item1.NeedsExplicitAuthentication.Value)
        //            //{

        //            //    #region The server does not have authentication but the Interface explicitly needs authentication

        //            //    responseBodyBytes = Encoding.UTF8.GetBytes("Authentication not supported for this server!");
        //            //    responseHeader = new HTTPResponseBuilderHeader() { HttpStatusCode = HTTPStatusCode.InternalServerError, ContentLength = responseBodyBytes.ULongLength() };
        //            //    responseHeaderBytes = responseHeader.ToBytes();

        //            //    #endregion

        //            //    Debug.WriteLine("!!!Authentication not supported for this server!!!");

        //            //}

        //            //else
        //            //    authenticated = true;

        //            #endregion

        //            //HACK: authenticated = true!!!!!!!!!!!!!!
        //            IsAuthenticated = true;

        //            #endregion

        //            #region Invoke callback within the upper-layer protocol

        //            if (IsAuthenticated)
        //            {

        //                try
        //                {

        //                    var _HTTPResponse = _ParsedCallback.Item1.Invoke(_HTTPServiceInterface, _ParsedCallback.Item2.ToArray()) as HTTPResponse;
        //                    if (_HTTPResponse == null)
        //                    {

        //                        SendErrorpage(HTTPStatusCode.InternalServerError,
        //                                      InHTTPRequest,
        //                                      ErrorReason: "Could not invoke method for URL: " + InHTTPRequest.UrlPath);

        //                        return;

        //                    }

        //                    ResponseHeader = _HTTPResponse;

        //                    #region In case of errors => send errorpage

        //                    if (ResponseHeader.HTTPStatusCode.IsClientError ||
        //                        ResponseHeader.HTTPStatusCode.IsServerError)
        //                    {

        //                        SendErrorpage(ResponseHeader.HTTPStatusCode,
        //                                      InHTTPRequest,
        //                                      LastException: LastException);

        //                        return;

        //                    }

        //                    #endregion

        //                    else
        //                        WriteToResponseStream(_HTTPResponse);

        //                }

        //                catch (Exception e)
        //                {

        //                    WriteToResponseStream(
                    
        //                        new HTTPResponseBuilder()
        //                        {
        //                            HTTPStatusCode = HTTPStatusCode.InternalServerError,
        //                            CacheControl   = "no-cache",
        //                            ContentType    = HTTPContentType.TEXT_UTF8,
        //                            Content        = e.ToString().ToUTF8Bytes()
        //                        });

        //                }

        //            }

        //            #endregion

        //        }

        //        catch (SocketException Exception)
        //        {
        //            Debug.WriteLine("The remote host has disconnected: " + Exception);
        //        }

        //        catch (Exception Exception)
        //        {

        //            Debug.WriteLine("General ProcessHTTP() exception: " + Exception);

        //            SendErrorpage(ResponseHeader.HTTPStatusCode,
        //                          InHTTPRequest,
        //                          LastException: Exception);

        //        }

        //}

        #region ProcessHTTP()

        public void ProcessHTTP()
        {

            //Console.WriteLine("HTTPConnection from {0}, thread {1}", TCPClientConnection.Client.RemoteEndPoint, Thread.CurrentThread.ManagedThreadId);

            using (var _HTTPStream = Stream)
            {

                var     _MemoryStream       = new MemoryStream();
                var     _Buffer             = new Byte[65535];
                Byte[]  _ByteArray          = null;
                Boolean _EndOfHTTPHeader    = false;
                long    _Length             = 0;
                long    _ReadPosition       = 6;

                try
                {

                    //ToDo: Improve reading from very slow and strange HTTP clients

                    #region Read from networkstream

                    Int32 _DataRead;
                    UInt32 _Retries = 0;

                    while (!_EndOfHTTPHeader || _HTTPStream.DataAvailable || !IsConnected)
                    {

                        while (_HTTPStream.DataAvailable)
                        {
                            _DataRead = _HTTPStream.Read(_Buffer, 0, _Buffer.Length);
                            Debug.WriteLine("Thread[" + Thread.CurrentThread.ManagedThreadId + "]: Number of bytes read from network stream: " + _DataRead);
                            _MemoryStream.Write(_Buffer, 0, _DataRead);
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

                        else
                        {
                            //Debug.WriteLine("Thread[" + Thread.CurrentThread.ManagedThreadId + "]: end of network stream!");
                            Debug.WriteLine("Thread[" + Thread.CurrentThread.ManagedThreadId + " from: " + RemoteHost + ":" + RemotePort + "]: length of stream so far: " + _MemoryStream.Length + " @ " + _EndOfHTTPHeader + ", " + _HTTPStream.DataAvailable + ", " + IsConnected);
                        }

                        Thread.Sleep(100);
                        _Retries++;

                        if (_Retries > 20)
                        {
                            Debug.WriteLine("Thread[" + Thread.CurrentThread.ManagedThreadId + " from: " + RemoteHost + ":" + RemotePort + "]: Closing connection!");
                            Close();
                            break;
                        }

                    }

                    if (!_EndOfHTTPHeader)
                    {

                        if (IsConnected)
                            SendErrorpage(HTTPStatusCode.InternalServerError, Error: "Could not find the end of a valid HTTP header!");

                        return;

                    }

                    #endregion

                    //Debug.WriteLine("Thread[" + Thread.CurrentThread.ManagedThreadId + "]: Valid HTTP header found!");

                    #region Create a new HTTP request header

                    var HeaderBytes = new Byte[_ReadPosition - 1];
                    Array.Copy(_ByteArray, 0, HeaderBytes, 0, _ReadPosition - 1);

                    RequestHeader = new HTTPRequest(RemoteHost, RemotePort, HeaderBytes.ToUTF8String());

                    // The parsing of the http header failed!
                    if (RequestHeader.HTTPStatusCode != HTTPStatusCode.OK)
                    {
                        SendErrorpage(RequestHeader.HTTPStatusCode);
                        return;
                    }

                    #endregion

                    #region Get HTTP request body

                    // Copy only the number of bytes given within
                    // the HTTP header element 'ContentType'!
                    if (RequestHeader.ContentLength.HasValue)
                    {

                        if (_ByteArray.Length < _ReadPosition + 1 + (Int64) RequestHeader.ContentLength.Value)
                            throw new Exception("Client sent less data than expected within the content length header field!");

                        RequestBody = new Byte[RequestHeader.ContentLength.Value];
                        Array.Copy(_ByteArray, _ReadPosition + 1, RequestBody, 0, (Int64) RequestHeader.ContentLength.Value);

                    }
                    else
                        RequestBody = new Byte[0];

                    #endregion

                    //ToDo: Fix best-machting HTTP service implementation selection (based on the request content-type or accept-types)!

                    #region Get best-matching HTTP service implementation (based on the request content-type or accept-types)

                    RequestHeader.BestMatchingAcceptType = RequestHeader.Accept.BestMatchingContentType(Implementations.Keys.ToArray());

                    HTTPServiceInterface BestMatchingHTTPServiceImplementation;

                    if (RequestHeader.ContentType == HTTPContentType.XWWWFormUrlEncoded && RequestHeader.BestMatchingAcceptType == HTTPContentType.EVENTSTREAM)
                        BestMatchingHTTPServiceImplementation = Implementations[HTTPContentType.EVENTSTREAM];

                    else
                    {

                        if (RequestHeader.ContentType != null)
                            BestMatchingHTTPServiceImplementation = Implementations[RequestHeader.ContentType];

                        else
                            BestMatchingHTTPServiceImplementation = Implementations[RequestHeader.BestMatchingAcceptType];

                    }

                    #endregion

                    #region Get constructor for best-matching HTTP service implementation

                    var BestMatchingHTTPServiceConstructor = BestMatchingHTTPServiceImplementation.
                                                             GetType().
                                                             GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                                            null,
                                                                            new Type[] {
                                                                                typeof(IHTTPConnection)
                                                                            },
                                                                            null);

                    if (BestMatchingHTTPServiceConstructor == null)
                        throw new ArgumentException("A appropriate constructor for type '" + typeof(HTTPServiceInterface).Name + "' could not be found!");

                    #endregion

                    #region Invoke best-matching HTTP service implementation constructor

                    _HTTPServiceInterface = BestMatchingHTTPServiceConstructor.Invoke(new Object[] { this }) as HTTPServiceInterface;

                    if (_HTTPServiceInterface == null)
                        throw new ArgumentException("A http connection of type '" + typeof(HTTPServiceInterface).Name + "' could not be created!");

                    if (NewHTTPServiceHandler != null)
                        NewHTTPServiceHandler(_HTTPServiceInterface);

                    #endregion

                    //ToDo: Add HTTP pipelining!

                    #region Get and check callback...

                    var _ParsedCallbackWithParameters = URLMapping.GetHandler(RequestHeader.Host,
                                                                              RequestHeader.UrlPath,
                                                                              RequestHeader.HTTPMethod,
                                                                              RequestHeader.BestMatchingAcceptType);

                    if (_ParsedCallbackWithParameters == null || _ParsedCallbackWithParameters.Item1 == null)
                    {
                        SendErrorpage(HTTPStatusCode.InternalServerError, "Could not find a valid handler for URL: " + RequestHeader.UrlPath);
                        return;
                    }

                    #endregion

                    #region Check authentication

                    var _AuthenticationAttributes      = _ParsedCallbackWithParameters.Item1.GetCustomAttributes(typeof(AuthenticationAttribute),      false);
                    var _ForceAuthenticationAttributes = _ParsedCallbackWithParameters.Item1.GetCustomAttributes(typeof(ForceAuthenticationAttribute), false);

                    //if (_AuthenticationAttributes.Any())
                    //{

                    //    var _AuthenticationAttribute = _AuthenticationAttributes[0] as AuthenticationAttribute;

                    //    if (_AuthenticationAttribute.AuthenticationType == HTTPAuthenticationTypes.Basic)
                    //    {
                    //    }

                    //}

                    if (_ForceAuthenticationAttributes.Any())
                    {

                        var _ForceAuthenticationAttribute = _ForceAuthenticationAttributes[0] as ForceAuthenticationAttribute;

                        if (_ForceAuthenticationAttribute.AuthenticationType == HTTPAuthenticationTypes.Basic)
                        {

                            var CurrentAuthentication = RequestHeader.Authorization;

                            if (CurrentAuthentication == null)
                            {
                                SendAuthenticationRequiredHeader(HTTPAuthenticationTypes.Basic, _ForceAuthenticationAttribute.Realm);
                                return;
                            }

                            else if (HTTPSecurity != null &&
                                     HTTPSecurity.Verify(CurrentAuthentication.Username, CurrentAuthentication.Password))
                            { }

                            else
                            {
                                SendAuthenticationRequiredHeader(HTTPAuthenticationTypes.Basic, _ForceAuthenticationAttribute.Realm);
                                return;
                            }

                        }

                        else
                        {
                            SendErrorpage(HTTPStatusCode.NotImplemented, "Please use HTTP basic authentication!");
                            return;
                        }


                    }

                    #endregion

                    #region Invoke callback within the upper-layer protocol

                    ResponseHeader = _ParsedCallbackWithParameters.Item1.Invoke(_HTTPServiceInterface, _ParsedCallbackWithParameters.Item2.ToArray()) as HTTPResponse;

                    if (ResponseHeader == null)
                    {
                        SendErrorpage(HTTPStatusCode.InternalServerError, "Could not invoke method for URL: " + RequestHeader.UrlPath);
                        return;
                    }

                    #endregion

                    #region Call logging delegate

                    HTTPServer.LogAccess(DateTime.Now, RequestHeader, ResponseHeader);

                    #endregion

                    #region In case of errors => Send errorpage

                    if ((ResponseHeader.HTTPStatusCode.IsClientError ||
                         ResponseHeader.HTTPStatusCode.IsServerError) &&
                         ResponseHeader == null)
                    {
                        SendErrorpage(ResponseHeader.HTTPStatusCode, LastException: LastException);
                        return;
                    }

                    #endregion

                    WriteToResponseStream(ResponseHeader);

                }

                catch (SocketException SocketException)
                {
                    HTTPServer.LogError(DateTime.Now, RequestHeader, ResponseHeader, LastException: SocketException);
                }

                catch (Exception Exception)
                {
                    SendErrorpage(HTTPStatusCode.InternalServerError, LastException: Exception);
                }

            }

        }

        #endregion


        #region (private) WriteToResponseStream(myHTTPResponseBuilderHeader, ReadTimeout = 1000)

        private void WriteToResponseStream(HTTPResponse HTTPResponse, Int32 ReadTimeout = 1000)
        {

            WriteToResponseStream(HTTPResponse.RawHTTPHeader.ToUTF8Bytes());

            if (HTTPResponse.Content != null)
                WriteToResponseStream(HTTPResponse.Content);

            else if (HTTPResponse.ContentStream != null)
                WriteToResponseStream(HTTPResponse.ContentStream, ReadTimeout);

        }

        #endregion

        #region (private) SendAuthenticationRequiredHeader(HTTPAuthenticationType, Realm)

        private void SendAuthenticationRequiredHeader(HTTPAuthenticationTypes HTTPAuthenticationType, String Realm)
        {
            WriteToResponseStream(
                new HTTPResponseBuilder() {
                    HTTPStatusCode  = HTTPStatusCode.Unauthorized,
                    WWWAuthenticate = HTTPAuthenticationType.ToString() + " realm=\"" + Realm + "\""
                }
            );
        }

        #endregion


        #region SendErrorpage(HTTPStatusCode, Error = null, LastException = null)

        /// <summary>
        /// Send a HTTP error page.
        /// </summary>
        /// <param name="HTTPStatusCode">The HTTP status code.</param>
        /// <param name="HTTPRequest">The incoming HTTP request.</param>
        /// <param name="Error">A reason for this error.</param>
        /// <param name="LastException">The exception causing this error.</param>
        public void SendErrorpage(HTTPStatusCode HTTPStatusCode, String Error = null, Exception LastException = null)
        {

            #region Initial checks

            if (Error != null && this.ErrorReason == null)
                this.ErrorReason = Error;

            if (LastException != null && this.LastException == null)
                this.LastException = LastException;

            #endregion

            HTTPServer.LogError(DateTime.Now, RequestHeader, ResponseHeader, Error, LastException);

            #region Send a customized errorpage...

            var __ErrorHandler = URLMapping.GetErrorHandler("*",
                                                            RequestHeader.UrlPath,
                                                            RequestHeader.HTTPMethod,
                                                            null,
                                                            HTTPStatusCode);

            if (__ErrorHandler != null && __ErrorHandler.Item1 != null)
            {

                var _ParamArray    = __ErrorHandler.Item2.ToArray();
                var _Parameters    = new Object[_ParamArray.Count() + 2];
                    _Parameters[0] = Error;
                    _Parameters[1] = LastException;
                Array.Copy(_ParamArray, 0, _Parameters, 2, _ParamArray.Count());

                var _Response            = __ErrorHandler.Item1.Invoke(_HTTPServiceInterface, _Parameters);
                var _HTTPResponseBuilder = _Response as HTTPResponse;

                if (_Response == null || _HTTPResponseBuilder == null)
                {
                    SendErrorpage(HTTPStatusCode.InternalServerError, "Could not invoke errorpage for URL: " + RequestHeader.UrlPath);
                    return;
                }

                WriteToResponseStream(_HTTPResponseBuilder);

            }

            #endregion

            #region ...or send a general HTML errorpage

            else
            {

                #region Generate HTML errorpage

                var _StringBuilder = new StringBuilder();

                _StringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" ?>");
                _StringBuilder.AppendLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">");
                _StringBuilder.AppendLine("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
                _StringBuilder.AppendLine("  <head>");
                _StringBuilder.Append    ("    <title>Error ").Append(HTTPStatusCode).AppendLine("</title>");
                _StringBuilder.AppendLine("  </head>");
                _StringBuilder.AppendLine("  <body>");

                _StringBuilder.Append    ("    <h1>Error ").Append(HTTPStatusCode).AppendLine("</h1>");
                _StringBuilder.AppendLine("    <p>");
                _StringBuilder.AppendLine("      The client reqest from '" + RemoteSocket + "' led to an error!<br /><br />");
                _StringBuilder.AppendLine("    </p>");

                _StringBuilder.AppendLine("    <h3>Raw request header:</h3>");
                _StringBuilder.AppendLine("    <pre>");
                _StringBuilder.AppendLine(RequestHeader.RawHTTPHeader);
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

                if (Error != null)
                {
                    _StringBuilder.AppendLine("    <h3>Reason:</h3>");
                    _StringBuilder.AppendLine("    <pre>");
                    _StringBuilder.AppendLine(Error);
                    _StringBuilder.AppendLine("    </pre>");
                }

                _StringBuilder.AppendLine("  </body>");
                _StringBuilder.AppendLine("</html>");
                _StringBuilder.AppendLine();

                var _HTMLErrorpage = _StringBuilder.ToString().ToUTF8Bytes();

                #endregion

                WriteToResponseStream(
                    new HTTPResponseBuilder()
                    {
                        HTTPStatusCode = HTTPStatusCode,
                        Server         = ServerName,
                        ContentType    = HTTPContentType.HTML_UTF8,
                        Content        = _HTMLErrorpage
                    }
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
