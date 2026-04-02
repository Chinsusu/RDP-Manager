using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using RdpManager.Models;

namespace RdpManager.Services
{
    public static class CloudminiClient
    {
        private const string BaseUrl = "https://client.cloudmini.net/api/v2";
        private const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;

        public static CloudminiAccountSummary GetAccountSummary(string token)
        {
            var response = SendGetRequest<CloudminiAccountSummary>("/account", token);
            if (response.Error)
            {
                throw CreateApiException(response.Message);
            }

            return response.Data.Count == 0 ? new CloudminiAccountSummary() : response.Data[0];
        }

        public static List<CloudminiVps> GetVps(string token)
        {
            var response = SendGetRequest<CloudminiVps>("/vps", token);
            if (response.Error)
            {
                throw CreateApiException(response.Message);
            }

            return response.Data ?? new List<CloudminiVps>();
        }

        private static CloudminiListResponse<T> SendGetRequest<T>(string path, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Cloudmini token cannot be empty.");
            }

            ServicePointManager.SecurityProtocol |= Tls12;

            var request = (HttpWebRequest)WebRequest.Create(BaseUrl + path);
            request.Method = "GET";
            request.Accept = "application/json";
            request.Headers[HttpRequestHeader.Authorization] = "Token " + token.Trim();

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    if (stream == null)
                    {
                        throw new InvalidOperationException("Cloudmini API returned an empty response stream.");
                    }

                    var serializer = new DataContractJsonSerializer(typeof(CloudminiListResponse<T>));
                    var payload = serializer.ReadObject(stream) as CloudminiListResponse<T>;
                    return payload ?? new CloudminiListResponse<T>();
                }
            }
            catch (WebException ex)
            {
                throw new InvalidOperationException(TryReadApiError(ex), ex);
            }
        }

        private static string TryReadApiError(WebException ex)
        {
            if (ex.Response == null)
            {
                return "Cloudmini request failed. " + ex.Message;
            }

            try
            {
                using (var response = ex.Response)
                using (var stream = response.GetResponseStream())
                {
                    if (stream == null)
                    {
                        return "Cloudmini request failed. " + ex.Message;
                    }

                    var serializer = new DataContractJsonSerializer(typeof(CloudminiListResponse<object>));
                    var payload = serializer.ReadObject(stream) as CloudminiListResponse<object>;
                    if (payload != null && !string.IsNullOrWhiteSpace(payload.Message))
                    {
                        return payload.Message;
                    }
                }
            }
            catch (SerializationException)
            {
            }
            catch (IOException)
            {
            }

            return "Cloudmini request failed. " + ex.Message;
        }

        private static Exception CreateApiException(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return new InvalidOperationException("Cloudmini API returned an unknown error.");
            }

            return new InvalidOperationException(message);
        }

        [DataContract]
        private class CloudminiListResponse<T>
        {
            public CloudminiListResponse()
            {
                Data = new List<T>();
            }

            [DataMember(Name = "error")]
            public bool Error { get; set; }

            [DataMember(Name = "msg")]
            public string Message { get; set; }

            [DataMember(Name = "data")]
            public List<T> Data { get; set; }
        }
    }
}
