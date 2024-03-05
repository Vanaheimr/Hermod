/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Well-known protocols.
    /// </summary>
    public enum URLProtocols
    {

        /// <summary>
        /// HTTP
        /// </summary>
        http,

        /// <summary>
        /// HTTPS
        /// </summary>
        https,

        /// <summary>
        /// Web Sockets
        /// </summary>
        ws,

        /// <summary>
        /// Web Sockets Secure
        /// </summary>
        wss,

        /// <summary>
        /// Modbus/TCP
        /// </summary>
        modbus,

        /// <summary>
        /// Modbus/TLS (Modbus/TCP Security Protocol Specification)
        /// </summary>
        smodbus

    }


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
        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => !InternalId.IsNullOrEmpty();

        /// <summary>
        /// The length of the uniform resource location.
        /// </summary>
        public UInt64 Length

            => (UInt64) (InternalId?.Length ?? 0);

        public URLProtocols  Protocol       { get; }

        public String?       Login          { get; }

        public String?       Password       { get; }

        public HTTPHostname  Hostname       { get; }

        public IPPort?       Port           { get; }

        public HTTPPath      Path           { get; }

        public QueryString?  QueryString    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new uniform resource location based on the given string.
        /// </summary>
        /// <param name="String">The string representation of the uniform resource location.</param>
        private URL(String        String,
                    URLProtocols  Protocol,
                    HTTPHostname  Hostname,
                    IPPort?       Port,
                    HTTPPath      Path,
                    QueryString?  QueryString = null)
        {

            this.InternalId   = String;
            this.Protocol     = Protocol;
            this.Hostname     = Hostname;
            this.Port         = Port;
            this.Path         = Path;
            this.QueryString  = QueryString;

        }

        /// <summary>
        /// Create a new uniform resource location based on the given string.
        /// </summary>
        /// <param name="String">The string representation of the uniform resource location.</param>
        private URL(String        String,
                    URLProtocols  Protocol,
                    String?       Login,
                    String?       Password,
                    HTTPHostname  Hostname,
                    IPPort?       Port,
                    HTTPPath      Path,
                    QueryString?  QueryString = null)
        {

            this.InternalId   = String;
            this.Protocol     = Protocol;
            this.Login        = Login;
            this.Password     = Password;
            this.Hostname     = Hostname;
            this.Port         = Port;
            this.Path         = Path;
            this.QueryString  = QueryString;

        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an uniform resource location.
        /// </summary>
        /// <param name="Text">A text representation of an uniform resource location.</param>
        public static URL Parse(String Text)
        {

            if (TryParse(Text, out var url))
                return url;

            throw new ArgumentException("The given text representation of an uniform resource location is invalid: " + Text,
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as an uniform resource location.
        /// </summary>
        /// <param name="Text">A text representation of an uniform resource location.</param>
        public static URL? TryParse(String Text)
        {

            if (TryParse(Text, out var url))
                return url;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out URL)

        /// <summary>
        /// Try to parse the given text as an uniform resource location.
        /// </summary>
        /// <param name="Text">A text representation of an uniform resource location.</param>
        /// <param name="URL">The parsed uniform resource location.</param>
        public static Boolean TryParse(String Text, out URL URL)
        {

            Text  = Text.Trim();
            URL   = default;

            if (Text.IsNotNullOrEmpty())
            {
                try
                {

                    if (!Text.Contains("://"))
                        Text = "https://" + Text;

                    var elements = Text.Split('/');

                    URLProtocols  protocol      = URLProtocols.https;
                    HTTPHostname  hostname;
                    IPPort?       port          = null;
                    HTTPPath?     path          = null;
                    QueryString?  queryString   = null;


                    switch (elements[0])
                    {

                        case "http:":
                            protocol = URLProtocols.http;
                            break;

                        case "ws:":
                            protocol = URLProtocols.ws;
                            break;

                        case "wss:":
                            protocol = URLProtocols.wss;
                            break;

                        default:
                            protocol = URLProtocols.https;
                            break;

                    }

                    String? login     = null;
                    String? password  = null;

                    // Login (+ password) is given...
                    if (elements[2].Contains('@'))
                    {

                        var loginAndPassword = elements[2][..elements[2].IndexOf('@')];
                        elements[2]          = elements[2][ (elements[2].IndexOf('@') + 1)..];

                        if (loginAndPassword.IndexOf(':') > 0)
                        {
                            login     = loginAndPassword[..loginAndPassword.IndexOf(':')];
                            password  = loginAndPassword[ (loginAndPassword.IndexOf(':') + 1)..];
                        }
                        else
                            login     = loginAndPassword;

                    }

                    // A HTTP(S) port is given...
                    if (elements[2].Contains(':'))
                    {

                        if (!HTTPHostname.TryParse(elements[2][..elements[2].IndexOf(':')],  out hostname))
                            return false;

                        var portText = elements[2][(elements[2].IndexOf(':') + 1)..]?.Trim();

                        if (portText.IsNotNullOrEmpty())
                        {

                            if (IPPort.TryParse(elements[2][(elements[2].IndexOf(":") + 1)..], out IPPort _port))
                                port = _port;

                            else
                                return false;

                        }

                    }

                    else if (!HTTPHostname.TryParse(elements[2], out hostname))
                        return false;

                    if (elements.Length > 3)
                    {

                        if (elements[^1].Contains('?'))
                        {
                            queryString   = QueryString.Parse(elements[^1][elements[^1].IndexOf('?')..]);
                            var fullPath  = elements.Skip(3).AggregateWith("/");
                            path          = HTTPPath.TryParse(fullPath[..fullPath.IndexOf('?')]);
                        }

                        else
                            path          = HTTPPath.TryParse(elements.Skip(3).AggregateWith("/"));

                    }

                    if (port is null)
                    {
                        switch (protocol)
                        {

                            case URLProtocols.http:
                                port = IPPort.HTTP;
                                break;

                            case URLProtocols.https:
                                port = IPPort.HTTPS;
                                break;

                            case URLProtocols.ws:
                                port = IPPort.HTTP;
                                break;

                            case URLProtocols.wss:
                                port = IPPort.HTTPS;
                                break;

                        }
                    }

                    URL = new URL(
                              Text,
                              protocol,
                              login,
                              password,
                              hostname,
                              port,
                              path ?? HTTPPath.Parse("/"),
                              queryString
                          );

                    return true;

                }
                catch
                { }
            }

            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this uniform resource location.
        /// </summary>
        public URL Clone

            => new (
                   new String(InternalId?.ToCharArray()),
                   Protocol,
                   Hostname.Clone,
                   Port?.   Clone,
                   Path.    Clone
               );

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
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URL">A uniform resource location.</param>
        /// <param name="PathSuffix">A path suffix which will be added to the existing path.</param>
        /// <returns>true|false</returns>
        public static URL operator + (URL       URL,
                                      HTTPPath  PathSuffix)

            => new (URL.InternalId + "/" + PathSuffix.ToString(),
                    URL.Protocol,
                    URL.Hostname,
                    URL.Port,
                    URL.Path + PathSuffix);


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URL">A uniform resource location.</param>
        /// <param name="PathSuffix">A path suffix which will be added to the existing path.</param>
        /// <returns>true|false</returns>
        public static URL operator + (URL     URL,
                                      String  PathSuffix)

            => PathSuffix.StartsWith("?")

                   ? new (URL.InternalId + PathSuffix,
                          URL.Protocol,
                          URL.Hostname,
                          URL.Port,
                          URL.Path + PathSuffix)

                   : new (URL.InternalId + "/" + PathSuffix,
                          URL.Protocol,
                          URL.Hostname,
                          URL.Port,
                          URL.Path + PathSuffix);

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

            => String.Compare(InternalId,
                              URL.InternalId,
                              StringComparison.OrdinalIgnoreCase);

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

            => String.Equals(InternalId,
                             URL.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
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
