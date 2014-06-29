/*
 * Copyright (c) 2011-2013 Achim 'ahzf' Friedland <achim@ahzf.de>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 * 
 * You may obtain a copy of the License at
 *   http://www.gnu.org/licenses/gpl.html
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 */

#region Usings

using System;

using eu.Vanaheimr.Illias.Commons;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    public static class HTTPErrors
    {

        #region HTTPErrorResponse(StatusCode, Reasons = null)

        /// <summary>
        /// Return a HTTP error response using the best-matching content type.
        /// </summary>
        /// <param name="HTTPRequest">The HTTP request.</param>
        /// <param name="StatusCode">A HTTP status code.</param>
        /// <param name="Reasons">Optional application side reasons for this error.</param>
        public static HTTPResponse HTTPErrorResponse_old(HTTPRequest HTTPRequest, HTTPStatusCode StatusCode, String Reasons = null)
        {

            #region Initial checks

            if (StatusCode == null)
                return HTTPErrorResponse_old(HTTPRequest, HTTPStatusCode.InternalServerError, "Calling the HTTPError lead to an error!");

            var Content     = String.Empty;
            var ContentType = HTTPRequest.Accept.BestMatchingContentType(HTTPContentType.JSON_UTF8,
                                                                         HTTPContentType.HTML_UTF8,
                                                                         HTTPContentType.TEXT_UTF8,
                                                                         HTTPContentType.XML_UTF8);

            #endregion

            #region JSON_UTF8

            // {
            //     "error": {
            //         "code"    : 400
            //         "message" : "Bad Request"
            //         "reason"  : "The first paramter is not a valid number!"
            //     }
            // }
            if (ContentType == HTTPContentType.JSON_UTF8)
                Content = (Reasons == null) ? "{ \"error\": { \"code\" : " + StatusCode.Code + ", \"message\" : \"" + StatusCode.Name + "\" } }" :
                                              "{ \"error\": { \"code\" : " + StatusCode.Code + ", \"message\" : \"" + StatusCode.Name + "\", \"reasons\" : \"" + Reasons + "\" } }";

            #endregion

            #region HTML_UTF8

            //<!doctype html>
            //<html>
            //  <head>
            //    <meta charset="UTF-8">
            //    <title>Error 400 - Bad Request</title>
            //  </head>
            //  <body>
            //    <h1>Error 400 - Bad Request</h1>
            //    The first paramter is not a valid number!
            //  </body>
            //</html>
            else if (ContentType == HTTPContentType.HTML_UTF8)
                Content = (Reasons == null) ? "<!doctype html><html><head><meta charset=\"UTF-8\"><title>Error " + StatusCode.Code + " - " + StatusCode.Name + "</title></head><body><h1>Error " + StatusCode.Code + " - " + StatusCode.Name + "</h1></body></html>" :
                                              "<!doctype html><html><head><meta charset=\"UTF-8\"><title>Error " + StatusCode.Code + " - " + StatusCode.Name + "</title></head><body><h1>Error " + StatusCode.Code + " - " + StatusCode.Name + "</h1>" + Reasons + "</body></html>";

            #endregion

            #region TEXT_UTF8

            // Error 400 - Bad Request
            // The first paramter is not a valid number!
            else if (ContentType == HTTPContentType.TEXT_UTF8 || ContentType == HTTPContentType.ALL)
                Content = (Reasons == null) ? "Error " + StatusCode.Code + " - " + StatusCode.Name :
                                              "Error " + StatusCode.Code + " - " + StatusCode.Name + Environment.NewLine + Reasons;

            #endregion

            #region XML_UTF8

            // <?xml version="1.0" encoding="UTF-8"?>
            // <error>
            //     <code>400</code>
            //     <message>Bad Request</message>
            //     <reason>The first paramter is not a valid number!</message>
            // </error>
            else if (ContentType == HTTPContentType.XML_UTF8)
                Content = (Reasons == null) ? "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><code>" + StatusCode.Code + "</code><message>" + StatusCode.Name + "</message></error></xml>" :
                                              "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><code>" + StatusCode.Code + "</code><message>" + StatusCode.Name + "</message><reasons>" + Reasons + "</reasons></error></xml>";

            #endregion

            #region GEXF+XML

            // <?xml version="1.0" encoding="UTF-8"?>
            // <gexf xmlns=\"http://www.gexf.net/1.2draft\" version="1.2">
            //   <meta lastmodifieddate="2009-03-20">
            //     <creator>Vanaheimr Walkyr</creator>
            //     <description>HTTP Error</description>
            //   </meta>
            //   <graph mode="static" defaultedgetype="directed">
            //     <attributes class="edge">
            //       <attribute id="0" title="Reasons" type="string"/>
            //     </attributes>
            //     <nodes>
            //       <node id="Request" label="Request" />
            //       <node id="Error"   label="Error 400 - Bad Request" />
            //     </nodes>
            //     <edges>
            //       <edge id="0" source="Request" target="Error">
            //         <attvalues>
            //           <attvalue for="0" value="The first paramter is not a valid number!"/>
            //         </attvalues>
            //       <edge>
            //     </edges>
            //   </graph>
            // </gexf>
            //     <reason></message>
            else if (ContentType == HTTPContentType.GEXF_UTF8)
                Content = (Reasons == null) ? 
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<gexf xmlns=\"http://www.gexf.net/1.2draft\" version=\"1.2\">" +
                    "<meta lastmodifieddate=\"2009-03-20\"><creator>Vanaheimr Walkyr</creator><description>HTTP Error</description></meta>" +
                    "<graph mode=\"static\" defaultedgetype=\"directed\">" +
                    "<nodes>" +
                      "<node id=\"Request\" label=\"Request\" />" +
                      "<node id=\"Error\"   label=\"Error " + StatusCode.Code + " - " + StatusCode.Name + "\" />" +
                    "</nodes><edges>" +
                      "<edge id=\"0\" source=\"Request\" target=\"Error\" />" +
                    "</edges></graph></gexf>" :

                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<gexf xmlns=\"http://www.gexf.net/1.2draft\" version=\"1.2\">" +
                    "<meta lastmodifieddate=\"2009-03-20\"><creator>Vanaheimr Walkyr</creator><description>HTTP Error</description></meta>" +
                    "<graph mode=\"static\" defaultedgetype=\"directed\">" +
                    "<attributes class=\"edge\">" +
                    "  <attribute id=\"0\" title=\"Reasons\" type=\"string\" />" +
                    "</attributes> " +
                    "<nodes>" +
                      "<node id=\"Request\" label=\"Request\" />" +
                      "<node id=\"Error\"   label=\"Error " + StatusCode.Code + " - " + StatusCode.Name + "\" />" +
                    "</nodes><edges>" +
                      "<edge id=\"0\" source=\"Request\" target=\"Error\">" +
                        "<attvalues>" +
                          "<attvalue for=\"0\" value=\"" + Reasons + "\" />" +
                        "</attvalues>" +
                      "<edge>" +
                    "</edges></graph></gexf>";

            #endregion

            var x = new HTTPResponseBuilder()
            {
                HTTPStatusCode = StatusCode,
                CacheControl   = "no-cache",
                Connection     = "close",
                Content        = Content.ToUTF8Bytes()
            };

            return x;

        }

        #endregion

    }

}
