namespace CubleyControl
{
    internal static class RestConnectionPolicy
    {
        internal static bool ShouldKeepAlive(string headers)
        {
            if (headers == null)
            {
                return false;
            }

            int firstLineEnd = headers.IndexOf("\r\n");
            if (firstLineEnd < 0 ||
                !headers.Substring(0, firstLineEnd).EndsWith(" HTTP/1.1"))
            {
                return false;
            }

            int lineStart = firstLineEnd + 2;
            while (lineStart < headers.Length)
            {
                int lineEnd = headers.IndexOf("\r\n", lineStart);
                if (lineEnd < 0)
                {
                    lineEnd = headers.Length;
                }

                int colon = headers.IndexOf(':', lineStart);
                if (colon > lineStart && colon < lineEnd)
                {
                    string name = headers.Substring(lineStart, colon - lineStart).Trim().ToLower();
                    if (name == "connection")
                    {
                        string value = headers.Substring(colon + 1, lineEnd - colon - 1).ToLower();
                        if (ContainsToken(value, "close"))
                        {
                            return false;
                        }
                    }
                }

                lineStart = lineEnd + 2;
            }

            return true;
        }

        private static bool ContainsToken(string value, string expected)
        {
            int start = 0;
            while (start < value.Length)
            {
                int comma = value.IndexOf(',', start);
                int end = comma < 0 ? value.Length : comma;
                if (value.Substring(start, end - start).Trim() == expected)
                {
                    return true;
                }

                if (comma < 0)
                {
                    break;
                }

                start = comma + 1;
            }

            return false;
        }
    }
}
