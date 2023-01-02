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
    /// Each of the function codes has a potentially different body payload
    /// and potentially different parameters to send.
    /// </summary>
    public class FunctionCode : IEquatable<FunctionCode>,
                                IComparable<FunctionCode>,
                                IComparable
    {

        #region Data

        private static readonly Dictionary<Byte, FunctionCode> lookup = new ();

        #endregion

        #region Properties

        /// <summary>
        /// A human readable short information on this function code.
        /// </summary>
        public String       Info           { get; }

        /// <summary>
        /// The numeric value of this function code.
        /// </summary>
        public Byte         Value          { get; }

        /// <summary>
        /// The access right (e.g. read or read/write).
        /// </summary>
        public AccessRight  AccessRight    { get; }

        /// <summary>
        /// The access group, which could e.g. define
        /// the expected type of the return value.
        /// </summary>
        public AccessGroup  AccessGroup    { get; }

        /// <summary>
        /// A description of this function code.
        /// </summary>
        public String?      Description    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new ExceptionCode.
        /// </summary>
        /// <param name="Info">A short information on the function code.</param>
        /// <param name="Value">The numberic value of this function code.</param>
        /// <param name="AccessRight">The access right (e.g. read or readwrite).</param>
        /// <param name="AccessGroup">The access group, which could e.g. define the expected type of the return value.</param>
        /// <param name="Description">A description of this function code.</param>
        public FunctionCode(String       Info,
                            Byte         Value,
                            AccessRight  AccessRight,
                            AccessGroup  AccessGroup,
                            String?      Description   = null)
        {

            this.Info         = Info;
            this.Value        = Value;
            this.AccessRight  = AccessRight;
            this.AccessGroup  = AccessGroup;
            this.Description  = Description;

            if (lookup is not null)
            {
                if (!lookup.ContainsKey(Value))
                    lookup.Add(this.Value, this);
            }

        }

        #endregion


        #region (static) Static definitions

        public static readonly FunctionCode ReadCoils                       = new ("Read coils",                          1, AccessRight.Read,      AccessGroup.Bit,         "Read multiple boolean values");
        public static readonly FunctionCode ReadDiscreteInputs              = new ("Read discrete inputs",                2, AccessRight.Read,      AccessGroup.Bit,         "Single sensor bit");
        public static readonly FunctionCode ReadHoldingRegister             = new ("Read holding register",               3, AccessRight.Read,      AccessGroup.Word,        "16-bit sensor word");
        public static readonly FunctionCode ReadInputRegister               = new ("Read input register",                 4, AccessRight.Read,      AccessGroup.Word,        "16-bit sensor word");

        public static readonly FunctionCode WriteSingleCoil                 = new ("Write single coil",                   5, AccessRight.ReadWrite, AccessGroup.Bit,         "Single actor bit");
        public static readonly FunctionCode WriteSingleRegister             = new ("Write single register",               6, AccessRight.ReadWrite, AccessGroup.Word);
        public static readonly FunctionCode ReadExceptionStatus             = new ("Read Exception Status",               7, AccessRight.Read,      AccessGroup.Diagnostics);
        public static readonly FunctionCode Diagnostic                      = new ("Diagnostic",                          8, AccessRight.ReadWrite, AccessGroup.Diagnostics);

        public static readonly FunctionCode GetComEventCounter              = new ("Get Com Event Counter",              11, AccessRight.Read,      AccessGroup.Diagnostics);
        public static readonly FunctionCode GetComEventLog                  = new ("Get Com Event Log",                  12, AccessRight.Read,      AccessGroup.Diagnostics);

        public static readonly FunctionCode WriteMultipleCoils              = new ("Write multiple coils",               15, AccessRight.ReadWrite, AccessGroup.Bit);
        public static readonly FunctionCode WriteMultipleRegister           = new ("Write multiple registers",           16, AccessRight.ReadWrite, AccessGroup.Word);
        public static readonly FunctionCode ReportSlaveID                   = new ("Report Slave ID",                    17, AccessRight.Read,      AccessGroup.Diagnostics);

        public static readonly FunctionCode ReadFileRecord                  = new ("Read File Record",                   20, AccessRight.Read,      AccessGroup.File);
        public static readonly FunctionCode WriteFileRecord                 = new ("Write File Record",                  21, AccessRight.ReadWrite, AccessGroup.File);
        public static readonly FunctionCode MaskWriteRegister               = new ("Mask Write Register",                22, AccessRight.ReadWrite, AccessGroup.Word);
        public static readonly FunctionCode ReadWriteMultipleRegister       = new ("Write and write multiple registers", 23, AccessRight.ReadWrite, AccessGroup.Word);
        public static readonly FunctionCode ReadFIFOQueue                   = new ("Read FIFO Queue",                    24, AccessRight.ReadWrite, AccessGroup.Word);

        public static readonly FunctionCode EncapsulatedInterfaceTransport  = new ("Encapsulated Interface Transport",   43, AccessRight.Read,      AccessGroup.Diagnostics);

        #endregion

        #region (static) TryParseValue(Value)

        public static FunctionCode? TryParseValue(Byte Value)
        {

            if (lookup.TryGetValue(Value, out var functionCode))
                return functionCode;

            return null;

        }

        #endregion


        #region Operator overloading

        #region Operator == (FunctionCode1, FunctionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FunctionCode1">A function code.</param>
        /// <param name="FunctionCode2">Another function code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (FunctionCode? FunctionCode1,
                                           FunctionCode? FunctionCode2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(FunctionCode1, FunctionCode2))
                return true;

            // If one is null, but not both, return false.
            if (FunctionCode1 is null || FunctionCode2 is null)
                return false;

            return FunctionCode1.Equals(FunctionCode2);

        }

        #endregion

        #region Operator != (FunctionCode1, FunctionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FunctionCode1">A function code.</param>
        /// <param name="FunctionCode2">Another function code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (FunctionCode? FunctionCode1,
                                           FunctionCode? FunctionCode2)
        {
            return !(FunctionCode1 == FunctionCode2);
        }

        #endregion

        #region Operator <  (FunctionCode1, FunctionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FunctionCode1">A function code code.</param>
        /// <param name="FunctionCode2">Another function code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (FunctionCode? FunctionCode1,
                                          FunctionCode? FunctionCode2)
        {

            if (FunctionCode1 is null)
                throw new ArgumentNullException(nameof(FunctionCode1), "The given function code must not be null!");

            return FunctionCode1.CompareTo(FunctionCode2) < 0;

        }

        #endregion

        #region Operator <= (FunctionCode1, FunctionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FunctionCode1">A function code code.</param>
        /// <param name="FunctionCode2">Another function code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (FunctionCode? FunctionCode1,
                                           FunctionCode? FunctionCode2)

            => !(FunctionCode1 > FunctionCode2);

        #endregion

        #region Operator >  (FunctionCode1, FunctionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FunctionCode1">A function code code.</param>
        /// <param name="FunctionCode2">Another function code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (FunctionCode? FunctionCode1,
                                          FunctionCode? FunctionCode2)
        {

            if (FunctionCode1 is null)
                throw new ArgumentNullException(nameof(FunctionCode1), "The given function code must not be null!");

            return FunctionCode1.CompareTo(FunctionCode2) > 0;

        }

        #endregion

        #region Operator >= (FunctionCode1, FunctionCode2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FunctionCode1">A function code code.</param>
        /// <param name="FunctionCode2">Another function code.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (FunctionCode? FunctionCode1,
                                           FunctionCode? FunctionCode2)

            => !(FunctionCode1 < FunctionCode2);

        #endregion

        #endregion

        #region IComparable<FunctionCode> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two function codes.
        /// </summary>
        /// <param name="Object">A function code code to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is FunctionCode functionCode
                   ? CompareTo(functionCode)
                   : throw new ArgumentException("The given object is not a function code!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(FunctionCode)

        /// <summary>
        /// Compares two function codes.
        /// </summary>
        /// <param name="FunctionCode">A function code code to compare with.</param>
        public Int32 CompareTo(FunctionCode? FunctionCode)
        {

            if (FunctionCode is null)
                throw new ArgumentNullException(nameof(FunctionCode), "The given function code must not be null!");

            var c = Info.       CompareTo(FunctionCode.Info);

            if (c == 0)
                c = Value.      CompareTo(FunctionCode.Value);

            if (c == 0)
                c = AccessRight.CompareTo(FunctionCode.AccessRight);

            if (c == 0)
                c = AccessGroup.CompareTo(FunctionCode.AccessGroup);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<FunctionCode> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two function codes for equality.
        /// </summary>
        /// <param name="Object">A function code code to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is FunctionCode functionCode &&
                   Equals(functionCode);

        #endregion

        #region Equals(FunctionCode)

        /// <summary>
        /// Compares two function codes for equality.
        /// </summary>
        /// <param name="FunctionCode">A function code code to compare with.</param>
        public Boolean Equals(FunctionCode? FunctionCode)

            => FunctionCode is not null &&

               Info.       Equals(FunctionCode.Info)        &&
               Value.      Equals(FunctionCode.Value)       &&
               AccessRight.Equals(FunctionCode.AccessRight) &&
               AccessGroup.Equals(FunctionCode.AccessGroup) &&

             ((Description is     null &&  FunctionCode.Description is     null) ||
              (Description is not null &&  FunctionCode.Description is not null && Description.Equals(FunctionCode.Description)));

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

                return Info.        GetHashCode() * 11 ^
                       Value.       GetHashCode() *  7 ^
                       AccessRight. GetHashCode() *  5 ^
                       AccessGroup. GetHashCode() *  3 ^
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
