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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A single HTTP cookie: a name, a value and, where it comes from a
    /// Set-Cookie header field, its attributes (RFC 6265 and
    /// draft-ietf-httpbis-rfc6265bis-22).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is kept as it was sent. Its "crumbs" are a convention of
    /// Hermod's own HTTP API, not of any RFC: a value "login=YWxpY2U=:isAdminRO"
    /// is read as the crumbs "login" and "isAdminRO". They are what the
    /// enumerator and the indexer return. <see cref="Value"/> used to be the
    /// first crumb's name, so a value "abc==", base64 with its padding, read
    /// as "abc", and a value "a:a" was written back as "a".
    /// </para>
    /// <para>
    /// Hermod keeps no cookie store. Of the storage model of 6265bis,
    /// Section 5.7, it applies what needs no request to judge: a cookie every
    /// user agent ignores whatever the request - one that names
    /// SameSite=None without Secure, a "__Secure-" cookie without Secure, a
    /// "__Host-" cookie without Secure, with a Domain or without Path=/ - is
    /// no cookie here either. A nameless cookie is not one either: servers
    /// must not produce them (Section 4.1.1), and the collections here are
    /// keyed by name.
    /// </para>
    /// </remarks>
    public class HTTPCookie : IEnumerable<KeyValuePair<String, String>>,
                              IEquatable<HTTPCookie>,
                              IComparable<HTTPCookie>,
                              IComparable
    {

        #region Data

        /// <summary>
        /// The longest name and value together a user agent keeps, in octets (Section 5.6, step 5).
        /// </summary>
        public const Int32 MaxNameAndValueSize    = 4096;

        /// <summary>
        /// The longest attribute value a user agent reads, in octets (Section 5.6, step 6).
        /// </summary>
        public const Int32 MaxAttributeValueSize  = 1024;

        /// <summary>
        /// The crumbs of the value - Hermod's own convention, see the remarks.
        /// </summary>
        private readonly Dictionary<String, String>              crumbs;

        /// <summary>
        /// The attributes no field below holds, in the order they came.
        /// </summary>
        private readonly List<KeyValuePair<String, String?>>     extensions;

        #endregion

        #region Properties

        /// <summary>
        /// The name of the cookie.
        /// </summary>
        public HTTPCookieName   Name          { get; }

        /// <summary>
        /// The value of the cookie, as it was sent: double quotes around it,
        /// if any, are part of it (Section 4.1.1).
        /// </summary>
        public String           Value         { get; }

        /// <summary>
        /// The Expires attribute, in UTC, when it is a cookie-date.
        /// </summary>
        public DateTimeOffset?  Expires       { get; }

        /// <summary>
        /// The Max-Age attribute, in seconds. Zero or less means the cookie
        /// has expired. It takes precedence over <see cref="Expires"/>.
        /// </summary>
        public Int64?           MaxAge        { get; }

        /// <summary>
        /// The Domain attribute, without a leading dot and in lower case.
        /// Null for a host-only cookie.
        /// </summary>
        public String?          Domain        { get; }

        /// <summary>
        /// The Path attribute when it is one: an attribute value beginning
        /// with "/". Null where the user agent takes the default-path.
        /// </summary>
        public String?          Path          { get; }

        /// <summary>
        /// The Secure attribute.
        /// </summary>
        public Boolean          Secure        { get; }

        /// <summary>
        /// The HttpOnly attribute.
        /// </summary>
        public Boolean          HTTPOnly      { get; }

        /// <summary>
        /// The SameSite attribute: "Strict", "Lax" or "None", "Default" for
        /// any other value (Section 5.6.7), and null without the attribute.
        /// </summary>
        public String?          SameSite      { get; }

        /// <summary>
        /// The attributes this cookie carries that none of the properties
        /// above holds ("Partitioned", "Priority=High"), in their order, with
        /// a null value where the attribute had no "=".
        /// </summary>
        public IEnumerable<KeyValuePair<String, String?>>  Extensions
            => extensions;

        #endregion

        #region Constructor(s)

        private HTTPCookie(HTTPCookieName                            Name,
                           String                                    Value,
                           DateTimeOffset?                           Expires,
                           Int64?                                    MaxAge,
                           String?                                   Domain,
                           String?                                   Path,
                           Boolean                                   Secure,
                           Boolean                                   HTTPOnly,
                           String?                                   SameSite,
                           IEnumerable<KeyValuePair<String, String?>>  Extensions)
        {

            this.Name        = Name;
            this.Value       = Value;
            this.Expires     = Expires;
            this.MaxAge      = MaxAge;
            this.Domain      = Domain;
            this.Path        = Path;
            this.Secure      = Secure;
            this.HTTPOnly    = HTTPOnly;
            this.SameSite    = SameSite;
            this.extensions  = [.. Extensions];
            this.crumbs      = ParseCrumbs(Value);

        }

        #endregion


        #region Create(Name, Value, ...)

        /// <summary>
        /// Create a cookie for a Set-Cookie header field, held to what
        /// Section 4.1 asks of a server: a token as its name, cookie-octets
        /// as its value, attribute values without control characters or ";",
        /// no more than 4096 octets of name and value and 1024 of any
        /// attribute value, and the requirements of the "__Secure-" and
        /// "__Host-" prefixes and of SameSite=None.
        /// </summary>
        /// <param name="Name">The name of the cookie, a token.</param>
        /// <param name="Value">The value of the cookie: cookie-octets, possibly in double quotes.</param>
        /// <param name="Expires">The Expires attribute.</param>
        /// <param name="MaxAge">The Max-Age attribute, in seconds.</param>
        /// <param name="Domain">The Domain attribute.</param>
        /// <param name="Path">The Path attribute, beginning with "/".</param>
        /// <param name="Secure">The Secure attribute.</param>
        /// <param name="HTTPOnly">The HttpOnly attribute.</param>
        /// <param name="SameSite">The SameSite attribute: "Strict", "Lax" or "None".</param>
        /// <exception cref="ArgumentException">The cookie is not one a server may send.</exception>
        public static HTTPCookie Create(HTTPCookieName   Name,
                                        String           Value,
                                        DateTimeOffset?  Expires    = null,
                                        Int64?           MaxAge     = null,
                                        String?          Domain     = null,
                                        String?          Path       = null,
                                        Boolean          Secure     = false,
                                        Boolean          HTTPOnly   = false,
                                        String?          SameSite   = null)
        {

            var name = Name.ToString();

            if (name.Length == 0 || !name.All(IsTokenChar))
                throw new ArgumentException($"The cookie name '{name}' is not a token!", nameof(Name));

            ArgumentNullException.ThrowIfNull(Value);

            var unquoted = Value.Length >= 2 && Value[0] == '"' && Value[^1] == '"'
                               ? Value[1..^1]
                               : Value;

            if (!unquoted.All(IsCookieOctet))
                throw new ArgumentException($"The cookie value '{Value}' has characters a cookie-value has not!", nameof(Value));

            if (OctetCount(name) + OctetCount(Value) > MaxNameAndValueSize)
                throw new ArgumentException($"The cookie's name and value are longer than {MaxNameAndValueSize} octets!", nameof(Value));

            if (Domain is not null)
            {
                Domain = Domain.StartsWith('.') ? Domain[1..] : Domain;
                if (Domain.Length == 0 || !IsAttributeValue(Domain) || Domain.Any(c => c > '\x7F'))
                    throw new ArgumentException($"The domain '{Domain}' is not a domain-value!", nameof(Domain));
                Domain = Domain.ToLowerInvariant();
            }

            if (Path is not null && (!Path.StartsWith('/') || !IsAttributeValue(Path)))
                throw new ArgumentException($"The path '{Path}' does not begin with '/' or has characters a path-value has not!", nameof(Path));

            if (SameSite is not null)
            {
                SameSite = CanonicalSameSite(SameSite);
                if (SameSite == "Default")
                    throw new ArgumentException("SameSite is \"Strict\", \"Lax\" or \"None\"!", nameof(SameSite));
            }

            var cookie = new HTTPCookie(Name, Value, Expires?.ToUniversalTime(), MaxAge, Domain, Path, Secure, HTTPOnly, SameSite, []);

            if (cookie.Rejected() is String reason)
                throw new ArgumentException(reason, nameof(Name));

            return cookie;

        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text as a single HTTP cookie: the value of a
        /// Set-Cookie header field.
        /// </summary>
        /// <param name="Text">A set-cookie-string, e.g. "session=abc; Path=/; HttpOnly".</param>
        public static HTTPCookie Parse(String Text)
        {

            if (TryParse(Text, out var httpCookie))
                return httpCookie;

            throw new ArgumentException("The given text representation of a single HTTP cookie is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text, out HTTPCookie)

        /// <summary>
        /// Parse the given text as a single HTTP cookie: the value of a
        /// Set-Cookie header field, read as Section 5.6 of
        /// draft-ietf-httpbis-rfc6265bis-22 reads it.
        /// </summary>
        /// <param name="Text">A set-cookie-string, e.g. "session=abc; Path=/; HttpOnly".</param>
        /// <param name="HTTPCookie">The parsed HTTP cookie.</param>
        public static Boolean TryParse(String?                              Text,
                                       [NotNullWhen(true)] out HTTPCookie?  HTTPCookie)
        {

            HTTPCookie = null;

            // Step 1: a control character other than HTAB, and it is no cookie.
            if (Text is null || Text.Any(IsForbiddenControl))
                return false;

            // Step 2: the name-value-pair up to the first ";".
            var semicolon      = Text.IndexOf(';');
            var nameValuePair  = semicolon < 0 ? Text : Text[..semicolon];
            var attributes     = semicolon < 0 ? ""   : Text[semicolon..];

            if (!TryParseNameValuePair(nameValuePair, out var name, out var value))
                return false;

            DateTimeOffset?  expires   = null;
            Int64?           maxAge    = null;
            String?          domain    = null;
            String?          path      = null;
            var              secure    = false;
            var              httpOnly  = false;
            String?          sameSite  = null;
            var              others    = new List<KeyValuePair<String, String?>>();

            // The attributes, each the last of its name where it counts (Section 5.7).
            foreach (var cookieAV in attributes.Split(';').Skip(1))
            {

                var equals          = cookieAV.IndexOf('=');
                var attributeName   = TrimWSP(equals < 0 ? cookieAV : cookieAV[..equals]);
                var attributeValue  = equals < 0 ? "" : TrimWSP(cookieAV[(equals + 1)..]);

                if (OctetCount(attributeValue) > MaxAttributeValueSize)
                    continue;

                switch (attributeName.ToLowerInvariant())
                {

                    case "expires":
                        if (HTTPCookieDate.TryParse(attributeValue, out var date))
                            expires = date;
                        break;

                    case "max-age":
                        if (TryParseMaxAge(attributeValue, out var seconds))
                            maxAge = seconds;
                        break;

                    case "domain":
                        var cookieDomain = attributeValue.StartsWith('.') ? attributeValue[1..] : attributeValue;
                        domain = cookieDomain.Length > 0 ? cookieDomain.ToLowerInvariant() : null;
                        break;

                    case "path":
                        path = attributeValue.StartsWith('/') ? attributeValue : null;
                        break;

                    case "secure":
                        secure = true;
                        break;

                    case "httponly":
                        httpOnly = true;
                        break;

                    case "samesite":
                        sameSite = CanonicalSameSite(attributeValue);
                        break;

                    default:
                        if (attributeName.Length > 0)
                            others.Add(new (attributeName, equals < 0 ? null : attributeValue));
                        break;

                }

            }

            // Section 5.7, step 8: a domain outside US-ASCII.
            if (domain is not null && domain.Any(c => c > '\x7F'))
                return false;

            var cookie = new HTTPCookie(name, value, expires, maxAge, domain, path, secure, httpOnly, sameSite, others);

            if (cookie.Rejected() is not null)
                return false;

            HTTPCookie = cookie;
            return true;

        }

        #endregion

        #region (internal) TryParseCookiePair(Text, out HTTPCookie)

        /// <summary>
        /// One cookie-pair of a Cookie header field: a name and a value, and
        /// no attributes - "Path=/" there is a cookie named Path.
        /// </summary>
        /// <param name="Text">A cookie-pair, e.g. "session=abc".</param>
        /// <param name="HTTPCookie">The cookie.</param>
        internal static Boolean TryParseCookiePair(String                               Text,
                                                   [NotNullWhen(true)] out HTTPCookie?  HTTPCookie)
        {

            HTTPCookie = null;

            if (Text.Any(IsForbiddenControl) ||
                !TryParseNameValuePair(Text, out var name, out var value))
            {
                return false;
            }

            HTTPCookie = new HTTPCookie(name, value, null, null, null, null, false, false, null, []);
            return true;

        }

        #endregion


        #region this[Crumb]

        /// <summary>
        /// Return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        public String? this[String Crumb]

            => crumbs.TryGetValue(Crumb, out var value)
                   ? value
                   : null;

        #endregion

        #region Get(Crumb)

        /// <summary>
        /// Return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        public String? Get(String Crumb)

            => crumbs.TryGetValue(Crumb, out var value)
                   ? value
                   : null;

        #endregion

        #region TryGet(Crumb, out Value)

        /// <summary>
        /// Try to return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        /// <param name="Value">The value of the crumb.</param>
        public Boolean TryGet(String Crumb, out String? Value)

            => crumbs.TryGetValue(Crumb, out Value);

        #endregion


        #region GetEnumerator()

        public IEnumerator<KeyValuePair<String, String>> GetEnumerator()
            => crumbs.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => crumbs.GetEnumerator();

        #endregion


        #region ToCookiePair()

        /// <summary>
        /// The cookie as a Cookie header field carries it: its name and value,
        /// and none of its attributes (Section 5.8.3, step 6).
        /// </summary>
        public String ToCookiePair()

            => $"{Name}={Value}";

        #endregion


        #region Operator overloading

        #region Operator == (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">An HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPCookie HTTPCookie1,
                                           HTTPCookie HTTPCookie2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPCookie1, HTTPCookie2))
                return true;

            // If one is null, but not both, return false.
            if (HTTPCookie1 is null || HTTPCookie2 is null)
                return false;

            return HTTPCookie1.Equals(HTTPCookie2);

        }

        #endregion

        #region Operator != (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">An HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPCookie HTTPCookie1,
                                           HTTPCookie HTTPCookie2)

            => !(HTTPCookie1 == HTTPCookie2);

        #endregion

        #region Operator <  (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">An HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPCookie HTTPCookie1,
                                          HTTPCookie HTTPCookie2)
        {

            if (HTTPCookie1 is null)
                throw new ArgumentNullException("The given HTTPCookie1 must not be null!");

            return HTTPCookie1.CompareTo(HTTPCookie2) < 0;

        }

        #endregion

        #region Operator <= (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">An HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPCookie HTTPCookie1,
                                           HTTPCookie HTTPCookie2)

            => !(HTTPCookie1 > HTTPCookie2);

        #endregion

        #region Operator >  (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">An HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPCookie HTTPCookie1,
                                          HTTPCookie HTTPCookie2)
        {

            if (HTTPCookie1 is null)
                throw new ArgumentNullException("The given HTTPCookie1 must not be null!");

            return HTTPCookie1.CompareTo(HTTPCookie2) > 0;

        }

        #endregion

        #region Operator >= (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">An HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPCookie HTTPCookie1,
                                           HTTPCookie HTTPCookie2)

            => !(HTTPCookie1 < HTTPCookie2);

        #endregion

        #endregion

        #region IComparable<HTTPCookie> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP cookies.
        /// </summary>
        /// <param name="Object">An HTTP cookie to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPCookie httpCookie
                   ? CompareTo(httpCookie)
                   : throw new ArgumentException("The given object is not a HTTP cookie!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPCookie)

        /// <summary>
        /// Compares two HTTP cookies.
        /// </summary>
        /// <param name="HTTPCookie">An HTTP cookie to compare with.</param>
        public Int32 CompareTo(HTTPCookie? HTTPCookie)
        {

            if (HTTPCookie is null)
                throw new ArgumentNullException(nameof(HTTPCookie), "The given HTTP Cookie must not be null!");

            return String.Compare(ToString(),
                                  HTTPCookie.ToString(),
                                  StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPCookie> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP cookies for equality.
        /// </summary>
        /// <param name="Object">An HTTP cookie to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPCookie httpCookie &&
                   Equals(httpCookie);

        #endregion

        #region Equals(HTTPCookie)

        /// <summary>
        /// Compares two HTTP cookies for equality.
        /// </summary>
        /// <param name="HTTPCookie">An HTTP cookie to compare with.</param>
        public Boolean Equals(HTTPCookie? HTTPCookie)

            => HTTPCookie is not null &&
               String.Equals(ToString(),
                             HTTPCookie.ToString(),
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => Name.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The cookie as a Set-Cookie header field carries it: its name and
        /// value as they came, then each attribute once, in the spelling of
        /// Section 4.1.1, and the extensions as they came.
        /// </summary>
        public override String ToString()
        {

            var text = new StringBuilder(ToCookiePair());

            if (Expires.HasValue)
                text.Append("; Expires=").Append(HTTPCookieDate.ToString(Expires.Value));

            if (MaxAge.HasValue)
                text.Append("; Max-Age=").Append(MaxAge.Value);

            if (Domain is not null)
                text.Append("; Domain=").Append(Domain);

            if (Path is not null)
                text.Append("; Path=").Append(Path);

            if (Secure)
                text.Append("; Secure");

            if (HTTPOnly)
                text.Append("; HttpOnly");

            // "Default" is what any other value means, not a value to write.
            if (SameSite is not null && SameSite != "Default")
                text.Append("; SameSite=").Append(SameSite);

            foreach (var extension in extensions)
                text.Append("; ").Append(extension.Key).Append(extension.Value is null ? "" : "=" + extension.Value);

            return text.ToString();

        }

        #endregion


        #region (private) Rejected()

        /// <summary>
        /// Why every user agent ignores this cookie whatever the request:
        /// Section 5.7, steps 19 to 21. Null when none of them applies.
        /// </summary>
        private String? Rejected()
        {

            var name = Name.ToString();

            if (SameSite == "None" && !Secure)
                return "A cookie with SameSite=None needs the Secure attribute!";

            if (name.StartsWith("__Secure-", StringComparison.OrdinalIgnoreCase) && !Secure)
                return "A cookie named \"__Secure-...\" needs the Secure attribute!";

            if (name.StartsWith("__Host-", StringComparison.OrdinalIgnoreCase) && (!Secure || Domain is not null || Path != "/"))
                return "A cookie named \"__Host-...\" needs the Secure attribute, Path=/ and no Domain attribute!";

            return null;

        }

        #endregion

        #region (private) TryParseNameValuePair(Text, out Name, out Value)

        /// <summary>
        /// Section 5.6, steps 3 to 5: the name up to the first "=", the value
        /// after it, each without the whitespace around it, and no more than
        /// 4096 octets of both.
        /// </summary>
        private static Boolean TryParseNameValuePair(String Text, out HTTPCookieName Name, out String Value)
        {

            Name   = default;
            Value  = "";

            var equals = Text.IndexOf('=');

            // A nameless cookie (Section 5.6, step 3) - see the remarks.
            if (equals < 0)
                return false;

            var name = TrimWSP(Text[..equals]);
            Value    = TrimWSP(Text[(equals + 1)..]);

            if (name.Length == 0 ||
                OctetCount(name) + OctetCount(Value) > MaxNameAndValueSize)
            {
                return false;
            }

            return HTTPCookieName.TryParse(name, out Name);

        }

        #endregion

        #region (private) TryParseMaxAge(Text, out Seconds)

        /// <summary>
        /// Section 5.6.2: a DIGIT, or "-" and a DIGIT, and nothing but digits after it.
        /// </summary>
        private static Boolean TryParseMaxAge(String Text, out Int64 Seconds)
        {

            Seconds = 0;

            var digits = Text.StartsWith('-') ? Text[1..] : Text;

            if (digits.Length == 0 || !digits.All(Char.IsAsciiDigit))
                return false;

            // A number too long for 64 bits is still the longest or the shortest of lives.
            if (!Int64.TryParse(digits, out var magnitude))
                magnitude = Int64.MaxValue;

            Seconds = Text.StartsWith('-') ? -magnitude : magnitude;
            return true;

        }

        #endregion

        #region (private) ParseCrumbs(Value)

        /// <summary>
        /// The crumbs of a value, read as Hermod's HTTP API has always read
        /// them: separated by ":", each a name with an optional "=" and value.
        /// </summary>
        private static Dictionary<String, String> ParseCrumbs(String Value)
        {

            var crumbs = new Dictionary<String, String>();

            foreach (var crumb in Value.Split(':'))
            {

                var tuple = crumb.Split('=', 2);
                var key   = tuple[0].Trim();

                if (key.Length > 0)
                    crumbs[key] = tuple.Length == 2 ? tuple[1] : "";

            }

            return crumbs;

        }

        #endregion

        #region (private) Character classes

        /// <summary>
        /// %x00-08 / %x0A-1F / %x7F: the control characters other than HTAB (Section 5.6, step 1).
        /// </summary>
        private static Boolean IsForbiddenControl(Char Character)

            => Character is <= '\x08' or (>= '\x0A' and <= '\x1F') or '\x7F';

        /// <summary>
        /// tchar (RFC 9110, Section 5.6.2).
        /// </summary>
        private static Boolean IsTokenChar(Char Character)

            => Char.IsAsciiLetterOrDigit(Character) || "!#$%&'*+-.^_`|~".Contains(Character);

        /// <summary>
        /// cookie-octet = %x21 / %x23-2B / %x2D-3A / %x3C-5B / %x5D-7E
        /// </summary>
        private static Boolean IsCookieOctet(Char Character)

            => Character is '\x21'
                         or (>= '\x23' and <= '\x2B')
                         or (>= '\x2D' and <= '\x3A')
                         or (>= '\x3C' and <= '\x5B')
                         or (>= '\x5D' and <= '\x7E');

        /// <summary>
        /// av-octet = %x20-3A / %x3C-7E, and no whitespace at either end.
        /// </summary>
        private static Boolean IsAttributeValue(String Text)

            => Text.All(c => c is (>= '\x20' and <= '\x3A') or (>= '\x3C' and <= '\x7E'))
            && TrimWSP(Text).Length == Text.Length
            && OctetCount(Text) <= MaxAttributeValueSize;

        private static String TrimWSP(String Text)

            => Text.Trim(' ', '\t');

        private static Int32 OctetCount(String Text)

            => Encoding.UTF8.GetByteCount(Text);

        private static String CanonicalSameSite(String Value)

            => Value.ToLowerInvariant() switch {
                   "strict"  => "Strict",
                   "lax"     => "Lax",
                   "none"    => "None",
                   _         => "Default"
               };

        #endregion

    }

}
