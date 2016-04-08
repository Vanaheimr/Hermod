﻿/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A common interface for all kinds of IP Addresses.
    /// </summary>
    public interface IIPAddress : IComparable, IComparable<IIPAddress>, IEquatable<IIPAddress>
    {

        /// <summary>
        /// The length of the IP Address.
        /// </summary>
        Byte   Length { get; }

        /// <summary>
        /// Whether the IP address is an IPv4 multicast address.
        /// </summary>
        Boolean IsMulticast { get; }


        /// <summary>
        /// Return a byte array representation of this object.
        /// </summary>
        Byte[] GetBytes();


        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        Int32  GetHashCode();
        
        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        String ToString();

    }

}
