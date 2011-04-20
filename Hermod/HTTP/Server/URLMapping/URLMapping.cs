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
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;

using de.ahzf.Hermod.HTTP.Common;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// The mapping from URLTemplates onto C# methods.
    /// </summary>
    public class URLMapping
    {

        #region Data

        private readonly ConcurrentDictionary<String, HostNode> _HostNodes;

        #endregion

        #region Constructor(s)

        #region URLMapping()

        /// <summary>
        /// Main constructor.
        /// </summary>
        public URLMapping()
        {
            _HostNodes  = new ConcurrentDictionary<String, HostNode>();
        }

        #endregion

        #endregion


        #region AddHandler(myMethodHandler, myHost, myURL, myHTTPMethod = null, myHTTPContentType = null, ...)

        /// <summary>
        /// Add a method handler for the given parameters.
        /// </summary>
        /// <param name="myMethodHandler"></param>
        /// <param name="myHost"></param>
        /// <param name="myURL"></param>
        /// <param name="myHTTPMethod"></param>
        /// <param name="myHTTPContentType"></param>
        /// <param name="HostAuthentication"></param>
        /// <param name="URLAuthentication"></param>
        /// <param name="HTTPMethodAuthentication"></param>
        /// <param name="ContentTypeAuthentication"></param>
        public void AddHandler(MethodInfo       myMethodHandler,
                               String           myHost,
                               String           myURL,
                               HTTPMethod       myHTTPMethod                = null,
                               HTTPContentType  myHTTPContentType           = null,
                               Boolean          HostAuthentication          = false,
                               Boolean          URLAuthentication           = false,
                               Boolean          HTTPMethodAuthentication    = false,
                               Boolean          ContentTypeAuthentication   = false)
        {

            #region Initial Checks

            if (myMethodHandler == null)
                throw new ArgumentNullException("The MethodHandler must not be null!");

            if (myHost == null || myHost == String.Empty)
                myHost = "*";

            if (myURL == null || myURL == String.Empty)
                throw new ArgumentNullException("The URL must not be null!");

            if (myHTTPMethod == null && myHTTPContentType != null)
                throw new ArgumentNullException("If myHTTPMethod is null the myHTTPContentType has also to be null!");

            #endregion

            #region AddOrUpdate HostNode

            HostNode _HostNode = null;
            if (!_HostNodes.TryGetValue(myHost, out _HostNode))
            {
                _HostNode = new HostNode(myHost, HostAuthentication);
                _HostNodes.AddOrUpdate(myHost, _HostNode, (k, v) => v);
            }

            #endregion

            #region AddOrUpdate URLNode

            URLNode _URLNode = null;
            if (!_HostNode.URLNodes.TryGetValue(myURL, out _URLNode))
            {

                if (myHTTPMethod == null)
                    _URLNode = new URLNode(myURL, myMethodHandler, URLAuthentication);

                else
                    _URLNode = new URLNode(myURL, null, URLAuthentication);

            }

            _HostNode.URLNodes.AddOrUpdate(myURL, _URLNode, (k, v) => v);

            if (myHTTPMethod == null)
                return;

            #endregion

            #region AddOrUpdate HTTPMethodNode

            HTTPMethodNode _HTTPMethodNode = null;
            if (!_URLNode.HTTPMethods.TryGetValue(myHTTPMethod, out _HTTPMethodNode))
            {

                if (myHTTPContentType == null)
                    _HTTPMethodNode = new HTTPMethodNode(myHTTPMethod, myMethodHandler, HTTPMethodAuthentication);

                else
                    _HTTPMethodNode = new HTTPMethodNode(myHTTPMethod, null, HTTPMethodAuthentication);

            }

            _URLNode.HTTPMethods.AddOrUpdate(myHTTPMethod, _HTTPMethodNode, (k, v) => v);

            if (myHTTPContentType == null)
                return;

            #endregion

            #region AddOrUpdate ContentTypeNode

            ContentTypeNode _ContentTypeNode = null;
            if (!_HTTPMethodNode.HTTPContentTypes.TryGetValue(myHTTPContentType, out _ContentTypeNode))
            {
                _ContentTypeNode = new ContentTypeNode(myHTTPContentType, myMethodHandler, ContentTypeAuthentication);
            }

            _HTTPMethodNode.HTTPContentTypes.AddOrUpdate(myHTTPContentType, _ContentTypeNode, (k, v) => v);

            #endregion

        }

        #endregion

        #region GetHandler(myHost, myURL, myHTTPMethod = null, myHTTPContentType = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        /// <param name="myHost"></param>
        /// <param name="myURL"></param>
        /// <param name="myHTTPMethod"></param>
        /// <param name="myHTTPContentType"></param>
        /// <returns></returns>
        public Tuple<MethodInfo, IEnumerable<Object>> GetHandler(String myHost, String myURL, HTTPMethod myHTTPMethod = null, HTTPContentType myHTTPContentType = null)
        {

            #region Initial Checks

            if (myHost == null || myHost == String.Empty)
                myHost = "*";

            if (myURL == null || myURL == String.Empty)
                throw new ArgumentNullException("The URL must not be null!");

            #endregion

            #region Get HostNode

            HostNode _HostNode = null;
            if (!_HostNodes.TryGetValue(myHost, out _HostNode))
                if (!_HostNodes.TryGetValue("*", out _HostNode))
                    return null;

            #endregion

            #region Get best matchin URLNode

            var _RegexList = from __URLNode in _HostNode.URLNodes.Values select new { URLNode = __URLNode, Regex = __URLNode.URLRegex };
            var _Matches   = from _RegexTupel in _RegexList select new { URLNode = _RegexTupel.URLNode, Match = _RegexTupel.Regex.Match(myURL) };
            var _BestMatch = (from _Match in _Matches orderby 100*_Match.Match.Length+_Match.URLNode.ParameterCount descending select new { URLNode = _Match.URLNode, Match = _Match.Match }).First();

            Console.WriteLine("URL: " + myURL + " => " + _BestMatch.URLNode.URLTemplate);
            //_Matches.ForEach(_m => { if (_m.Match.Success) Console.WriteLine("Match Length:'" + _m.Match.Length + "'+" + _m.URLNode.ParameterCount + " Groups[1]:'" + _m.Match.Groups[1] + "' URLTemplate:'" + _m.URLNode.URLTemplate + "'"); });

            // Copy MethodHandler Parameters
            var _Parameters = new List<Object>();
            for (var i=1; i< _BestMatch.Match.Groups.Count; i++)
                _Parameters.Add(_BestMatch.Match.Groups[i].Value);

            // If no HTTPMethod was given => return best matching URL MethodHandler
            if (myHTTPMethod == null)
                return new Tuple<MethodInfo, IEnumerable<Object>>(_BestMatch.URLNode.MethodHandler, _Parameters);

            #endregion

            #region Get HTTPMethodNode

            HTTPMethodNode _HTTPMethodNode = null;

            // If no HTTPMethod was found => return best matching URL MethodHandler
            if (!_BestMatch.URLNode.HTTPMethods.TryGetValue(myHTTPMethod, out _HTTPMethodNode))
                return new Tuple<MethodInfo, IEnumerable<Object>>(_BestMatch.URLNode.MethodHandler, _Parameters);

            // If no HTTPContentType was given => return HTTPMethod MethodHandler
            if (myHTTPContentType == null)
                return new Tuple<MethodInfo, IEnumerable<Object>>(_HTTPMethodNode.MethodHandler, _Parameters);

            #endregion

            // If all fails => return ErrorHandler!
            return GetErrorHandler(myHost, myURL, myHTTPMethod, myHTTPContentType, HTTPStatusCode.BadRequest);

        }

        #endregion

        #region GetErrorHandler(myHost, myURL, myHTTPMethod = null, myHTTPContentType = null, myHTTPStatusCode = null)

        /// <summary>
        /// Return the best matching error handler for the given parameters.
        /// </summary>
        /// <param name="myHost"></param>
        /// <param name="myURL"></param>
        /// <param name="myHTTPMethod"></param>
        /// <param name="myHTTPContentType"></param>
        /// <param name="myHTTPStatusCode"></param>
        /// <returns></returns>
        public Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String myHost, String myURL, HTTPMethod myHTTPMethod = null, HTTPContentType myHTTPContentType = null, HTTPStatusCode myHTTPStatusCode = null)
        {

            return null;

        }

        #endregion


    }

}
