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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for crypto key usage triples.
    /// </summary>
    public static class CryptoKeyUsageTripleExtensions
    {

        public static CryptoKeyUsageTriple RelatesTo(this CryptoKeyUsageTuple  Tuple,
                                                     CryptoKeyUsage            Usage2)

            => new (Tuple.Usage,
                    Tuple.Relation,
                    Usage2);

        public static CryptoKeyUsageTriple RelatesFrom(this CryptoKeyUsageTuple  Tuple,
                                                       CryptoKeyUsage            Usage1)

            => new (Usage1,
                    Tuple.Relation,
                    Tuple.Usage);

    }


    /// <summary>
    /// Two crypto key usages with a relation inbetween.
    /// </summary>
    public class CryptoKeyUsageTriple : IEquatable <CryptoKeyUsageTriple>,
                                        IComparable<CryptoKeyUsageTriple>,
                                        IComparable
    {

        #region Properties

        /// <summary>
        /// The first crypto key usage.
        /// </summary>
        public CryptoKeyUsage          Usage1      { get; }

        /// <summary>
        /// The crypto key usage relation.
        /// </summary>
        public CryptoKeyUsageRelation  Relation    { get; }

        /// <summary>
        /// The second crypto key usage.
        /// </summary>
        public CryptoKeyUsage          Usage2      { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique crypto key usage triple.
        /// </summary>
        /// <param name="Usage1">The first crypto key usage.</param>
        /// <param name="Relation">The crypto key usage relation.</param>
        /// <param name="Usage2">The second crypto key usage.</param>
        public CryptoKeyUsageTriple(CryptoKeyUsage          Usage1,
                                    CryptoKeyUsageRelation  Relation,
                                    CryptoKeyUsage          Usage2)
        {

            this.Usage1    = Usage1;
            this.Relation  = Relation;
            this.Usage2    = Usage2;

        }

        #endregion


        #region Operator overloading

        #region Operator == (CryptoKeyUsageTriple1, CryptoKeyUsageTriple2)

        /// <summary>
        /// Compares two crypto key usage triples for equality.
        /// </summary>
        /// <param name="CryptoKeyUsageTriple1">A crypto key usage triple.</param>
        /// <param name="CryptoKeyUsageTriple2">Another crypto key usage triple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoKeyUsageTriple? CryptoKeyUsageTriple1,
                                           CryptoKeyUsageTriple? CryptoKeyUsageTriple2)
        {

            if (Object.ReferenceEquals(CryptoKeyUsageTriple1, CryptoKeyUsageTriple2))
                return true;

            if (CryptoKeyUsageTriple1 is null || CryptoKeyUsageTriple2 is null)
                return false;

            return CryptoKeyUsageTriple1.Equals(CryptoKeyUsageTriple2);

        }

        #endregion

        #region Operator != (CryptoKeyUsageTriple1, CryptoKeyUsageTriple2)

        /// <summary>
        /// Compares two crypto key usage triples for inequality.
        /// </summary>
        /// <param name="CryptoKeyUsageTriple1">A crypto key usage triple.</param>
        /// <param name="CryptoKeyUsageTriple2">Another crypto key usage triple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoKeyUsageTriple? CryptoKeyUsageTriple1,
                                           CryptoKeyUsageTriple? CryptoKeyUsageTriple2)

            => !(CryptoKeyUsageTriple1 == CryptoKeyUsageTriple2);

        #endregion

        #region Operator <  (CryptoKeyUsageTriple1, CryptoKeyUsageTriple2)

        /// <summary>
        /// Compares two crypto key usage triples.
        /// </summary>
        /// <param name="CryptoKeyUsageTriple1">A crypto key usage triple.</param>
        /// <param name="CryptoKeyUsageTriple2">Another crypto key usage triple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoKeyUsageTriple? CryptoKeyUsageTriple1,
                                          CryptoKeyUsageTriple? CryptoKeyUsageTriple2)

            => CryptoKeyUsageTriple1 is null
                   ? throw new ArgumentNullException(nameof(CryptoKeyUsageTriple1), "The given crypto key usage triple must not be null!")
                   : CryptoKeyUsageTriple1.CompareTo(CryptoKeyUsageTriple2) < 0;

        #endregion

        #region Operator <= (CryptoKeyUsageTriple1, CryptoKeyUsageTriple2)

        /// <summary>
        /// Compares two crypto key usage triples.
        /// </summary>
        /// <param name="CryptoKeyUsageTriple1">A crypto key usage triple.</param>
        /// <param name="CryptoKeyUsageTriple2">Another crypto key usage triple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoKeyUsageTriple? CryptoKeyUsageTriple1,
                                           CryptoKeyUsageTriple? CryptoKeyUsageTriple2)

            => !(CryptoKeyUsageTriple1 > CryptoKeyUsageTriple2);

        #endregion

        #region Operator >  (CryptoKeyUsageTriple1, CryptoKeyUsageTriple2)

        /// <summary>
        /// Compares two crypto key usage triples.
        /// </summary>
        /// <param name="CryptoKeyUsageTriple1">A crypto key usage triple.</param>
        /// <param name="CryptoKeyUsageTriple2">Another crypto key usage triple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoKeyUsageTriple? CryptoKeyUsageTriple1,
                                          CryptoKeyUsageTriple? CryptoKeyUsageTriple2)

            => CryptoKeyUsageTriple1 is null
                   ? throw new ArgumentNullException(nameof(CryptoKeyUsageTriple1), "The given crypto key usage triple must not be null!")
                   : CryptoKeyUsageTriple1.CompareTo(CryptoKeyUsageTriple2) > 0;

        #endregion

        #region Operator >= (CryptoKeyUsageTriple1, CryptoKeyUsageTriple2)

        /// <summary>
        /// Compares two crypto key usage triples.
        /// </summary>
        /// <param name="CryptoKeyUsageTriple1">A crypto key usage triple.</param>
        /// <param name="CryptoKeyUsageTriple2">Another crypto key usage triple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoKeyUsageTriple? CryptoKeyUsageTriple1,
                                           CryptoKeyUsageTriple? CryptoKeyUsageTriple2)

            => !(CryptoKeyUsageTriple1 < CryptoKeyUsageTriple2);

        #endregion

        #endregion

        #region IComparable<CryptoKeyUsageTriple> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto key usage triples.
        /// </summary>
        /// <param name="Object">A crypto key usage triple to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoKeyUsageTriple cryptoKeyUsageTriple
                   ? CompareTo(cryptoKeyUsageTriple)
                   : throw new ArgumentException("The given object is not a crypto key usage triple!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoKeyUsageTriple)

        /// <summary>
        /// Compares two crypto key usage triples.
        /// </summary>
        /// <param name="CryptoKeyUsageTriple">A crypto key usage triple to compare with.</param>
        public Int32 CompareTo(CryptoKeyUsageTriple? CryptoKeyUsageTriple)
        {

            if (CryptoKeyUsageTriple is null)
                throw new ArgumentNullException(nameof(CryptoKeyUsageTriple), "The given crypto key usage triple must not be null!");

            var c = Usage1.  CompareTo(CryptoKeyUsageTriple.Usage1);

            if (c == 0)
                c = Relation.CompareTo(CryptoKeyUsageTriple.Relation);

            if (c == 0)
                c = Usage2.  CompareTo(CryptoKeyUsageTriple.Usage2);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<CryptoKeyUsageTriple> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto key usage triples for equality.
        /// </summary>
        /// <param name="Object">A crypto key usage triple to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoKeyUsageTriple cryptoKeyUsageTriple &&
                   Equals(cryptoKeyUsageTriple);

        #endregion

        #region Equals(CryptoKeyUsageTriple)

        /// <summary>
        /// Compares two crypto key usage triples for equality.
        /// </summary>
        /// <param name="CryptoKeyUsageTriple">A crypto key usage triple to compare with.</param>
        public Boolean Equals(CryptoKeyUsageTriple? CryptoKeyUsageTriple)


            => CryptoKeyUsageTriple is not null &&

               Usage1.  Equals(CryptoKeyUsageTriple.Usage1)   &&
               Relation.Equals(CryptoKeyUsageTriple.Relation) &&
               Usage2.  Equals(CryptoKeyUsageTriple.Usage2);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => Usage1.  GetHashCode() ^
               Relation.GetHashCode() ^
               Usage2.  GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{Usage1} -{Relation}-> {Usage2}";

        #endregion

    }

}
