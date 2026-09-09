using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CubleyControl
{
    public static partial class Program
    {
        private const int RestPort = 80;
        private const string RestCommandPath = "/api/v2/commands";
        private const int RestHeaderMaxLength = 768;
        private const int RestSocketTimeoutMs = 2000;
        private const string RestJobsPathPrefix = "/api/v2/jobs/";
        private const string RestHealthPath = "/api/v2/health";
        private const string RestPositionerStatePath = "/api/v2/state/positioner";
        private const string RestLnbStatePath = "/api/v2/state/lnb";
        private static readonly string RestBootId = Guid.NewGuid().ToString();

        private static void RestLoop()
        {
            while (true)
            {
                if (!HasUsableIpv4Address())
                {
                    Thread.Sleep(1000);
                    continue;
                }

                Socket listener = null;
                try
                {
                    listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    listener.Bind(new IPEndPoint(IPAddress.Any, RestPort));
                    listener.Listen(2);
                    WriteStructuredDebug(
                        "REST",
                        "schema=1 sub=rest comp=server operation=listen stat=ok port=" + RestPort.ToString());

                    while (true)
                    {
                        Socket client = listener.Accept();
                        try
                        {
                            client.ReceiveTimeout = RestSocketTimeoutMs;
                            client.SendTimeout = RestSocketTimeoutMs;
                            HandleRestRequest(client);
                        }
                        finally
                        {
                            client.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteStructuredDebug(
                        "REST",
                        "schema=1 sub=rest comp=server operation=listen stat=error detail=" +
                        SanitizeToken(ex.Message));
                    Thread.Sleep(1000);
                }
                finally
                {
                    if (listener != null)
                    {
                        listener.Close();
                    }
                }
            }
        }

        private static void HandleRestRequest(Socket client)
        {
            byte[] request = new byte[RestHeaderMaxLength + MqttCommandEnvelopeMaxLength];
            int received = 0;
            int bodyOffset = -1;
            while (received < request.Length && bodyOffset < 0)
            {
                int count = client.Receive(request, received, request.Length - received, SocketFlags.None);
                if (count <= 0)
                {
                    WriteRestResponse(client, 400, null);
                    return;
                }

                received += count;
                bodyOffset = FindRestBodyOffset(request, received);
                if (bodyOffset > RestHeaderMaxLength)
                {
                    WriteRestResponse(client, 400, null);
                    return;
                }

                if (bodyOffset < 0 && received >= RestHeaderMaxLength)
                {
                    WriteRestResponse(client, 400, null);
                    return;
                }
            }

            string headers = AsciiBytesToString(request, 0, bodyOffset);
            int firstSpace = headers.IndexOf(' ');
            int secondSpace = firstSpace < 0 ? -1 : headers.IndexOf(' ', firstSpace + 1);
            if (firstSpace < 0 || secondSpace < 0)
            {
                WriteRestResponse(client, 400, null);
                return;
            }

            string method = headers.Substring(0, firstSpace);
            string path = headers.Substring(firstSpace + 1, secondSpace - firstSpace - 1);
            if (method == "GET")
            {
                HandleRestGetRequest(client, path);
                return;
            }

            if (path != RestCommandPath)
            {
                WriteRestResponse(client, 404, null);
                return;
            }

            if (method != "POST")
            {
                WriteRestResponse(client, 405, null);
                return;
            }

            int contentLength = ParseRestContentLength(headers);
            if (contentLength <= 0 || contentLength > MqttCommandEnvelopeMaxLength)
            {
                WriteRestResponse(client, 400, null);
                return;
            }

            int required = bodyOffset + contentLength;
            while (received < required)
            {
                int count = client.Receive(request, received, required - received, SocketFlags.None);
                if (count <= 0)
                {
                    WriteRestResponse(client, 400, null);
                    return;
                }

                received += count;
            }

            string responseBody;
            lock (_commandLock)
            {
                responseBody = ProcessApiCommand(AsciiBytesToString(request, bodyOffset, contentLength));
            }
            WriteRestResponse(client, 200, responseBody);
        }

        private static void HandleRestGetRequest(Socket client, string path)
        {
            if (path == RestHealthPath)
            {
                string health = new JsonBuilder()
                    .AddString("status", "ok")
                    .AddString("firmware_version", BuildInfo.Version)
                    .Build();
                WriteRestResponse(client, 200, BuildRestQueryResponse(true, "ok", health, null));
                return;
            }

            if (path == RestPositionerStatePath)
            {
                WriteRestResponse(client, 200, BuildRestQueryResponse(true, "ok", BuildDiseqcStateJson(), null));
                return;
            }

            if (path == RestLnbStatePath)
            {
                WriteRestResponse(client, 200, BuildRestQueryResponse(true, "ok", BuildLnbStateJson(), null));
                return;
            }

            if (path.StartsWith(RestJobsPathPrefix))
            {
                int jobId;
                string text = path.Substring(RestJobsPathPrefix.Length);
                if (!TryParsePositiveInt(text, out jobId))
                {
                    WriteRestResponse(client, 400, BuildRestQueryResponse(false, "validation_error", null, "job must be a positive integer"));
                    return;
                }

                string job = BuildDiseqcJobJson(jobId);
                if (job == "null")
                {
                    WriteRestResponse(client, 404, BuildRestQueryResponse(false, "not_found", null, "job is unknown or has been evicted"));
                    return;
                }

                WriteRestResponse(client, 200, BuildRestQueryResponse(true, "ok", job, null));
                return;
            }

            WriteRestResponse(client, 404, BuildRestQueryResponse(false, "not_found", null, "resource not found"));
        }

        private static string BuildRestQueryResponse(bool ok, string code, string data, string message)
        {
            JsonBuilder builder = new JsonBuilder()
                .AddInt("v", DeviceContractVersion)
                .AddString("boot_id", RestBootId)
                .AddBool("ok", ok)
                .AddString("code", code)
                .AddLong("ts_ms", Environment.TickCount64);

            if (message != null)
            {
                builder.AddString("msg", message);
            }

            if (data != null)
            {
                builder.AddRaw("data", data);
            }

            return builder.Build();
        }

        private static int FindRestBodyOffset(byte[] request, int length)
        {
            for (int index = 3; index < length; index++)
            {
                if (request[index - 3] == '\r' && request[index - 2] == '\n' &&
                    request[index - 1] == '\r' && request[index] == '\n')
                {
                    return index + 1;
                }
            }

            return -1;
        }

        private static int ParseRestContentLength(string headers)
        {
            const string name = "\r\ncontent-length:";
            string lower = headers.ToLower();
            int start = lower.IndexOf(name);
            if (start < 0)
            {
                return -1;
            }

            start += name.Length;
            int end = lower.IndexOf("\r\n", start);
            int value;
            return end > start && TryParsePositiveInt(lower.Substring(start, end - start).Trim(), out value)
                ? value
                : -1;
        }

        private static void WriteRestResponse(Socket client, int statusCode, string body)
        {
            string reason = statusCode == 200 ? "OK" :
                (statusCode == 404 ? "Not Found" :
                (statusCode == 405 ? "Method Not Allowed" : "Bad Request"));
            string payload = body == null ? string.Empty : body;
            byte[] response = AsciiStringToBytes(
                "HTTP/1.1 " + statusCode.ToString() + " " + reason + "\r\n" +
                "Content-Type: application/json\r\n" +
                "Content-Length: " + payload.Length.ToString() + "\r\n" +
                "Connection: close\r\n\r\n" + payload);
            int sent = 0;
            while (sent < response.Length)
            {
                int count = client.Send(response, sent, response.Length - sent, SocketFlags.None);
                if (count <= 0)
                {
                    return;
                }

                sent += count;
            }
        }

        private static string AsciiBytesToString(byte[] bytes, int offset, int length)
        {
            char[] chars = new char[length];
            for (int index = 0; index < length; index++)
            {
                byte value = bytes[offset + index];
                chars[index] = value <= 0x7F ? (char)value : '?';
            }

            return new string(chars);
        }
    }
}