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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using static org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPServer;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    public class ParsedRequest
    {

        public HTTPRequestHandlersX?       RequestHandlers    { get; }
        public Dictionary<String, String>  Parameters         { get; }
        public String?                     ErrorResponse      { get; }

        private ParsedRequest(HTTPRequestHandlersX?       RequestHandlers,
                              Dictionary<String, String>  Parameters,
                              String?                     ErrorResponse   = null)
        {

            this.RequestHandlers  = RequestHandlers;
            this.Parameters       = Parameters;
            this.ErrorResponse    = ErrorResponse;

        }

        public static ParsedRequest Parsed(HTTPRequestHandlersX?       RequestHandler,
                                           Dictionary<String, String>  Parameters)

            => new (
                   RequestHandler,
                   Parameters
               );


        public static ParsedRequest Error(String ErrorResponse)

            => new (
                   null,
                   [],
                   ErrorResponse
               );


    }


    public class ParsedRequest2
    {

        public RouteNode2?                 RouteNode         { get; }
        public Dictionary<String, String>  Parameters        { get; }
        public String?                     ErrorResponse     { get; }

        private ParsedRequest2(RouteNode2?                 RouteNode,
                               Dictionary<String, String>  Parameters,
                               String?                     ErrorResponse   = null)
        {

            this.RouteNode       = RouteNode;
            this.Parameters      = Parameters;
            this.ErrorResponse   = ErrorResponse;

        }

        public static ParsedRequest2 Parsed(RouteNode2?                 RouteNode,
                                            Dictionary<String, String>  Parameters)

            => new (
                   RouteNode,
                   Parameters
               );


        public static ParsedRequest2 Error(String                       ErrorResponse,
                                           Dictionary<String, String>?  Parameters   = null)

            => new (
                   null,
                   Parameters ?? [],
                   ErrorResponse
               );

    }



    public class MethodNode(HTTPMethod             Method,
                            HTTPRequestHandlersX?  RequestHandlers    = null,
                            URLReplacement?        AllowReplacement   = null)

        : IEquatable<MethodNode>,
          IComparable<MethodNode>,
          IComparable

    {

        #region Data

        private          HTTPRequestHandlersX?                              requestHandlers   = RequestHandlers;

        private readonly Dictionary<HTTPContentType, HTTPRequestHandlersX>  contentTypes      = [];

        #endregion

        #region Properties
        public HTTPMethod                    Method                { get; }      = Method;
        public HTTPRequestHandlersX? RequestHandlers
        {

            get
            {
                return requestHandlers;
            }

            set
            {

                if (requestHandlers is null || AllowReplacement == URLReplacement.Allow)
                    requestHandlers = value;

                else
                    throw new InvalidOperationException("Cannot override existing RequestHandlers!");

            }

        }
        //public OnHTTPRequestLogDelegate?     HTTPRequestLogger     { get; set; }
        //public OnHTTPResponseLogDelegate?    HTTPResponseLogger    { get; set; }
        public URLReplacement                AllowReplacement      { get; set; } = AllowReplacement ?? URLReplacement.Fail;

        public IEnumerable<HTTPContentType>  ContentTypes
            => contentTypes.Keys;

        #endregion


        public void AddContentType(HTTPContentType       HTTPContentType,
                                   HTTPRequestHandlersX  Handler)
        {
            contentTypes.Add(HTTPContentType, Handler);
        }


        public Boolean TryGetContentType(HTTPContentType                                HTTPContentType,
                                         [NotNullWhen(true)] out HTTPRequestHandlersX?  Handler)
        {

            if (contentTypes.TryGetValue(HTTPContentType, out var handler) &&
                handler is not null)
            {
                Handler = handler;
                return true;
            }

            Handler = null;
            return false;

        }


        #region Operator overloading

        #region Operator == (MethodNode1, MethodNode2)

        public static Boolean operator == (MethodNode? MethodNode1,
                                           MethodNode? MethodNode2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(MethodNode1, MethodNode2))
                return true;

            // If one is null, but not both, return false.
            if (MethodNode1 is null || MethodNode2 is null)
                return false;

            return MethodNode1.Equals(MethodNode2);

        }

        #endregion

        #region Operator != (MethodNode1, MethodNode2)

        public static Boolean operator != (MethodNode? MethodNode1,
                                           MethodNode? MethodNode2)

            => !(MethodNode1 == MethodNode2);

        #endregion

        #region Operator <  (MethodNode1, MethodNode2)

        public static Boolean operator < (MethodNode? MethodNode1,
                                          MethodNode? MethodNode2)
        {

            if (MethodNode1 is null)
                throw new ArgumentNullException(nameof(MethodNode1), "The given MethodNode1 must not be null!");

            if (MethodNode2 is null)
                throw new ArgumentNullException(nameof(MethodNode2), "The given MethodNode2 must not be null!");

            return MethodNode1.CompareTo(MethodNode2) < 0;

        }

        #endregion

        #region Operator >  (MethodNode1, MethodNode2)

        public static Boolean operator > (MethodNode? MethodNode1,
                                          MethodNode? MethodNode2)
        {

            if (MethodNode1 is null)
                throw new ArgumentNullException(nameof(MethodNode1), "The given MethodNode1 must not be null!");

            if (MethodNode2 is null)
                throw new ArgumentNullException(nameof(MethodNode2), "The given MethodNode2 must not be null!");

            return MethodNode1.CompareTo(MethodNode2) > 0;

        }

        #endregion

        #region Operator <= (MethodNode1, MethodNode2)

        public static Boolean operator <= (MethodNode? MethodNode1,
                                           MethodNode? MethodNode2)

            => !(MethodNode1 > MethodNode2);

        #endregion

        #region Operator >= (MethodNode1, MethodNode2)

        public static Boolean operator >= (MethodNode? MethodNode1,
                                           MethodNode? MethodNode2)

            => !(MethodNode1 < MethodNode2);

        #endregion

        #endregion

        #region IComparable<MethodNode> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two MethodNodes.
        /// </summary>
        /// <param name="Object">A MethodNode to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is MethodNode methodNode
                   ? CompareTo(methodNode)
                   : throw new ArgumentException("The given object is not a MethodNode!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(MethodNode)

        /// <summary>
        /// Compares two MethodNodes.
        /// </summary>
        /// <param name="MethodNode">A MethodNode to compare with.</param>
        public Int32 CompareTo(MethodNode? MethodNode)
        {

            if (MethodNode is null)
                throw new ArgumentNullException(nameof(MethodNode),
                                                "The given MethodNode must not be null!");

            var c = Method.CompareTo(MethodNode.Method);

            //if (c == 0)
            //    return MediaSubType.CompareTo(MethodNode.MediaSubType);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<MethodNode> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two MethodNodes for equality.
        /// </summary>
        /// <param name="MethodNode">A MethodNode to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is MethodNode methodNode &&
                   Equals(methodNode);

        #endregion

        #region Equals(MethodNode)

        /// <summary>
        /// Compares two MethodNodes for equality.
        /// </summary>
        /// <param name="MethodNode">A MethodNode to compare with.</param>
        public Boolean Equals(MethodNode? MethodNode)

            => MethodNode is not null &&

               Method.Equals(MethodNode.Method);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Get the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return Method.GetHashCode();
                       //RequestHandlers
            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{Method}: {contentTypes.Select(ct => ct.Key.MediaType).AggregateWith(", ")}";

        #endregion

    }

    //public class ContentTypeNode
    //{

    //    public ContentTypeNode(HTTPContentType     ContentType,
    //                           HTTPRequestHandleX  RequestHandle)
    //    {
    //        this.ContentType    = ContentType;
    //        this.RequestHandle  = RequestHandle;
    //    }

    //    public HTTPContentType     ContentType      { get; }
    //    public HTTPRequestHandleX  RequestHandle    { get; set; }
    //    public Dictionary<HTTPContentType, HTTPRequestHandleX> ContentTypes { get; } = []; // ContentType -> ContentType -> Handler


    //    public override String ToString()
    //        => $"{ContentType} {ContentTypes.Count} content types";

    //}



    /// <summary>
    /// A node which stores information for maintaining multiple http hostnames.
    /// </summary>
    public class RouteNode2
    {

        #region Data

        private HTTPRequestHandlersX? requestHandlers;

        #endregion

        #region Properties

        public String                                        FullPath           { get; }

        public String                                        Path               { get; }

        public String?                                       ParamName          { get; }

        public Boolean                                       CatchRestOfPath    { get; } = false;

        public ConcurrentDictionary<String, RouteNode2>      Children           { get; } = [];

        public ConcurrentDictionary<HTTPMethod, MethodNode>  Methods            { get; } = []; // Method -> ContentType -> Handler


        public URLReplacement                                AllowReplacement   { get; set; }

        public HTTPRequestHandlersX?                         RequestHandlers
        {

            get
            {
                return requestHandlers;
            }

            set
            {

                if (requestHandlers is null || AllowReplacement == URLReplacement.Allow)
                    requestHandlers = value;

                else
                    throw new InvalidOperationException("Cannot override existing RequestHandlers!");

            }

        }

        #endregion

        private RouteNode2(String           FullPath,
                           String           Path,
                           String?          ParamName,
                           Boolean          CatchRestOfPath,
                           URLReplacement?  AllowReplacement   = null)
        {

            this.FullPath          = FullPath;
            this.Path              = Path;
            this.ParamName         = ParamName;
            this.CatchRestOfPath   = CatchRestOfPath;
            this.AllowReplacement  = AllowReplacement ?? URLReplacement.Fail;

        }


        public static RouteNode2 FromPath(String FullPath, String Path)
            => new (FullPath, Path, null, false);

        public static RouteNode2 ForParameter(String FullPath, String ParamName)
            => new (FullPath, "", ParamName, false);

        public static RouteNode2 ForCatchRestOfPath(String FullPath, String ParamName, URLReplacement? AllowReplacement = null)
            => new (FullPath, "", ParamName, true);


        public override String ToString()

            => $"{FullPath}{(CatchRestOfPath ? " [catch rest of path]" : "")}: {Children.Count} childs, {Methods.Count} methods";

    }

}
