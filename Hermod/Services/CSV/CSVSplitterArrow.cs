/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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

#region Usings

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;

using eu.Vanaheimr.Hermod.Sockets.TCP;
using eu.Vanaheimr.Hermod.Datastructures;
using eu.Vanaheimr.Styx;

#endregion

namespace eu.Vanaheimr.Hermod.Services.CSV
{

    public static class CSVSplitterArrowExtention
    {

        public static CSVSplitterArrow CSVSplitter(this IArrowSender<Byte[]> In,
                                                   Char[] Separators)
        {
            return new CSVSplitterArrow(Separators, In);
        }

    }

    public class CSVSplitterArrow : FunctionArrow<Byte[], String[]>
    {

        public CSVSplitterArrow(Char[] Separators, IArrowSender<Byte[]> In = null)
            : base(Bytes => {

                       return Encoding.UTF8.
                                  GetString(Bytes).
                                  Split(Separators, StringSplitOptions.None);

                   })

        {

            if (In != null)
                In.SendTo(this);

        }

    }

}
