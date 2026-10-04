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

using org.GraphDefined.Vanaheimr.Hermod;
public class IPv6FragmentHeader
{

    private byte fragmentNextHeader;
    private byte fragmentReserved;
    private ushort fragmentOffset;
    private uint fragmentId;
    
    static public int Ipv6FragmentHeaderLength = 8;

    /// <summary>
    /// Simple constructor that initializes the member properties.
    /// </summary>
    public IPv6FragmentHeader()
    {
        fragmentNextHeader  = 0;
        fragmentReserved    = 0;
        fragmentOffset      = 0;
        fragmentId          = 0;
    }

    /// <summary>
    /// Gets and sets the next protocol value field.
    /// </summary>
    public byte NextHeader
    {
        get
        {
            return fragmentNextHeader;
        }
        set
        {
            fragmentNextHeader = value;
        }
    }

    /// <summary>
    /// Gets and sets the reserved field. Performs the necessary byte order conversion.
    /// </summary>
    public byte Reserved
    {
        get
        {
            return fragmentReserved;
        }
        set
        {
            fragmentReserved = value;
        }
    }

    /// <summary>
    /// Gets and sets the offset field. Performs the necessary byte order conversion.
    /// </summary>
    public ushort Offset
    {
        get
        {
            return (ushort)NetworkingHelpers.NetworkToHostOrder((short)fragmentOffset);
        }
        set
        {
            fragmentOffset = (ushort)NetworkingHelpers.HostToNetworkOrder((short)value);
        }
    }

    /// <summary>
    /// Gets and sets the id property. Performs the necessary byte order conversion.
    /// </summary>
    public uint Id
    {
        get
        {
            return (uint)NetworkingHelpers.NetworkToHostOrder((int)fragmentId);
        }
        set
        {
            fragmentId = (uint)NetworkingHelpers.HostToNetworkOrder((int)value);
        }
    }

}
