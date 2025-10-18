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
    /// Extensions methods for DNS HTTPS resource records.
    /// </summary>
    public static class DNS_HTTPS_Extensions
    {

        #region CacheHTTPS(this DNSClient, DomainName, Class = IN, TimeToLive = 365days, Priority, TargetName, SVCParameters, ...)

        /// <summary>
        /// Add a DNS HTTPS record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Class">The DNS query class of this HTTPS record.</param>
        /// <param name="TimeToLive">The time to live of this HTTPS record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="TargetName">The domain name of the target host.</param>
        /// <param name="SVCParameters">The HTTPS parameters.</param>
        public static void CacheHTTPS(this DNSClient             DNSClient,
                                      DomainName                 DomainName,
                                      UInt16                     Priority,
                                      DomainName                 TargetName,
                                      IEnumerable<SVCParameter>  SVCParameters,
                                      DNSQueryClasses            Class        = DNSQueryClasses.IN,
                                      TimeSpan?                  TimeToLive   = null)
        {

            var dnsRecord = new HTTPS(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Priority,
                                TargetName,
                                SVCParameters
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS HTTPS resource record.
    /// https://www.rfc-editor.org/rfc/rfc9460
    /// </summary>
    public class HTTPS : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS HTTPS resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.HTTPS;

        #endregion

        #region Properties

        /// <summary>
        /// The priority of this target host.
        /// </summary>
        public UInt16                     Priority        { get; }

        /// <summary>
        /// The domain name of the target host.
        /// </summary>
        public DomainName                 TargetName      { get; }

        /// <summary>
        /// The HTTPS parameters.
        /// </summary>
        public IEnumerable<SVCParameter>  SVCParameters    { get; }

        #endregion

        #region Constructors

        #region HTTPS(Stream)

        /// <summary>
        /// Create a new HTTPS resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the HTTPS resource record data.</param>
        public HTTPS(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Priority    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.TargetName  = DNSTools.ExtractDomainName(Stream);

            var svcParams    = new List<SVCParameter>();

            while (true)
            {

                var b1 = Stream.ReadByte();

                if (b1 == -1)
                    break;

                var b2 = Stream.ReadByte();

                if (b2 == -1)
                    break;

                var key = (UInt16) ((b1 << 8) | b2);
                b1 = Stream.ReadByte();
                b2 = Stream.ReadByte();

                if (b2 == -1)
                    break;

                var len   = (UInt16) ((b1 << 8) | b2);
                var value = new Byte[len];

                if (Stream.Read(value, 0, len) != len)
                    break;

                svcParams.Add(
                    new SVCParameter(
                        key,
                        value
                    )
                );

            }

            this.SVCParameters = svcParams.AsReadOnly();

        }

        #endregion

        #region HTTPS(DomainName, Stream)

        /// <summary>
        /// Create a new HTTPS resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this HTTPS resource record.</param>
        /// <param name="Stream">A stream containing the HTTPS resource record data.</param>
        public HTTPS(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Priority    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.TargetName  = DNSTools.ExtractDomainName(Stream);

            var svcParams    = new List<SVCParameter>();

            while (true)
            {

                var b1 = Stream.ReadByte();

                if (b1 == -1)
                    break;

                var b2 = Stream.ReadByte();

                if (b2 == -1)
                    break;

                var key = (UInt16) ((b1 << 8) | b2);
                b1 = Stream.ReadByte();
                b2 = Stream.ReadByte();

                if (b2 == -1)
                    break;

                var len   = (UInt16) ((b1 << 8) | b2);
                var value = new Byte[len];

                if (Stream.Read(value, 0, len) != len)
                    break;

                svcParams.Add(
                    new SVCParameter(
                        key,
                        value
                    )
                );

            }

            this.SVCParameters = svcParams.AsReadOnly();

        }

        #endregion

        #region HTTPS(DomainName, Class, TimeToLive, Priority, TargetName, SVCParameters)

        /// <summary>
        /// Create a new DNS HTTPS record.
        /// </summary>
        /// <param name="DomainName">The domain name of this HTTPS record.</param>
        /// <param name="Class">The DNS query class of this HTTPS record.</param>
        /// <param name="TimeToLive">The time to live of this HTTPS record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="TargetName">The domain name of the target host.</param>
        /// <param name="SVCParameters">The HTTPS parameters.</param>
        public HTTPS(DomainName                 DomainName,
                     DNSQueryClasses            Class,
                     TimeSpan                   TimeToLive,
                     UInt16                     Priority,
                     DomainName                 TargetName,
                     IEnumerable<SVCParameter>  SVCParameters)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Priority       = Priority;
            this.TargetName     = TargetName;
            this.SVCParameters  = SVCParameters;

        }

        #endregion

        #endregion


        #region (private) BuildPresentation(Priority, TargetName, SVCParameters)

        private static String BuildPresentation(UInt16                     Priority,
                                                DomainName                 TargetName,
                                                IEnumerable<SVCParameter>  SVCParameters)
        {

            var sb = new StringBuilder();

            sb.Append(Priority);
            sb.Append(' ');
            sb.Append(TargetName);

            foreach (var param in SVCParameters.OrderBy(p => p.Key))
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

                if (b >= 32 && b <= 126 && b != (Byte)'\\' && b != (Byte)'"')
                    sb.Append((Char) b);

                else
                {
                    sb.Append('\\');
                    sb.Append( (b / 100).      ToString("D1"));
                    sb.Append(((b /  10) % 10).ToString("D1"));
                    sb.Append( (b        % 10).ToString("D1"));
                }

            }

            return sb.ToString();

        }

        #endregion


        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression (true by default).</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            var tempStream = new MemoryStream();

            tempStream.WriteUInt16BE(Priority);

            // TargetName (variable, with compression)
            int targetOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;  // +2 for RDLength
            TargetName.Serialize(
                tempStream,
                targetOffset,
                UseCompression,
                CompressionOffsets
            );

            // SVC parameters sorted by their keys
            foreach (var svcParameter in SVCParameters.OrderBy(kvp => kvp.Key))
            {
                tempStream.WriteUInt16BE(svcParameter.Key);
                tempStream.WriteUInt16BE(svcParameter.Value.Length);
                tempStream.Write        (svcParameter.Value, 0, svcParameter.Value.Length);
            }

            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH (2 bytes)
            Stream.WriteUInt16BE(tempStream.Length);

            // Copy RDATA to main stream
            tempStream.Position = 0;
            tempStream.CopyTo(Stream);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Priority={Priority}, Target={TargetName}, SvcParams={string.Join(", ", SVCParameters.Select(p => $"{GetKeyName(p.Key)}={(p.Value.Length > 0 ? EscapeValue(p.Value) : "")}"))}, {base.ToString()}";

        #endregion

    }

}