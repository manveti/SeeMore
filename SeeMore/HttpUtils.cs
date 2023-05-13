using System;
using System.IO;
using System.Net.Http;

namespace SeeMore {
    public class HttpUtils {
        public const string HEADER_USER_AGENT = "User-Agent";
        public const string DEFAULT_USER_AGENT = "See-More/0.1";
        public static string userAgent;
        public static readonly HttpClient httpClient;

        static HttpUtils() {
            userAgent = DEFAULT_USER_AGENT;
            httpClient = new HttpClient();
        }

        public static byte[] downloadFile(string url) {
            if (url == null) {
                return null;
            }
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(HEADER_USER_AGENT, userAgent);
            try {
                HttpResponseMessage resp = httpClient.Send(request);
                return resp.Content.ReadAsByteArrayAsync().Result;
            }
            catch (Exception e) when ((e is FormatException) || (e is HttpRequestException)) {
                return null;
            }
        }

        public static Stream openStream(string path) {
            if (path == null) {
                return null;
            }
            if (path.Contains('\\')) {
                // handle local files
                return new FileStream(path, FileMode.OpenOrCreate);
            }
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add(HEADER_USER_AGENT, userAgent);
            HttpResponseMessage resp = httpClient.Send(request);
            return resp.Content.ReadAsStream();
        }
    }
}
