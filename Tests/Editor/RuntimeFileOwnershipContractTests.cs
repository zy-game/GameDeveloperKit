using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class RuntimeFileOwnershipContractTests
    {
        private static readonly Regex s_QualifiedPhysicalIo = new Regex(
            @"\bSystem\s*\.\s*IO\s*\.\s*(File|Directory|FileStream|FileInfo)\b",
            RegexOptions.Compiled);

        private static readonly Regex s_UnqualifiedPhysicalIo = new Regex(
            @"(?<![\.\w])(File|Directory)\s*\.|\b(FileStream|FileInfo)\b",
            RegexOptions.Compiled);

        [Test]
        public void RuntimeOutsideFileSystem_WhenScanned_HasNoPhysicalIoReferences()
        {
            var runtimeRoot = ResolveRuntimeRoot();
            var fileSystemRoot = NormalizePath(Path.Combine(runtimeRoot, "FileSystem")) + "/";
            var violations = new List<string>();
            foreach (var filePath in Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (NormalizePath(filePath).StartsWith(fileSystemRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lines = System.IO.File.ReadAllLines(filePath);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (ContainsPhysicalIoReference(lines[index]))
                    {
                        violations.Add($"{NormalizePath(filePath)}:{index + 1}: {lines[index].Trim()}");
                    }
                }
            }

            Assert.IsEmpty(
                violations,
                "Runtime physical I/O must be owned by Runtime/FileSystem." +
                Environment.NewLine + string.Join(Environment.NewLine, violations));
        }

        [TestCase("using System.IO;", false)]
        [TestCase("Path.Combine(root, name)", false)]
        [TestCase("Stream source", false)]
        [TestCase("catch (IOException exception)", false)]
        [TestCase("System.IO.File.ReadAllBytes(path)", true)]
        [TestCase("Directory.CreateDirectory(path)", true)]
        [TestCase("new FileStream(path, FileMode.Open)", true)]
        [TestCase("new FileInfo(path).Length", true)]
        public void PhysicalIoRule_WhenSourceLineProvided_FlagsOnlyOwnedApis(
            string source,
            bool expected)
        {
            Assert.AreEqual(expected, ContainsPhysicalIoReference(source));
        }

        private static bool ContainsPhysicalIoReference(string source)
        {
            var trimmed = source.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                return false;
            }

            return s_QualifiedPhysicalIo.IsMatch(source) ||
                   s_UnqualifiedPhysicalIo.IsMatch(source);
        }

        private static string ResolveRuntimeRoot()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(App).Assembly);
            if (!string.IsNullOrWhiteSpace(packageInfo?.resolvedPath))
            {
                return Path.Combine(packageInfo.resolvedPath, "Runtime");
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "GameDeveloperKit", "Runtime"));
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
