﻿/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Query Result/Response Codes
    /// </summary>
    public enum DNSResponseCodes : int
    {
        NoError         = 0,
        FormatError     = 1,
        ServerFailure   = 2,
        NameError       = 3,
        NotImplemented  = 4,
        Refused         = 5,
        Reserved        = 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15
    }

}
