using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Collections.Specialized;

namespace CodeProse.CorsProxy.Web.Controllers
{
    public class HomeController : Controller
    {
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Index(string url)
        {
            Response.AddHeader("Access-Control-Allow-Origin", "*");
            Response.AddHeader("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept, X-DocuSign-Authentication"); // X-DocuSign-Authentication is a hack, but so is their API
            //Response.AddHeader("Access-Control-Allow-Credentials", "true");

            if (Request.HttpMethod == "OPTIONS")
            {
                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            else
            {
                var request = (HttpWebRequest)WebRequest.Create(url);

                request.Method = Request.HttpMethod;
                SetHeaders(Request.Headers, request.Headers);

                switch (Request.HttpMethod)
                {
                    case "POST":
                    case "PUT":
                        var data = ReadStream(Request.InputStream);
                        WriteStream(data, request);
                        break;
                }

                var response = (HttpWebResponse)request.GetResponse();

                return new HttpWebResponseResult(response);
            }
        }

        private static void SetHeaders(NameValueCollection inputHeaders, NameValueCollection outputHeaders)
        {
            foreach (string key in inputHeaders.Keys)
            {
                Debug.WriteLine("Header: " + key + ": " + inputHeaders[key]);

                switch (key)
                {
                    case "Accept":
                    case "Connection":
                    case "Content-Length":
                    case "Content-Type":
                    case "Date":
                    case "Expect":
                    case "Host":
                    case "If-Modified-Since":
                    case "Range":
                    case "Referer":
                    case "Transfer-Encoding":
                    case "User-Agent":
                    case "Proxy-Connection":
                        // Handled by IIS
                        break;

                    default:
                        outputHeaders[key] = inputHeaders[key];
                        break;
                }
            }
        }

        private static void WriteStream(byte[] data, HttpWebRequest request)
        {
            //byte[] postBytes = System.Text.Encoding.UTF8.GetBytes(settings.Data);
            request.ContentLength = data.Length;
            Stream stream = request.GetRequestStream();
            stream.Write(data, 0, data.Length);
            stream.Close();
        }

        private static byte[] ReadStream(Stream input)
        {
            input.Position = 0;
            var ms = new MemoryStream();
            input.CopyTo(ms);
            byte[] data = ms.ToArray();

            return data;
        }
    }

    public class CorsRequestSettings
    {
        public CorsRequestSettings()
        {
            Method = "GET";
            Headers = new Dictionary<string, string>();
        }

        public string Method { get; set; }
        public string Url { get; set; }
        public string Data { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }

    /// <summary>
    /// Result for relaying an HttpWebResponse
    /// </summary>
    public class HttpWebResponseResult : ActionResult
    {
        private readonly HttpWebResponse _response;
        private readonly ActionResult _innerResult;

        /// <summary>
        /// Relays an HttpWebResponse as verbatim as possible.
        /// </summary>
        /// <param name="responseToRelay">The HTTP response to relay</param>
        public HttpWebResponseResult(HttpWebResponse responseToRelay)
        {
            if (responseToRelay == null)
            {
                throw new ArgumentNullException("response");
            }

            _response = responseToRelay;

            Stream contentStream;
            if (responseToRelay.ContentEncoding.Contains("gzip"))
            {
                contentStream = new GZipStream(responseToRelay.GetResponseStream(), CompressionMode.Decompress);
            }
            else if (responseToRelay.ContentEncoding.Contains("deflate"))
            {
                contentStream = new DeflateStream(responseToRelay.GetResponseStream(), CompressionMode.Decompress);
            }
            else
            {
                contentStream = responseToRelay.GetResponseStream();
            }


            if (string.IsNullOrEmpty(responseToRelay.CharacterSet))
            {
                // File result
                _innerResult = new FileStreamResult(contentStream, responseToRelay.ContentType);
            }
            else
            {
                // Text result
                var contentResult = new ContentResult();
                contentResult = new ContentResult();
                contentResult.Content = new StreamReader(contentStream).ReadToEnd();
                _innerResult = contentResult;
            }
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var clientResponse = context.HttpContext.Response;
            clientResponse.StatusCode = (int)_response.StatusCode;

            foreach (var headerKey in _response.Headers.AllKeys)
            {
                switch (headerKey)
                {
                    case "Content-Length":
                    case "Transfer-Encoding":
                    case "Content-Encoding":
                        // Handled by IIS
                        break;

                    default:
                        clientResponse.AddHeader(headerKey, _response.Headers[headerKey]);
                        break;
                }
            }

            _innerResult.ExecuteResult(context);
        }
    }
}
