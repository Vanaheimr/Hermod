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

#region Usings

using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS SVCB resource records.
    /// </summary>
    public static class DNS_SVCB_Extensions
    {

        #region CacheSVCB(this DNSClient, DomainName, Target, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS SVCB record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this SVCB resource record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="TargetName">The domain name of the target host.</param>
        /// <param name="SVCParams">The SVCB parameters.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheSVCB(this DNSClient             DNSClient,
                                     DomainName                 DomainName,
                                     UInt16                     Priority,
                                     String                     TargetName,
                                     IEnumerable<SVCParameter>  SVCParams,
                                     DNSQueryClasses            Class        = DNSQueryClasses.IN,
                                     TimeSpan?                  TimeToLive   = null)
        {

            var dnsRecord = new SVCB(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Priority,
                                TargetName,
                                SVCParams
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// A DNS SVCB parameter.
    /// </summary>
    public struct SVCParameter
    {

        #region Properties

        /// <summary>
        /// The parameter key.
        /// </summary>
        public UInt16  Key    { get; }

        /// <summary>
        /// The parameter value.
        /// </summary>
        public Byte[]  Value  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SVCB parameter.
        /// </summary>
        /// <param name="Key">The parameter key.</param>
        /// <param name="Value">The parameter value.</param>
        public SVCParameter(UInt16  Key,
                        Byte[]  Value)
        {

            this.Key    = Key;
            this.Value  = Value ?? throw new ArgumentNullException(nameof(Value), "The given value must not be null!");

        }

        #endregion

    }


    /// <summary>
    /// The DNS Service Binding (SVCB) resource record.
    /// https://www.rfc-editor.org/rfc/rfc9460
    /// </summary>
    public class SVCB : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Service Binding (SVCB) resource record type identifier.
        /// </summary>
        public const DNSResourceRecords TypeId = DNSResourceRecords.SVCB;

        #endregion

        #region Properties

        /// <summary>
        /// The priority of this target host.
        /// </summary>
        public UInt16                 Priority   { get; }

        /// <summary>
        /// The domain name of the target host.
        /// </summary>
        public String                 TargetName { get; }

        /// <summary>
        /// The SVCB parameters.
        /// </summary>
        public IEnumerable<SVCParameter>  SVCParams  { get; }

        #endregion

        #region Constructors

        #region SVCB(Stream)

        /// <summary>
        /// Create a new SVCB resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the SVCB resource record data.</param>
        public SVCB(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Priority = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);

            this.TargetName = DNSTools.ExtractName(Stream);

            var svcParams = new List<SVCParameter>();

            while (true)
            {

                int b1 = Stream.ReadByte();

                if (b1 == -1)
                    break;

                int b2 = Stream.ReadByte();

                if (b2 == -1)
                    break;

                UInt16 key = (UInt16) ((b1 << 8) | b2);

                b1 = Stream.ReadByte();

                b2 = Stream.ReadByte();

                if (b2 == -1)
                    break;

                UInt16 len = (UInt16) ((b1 << 8) | b2);

                Byte[] value = new Byte[len];

                if (Stream.Read(value, 0, len) != len)
                    break;

                svcParams.Add(new SVCParameter(key, value));

            }

            this.SVCParams = svcParams.AsReadOnly();

        }

        #endregion

        #region SVCB(DomainName, Stream)

        /// <summary>
        /// Create a new SVCB resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this SVCB resource record.</param>
        /// <param name="Stream">A stream containing the SVCB resource record data.</param>
        public SVCB(DomainName  DomainName,
                    Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            this.Priority    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.TargetName  = DNSTools.ExtractName(Stream);

            var svcParams    = new List<SVCParameter>();

            while (true)
            {

                int b1 = Stream.ReadByte();

                if (b1 == -1)
                    break;

                int b2 = Stream.ReadByte();

                if (b2 == -1)
                    break;

                UInt16 key = (UInt16) ((b1 << 8) | b2);

                b1 = Stream.ReadByte();

                b2 = Stream.ReadByte();

                if (b2 == -1)
                    break;

                UInt16 len = (UInt16) ((b1 << 8) | b2);

                Byte[] value = new Byte[len];

                if (Stream.Read(value, 0, len) != len)
                    break;

                svcParams.Add(new SVCParameter(key, value));

            }

            this.SVCParams = svcParams.AsReadOnly();

        }

        #endregion

        #region SVCB(DomainName, Class, TimeToLive, Priority, TargetName, SVCParams)

        /// <summary>
        /// Create a new DNS SVCB record.
        /// </summary>
        /// <param name="DomainName">The domain name of this SVCB record.</param>
        /// <param name="Class">The DNS query class of this SVCB record.</param>
        /// <param name="TimeToLive">The time to live of this SVCB record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="TargetName">The domain name of the target host.</param>
        /// <param name="SVCParams">The SVCB parameters.</param>
        public SVCB(DomainName                 DomainName,
                    DNSQueryClasses            Class,
                    TimeSpan                   TimeToLive,
                    UInt16                     Priority,
                    String                     TargetName,
                    IEnumerable<SVCParameter>  SVCParams)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive,
                   BuildPresentation(
                       Priority,
                       TargetName,
                       SVCParams
                   ))

        {

            this.Priority    = Priority;
            this.TargetName  = TargetName;
            this.SVCParams   = SVCParams ?? [];

        }

        #endregion

        #endregion


        #region (private) BuildPresentation(Priority, TargetName, SVCParams)

        private static String BuildPresentation(UInt16                 Priority,
                                                String                 TargetName,
                                                IEnumerable<SVCParameter>  SVCParams)
        {

            var sb = new StringBuilder();

            sb.Append(Priority);
            sb.Append(' ');
            sb.Append(TargetName);

            foreach (var param in SVCParams.OrderBy(p => p.Key)) // Ensure sorted by key for consistency
            {

                sb.Append(' ');
                sb.Append(GetKeyName(param.Key));

                if (param.Value.Length > 0)
                {
                    sb.Append('=');
                    sb.Append('"');
                    sb.Append(EscapeValue(param.Value));
                    sb.Append('"');
                }

            }

            return sb.ToString();

        }

        #endregion

        #region (private) GetKeyName(Key)

        private static String GetKeyName(UInt16 Key)

            => Key switch {
                   0  => "mandatory",
                   1  => "alpn",
                   2  => "no-default-alpn",
                   3  => "port",
                   4  => "ipv4hint",
                   6  => "ipv6hint",
                   _  => "key" + Key
            };

        #endregion

        #region (private) EscapeValue(Value)

        private static String EscapeValue(Byte[] Value)
        {

            var sb = new StringBuilder();

            foreach (var b in Value)
            {

                if (b >= 32 && b <= 126 && b != (byte) '\\' && b != (byte) '"')
                    sb.Append((char) b);

                else
                {
                    sb.Append('\\');
                    sb.Append((b / 100).ToString("D1"));
                    sb.Append(((b / 10) % 10).ToString("D1"));
                    sb.Append((b % 10).ToString("D1"));
                }

            }

            return sb.ToString();

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Priority={Priority}, Target={TargetName}, SVCParams={string.Join(", ", SVCParams.Select(p => $"{GetKeyName(p.Key)}={(p.Value.Length > 0 ? EscapeValue(p.Value) : "")}"))}, {base.ToString()}";

        #endregion

    }

}
