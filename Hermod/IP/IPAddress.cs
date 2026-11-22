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

using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for IP addresses.
    /// </summary>
    public static class IPAddressExtensions
    {

        // An extension method is not found through the conversion of a union, so an
        // IPv4, an IPv6 and an IPvX address each say it themselves.

        #region ToIPLiteral(this IPAddress)

        /// <summary>
        /// Return this IP address in the shape the authority of an URL may carry it:
        /// an IPv6 address enclosed in brackets, an IPv4 address as it is.
        /// </summary>
        /// <param name="IPAddress">An IPv4 address.</param>
        public static String ToIPLiteral(this IPv4Address IPAddress)
            => new IPAddress(IPAddress).ToIPLiteral();

        /// <summary>
        /// Return this IP address in the shape the authority of an URL may carry it:
        /// an IPv6 address enclosed in brackets, an IPv4 address as it is.
        /// </summary>
        /// <param name="IPAddress">An IPv6 address.</param>
        public static String ToIPLiteral(this IPv6Address IPAddress)
            => new IPAddress(IPAddress).ToIPLiteral();

        #endregion

        #region ToDotNet  (this IPAddress)

        /// <summary>
        /// Convert this IPv4 address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPAddress">An IPv4 address.</param>
        public static System.Net.IPAddress ToDotNet(this IPv4Address IPAddress)
            => new IPAddress(IPAddress).ToDotNet();

        /// <summary>
        /// Convert this IPv6 address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPAddress">An IPv6 address.</param>
        public static System.Net.IPAddress ToDotNet(this IPv6Address IPAddress)
            => new IPAddress(IPAddress).ToDotNet();

        /// <summary>
        /// Convert this IPvX address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPAddress">An IPvX address.</param>
        public static System.Net.IPAddress ToDotNet(this IPvXAddress IPAddress)
            => new IPAddress(IPAddress).ToDotNet();

        #endregion

    }


    /// <summary>
    /// An IP address: an IPv4 address, an IPv6 address, or an IPvX address - one that
    /// stands for both protocols at once, "any" or "localhost" of a dual-stack socket.
    ///
    /// This is a C# union of exactly these three: a switch over an IP address that
    /// forgets one of them is a compiler warning (CS8509), and each of them converts
    /// implicitly into an IP address. It is written out by hand rather than as
    /// "union IPAddress(...)", as the compiler's own union keeps its value as an
    /// object, and so would box every address; matching this one allocates nothing.
    /// </summary>
    [Union]
    public readonly struct IPAddress : IEquatable<IPAddress>,
                                       IComparable<IPAddress>,
                                       IComparable
    {

        #region (enum) Kinds

        private enum Kinds : Byte
        {
            None,
            IPv4,
            IPv6,
            IPvX

        }

        #endregion

        #region Data

        // Exactly the field of the kind is set; none for default(IPAddress).
        private readonly Kinds        kind;
        private readonly IPv4Address  ipv4Address;
        private readonly IPv6Address  ipv6Address;
        private readonly IPvXAddress  ipvXAddress;

        #endregion

        #region Properties

        /// <summary>
        /// The IPv4, IPv6 or IPvX address, or null for default(IPAddress).
        /// </summary>
        public Object?  Value

            => kind switch {
                   Kinds.IPv4  => ipv4Address,
                   Kinds.IPv6  => ipv6Address,
                   Kinds.IPvX  => ipvXAddress,
                   _           => null
               };

        /// <summary>
        /// Whether this is an address at all, i.e. not default(IPAddress).
        /// </summary>
        public Boolean  HasValue

            => kind != Kinds.None;

        /// <summary>
        /// The length of the IP address.
        /// </summary>
        public Byte     Length

            => kind switch {
                   Kinds.IPv4  => IPv4Address.Length,
                   Kinds.IPv6  => IPv6Address.Length,
                   Kinds.IPvX  => IPvXAddress.Length,
                   _           => 0
               };

        /// <summary>
        /// Whether the IP address is a multicast address.
        /// </summary>
        public Boolean  IsMulticast

            => kind switch {
                   Kinds.IPv4  => ipv4Address.IsMulticast,
                   Kinds.IPv6  => ipv6Address.IsMulticast,
                   Kinds.IPvX  => IPvXAddress.IsMulticast,
                   _           => false
               };

        /// <summary>
        /// Whether this is an IPv4 address - an IPvX address is one, too.
        /// </summary>
        public Boolean  IsIPv4

            => kind is Kinds.IPv4 or Kinds.IPvX;

        /// <summary>
        /// Whether this is an IPv4 address mapped into IPv6 (::ffff:w.x.y.z).
        /// </summary>
        public Boolean  IsMappedIPv4

            => kind switch {
                   Kinds.IPv6  => ipv6Address.IsMappedIPv4,
                   Kinds.IPvX  => IPvXAddress.IsMappedIPv4,
                   _           => false
               };

        /// <summary>
        /// Whether this is an IPv6 address - an IPvX address is one, too.
        /// </summary>
        public Boolean  IsIPv6

            => kind is Kinds.IPv6 or Kinds.IPvX;

        /// <summary>
        /// Whether this is the address of the local machine.
        /// </summary>
        public Boolean  IsLocalhost

            => kind switch {
                   Kinds.IPv4  => ipv4Address.IsLocalhost,
                   Kinds.IPv6  => ipv6Address.IsLocalhost,
                   Kinds.IPvX  => ipvXAddress.IsLocalhost,
                   _           => false
               };

        /// <summary>
        /// Whether this is the "any" address of a listening socket.
        /// </summary>
        public Boolean  IsAny
            => kind switch {
                   Kinds.IPv4  => ipv4Address.IsAny,
                   Kinds.IPv6  => ipv6Address.IsAny,
                   Kinds.IPvX  => ipvXAddress.IsAny,
                   _           => false
               };

        /// <summary>
        /// This address as an IPv4 address: itself, the IPv4 address an IPv6 address
        /// maps, or null.
        /// </summary>
        public IPv4Address? AsIPv4

            => kind switch {
                   Kinds.IPv4  => ipv4Address,
                   Kinds.IPv6  => ipv6Address.MappedIPv4,
                   _           => null
               };

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new IP address from the given IPv4 address.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address.</param>
        public IPAddress(IPv4Address IPv4Address)
        {
            this.kind         = Kinds.IPv4;
            this.ipv4Address  = IPv4Address;
        }

        /// <summary>
        /// Create a new IP address from the given IPv6 address.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address.</param>
        public IPAddress(IPv6Address IPv6Address)
        {
            this.kind         = Kinds.IPv6;
            this.ipv6Address  = IPv6Address;
        }

        /// <summary>
        /// Create a new IP address from the given IPvX address.
        /// </summary>
        /// <param name="IPvXAddress">An IPvX address.</param>
        public IPAddress(IPvXAddress IPvXAddress)
        {
            this.kind         = Kinds.IPvX;
            this.ipvXAddress  = IPvXAddress;
        }

        #endregion


        #region TryGetValue(out ...)

        // The cases of the union, as the compiler matches them without boxing.

        /// <summary>
        /// Whether this is an IPv4 address, and which.
        /// </summary>
        public Boolean TryGetValue(out IPv4Address Value)
        {
            Value = ipv4Address;
            return kind == Kinds.IPv4;
        }

        /// <summary>
        /// Whether this is an IPv6 address, and which.
        /// </summary>
        public Boolean TryGetValue(out IPv6Address Value)
        {
            Value = ipv6Address;
            return kind == Kinds.IPv6;
        }

        /// <summary>
        /// Whether this is an IPvX address, and which.
        /// </summary>
        public Boolean TryGetValue(out IPvXAddress Value)
        {
            Value = ipvXAddress;
            return kind == Kinds.IPvX;
        }

        #endregion


        #region (static) Any / Localhost

        /// <summary>
        /// The "any" address of both IPv4 and IPv6.
        /// </summary>
        public static IPAddress Any
            => IPvXAddress.Any;

        /// <summary>
        /// The localhost of both IPv4 and IPv6.
        /// </summary>
        public static IPAddress Localhost
            => IPvXAddress.Localhost;

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given string as an IP address.
        /// </summary>
        /// <param name="Text">A text representation of an IP address.</param>
        public static IPAddress Parse(String Text)
        {

            if (TryParse(Text, out var ipAddress))
                return ipAddress;

            throw new ArgumentException($"Invalid text representation of an IP address: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) Parse    (Bytes)

        /// <summary>
        /// Parse the given byte array as an IP address.
        /// </summary>
        /// <param name="Bytes">A binary representation of an IP address.</param>
        public static IPAddress Parse(Byte[] Bytes)
        {

            if (TryParse(Bytes, out var ipAddress))
                return ipAddress;

            throw new ArgumentException($"Invalid binary representation of an IP address: '{Bytes.ToHexString()}'!",
                                        nameof(Bytes));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as an IP address.
        /// </summary>
        /// <param name="Text">A text representation of an IP address.</param>
        public static IPAddress? TryParse(String Text)
        {

            if (TryParse(Text, out var ipAddress))
                return ipAddress;

            return null;

        }

        #endregion

        #region (static) TryParse (Text,  out IPAddress)

        /// <summary>
        /// Try to parse the given text as an IP address.
        /// </summary>
        /// <remarks>
        /// The whole text has to be the address. This used to find one
        /// anywhere in it with IPv4AddressRegExpr and then Parse it, which threw
        /// on what the pattern had let through: "udp://213.133.98.98:53" in a
        /// configuration file stopped a program at its start with an exception
        /// out of a TryParse, where the file should have been refused with a
        /// sentence. The typed parsers read the whole text and say no instead.
        /// </remarks>
        /// <param name="Text">A text representation of an IP address.</param>
        /// <param name="IPAddress">The parsed IP address.</param>
        public static Boolean TryParse(String         Text,
                                       out IPAddress  IPAddress)
        {

            if (IPv4Address.TryParse(Text, out var ipv4Address))
            {
                IPAddress = ipv4Address;
                return true;
            }

            if (IPv6Address.TryParse(Text, out var ipv6Address))
            {
                IPAddress = ipv6Address;
                return true;
            }

            IPAddress = default;
            return false;

        }

        #endregion

        #region (static) TryParse (Bytes, out IPAddress)

        /// <summary>
        /// Try to parse the given byte array as an IP address.
        /// </summary>
        /// <param name="Bytes">A binary representation of an IP address.</param>
        /// <param name="IPAddress">The parsed IP address.</param>
        public static Boolean TryParse(Byte[]         Bytes,
                                       out IPAddress  IPAddress)
        {

            if (Bytes.Length == 4)
            {
                IPAddress = new IPv4Address(Bytes);
                return true;
            }

            if (Bytes.Length == 16)
            {
                IPAddress = new IPv6Address(Bytes);
                return true;
            }

            IPAddress = default;
            return false;

        }

        #endregion


        // Whether the whole of it is an address, by the typed parsers - see
        // TryParse. Asked of a pattern, a host name that merely contained an
        // address was one: "10.0.0.1.nip.io" went on to IPv4Address.Parse in
        // the TCP client and threw, and "127.0.0.1.example.com" was taken for
        // localhost. The host name's own port is not part of the question;
        // the domain name's root dot is not either.
        //
        // These are no IsIPv4, IsIPv6 and IsLocalhost, as an IP address answers
        // those of itself.

        public static Boolean IsIPv4Address(String        Text)
            => IPv4Address.TryParse(Text, out _);

        public static Boolean IsIPv4Address(HTTPHostname  Hostname)
            => Hostname.IsNotNullOrEmpty &&
               IPv4Address.TryParse(Hostname, out _);

        public static Boolean IsIPv4Address(DomainName    DomainName)
            => DomainName.IsNotNullOrEmpty() &&
               IPv4Address.TryParse(DomainName, out _);


        public static Boolean IsIPv6Address(String        Text)
            => IPv6Address.TryParse(Text, out _);

        public static Boolean IsIPv6Address(HTTPHostname  Hostname)
            => Hostname.IsNotNullOrEmpty &&
               IPv6Address.TryParse(Hostname, out _);

        public static Boolean IsIPv6Address(DomainName    DomainName)
            => DomainName.IsNotNullOrEmpty() &&
               IPv6Address.TryParse(DomainName, out _);


        public static Boolean IsLocalhostAddress(String        Text)
            => IsIPv4Localhost(Text)       || IsIPv6Localhost(Text);

        public static Boolean IsLocalhostAddress(HTTPHostname  Hostname)
            => IsIPv4Localhost(Hostname)   || IsIPv6Localhost(Hostname);

        public static Boolean IsLocalhostAddress(DomainName    DomainName)
            => IsIPv4Localhost(DomainName) || IsIPv6Localhost(DomainName);


        public static Boolean IsIPv4Localhost(String        Text)
            => (IsIPv4Address(Text) && Text.StartsWith("127.")) ||
               Text.Equals("localhost",  StringComparison.CurrentCultureIgnoreCase);

        public static Boolean IsIPv4Localhost(HTTPHostname  Hostname)
            => Hostname.IsNotNullOrEmpty && IsIPv4Localhost(Hostname.ToString());

        public static Boolean IsIPv4Localhost(DomainName    DomainName)
            => DomainName.IsNotNullOrEmpty() && IsIPv4Localhost(DomainName.ToString());


        public static Boolean IsIPv6Localhost(String        Text)
            => (IsIPv6Address(Text) && Text == "::1") ||
               Text.Equals("localhost",  StringComparison.CurrentCultureIgnoreCase) ||
               Text.Equals("localhost6", StringComparison.CurrentCultureIgnoreCase);

        public static Boolean IsIPv6Localhost(HTTPHostname  Hostname)
            => Hostname.IsNotNullOrEmpty && IsIPv6Localhost(Hostname.ToString());

        public static Boolean IsIPv6Localhost(DomainName    DomainName)
            => DomainName.IsNotNullOrEmpty() && IsIPv6Localhost(DomainName.ToString());


        #region (static) FromDotNet(IPAddress)

        /// <summary>
        /// Convert the given System.Net.IPAddress into a Hermod IP address.
        /// </summary>
        /// <param name="IPAddress">A System.Net.IPAddress.</param>
        public static IPAddress FromDotNet(System.Net.IPAddress IPAddress)
        {

            var bytes = IPAddress.GetAddressBytes();

            if (bytes.Length == 4)
                return new IPv4Address(bytes);

            else if (bytes.Length == 16)
                return IPv6Address.From(IPAddress);

            else
                throw new ArgumentException($"Invalid byte array length for an IP address: {bytes.Length}!",
                                            nameof(IPAddress));

        }

        #endregion

        #region (static) Build(ByteArray)

        /// <summary>
        /// Create a new IP address based on the given byte array representation.
        /// </summary>
        /// <param name="ByteArray">A byte representation of an IP address.</param>
        public static IPAddress Build(Byte[] ByteArray)

            => ByteArray.Length switch {
                   4   => new IPv4Address(ByteArray),
                   16  => new IPv6Address(ByteArray),
                   _   => throw new FormatException("Not a valid IP address!")
               };

        #endregion


        #region ToDotNet()

        /// <summary>
        /// Convert this IP address into a System.Net.IPAddress.
        /// </summary>
        public System.Net.IPAddress ToDotNet()
        {

            switch (kind)
            {

                // IPv4/IPv6 dual mode...
                case Kinds.IPvX:
                    return ipvXAddress.IsLocalhost
                               ? System.Net.IPAddress.IPv6Loopback
                               : System.Net.IPAddress.IPv6Any;

                case Kinds.IPv6 when ipv6Address.InterfaceId.IsNotNullOrEmpty():

                    if (!TryResolveIPv6ScopeId(ipv6Address.InterfaceId, out var scopeId))
                        throw new ArgumentException($"Unknown IPv6 interface identification '{ipv6Address.InterfaceId}'!");

                    return new System.Net.IPAddress(ipv6Address.GetBytes(), scopeId);

                case Kinds.IPv4:
                case Kinds.IPv6:
                    return new (GetBytes());

                default:
                    throw new InvalidOperationException("An empty IP address has no System.Net.IPAddress!");

            }

        }

        #endregion

        #region ToIPLiteral()

        /// <summary>
        /// Return this IP address in the shape the authority of an URL may carry it:
        /// an IPv6 address enclosed in brackets, an IPv4 address as it is.
        ///
        /// Note: RFC 3986 section 3.2.2 calls the bracketed form an "IP-literal". The
        ///       brackets are what keeps the colon in front of a port apart from the
        ///       colons inside the address.
        ///
        ///       IPv6Address.ToString() brackets "::" and "::1" by itself and spells
        ///       every other address out bare, so this adds brackets only where they
        ///       are missing. Bracketing unconditionally - which is what the callers
        ///       of this used to do, each on its own - rendered the loopback as
        ///       "[[::1]]".
        ///
        ///       An IPvX address is no literal of either protocol: its text, "any" or
        ///       "localhost", stays as it is. It used to be bracketed as if it were
        ///       IPv6 - "[localhost]".
        /// </summary>
        public String ToIPLiteral()
        {

            var text = ToString();

            return kind == Kinds.IPv6 && !text.StartsWith('[')
                       ? $"[{text}]"
                       :   text;

        }

        #endregion

        #region GetBytes()

        /// <summary>
        /// Return a byte array representation of this IP address.
        /// </summary>
        public Byte[] GetBytes()

            => kind switch {
                   Kinds.IPv4  => ipv4Address.GetBytes(),
                   Kinds.IPv6  => ipv6Address.GetBytes(),
                   Kinds.IPvX  => ipvXAddress.GetBytes(),
                   _           => []
               };

        #endregion


        #region Operator overloading

        /// <summary>
        /// This IP address as the IPv4 address it is - or an InvalidCastException,
        /// as the cast of an IP address to an IPv4Address always was.
        /// </summary>
        public static explicit operator IPv4Address(IPAddress IPAddress)
            => IPAddress.kind == Kinds.IPv4
                   ? IPAddress.ipv4Address
                   : throw new InvalidCastException($"The IP address '{IPAddress}' is no IPv4 address!");

        /// <summary>
        /// This IP address as the IPv6 address it is - or an InvalidCastException,
        /// as the cast of an IP address to an IPv6Address always was.
        /// </summary>
        public static explicit operator IPv6Address(IPAddress IPAddress)
            => IPAddress.kind == Kinds.IPv6
                   ? IPAddress.ipv6Address
                   : throw new InvalidCastException($"The IP address '{IPAddress}' is no IPv6 address!");

        /// <summary>
        /// This IP address as the IPvX address it is - or an InvalidCastException,
        /// as the cast of an IP address to an IPvXAddress always was.
        /// </summary>
        public static explicit operator IPvXAddress(IPAddress IPAddress)
            => IPAddress.kind == Kinds.IPvX
                   ? IPAddress.ipvXAddress
                   : throw new InvalidCastException($"The IP address '{IPAddress}' is no IPvX address!");


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator == (IPAddress IPAddress1, IPAddress IPAddress2)
            => IPAddress1.Equals(IPAddress2);

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator != (IPAddress IPAddress1, IPAddress IPAddress2)
            => !IPAddress1.Equals(IPAddress2);

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator <  (IPAddress IPAddress1, IPAddress IPAddress2)
            => IPAddress1.CompareTo(IPAddress2) <  0;

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator <= (IPAddress IPAddress1, IPAddress IPAddress2)
            => IPAddress1.CompareTo(IPAddress2) <= 0;

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator >  (IPAddress IPAddress1, IPAddress IPAddress2)
            => IPAddress1.CompareTo(IPAddress2) >  0;

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        public static Boolean operator >= (IPAddress IPAddress1, IPAddress IPAddress2)
            => IPAddress1.CompareTo(IPAddress2) >= 0;

        #endregion

        #region IComparable<IPAddress> Members

        /// <summary>
        /// Compares two IP addresses.
        /// </summary>
        /// <param name="Object">An IP address to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is IPAddress ipAddress
                   ? CompareTo(ipAddress)
                   : throw new ArgumentException("The given object is not an IP address!",
                                                 nameof(Object));

        /// <summary>
        /// Compares two IP addresses: the shorter first, IPvX before IPv4 before IPv6,
        /// and then by the address.
        /// </summary>
        /// <param name="IPAddress">An IP address to compare with.</param>
        public Int32 CompareTo(IPAddress IPAddress)
        {

            var c = Length.CompareTo(IPAddress.Length);

            if (c == 0)
                c = kind.CompareTo(IPAddress.kind);

            if (c != 0)
                return c;

            return kind switch {
                       Kinds.IPv4  => ipv4Address.CompareTo(IPAddress.ipv4Address),
                       Kinds.IPv6  => ipv6Address.CompareTo(IPAddress.ipv6Address),
                       Kinds.IPvX  => ipvXAddress.CompareTo(IPAddress.ipvXAddress),
                       _           => 0
                   };

        }

        #endregion

        #region IEquatable<IPAddress> Members

        /// <summary>
        /// Compares two IP addresses for equality.
        /// </summary>
        /// <param name="Object">An IP address to compare with.</param>
        public override Boolean Equals(Object? Object)

            // Its cases boxed are this address, too: MulticastDNS.IPv4Group is an
            // IPv4Address, and an IPAddress? asked for its Equals(Object) of one was
            // never equal to it - every mDNS datagram was then no multicast one.
            => Object switch {
                   IPAddress    ipAddress    => Equals(ipAddress),
                   IPv4Address  ipv4Address  => Equals(new IPAddress(ipv4Address)),
                   IPv6Address  ipv6Address  => Equals(new IPAddress(ipv6Address)),
                   IPvXAddress  ipvXAddress  => Equals(new IPAddress(ipvXAddress)),
                   _                         => false
               };

        /// <summary>
        /// Compares two IP addresses for equality: the same kind, and the same address.
        /// </summary>
        /// <param name="IPAddress">An IP address to compare with.</param>
        public Boolean Equals(IPAddress IPAddress)

            => kind == IPAddress.kind &&
               kind switch {
                   Kinds.IPv4  => ipv4Address.Equals(IPAddress.ipv4Address),
                   Kinds.IPv6  => ipv6Address.Equals(IPAddress.ipv6Address),
                   Kinds.IPvX  => ipvXAddress.Equals(IPAddress.ipvXAddress),
                   _           => true
               };

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => kind switch {
                   Kinds.IPv4  => ipv4Address.GetHashCode(),
                   Kinds.IPv6  => ipv6Address.GetHashCode(),
                   Kinds.IPvX  => ipvXAddress.GetHashCode(),
                   _           => 0
               };

        #endregion

        #region (override) ToString(Format = Long)

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => kind switch {
                   Kinds.IPv4  => ipv4Address.ToString(),
                   Kinds.IPv6  => ipv6Address.ToString(),
                   Kinds.IPvX  => ipvXAddress.ToString(),
                   _           => ""
               };

        /// <summary>
        /// Return a text representation of this object, an IPv6 address in the
        /// given format - see <see cref="IPv6Format"/>. An IPv4 address has one
        /// text only, and that is what it returns whatever the format.
        /// </summary>
        /// <param name="Format">How an IPv6 address is written out.</param>
        public String ToString(IPv6Format Format)

            => kind == Kinds.IPv6
                   ? ipv6Address.ToString(Format)
                   : ToString();

        #endregion


        #region (private static) TryResolveIPv6ScopeId(InterfaceId, out ScopeId)

        private static Boolean TryResolveIPv6ScopeId(String InterfaceId, out Int64 ScopeId)
        {

            if (Int64.TryParse(InterfaceId,
                               NumberStyles.None,
                               CultureInfo.InvariantCulture,
                               out ScopeId))
            {
                return ScopeId >= 0;
            }

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {

                if (!networkInterface.Name.Equals(InterfaceId, StringComparison.Ordinal) &&
                    !networkInterface.Id.  Equals(InterfaceId, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {

                    var ipv6Properties = networkInterface.GetIPProperties().GetIPv6Properties();

                    if (ipv6Properties is not null)
                    {
                        ScopeId = ipv6Properties.Index;
                        return true;
                    }

                }
                catch (NetworkInformationException)
                { }

            }

            ScopeId = 0;
            return false;

        }

        #endregion

    }

}
