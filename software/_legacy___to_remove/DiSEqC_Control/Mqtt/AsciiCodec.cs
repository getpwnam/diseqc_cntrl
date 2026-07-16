namespace DiSEqC_Control.Mqtt
{
    /// <summary>
    /// Minimal ASCII codec used in lieu of <c>System.Text.Encoding.UTF8</c>.
    /// The firmware build does not register native bindings for
    /// <c>nanoFramework.System.Text</c>, so calling
    /// <c>Encoding.UTF8.GetBytes</c> would fail with a missing-internal-call abort.
    /// All MQTT topics, client IDs, and config payloads we use are ASCII, so
    /// a plain 1-byte-per-char codec is sufficient.
    /// </summary>
    public static class AsciiCodec
    {
        public static byte[] GetBytes(string text)
        {
            if (text == null)
            {
                return new byte[0];
            }

            byte[] result = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                result[i] = (c <= 0x7F) ? (byte)c : (byte)'?';
            }
            return result;
        }

        public static string GetString(byte[] bytes, int offset, int length)
        {
            if (bytes == null || length <= 0)
            {
                return string.Empty;
            }

            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                byte b = bytes[offset + i];
                chars[i] = (b <= 0x7F) ? (char)b : '?';
            }
            return new string(chars);
        }
    }
}
