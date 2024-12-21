/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A list of HTTP accept types.
    /// </summary>
    public class AcceptTypes : IEnumerable<AcceptType>
    {

        #region Data

        private        readonly Lock              acceptTypesLock = new();

        private        readonly List<AcceptType>  acceptedTypes;

        private static readonly Char[]            splitter = [','];

        #endregion

        #region Constructor(s)

        public AcceptTypes(params AcceptType[] AcceptTypes)
        {

            acceptedTypes = [];

            if (AcceptTypes is not null && AcceptTypes.Length != 0)
                acceptedTypes.AddRange(AcceptTypes);

        }

        #endregion


        #region TryParse(AcceptString, out AcceptTypes)

        public static Boolean TryParse(String                                AcceptString,
                                       [NotNullWhen(true)] out AcceptTypes?  AcceptTypes)
        {

            var elements = AcceptString.Split(splitter, StringSplitOptions.RemoveEmptyEntries);

            if (elements.Length != 0)
            {

                var list = new List<AcceptType>();

                foreach (var element in elements)
                {

                    var element2 = element?.Trim();

                    if (element2 is not null &&
                        element2.IsNotNullOrEmpty() &&
                        AcceptType.TryParse(element2, out var acceptType) &&
                        acceptType is not null)
                    {
                        list.Add(acceptType);
                    }

                }

                AcceptTypes = new AcceptTypes([.. list]);
                return true;

            }

            AcceptTypes = null;
            return false;

        }

        #endregion


        #region FromHTTPContentTypes(params HTTPContentTypes)

        public static AcceptTypes FromHTTPContentTypes(params HTTPContentType[] HTTPContentTypes)

            => new (HTTPContentTypes?.Select(contentType => new AcceptType(contentType, 1))?.ToArray()
                        ?? []);

        #endregion


        #region Clone()

        public AcceptTypes Clone()

            => new (acceptedTypes.Select (acceptType => acceptType.Clone()).
                                  ToArray());

        #endregion


        #region Add(AcceptType)

        public void Add(AcceptType AcceptType)
        {

            if (AcceptType is null)
                return;

            lock (acceptTypesLock)
            {
                acceptedTypes.Add(AcceptType);
            }

        }

        #endregion

        #region Add(HTTPContentType, Quality = 1)

        public void Add(HTTPContentType HTTPContentType, Double Quality = 1)
        {

            if (HTTPContentType is null)
                return;

            lock (acceptTypesLock)
            {
                acceptedTypes.Add(new AcceptType(HTTPContentType, Quality));
            }

        }

        #endregion


        #region BestMatchingContentType(AvailableContentTypes)

        /// <summary>
        /// Will return the best matching content type OR the first given!
        /// </summary>
        /// <param name="AvailableContentTypes"></param>
        public HTTPContentType BestMatchingContentType(params HTTPContentType[] AvailableContentTypes)
        {

            var MatchingAcceptHeaders = new AcceptTypes();

            // If no Accept-headerfield was given -> return anything
            if (acceptedTypes.IsNullOrEmpty())
                return HTTPContentType.ALL;

            foreach (var AcceptType in acceptedTypes)
            {

                if (AvailableContentTypes.Contains(AcceptType.ContentType))
                    MatchingAcceptHeaders.Add(AcceptType.ContentType, AcceptType.Quality);

                else if (AcceptType.ContentType.ToString() == "*/*")
                    MatchingAcceptHeaders.Add(AcceptType.ContentType, AcceptType.Quality);

            }

            if (MatchingAcceptHeaders.IsNullOrEmpty())
                return HTTPContentType.ALL;

            var maxQuality  = (from   Matching
                               in     MatchingAcceptHeaders
                               select Matching.Quality).Max();

            var bestMatches =  from   Matching
                               in     MatchingAcceptHeaders
                               where  Matching.Quality == maxQuality
                               select Matching;

            if (bestMatches.Skip(1).Any())
                bestMatches = from   Matching
                              in     MatchingAcceptHeaders
                              where  Matching.Quality == maxQuality
                              where  Matching.ContentType.ToString() != "*/*"
                              select Matching;


            if (bestMatches.IsNullOrEmpty())
                return HTTPContentType.ALL;
            else
                return bestMatches.Reverse().First().ContentType;

        }

        #endregion



        #region GetEnumerator()

        public IEnumerator<AcceptType> GetEnumerator()
        {
            return acceptedTypes.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return acceptedTypes.GetEnumerator();
        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {

            if (acceptedTypes.Count == 0)
                return String.Empty;

            return String.Join(",", acceptedTypes.Select(a => a.ContentType + ";q=" + a.Quality.ToString().Replace(',', '.')));

        }

        #endregion

    }

}
