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

            // Split by semicolons that are not inside quotes or square brackets or curly braces
            List<string> parts = new List<string>();
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
                    if (!inQuotes)
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else if (c == quoteChar)
                    {
                        inQuotes = false;
                    }
                }
                else if (c == '[' && !inQuotes)
                {
                    inSquareBrackets = true;
                }
                else if (c == ']' && !inQuotes)
                {
                    inSquareBrackets = false;
                }
                else if (c == '{' && !inQuotes)
                {
                    inCurlyBraces = true;
                }
                else if (c == '}' && !inQuotes)
                {
                    inCurlyBraces = false;
                }
                else if (c == ';' && !inQuotes && !inSquareBrackets && !inCurlyBraces)
                {
                    // Found a semicolon outside quotes, brackets, and braces
                    parts.Add(instructionContent.Substring(startIndex, i - startIndex).Trim());
                    startIndex = i + 1;
                }
            }

            // Add the last part
            if (startIndex < instructionContent.Length)
            {
                parts.Add(instructionContent.Substring(startIndex).Trim());
            }

            // Process each part as a key-value pair
            foreach (string part in parts)
            {
                int equalsPos = part.IndexOf('=');
                if (equalsPos > 0)
                {
                    string key = part.Substring(0, equalsPos).Trim();
                    string value = part.Substring(equalsPos + 1).Trim();

                    // Remove surrounding quotes if present
                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    instructions[key] = value;
                }
            }

            return instructions;
        }

        private Dictionary<string, string> DebugParseInstructions(string instructionContent)
        {
            var instructions = new Dictionary<string, string>();

            // Simple string split for debugging
            string[] parts = instructionContent.Split(';');

            foreach (string part in parts)
            {
                string trimmedPart = part.Trim();
                int equalsPos = trimmedPart.IndexOf('=');

                if (equalsPos > 0)
                {
                    string key = trimmedPart.Substring(0, equalsPos).Trim();
                    string value = trimmedPart.Substring(equalsPos + 1).Trim();

                    // Remove surrounding quotes if present
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    else if (value.StartsWith("'") && value.EndsWith("'"))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    instructions[key] = value;

                    // Debug output
                    System.Diagnostics.Debug.WriteLine($"Parsed: {key}={value}");
                }
            }

            return instructions;
        }

        private void ParseInstructionParts(string content, Dictionary<string, string> instructions)
        {
            // Your existing parsing logic here
            // Split by semicolons not in quotes or braces
            // ...
        }

        // Add this helper method to find matching closing brace
        private int FindMatchingClosingBrace(string text, int openBraceIndex)
        {
            int braceCount = 1;
            for (int i = openBraceIndex + 1; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    braceCount++;
                }
                else if (text[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }
    }
}