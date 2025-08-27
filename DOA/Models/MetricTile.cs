// Fájl: Models/MetricTile.cs
using Core.Helpers;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace Core.Models
{
    public class MetricTile
    {
        public string FileName { get; set; } // Pl. "OpenCAPs"
        public string Name { get; set; }     // Pl. "Open CAPs by Due Category"
        public string Description { get; set; }
        public string Owner { get; set; }
        public bool IsFavorite { get; set; }
    }
}