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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// One node in a forwarding chain, as identified by the "for" and "by"
    /// parameters of the Forwarded field (RFC 7239, Section 6):
    ///
    ///    node      = nodename [ ":" node-port ]
    ///    nodename  = IPv4address / "[" IPv6address "]" / "unknown" / obfnode
    ///    obfnode   = "_" 1*( ALPHA / DIGIT / "." / "_" / "-" )
    ///    node-port = port / obfport
    ///    port      = 1*5DIGIT
    ///    obfport   = "_" 1*( ALPHA / DIGIT / "." / "_" / "-" )
    ///
    /// Three of those four nodenames are not addresses, which is the whole
    /// reason this type exists rather than an IIPAddress: a node may say
    /// "unknown" when it has an address it cannot express, and it may send an
    /// obfuscated identifier when it has one it does not wish to disclose.
    /// Section 6.3 is explicit that an obfuscated identifier is opaque - it may
    /// be compared with other identifiers from the same source and nothing
    /// else - so it is kept as text and never guessed at.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc7239.html#section-6"/>
    public sealed class ForwardedNode : IEquatable<ForwardedNode>
    {

        #region Properties

        /// <summary>
        /// The node name exactly as it appears in the field, IPv6 addresses
        /// still inside their square brackets.
        /// </summary>
        public String       NodeName          { get; }

        /// <summary>
        /// The address, when the node name is an IP literal; null for
        /// "unknown" and for obfuscated identifiers.
        /// </summary>
        public IIPAddress?  IPAddress         { get; }

        /// <summary>
        /// The port, when the node carries a numeric one.
        /// </summary>
        public IPPort?      Port              { get; }

        /// <summary>
        /// The port, when the node carries an obfuscated one ("_hidden").
        /// </summary>
        public String?      ObfuscatedPort    { get; }

        /// <summary>
        /// Whether the node declined to give an identifier at all.
        /// </summary>
        public Boolean      IsUnknown

            => NodeName.Equals("unknown", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Whether the node gave an identifier that is opaque to us
        /// (RFC 7239, Section 6.3).
        /// </summary>
        public Boolean      IsObfuscated

            => NodeName.StartsWith('_');

        #endregion

        #region Constructor(s)

        private ForwardedNode(String       NodeName,
                              IIPAddress?  IPAddress        = null,
                              IPPort?      Port             = null,
                              String?      ObfuscatedPort   = null)
        {

            this.NodeName        = NodeName;
            this.IPAddress       = IPAddress;
            this.Port            = Port;
            this.ObfuscatedPort  = ObfuscatedPort;

        }

        #endregion


        #region (static) Unknown

        /// <summary>
        /// The node that declines to identify itself.
        /// </summary>
        public static ForwardedNode Unknown

            => new ("unknown");

        #endregion

        #region (static) From  (IPAddress, Port = null)

        /// <summary>
        /// A node identified by an address, with an optional port.
        /// </summary>
        public static ForwardedNode From(IIPAddress  IPAddress,
                                         IPPort?     Port   = null)
        {

            var text = IPAddress.ToString();

            // RFC 7239, Section 6 wants an IPv6 address in square brackets -
            // but IPv6Address.ToString() already brackets "::" and "::1", and
            // nothing else, so they are added only where they are missing
            // rather than unconditionally.
            return new (IPAddress is IPv6Address && !text.StartsWith('[')
                            ? $"[{text}]"
                            :    text,
                        IPAddress,
                        Port);

        }

        #endregion

        #region (static) Obfuscated(Identifier)

        /// <summary>
        /// A node identified by an opaque token, which RFC 7239 requires to
        /// begin with an underscore.
        /// </summary>
        public static ForwardedNode Obfuscated(String Identifier)
        {

            var nodeName = Identifier.StartsWith('_')
                               ? Identifier
                               : $"_{Identifier}";

            if (!TryParse(nodeName, out var node))
                throw new ArgumentException($"'{Identifier}' is not a valid obfuscated node identifier!", nameof(Identifier));

            return node;

        }

        #endregion

        #region (static) TryParse(Text, out Node)

        /// <summary>
        /// Try to parse one node identifier.
        /// </summary>
        /// <param name="Text">The unquoted node identifier.</param>
        /// <param name="Node">The parsed node.</param>
        public static Boolean TryParse(String                                 Text,
                                       [NotNullWhen(true)] out ForwardedNode?  Node)
        {

            Node = null;

            if (Text is null)
                return false;

            var text = Text.Trim();

            if (text.Length == 0)
                return false;

            String  nodeName;
            String? portText;

            #region Split the node name from the port

            // An IPv6 address is the only node name that may contain a colon of
            // its own, and RFC 7239 puts it in square brackets precisely so
            // that the port stays findable.
            if (text.StartsWith('['))
            {

                var closing = text.IndexOf(']');

                if (closing < 0)
                    return false;

                nodeName  = text[..(closing + 1)];
                portText  = text.Length > closing + 1
                                ? text[(closing + 1)..]
                                : null;

                if (portText is not null)
                {

                    if (!portText.StartsWith(':'))
                        return false;

                    portText = portText[1..];

                }

            }

            else
            {

                var colon = text.IndexOf(':');

                nodeName  = colon < 0 ? text : text[..colon];
                portText  = colon < 0 ? null : text[(colon + 1)..];

            }

            #endregion

            #region The node name: an address, "unknown", or something opaque

            IIPAddress? ipAddress = null;

            if (nodeName.StartsWith('[') && nodeName.EndsWith(']'))
            {

                if (!IPv6Address.TryParse(nodeName[1..^1], out var ipv6Address))
                    return false;

                ipAddress = ipv6Address;

            }

            else if (nodeName.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            { }

            else if (nodeName.StartsWith('_'))
            {
                if (!IsObfuscatedToken(nodeName))
                    return false;
            }

            else
            {

                // Bare IPv6 is deliberately *not* accepted: RFC 7239 requires
                // the brackets, and accepting it anyway would make the port
                // ambiguous for exactly the addresses that need one.
                if (!IPv4Address.TryParse(nodeName, out var ipv4Address))
                    return false;

                ipAddress = ipv4Address;

            }

            #endregion

            #region The port: numeric, or opaque

            IPPort? port           = null;
            String? obfuscatedPort = null;

            if (portText is not null)
            {

                if (portText.Length == 0)
                    return false;

                if (portText.StartsWith('_'))
                {

                    if (!IsObfuscatedToken(portText))
                        return false;

                    obfuscatedPort = portText;

                }

                else
                {

                    if (portText.Length > 5 || !portText.All(Char.IsAsciiDigit))
                        return false;

                    if (!IPPort.TryParse(portText, out var parsedPort))
                        return false;

                    port = parsedPort;

                }

            }

            #endregion

            Node = new ForwardedNode(
                       nodeName,
                       ipAddress,
                       port,
                       obfuscatedPort
                   );

            return true;

        }

        #endregion

        #region (static) Parse   (Text)

        /// <summary>
        /// Parse one node identifier.
        /// </summary>
        /// <param name="Text">The unquoted node identifier.</param>
        public static ForwardedNode Parse(String Text)

            => TryParse(Text, out var node)
                   ? node
                   : throw new ArgumentException($"'{Text}' is not a valid RFC 7239 node identifier!", nameof(Text));

        #endregion

        #region (private static) IsObfuscatedToken(Text)

        private static Boolean IsObfuscatedToken(String Text)

            => Text.Length > 1 &&
               Text[0] == '_'  &&
               Text.Skip(1).All(character => Char.IsAsciiLetterOrDigit(character) ||
                                             character is '.' or '_' or '-');

        #endregion


        #region Operator overloading

        public static Boolean operator == (ForwardedNode? Node1, ForwardedNode? Node2)

            => Node1 is null
                   ? Node2 is null
                   : Node1.Equals(Node2);

        public static Boolean operator != (ForwardedNode? Node1, ForwardedNode? Node2)

            => !(Node1 == Node2);

        #endregion

        #region Equals / GetHashCode

        public Boolean Equals(ForwardedNode? Other)

            => Other is not null &&
               String.Equals(ToString(), Other.ToString(), StringComparison.OrdinalIgnoreCase);

        public override Boolean Equals(Object? Object)

            => Equals(Object as ForwardedNode);

        public override Int32 GetHashCode()

            => ToString().ToLowerInvariant().GetHashCode();

        #endregion

        #region ToString()

        /// <summary>
        /// Return the node identifier as it belongs in a Forwarded field,
        /// without the quoting that the field value may require around it.
        /// </summary>
        public override String ToString()

            => Port is not null
                   ? $"{NodeName}:{Port}"
                   : ObfuscatedPort is not null
                         ? $"{NodeName}:{ObfuscatedPort}"
                         :   NodeName;

        #endregion

    }

}
