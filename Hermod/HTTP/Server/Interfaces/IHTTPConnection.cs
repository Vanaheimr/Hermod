///*
// * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
// * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
// *
// * Licensed under the Apache License, Version 2.0 (the "License");
// * you may not use this file except in compliance with the License.
// * You may obtain a copy of the License at
// *
// *     http://www.apache.org/licenses/LICENSE-2.0
// *
// * Unless required by applicable law or agreed to in writing, software
// * distributed under the License is distributed on an "AS IS" BASIS,
// * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// * See the License for the specific language governing permissions and
// * limitations under the License.
// */

//#region Usings

//using System;
//using System.IO;
//using System.Net;
//using System.Net.Sockets;

//using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

//#endregion

//namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
//{

//    /// <summary>
//    /// The interface between the HTTP connection and an
//    /// upper-layer application.
//    /// </summary>
//    public interface IHTTPConnection : ITCPConnection
//    {

//        HTTPRequest         RequestHeader   { get; }
//        //ToDo: Change this to a Stream!
//        Byte[]              RequestBody     { get; }

//        HTTPResponse        ResponseHeader  { get; }
//        NetworkStream       ResponseStream  { get; }

//        String              ServerName      { get; }
//        HTTPSecurity        HTTPSecurity    { get; set; }
//        //URIMapping          URLMapping      { get; set; }

//        String              ErrorReason     { get; set; }
//        Exception           LastException   { get; set; }

//        IHTTPServer         HTTPServer      { get; set; }

//        void SendErrorpage(HTTPStatusCode HTTPStatusCode,
//                           String         ErrorReason   = null,
//                           Exception      LastException = null);

//    }

//}
