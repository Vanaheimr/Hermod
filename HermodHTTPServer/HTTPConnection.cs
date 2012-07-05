/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IdentityModel.Tokens;

using de.ahzf.Illias.Commons;
using de.ahzf.Hermod.Sockets.TCP;

#endregion

namespace de.ahzf.Hermod.HTTP
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

        public HTTPRequest        InHTTPRequest  { get; protected set; }

        public Byte[]             RequestBody    { get; protected set; }

        public HTTPResponse       ResponseHeader { get; protected set; }

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
            ResponseHeader = new HTTPResponseBuilder();
            ResponseStream = myTCPClientConnection.GetStream();
        }

        #endregion

        #endregion

        
        #region ProcessHTTP()

        public void ProcessHTTP_new()
        {

            using (var _HTTPStream = TCPClientConnection.GetStream())
            {

                var helper = new StreamHelper(_HTTPStream, 65535);
                
                Debug.WriteLine("New connection " + RemoteHost + ":" + RemotePort + " @ " + Thread.CurrentThread.ManagedThreadId);

                helper.NetworkStream.BeginRead(helper.Buffer, 0, helper.Buffer.Length, StreamReadCallback, helper);

                Int32 ReadPosition = 0;

                while (TCPClientConnection.Connected)
                {
                    Thread.Sleep(1);
                    //helper.DataAvailable.WaitOne();
                };

                Debug.WriteLine("Closing connection " + RemoteHost + ":" + RemotePort + " @ " + Thread.CurrentThread.ManagedThreadId);
                TCPClientConnection.Close();

            }

        }

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

        private void ProcessHTTP1()
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

                    // Create a new HTTPRequestHeader
                    var HeaderBytes = new Byte[_ReadPosition - 1];
                    Array.Copy(_ByteArray, 0, HeaderBytes, 0, _ReadPosition - 1);

                    InHTTPRequest = new HTTPRequest(HeaderBytes.ToUTF8String());

                    // The parsing of the http header failed!
                    if (InHTTPRequest.HTTPStatusCode != HTTPStatusCode.OK)
                    {
                        SendErrorpage(InHTTPRequest.HTTPStatusCode, InHTTPRequest);
                        return;
                    }

                    // Copy only the number of bytes given within
                    // the HTTP header element 'ContentType'!
                    if (InHTTPRequest.ContentLength.HasValue)
                    {
                        RequestBody = new Byte[InHTTPRequest.ContentLength.Value];
                        Array.Copy(_ByteArray, _ReadPosition + 1, RequestBody, 0, (Int64) InHTTPRequest.ContentLength.Value);
                    }
                    else
                        RequestBody = new Byte[0];

                    #endregion

                    var BestContentType = InHTTPRequest.Accept.BestMatchingContentType(Implementations.Keys.ToArray());
                    var BestImpl        = Implementations[BestContentType];

                    #region Invoke upper-layer protocol constructor

                    // Get constructor for HTTPServiceType
                    var _Type = BestImpl.GetType().
                                GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                               null,
                                               new Type[] {
                                                   typeof(IHTTPConnection)
                                               },
                                               null);

                    if (_Type == null)
                        throw new ArgumentException("A appropriate constructor for type '" + typeof(HTTPServiceInterface).Name + "' could not be found!");


                    // Invoke constructor of HTTPServiceType
                    _HTTPServiceInterface = _Type.Invoke(new Object[] { this }) as HTTPServiceInterface;

                    if (_HTTPServiceInterface == null)
                        throw new ArgumentException("A http connection of type '" + typeof(HTTPServiceInterface).Name + "' could not be created!");

                    if (NewHTTPServiceHandler != null)
                        NewHTTPServiceHandler(_HTTPServiceInterface);

                    #endregion

                    //ToDo: Add HTTP pipelining!

                    #region Get and check callback...

                    var _ParsedCallback = URLMapping.GetHandler(InHTTPRequest.Host,
                                                                InHTTPRequest.UrlPath,
                                                                InHTTPRequest.HTTPMethod,
                                                                BestContentType);

                    if (_ParsedCallback == null || _ParsedCallback.Item1 == null)// || _ParsedCallback.Item1.MethodCallback == null)
                    {

                        SendErrorpage(HTTPStatusCode.InternalServerError,
                                      InHTTPRequest,
                                      ErrorReason: "Could not find a valid handler for URL: " + InHTTPRequest.UrlPath);

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
                    //        responseHeader = new HTTPResponseBuilderHeader() { HttpStatusCode = HTTPStatusCode.InternalServerError, ContentLength = responseBodyBytes.ULongLength() };
                    //        responseHeaderBytes = responseHeader.ToBytes();

                    //        Debug.WriteLine("!!!Authentication other than Basic currently not supported!!!");
                    //    }

                    //    #endregion

                    //}

                    //else if (parsedCallback.Item1.NeedsExplicitAuthentication.HasValue && parsedCallback.Item1.NeedsExplicitAuthentication.Value)
                    //{

                    //    #region The server does not have authentication but the Interface explicitly needs authentication

                    //    responseBodyBytes = Encoding.UTF8.GetBytes("Authentication not supported for this server!");
                    //    responseHeader = new HTTPResponseBuilderHeader() { HttpStatusCode = HTTPStatusCode.InternalServerError, ContentLength = responseBodyBytes.ULongLength() };
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
                            if (_HTTPResponse == null)
                            {

                                SendErrorpage(HTTPStatusCode.InternalServerError,
                                              InHTTPRequest,
                                              ErrorReason: "Could not invoke method for URL: " + InHTTPRequest.UrlPath);

                                return;

                            }

                            ResponseHeader = _HTTPResponse;

                            #region In case of errors => send errorpage

                            if (ResponseHeader.HTTPStatusCode.IsClientError ||
                                ResponseHeader.HTTPStatusCode.IsServerError)
                            {

                                SendErrorpage(ResponseHeader.HTTPStatusCode,
                                              InHTTPRequest,
                                              LastException: LastException);

                                return;

                            }

                            #endregion

                            else
                                WriteToResponseStream(_HTTPResponse);

                        }

                        catch (Exception e)
                        {

                            WriteToResponseStream(
                    
                                new HTTPResponseBuilder()
                                {
                                    HTTPStatusCode = HTTPStatusCode.InternalServerError,
                                    CacheControl   = "no-cache",
                                    ContentType    = HTTPContentType.TEXT_UTF8,
                                    Content        = e.ToString().ToUTF8Bytes()
                                });

                        }

                    }

                    #endregion

                }

                catch (SocketException Exception)
                {
                    Debug.WriteLine("The remote host has disconnected: " + Exception);
                }

                catch (Exception Exception)
                {

                    Debug.WriteLine("General ProcessHTTP() exception: " + Exception);

                    SendErrorpage(ResponseHeader.HTTPStatusCode,
                                  InHTTPRequest,
                                  LastException: Exception);

                }

        }

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

                    #region Read from networkstream

                    Int32 _DataRead;
                    UInt32 _Retries = 0;

                    while (!_EndOfHTTPHeader || _HTTPStream.DataAvailable || !TCPClientConnection.Connected)
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
                            Debug.WriteLine("Thread[" + Thread.CurrentThread.ManagedThreadId + " from: " + RemoteHost + ":" + RemotePort + "]: length of stream so far: " + _MemoryStream.Length + " @ " + _EndOfHTTPHeader + ", " + _HTTPStream.DataAvailable + ", " + TCPClientConnection.Connected);
                        }

                        Thread.Sleep(10);
                        _Retries++;

                        if (_Retries > 2)
                        {
                            Debug.WriteLine("Thread[" + Thread.CurrentThread.ManagedThreadId + " from: " + RemoteHost + ":" + RemotePort + "]: Closing connection!");
                            TCPClientConnection.Close();
                            break;
                        }

                    }

                    if (!_EndOfHTTPHeader)
                    {

                        if (TCPClientConnection.Connected)
                        {

                            SendErrorpage(HTTPStatusCode.InternalServerError,
                                          InHTTPRequest,
                                          ErrorReason: "Could not find the end of a valid HTTP header!");

                        }

                        return;

                    }

                    #endregion

                    //Debug.WriteLine("Thread[" + Thread.CurrentThread.ManagedThreadId + "]: Valid HTTP header found!");

                    // Create a new HTTPRequestHeader
                    var HeaderBytes = new Byte[_ReadPosition - 1];
                    Array.Copy(_ByteArray, 0, HeaderBytes, 0, _ReadPosition - 1);

                    InHTTPRequest = new HTTPRequest(HeaderBytes.ToUTF8String());

                    // The parsing of the http header failed!
                    if (InHTTPRequest.HTTPStatusCode != HTTPStatusCode.OK)
                    {
                        SendErrorpage(InHTTPRequest.HTTPStatusCode, InHTTPRequest);
                        return;
                    }

                    // Copy only the number of bytes given within
                    // the HTTP header element 'ContentType'!
                    if (InHTTPRequest.ContentLength.HasValue)
                    {
                        RequestBody = new Byte[InHTTPRequest.ContentLength.Value];
                        Array.Copy(_ByteArray, _ReadPosition + 1, RequestBody, 0, (Int64) InHTTPRequest.ContentLength.Value);
                    }
                    else
                        RequestBody = new Byte[0];

                    #endregion

                    var BestContentType = InHTTPRequest.Accept.BestMatchingContentType(Implementations.Keys.ToArray());
                    var BestImpl        = Implementations[BestContentType];

                    #region Invoke upper-layer protocol constructor

                    // Get constructor for HTTPServiceType
                    var _Type = BestImpl.GetType().
                                GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                               null,
                                               new Type[] {
                                                   typeof(IHTTPConnection)
                                               },
                                               null);

                    if (_Type == null)
                        throw new ArgumentException("A appropriate constructor for type '" + typeof(HTTPServiceInterface).Name + "' could not be found!");


                    // Invoke constructor of HTTPServiceType
                    _HTTPServiceInterface = _Type.Invoke(new Object[] { this }) as HTTPServiceInterface;

                    if (_HTTPServiceInterface == null)
                        throw new ArgumentException("A http connection of type '" + typeof(HTTPServiceInterface).Name + "' could not be created!");

                    if (NewHTTPServiceHandler != null)
                        NewHTTPServiceHandler(_HTTPServiceInterface);

                    #endregion

                    //ToDo: Add HTTP pipelining!

                    #region Get and check callback...

                    Console.Write(InHTTPRequest.HTTPMethod + "\t" + InHTTPRequest.UrlPath + " => " + BestContentType + "\t");

                    var _ParsedCallback = URLMapping.GetHandler(InHTTPRequest.Host,
                                                                InHTTPRequest.UrlPath,
                                                                InHTTPRequest.HTTPMethod,
                                                                BestContentType);

                    if (_ParsedCallback == null || _ParsedCallback.Item1 == null)// || _ParsedCallback.Item1.MethodCallback == null)
                    {

                        SendErrorpage(HTTPStatusCode.InternalServerError,
                                      InHTTPRequest,
                                      ErrorReason: "Could not find a valid handler for URL: " + InHTTPRequest.UrlPath);

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
                    //        responseHeader = new HTTPResponseBuilderHeader() { HttpStatusCode = HTTPStatusCode.InternalServerError, ContentLength = responseBodyBytes.ULongLength() };
                    //        responseHeaderBytes = responseHeader.ToBytes();

                    //        Debug.WriteLine("!!!Authentication other than Basic currently not supported!!!");
                    //    }

                    //    #endregion

                    //}

                    //else if (parsedCallback.Item1.NeedsExplicitAuthentication.HasValue && parsedCallback.Item1.NeedsExplicitAuthentication.Value)
                    //{

                    //    #region The server does not have authentication but the Interface explicitly needs authentication

                    //    responseBodyBytes = Encoding.UTF8.GetBytes("Authentication not supported for this server!");
                    //    responseHeader = new HTTPResponseBuilderHeader() { HttpStatusCode = HTTPStatusCode.InternalServerError, ContentLength = responseBodyBytes.ULongLength() };
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
                            if (_HTTPResponse == null)
                            {

                                SendErrorpage(HTTPStatusCode.InternalServerError,
                                              InHTTPRequest,
                                              ErrorReason: "Could not invoke method for URL: " + InHTTPRequest.UrlPath);

                                return;

                            }

                            ResponseHeader = _HTTPResponse;

                            #region In case of errors => send errorpage

                            if (ResponseHeader.HTTPStatusCode.IsClientError ||
                                ResponseHeader.HTTPStatusCode.IsServerError)
                            {

                                SendErrorpage(ResponseHeader.HTTPStatusCode,
                                              InHTTPRequest,
                                              LastException: LastException);

                                return;

                            }

                            #endregion

                            else
                                WriteToResponseStream(_HTTPResponse);

                        }

                        catch (Exception e)
                        {

                            WriteToResponseStream(
                    
                                new HTTPResponseBuilder()
                                {
                                    HTTPStatusCode = HTTPStatusCode.InternalServerError,
                                    CacheControl   = "no-cache",
                                    ContentType    = HTTPContentType.TEXT_UTF8,
                                    Content        = e.ToString().ToUTF8Bytes()
                                });

                        }

                    }

                    #endregion

                }

                catch (SocketException Exception)
                {
                    Debug.WriteLine("The remote host has disconnected: " + Exception);
                }

                catch (Exception Exception)
                {

                    Debug.WriteLine("General ProcessHTTP() exception: " + Exception);

                    SendErrorpage(ResponseHeader.HTTPStatusCode,
                                  InHTTPRequest,
                                  LastException: Exception);

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


        #region (private) GetAuthenticationRequiredHeader()

        private HTTPResponseBuilder GetAuthenticationRequiredHeader()
        {
            return new HTTPResponseBuilder() {
                HTTPStatusCode = HTTPStatusCode.Unauthorized
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


        #region SendErrorpage(myHTTPStatusCode, myHTTPRequest, ErrorReason = null, LastException = null)

        public void SendErrorpage(HTTPStatusCode myHTTPStatusCode, HTTPRequest myHTTPRequest, String ErrorReason = null, Exception LastException = null)
        {

            #region Initial checks

            if (ErrorReason != null && ErrorReason == null)
                this.ErrorReason = ErrorReason;

            if (LastException != null && LastException == null)
                this.LastException = LastException;

            #endregion

            #region Send a customized errorpage...

            var __ErrorHandler = URLMapping.GetErrorHandler("*",
                                                            InHTTPRequest.UrlPath,
                                                            InHTTPRequest.HTTPMethod,
                                                            null,
                                                            myHTTPStatusCode);

            if (__ErrorHandler != null && __ErrorHandler.Item1 != null)
            {

                var _ParamArray    = __ErrorHandler.Item2.ToArray();
                var _Parameters    = new Object[_ParamArray.Count() + 2];
                    _Parameters[0] = ErrorReason;
                    _Parameters[1] = LastException;
                Array.Copy(_ParamArray, 0, _Parameters, 2, _ParamArray.Count());

                var _Response            = __ErrorHandler.Item1.Invoke(_HTTPServiceInterface, _Parameters);
                var _HTTPResponseBuilder = _Response as HTTPResponse;

                if (_Response == null || _HTTPResponseBuilder == null)
                {

                    SendErrorpage(HTTPStatusCode.InternalServerError,
                                  InHTTPRequest,
                                  ErrorReason: "Could not invoke errorpage for URL: " + InHTTPRequest.UrlPath);
                    
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
                _StringBuilder.Append    ("    <title>Error ").Append(myHTTPStatusCode).AppendLine("</title>");
                _StringBuilder.AppendLine("  </head>");
                _StringBuilder.AppendLine("  <body>");

                _StringBuilder.Append    ("    <h1>Error ").Append(myHTTPStatusCode).AppendLine("</h1>");
                _StringBuilder.AppendLine("    <p>");
                _StringBuilder.AppendLine("      The client reqest from '" + RemoteSocket + "' led to an error!<br /><br />");
                _StringBuilder.AppendLine("    </p>");

                _StringBuilder.AppendLine("    <h3>Raw request header:</h3>");
                _StringBuilder.AppendLine("    <pre>");
                _StringBuilder.AppendLine(InHTTPRequest.RawHTTPHeader);
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

                var _HTMLErrorpage = _StringBuilder.ToString().ToUTF8Bytes();

                #endregion

                WriteToResponseStream(
                    new HTTPResponseBuilder()
                    {
                        HTTPStatusCode = myHTTPStatusCode,
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
