/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Collections.Generic;
using System.Net.NetworkInformation;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public static class DNSTools
    {

        public static Byte[] ExtractByteArray(Stream DNSStream, UInt32 LengthOfSegment)
        {

            if (LengthOfSegment > 0)
            {
                var ByteArray = new Byte[LengthOfSegment];
                DNSStream.Read(ByteArray, 0, (Int32) LengthOfSegment);
                return ByteArray;
            }

            return new Byte[0];

        }

        public static String ExtractName(Stream DNSStream)
        {

            var DNSName          = new StringBuilder();
            var LengthOfSegment  = 0;
            var OldPosition      = 0L;
            var Alias            = String.Empty;
            var buffer           = new Byte[512];

            do
            {

                LengthOfSegment = (DNSStream.ReadByte() & Byte.MaxValue);

                if (LengthOfSegment > 0)
                {

                    if (DNSName.Length > 0)
                        DNSName.Append(".");

                    // RDATA Compression
                    if ((LengthOfSegment & 0xC0) == 0xC0)
                    {

                        OldPosition         = DNSStream.Position;
                        DNSStream.Position  = ((LengthOfSegment & 0x3F) << 8) | (DNSStream.ReadByte() & Byte.MaxValue);
                        Alias               = ExtractName(DNSStream);
                        DNSStream.Position  = OldPosition + 1;

                        return Alias;

                    }

                    else
                    {
                        DNSStream.Read(buffer, 0, LengthOfSegment);
                        DNSName.Append(Encoding.ASCII.GetString(buffer, 0, LengthOfSegment));
                    }

                }

            }
            while (LengthOfSegment > 0);

            return DNSName.ToString();

        }

        public static DNSClient GoogleDNS()
        {
            return new DNSClient(IPv4Address.Parse("8.8.8.8"));
        }

    }

}
