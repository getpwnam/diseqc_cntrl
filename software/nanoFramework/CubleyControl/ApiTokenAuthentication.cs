using System;

namespace CubleyControl
{
    internal static class ApiTokenAuthentication
    {
        public static bool IsAuthorized(string headers, string expectedToken)
        {
            if (string.IsNullOrEmpty(expectedToken))
            {
                return true;
            }

            string providedToken;
            return TryReadBearerToken(headers, out providedToken) &&
                FixedTimeEquals(expectedToken, providedToken);
        }

        public static bool TryReadBearerToken(string headers, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(headers))
            {
                return false;
            }

            int lineStart = headers.IndexOf("\r\n");
            if (lineStart < 0)
            {
                return false;
            }

            lineStart += 2;
            bool found = false;
            while (lineStart < headers.Length)
            {
                int lineEnd = headers.IndexOf("\r\n", lineStart);
                if (lineEnd < 0 || lineEnd == lineStart)
                {
                    break;
                }

                int separator = headers.IndexOf(':', lineStart);
                if (separator > lineStart && separator < lineEnd &&
                    EqualsAsciiIgnoreCase(headers, lineStart, separator - lineStart, "Authorization"))
                {
                    if (found)
                    {
                        return false;
                    }

                    int valueStart = separator + 1;
                    while (valueStart < lineEnd &&
                        (headers[valueStart] == ' ' || headers[valueStart] == '\t'))
                    {
                        valueStart++;
                    }

                    if (lineEnd - valueStart <= 7 ||
                        !EqualsAsciiIgnoreCase(headers, valueStart, 6, "Bearer") ||
                        (headers[valueStart + 6] != ' ' && headers[valueStart + 6] != '\t'))
                    {
                        return false;
                    }

                    int tokenStart = valueStart + 7;
                    while (tokenStart < lineEnd &&
                        (headers[tokenStart] == ' ' || headers[tokenStart] == '\t'))
                    {
                        tokenStart++;
                    }

                    int tokenEnd = lineEnd;
                    while (tokenEnd > tokenStart &&
                        (headers[tokenEnd - 1] == ' ' || headers[tokenEnd - 1] == '\t'))
                    {
                        tokenEnd--;
                    }

                    token = headers.Substring(tokenStart, tokenEnd - tokenStart);
                    if (!ApplicationConfiguration.IsValidApiToken(token))
                    {
                        token = null;
                        return false;
                    }

                    found = true;
                }

                lineStart = lineEnd + 2;
            }

            return found;
        }

        public static bool FixedTimeEquals(string expected, string provided)
        {
            if (expected == null || provided == null)
            {
                return false;
            }

            int difference = expected.Length ^ provided.Length;
            for (int index = 0; index < ApplicationConfiguration.MaximumApiTokenLength; index++)
            {
                int expectedCharacter = index < expected.Length ? expected[index] : 0;
                int providedCharacter = index < provided.Length ? provided[index] : 0;
                difference |= expectedCharacter ^ providedCharacter;
            }

            return difference == 0;
        }

        private static bool EqualsAsciiIgnoreCase(
            string value,
            int offset,
            int length,
            string expected)
        {
            if (length != expected.Length || offset < 0 || offset + length > value.Length)
            {
                return false;
            }

            for (int index = 0; index < length; index++)
            {
                char actualCharacter = value[offset + index];
                char expectedCharacter = expected[index];
                if (actualCharacter >= 'A' && actualCharacter <= 'Z')
                {
                    actualCharacter = (char)(actualCharacter + ('a' - 'A'));
                }
                if (expectedCharacter >= 'A' && expectedCharacter <= 'Z')
                {
                    expectedCharacter = (char)(expectedCharacter + ('a' - 'A'));
                }
                if (actualCharacter != expectedCharacter)
                {
                    return false;
                }
            }

            return true;
        }
    }
}