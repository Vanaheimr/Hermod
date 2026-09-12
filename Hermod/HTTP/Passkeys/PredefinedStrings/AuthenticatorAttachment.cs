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

    // https://w3c.github.io/webauthn/#enumdef-authenticatorattachment

    /// <summary>
    /// Extension methods for AuthenticatorAttachments.
    /// </summary>
    public static class AuthenticatorAttachmentExtensions
    {

        /// <summary>
        /// Indicates whether this AuthenticatorAttachment is null or empty.
        /// </summary>
        /// <param name="AuthenticatorAttachment">A AuthenticatorAttachment.</param>
        public static Boolean IsNullOrEmpty(this AuthenticatorAttachment? AuthenticatorAttachment)
            => !AuthenticatorAttachment.HasValue || AuthenticatorAttachment.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this AuthenticatorAttachment is null or empty.
        /// </summary>
        /// <param name="AuthenticatorAttachment">A AuthenticatorAttachment.</param>
        public static Boolean IsNotNullOrEmpty(this AuthenticatorAttachment? AuthenticatorAttachment)
            => AuthenticatorAttachment.HasValue && AuthenticatorAttachment.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A AuthenticatorAttachment.
    /// </summary>
    public readonly struct AuthenticatorAttachment : IId,
                                                     IEquatable<AuthenticatorAttachment>,
                                                     IComparable<AuthenticatorAttachment>
    {

        #region Data

        private readonly static Dictionary<String, AuthenticatorAttachment>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                       InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this AuthenticatorAttachment is null or empty.
        /// </summary>
        public readonly  Boolean                    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this AuthenticatorAttachment is NOT null or empty.
        /// </summary>
        public readonly  Boolean                    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the AuthenticatorAttachment.
        /// </summary>
        public readonly  UInt64                     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// All registered AuthenticatorAttachments.
        /// </summary>
        public static    IEnumerable<AuthenticatorAttachment>  All
            => lookup.Values;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new AuthenticatorAttachment based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a AuthenticatorAttachment.</param>
        private AuthenticatorAttachment(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static AuthenticatorAttachment Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new AuthenticatorAttachment(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a AuthenticatorAttachment.
        /// </summary>
        /// <param name="Text">A text representation of a AuthenticatorAttachment.</param>
        public static AuthenticatorAttachment Parse(String Text)
        {

            if (TryParse(Text, out var authenticatorAttachment))
                return authenticatorAttachment;

            throw new ArgumentException($"Invalid text representation of a AuthenticatorAttachment: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a AuthenticatorAttachment.
        /// </summary>
        /// <param name="Text">A text representation of a AuthenticatorAttachment.</param>
        public static AuthenticatorAttachment? TryParse(String Text)
        {

            if (TryParse(Text, out var authenticatorAttachment))
                return authenticatorAttachment;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out AuthenticatorAttachment)

        /// <summary>
        /// Try to parse the given text as a AuthenticatorAttachment.
        /// </summary>
        /// <param name="Text">A text representation of a AuthenticatorAttachment.</param>
        /// <param name="AuthenticatorAttachment">The parsed AuthenticatorAttachment.</param>
        public static Boolean TryParse(String Text, out AuthenticatorAttachment AuthenticatorAttachment)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out AuthenticatorAttachment))
                    AuthenticatorAttachment = Register(Text);

                return true;

            }

            AuthenticatorAttachment = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this AuthenticatorAttachment.
        /// </summary>
        public AuthenticatorAttachment Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// platform
        /// </summary>
        public static AuthenticatorAttachment  Platform         { get; }
            = Register("platform");

        /// <summary>
        /// cross-platform
        /// </summary>
        public static AuthenticatorAttachment  CrossPlatform    { get; }
            = Register("cross-platform");

        #endregion


        #region Operator overloading

        #region Operator == (AuthenticatorAttachment1, AuthenticatorAttachment2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorAttachment1">A AuthenticatorAttachment.</param>
        /// <param name="AuthenticatorAttachment2">Another AuthenticatorAttachment.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (AuthenticatorAttachment AuthenticatorAttachment1,
                                           AuthenticatorAttachment AuthenticatorAttachment2)

            => AuthenticatorAttachment1.Equals(AuthenticatorAttachment2);

        #endregion

        #region Operator != (AuthenticatorAttachment1, AuthenticatorAttachment2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorAttachment1">A AuthenticatorAttachment.</param>
        /// <param name="AuthenticatorAttachment2">Another AuthenticatorAttachment.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (AuthenticatorAttachment AuthenticatorAttachment1,
                                           AuthenticatorAttachment AuthenticatorAttachment2)

            => !AuthenticatorAttachment1.Equals(AuthenticatorAttachment2);

        #endregion

        #region Operator <  (AuthenticatorAttachment1, AuthenticatorAttachment2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorAttachment1">A AuthenticatorAttachment.</param>
        /// <param name="AuthenticatorAttachment2">Another AuthenticatorAttachment.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (AuthenticatorAttachment AuthenticatorAttachment1,
                                          AuthenticatorAttachment AuthenticatorAttachment2)

            => AuthenticatorAttachment1.CompareTo(AuthenticatorAttachment2) < 0;

        #endregion

        #region Operator <= (AuthenticatorAttachment1, AuthenticatorAttachment2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorAttachment1">A AuthenticatorAttachment.</param>
        /// <param name="AuthenticatorAttachment2">Another AuthenticatorAttachment.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (AuthenticatorAttachment AuthenticatorAttachment1,
                                           AuthenticatorAttachment AuthenticatorAttachment2)

            => AuthenticatorAttachment1.CompareTo(AuthenticatorAttachment2) <= 0;

        #endregion

        #region Operator >  (AuthenticatorAttachment1, AuthenticatorAttachment2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorAttachment1">A AuthenticatorAttachment.</param>
        /// <param name="AuthenticatorAttachment2">Another AuthenticatorAttachment.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (AuthenticatorAttachment AuthenticatorAttachment1,
                                          AuthenticatorAttachment AuthenticatorAttachment2)

            => AuthenticatorAttachment1.CompareTo(AuthenticatorAttachment2) > 0;

        #endregion

        #region Operator >= (AuthenticatorAttachment1, AuthenticatorAttachment2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AuthenticatorAttachment1">A AuthenticatorAttachment.</param>
        /// <param name="AuthenticatorAttachment2">Another AuthenticatorAttachment.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (AuthenticatorAttachment AuthenticatorAttachment1,
                                           AuthenticatorAttachment AuthenticatorAttachment2)

            => AuthenticatorAttachment1.CompareTo(AuthenticatorAttachment2) >= 0;

        #endregion

        #endregion

        #region IComparable<AuthenticatorAttachment> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two AuthenticatorAttachments.
        /// </summary>
        /// <param name="Object">A AuthenticatorAttachment to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is AuthenticatorAttachment authenticatorAttachment
                   ? CompareTo(authenticatorAttachment)
                   : throw new ArgumentException("The given object is not a AuthenticatorAttachment!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(AuthenticatorAttachment)

        /// <summary>
        /// Compares two AuthenticatorAttachments.
        /// </summary>
        /// <param name="AuthenticatorAttachment">A AuthenticatorAttachment to compare with.</param>
        public Int32 CompareTo(AuthenticatorAttachment AuthenticatorAttachment)

            => String.Compare(InternalId,
                              AuthenticatorAttachment.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<AuthenticatorAttachment> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two AuthenticatorAttachments for equality.
        /// </summary>
        /// <param name="Object">A AuthenticatorAttachment to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is AuthenticatorAttachment authenticatorAttachment &&
                   Equals(authenticatorAttachment);

        #endregion

        #region Equals(AuthenticatorAttachment)

        /// <summary>
        /// Compares two AuthenticatorAttachments for equality.
        /// </summary>
        /// <param name="AuthenticatorAttachment">A AuthenticatorAttachment to compare with.</param>
        public Boolean Equals(AuthenticatorAttachment AuthenticatorAttachment)

            => String.Equals(InternalId,
                             AuthenticatorAttachment.InternalId,
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
