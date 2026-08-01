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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The URI scheme.
    /// https://datatracker.ietf.org/doc/html/rfc3986#section-3.1
    /// https://www.iana.org/assignments/uri-schemes/uri-schemes.xhtml
    /// </summary>
    public sealed class URIScheme : IEquatable<URIScheme>,
                                    IComparable<URIScheme>,
                                    IComparable
    {

        #region Data

        /// <summary>
        /// The registry of all well-known and explicitly declared URI schemes.
        ///
        /// Note: This registry is only ever grown by code explicitly calling Register(...).
        ///       Parse(...) and TryParse(...) never add to it, therefore a remote peer can
        ///       not grow it without any bound, no matter what it sends over the wire.
        ///
        ///       Unknown URI schemes are still parsed successfully. As equality, hash code
        ///       and comparison are all based on the scheme name, such a transient URI
        ///       scheme is fully interchangeable with a registered one.
        /// </summary>
        private readonly static ConcurrentDictionary<String, URIScheme> lookup = new (StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        /// <summary>
        /// The name of the URI scheme.
        /// </summary>
        public String   SchemeName      { get; }

        /// <summary>
        /// Whether this URI scheme enforces Transport Layer Security (TLS) or not.
        /// </summary>
        public Boolean  EnforcesTLS     { get; }

        /// <summary>
        /// The optional default port of this URI scheme.
        /// </summary>
        public IPPort?  DefaultPort    { get; }

        /// <summary>
        /// The optional description of this URI scheme.
        /// </summary>
        public String?  Description     { get; }

        /// <summary>
        /// The name of this URI scheme followed by "://", e.g. "https://".
        /// </summary>
        public String   Prefix          { get; }


        /// <summary>
        /// Indicates whether this URI scheme is null or empty.
        /// </summary>
        public Boolean  IsNullOrEmpty
            => SchemeName.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this URI scheme is NOT null or empty.
        /// </summary>
        public Boolean  IsNotNullOrEmpty
            => SchemeName.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the URI scheme.
        /// </summary>
        public UInt64   Length
            => (UInt64) (SchemeName?.Length ?? 0);


        /// <summary>
        /// The number of currently registered URI schemes.
        /// </summary>
        public static Int32 RegisteredCount
            => lookup.Count;

        #endregion

        #region (private) Constructor(s)

        /// <summary>
        /// Creates a new URI scheme based on the given parameters.
        /// </summary>
        /// <param name="SchemeName">The name of the URI scheme.</param>
        /// <param name="EnforcesTLS">Whether this URI scheme enforces Transport Layer Security (TLS) or not.</param>
        /// <param name="DefaultPort">The optional default port of this URI scheme.</param>
        /// <param name="Description">An optional description of this URI scheme.</param>
        private URIScheme(String   SchemeName,
                          Boolean  EnforcesTLS   = false,
                          IPPort?  DefaultPort   = null,
                          String?  Description   = null)
        {

            this.SchemeName   = SchemeName;
            this.EnforcesTLS  = EnforcesTLS;
            this.DefaultPort  = DefaultPort;
            this.Description  = Description;
            this.Prefix       = $"{SchemeName}://";

        }

        #endregion


        #region (private static) IsValidSchemeName(SchemeName)

        /// <summary>
        /// Whether the given text is a syntactically valid URI scheme name.
        /// https://datatracker.ietf.org/doc/html/rfc3986#section-3.1
        /// scheme = ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )
        /// </summary>
        /// <param name="SchemeName">An URI scheme name.</param>
        private static Boolean IsValidSchemeName([NotNullWhen(true)] String? SchemeName)
        {

            if (SchemeName is null || SchemeName.Length == 0)
                return false;

            if (!Char.IsAsciiLetter(SchemeName[0]))
                return false;

            for (var i = 1; i < SchemeName.Length; i++)
            {

                var character = SchemeName[i];

                if (!Char.IsAsciiLetterOrDigit(character) &&
                    character != '+'                      &&
                    character != '-'                      &&
                    character != '.')
                {
                    return false;
                }

            }

            return true;

        }

        #endregion

        #region (static) Register(SchemeName, EnforcesTLS = false, DefaultPort = null, Description = null)

        /// <summary>
        /// Register a new URI scheme with the given parameters.
        ///
        /// Note: This is the only way to grow the registry of well-known URI schemes and is
        ///       meant for code declaring its own schemes. Never call it with data received
        ///       over the wire, use Parse(...)/TryParse(...) for that!
        ///
        ///       When the given scheme is already registered, the already registered scheme
        ///       is returned unchanged and the other parameters are ignored.
        /// </summary>
        /// <param name="SchemeName">An URI scheme name.</param>
        /// <param name="EnforcesTLS">Whether this URI scheme enforces Transport Layer Security (TLS) or not.</param>
        /// <param name="DefaultPort">The optional default port of this URI scheme.</param>
        /// <param name="Description">An optional description of this URI scheme.</param>
        public static URIScheme Register(String   SchemeName,
                                         Boolean  EnforcesTLS   = false,
                                         IPPort?  DefaultPort   = null,
                                         String?  Description   = null)
        {

            if (!IsValidSchemeName(SchemeName))
                throw new ArgumentException($"Invalid URI scheme name: '{SchemeName}'!",
                                            nameof(SchemeName));

            return lookup.GetOrAdd(
                       SchemeName.ToLowerInvariant(),
                       static (schemeName, parameters) => new URIScheme(
                                                              schemeName,
                                                              parameters.EnforcesTLS,
                                                              parameters.DefaultPort,
                                                              parameters.Description
                                                          ),
                       (EnforcesTLS, DefaultPort, Description)
                   );

        }

        #endregion


        #region Well-known URI schemes

        /// <summary>
        /// Transmission Control Protocol (TCP)
        /// </summary>
        public static URIScheme  tcp        { get; }

            = Register(
                  "tcp",
                  Description:  "Transmission Control Protocol (TCP)"
              );


        /// <summary>
        /// Transport Layer Security (TLS)
        /// </summary>
        public static URIScheme  tls        { get; }

            = Register(
                  "tls",
                  EnforcesTLS:   true,
                  Description:  "Transport Layer Security (TLS)"
              );


        /// <summary>
        /// Modbus/TCP
        /// </summary>
        public static URIScheme  modbus     { get; }

            = Register(
                  "modbus",
                  EnforcesTLS:   false,
                  DefaultPort:   IPPort.ModbusTCP,
                  Description:  "Modbus/TCP"
              );


        /// <summary>
        /// Modbus/TLS (Modbus/TCP Security Protocol Specification)
        /// </summary>
        public static URIScheme  smodbus    { get; }

            = Register(
                  "smodbus",
                  EnforcesTLS:   true,
                  DefaultPort:   IPPort.ModbusTLS,
                  Description:  "Modbus/TLS (Modbus/TCP Security Protocol Specification)"
              );

        #endregion

        #region IANA-registered URI schemes

        // https://www.iana.org/assignments/uri-schemes/uri-schemes.xhtml

        /// <summary>
        /// User Datagram Protocol (UDP)
        /// </summary>
        public static URIScheme  udp        { get; }

            = Register(
                  "udp",
                  Description:  "User Datagram Protocol (UDP)"
              );


        /// <summary>
        /// Hypertext Transfer Protocol (HTTP)
        /// </summary>
        public static URIScheme  http       { get; }

            = Register(
                  "http",
                  EnforcesTLS:   false,
                  DefaultPort:   IPPort.HTTP,
                  Description:  "Hypertext Transfer Protocol (HTTP)"
              );


        /// <summary>
        /// Hypertext Transfer Protocol Secure (HTTPS)
        /// </summary>
        public static URIScheme  https      { get; }

            = Register(
                  "https",
                  EnforcesTLS:   true,
                  DefaultPort:   IPPort.HTTPS,
                  Description:  "Hypertext Transfer Protocol Secure (HTTPS)"
              );


        /// <summary>
        /// WebSocket Protocol (WS)
        /// </summary>
        public static URIScheme  ws         { get; }

            = Register(
                  "ws",
                  EnforcesTLS:   false,
                  DefaultPort:   IPPort.HTTP,
                  Description:  "WebSocket Protocol (WS)"
              );


        /// <summary>
        /// WebSocket Secure Protocol (WSS)
        /// </summary>
        public static URIScheme  wss        { get; }

            = Register(
                  "wss",
                  EnforcesTLS:   true,
                  DefaultPort:   IPPort.HTTPS,
                  Description:  "WebSocket Secure Protocol (WSS)"
              );


        /// <summary>
        /// Secure Shell (SSH)
        /// </summary>
        public static URIScheme  ssh        { get; }

            = Register(
                  "ssh",
                  EnforcesTLS:   false,
                  DefaultPort:   IPPort.SSH,
                  Description:  "Secure Shell (SSH)"
              );


        /// <summary>
        /// Secure File Transfer Protocol (SFTP)
        /// </summary>
        public static URIScheme  sftp       { get; }

            = Register(
                  "sftp",
                  EnforcesTLS:   false,
                  DefaultPort:   IPPort.SSH,
                  Description:  "Secure File Transfer Protocol (SFTP)"
              );


        /// <summary>
        /// File Transfer Protocol (FTP)
        /// </summary>
        public static URIScheme  ftp        { get; }

            = Register(
                  "ftp",
                  EnforcesTLS:   false,
                  DefaultPort:   IPPort.FTP,
                  Description:  "File Transfer Protocol (FTP)"
              );


        /// <summary>
        /// File Transfer Protocol Secure (FTPS)
        /// </summary>
        public static URIScheme  ftps       { get; }

            = Register(
                  "ftps",
                  EnforcesTLS:   true,
                  DefaultPort:   IPPort.FTP,
                  Description:  "File Transfer Protocol Secure (FTPS)"
              );


        /// <summary>
        /// Git (GIT)
        /// </summary>
        public static URIScheme  git        { get; }

            = Register(
                  "git",
                  EnforcesTLS:   true,
                  DefaultPort:   IPPort.SSH,
                  Description:  "Git (GIT)"
              );



        /// <summary>
        /// File (Host-specific file names)
        /// </summary>
        public static URIScheme  file       { get; }

            = Register(
                  "file",
                  EnforcesTLS:   false,
                  Description:  "File (Host-specific file names)"
              );



        /// <summary>
        /// Data (inline data)
        /// </summary>
        public static URIScheme  data       { get; }

            = Register(
                  "data",
                  EnforcesTLS:   false,
                  Description:  "Data (inline data)"
              );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a URI scheme.
        ///
        /// Note: This never adds an unknown URI scheme to the registry, use Register(...) for that.
        /// </summary>
        /// <param name="Text">A text representation of a URI scheme.</param>
        public static URIScheme Parse(String? Text)
        {

            if (TryParse(Text, out var urlScheme))
                return urlScheme;

            throw new ArgumentException($"Invalid text representation of a URI scheme: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a URI scheme.
        ///
        /// Note: This never adds an unknown URI scheme to the registry, use Register(...) for that.
        /// </summary>
        /// <param name="Text">A text representation of a URI scheme.</param>
        public static URIScheme? TryParse(String? Text)
        {

            if (TryParse(Text, out var urlScheme))
                return urlScheme;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out Scheme)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given text as a URI scheme.
        ///
        /// Note: This never adds an unknown URI scheme to the registry, use Register(...) for that.
        ///       Therefore this is safe to call with anything received over the wire.
        /// </summary>
        /// <param name="Text">An URI scheme name.</param>
        /// <param name="Scheme">The parsed URI scheme.</param>
        public static Boolean TryParse(String?                             Text,
                                       [NotNullWhen(true)] out URIScheme?  Scheme)
        {

            if (IsValidSchemeName(Text))
            {

                // URI schemes are case-insensitive and normalized to lower case, see RFC 3986 section 3.1.
                var schemeName = Text.ToLowerInvariant();

                if (!lookup.TryGetValue(schemeName, out Scheme))
                    Scheme = new URIScheme(schemeName);

                return true;

            }

            Scheme = null;
            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (Scheme1, Scheme2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Scheme1">An URI scheme.</param>
        /// <param name="Scheme2">Another URI scheme.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (URIScheme? Scheme1,
                                           URIScheme? Scheme2)
        {

            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(Scheme1, Scheme2))
                return true;

            // If one is null, but not both, return false.
            if (Scheme1 is null || Scheme2 is null)
                return false;

            return Scheme1.Equals(Scheme2);

        }

        #endregion

        #region Operator != (Scheme1, Scheme2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Scheme1">An URI scheme.</param>
        /// <param name="Scheme2">Another URI scheme.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (URIScheme? Scheme1,
                                           URIScheme? Scheme2)

            => !(Scheme1 == Scheme2);

        #endregion

        #region Operator <  (Scheme1, Scheme2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Scheme1">An URI scheme.</param>
        /// <param name="Scheme2">Another URI scheme.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (URIScheme? Scheme1,
                                          URIScheme? Scheme2)

            => Compare(Scheme1, Scheme2) < 0;

        #endregion

        #region Operator <= (Scheme1, Scheme2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Scheme1">An URI scheme.</param>
        /// <param name="Scheme2">Another URI scheme.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (URIScheme? Scheme1,
                                           URIScheme? Scheme2)

            => Compare(Scheme1, Scheme2) <= 0;

        #endregion

        #region Operator >  (Scheme1, Scheme2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Scheme1">An URI scheme.</param>
        /// <param name="Scheme2">Another URI scheme.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (URIScheme? Scheme1,
                                          URIScheme? Scheme2)

            => Compare(Scheme1, Scheme2) > 0;

        #endregion

        #region Operator >= (Scheme1, Scheme2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Scheme1">An URI scheme.</param>
        /// <param name="Scheme2">Another URI scheme.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (URIScheme? Scheme1,
                                           URIScheme? Scheme2)

            => Compare(Scheme1, Scheme2) >= 0;

        #endregion

        #endregion

        #region IComparable<URIScheme> Members

        #region (private static) Compare(Scheme1, Scheme2)

        /// <summary>
        /// Compare two URI schemes, treating null as smaller than any URI scheme.
        /// This keeps all comparison operators consistent with CompareTo(null) == 1.
        /// </summary>
        /// <param name="Scheme1">An URI scheme.</param>
        /// <param name="Scheme2">Another URI scheme.</param>
        private static Int32 Compare(URIScheme? Scheme1,
                                     URIScheme? Scheme2)

            => ReferenceEquals(Scheme1, Scheme2)
                   ? 0
                   : Scheme1 is null
                         ? -1
                         : Scheme1.CompareTo(Scheme2);

        #endregion

        #region CompareTo(Object)

        /// <summary>
        /// Compares two URI schemes.
        /// </summary>
        /// <param name="Object">An URI scheme to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is URIScheme urlScheme
                   ? CompareTo(urlScheme)
                   : throw new ArgumentException("The given object is not a URI scheme!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(Scheme)

        /// <summary>
        /// Compares two URI schemes.
        /// </summary>
        /// <param name="Scheme">An URI scheme to compare with.</param>
        public Int32 CompareTo(URIScheme? Scheme)

            => Scheme is null
                   ? 1
                   : StringComparer.OrdinalIgnoreCase.Compare(
                         SchemeName,
                         Scheme.SchemeName
                     );

        #endregion

        #endregion

        #region IEquatable<URIScheme> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two URI schemes for equality.
        /// </summary>
        /// <param name="Object">An URI scheme to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is URIScheme urlScheme &&
                   Equals(urlScheme);

        #endregion

        #region Equals(Scheme)

        /// <summary>
        /// Compares two URI schemes for equality.
        /// </summary>
        /// <param name="Scheme">An URI scheme to compare with.</param>
        public Boolean Equals(URIScheme? Scheme)

            => Scheme is not null &&

               String.Equals(SchemeName,
                             Scheme.SchemeName,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => StringComparer.OrdinalIgnoreCase.GetHashCode(SchemeName);

        #endregion

        #region DebugView()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public String DebugView

            => String.Concat(

                   SchemeName,

                   EnforcesTLS
                       ? " (enforces TLS)"
                       : String.Empty,

                   DefaultPort.HasValue
                       ? $" (default port: {DefaultPort.Value})"
                       : String.Empty,

                   Description.IsNotNullOrEmpty()
                       ? $": '{Description}'"
                       : String.Empty

               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => SchemeName;

        #endregion

    }

}
