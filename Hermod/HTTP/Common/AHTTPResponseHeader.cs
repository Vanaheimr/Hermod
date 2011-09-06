/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
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

#region Usings

using System;
using System.IO;
using System.Web;
using System.Text;
using System.Linq;
using System.Net.Mime;
using System.Collections.Generic;
using System.Collections.Specialized;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    public abstract class AHTTPResponseHeader
    {

        #region Data

        protected readonly Dictionary<String, Object> _HeaderFields;

        #endregion

        #region Constructor(s)

        #region AHTTPResponseHeader()

        public AHTTPResponseHeader()
        {
            _HeaderFields = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #endregion

    }

}
