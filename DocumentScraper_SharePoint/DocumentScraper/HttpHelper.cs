using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using DocumentScraper.Helpers;

namespace DocumentScraper
{
    public static class HttpHelper
    {
        /// <summary>
        /// Sends a JSON OData request appending SPO auth cookies to the request header.
        /// </summary>
        public static byte[] SendODataJsonRequest(Uri uri, string method, byte[] requestContent, HttpWebRequest clientHandler, SpoAuthUtility authUtility, Dictionary<string, string> headers = null)
        {
            clientHandler.CookieContainer = clientHandler.CookieContainer ?? new CookieContainer();

            foreach (Cookie c in authUtility.GetCookieContainer().GetCookies(uri))
            {
                clientHandler.CookieContainer.Add(uri, c);
            }
            return SendHttpRequest(uri, method, requestContent, "application/json;odata=verbose;charset=utf-8", clientHandler, headers);
        }

        /// <summary>
        /// Sends an http request to the specified uri and returns the response as a byte array 
        /// </summary>
        public static byte[] SendHttpRequest(Uri uri, string method, byte[] requestContent = null, string contentType = null, HttpWebRequest clientHandler = null, Dictionary<string, string> headers = null)
        {
            var request = clientHandler ?? (HttpWebRequest)WebRequest.Create(uri);

            request.Method = method;
            request.Accept = contentType;
            request.UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";
            request.AllowAutoRedirect = false;

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (request.Headers.AllKeys.Contains(header.Key))
                    {
                        request.Headers.Remove(header.Key);
                    }
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (requestContent != null && (method == Method.POST || method == Method.PUT || method == Method.DELETE))
            {
                if (!string.IsNullOrEmpty(contentType))
                {
                    request.ContentType = contentType;
                }

                request.ContentLength = requestContent.Length;
                using (var s = request.GetRequestStream())
                {
                    s.Write(requestContent, 0, requestContent.Length);
                    s.Close();
                }
            }

            var response = (HttpWebResponse)request.GetResponse();
            var sr = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));

            return Encoding.UTF8.GetBytes(sr.ReadToEnd());
        }
    }
}