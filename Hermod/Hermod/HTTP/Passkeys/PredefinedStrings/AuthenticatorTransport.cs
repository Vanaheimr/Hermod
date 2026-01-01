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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    // https://w3c.github.io/webauthn/#enum-transport

    /// <summary>
    /// Extension methods for AuthenticatorTransports.
    /// </summary>
    public static class AuthenticatorTransportExtensions
    {

        /// <summary>
        /// Indicates whether this AuthenticatorTransport is null or empty.
        /// </summary>
        /// <param name="AuthenticatorTransport">An AuthenticatorTransport.</param>
        public static Boolean IsNullOrEmpty(this AuthenticatorTransport? AuthenticatorTransport)
            => !AuthenticatorTransport.HasValue || AuthenticatorTransport.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this AuthenticatorTransport is null or empty.
        /// </summary>
        /// <param name="AuthenticatorTransport">An AuthenticatorTransport.</param>
        public static Boolean IsNotNullOrEmpty(this AuthenticatorTransport? AuthenticatorTransport)
            => AuthenticatorTransport.HasValue && AuthenticatorTransport.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// An AuthenticatorTransport.
    /// </summary>
    public readonly struct AuthenticatorTransport : IId,
                                                     IEquatable<AuthenticatorTransport>,
                                                     IComparable<AuthenticatorTransport>
    {

        #region Data

        private readonly static Dictionary<String, AuthenticatorTransport>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                       InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this AuthenticatorTransport is null or empty.
        /// </summary>
        public readonly  Boolean                    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this AuthenticatorTransport is NOT null or empty.
        /// </summary>
        public readonly  Boolean                    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the AuthenticatorTransport.
        /// </summary>
        public readonly  UInt64                     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// All registered AuthenticatorTransports.
        /// </summary>
        public static    IEnumerable<AuthenticatorTransport>  All
            => lookup.Values;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new AuthenticatorTransport based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of an AuthenticatorTransport.</param>
        private AuthenticatorTransport(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static AuthenticatorTransport Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new AuthenticatorTransport(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an AuthenticatorTransport.
        /// </summary>
        /// <param name="Text">A text representation of an AuthenticatorTransport.</param>
        public static AuthenticatorTransport Parse(String Text)
        {

            if (TryParse(Text, out var authenticatorTransport))
                return authenticatorTransport;

            throw new ArgumentException($"Invalid text representation of an AuthenticatorTransport: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as an AuthenticatorTransport.
        /// </summary>
        /// <param name="Text">A text representation of an AuthenticatorTransport.</param>
        public static AuthenticatorTransport? TryParse(String Text)
        {

            if (TryParse(Text, out var authenticatorTransport))
                return authenticatorTransport;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out AuthenticatorTransport)

        /// <summary>
        /// Try to parse the given text as an AuthenticatorTransport.
        /// </summary>
        /// <param name="Text">A text representation of an AuthenticatorTransport.</param>
        /// <param name="AuthenticatorTransport">The parsed AuthenticatorTransport.</param>
        public static Boolean TryParse(String Text, out AuthenticatorTransport AuthenticatorTransport)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out AuthenticatorTransport))
                    AuthenticatorTransport = Register(Text);

                return true;

            }

            AuthenticatorTransport = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this AuthenticatorTransport.
        /// </summary>
        public AuthenticatorTransport Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// Universal Serial Bus (USB)
        /// </summary>
        public static AuthenticatorTransport  USB          { get; }
            = Register("usb");

        /// <summary>
        /// Near-Field Communication (NFC)
        /// </summary>
        public static AuthenticatorTransport  NFC          { get; }
            = Register("nfc");

        /// <summary>
        /// Bluetooth Low Energy (BLE)
        /// </summary>
        public static AuthenticatorTransport  BLE          { get; }
            = Register("ble");

        /// <summary>
        /// ISO/IEC 7816 Smart Card with contacts
        /// </summary>
        public static AuthenticatorTransport  SmartCard    { get; }
            = Register("smart-card");

        /// <summary>
        /// Hybrid, e.g. authentication on a desktop computer using a smartphone.
        /// </summary>
        public static AuthenticatorTransport  Hybrid       { get; }
            = Register("hybrid");

        /// <summary>
        /// Internal authenticators, not removable from the client device.
        /// </summary>
        public static AuthenticatorTransport  Internal     { get; }
            = Register("internal");

        #endregion


        #region Operator overloading

        #region Operator == (AuthenticatorTransport1, AuthenticatorTransport2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorTransport1">An AuthenticatorTransport.</param>
        /// <param name="AuthenticatorTransport2">Another AuthenticatorTransport.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (AuthenticatorTransport AuthenticatorTransport1,
                                           AuthenticatorTransport AuthenticatorTransport2)

            => AuthenticatorTransport1.Equals(AuthenticatorTransport2);

        #endregion

        #region Operator != (AuthenticatorTransport1, AuthenticatorTransport2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorTransport1">An AuthenticatorTransport.</param>
        /// <param name="AuthenticatorTransport2">Another AuthenticatorTransport.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (AuthenticatorTransport AuthenticatorTransport1,
                                           AuthenticatorTransport AuthenticatorTransport2)

            => !AuthenticatorTransport1.Equals(AuthenticatorTransport2);

        #endregion

        #region Operator <  (AuthenticatorTransport1, AuthenticatorTransport2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorTransport1">An AuthenticatorTransport.</param>
        /// <param name="AuthenticatorTransport2">Another AuthenticatorTransport.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (AuthenticatorTransport AuthenticatorTransport1,
                                          AuthenticatorTransport AuthenticatorTransport2)

            => AuthenticatorTransport1.CompareTo(AuthenticatorTransport2) < 0;

        #endregion

        #region Operator <= (AuthenticatorTransport1, AuthenticatorTransport2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorTransport1">An AuthenticatorTransport.</param>
        /// <param name="AuthenticatorTransport2">Another AuthenticatorTransport.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (AuthenticatorTransport AuthenticatorTransport1,
                                           AuthenticatorTransport AuthenticatorTransport2)

            => AuthenticatorTransport1.CompareTo(AuthenticatorTransport2) <= 0;

        #endregion

        #region Operator >  (AuthenticatorTransport1, AuthenticatorTransport2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorTransport1">An AuthenticatorTransport.</param>
        /// <param name="AuthenticatorTransport2">Another AuthenticatorTransport.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (AuthenticatorTransport AuthenticatorTransport1,
                                          AuthenticatorTransport AuthenticatorTransport2)

            => AuthenticatorTransport1.CompareTo(AuthenticatorTransport2) > 0;

        #endregion

        #region Operator >= (AuthenticatorTransport1, AuthenticatorTransport2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorTransport1">An AuthenticatorTransport.</param>
        /// <param name="AuthenticatorTransport2">Another AuthenticatorTransport.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (AuthenticatorTransport AuthenticatorTransport1,
                                           AuthenticatorTransport AuthenticatorTransport2)

            => AuthenticatorTransport1.CompareTo(AuthenticatorTransport2) >= 0;

        #endregion

        #endregion

        #region IComparable<AuthenticatorTransport> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two AuthenticatorTransports.
        /// </summary>
        /// <param name="Object">An AuthenticatorTransport to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is AuthenticatorTransport authenticatorTransport
                   ? CompareTo(authenticatorTransport)
                   : throw new ArgumentException("The given object is not an AuthenticatorTransport!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(AuthenticatorTransport)

        /// <summary>
        /// Compares two AuthenticatorTransports.
        /// </summary>
        /// <param name="AuthenticatorTransport">An AuthenticatorTransport to compare with.</param>
        public Int32 CompareTo(AuthenticatorTransport AuthenticatorTransport)

            => String.Compare(InternalId,
                              AuthenticatorTransport.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<AuthenticatorTransport> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two AuthenticatorTransports for equality.
        /// </summary>
        /// <param name="Object">An AuthenticatorTransport to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is AuthenticatorTransport authenticatorTransport &&
                   Equals(authenticatorTransport);

        #endregion

        #region Equals(AuthenticatorTransport)

        /// <summary>
        /// Compares two AuthenticatorTransports for equality.
        /// </summary>
        /// <param name="AuthenticatorTransport">An AuthenticatorTransport to compare with.</param>
        public Boolean Equals(AuthenticatorTransport AuthenticatorTransport)

            => String.Equals(InternalId,
                             AuthenticatorTransport.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => InternalId?.ToLower().GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
