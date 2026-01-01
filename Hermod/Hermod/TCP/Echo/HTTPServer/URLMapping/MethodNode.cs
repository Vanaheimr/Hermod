/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

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
        public HTTPMethod             Method             { get; } = Method;
        public HTTPRequestHandlersX?  RequestHandlers
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


        public IEnumerable<HTTPRequestHandlersX>  HTTPRequestHandlers
            => contentTypes.Values;

        #endregion


        public void AddContentType(HTTPContentType       HTTPContentType,
                                   HTTPRequestHandlersX  Handler)
        {

            if (!contentTypes.TryAdd(HTTPContentType, Handler))
            {
                if (AllowReplacement == URLReplacement.Allow)
                    contentTypes[HTTPContentType] = Handler;
                else
                    throw new InvalidOperationException("Cannot override existing ContentType handler!");
            }

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
            //    c = MediaSubType.CompareTo(MethodNode.MediaSubType);

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

}
