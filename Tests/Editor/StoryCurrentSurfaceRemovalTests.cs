using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.StoryEditor.Model;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryCurrentSurfaceRemovalTests
    {
        private static readonly string[] s_LegacyPublicTerms =
        {
            "AuthoringChapter",
            "ChapterId",
            "EntryChapterId",
            "EntryEpisodeId",
            "JumpChapter",
            "JumpEpisode",
            "GraphLayout"
        };

        private static readonly string[] s_SpecializedInteractionTerms =
        {
            "Qte",
            "QTE",
            "MiniGame",
            "Unlock"
        };

        [Test]
        public void CurrentStoryApi_WhenInspected_ContainsNoLegacyPublicContracts()
        {
            var assemblies = new[] { typeof(StoryModule).Assembly, typeof(AuthoringAsset).Assembly };
            var types = assemblies
                .SelectMany(GetLoadableExportedTypes)
                .Where(IsStoryType)
                .ToArray();

            Assert.IsNull(typeof(AuthoringAsset).GetProperty(
                "LegacyEntryEpisodeId",
                BindingFlags.Instance | BindingFlags.Public));
            Assert.IsFalse(Enum.GetNames(typeof(NodeKind)).Any(IsLegacyNodeKind));
            Assert.IsFalse(Enum.GetNames(typeof(TransitionTargetKind)).Any(x =>
                string.Equals(x, "Episode", StringComparison.Ordinal) ||
                x.IndexOf("Chapter", StringComparison.Ordinal) >= 0));

            var legacyTypes = types.Where(type =>
                s_LegacyPublicTerms.Any(term => type.Name.IndexOf(term, StringComparison.Ordinal) >= 0) ||
                s_SpecializedInteractionTerms.Any(term => type.Name.IndexOf(term, StringComparison.Ordinal) >= 0))
                .Select(x => x.FullName)
                .ToArray();
            Assert.IsEmpty(legacyTypes, string.Join(Environment.NewLine, legacyTypes));

            var storyPrefixed = types
                .Where(x => x.Name.StartsWith("Story", StringComparison.Ordinal) && x != typeof(StoryModule))
                .Select(x => x.FullName)
                .ToArray();
            Assert.IsEmpty(storyPrefixed, string.Join(Environment.NewLine, storyPrefixed));

            var legacyMembers = types
                .SelectMany(PublicMemberNames)
                .Where(name => s_LegacyPublicTerms.Any(term => name.IndexOf(term, StringComparison.Ordinal) >= 0))
                .ToArray();
            Assert.IsEmpty(legacyMembers, string.Join(Environment.NewLine, legacyMembers));
        }

        [Test]
        public void CurrentStorySource_WhenScanned_ContainsNoLegacyProductionFallbacks()
        {
            var files = ProductionStoryFiles().ToArray();
            var violations = new List<string>();
            var typePattern = new Regex(@"\b(?:class|struct|interface|enum|record)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);

            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                var lines = System.IO.File.ReadAllLines(files[fileIndex]);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var line = lines[lineIndex];
                    if (line.Contains("FormerlySerializedAs") || line.Contains("MovedFrom"))
                    {
                        continue;
                    }

                    if (line.Contains("ResolveLegacyRoute") ||
                        line.Contains("CompileLegacyRoute") ||
                        line.Contains("LegacyRouteResolver"))
                    {
                        violations.Add(Location(files[fileIndex], lineIndex, line));
                    }

                    var match = typePattern.Match(line);
                    if (match.Success is false)
                    {
                        continue;
                    }

                    var name = match.Groups["name"].Value;
                    if ((name.StartsWith("Story", StringComparison.Ordinal) && name != nameof(StoryModule)) ||
                        s_LegacyPublicTerms.Any(term => name.IndexOf(term, StringComparison.Ordinal) >= 0) ||
                        s_SpecializedInteractionTerms.Any(term => name.IndexOf(term, StringComparison.Ordinal) >= 0))
                    {
                        violations.Add(Location(files[fileIndex], lineIndex, line));
                    }
                }
            }

            Assert.IsEmpty(violations, string.Join(Environment.NewLine, violations));
        }

        private static bool IsLegacyNodeKind(string name)
        {
            return name.IndexOf("Jump", StringComparison.Ordinal) >= 0 ||
                   name.IndexOf("Qte", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("MiniGame", StringComparison.Ordinal) >= 0 ||
                   name.IndexOf("Unlock", StringComparison.Ordinal) >= 0;
        }

        private static bool IsStoryType(Type type)
        {
            return type?.Namespace != null &&
                   (type.Namespace.StartsWith("GameDeveloperKit.Story", StringComparison.Ordinal) ||
                    type.Namespace.StartsWith("GameDeveloperKit.StoryEditor", StringComparison.Ordinal));
        }

        private static IEnumerable<string> PublicMemberNames(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;
            foreach (var member in type.GetMembers(flags))
            {
                yield return type.FullName + "." + member.Name;
                if (member is MethodBase method)
                {
                    foreach (var parameter in method.GetParameters())
                    {
                        yield return type.FullName + "." + member.Name + "(" + parameter.Name + ")";
                    }
                }
            }
        }

        private static IEnumerable<string> ProductionStoryFiles()
        {
            var framework = FindFrameworkRoot();
            foreach (var root in new[] { "Runtime/Story", "Editor/StoryEditor" })
            {
                var absoluteRoot = Path.Combine(framework, root.Replace('/', Path.DirectorySeparatorChar));
                foreach (var file in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                {
                    var normalized = file.Replace('\\', '/');
                    if (normalized.Contains("/Editor/StoryEditor/Migration/") ||
                        normalized.EndsWith("/Editor/StoryEditor/Excel/LegacyImporter.cs", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return file;
                }
            }
        }

        private static IEnumerable<Type> GetLoadableExportedTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(x => x != null && x.IsPublic);
            }
        }

        private static string FindFrameworkRoot()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "Assets", "GameDeveloperKit");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("GameDeveloperKit framework root was not found.");
        }

        private static string Location(string file, int lineIndex, string line)
        {
            return file.Replace('\\', '/') + ":" + (lineIndex + 1) + ": " + line.Trim();
        }
    }
}
