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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// HTTP Keep-Alive response header property.
    /// </summary>
    public class KeepAliveType : IEquatable<KeepAliveType>,
                                 IComparable<KeepAliveType>,
                                 IComparable
    {

        #region Properties

        /// <summary>
        /// The timeout till the next request.
        /// </summary>
        public TimeSpan? Timeout                { get; private set; }

        /// <summary>
        /// The maximum number of requests within this connection.
        /// </summary>
        public UInt32?   MaxNumberOfRequests    { get; private set; }

        #endregion

        #region Constructor(s)

        #region KeepAliveType(Timeout = null, MaxNumberOfRequests = null)

        public KeepAliveType(TimeSpan? Timeout              = null,
                             UInt32?   MaxNumberOfRequests  = null)
        {
            this.Timeout              = Timeout;
            this.MaxNumberOfRequests  = MaxNumberOfRequests;
        }

        #endregion

        #region KeepAliveType(KeepAliveString)

        /// <summary>
        /// Parse the string representation of a HTTP accept header field.
        /// </summary>
        /// <param name="AcceptString"></param>
        public KeepAliveType(String KeepAliveString)
        {

            UInt32 _Value;
            Char[] _Equals  = new Char[1] { '=' };

            // "timeout=10, max=5"
            foreach (var Token in KeepAliveString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()))
            {

                var Property = Token.Split(_Equals, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToArray();

                if (Property.Length == 2)
                    if (Property[0] == "timeout")
                        if (UInt32.TryParse(Property[1], out _Value))
                        {
                            this.Timeout = TimeSpan.FromSeconds(_Value);
                            continue;
                        }

                if (Property.Length == 2)
                    if (Property[0] == "max")
                        if (UInt32.TryParse(Property[1], out _Value))
                        {
                            this.MaxNumberOfRequests = _Value;
                            continue;
                        }

                throw new ArgumentException("Invalid KeepAliv string!", "KeepAliveString");

            }

        }

        #endregion

        #endregion


        #region TryParse(KeepAliveString, KeepAliveType)

        /// <summary>
        /// Parse the string representation of a HTTP accept header field.
        /// </summary>
        /// <param name="AcceptString"></param>
        public static Boolean TryParse(String KeepAliveString, out KeepAliveType? KeepAliveType)
        {

            UInt32 _Value;
            Char[] equals  = new[] { '=' };

            TimeSpan? timeout               = null;
            UInt32?   maxNumberOfRequests   = null;

            KeepAliveType = null;

            // "timeout=10, max=5"
            foreach (var token in KeepAliveString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).
                                                  Select(v => v.Trim()))
            {

                var property = token.Split (equals, StringSplitOptions.RemoveEmptyEntries).
                                     Select(v => v.Trim()).ToArray();

                if (property.Length == 2)
                    if (property[0] == "timeout")
                        if (UInt32.TryParse(property[1], out _Value))
                        {
                            timeout = TimeSpan.FromSeconds(_Value);
                            continue;
                        }

                if (property.Length == 2)
                    if (property[0] == "max")
                        if (UInt32.TryParse(property[1], out _Value))
                        {
                            maxNumberOfRequests = _Value;
                            continue;
                        }

                return false;

            }

            KeepAliveType = new KeepAliveType(timeout,
                                              maxNumberOfRequests);

            return true;

        }

        #endregion


        #region IComparable<KeepAliveType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP Keep Alives.
        /// </summary>
        /// <param name="Object">A HTTP Keep Alive to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is KeepAliveType keepAliveType
                   ? CompareTo(keepAliveType)
                   : throw new ArgumentException("The given object is not a HTTP Keep Alive!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(KeepAliveType)

        /// <summary>
        /// Compares two HTTP Keep Alives.
        /// </summary>
        /// <param name="KeepAliveType">A HTTP Keep Alive to compare with.</param>
        public Int32 CompareTo(KeepAliveType? KeepAliveType)
        {

            if (KeepAliveType is null)
                throw new ArgumentNullException(nameof(KeepAliveType),
                                                "The given KeepAliveType must not be null!");

            var c = -1;

            if (MaxNumberOfRequests.HasValue && KeepAliveType.MaxNumberOfRequests.HasValue)
                MaxNumberOfRequests.Value.CompareTo(KeepAliveType.MaxNumberOfRequests.Value);

            if (c == 0 && Timeout.HasValue && KeepAliveType.Timeout.HasValue)
                c = Timeout.Value.CompareTo(KeepAliveType.Timeout.Value);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<KeepAliveType> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP Keep Alives for equality.
        /// </summary>
        /// <param name="Object">A HTTP Keep Alive to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is KeepAliveType keepAliveType &&
                   Equals(keepAliveType);

        #endregion

        #region Equals(KeepAliveType)

        /// <summary>
        /// Compares two HTTP Keep Alives for equality.
        /// </summary>
        /// <param name="KeepAliveType">A HTTP Keep Alive to compare with.</param>
        public Boolean Equals(KeepAliveType? KeepAliveType)

            => KeepAliveType is not null &&

            ((!Timeout.            HasValue && !KeepAliveType.Timeout.            HasValue) ||
              (Timeout.            HasValue &&  KeepAliveType.Timeout.            HasValue && Timeout.            Value.Equals(KeepAliveType.Timeout.            Value))) &&

            ((!MaxNumberOfRequests.HasValue && !KeepAliveType.MaxNumberOfRequests.HasValue) ||
              (MaxNumberOfRequests.HasValue &&  KeepAliveType.MaxNumberOfRequests.HasValue && MaxNumberOfRequests.Value.Equals(KeepAliveType.MaxNumberOfRequests.Value)));

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return (Timeout?.            GetHashCode() ?? 0) * 3 ^
                       (MaxNumberOfRequests?.GetHashCode() ?? 0);

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   Timeout.HasValue
                       ? "timeout=" + ((UInt32) Timeout.Value.TotalSeconds)
                       : "",

                   MaxNumberOfRequests.HasValue
                       ? "max="     + MaxNumberOfRequests.Value
                       : ""

               );

        #endregion

    }

}
