/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// DNS Resource Record Types
    /// </summary>
    public enum DNSResourceRecordTypes : int
    {

        A               = 1,
        NS              = 2,
        CNAME           = 5,
        SOA             = 6,
        MB              = 7,
        MG              = 8,
        MR              = 9,
        NULL            = 10,
        WKS             = 11,
        PTR             = 12,
        HINFO           = 13,
        MINFO           = 14,
        MX              = 15,
        TXT             = 16,
        AAAA            = 28,
        CERT            = 37,
        SSHFP           = 44,

        ANY             = 255

    }

}
