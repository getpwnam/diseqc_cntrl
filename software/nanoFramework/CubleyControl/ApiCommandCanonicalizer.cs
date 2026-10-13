namespace CubleyControl
{
    internal static class ApiCommandCanonicalizer
    {
        internal static string Build(JsonObject command, int contractVersion)
        {
            string[] keys = new string[JsonObject.MaxMembers];
            int keyCount = 0;
            for (int index = 0; index < command.Count; index++)
            {
                string key = command.KeyAt(index);
                if (key != "v")
                {
                    keys[keyCount++] = key;
                }
            }

            for (int index = 1; index < keyCount; index++)
            {
                string current = keys[index];
                int destination = index;
                while (destination > 0 && CompareOrdinal(keys[destination - 1], current) > 0)
                {
                    keys[destination] = keys[destination - 1];
                    destination--;
                }

                keys[destination] = current;
            }

            JsonBuilder builder = new JsonBuilder().AddInt("v", contractVersion);
            for (int index = 0; index < keyCount; index++)
            {
                string key = keys[index];
                int memberIndex = command.IndexOf(key);
                int kind = command.KindAt(memberIndex);
                if (kind == JsonObject.KindString)
                {
                    builder.AddString(key, command.StringAt(memberIndex));
                }
                else if (kind == JsonObject.KindInt)
                {
                    builder.AddInt(key, command.NumberAt(memberIndex));
                }
                else if (kind == JsonObject.KindBool)
                {
                    builder.AddBool(key, command.NumberAt(memberIndex) != 0);
                }
                else
                {
                    builder.AddRaw(key, "null");
                }
            }

            return builder.Build();
        }

        private static int CompareOrdinal(string left, string right)
        {
            int commonLength = left.Length < right.Length ? left.Length : right.Length;
            for (int index = 0; index < commonLength; index++)
            {
                int difference = left[index] - right[index];
                if (difference != 0)
                {
                    return difference;
                }
            }

            return left.Length - right.Length;
        }
    }
}
