namespace Cubley.Lnbh26
{
    public static class LNBH26JsonRenderer
    {
        public static string Render(Lnbh26ParsedPayload payload)
        {
            return
                "{\"schema\":\"cubley/v1/lnbh26\",\"channel\":\"" + JsonEscape(payload.Channel) +
                "\",\"registers\":{" +
                "\"status1\":" + RenderRegister(payload.Status1) + "," +
                "\"status2\":" + RenderRegister(payload.Status2) + "," +
                "\"data1\":" + RenderRegister(payload.Data1) + "," +
                "\"data2\":" + RenderRegister(payload.Data2) + "," +
                "\"data3\":" + RenderRegister(payload.Data3) + "," +
                "\"data4\":" + RenderRegister(payload.Data4) +
                "}}";
        }

        private static string RenderRegister(Lnbh26RegisterDecode registerDecode)
        {
            return "{\"raw_u8\":" + registerDecode.RawU8.ToString() +
                   ",\"raw_hex\":\"" + registerDecode.RawHex +
                   "\",\"bits\":[" + RenderBits(registerDecode.Bits) + "]}";
        }

        private static string RenderBits(Lnbh26BitField[] bits)
        {
            string json = string.Empty;

            for (int i = 0; i < bits.Length; i++)
            {
                if (json.Length > 0)
                {
                    json += ",";
                }

                json += RenderBit(bits[i]);
            }

            return json;
        }

        private static string RenderBit(Lnbh26BitField bit)
        {
            string json = "{\"name\":\"" + JsonEscape(bit.Name) +
                          "\",\"value\":\"" + JsonEscape(bit.Value) +
                          "\",\"description\":\"" + JsonEscape(bit.Description) + "\"";

            if (bit.Bit >= 0)
            {
                json += ",\"bit\":" + bit.Bit.ToString();
            }

            json += ",\"mask_hex\":\"" + ToHexU8(bit.Mask) + "\"}";

            return json;
        }

        private static string ToHexU8(int value)
        {
            return "0x" + (value & 0xFF).ToString("X2");
        }

        private static string JsonEscape(string text)
        {
            if (text == null || text.Length == 0)
            {
                return string.Empty;
            }

            string escaped = string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    escaped += "\\\"";
                }
                else if (c == '\\')
                {
                    escaped += "\\\\";
                }
                else if (c == '\r')
                {
                    escaped += "\\r";
                }
                else if (c == '\n')
                {
                    escaped += "\\n";
                }
                else if (c == '\t')
                {
                    escaped += "\\t";
                }
                else
                {
                    escaped += c.ToString();
                }
            }

            return escaped;
        }
    }
}