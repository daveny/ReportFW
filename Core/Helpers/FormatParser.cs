using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Core.Helpers
{
    public static class FormatParser
    {
        public static FormatOptions ParseFormattingOptions(string formattingString)
        {
            FormatOptions options = new FormatOptions();

            if (string.IsNullOrWhiteSpace(formattingString))
                return options;

            try
            {
                formattingString = formattingString.Trim();
                if (formattingString.StartsWith("{") && formattingString.EndsWith("}"))
                {
                    formattingString = formattingString.Substring(1, formattingString.Length - 2);
                }

                if (formattingString.Contains("row:"))
                {
                    int rowStart = formattingString.IndexOf("row:") + 4;
                    int rowEnd = FindMatchingClosingBrace(formattingString, rowStart);
                    if (rowEnd > rowStart)
                    {
                        string rowOptions = formattingString.Substring(rowStart, rowEnd - rowStart).Trim();
                        int indexStart = rowOptions.IndexOf("index:") + 6;
                        int indexEnd = rowOptions.IndexOf(",", indexStart);
                        if (indexEnd == -1) indexEnd = rowOptions.Length;
                        int styleStart = rowOptions.IndexOf("style:") + 6;
                        int styleEnd = rowOptions.IndexOf(",", styleStart);
                        if (styleEnd == -1) styleEnd = rowOptions.Length;

                        if (indexStart > 6 && indexEnd > indexStart &&
                            styleStart > 6 && styleEnd > styleStart)
                        {
                            string indexStr = rowOptions.Substring(indexStart, indexEnd - indexStart).Trim();
                            string styleStr = rowOptions.Substring(styleStart, styleEnd - styleStart).Trim();
                            if (styleStr.StartsWith("\"") && styleStr.EndsWith("\""))
                                styleStr = styleStr.Substring(1, styleStr.Length - 2);
                            options.RowPattern = new RowPatternOptions
                            {
                                Index = int.Parse(indexStr),
                                Style = styleStr
                            };
                        }
                    }
                }

                if (formattingString.Contains("column:"))
                {
                    int colStart = formattingString.IndexOf("column:") + 7;
                    int colEnd = FindMatchingClosingBrace(formattingString, colStart);
                    if (colEnd > colStart)
                    {
                        string colOptions = formattingString.Substring(colStart, colEnd - colStart).Trim();
                        int nameStart = colOptions.IndexOf("nameContains:") + 13;
                        int nameEnd = colOptions.IndexOf(",", nameStart);
                        if (nameEnd == -1) nameEnd = colOptions.Length;
                        int styleStart = colOptions.IndexOf("style:") + 6;
                        int styleEnd = colOptions.IndexOf(",", styleStart);
                        if (styleEnd == -1) styleEnd = colOptions.Length;

                        if (nameStart > 13 && nameEnd > nameStart &&
                            styleStart > 6 && styleEnd > styleStart)
                        {
                            string nameStr = colOptions.Substring(nameStart, nameEnd - nameStart).Trim();
                            string styleStr = colOptions.Substring(styleStart, styleEnd - styleStart).Trim();
                            if (nameStr.StartsWith("\"") && nameStr.EndsWith("\""))
                                nameStr = nameStr.Substring(1, nameStr.Length - 2);
                            if (styleStr.StartsWith("\"") && styleStr.EndsWith("\""))
                                styleStr = styleStr.Substring(1, styleStr.Length - 2);
                            options.ColumnPattern = new ColumnPatternOptions
                            {
                                NameContains = nameStr,
                                Style = styleStr
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing formatting options: {ex.Message}");
            }

            return options;
        }

        private static Dictionary<string, string> ParseValueColors(string formattingStr)
        {
            var valueColors = new Dictionary<string, string>();
            var match = Regex.Match(formattingStr, @"valueColors\s*:\s*\{([^}]+)\}");
            if (match.Success)
            {
                string pairsStr = match.Groups[1].Value;
                var pairMatches = Regex.Matches(pairsStr, @"['""]([^'""]+)['""]\s*:\s*['""]([^'""]+)['""]");
                foreach (Match pairMatch in pairMatches)
                {
                    valueColors[pairMatch.Groups[1].Value] = pairMatch.Groups[2].Value;
                }
            }
            return valueColors;
        }

        public static BarChartOptions ParseBarChartOptions(Dictionary<string, string> instructions)
        {
            var options = new BarChartOptions
            {
                ChartTitle = "Bar Chart",
                BorderWidth = 1,
                Horizontal = false,
                Stacked = false,
                BackgroundColors = GetDefaultBackgroundColors(),
                BorderColors = GetDefaultBorderColors()
            };

            if (instructions.ContainsKey("sortBy"))
            {
                options.SortBy = instructions["sortBy"];
            }

            if (instructions.ContainsKey("sortDirection"))
            {
                options.SortDirection = instructions["sortDirection"].ToLower();
            }

            if (instructions.ContainsKey("formatting"))
            {
                string formattingStr = instructions["formatting"];
                if (formattingStr.Contains("title:"))
                {
                    int start = formattingStr.IndexOf("title:") + 6;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        options.ChartTitle = formattingStr.Substring(start, end - start).Trim();
                        if (options.ChartTitle.StartsWith("\"") && options.ChartTitle.EndsWith("\""))
                            options.ChartTitle = options.ChartTitle.Substring(1, options.ChartTitle.Length - 2);
                    }
                }

                if (formattingStr.Contains("borderWidth:"))
                {
                    int start = formattingStr.IndexOf("borderWidth:") + 12;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string borderWidthStr = formattingStr.Substring(start, end - start).Trim();
                        int borderWidth;
                        int.TryParse(borderWidthStr, out borderWidth);
                        options.BorderWidth = borderWidth;
                    }
                }

                if (formattingStr.Contains("horizontal:"))
                {
                    int start = formattingStr.IndexOf("horizontal:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string horizontalStr = formattingStr.Substring(start, end - start).Trim();
                        bool horizontal;
                        bool.TryParse(horizontalStr, out horizontal);
                        options.Horizontal = horizontal;
                    }
                }

                if (formattingStr.Contains("stacked:"))
                {
                    int start = formattingStr.IndexOf("stacked:") + 8;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string stackedStr = formattingStr.Substring(start, end - start).Trim();
                        bool stacked;
                        bool.TryParse(stackedStr, out stacked);
                        options.Stacked = stacked;
                    }
                }
                options.ValueColors = ParseValueColors(formattingStr);
                options.FormatOptions = ParseFormattingOptions(formattingStr);
            }

            return options;
        }

        public static LineChartOptions ParseLineChartOptions(Dictionary<string, string> instructions)
        {
            var options = new LineChartOptions
            {
                ChartTitle = "Line Chart",
                ShowPoints = true,
                Tension = 0,
                Colors = GetDefaultColors()
            };

            if (instructions.ContainsKey("sortBy"))
            {
                options.SortBy = instructions["sortBy"];
            }

            if (instructions.ContainsKey("sortDirection"))
            {
                options.SortDirection = instructions["sortDirection"].ToLower();
            }

            if (instructions.ContainsKey("formatting"))
            {
                string formattingStr = instructions["formatting"];
                if (formattingStr.Contains("title:"))
                {
                    int start = formattingStr.IndexOf("title:") + 6;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        options.ChartTitle = formattingStr.Substring(start, end - start).Trim();
                        if (options.ChartTitle.StartsWith("\"") && options.ChartTitle.EndsWith("\""))
                            options.ChartTitle = options.ChartTitle.Substring(1, options.ChartTitle.Length - 2);
                    }
                }

                if (formattingStr.Contains("showPoints:"))
                {
                    int start = formattingStr.IndexOf("showPoints:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showPointsStr = formattingStr.Substring(start, end - start).Trim();
                        bool showPoints;
                        bool.TryParse(showPointsStr, out showPoints);
                        options.ShowPoints = showPoints;
                    }
                }

                if (formattingStr.Contains("tension:"))
                {
                    int start = formattingStr.IndexOf("tension:") + 8;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string tensionStr = formattingStr.Substring(start, end - start).Trim();
                        int tension;
                        int.TryParse(tensionStr, out tension);
                        options.Tension = tension;
                    }
                }
                options.ValueColors = ParseValueColors(formattingStr);
                options.FormatOptions = ParseFormattingOptions(formattingStr);
            }

            return options;
        }

        public static PieChartOptions ParsePieChartOptions(Dictionary<string, string> instructions)
        {
            var options = new PieChartOptions
            {
                ChartTitle = "Pie Chart",
                ShowLegend = true,
                IsDoughnut = false,
                ShowValues = true,
                ShowPercentages = true,
                ValuePosition = "legend",
                BackgroundColors = GetDefaultBackgroundColors(),
                BorderColors = GetDefaultBorderColors(),
                SortDirection = "desc"
            };

            if (instructions.ContainsKey("sortBy"))
            {
                options.SortBy = instructions["sortBy"];
            }

            if (instructions.ContainsKey("sortDirection"))
            {
                options.SortDirection = instructions["sortDirection"].ToLower();
            }

            if (instructions.ContainsKey("formatting"))
            {
                string formattingStr = instructions["formatting"];
                if (formattingStr.Contains("title:"))
                {
                    int start = formattingStr.IndexOf("title:") + 6;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        options.ChartTitle = formattingStr.Substring(start, end - start).Trim();
                        if (options.ChartTitle.StartsWith("\"") && options.ChartTitle.EndsWith("\""))
                            options.ChartTitle = options.ChartTitle.Substring(1, options.ChartTitle.Length - 2);
                    }
                }

                if (formattingStr.Contains("showLegend:"))
                {
                    int start = formattingStr.IndexOf("showLegend:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showLegendStr = formattingStr.Substring(start, end - start).Trim();
                        bool showLegend;
                        bool.TryParse(showLegendStr, out showLegend);
                        options.ShowLegend = showLegend;
                    }
                }

                if (formattingStr.Contains("doughnut:"))
                {
                    int start = formattingStr.IndexOf("doughnut:") + 9;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string doughnutStr = formattingStr.Substring(start, end - start).Trim();
                        bool isDoughnut;
                        bool.TryParse(doughnutStr, out isDoughnut);
                        options.IsDoughnut = isDoughnut;
                    }
                }

                if (formattingStr.Contains("showValues:"))
                {
                    int start = formattingStr.IndexOf("showValues:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showValuesStr = formattingStr.Substring(start, end - start).Trim();
                        bool showValues;
                        bool.TryParse(showValuesStr, out showValues);
                        options.ShowValues = showValues;
                    }
                }

                if (formattingStr.Contains("showPercentages:"))
                {
                    int start = formattingStr.IndexOf("showPercentages:") + 16;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showPercentagesStr = formattingStr.Substring(start, end - start).Trim();
                        bool showPercentages;
                        bool.TryParse(showPercentagesStr, out showPercentages);
                        options.ShowPercentages = showPercentages;
                    }
                }

                if (formattingStr.Contains("valuePosition:"))
                {
                    int start = formattingStr.IndexOf("valuePosition:") + 14;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string valuePositionStr = formattingStr.Substring(start, end - start).Trim();
                        if (valuePositionStr.StartsWith("\"") && valuePositionStr.EndsWith("\""))
                            valuePositionStr = valuePositionStr.Substring(1, valuePositionStr.Length - 2);
                        if (valuePositionStr == "inside" || valuePositionStr == "outside" || valuePositionStr == "legend")
                        {
                            options.ValuePosition = valuePositionStr;
                        }
                    }
                }
                options.ValueColors = ParseValueColors(formattingStr);
                options.FormatOptions = ParseFormattingOptions(formattingStr);
            }

            return options;
        }

        private static int FindMatchingClosingBrace(string text, int openBraceIndex)
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

        private static string[] GetDefaultBackgroundColors()
        {
            return new string[]
            {
                "rgba(75, 192, 192, 0.2)",
                "rgba(255, 99, 132, 0.2)",
                "rgba(54, 162, 235, 0.2)",
                "rgba(255, 206, 86, 0.2)",
                "rgba(153, 102, 255, 0.2)",
                "rgba(255, 159, 64, 0.2)",
                "rgba(201, 203, 207, 0.2)",
                "rgba(100, 149, 237, 0.2)"
            };
        }

        private static string[] GetDefaultBorderColors()
        {
            return new string[]
            {
                "rgba(75, 192, 192, 1)",
                "rgba(255, 99, 132, 1)",
                "rgba(54, 162, 235, 1)",
                "rgba(255, 206, 86, 1)",
                "rgba(153, 102, 255, 1)",
                "rgba(255, 159, 64, 1)",
                "rgba(201, 203, 207, 1)",
                "rgba(100, 149, 237, 1)"
            };
        }

        private static string[] GetDefaultColors()
        {
            return new string[]
            {
                "rgba(75, 192, 192, 1)",
                "rgba(255, 99, 132, 1)",
                "rgba(54, 162, 235, 1)",
                "rgba(255, 206, 86, 1)",
                "rgba(153, 102, 255, 1)",
                "rgba(255, 159, 64, 1)",
                "rgba(201, 203, 207, 1)",
                "rgba(100, 149, 237, 1)"
            };
        }
    }
}
