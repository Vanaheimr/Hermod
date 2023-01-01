/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Warden
{

    public class WardenProperties
    {

        #region Data

        private readonly Dictionary<String, Object> _Properties;

        #endregion

        #region Constructor(s)

        public WardenProperties()
        {
            this._Properties = new Dictionary<String, Object>();
        }

        #endregion


        public WardenProperties Set(String Key, Object Value)
        {

            lock (_Properties)
            {

                if (Value == null)
                {
                    if (_Properties.ContainsKey(Key))
                        _Properties.Remove(Key);
                }

                else
                {

                    if (!_Properties.ContainsKey(Key))
                        _Properties.Add(Key, Value);

                    else
                        _Properties[Key] = Value;

                }

            }

            return this;

        }

        public Object Get(String Key)
        {

            lock (_Properties)
            {

                if (_Properties.ContainsKey(Key))
                    return _Properties[Key];

                else
                    return null;

            }

        }

        public WardenProperties Remove(String Key)
        {

            lock (_Properties)
            {

                if (_Properties.ContainsKey(Key))
                    _Properties.Remove(Key);

            }

            return this;

        }

    }

}
