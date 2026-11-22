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
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The host of an URL: a domain name, an IPv4 address or an IPv6 address.
    ///
    /// RFC 3986 section 3.2.2: host = IP-literal / IPv4address / reg-name
    ///
    /// This is a C# union of exactly these three: a switch over an URLHost that forgets
    /// one of them is a compiler warning (CS8509), and each of them converts implicitly
    /// into an URLHost. It is written out by hand rather than as "union URLHost(...)",
    /// as the compiler's own union keeps its value as an object, and so would box every
    /// IPv4 and IPv6 address; matching this one allocates nothing.
    ///
    /// This is deliberately NOT a HTTPHostname: a HTTPHostname carries an optional TCP/IP
    /// port and allows the "*" wildcard, as it also models the server-side vHost matching.
    /// An URL keeps its port separately and can never have a wildcard host, so both states
    /// would be unrepresentable-but-possible.
    /// </summary>
    [Union]
    public readonly struct URLHost : IEquatable<URLHost>,
                                     IComparable<URLHost>,
                                     IComparable
    {

        #region (enum) Kinds

        private enum Kinds : Byte
        {
            None,
            DomainName,
            IPv4,
            IPv6

        }

        #endregion


        #region Data

        // Exactly the field of the kind is set; none for default(URLHost).
        private readonly Kinds        kind;
        private readonly DomainName?  domainName;
        private readonly IPv4Address  ipv4Address;
        private readonly IPv6Address  ipv6Address;

        #endregion

        #region Properties

        /// <summary>
        /// The domain name, the IPv4 address or the IPv6 address of this host,
        /// or null for default(URLHost).
        /// </summary>
        public Object?       Value
            => kind switch {
                   Kinds.DomainName  => domainName,
                   Kinds.IPv4        => ipv4Address,
                   Kinds.IPv6        => ipv6Address,
                   _                 => null
               };

        /// <summary>
        /// Whether this host is a domain name or an IP address, i.e. not default(URLHost).
        /// </summary>
        public Boolean       HasValue
            => kind != Kinds.None;

        /// <summary>
        /// The domain name, or null if this host is an IP address.
        /// </summary>
        public DomainName?   DomainName
            => domainName;

        /// <summary>
        /// The IP address, or null if this host is a domain name.
        /// </summary>
        public IPAddress?    IPAddress
            => kind switch {
                   Kinds.IPv4  => ipv4Address,
                   Kinds.IPv6  => ipv6Address,
                   _           => null
               };

        /// <summary>
        /// Whether this host is a domain name.
        /// </summary>
        public Boolean       IsDomainName
            => kind == Kinds.DomainName;

        /// <summary>
        /// Whether this host is an IP address, i.e. does not have to be resolved.
        /// </summary>
        public Boolean       IsIPAddress
            => kind is Kinds.IPv4 or Kinds.IPv6;

        /// <summary>
        /// Whether this host is an IPv4 address.
        /// </summary>
        public Boolean       IsIPv4
            => kind == Kinds.IPv4;

        /// <summary>
        /// Whether this host is an IPv6 address.
        /// </summary>
        public Boolean       IsIPv6
            => kind == Kinds.IPv6;

        /// <summary>
        /// Whether this host refers to the local machine.
        /// </summary>
        /// Note: This deliberately compares the normalized text representation, as
        ///       DomainName.FullName is inconsistent about its trailing dot:
        ///       DomainName.Parse("localhost") yields "localhost." while the
        ///       DomainName.Localhost shortcut yields "localhost".
        public Boolean       IsLocalhost
            => kind switch {
                   Kinds.DomainName  => ToString().Equals("localhost", StringComparison.OrdinalIgnoreCase),
                   Kinds.IPv4        => ipv4Address.IsLocalhost,
                   Kinds.IPv6        => ipv6Address.IsLocalhost,
                   _                 => false
               };

        /// <summary>
        /// Indicates whether this host is null or empty, which is only ever
        /// the case for default(URLHost).
        /// </summary>
        public Boolean       IsNullOrEmpty
            => !HasValue;

        /// <summary>
        /// Indicates whether this host is NOT null or empty.
        /// </summary>
        public Boolean       IsNotNullOrEmpty
            => HasValue;

        /// <summary>
        /// The length of the text representation of this host.
        /// </summary>
        public UInt64        Length
            => (UInt64) ToString().Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new URL host from the given domain name.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        public URLHost(DomainName DomainName)
        {
            this.kind         = Kinds.DomainName;
            this.domainName   = DomainName;
        }

        /// <summary>
        /// Create a new URL host from the given IPv4 address.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address.</param>
        public URLHost(IPv4Address IPv4Address)
        {
            this.kind         = Kinds.IPv4;
            this.ipv4Address  = IPv4Address;
        }

        /// <summary>
        /// Create a new URL host from the given IPv6 address.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address.</param>
        public URLHost(IPv6Address IPv6Address)
        {
            this.kind         = Kinds.IPv6;
            this.ipv6Address  = IPv6Address;
        }

        #endregion


        #region (static) Localhost

        /// <summary>
        /// The "localhost" host.
        /// </summary>
        public static URLHost Localhost

            => new (DNS.DomainName.Localhost);

        #endregion

        #region (static) From     (DomainName)

        /// <summary>
        /// Create a new URL host from the given domain name.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        public static URLHost From(DomainName DomainName)

            => new (DomainName);

        #endregion

        #region (static) From     (IPAddress)

        /// <summary>
        /// Create a new URL host from the given IP address.
        /// </summary>
        /// Note: An IPvXAddress stands for both protocols at once, so it is no IP literal
        ///       of an URL: its localhost is the domain name "localhost", and its "any"
        ///       is 0.0.0.0. Its text used to be bracketed as if it were IPv6 - "[localhost]".
        /// <param name="IPAddress">An IP address.</param>
        public static URLHost From(IPAddress IPAddress)

            => IPAddress switch {
                   IPv4Address ipv4                   => new (ipv4),
                   IPv6Address ipv6                   => new (ipv6),
                   IPvXAddress { IsLocalhost: true }  => Localhost,
                   IPvXAddress                        => new (IPv4Address.Any),
                   _                                  => throw new ArgumentException(
                                                                   "An empty IP address is no URL host!",
                                                                   nameof(IPAddress)
                                                               )
               };

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as the host of an URL.
        /// </summary>
        /// <param name="Text">A text representation of an URL host.</param>
        public static URLHost Parse(String? Text, IPPort Port)
        {

            if (TryParse($"{Text}:{Port}", out var host, out var errorResponse))
                return host;

            throw new ArgumentException(errorResponse, nameof(Text));

        }

        #endregion

        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as the host of an URL.
        /// </summary>
        /// <param name="Text">A text representation of an URL host.</param>
        public static URLHost Parse(String? Text)
        {

            if (TryParse(Text, out var host, out var errorResponse))
                return host;

            throw new ArgumentException(errorResponse, nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as the host of an URL.
        /// </summary>
        /// <param name="Text">A text representation of an URL host.</param>
        public static URLHost? TryParse(String? Text)
        {

            if (TryParse(Text, out var host, out _))
                return host;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out Host)

        /// <summary>
        /// Try to parse the given text as the host of an URL.
        /// </summary>
        /// <param name="Text">A text representation of an URL host.</param>
        /// <param name="Host">The parsed URL host.</param>
        public static Boolean TryParse(String? Text, out URLHost Host)

            => TryParse(Text, out Host, out _);

        #endregion

        #region (static) TryParse (Text, out Host, out ErrorResponse)

        /// <summary>
        /// Try to parse the given text as the host of an URL.
        /// </summary>
        /// <param name="Text">A text representation of an URL host.</param>
        /// <param name="Host">The parsed URL host.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(String?                               Text,
                                       out URLHost                           Host,
                                       [NotNullWhen(false)] out String?      ErrorResponse)
        {

            Host           = default;
            ErrorResponse  = null;

            if (Text is null)
            {
                ErrorResponse = "The given text representation of an URL host must not be null!";
                return false;
            }

            Text = Text.Trim();

            if (Text.Length == 0)
            {
                ErrorResponse = "The given text representation of an URL host must not be empty!";
                return false;
            }

            #region IP-literal, i.e. an IPv6 address, which MUST be enclosed in brackets

            if (Text[0] == '[')
            {

                if (Text[^1] != ']')
                {
                    ErrorResponse = $"Unterminated IPv6 literal: '{Text}'!";
                    return false;
                }

                if (!IPv6Address.TryParse(Text[1..^1], out var ipv6Address))
                {
                    ErrorResponse = $"Invalid IPv6 address literal: '{Text}'!";
                    return false;
                }

                Host = ipv6Address;
                return true;

            }

            // An unbracketed IPv6 address is not a valid URL host, see RFC 3986 section 3.2.2.
            if (Text.Contains(':'))
            {
                ErrorResponse = $"An IPv6 address literal must be enclosed in brackets: '{Text}'!";
                return false;
            }

            #endregion

            #region IPv4address

            if (Hermod.IPAddress.IsIPv4Address(Text) &&
                IPv4Address.TryParse(Text, out var ipv4Address))
            {
                Host = ipv4Address;
                return true;
            }

            #endregion

            #region reg-name, i.e. a domain name

            if (DNS.DomainName.TryParse(Text, out var parsedDomainName, out var domainNameError))
            {
                Host = parsedDomainName;
                return true;
            }

            ErrorResponse = domainNameError ?? $"Invalid text representation of an URL host: '{Text}'!";
            return false;

            #endregion

        }

        #endregion


        #region TryGetValue(out ...)

        // The cases of the union, as the compiler matches them without boxing.

        /// <summary>
        /// Whether this host is a domain name, and which.
        /// </summary>
        public Boolean TryGetValue([NotNullWhen(true)] out DomainName? Value)
        {
            Value = domainName;
            return kind == Kinds.DomainName;
        }

        /// <summary>
        /// Whether this host is an IPv4 address, and which.
        /// </summary>
        public Boolean TryGetValue(out IPv4Address Value)
        {
            Value = ipv4Address;
            return kind == Kinds.IPv4;
        }

        /// <summary>
        /// Whether this host is an IPv6 address, and which.
        /// </summary>
        public Boolean TryGetValue(out IPv6Address Value)
        {
            Value = ipv6Address;
            return kind == Kinds.IPv6;
        }

        #endregion

        #region ToHTTPHostname(Port = null)

        /// <summary>
        /// Convert this URL host into a HTTP hostname, e.g. for the HTTP 'Host' header.
        ///
        /// Note: RFC 9110 section 7.2 defines the 'Host' header as "uri-host [ ':' port ]",
        ///       so the port belongs into it whenever it differs from the default port of
        ///       the URL scheme.
        /// </summary>
        /// <param name="Port">An optional TCP/IP port.</param>
        public HTTPHostname ToHTTPHostname(IPPort? Port = null)

            => Port.HasValue
                   ? HTTPHostname.Parse(ToString(), Port.Value)
                   : HTTPHostname.Parse(ToString());

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this URL host.
        /// </summary>
        public URLHost Clone()

            // Note: Both DomainName and the IP addresses are immutable.
            => this;

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator == (URLHost Host1, URLHost Host2)
            => Host1.Equals(Host2);

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator != (URLHost Host1, URLHost Host2)
            => !Host1.Equals(Host2);

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator < (URLHost Host1, URLHost Host2)
            => Host1.CompareTo(Host2) < 0;

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator <= (URLHost Host1, URLHost Host2)
            => Host1.CompareTo(Host2) <= 0;

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator > (URLHost Host1, URLHost Host2)
            => Host1.CompareTo(Host2) > 0;

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator >= (URLHost Host1, URLHost Host2)
            => Host1.CompareTo(Host2) >= 0;

        #endregion

        #region IComparable<URLHost> Members

        /// <summary>
        /// Compares two URL hosts.
        /// </summary>
        /// <param name="Object">An URL host to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is URLHost host
                   ? CompareTo(host)
                   : throw new ArgumentException("The given object is not an URL host!",
                                                 nameof(Object));

        /// <summary>
        /// Compares two URL hosts.
        /// </summary>
        /// <param name="Host">An URL host to compare with.</param>
        public Int32 CompareTo(URLHost Host)

            // Note: Host names are case-insensitive, see RFC 3986 section 3.2.2.
            => String.Compare(ToString(),
                              Host.ToString(),
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #region IEquatable<URLHost> Members

        /// <summary>
        /// Compares two URL hosts for equality.
        /// </summary>
        /// <param name="Object">An URL host to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is URLHost host &&
                   Equals(host);

        /// <summary>
        /// Compares two URL hosts for equality.
        /// </summary>
        /// <param name="Host">An URL host to compare with.</param>
        public Boolean Equals(URLHost Host)

            // Note: Host names are case-insensitive, see RFC 3986 section 3.2.2.
            => String.Equals(ToString(),
                             Host.ToString(),
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => StringComparer.OrdinalIgnoreCase.GetHashCode(ToString());

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object, in the form used within an URL,
        /// i.e. without the trailing dot of a domain name and with an IPv6 address
        /// enclosed in brackets.
        /// </summary>
        /// Note: Bare is not what an URL or a Host header may carry: the colons of an
        ///       unbracketed IPv6 address would be read as the host/port separator, so
        ///       URLHost.Parse and HTTPHostname.Parse both reject it, and
        ///       URLHost.From(address).ToString() did not survive its own parser.
        public override String ToString()

            => kind switch {
                   Kinds.DomainName  => domainName!.FullName.TrimEnd('.'),
                   Kinds.IPv4        => ipv4Address.ToString(),
                   Kinds.IPv6        => ipv6Address.ToIPLiteral(),
                   _                 => ""
               };

        #endregion

    }

}
