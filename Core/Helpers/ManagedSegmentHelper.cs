using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Web;

namespace Core.Helpers
{
    public sealed class ManagedSegmentOption
    {
        public ManagedSegmentOption(string value, string label)
        {
            Value = value ?? string.Empty;
            Label = label ?? value ?? string.Empty;
        }

        public string Value { get; }
        public string Label { get; }
    }

    public sealed class ManagedSegmentInfo
    {
        public ManagedSegmentInfo(IEnumerable<ManagedSegmentOption> segments, ManagedSegmentOption adminOption)
        {
            Segments = segments?.ToList() ?? new List<ManagedSegmentOption>();
            AdminOption = adminOption;
        }

        public IReadOnlyList<ManagedSegmentOption> Segments { get; }
        public ManagedSegmentOption AdminOption { get; }
        public bool IsAdmin => AdminOption != null;

        public IEnumerable<ManagedSegmentOption> EnumerateAll()
        {
            if (AdminOption != null)
                yield return AdminOption;

            foreach (var option in Segments)
                yield return option;
        }
    }

    public static class ManagedSegmentHelper
    {
        private static readonly char[] Separator = { ';', ',', '|' };

        public static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var lowered = value.Trim();
            return lowered.Equals("true", StringComparison.OrdinalIgnoreCase)
                || lowered.Equals("1", StringComparison.OrdinalIgnoreCase)
                || lowered.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        public static ManagedSegmentInfo GetManagedSegmentInfo(IPrincipal principal = null)
        {
            var targetPrincipal = principal ?? HttpContext.Current?.User ?? Thread.CurrentPrincipal;
            var pattern = ConfigurationManager.AppSettings["ManagedSegmentGroupPattern"] ?? @"EUR\app_eur_contar_*";
            var adminGroupsSetting = ConfigurationManager.AppSettings["ManagedSegmentAdminGroups"] ?? string.Empty;
            var adminLabel = ConfigurationManager.AppSettings["ManagedSegmentAdminLabel"] ?? "All segments (admin)";
            var adminValue = ConfigurationManager.AppSettings["ManagedSegmentAdminValue"] ?? string.Empty;

            var adminGroups = adminGroupsSetting
                .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var segments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool isAdmin = false;

            var identity = ResolveIdentity(targetPrincipal);
            if (identity?.Groups != null)
            {
                foreach (var groupSid in identity.Groups)
                {
                    string name = null;
                    try
                    {
                        name = groupSid.Translate(typeof(NTAccount)).ToString();
                    }
                    catch (IdentityNotMappedException)
                    {
                        continue;
                    }
                    catch (SystemException)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (MatchesPattern(name, pattern))
                        segments.Add(name);

                    if (adminGroups.Count > 0)
                    {
                        if (adminGroups.Contains(name))
                            isAdmin = true;
                    }
                    else if (IsDefaultAdminGroup(name))
                    {
                        isAdmin = true;
                    }
                }
            }

            var orderedSegments = segments
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Select(s => new ManagedSegmentOption(s, FormatLabel(s)))
                .ToList();

            ManagedSegmentOption adminOption = null;
            if (isAdmin)
                adminOption = new ManagedSegmentOption(adminValue, adminLabel);

            return new ManagedSegmentInfo(orderedSegments, adminOption);
        }

        public static DataTable BuildOptionsTable(ManagedSegmentInfo info)
        {
            var table = new DataTable();
            table.Columns.Add("value", typeof(string));
            table.Columns.Add("text", typeof(string));

            if (info != null)
            {
                foreach (var option in info.EnumerateAll())
                    table.Rows.Add(option.Value ?? string.Empty, option.Label ?? string.Empty);
            }

            return table;
        }

        public static void PrepareInstructionsForManagedFilter(IDictionary<string, string> instructions)
        {
            if (instructions == null) return;
            instructions["managedSegment"] = "true";
            instructions["type"] = "dropdown";
            instructions["valueField"] = "value";
            instructions["textField"] = "text";
            instructions["query"] = string.Empty;
            instructions["required"] = "true";
        }

        public static string ResolveValue(Dictionary<string, string> requestParameters, string paramName, string defaultValue, ManagedSegmentInfo info)
        {
            if (string.IsNullOrWhiteSpace(paramName))
                return string.Empty;

            if (requestParameters == null)
                requestParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            requestParameters.TryGetValue(paramName, out string rawValue);
            var managedValue = BuildManagedSegmentValue(rawValue, defaultValue, info, out var isAdmin);
            requestParameters[paramName] = managedValue ?? string.Empty;
            requestParameters[paramName + "_admin"] = isAdmin ? "1" : "0";
            return managedValue ?? string.Empty;
        }

        private static string BuildManagedSegmentValue(string currentValue, string defaultValue, ManagedSegmentInfo info, out bool isAdmin)
        {
            isAdmin = info?.IsAdmin == true;

            var values = new List<string>();

            if (info != null)
            {
                foreach (var segment in info.Segments)
                {
                    if (!string.IsNullOrWhiteSpace(segment.Value))
                        values.Add(segment.Value);
                }

                if (isAdmin && info.AdminOption != null && !string.IsNullOrWhiteSpace(info.AdminOption.Value))
                    values.Add(info.AdminOption.Value);
            }

            if (!isAdmin && values.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(currentValue))
                    values.Add(currentValue);
                else if (!string.IsNullOrWhiteSpace(defaultValue))
                    values.Add(defaultValue);
            }

            if (values.Count == 0)
                return string.Empty;

            var distinct = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return distinct.Count > 0 ? string.Join(",", distinct) : string.Empty;
        }

        private static WindowsIdentity ResolveIdentity(IPrincipal principal)
        {
            if (principal is WindowsPrincipal wp && wp.Identity is WindowsIdentity wi)
                return wi;

            var httpIdentity = HttpContext.Current?.Request?.LogonUserIdentity;
            if (httpIdentity != null)
                return httpIdentity;

            return WindowsIdentity.GetCurrent();
        }

        private static bool MatchesPattern(string value, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(value))
                return false;

            pattern = pattern.Trim();
            if (pattern.EndsWith("*", StringComparison.Ordinal))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDefaultAdminGroup(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.EndsWith("_admin", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("-admin", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatLabel(string group)
        {
            if (string.IsNullOrWhiteSpace(group))
                return string.Empty;

            var idx = group.IndexOf('\\');
            return idx >= 0 && idx < group.Length - 1 ? group.Substring(idx + 1) : group;
        }
    }
}
