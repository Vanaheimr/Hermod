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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    /// <summary>
    /// A node which stores information for maintaining HTTP paths.
    /// </summary>
    public class PathNode
    {

        #region Data

        private HTTPRequestHandlersX? requestHandlers;

        #endregion

        #region Properties

        public String                                        FullPath           { get; }

        public String                                        Path               { get; }

        public String?                                       ParameterName      { get; }

        public Boolean                                       CatchRestOfPath    { get; } = false;

        public URLReplacement                                AllowReplacement   { get; set; }


        public ConcurrentDictionary<String, PathNode>        Children           { get; } = [];

        public ConcurrentDictionary<HTTPMethod, MethodNode>  Methods            { get; } = []; // Method -> ContentType -> Handler

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

        #region Constructor(s)

        private PathNode(String           FullPath,
                         String           Path,
                         String?          ParameterName,
                         Boolean          CatchRestOfPath,
                         URLReplacement?  AllowReplacement   = null)
        {

            this.FullPath          = FullPath;
            this.Path              = Path;
            this.ParameterName     = ParameterName;
            this.CatchRestOfPath   = CatchRestOfPath;
            this.AllowReplacement  = AllowReplacement ?? URLReplacement.Fail;

            unchecked
            {

                hashCode = this.FullPath.        GetHashCode()       * 11 ^
                           this.Path.            GetHashCode()       *  7 ^
                          (this.ParameterName?.  GetHashCode() ?? 0) *  5 ^
                           this.CatchRestOfPath. GetHashCode()       *  3 ^
                           this.AllowReplacement.GetHashCode();

            }

        }

        #endregion


        public static PathNode FromPath(String FullPath, String Path)
            => new (FullPath, Path, null, false);

        public static PathNode ForParameter(String FullPath, String ParamName)
            => new (FullPath, "", ParamName, false);

        public static PathNode ForCatchRestOfPath(String FullPath, String ParamName, URLReplacement? AllowReplacement = null)
            => new (FullPath, "", ParamName, true, AllowReplacement);



        #region Operator overloading

        #region Operator == (PathNode1, PathNode2)

        public static Boolean operator == (PathNode? PathNode1,
                                           PathNode? PathNode2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(PathNode1, PathNode2))
                return true;

            // If one is null, but not both, return false.
            if (PathNode1 is null || PathNode2 is null)
                return false;

            return PathNode1.Equals(PathNode2);

        }

        #endregion

        #region Operator != (PathNode1, PathNode2)

        public static Boolean operator != (PathNode? PathNode1,
                                           PathNode? PathNode2)

            => !(PathNode1 == PathNode2);

        #endregion

        #region Operator <  (PathNode1, PathNode2)

        public static Boolean operator < (PathNode? PathNode1,
                                          PathNode? PathNode2)
        {

            if (PathNode1 is null)
                throw new ArgumentNullException(nameof(PathNode1), "The given PathNode1 must not be null!");

            if (PathNode2 is null)
                throw new ArgumentNullException(nameof(PathNode2), "The given PathNode2 must not be null!");

            return PathNode1.CompareTo(PathNode2) < 0;

        }

        #endregion

        #region Operator >  (PathNode1, PathNode2)

        public static Boolean operator > (PathNode? PathNode1,
                                          PathNode? PathNode2)
        {

            if (PathNode1 is null)
                throw new ArgumentNullException(nameof(PathNode1), "The given PathNode1 must not be null!");

            if (PathNode2 is null)
                throw new ArgumentNullException(nameof(PathNode2), "The given PathNode2 must not be null!");

            return PathNode1.CompareTo(PathNode2) > 0;

        }

        #endregion

        #region Operator <= (PathNode1, PathNode2)

        public static Boolean operator <= (PathNode? PathNode1,
                                           PathNode? PathNode2)

            => !(PathNode1 > PathNode2);

        #endregion

        #region Operator >= (PathNode1, PathNode2)

        public static Boolean operator >= (PathNode? PathNode1,
                                           PathNode? PathNode2)

            => !(PathNode1 < PathNode2);

        #endregion

        #endregion

        #region IComparable<PathNode> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two PathNodes.
        /// </summary>
        /// <param name="Object">A PathNode to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is PathNode pathNode
                   ? CompareTo(pathNode)
                   : throw new ArgumentException("The given object is not a PathNode!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(PathNode)

        /// <summary>
        /// Compares two PathNodes.
        /// </summary>
        /// <param name="PathNode">A PathNode to compare with.</param>
        public Int32 CompareTo(PathNode? PathNode)
        {

            if (PathNode is null)
                throw new ArgumentNullException(nameof(PathNode),
                                                "The given PathNode must not be null!");

            var c = FullPath.        CompareTo(PathNode.FullPath);

            if (c == 0)
                c = Path.            CompareTo(PathNode.Path);

            if (c == 0)
                c = String.Compare(
                        ParameterName,
                        PathNode.ParameterName,
                        StringComparison.OrdinalIgnoreCase
                    );

            if (c == 0)
                c = CatchRestOfPath. CompareTo(PathNode.CatchRestOfPath);

            if (c == 0)
                c = AllowReplacement.CompareTo(PathNode.AllowReplacement);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<PathNode> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two PathNodes for equality.
        /// </summary>
        /// <param name="PathNode">A PathNode to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is PathNode pathNode &&
                   Equals(pathNode);

        #endregion

        #region Equals(PathNode)

        /// <summary>
        /// Compares two PathNodes for equality.
        /// </summary>
        /// <param name="PathNode">A PathNode to compare with.</param>
        public Boolean Equals(PathNode? PathNode)

            => PathNode is not null &&

               FullPath.        Equals(PathNode.FullPath)                     &&
               Path.            Equals(PathNode.Path)                         &&
               String.          Equals(ParameterName, PathNode.ParameterName) &&
               CatchRestOfPath. Equals(PathNode.CatchRestOfPath)              &&
               AllowReplacement.Equals(PathNode.AllowReplacement);

        #endregion

        #endregion

        #region (override) GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{FullPath}{(CatchRestOfPath ? " [catch rest of path]" : "")}: {Children.Count} childs, {Methods.Count} methods";

        #endregion

    }

}
