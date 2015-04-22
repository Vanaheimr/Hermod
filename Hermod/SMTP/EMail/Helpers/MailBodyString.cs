/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.BouncyCastle;
using Org.BouncyCastle.Bcpg;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    public class MailBodyString
    {

        private readonly String[] _Lines;

        public IEnumerable<String> Lines
        {
            get
            {
                return _Lines;
            }
        }


        public MailBodyString(String Lines)
        {
            this._Lines = Lines.Replace("\r\n", "\n").Split(new Char[] { '\n' }, StringSplitOptions.None);
        }

        public MailBodyString(IEnumerable<String> Lines)
        {

            this._Lines = Lines != null
                              ? Lines.ToArray()
                              : new String[0];

        }

    }

}
