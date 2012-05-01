/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <code@ahzf.de>
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
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

using de.ahzf.Illias.Commons;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A list of HTTP accept types.
    /// </summary>
    public class AcceptTypes : IEnumerable<AcceptType>
    {

        #region Data

        private readonly List<AcceptType> List;

        #endregion

        #region Constructor(s)

        #region AcceptTypes()

        public AcceptTypes()
        {
            this.List = new List<AcceptType>();
        }

        #endregion

        #region AcceptTypes(FirstAcceptType, MoreAcceptTypes)

        public AcceptTypes(AcceptType FirstAcceptType, params AcceptType[] MoreAcceptTypes)
        {

            #region Initial checks

            if (FirstAcceptType == null)
                throw new ArgumentNullException("The given AcceptType must not be null!");

            #endregion

            this.List = new List<AcceptType>() { FirstAcceptType };

            if (MoreAcceptTypes != null && MoreAcceptTypes.Length > 0)
                this.List.AddRange(MoreAcceptTypes);

        }

        #endregion

        #region AcceptTypes(FirstAcceptType, MoreAcceptTypes)

        public AcceptTypes(HTTPContentType FirstHTTPContentType, params HTTPContentType[] MoreHTTPContentTypes)
        {

            #region Initial checks

            if (FirstHTTPContentType == null)
                throw new ArgumentNullException("The given HTTPContentType must not be null!");

            #endregion

            this.List = new List<AcceptType>() { new AcceptType(FirstHTTPContentType, 1) };

            if (MoreHTTPContentTypes != null && MoreHTTPContentTypes.Length > 0)
                this.List.AddRange(from _HTTPContentType in MoreHTTPContentTypes select new AcceptType(_HTTPContentType, 1));

        }

        #endregion

        #region AcceptTypes(AcceptsString)

        public AcceptTypes(String AcceptsString)
        {

            // text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8

            // text/html
            // application/xhtml+xml
            // application/xml;q=0.9
            // */*;q=0.8

            // text/html,application/xhtml+xml,application/xml
            // q=0.9,*/*
            // q=0.8

            this.List = new List<AcceptType>();
            var CurrentQuality = 1.0;

            foreach (var AcceptString in AcceptsString.Split(new Char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Reverse())
            {

                if (AcceptString.StartsWith("q="))
                    CurrentQuality = Double.Parse(AcceptString.Substring(2), CultureInfo.InvariantCulture);

                else
                    List.Add(new AcceptType(AcceptString, CurrentQuality));

            }

            // text/html
            // application/xhtml+xml
            // application/xml
            // q=0.9
            // */*
            // q=0.8

            //this.List = new List<AcceptType>().Select(s => new AcceptType(s)));
        }

        #endregion

        #endregion



        public void Clear()
        {
            List.Clear();
        }


        public void Add(AcceptType AcceptType)
        {

            #region Initial checks

            if (AcceptType == null)
                throw new ArgumentNullException("The given AcceptType must not be null!");

            #endregion

            List.Add(AcceptType);

        }

        public void Add(HTTPContentType HTTPContentType, Double Quality = 1)
        {

            #region Initial checks

            if (HTTPContentType == null)
                throw new ArgumentNullException("The given HTTPContentType must not be null!");

            #endregion
            
            List.Add(new AcceptType(HTTPContentType, Quality));

        }



        #region BestMatchingContentType(AvailableContentTypes)

        /// <summary>
        /// Will return the best matching content type OR the first given!
        /// </summary>
        /// <param name="AvailableContentTypes"></param>
        public HTTPContentType BestMatchingContentType(params HTTPContentType[] AvailableContentTypes)
        {

            var MatchingAcceptHeaders = new AcceptTypes();

            // If no Accept-headerfield was given -> return anything.
            if (List.IsNullOrEmpty())
                return HTTPContentType.ALL;

            foreach (var AcceptType in List)
            {

                if (AvailableContentTypes.Contains(AcceptType.ContentType))
                    MatchingAcceptHeaders.Add(AcceptType.ContentType, AcceptType.Quality);

                else if (AcceptType.ContentType.MediaType == "*/*")
                    MatchingAcceptHeaders.Add(AcceptType.ContentType, AcceptType.Quality);

            }

            if (MatchingAcceptHeaders.IsNullOrEmpty())
                return HTTPContentType.ALL;

            var MaxQuality  = (from   Matching
                               in     MatchingAcceptHeaders
                               select Matching.Quality).Max();

            var BestMatches =  from   Matching
                               in     MatchingAcceptHeaders
                               where  Matching.Quality == MaxQuality
                               select Matching;

            if (BestMatches.Count() > 1)
                BestMatches = from   Matching
                              in     MatchingAcceptHeaders
                              where  Matching.Quality == MaxQuality
                              where  Matching.ContentType.MediaType != "*/*"
                              select Matching;


            if (BestMatches.IsNullOrEmpty())
                return HTTPContentType.ALL;
            else
                return BestMatches.Reverse().First().ContentType;

        }

        #endregion





        public IEnumerator<AcceptType> GetEnumerator()
        {
            return List.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return List.GetEnumerator();
        }


        public override String ToString()
        {
            return String.Join(",", List.Select(a => a.ContentType.MediaType.ToString() + ";q=" + a.Quality.ToString().Replace(',', '.')));
        }


    }

}
