using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.LubanConfigEditor;
using NUnit.Framework;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class LubanGenerationTransactionTests
    {
        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-luban-transaction-tests", Guid.NewGuid().ToString("N"));
            IODirectory.CreateDirectory(m_Root);
        }

        [TearDown]
        public void TearDown()
        {
            if (IODirectory.Exists(m_Root))
            {
                IODirectory.Delete(m_Root, true);
            }
        }

        [Test]
        public void CommitStagedOutputs_WhenBothOutputsValid_ReplacesBothDirectories()
        {
            var profile = CreateProfile();
            IODirectory.CreateDirectory(profile.OutputCodeDirectory);
            IODirectory.CreateDirectory(profile.OutputDataDirectory);
            IOFile.WriteAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs"), "old");
            IOFile.WriteAllText(Path.Combine(profile.OutputDataDirectory, "old.json"), "old");

            using (var transaction = LubanGenerationTransaction.Create(profile))
            {
                IOFile.WriteAllText(Path.Combine(transaction.StagingCodeDirectory, "new.cs"), "new");
                IOFile.WriteAllText(Path.Combine(transaction.StagingDataDirectory, "new.json"), "new");

                transaction.CommitStagedOutputs();
            }

            Assert.IsFalse(IOFile.Exists(Path.Combine(profile.OutputCodeDirectory, "old.cs")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(profile.OutputDataDirectory, "old.json")));
            Assert.AreEqual("new", IOFile.ReadAllText(Path.Combine(profile.OutputCodeDirectory, "new.cs")));
            Assert.AreEqual("new", IOFile.ReadAllText(Path.Combine(profile.OutputDataDirectory, "new.json")));
        }

        [Test]
        public void CommitStagedOutputs_WhenOneOutputEmpty_PreservesBothDirectories()
        {
            var profile = CreateProfile();
            IODirectory.CreateDirectory(profile.OutputCodeDirectory);
            IODirectory.CreateDirectory(profile.OutputDataDirectory);
            IOFile.WriteAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs"), "old");
            IOFile.WriteAllText(Path.Combine(profile.OutputDataDirectory, "old.json"), "old");

            using (var transaction = LubanGenerationTransaction.Create(profile))
            {
                IOFile.WriteAllText(Path.Combine(transaction.StagingCodeDirectory, "new.cs"), "new");

                Assert.Throws<InvalidOperationException>(() => transaction.CommitStagedOutputs());
            }

            Assert.AreEqual("old", IOFile.ReadAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs")));
            Assert.AreEqual("old", IOFile.ReadAllText(Path.Combine(profile.OutputDataDirectory, "old.json")));
        }

        [Test]
        public void CommitStagedOutputs_WhenSecondCommitFails_RestoresFirstOutput()
        {
            var profile = CreateProfile();
            IODirectory.CreateDirectory(profile.OutputCodeDirectory);
            IODirectory.CreateDirectory(profile.OutputDataDirectory);
            IOFile.WriteAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs"), "old-code");
            IOFile.WriteAllText(Path.Combine(profile.OutputDataDirectory, "old.json"), "old-data");

            using (var transaction = LubanGenerationTransaction.Create(profile))
            {
                IOFile.WriteAllText(Path.Combine(transaction.StagingCodeDirectory, "new.cs"), "new-code");
                IOFile.WriteAllText(Path.Combine(transaction.StagingDataDirectory, "new.json"), "new-data");

                Assert.Throws<IOException>(() => transaction.CommitStagedOutputs(index =>
                {
                    if (index == 1)
                    {
                        throw new IOException("Injected second output failure.");
                    }
                }));
            }

            Assert.AreEqual("old-code", IOFile.ReadAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs")));
            Assert.AreEqual("old-data", IOFile.ReadAllText(Path.Combine(profile.OutputDataDirectory, "old.json")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(profile.OutputCodeDirectory, "new.cs")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(profile.OutputDataDirectory, "new.json")));
        }

        [Test]
        public void Create_WhenOutputsAreNested_Throws()
        {
            var profile = CreateProfile();
            profile.OutputDataDirectory = Path.Combine(profile.OutputCodeDirectory, "Data");

            Assert.Throws<ArgumentException>(() => LubanGenerationTransaction.Create(profile));
        }

        [Test]
        public void SourceCatalog_WhenOneWorkbookIsBroken_KeepsValidWorkbookAndStableTableId()
        {
            var tableRoot = Path.Combine(m_Root, "Tables");
            var dataRoot = Path.Combine(tableRoot, "Datas");
            IODirectory.CreateDirectory(dataRoot);
            IOFile.WriteAllText(
                Path.Combine(tableRoot, "luban.conf"),
                "{\"dataDir\":\"Datas\",\"targets\":[]}");
            var fixture = Path.Combine(
                LubanCommandRunner.GetProjectRoot(),
                "DataTables",
                "Datas",
                "#test.xlsx");
            Assert.IsTrue(IOFile.Exists(fixture), $"缺少 Luban 测试表：{fixture}");
            IOFile.Copy(fixture, Path.Combine(dataRoot, "valid.xlsx"));
            IOFile.WriteAllText(Path.Combine(dataRoot, "broken.xlsx"), "not-an-xlsx");

            var catalog = new LubanSourceCatalog();
            var snapshot = catalog.Refresh(new LubanProjectConfig { TableDirectory = tableRoot });
            var validSource = snapshot.Sources.Single(source => source.DisplayName == "valid.xlsx");
            var table = validSource.Tables.First();

            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == LubanDiagnosticSeverity.Error &&
                diagnostic.SourceId.EndsWith("broken.xlsx", StringComparison.Ordinal)));
            Assert.AreEqual(
                $"{validSource.SourceId}#{table.SheetName}#{table.TableName}",
                table.TableId);
            Assert.IsTrue(catalog.TryReadTable(table.TableId, out var data, out var diagnostic), diagnostic?.Message);
            Assert.IsNotNull(data.Rows);
        }

        [Test]
        public void SourceCatalog_WhenRootWorkbookUsesFourRowHeader_ParsesFieldsAndRows()
        {
            var tableRoot = Path.Combine(m_Root, "Tables");
            IODirectory.CreateDirectory(Path.Combine(tableRoot, "Datas"));
            IOFile.WriteAllText(
                Path.Combine(tableRoot, "luban.conf"),
                "{\"dataDir\":\"Datas\",\"targets\":[]}");
            CreateWorkbook(
                Path.Combine(tableRoot, "LanguageTable.xlsx"),
                "Sheet1",
                new[] { "cs", string.Empty, "cs", "cs" },
                new[] { "int", string.Empty, "string", "string" },
                new[] { "sn", string.Empty, "value_cn", "value_en" },
                new[] { "sn", "description", "Chinese", "English" },
                new[] { "110010", "achievement", "achievement", "achievement1" });

            var catalog = new LubanSourceCatalog();
            var snapshot = catalog.Refresh(new LubanProjectConfig { TableDirectory = tableRoot });
            var source = snapshot.Sources.Single();
            var table = source.Tables.Single();

            Assert.AreEqual("LanguageTable.xlsx", source.DisplayName);
            Assert.AreEqual("TbLanguageTable", table.TableName);
            CollectionAssert.AreEqual(
                new[] { "sn", "value_cn", "value_en" },
                table.Fields.Select(field => field.Name));
            Assert.IsTrue(catalog.TryReadTable(table.TableId, out var data, out var diagnostic), diagnostic?.Message);
            Assert.AreEqual("110010", data.Rows.Single().Values["sn"]);
            Assert.AreEqual("achievement", data.Rows.Single().Values["value_cn"]);
            Assert.IsFalse(snapshot.Diagnostics.Any(item =>
                item.Severity == LubanDiagnosticSeverity.Warning ||
                item.Severity == LubanDiagnosticSeverity.Error));
        }

        [Test]
        public void SourceCatalog_RepeatedRefreshKeepsRevisionUntilWorkbookContentChanges()
        {
            var tableRoot = Path.Combine(m_Root, "Tables");
            IODirectory.CreateDirectory(tableRoot);
            var workbookPath = Path.Combine(tableRoot, "LanguageTable.xlsx");
            CreateWorkbook(
                workbookPath,
                "Sheet1",
                new[] { "cs", "cs" },
                new[] { "int", "string" },
                new[] { "sn", "zh-CN" },
                new[] { "sn", "Chinese" },
                new[] { "110010", "成就1" });
            var config = new LubanProjectConfig { TableDirectory = tableRoot };
            var catalog = new LubanSourceCatalog();

            var first = catalog.Refresh(config);
            var unchanged = catalog.Refresh(config);

            Assert.AreEqual(first.Revision, unchanged.Revision);

            CreateWorkbook(
                workbookPath,
                "Sheet1",
                new[] { "cs", "cs" },
                new[] { "int", "string" },
                new[] { "sn", "zh-CN" },
                new[] { "sn", "Chinese" },
                new[] { "110010", "修改后的成就" });
            var changed = catalog.Refresh(config);

            Assert.AreNotEqual(first.Revision, changed.Revision);
            var table = changed.Tables.Single();
            Assert.IsTrue(catalog.TryReadTable(table.TableId, out var data, out var diagnostic), diagnostic?.Message);
            Assert.AreEqual("修改后的成就", data.Rows.Single().Values["zh-CN"]);
        }

        [Test]
        public void CommandPreview_WithFixedClientProfile_UsesExpectedProtocolOnly()
        {
            var workspace = new LubanWorkspaceProfile
            {
                ConfPath = "DataTables/luban.conf",
                DefaultTarget = "client"
            };
            var profile = new LubanGenerationProfile
            {
                Target = "client",
                CodeTarget = "cs-simple-json",
                DataTarget = "json",
                ValidationFailAsError = true,
                IncludeTag = string.Empty,
                ExcludeTag = string.Empty,
                Variant = string.Empty,
                Pipeline = string.Empty,
                Xargs = string.Empty
            };
            profile.EnsureDefaults();

            var preview = LubanCommandPreview.CreateGenerate(
                "Tools/Luban/Luban.dll",
                workspace,
                profile,
                "staging/code",
                "staging/data");

            StringAssert.Contains("-t client", preview.Arguments);
            StringAssert.Contains("-c cs-simple-json", preview.Arguments);
            StringAssert.Contains("-d json", preview.Arguments);
            StringAssert.Contains("--validationFailAsError", preview.Arguments);
            StringAssert.DoesNotContain("--pipeline", preview.Arguments);
            StringAssert.DoesNotContain("--variant", preview.Arguments);
            StringAssert.DoesNotContain("--customTemplateDir", preview.Arguments);
        }

        [Test]
        public void EnsureTargetTopModule_WhenClientExists_PreservesUnknownConfiguration()
        {
            var confPath = Path.Combine(m_Root, "luban.conf");
            IOFile.WriteAllText(
                confPath,
                "{\"custom\":{\"keep\":true},\"targets\":[{\"name\":\"client\",\"topModule\":\"old\",\"unknown\":7}]}");
            var model = LubanConfModel.Load(confPath);

            Assert.IsTrue(model.EnsureTargetTopModule("client", "Game.Config"));
            model.Save();
            var saved = IOFile.ReadAllText(confPath);

            StringAssert.Contains("\"topModule\": \"Game.Config\"", saved);
            StringAssert.Contains("\"unknown\": 7", saved);
            StringAssert.Contains("\"keep\": true", saved);
        }

        private LubanGenerationProfile CreateProfile()
        {
            var profile = new LubanGenerationProfile
            {
                OutputCodeDirectory = Path.Combine(m_Root, "Code"),
                OutputDataDirectory = Path.Combine(m_Root, "Data")
            };
            profile.EnsureDefaults();
            return profile;
        }

        private static void CreateWorkbook(string path, string sheetName, params string[][] rows)
        {
            var spreadsheetNamespace = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var packageRelationships = (XNamespace)"http://schemas.openxmlformats.org/package/2006/relationships";
            var officeRelationships = (XNamespace)"http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            using (var stream = IOFile.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteXmlEntry(
                    archive,
                    "xl/workbook.xml",
                    new XDocument(
                        new XElement(
                            spreadsheetNamespace + "workbook",
                            new XAttribute(XNamespace.Xmlns + "r", officeRelationships),
                            new XElement(
                                spreadsheetNamespace + "sheets",
                                new XElement(
                                    spreadsheetNamespace + "sheet",
                                    new XAttribute("name", sheetName),
                                    new XAttribute("sheetId", "1"),
                                    new XAttribute(officeRelationships + "id", "rId1"))))));
                WriteXmlEntry(
                    archive,
                    "xl/_rels/workbook.xml.rels",
                    new XDocument(
                        new XElement(
                            packageRelationships + "Relationships",
                            new XElement(
                                packageRelationships + "Relationship",
                                new XAttribute("Id", "rId1"),
                                new XAttribute(
                                    "Type",
                                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                                new XAttribute("Target", "worksheets/sheet1.xml")))));

                var sheetData = new XElement(spreadsheetNamespace + "sheetData");
                for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
                {
                    var rowNumber = rowIndex + 1;
                    var row = new XElement(
                        spreadsheetNamespace + "row",
                        new XAttribute("r", rowNumber));
                    for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
                    {
                        var value = rows[rowIndex][columnIndex];
                        if (string.IsNullOrEmpty(value))
                        {
                            continue;
                        }

                        row.Add(new XElement(
                            spreadsheetNamespace + "c",
                            new XAttribute("r", $"{GetColumnName(columnIndex + 1)}{rowNumber}"),
                            new XAttribute("t", "inlineStr"),
                            new XElement(
                                spreadsheetNamespace + "is",
                                new XElement(spreadsheetNamespace + "t", value))));
                    }

                    sheetData.Add(row);
                }

                WriteXmlEntry(
                    archive,
                    "xl/worksheets/sheet1.xml",
                    new XDocument(
                        new XElement(
                            spreadsheetNamespace + "worksheet",
                            sheetData)));
            }
        }

        private static void WriteXmlEntry(ZipArchive archive, string name, XDocument document)
        {
            var entry = archive.CreateEntry(name);
            using (var stream = entry.Open())
            {
                document.Save(stream);
            }
        }

        private static string GetColumnName(int column)
        {
            var result = string.Empty;
            while (column > 0)
            {
                column--;
                result = (char)('A' + column % 26) + result;
                column /= 26;
            }

            return result;
        }
    }
}
