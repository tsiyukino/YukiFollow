using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.ndmf;
using TsiYuki.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace TsiYuki.Follow.Editor
{
    // UI strings (Localization/<code>.txt) for both the inspectors and NDMF's error report window.
    internal static class FollowText
    {
        public const string Package = "moe.tsiyuki.follow";
        public static readonly YukiLocalizer L = new YukiLocalizer(Package);

        static string _version;

        public static string Version
        {
            get
            {
                if (_version != null) return _version;
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FollowText).Assembly);
                return _version = info != null ? "v" + info.version : "";
            }
        }

        static nadena.dev.ndmf.localization.Localizer _ndmf;

        // NDMF shows errors in its own language setting; we feed it our tables.
        public static nadena.dev.ndmf.localization.Localizer Ndmf => _ndmf ?? (_ndmf = new nadena.dev.ndmf.localization.Localizer("en-us", () => new List<(string, Func<string, string>)>
        {
            ("en-us", Table("en")),
            ("zh-hans", Table("zh-Hans")),
            ("ja-jp", Table("ja")),
        }));

        public static void Report(ErrorSeverity severity, string key, UnityEngine.Object context, params object[] args)
        {
            // Strings fill the message; a trailing Unity object becomes a clickable reference.
            var all = (args ?? new object[0]).Select(a => (object)(a?.ToString() ?? "")).ToList();
            if (context != null) all.Add(context);
            ErrorReport.ReportError(Ndmf, severity, key, all.ToArray());
        }

        static Func<string, string> Table(string code)
        {
            var table = new Dictionary<string, string>();
            var path = Path.GetFullPath($"Packages/{Package}/Localization/{code}.txt");
            if (File.Exists(path))
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    table[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                }
            return key => table.TryGetValue(key, out var v) ? v : null;
        }
    }
}
