using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Helpers
{
    public class InstructionParser : IInstructionParser
    {
        public Dictionary<string, string> ParseInstructions(string instructionContent)
        {
            var instructions = new Dictionary<string, string>();
            var parts = new List<string>();
            bool inQuotes = false;
            bool inSquareBrackets = false;
            bool inCurlyBraces = false;
            int startIndex = 0;
            char quoteChar = '"';

            for (int i = 0; i < instructionContent.Length; i++)
            {
                char c = instructionContent[i];

                if ((c == '"' || c == '\'') && (i == 0 || instructionContent[i - 1] != '\\'))
                {
                    if (!inQuotes) { inQuotes = true; quoteChar = c; }
                    else if (c == quoteChar) { inQuotes = false; }
                }
                else if (c == '[' && !inQuotes) { inSquareBrackets = true; }
                else if (c == ']' && !inQuotes) { inSquareBrackets = false; }
                else if (c == '{' && !inQuotes) { inCurlyBraces = true; }
                else if (c == '}' && !inQuotes) { inCurlyBraces = false; }
                else if (c == ';' && !inQuotes && !inSquareBrackets && !inCurlyBraces)
                {
                    parts.Add(instructionContent.Substring(startIndex, i - startIndex).Trim());
                    startIndex = i + 1;
                }
            }

            if (startIndex < instructionContent.Length)
            {
                parts.Add(instructionContent.Substring(startIndex).Trim());
            }

            foreach (string part in parts)
            {
                int equalsPos = part.IndexOf('=');
                if (equalsPos > 0)
                {
                    string key = part.Substring(0, equalsPos).Trim();
                    string value = part.Substring(equalsPos + 1).Trim();

                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    // JAVÍTVA: Ha a kulcs a "query", a sortöréseket szóközökre cseréljük.
                    // Ez lehetővé teszi a formázott, többsoros SQL írását a .thtml fájlban.
                    if (key.Equals("query", StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Replace("\r\n", " ").Replace("\n", " ").Trim();
                    }

                    instructions[key] = value;
                }
            }

            return instructions;
        }
    }
}
