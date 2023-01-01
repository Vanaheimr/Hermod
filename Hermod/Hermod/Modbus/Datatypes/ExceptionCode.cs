/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    /// <summary>
    /// If it's an exception response, then the next byte will be one
    /// these exception codes, indicating the reason for the failure.
    /// </summary>
    public sealed class ExceptionCode : IEquatable<ExceptionCode>,
                                        IComparable<ExceptionCode>,
                                        IComparable
    {

        #region Properties

        /// <summary>
        /// A human readable short information on this exception code.
        /// </summary>
        public String   Info           { get; }

        /// <summary>
        /// The numeric value of this exception code.
        /// </summary>
        public Byte     Value          { get; }

        /// <summary>
        /// A description of this exception code.
        /// </summary>
        public String?  Description    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new ExceptionCode.
        /// </summary>
        /// <param name="Info">A short information on the exception code.</param>
        /// <param name="Value">The numberic value of this exception code.</param>
        /// <param name="Description">A description of this exception code.</param>
        public ExceptionCode(String   Info,
                             Byte     Value,
                             String?  Description   = null)
        {

            this.Info         = Info;
            this.Value        = Value;
            this.Description  = Description;

        }

        #endregion


        #region (static) Static definitions

        public static readonly ExceptionCode IllegalFunction                   = new ("Illegal Function",                        1);
        public static readonly ExceptionCode IllegalDataAddress                = new ("Illegal Data Address",                    2);
        public static readonly ExceptionCode IllegalDataValue                  = new ("Illegal Data Value",                      3);
        public static readonly ExceptionCode SlaveDeviceFailure                = new ("Slave Device Failure",                    4);

        public static readonly ExceptionCode Acknowledge                       = new ("Acknowledge",                             5);
        public static readonly ExceptionCode SlaveDeviceBusy                   = new ("Slave Device Busy",                       6);
        public static readonly ExceptionCode MemoryParityError                 = new ("Memory Parity Error",                     8);
        public static readonly ExceptionCode GatewayPathUnavailable            = new ("Gateway Path Unavailable",               10);

        public static readonly ExceptionCode GatewayTargetPathFailedToRespond  = new ("Gateway Target Path Failed to Respond",  11);

        public static readonly ExceptionCode SentFailed                        = new ("Sent failed",                           100);
        public static readonly ExceptionCode NotConnected                      = new ("Not Connected",                         253);
        public static readonly ExceptionCode ConnectionLost                    = new ("Connection Lost",                       254);
        public static readonly ExceptionCode Timeout                           = new ("Timeout",                               255);

        #endregion


        #region Operator overloading

        #region Operator == (ExceptionCode1, ExceptionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ExceptionCode1">An exception code.</param>
        /// <param name="ExceptionCode2">Another exception code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (ExceptionCode? ExceptionCode1,
                                           ExceptionCode? ExceptionCode2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(ExceptionCode1, ExceptionCode2))
                return true;

            // If one is null, but not both, return false.
            if (ExceptionCode1 is null || ExceptionCode2 is null)
                return false;

            return ExceptionCode1.Equals(ExceptionCode2);

        }

        #endregion

        #region Operator != (ExceptionCode1, ExceptionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ExceptionCode1">An exception code.</param>
        /// <param name="ExceptionCode2">Another exception code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (ExceptionCode? ExceptionCode1,
                                           ExceptionCode? ExceptionCode2)
        {
            return !(ExceptionCode1 == ExceptionCode2);
        }

        #endregion

        #region Operator <  (ExceptionCode1, ExceptionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ExceptionCode1">An exception code.</param>
        /// <param name="ExceptionCode2">Another exception code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (ExceptionCode? ExceptionCode1,
                                          ExceptionCode? ExceptionCode2)
        {

            if (ExceptionCode1 is null)
                throw new ArgumentNullException(nameof(ExceptionCode1), "The given exception code must not be null!");

            return ExceptionCode1.CompareTo(ExceptionCode2) < 0;

        }

        #endregion

        #region Operator <= (ExceptionCode1, ExceptionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ExceptionCode1">An exception code.</param>
        /// <param name="ExceptionCode2">Another exception code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (ExceptionCode? ExceptionCode1,
                                           ExceptionCode? ExceptionCode2)

            => !(ExceptionCode1 > ExceptionCode2);

        #endregion

        #region Operator >  (ExceptionCode1, ExceptionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ExceptionCode1">An exception code.</param>
        /// <param name="ExceptionCode2">Another exception code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (ExceptionCode? ExceptionCode1,
                                          ExceptionCode? ExceptionCode2)
        {

            if (ExceptionCode1 is null)
                throw new ArgumentNullException(nameof(ExceptionCode1), "The given exception code must not be null!");

            return ExceptionCode1.CompareTo(ExceptionCode2) > 0;

        }

        #endregion

        #region Operator >= (ExceptionCode1, ExceptionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ExceptionCode1">An exception code.</param>
        /// <param name="ExceptionCode2">Another exception code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (ExceptionCode? ExceptionCode1,
                                           ExceptionCode? ExceptionCode2)

            => !(ExceptionCode1 < ExceptionCode2);

        #endregion

        #endregion

        #region IComparable<ExceptionCode> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two exception codes.
        /// </summary>
        /// <param name="Object">An exception code to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is ExceptionCode exceptionCode
                   ? CompareTo(exceptionCode)
                   : throw new ArgumentException("The given object is not an exception code!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(ExceptionCode)

        /// <summary>
        /// Compares two exception codes.
        /// </summary>
        /// <param name="ExceptionCode">An exception code to compare with.</param>
        public Int32 CompareTo(ExceptionCode? ExceptionCode)
        {

            if (ExceptionCode is null)
                throw new ArgumentNullException(nameof(ExceptionCode), "The given exception code must not be null!");

            var c = Info. CompareTo(ExceptionCode.Info);

            if (c == 0)
                c = Value.CompareTo(ExceptionCode.Value);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<ExceptionCode> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two exception codes for equality.
        /// </summary>
        /// <param name="Object">An exception code to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is ExceptionCode exceptionCode &&
                   Equals(exceptionCode);

        #endregion

        #region Equals(ExceptionCode)

        /// <summary>
        /// Compares two exception codes for equality.
        /// </summary>
        /// <param name="ExceptionCode">An exception code to compare with.</param>
        public Boolean Equals(ExceptionCode? ExceptionCode)

            => ExceptionCode is not null &&

               Info. Equals(ExceptionCode.Info)  &&
               Value.Equals(ExceptionCode.Value) &&

             ((Description is     null &&  ExceptionCode.Description is     null) ||
              (Description is not null &&  ExceptionCode.Description is not null && Description.Equals(ExceptionCode.Description)));

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return Info.        GetHashCode() * 5 ^
                       Value.       GetHashCode() * 3 ^
                       Description?.GetHashCode() ?? 0;

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   Info,
                   ": ",
                   Value

               );

        #endregion

    }

}
