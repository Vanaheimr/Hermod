﻿/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Different authentication types for HTTP authentication.
    /// </summary>
    [Flags]
    public enum HTTPAuthenticationTypes
    {

        /// <summary>
        /// No authentication required.
        /// </summary>
        None,

        /// <summary>
        /// Basic username+password authentication required.
        /// </summary>
        Basic,

        /// <summary>
        /// Bearer token authentication required.
        /// </summary>
        Bearer,

        /// <summary>
        /// Digest authentication required.
        /// </summary>
        Digest,

        /// <summary>
        /// Mutual authentication required.
        /// </summary>
        Mutual

    }

}
