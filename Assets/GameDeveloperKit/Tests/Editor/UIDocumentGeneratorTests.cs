using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using GameDeveloperKit.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class UIDocumentGeneratorTests
    {
        private readonly List<UnityEngine.Object> m_CreatedObjects = new List<UnityEngine.Object>();
        private readonly List<string> m_TempFolders = new List<string>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < m_CreatedObjects.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(m_CreatedObjects[i]);
            }

            for (var i = 0; i < m_TempFolders.Count; i++)
            {
                if (Directory.Exists(m_TempFolders[i]))
                {
                    Directory.Delete(m_TempFolders[i], true);
                }
            }

            m_CreatedObjects.Clear();
            m_TempFolders.Clear();
        }

        [Test]
        public void Generate_WhenLocalizedTextBindingExists_EmitsRefreshAndSubscription()
        {
            var document = CreateTextDocument(out var text, new[] { "ui.title" });
            var folder = CreateTempFolder();

            InvokeGenerate(document, "Sample", folder);

            var window = System.IO.File.ReadAllText(Path.Combine(folder, "SampleWindow.g.cs"));
            var module = System.IO.File.ReadAllText(Path.Combine(folder, "SampleModule.g.cs"));

            StringAssert.Contains("using GameDeveloperKit.Localization;", window);
            StringAssert.Contains("private bool m_LocalizationSubscribed;", window);
            StringAssert.Contains("App.Localization.LocaleChanged += OnLocaleChanged;", window);
            StringAssert.Contains("private void RefreshLocalization()", window);
            StringAssert.Contains("Model.text_title.text = App.Localization.GetText(\"ui.title\");", window);
            StringAssert.Contains("App.TryGetRegistered<LocalizationModule>(out var localization)", window);
            StringAssert.Contains("return App.UI.OpenAsync<SampleWindow>();", module);
            Assert.IsFalse(module.Contains("Super.UI"), module);

            Assert.Less(
                window.IndexOf("RefreshLocalization();", StringComparison.Ordinal),
                window.IndexOf("await m_Controller.OnAwakeAsync(this, Model);", StringComparison.Ordinal));
            Assert.AreSame(text, document.LocalizedTexts[0].Component);
        }

        [Test]
        public void Generate_WhenNoLocalizedTextBindings_DoesNotReferenceLocalization()
        {
            var document = CreateTextDocument(out _, Array.Empty<string>());
            var folder = CreateTempFolder();

            InvokeGenerate(document, "Plain", folder);

            var window = System.IO.File.ReadAllText(Path.Combine(folder, "PlainWindow.g.cs"));

            Assert.IsFalse(window.Contains("GameDeveloperKit.Localization"), window);
            Assert.IsFalse(window.Contains("RefreshLocalization"), window);
            Assert.IsFalse(window.Contains("LocaleChanged"), window);
        }

        [Test]
        public void Generate_WhenLocalizedTextKeyIsEmpty_Throws()
        {
            var document = CreateTextDocument(out _, new[] { " " });
            var folder = CreateTempFolder();

            var exception = Assert.Throws<GameException>(() => InvokeGenerate(document, "Broken", folder));

            StringAssert.Contains("key cannot be empty", exception.Message);
        }

        [Test]
        public void Generate_WhenLocalizedComponentIsNotSelected_Throws()
        {
            var document = CreateTextDocument(out var text, Array.Empty<string>());
            SetDocumentData(document, new[]
            {
                new UIBindMapping
                {
                    Name = "b_Title",
                    Target = text.gameObject,
                    Components = Array.Empty<Component>()
                }
            }, new[]
            {
                new UILocalizedTextBinding
                {
                    Component = text,
                    Key = "ui.title"
                }
            });
            var folder = CreateTempFolder();

            var exception = Assert.Throws<GameException>(() => InvokeGenerate(document, "Broken", folder));

            StringAssert.Contains("not selected in UI binding", exception.Message);
        }

        [Test]
        public void Generate_WhenLocalizedComponentIsNotText_Throws()
        {
            var document = CreateImageDocument(out var image);
            var folder = CreateTempFolder();

            var exception = Assert.Throws<GameException>(() => InvokeGenerate(document, "Broken", folder));

            StringAssert.Contains("type is not supported", exception.Message);
            StringAssert.Contains("Image", exception.Message);
        }

        [Test]
        public void Generate_WhenLocalizedComponentIsDuplicated_Throws()
        {
            var document = CreateTextDocument(out var text, new[] { "ui.title" });
            SetDocumentData(document, new[]
            {
                new UIBindMapping
                {
                    Name = "b_Title",
                    Target = text.gameObject,
                    Components = new Component[] { text }
                }
            }, new[]
            {
                new UILocalizedTextBinding
                {
                    Component = text,
                    Key = "ui.title"
                },
                new UILocalizedTextBinding
                {
                    Component = text,
                    Key = "ui.title.other"
                }
            });
            var folder = CreateTempFolder();

            var exception = Assert.Throws<GameException>(() => InvokeGenerate(document, "Broken", folder));

            StringAssert.Contains("Duplicate UI localized text binding", exception.Message);
        }

        private UIDocument CreateTextDocument(out Text text, string[] keys)
        {
            var document = CreateDocument("b_Title");
            text = document.transform.GetChild(0).gameObject.AddComponent<Text>();
            var localizedTexts = new UILocalizedTextBinding[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                localizedTexts[i] = new UILocalizedTextBinding
                {
                    Component = text,
                    Key = keys[i]
                };
            }

            SetDocumentData(document, new[]
            {
                new UIBindMapping
                {
                    Name = "b_Title",
                    Target = text.gameObject,
                    Components = new Component[] { text }
                }
            }, localizedTexts);
            return document;
        }

        private UIDocument CreateImageDocument(out Image image)
        {
            var document = CreateDocument("b_Icon");
            image = document.transform.GetChild(0).gameObject.AddComponent<Image>();
            SetDocumentData(document, new[]
            {
                new UIBindMapping
                {
                    Name = "b_Icon",
                    Target = image.gameObject,
                    Components = new Component[] { image }
                }
            }, new[]
            {
                new UILocalizedTextBinding
                {
                    Component = image,
                    Key = "ui.icon"
                }
            });
            return document;
        }

        private UIDocument CreateDocument(string childName)
        {
            var root = new GameObject("Document");
            var child = new GameObject(childName);
            child.transform.SetParent(root.transform, false);
            m_CreatedObjects.Add(root);
            return root.AddComponent<UIDocument>();
        }

        private static void SetDocumentData(UIDocument document, UIBindMapping[] mappings, UILocalizedTextBinding[] localizedTexts)
        {
            SetPrivateField(document, "mappings", mappings);
            SetPrivateField(document, "localizedTexts", localizedTexts);
        }

        private static void SetPrivateField(UIDocument document, string fieldName, object value)
        {
            var field = typeof(UIDocument).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(document, value);
        }

        private string CreateTempFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "uidocument-generator-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            m_TempFolders.Add(folder);
            return folder;
        }

        private static void InvokeGenerate(UIDocument document, string className, string folder)
        {
            var generatorType = Type.GetType("GameDeveloperKit.UIEditor.UIDocumentGenerator, GameDeveloperKit.Editor", true);
            var generate = generatorType.GetMethod("Generate", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(generate);
            try
            {
                generate.Invoke(null, new object[] { document, className, folder, "Assets/UI/Test.prefab", UILayer.Window });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }
    }
}
