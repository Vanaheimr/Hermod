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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class Ext
    {

        public static String Repeat(this String Text, Int64 Times)
        {
            return Text.Repeat((UInt64)Math.Max(0L, Times));
        }

        public static String Repeat(this String Text, UInt32 Times)
        {
            return Text.Repeat((UInt64)Times);
        }

        public static String Repeat(this String Text, Int32 Times)
        {
            return Text.Repeat((UInt64)Math.Max(0, Times));
        }

        public static String Repeat(this String Text, UInt64 Times)
        {

            var result = "";

            for (var i = 0u; i < Times; i++)
                result += Text;

            return result;

        }

    }

    public static class NewLine
    {

        public static String Concat(params String[] Lines)
        {
            return Lines.Aggregate((a, b) => a + Environment.NewLine + b);
        }

    }

}
