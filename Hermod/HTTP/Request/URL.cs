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
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for uniform resource locations.
    /// </summary>
    public static class URLExtensions
    {

        /// <summary>
        /// Indicates whether this uniform resource location is null or empty.
        /// </summary>
        /// <param name="URL">An uniform resource location.</param>
        public static Boolean IsNullOrEmpty(this URL? URL)
            => !URL.HasValue || URL.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this uniform resource location is null or empty.
        /// </summary>
        /// <param name="URL">An uniform resource location.</param>
        public static Boolean IsNotNullOrEmpty(this URL? URL)
            => URL.HasValue && URL.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// An uniform resource location (URL).
    /// </summary>
    public readonly struct URL : IId<URL>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private        readonly  String      InternalId;

        /// <summary>
        /// The URL scheme, or null for default(URL).
        /// </summary>
        private        readonly  URIScheme?  scheme;

        /// <summary>
        /// The characters terminating the "scheme://[userinfo@]host[:port]" part of an URL.
        /// </summary>
        private static readonly  Char[]      pathQueryOrFragment = ['/', '?', '#'];

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean       IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean       IsNotNullOrEmpty
            => !InternalId.IsNullOrEmpty();

        /// <summary>
        /// The length of the uniform resource location.
        /// </summary>
        public UInt64        Length

            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// The URL scheme, e.g. "https", or null when this URL has none.
        ///
        /// Note: Nullable on purpose. A struct can not prevent its own default value from
        ///       being created, so default(URL) genuinely has no scheme. Declaring this
        ///       non-nullable and papering over it with a fallback would force one invented
        ///       answer on every caller, although only the caller knows what "no scheme"
        ///       means for it: AHTTPClient must not enforce TLS, a URL builder may well want
        ///       https, a log line wants nothing at all. A fallback of https once turned every
        ///       client whose RemoteURL was never assigned into one demanding a TLS handshake
        ///       on a plain connection - and nothing in the type system could have caught it.
        /// </summary>
        public URIScheme?    Scheme
            => scheme;

        /// <summary>
        /// The optional login of the user information.
        /// </summary>
        public String?       Login          { get; }

        /// <summary>
        /// The optional password of the user information.
        /// </summary>
        public String?       Password       { get; }

        /// <summary>
        /// The host, i.e. a domain name, an IPv4 address or an IPv6 address.
        /// </summary>
        public URLHost       Host           { get; }

        /// <summary>
        /// The optional TCP/IP port, defaulting to the default port of the URL scheme.
        /// </summary>
        public IPPort?       Port           { get; }

        /// <summary>
        /// The host as it belongs into the HTTP 'Host' header, i.e. the host plus the
        /// TCP/IP port whenever that differs from the default port of the URL scheme.
        ///
        /// RFC 9110 section 7.2: Host = uri-host [ ":" port ]
        /// RFC 3986 section 3.2.3: the port is omitted when it is the scheme's default.
        /// </summary>
        public HTTPHostname  HostHeader

            => HTTPHostname.From(
                   Host,
                   Port.HasValue && Port != Scheme?.DefaultPort
                       ? Port.Value
                       : null
               );

        /// <summary>
        /// The path.
        /// </summary>
        public HTTPPath      Path           { get; }

        /// <summary>
        /// The optional query string.
        /// </summary>
        public QueryString?  QueryString    { get; }

        /// <summary>
        /// The optional fragment, without the leading '#'.
        /// </summary>
        public String?       Fragment       { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new uniform resource location based on the given string.
        ///
        /// Note: This is deliberately the only constructor. Additional constructors taking
        ///       just a subset of the components led to Clone() and operator + silently
        ///       dropping the login, the password and the query string.
        /// </summary>
        /// <param name="String">The string representation of the uniform resource location.</param>
        /// <param name="Protocol">The URL scheme.</param>
        /// <param name="Login">An optional login of the user information.</param>
        /// <param name="Password">An optional password of the user information.</param>
        /// <param name="Host">The host, i.e. a domain name, an IPv4 address or an IPv6 address.</param>
        /// <param name="Port">An optional TCP/IP port.</param>
        /// <param name="Path">The path.</param>
        /// <param name="QueryString">An optional query string.</param>
        /// <param name="Fragment">An optional fragment, without the leading '#'.</param>
        private URL(String        String,
                    URIScheme?    Protocol,
                    String?       Login,
                    String?       Password,
                    URLHost       Host,
                    IPPort?       Port,
                    HTTPPath      Path,
                    QueryString?  QueryString,
                    String?       Fragment)
        {

            this.InternalId   = String;
            this.scheme     = Protocol;
            this.Login        = Login;
            this.Password     = Password;
            this.Host         = Host;
            this.Port         = Port;
            this.Path         = Path;
            this.QueryString  = QueryString;
            this.Fragment     = Fragment;

        }

        #endregion


        #region (static) Parse     (Text, URLScheme = null)

        /// <summary>
        /// Parse the given string as an uniform resource location.
        /// </summary>
        /// <param name="Text">A text representation of an uniform resource location.</param>
        /// <param name="URLScheme">An optional URL schemel.</param>
        public static URL Parse(String         Text,
                                URIScheme ?  URLScheme   = null)
        {

            if (TryParse(Text, out var url, URLScheme))
                return url;

            throw new ArgumentException("The given text representation of an uniform resource location is invalid: " + Text,
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse  (Text, URLScheme = null)

        /// <summary>
        /// Try to parse the given text as an uniform resource location.
        /// </summary>
        /// <param name="Text">A text representation of an uniform resource location.</param>
        public static URL? TryParse(String Text)
            => TryParse(Text, null);


        /// <summary>
        /// Try to parse the given text as an uniform resource location.
        /// </summary>
        /// <param name="Text">A text representation of an uniform resource location.</param>
        /// <param name="URLScheme">An optional URL schemel.</param>
        public static URL? TryParse(String         Text,
                                    URIScheme ?  URLScheme   = null)
        {

            if (TryParse(Text, out var url, URLScheme))
                return url;

            return null;

        }

        #endregion

        #region (static) TryParse  (Text, out URL, URLScheme = null)

        /// <summary>
        /// Try to parse the given text as an uniform resource location.
        /// </summary>
        /// <param name="Text">A text representation of an uniform resource location.</param>
        /// <param name="URL">The parsed uniform resource location.</param>
        public static Boolean TryParse(String Text, out URL URL)
            => TryParse(Text, out URL, null);


        /// <summary>
        /// Try to parse the given text as an uniform resource location.
        /// </summary>
        /// <param name="Text">A text representation of an uniform resource location.</param>
        /// <param name="URL">The parsed uniform resource location.</param>
        /// <param name="URLScheme">An optional URL schemel.</param>
        public static Boolean TryParse(String      Text,
                                       out URL     URL,
                                       URIScheme?  URLScheme)
        {

            #region Initial checks

            URL = default;

            if (Text is null)
                return false;

            Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            if (!Text.Contains("://"))
                Text = $"{URLScheme?.SchemeName ?? "https"}://{Text}";

            #endregion

            #region Scheme

            var schemeEnd = Text.IndexOf("://", StringComparison.Ordinal);

            // Note: TryParse(...) never grows the URL scheme registry, therefore it is
            //       safe to call with anything received over the wire.
            if (!URIScheme.TryParse(Text[..schemeEnd], out var scheme))
                return false;

            // Everything after "scheme://", parsed in the order defined by RFC 3986:
            // [ userinfo "@" ] host [ ":" port ] path [ "?" query ] [ "#" fragment ]
            var rest = Text[(schemeEnd + 3)..];

            #endregion

            #region Fragment

            String? fragment      = null;
            var     fragmentStart = rest.IndexOf('#');

            if (fragmentStart >= 0)
            {
                fragment  = rest[(fragmentStart + 1)..];
                rest      = rest[..fragmentStart];
            }

            #endregion

            #region Query string

            QueryString?  queryString  = null;
            var           queryStart   = rest.IndexOf('?');

            if (queryStart >= 0)
            {
                // Note: The query string is cut off before the path is split, so that
                //       a '/' inside the query can no longer end up within the path.
                queryString  = QueryString.Parse(rest[queryStart..]);
                rest         = rest[..queryStart];
            }

            #endregion

            #region Authority and path

            var pathStart  = rest.IndexOf('/');
            var authority  = pathStart >= 0 ? rest[..pathStart] : rest;
            var pathText   = pathStart >= 0 ? rest[pathStart..] : "/";

            #endregion

            #region User information

            String?  login       = null;
            String?  password    = null;
            var      userInfoEnd = authority.LastIndexOf('@');

            if (userInfoEnd >= 0)
            {

                var userInfo    = authority[..userInfoEnd];
                authority       = authority[(userInfoEnd + 1)..];

                var colonIndex  = userInfo.IndexOf(':');

                if (colonIndex > 0)
                {
                    login     = userInfo[..colonIndex];
                    password  = userInfo[(colonIndex + 1)..];
                }
                else
                    login     = userInfo;

            }

            #endregion

            #region Host and port

            if (!TrySplitHostAndPort(authority, out var hostText, out var portText))
                return false;

            if (!URLHost.TryParse(hostText, out var host, out _))
                return false;

            IPPort? port = null;

            if (portText is not null)
            {

                if (!IPPort.TryParse(portText, out var parsedPort))
                    return false;

                port = parsedPort;

            }

            #endregion

            if (!HTTPPath.TryParse(pathText, out var path))
                return false;

            URL = new URL(
                      Text,
                      scheme,
                      login,
                      password,
                      host,
                      port ?? scheme.DefaultPort,
                      path,
                      queryString,
                      fragment
                  );

            return true;

        }

        #endregion

        #region (private static) TrySplitHostAndPort(Text, out HostText, out PortText)

        /// <summary>
        /// Split the "host[:port]" part of an authority.
        ///
        /// Note: The ':' separating the port must not be searched for within an IPv6
        ///       literal, as that is full of them.
        /// </summary>
        /// <param name="Text">The "host[:port]" part of an authority.</param>
        /// <param name="HostText">The host.</param>
        /// <param name="PortText">The optional TCP/IP port.</param>
        private static Boolean TrySplitHostAndPort(String        Text,
                                                   out String    HostText,
                                                   out String?   PortText)
        {

            HostText  = Text;
            PortText  = null;

            var searchFrom = 0;

            if (Text.StartsWith('['))
            {

                var closingBracket = Text.IndexOf(']');

                if (closingBracket < 0)
                    return false;

                searchFrom = closingBracket + 1;

            }

            var colon = Text.IndexOf(':', searchFrom);

            if (colon >= 0)
            {

                HostText  = Text[..colon];
                PortText  = Text[(colon + 1)..];

                if (PortText.Length == 0)
                    PortText = null;

            }

            return true;

        }

        #endregion


        #region (static) Concat    (Texts)

        /// <summary>
        /// Parse the given string fragments as an uniform resource location.
        /// </summary>
        /// <param name="Texts">An enumeration of fragments of a text representation of an uniform resource location.</param>
        public static URL Concat(params IEnumerable<Object?> Texts)
        {

            if (TryConcat(Texts, out var url))
                return url;

            throw new ArgumentException("The given fragments of a text representation of an uniform resource location is invalid: " + Texts.AggregateWith("|"),
                                        nameof(Texts));

        }

        #endregion

        #region (static) TryConcat (Texts)

        /// <summary>
        /// Try to parse the given string fragments as an uniform resource location.
        /// </summary>
        /// <param name="Texts">An enumeration of fragments of a text representation of an uniform resource location.</param>
        public static URL? TryConcat(params IEnumerable<Object?> Texts)
        {

            if (TryConcat(Texts, out var url))
                return url;

            return null;

        }

        #endregion

        #region (static) TryConcat (Texts, out URL)

        /// <summary>
        /// Try to parse the given string fragments as an uniform resource location.
        /// </summary>
        /// <param name="Texts">An enumeration of fragments of a text representation of an uniform resource location.</param>
        /// <param name="URL">The parsed uniform resource location.</param>
        public static Boolean TryConcat(IEnumerable<Object?> Texts, out URL URL)

            => TryParse(String.Concat(Texts), out URL);

        #endregion


        #region Clone()

        /// <summary>
        /// Clone this uniform resource location.
        /// </summary>
        public URL Clone()

            => new (
                   InternalId.CloneString(),
                   Scheme,
                   Login?.    CloneString(),
                   Password?. CloneString(),
                   Host.      Clone(),
                   Port?.     Clone(),
                   Path.      Clone(),

                   // Note: QueryString is mutable (Add(...) modifies the instance and
                   //       returns it), so it must not be shared between two URLs.
                   QueryString is not null
                       ? QueryString.Parse(QueryString.ToString())
                       : null,

                   Fragment?. CloneString()
               );

        #endregion


        #region (private) WithPathAndQuery(Path, QueryString)

        /// <summary>
        /// Return a copy of this uniform resource location having the given path and query string.
        /// The scheme, the user information and the host of the original text are kept as-is,
        /// so that e.g. an omitted default port does not suddenly appear.
        /// </summary>
        /// <param name="Path">The new path.</param>
        /// <param name="QueryString">The new query string.</param>
        private URL WithPathAndQuery(HTTPPath      Path,
                                     QueryString?  QueryString)
        {

            var text       = InternalId ?? "";
            var schemeEnd  = text.IndexOf("://", StringComparison.Ordinal);
            var headStart  = schemeEnd >= 0 ? schemeEnd + 3 : 0;
            var headEnd    = text.IndexOfAny(pathQueryOrFragment, headStart);

            // "scheme://[userinfo@]host[:port]"
            var head       = headEnd >= 0 ? text[..headEnd] : text;

            return new URL(
                       String.Concat(
                           head,
                           Path.ToString(),
                           QueryString?.ToString() ?? "",
                           Fragment is not null ? $"#{Fragment}" : ""
                       ),
                       Scheme,
                       Login,
                       Password,
                       Host,
                       Port,
                       Path,
                       QueryString,
                       Fragment
                   );

        }

        #endregion


        #region Operator overloading

        #region Operator == (URL1, URL2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URL1">A uniform resource location.</param>
        /// <param name="URL2">Another uniform resource location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (URL URL1,
                                           URL URL2)

            => URL1.Equals(URL2);

        #endregion

        #region Operator != (URL1, URL2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URL1">A uniform resource location.</param>
        /// <param name="URL2">Another uniform resource location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (URL URL1,
                                           URL URL2)

            => !(URL1 == URL2);

        #endregion

        #region Operator <  (URL1, URL2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URL1">A uniform resource location.</param>
        /// <param name="URL2">Another uniform resource location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (URL URL1,
                                          URL URL2)

            => URL1.CompareTo(URL2) < 0;

        #endregion

        #region Operator <= (URL1, URL2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URL1">A uniform resource location.</param>
        /// <param name="URL2">Another uniform resource location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (URL URL1,
                                           URL URL2)

            => !(URL1 > URL2);

        #endregion

        #region Operator >  (URL1, URL2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URL1">A uniform resource location.</param>
        /// <param name="URL2">Another uniform resource location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (URL URL1,
                                          URL URL2)

            => URL1.CompareTo(URL2) > 0;

        #endregion

        #region Operator >= (URL1, URL2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URL1">A uniform resource location.</param>
        /// <param name="URL2">Another uniform resource location.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (URL URL1,
                                           URL URL2)

            => !(URL1 < URL2);

        #endregion


        #region Operator  + (URL, PathSuffix)

        /// <summary>
        /// Combines a uniform resource location with a path suffix.
        /// </summary>
        /// <param name="URL">A uniform resource location.</param>
        /// <param name="PathSuffix">A path suffix which will be added to the existing path.</param>
        public static URL operator + (URL       URL,
                                      HTTPPath  PathSuffix)

            => URL.WithPathAndQuery(URL.Path + PathSuffix.ToString(),
                                    URL.QueryString);


        /// <summary>
        /// Combines a uniform resource location with a path suffix, or, when the given
        /// suffix starts with a '?', with a query string.
        /// </summary>
        /// <param name="URL">A uniform resource location.</param>
        /// <param name="PathSuffix">A path suffix which will be added to the existing path, or a query string.</param>
        public static URL operator + (URL     URL,
                                      String  PathSuffix)
        {

            if (!PathSuffix.StartsWith('?'))
                return URL.WithPathAndQuery(URL.Path + PathSuffix,
                                            URL.QueryString);

            // A query string suffix must not end up within the path. When the URL already
            // has a query string, both are merged instead of being concatenated into an
            // invalid "?a=1?b=2".
            var existingQuery = URL.QueryString?.ToString();

            return URL.WithPathAndQuery(
                       URL.Path,
                       QueryString.Parse(
                           existingQuery.IsNullOrEmpty()
                               ? PathSuffix
                               : $"{existingQuery}&{PathSuffix[1..]}"
                       )
                   );

        }


        /// <summary>
        /// Combines a uniform resource location with a path suffix, or, when the given
        /// suffix starts with a '?', with a query string.
        /// </summary>
        /// <param name="URL">A uniform resource location.</param>
        /// <param name="PathSuffix">A path suffix which will be added to the existing path, or a query string.</param>
        public static URL? operator + (URL?    URL,
                                       String  PathSuffix)

            => URL.HasValue
                   ? URL.Value + PathSuffix
                   : null;

        #endregion

        #endregion

        #region IComparable<URL> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two uniform resource locations for equality.
        /// </summary>
        /// <param name="Object">An uniform resource location to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is URL url
                   ? CompareTo(url)
                   : throw new ArgumentException("The given object is not an uniform resource location!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(URL)

        /// <summary>
        /// Compares two uniform resource locations for equality.
        /// </summary>
        /// <param name="URL">An uniform resource location to compare with.</param>
        public Int32 CompareTo(URL URL)
        {

            // Note: Must order by the very same components Equals(URL) compares, otherwise
            //       CompareTo(...) == 0 and Equals(...) could disagree.
            //       A missing scheme sorts before any scheme.
            var c = Scheme is null
                        ? (URL.Scheme is null ? 0 : -1)
                        : Scheme.CompareTo(URL.Scheme);
            if (c != 0) return c;

            c = Host.CompareTo(URL.Host);
            if (c != 0) return c;

            c = Nullable.Compare(Port, URL.Port);
            if (c != 0) return c;

            c = Path.CompareTo(URL.Path);
            if (c != 0) return c;

            c = String.CompareOrdinal(Login,    URL.Login);
            if (c != 0) return c;

            c = String.CompareOrdinal(Password, URL.Password);
            if (c != 0) return c;

            c = String.CompareOrdinal(QueryString?.ToString(), URL.QueryString?.ToString());
            if (c != 0) return c;

            return String.CompareOrdinal(Fragment, URL.Fragment);

        }

        #endregion

        #endregion

        #region IEquatable<URL> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two uniform resource locations for equality.
        /// </summary>
        /// <param name="Object">An uniform resource location to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is URL url &&
                   Equals(url);

        #endregion

        #region Equals(URL)

        /// <summary>
        /// Compares two uniform resource locations for equality.
        /// </summary>
        /// <param name="URL">An uniform resource location to compare with.</param>
        public Boolean Equals(URL URL)

               // Scheme and host are case-insensitive, everything else is not, see RFC 3986
               // section 6.2.2.1. URIScheme and URLHost both compare case-insensitively,
               // so both are covered by their own comparisons. The '==' of URIScheme also
               // copes with a missing scheme on either side.
            => Scheme ==     URL.Scheme        &&
               Host.    Equals(URL.Host)      &&
               Port.    Equals(URL.Port)      &&
               Path.    Equals(URL.Path)      &&

               String.Equals(Login,                  URL.Login,                  StringComparison.Ordinal) &&
               String.Equals(Password,               URL.Password,               StringComparison.Ordinal) &&
               String.Equals(QueryString?.ToString(), URL.QueryString?.ToString(), StringComparison.Ordinal) &&
               String.Equals(Fragment,               URL.Fragment,               StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            // Note: This must use the very same comparison as Equals(URL), otherwise two
            //       equal URLs could end up with different hash codes.
            => HashCode.Combine(
                   Scheme,
                   Host,
                   Port,
                   Path,
                   Login,
                   Password,
                   QueryString?.ToString(),
                   Fragment
               );

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
