using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SysIO = System.IO;

namespace GameDeveloperKit.UnityBridge
{
    public static class SkillInstaller
    {
        private static readonly IReadOnlyList<ICliToolAdapter> s_Adapters = new ICliToolAdapter[]
        {
            new ZCodeAdapter(),
            new ClaudeCodeAdapter()
        };

        public static IReadOnlyList<ICliToolAdapter> Adapters => s_Adapters;

        public static IReadOnlyList<IUnityBridgeSkill> Skills => UnityBridgeSkillRegistry.Skills;

        public static void Install(ICliToolAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));

            var settings = UnityBridgeSettings.LoadOrCreate();
            var root = adapter.GetSkillsRoot();
            SysIO.Directory.CreateDirectory(root);

            foreach (var skill in Skills)
            {
                var skillDir = SysIO.Path.Combine(root, skill.Name);
                SysIO.Directory.CreateDirectory(skillDir);
                SysIO.File.WriteAllText(SysIO.Path.Combine(skillDir, "SKILL.md"),
                    BuildSkillMarkdown(skill), new UTF8Encoding(false));
            }
        }

        public static void Uninstall(ICliToolAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));

            var root = adapter.GetSkillsRoot();
            foreach (var skill in Skills)
            {
                var dir = SysIO.Path.Combine(root, skill.Name);
                if (SysIO.Directory.Exists(dir))
                    SysIO.Directory.Delete(dir, true);
            }
        }

        public static bool IsInstalled(ICliToolAdapter adapter)
        {
            if (adapter == null) return false;
            var first = Skills.FirstOrDefault();
            if (first == null) return false;
            var dir = SysIO.Path.Combine(adapter.GetSkillsRoot(), first.Name);
            return SysIO.Directory.Exists(dir)
                && SysIO.File.Exists(SysIO.Path.Combine(dir, "SKILL.md"));
        }

        public static string GetInstallStatusText(ICliToolAdapter adapter)
        {
            return IsInstalled(adapter) ? "Installed" : "Not installed";
        }

    public static string BuildSkillMarkdown(IUnityBridgeSkill skill)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {skill.Name}");
        sb.AppendLine($"description: {EscapeYaml(skill.Description)}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {skill.Name}");
        sb.AppendLine();
        sb.AppendLine(skill.Description);
        sb.AppendLine();
        sb.AppendLine($"**Trigger:** {skill.Trigger}");
        sb.AppendLine();
        sb.AppendLine($"Uses file-based task queue at `Temp/UnityBridge/`.  CLI: `node scripts/unity-bridge.js`");
        sb.AppendLine();
            sb.AppendLine("## Endpoints");
            sb.AppendLine();
            foreach (var ep in skill.Endpoints)
            {
                sb.AppendLine($"- `{ep.Method} {ep.Path}`");
                sb.AppendLine($"  {ep.Description}");
                if (!string.IsNullOrWhiteSpace(ep.BodyExample))
                    sb.AppendLine($"  Body: `{ep.BodyExample}`");
            }
            sb.AppendLine();
            sb.AppendLine("## Examples");
            foreach (var ex in skill.Examples)
                sb.AppendLine($"- {ex}");
            var notes = skill.Notes?.ToArray() ?? Array.Empty<string>();
            if (notes.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Notes");
                foreach (var n in notes)
                    sb.AppendLine($"- {n}");
            }
            return sb.ToString();
        }


        private static string EscapeYaml(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            // Replace characters that break YAML: colons, quotes, newlines
            return text.Replace(":", " -").Replace("\"", "'").Replace("\n", " ").Replace("\r", "");
        }

        public interface ICliToolAdapter
        {
            string DisplayName { get; }
            string GetSkillsRoot();
            bool IsAvailable();
        }

        private sealed class ZCodeAdapter : ICliToolAdapter
        {
            public string DisplayName => "ZCode";

            public string GetSkillsRoot()
            {
                return SysIO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agents", "skills");
            }

            public bool IsAvailable() => true;
        }

        private sealed class ClaudeCodeAdapter : ICliToolAdapter
        {
            public string DisplayName => "Claude Code";

            public string GetSkillsRoot()
            {
                return SysIO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");
            }

            public bool IsAvailable() => true;
        }
    }
}
