/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod
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

namespace de.ahzf.Hermod.Sockets.RawIP
{

    public class WinsockIoctl
    {

        /// <summary>
        /// An interface query takes the socket address of a remote destination and
        /// returns the local interface that destination is reachable on.
        /// </summary>
        public const int SIO_ROUTING_INTERFACE_QUERY = -939524076;  // otherwise equal to 0xc8000014

        /// <summary>
        /// The address list query returns a list of all local interface addresses.
        /// </summary>
        public const int SIO_ADDRESS_LIST_QUERY = 0x48000016;

    }

}
