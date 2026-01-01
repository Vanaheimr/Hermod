/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.PKI
{

    public sealed record NameConstraintsInput(IEnumerable<DomainName>?     PermittedDNS     = null,  // e.g. ["example.com", "sub.example.net"]
                                              IEnumerable<DomainName>?     ExcludedDNS      = null,
                                              IEnumerable<IPAddressCidr>?  PermittedIP      = null,  // CIDR blocks
                                              IEnumerable<IPAddressCidr>?  ExcludedIP       = null,
                                              IEnumerable<String>?         PermittedEmail   = null,  // rfc822Name (domains like "example.com" or mailbox "user@example.com")
                                              IEnumerable<String>?         ExcludedEmail    = null);

}
