/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Threading;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Warden
{

    /// <summary>
    /// A delegate for checking whether it is time to run a serive check.
    /// </summary>
    /// <param name="Timestamp">The current timestamp.</param>
    /// <param name="ServiceProperties">Some properties which might enable or disable service checks.</param>
    public delegate Boolean RunCheckDelegate(DateTime               Timestamp,
                                             WardenProperties       ServiceProperties);

    /// <summary>
    /// A delegate for checking whether it is time to run a serive check.
    /// </summary>
    /// <param name="Timestamp">The current timestamp.</param>
    public delegate Boolean TimeCheckDelegate(DateTime              Timestamp);

    /// <summary>
    /// A delegate for checking whether to run a serive check.
    /// </summary>
    /// <param name="ServiceProperties">Some properties which might enable or disable service checks.</param>
    public delegate Boolean PropertyCheckDelegate(WardenProperties  ServiceProperties);



    /// <summary>
    /// A delegate representing a warden service check.
    /// </summary>
    /// <param name="Timestamp">The current timestamp.</param>
    /// <param name="DNSClient">An optional DNS client for warden checks requiring network access.</param>
    /// <param name="Entity">An entity to check.</param>
    /// <param name="CancellationToken">A cancellation token.</param>
    public delegate Task ServiceCheckDelegate(DateTime           Timestamp,
                                              DNSClient          DNSClient,
                                              Object             Entity,
                                              CancellationToken  CancellationToken);

    /// <summary>
    /// A delegate representing a warden service check.
    /// </summary>
    /// <param name="Timestamp">The current timestamp.</param>
    /// <param name="DNSClient">An optional DNS client for warden checks requiring network access.</param>
    /// <param name="Entity">An entity to check.</param>
    /// <param name="CancellationToken">A cancellation token.</param>
    public delegate Task<TResult> ServiceCheckDelegate<TResult>(DateTime           Timestamp,
                                                                DNSClient          DNSClient,
                                                                Object             Entity,
                                                                CancellationToken  CancellationToken);

}
