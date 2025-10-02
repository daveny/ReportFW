using System.Collections.Generic;
using Core.Helpers;

namespace Core.Models
{
    public class ManagedSegmentDebugViewModel
    {
        public string Pattern { get; set; }
        public IReadOnlyList<string> AdminGroups { get; set; } = new List<string>();
        public IReadOnlyList<string> AllGroups { get; set; } = new List<string>();
        public ManagedSegmentInfo Info { get; set; }
        public string CombinedValue { get; set; }
        public string AdminFlag { get; set; }
        public string LookupAccountName { get; set; }
        public string EffectiveAccountName { get; set; }
        public string SignedInAccountName { get; set; }
        public string LookupError { get; set; }
        public bool IsImpersonating => !string.IsNullOrWhiteSpace(LookupAccountName);
    }
}
