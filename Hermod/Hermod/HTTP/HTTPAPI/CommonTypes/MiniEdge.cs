/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of UsersAPI <https://www.github.com/Vanaheimr/UsersAPI>
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

    public class MiniEdge<TSource, TEdge, TTarget> : IEquatable<MiniEdge<TSource, TEdge, TTarget>>,
                                                     IComparable<MiniEdge<TSource, TEdge, TTarget>>,
                                                     IComparable

        where TSource : IEntity
        where TEdge   : IComparable
        where TTarget : IEntity

    {

        #region Properties

        public TSource         Source          { get; internal set; }

        public TEdge           EdgeLabel       { get; }

        public TTarget         Target          { get; internal set; }


        public PrivacyLevel    PrivacyLevel    { get; }

        public DateTimeOffset  Created         { get; }

        #region UserDefined

        //private readonly Dictionary<String, Object> _UserDefined;

        ///// <summary>
        ///// A lookup for user-defined properties.
        ///// </summary>
        //public Dictionary<String, Object> UserDefined
        //{
        //    get
        //    {
        //        return _UserDefined;
        //    }
        //}

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new miniedge.
        /// </summary>
        /// <param name="Source">The source of the edge.</param>
        /// <param name="EdgeLabel">The label of the edge.</param>
        /// <param name="Target">The target of the edge</param>
        /// <param name="PrivacyLevel">The level of privacy of this edge.</param>
        /// <param name="Created">The creation timestamp of the miniedge.</param>
        public MiniEdge(TSource       Source,
                        TEdge         EdgeLabel,
                        TTarget       Target,
                        PrivacyLevel  PrivacyLevel  = PrivacyLevel.Private,
                        DateTime?     Created       = null)
        {

            #region Initial checks

            if (System.Collections.Generic.EqualityComparer<TSource>.Default.Equals(Source, default))
                throw new ArgumentNullException(nameof(Source), "The given source must not be null!");

            if (System.Collections.Generic.EqualityComparer<TTarget>.Default.Equals(Target, default))
                throw new ArgumentNullException(nameof(Target), "The given target must not be null!");

            #endregion

            this.Source        = Source;
            this.Target        = Target;
            this.EdgeLabel     = EdgeLabel;
            this.PrivacyLevel  = PrivacyLevel;
            this.Created       = Created ?? Timestamp.Now;
            //   this._UserDefined   = new Dictionary<String, Object>();

        }

        #endregion


        #region IComparable<MiniEdge<TSource, TEdge, TTarget>> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object is null)
                throw new ArgumentNullException("The given object must not be null!");

            if (!(Object is MiniEdge<TSource, TEdge, TTarget>))
                throw new ArgumentException("The given object is not a miniedge!");

            return CompareTo((MiniEdge<TSource, TEdge, TTarget>)Object);

        }

        #endregion

        #region CompareTo(MiniEdge)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MiniEdge">A miniedge to compare with.</param>
        public Int32 CompareTo(MiniEdge<TSource, TEdge, TTarget> MiniEdge)
        {

            if ((Object)MiniEdge is null)
                throw new ArgumentNullException("The given miniedge must not be null!");

            var source = Source.CompareTo(MiniEdge.Source);
            var type = EdgeLabel.CompareTo(MiniEdge.EdgeLabel);
            var target = Target.CompareTo(MiniEdge.Target);

            if (type != 0)
                return type;

            if (source != 0)
                return source;

            if (target != 0)
                return target;

            return 0;

        }

        #endregion

        #endregion

        #region IEquatable<MiniEdge> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object is null)
                return false;

            if (!(Object is MiniEdge<TSource, TEdge, TTarget>))
                return false;

            return this.Equals((MiniEdge<TSource, TEdge, TTarget>)Object);

        }

        #endregion

        #region Equals(MiniEdge)

        /// <summary>
        /// Compares two miniedges for equality.
        /// </summary>
        /// <param name="MiniEdge">A miniedge to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(MiniEdge<TSource, TEdge, TTarget> MiniEdge)
        {

            if ((Object)MiniEdge is null)
                return false;

            if (!Source.Equals(MiniEdge.Source))
                return false;

            if (!EdgeLabel.Equals(MiniEdge.EdgeLabel))
                return false;

            if (!Target.Equals(MiniEdge.Target))
                return false;

            return true;

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Get the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {

            // Overflow is fine, just wrap
            // http://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethash code

            unchecked
            {

                var hash = 17;

                hash = hash * 23 + Source.GetHashCode();
                hash = hash * 23 + EdgeLabel.GetHashCode();
                hash = hash * 23 + Target.GetHashCode();
                hash = hash * 23 + PrivacyLevel.GetHashCode();
                hash = hash * 23 + Created.GetHashCode();

                return hash;

            }

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            var IsNotPublic = PrivacyLevel != PrivacyLevel.World ? String.Concat(" [", PrivacyLevel.ToString(), "]") : String.Empty;

            return String.Concat(Source, " --", EdgeLabel, IsNotPublic, "->", Target);

        }

        #endregion

    }

}
