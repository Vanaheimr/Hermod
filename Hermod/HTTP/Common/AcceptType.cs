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
using System.Globalization;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    public class AcceptType : IComparable<AcceptType>
    {

        /// <summary>
        /// Value between 0..1, default is 1
        /// </summary>
        private Double _Quality = 1;
        public Double Quality
        {
            get { return _Quality; }
            set { _Quality = value; }
        }


        private HTTPContentType _ContentType;
        public HTTPContentType ContentType
        {
            get { return _ContentType; }
            set { _ContentType = value; }
        }

        private UInt32 _PlaceOfOccurence;

        public AcceptType(String accept, UInt32 placeOfOccurence = 0)
        {

            if (accept.Contains(";"))
            {
                var split = accept.Split(';');
                try
                {
                    ContentType = new HTTPContentType(split[0]);
                    Double.TryParse(split[1].Replace("q=", "").Trim(), NumberStyles.Any, new CultureInfo("en"), out _Quality);
                }
                catch { }
            }

            else
            {
                try
                {
                    ContentType = new HTTPContentType(accept);
                }
                catch { }
            }

            _PlaceOfOccurence = placeOfOccurence;

        }





        #region IComparable<AcceptType> Members

        public int CompareTo(AcceptType other)
        {
            if (_Quality == other._Quality)
                return _PlaceOfOccurence.CompareTo(other._PlaceOfOccurence);
            else
                return _Quality.CompareTo(other._Quality) * -1;
        }

        #endregion

        public override string ToString()
        {
            return String.Concat(_ContentType, ";", "q=", _Quality);
        }

        public override bool Equals(object obj)
        {

            if (_ContentType.Equals((obj as AcceptType).ContentType))
                return true;

            else if (_ContentType.GetMediaSubType() == "*" && _ContentType.GetMediaType().Equals((obj as AcceptType).ContentType.GetMediaType()))
                return true;

            else
                return false;

        }

        public override int GetHashCode()
        {
            return _ContentType.GetHashCode();
        }

    }

}
