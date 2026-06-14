using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using IODirectory = System.IO.Directory;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor
{
    public sealed partial class LubanConfigEditorWindow
    {
        /// <summary>
        /// 创建 Profile Panel。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateProfilePanel()
        {
            var panel = CreatePanel();
            panel.style.minWidth = 0;
            panel.style.paddingRight = 20;

            panel.Add(CreateSectionHeader("生成配置"));

            m_ProfileSelectorField = CreateDropdownField("Profile");
            m_ProfileSelectorField.RegisterValueChangedCallback(evt =>
            {
                SelectProfileByLabel(evt.newValue);
            });
            panel.Add(m_ProfileSelectorField);

            m_ProfileNameField = CreateProfileTextField("Name", profile => profile.Name, (profile, value) => profile.Name = value);
            m_TargetField = CreateProfileDropdownField("Target", BuildTargetChoices, profile => profile.Target, (profile, value) => profile.Target = value);
            m_CodeTargetField = CreateProfileDropdownField("Code target", BuildCodeTargetChoices, profile => profile.CodeTarget, (profile, value) => profile.CodeTarget = value);
            m_DataTargetField = CreateProfileDropdownField("Data target", BuildDataTargetChoices, profile => profile.DataTarget, (profile, value) => profile.DataTarget = value);
            m_IncludeTagField = CreateProfileTextField("Include tag", profile => profile.IncludeTag, (profile, value) => profile.IncludeTag = value);
            m_ExcludeTagField = CreateProfileTextField("Exclude tag", profile => profile.ExcludeTag, (profile, value) => profile.ExcludeTag = value);
            m_VariantField = CreateProfileTextField("Variant", profile => profile.Variant, (profile, value) => profile.Variant = value);
            m_PipelineField = CreateProfileTextField("Pipeline", profile => profile.Pipeline, (profile, value) => profile.Pipeline = value);
            m_XargsField = CreateProfileTextField("Xargs", profile => profile.Xargs, (profile, value) => profile.Xargs = value, true);
            m_OutputCodeDirectoryField = CreateProfileTextField("Output code", profile => profile.OutputCodeDirectory, (profile, value) => profile.OutputCodeDirectory = value);
            m_OutputDataDirectoryField = CreateProfileTextField("Output data", profile => profile.OutputDataDirectory, (profile, value) => profile.OutputDataDirectory = value);
            m_CustomTemplateDirectoryField = CreateProfileTextField("Custom template dir", profile => profile.CustomTemplateDirectory, (profile, value) => profile.CustomTemplateDirectory = value);

            panel.Add(m_ProfileNameField);
            panel.Add(m_TargetField);
            panel.Add(m_CodeTargetField);
            panel.Add(m_DataTargetField);
            panel.Add(CreateFolderSelectRow(m_OutputCodeDirectoryField, new Button(() => BrowseProfileDirectory(m_OutputCodeDirectoryField, profile => profile.OutputCodeDirectory = m_OutputCodeDirectoryField.value)) { text = "选择" }));
            panel.Add(CreateFolderSelectRow(m_OutputDataDirectoryField, new Button(() => BrowseProfileDirectory(m_OutputDataDirectoryField, profile => profile.OutputDataDirectory = m_OutputDataDirectoryField.value)) { text = "选择" }));

            m_UseCustomTemplateDirToggle = CreateToggleField("Use custom template dir");
            m_UseCustomTemplateDirToggle.RegisterValueChangedCallback(evt =>
            {
                SaveProfileEdit(profile => profile.UseCustomTemplateDir = evt.newValue);
                RefreshProfileFields();
            });
            panel.Add(m_UseCustomTemplateDirToggle);

            m_CustomTemplateDirectoryButton = new Button(() => BrowseProfileDirectory(m_CustomTemplateDirectoryField, profile => profile.CustomTemplateDirectory = m_CustomTemplateDirectoryField.value))
            {
                text = "选择"
            };
            panel.Add(CreateFolderSelectRow(m_CustomTemplateDirectoryField, m_CustomTemplateDirectoryButton));

            m_ValidationFailAsErrorToggle = CreateToggleField("Validation fail as error");
            m_ValidationFailAsErrorToggle.RegisterValueChangedCallback(evt =>
            {
                SaveProfileEdit(profile => profile.ValidationFailAsError = evt.newValue);
            });
            panel.Add(m_ValidationFailAsErrorToggle);

            var advanced = new Foldout
            {
                text = "Advanced",
                value = false
            };
            advanced.style.marginTop = 6;
            panel.Add(advanced);

            advanced.Add(m_IncludeTagField);
            advanced.Add(m_ExcludeTagField);
            advanced.Add(m_VariantField);
            advanced.Add(m_PipelineField);
            advanced.Add(m_XargsField);

            RefreshProfileSelector();
            RefreshProfileFields();
            return panel;
        }

        /// <summary>
        /// 创建 Profile Text Field。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <param name="getter">getter 参数。</param>
        /// <param name="setter">setter 参数。</param>
        /// <param name="multiline">multiline 参数。</param>
        /// <returns>执行结果。</returns>
        private TextField CreateProfileTextField(string label, Func<LubanGenerationProfile, string> getter, Action<LubanGenerationProfile, string> setter, bool multiline = false)
        {
            var field = CreateTextField(label);
            field.isDelayed = true;
            field.multiline = multiline;
            if (multiline)
            {
                field.style.minHeight = 54;
                field.style.height = 68;
                field.style.marginBottom = 10;
            }

            field.RegisterValueChangedCallback(evt =>
            {
                SaveProfileEdit(profile => setter(profile, evt.newValue));
            });
            return field;
        }

        /// <summary>
        /// 创建 Profile Dropdown Field。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <param name="choicesBuilder">choices Builder 参数。</param>
        /// <param name="getter">getter 参数。</param>
        /// <param name="setter">setter 参数。</param>
        /// <returns>执行结果。</returns>
        private DropdownField CreateProfileDropdownField(
            string label,
            Func<LubanGenerationProfile, List<string>> choicesBuilder,
            Func<LubanGenerationProfile, string> getter,
            Action<LubanGenerationProfile, string> setter)
        {
            var field = CreateDropdownField(label);
            field.RegisterValueChangedCallback(evt =>
            {
                SaveProfileEdit(profile => setter(profile, evt.newValue));
            });
            return field;
        }

        /// <summary>
        /// 刷新 Profile Fields。
        /// </summary>
        private void RefreshProfileFields()
        {
            var profile = EnsureGenerationProfile();
            if (profile == null)
            {
                return;
            }

            SetProfileField(m_ProfileNameField, profile.Name);
            SetProfileDropdown(m_TargetField, BuildTargetChoices(profile), profile.Target);
            SetProfileDropdown(m_CodeTargetField, BuildCodeTargetChoices(profile), profile.CodeTarget);
            SetProfileDropdown(m_DataTargetField, BuildDataTargetChoices(profile), profile.DataTarget);
            SetProfileField(m_IncludeTagField, profile.IncludeTag);
            SetProfileField(m_ExcludeTagField, profile.ExcludeTag);
            SetProfileField(m_VariantField, profile.Variant);
            SetProfileField(m_PipelineField, profile.Pipeline);
            SetProfileField(m_XargsField, profile.Xargs);
            SetProfileField(m_OutputCodeDirectoryField, profile.OutputCodeDirectory);
            SetProfileField(m_OutputDataDirectoryField, profile.OutputDataDirectory);
            SetProfileField(m_CustomTemplateDirectoryField, profile.CustomTemplateDirectory);
            m_UseCustomTemplateDirToggle?.SetValueWithoutNotify(profile.UseCustomTemplateDir);
            m_ValidationFailAsErrorToggle?.SetValueWithoutNotify(profile.ValidationFailAsError);
            if (m_CustomTemplateDirectoryField != null)
            {
                m_CustomTemplateDirectoryField.SetEnabled(profile.UseCustomTemplateDir);
            }

            m_CustomTemplateDirectoryButton?.SetEnabled(profile.UseCustomTemplateDir);
            SetProfileDropdown(m_TableScopeField, BuildTableScopeChoices(), LabelFromTableScope(profile.TableSelection.Scope));
            RefreshProfileSelector();
        }

        /// <summary>
        /// 设置 Profile Field。
        /// </summary>
        /// <param name="field">field 参数。</param>
        /// <param name="value">value 参数。</param>
        private static void SetProfileField(TextField field, string value)
        {
            field?.SetValueWithoutNotify(value ?? string.Empty);
        }

        /// <summary>
        /// 设置 Profile Dropdown。
        /// </summary>
        /// <param name="field">field 参数。</param>
        /// <param name="choices">choices 参数。</param>
        /// <param name="value">value 参数。</param>
        private static void SetProfileDropdown(DropdownField field, List<string> choices, string value)
        {
            if (field == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value) is false && choices.Contains(value) is false)
            {
                choices.Insert(0, value);
            }

            if (choices.Count == 0)
            {
                choices.Add(string.Empty);
            }

            field.choices = choices;
            field.SetValueWithoutNotify(string.IsNullOrWhiteSpace(value) ? choices[0] : value);
        }

        /// <summary>
        /// 保存 Profile Edit。
        /// </summary>
        /// <param name="edit">edit 参数。</param>
        private void SaveProfileEdit(Action<LubanGenerationProfile> edit)
        {
            var profile = EnsureGenerationProfile();
            if (profile == null)
            {
                return;
            }

            edit(profile);
            profile.EnsureDefaults();
            m_Settings.SaveSettings();
            RefreshProfileSelector();
            if (m_ConfModel != null)
            {
                RefreshTableIndexSummary();
            }

            RefreshTablePanel();
            RefreshCommandPreview();
            RefreshActionState();
        }

        /// <summary>
        /// 刷新 Profile Selector。
        /// </summary>
        private void RefreshProfileSelector()
        {
            if (m_ProfileSelectorField == null)
            {
                return;
            }

            var choices = m_Settings.GenerationProfiles
                .Where(x => x != null)
                .Select(GetProfileLabel)
                .ToList();
            m_ProfileSelectorField.choices = choices;
            if (choices.Count == 0)
            {
                m_ProfileSelectorField.SetValueWithoutNotify(string.Empty);
                return;
            }

            m_ProfileSelectorField.SetValueWithoutNotify(GetProfileLabel(GetSelectedGenerationProfile()));
        }

        /// <summary>
        /// 选择 Profile By Label。
        /// </summary>
        /// <param name="label">label 参数。</param>
        private void SelectProfileByLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            for (var i = 0; i < m_Settings.GenerationProfiles.Count; i++)
            {
                if (string.Equals(GetProfileLabel(m_Settings.GenerationProfiles[i]), label, StringComparison.Ordinal))
                {
                    m_Settings.SelectedGenerationProfileIndex = i;
                    m_Settings.SaveSettings();
                    RefreshProfileFields();
                    RefreshWorkspaceStatus(m_ConfModel != null, m_ConfModel == null ? "No workspace selected." : "Workspace loaded.");
                    return;
                }
            }
        }

        /// <summary>
        /// 获取 Profile Label。
        /// </summary>
        /// <param name="profile">profile 参数。</param>
        /// <returns>执行结果。</returns>
        private static string GetProfileLabel(LubanGenerationProfile profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            return $"{profile.Name} · {profile.Target}";
        }

        /// <summary>
        /// 浏览 Profile Directory。
        /// </summary>
        /// <param name="field">field 参数。</param>
        /// <param name="apply">apply 参数。</param>
        private void BrowseProfileDirectory(TextField field, Action<LubanGenerationProfile> apply)
        {
            var currentValue = field?.value;
            var startDirectory = string.IsNullOrWhiteSpace(currentValue)
                ? LubanCommandRunner.GetProjectRoot()
                : LubanCommandRunner.GetAbsoluteProjectPath(currentValue);
            if (IODirectory.Exists(startDirectory) is false)
            {
                startDirectory = LubanCommandRunner.GetProjectRoot();
            }

            var selectedPath = EditorUtility.OpenFolderPanel("选择目录", startDirectory, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            field.SetValueWithoutNotify(LubanCommandRunner.ToProjectRelativePath(selectedPath));
            SaveProfileEdit(apply);
            RefreshProfileFields();
        }

        /// <summary>
        /// 构建 Target Choices。
        /// </summary>
        /// <param name="profile">profile 参数。</param>
        /// <returns>执行结果。</returns>
        private List<string> BuildTargetChoices(LubanGenerationProfile profile)
        {
            var choices = m_ConfModel?.Targets?.ToList() ?? new List<string>();
            var workspaceTarget = GetSelectedWorkspace()?.DefaultTarget;
            if (string.IsNullOrWhiteSpace(workspaceTarget) is false && choices.Contains(workspaceTarget) is false)
            {
                choices.Insert(0, workspaceTarget);
            }

            if (choices.Count == 0)
            {
                choices.Add("client");
            }

            return choices;
        }

        /// <summary>
        /// 构建 Code Target Choices。
        /// </summary>
        /// <param name="profile">profile 参数。</param>
        /// <returns>执行结果。</returns>
        private List<string> BuildCodeTargetChoices(LubanGenerationProfile profile)
        {
            var templatesRoot = IOPath.Combine(IOPath.GetDirectoryName(LubanCommandRunner.GetAbsoluteProjectPath(m_Settings.ReleasePath)) ?? string.Empty, "Templates");
            var choices = IODirectory.Exists(templatesRoot)
                ? IODirectory.GetDirectories(templatesRoot)
                    .Select(IOPath.GetFileName)
                    .Where(x => string.IsNullOrWhiteSpace(x) is false && string.Equals(x, "common", StringComparison.OrdinalIgnoreCase) is false)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();
            if (choices.Contains("cs-simple-json") is false)
            {
                choices.Insert(0, "cs-simple-json");
            }

            return choices;
        }

        /// <summary>
        /// 构建 Data Target Choices。
        /// </summary>
        /// <param name="profile">profile 参数。</param>
        /// <returns>执行结果。</returns>
        private List<string> BuildDataTargetChoices(LubanGenerationProfile profile)
        {
            return new List<string> { "json", "json2", "json-convert", "xml", "yaml", "bin", "bin-offset" };
        }

        /// <summary>
        /// 构建 Table Scope Choices。
        /// </summary>
        /// <returns>执行结果。</returns>
        private static List<string> BuildTableScopeChoices()
        {
            return new List<string>
            {
                LabelFromTableScope(LubanTableScope.AllTables),
                LabelFromTableScope(LubanTableScope.SelectedTables)
            };
        }

        /// <summary>
        /// 确保 Generation Profile。
        /// </summary>
        /// <returns>执行结果。</returns>
        private LubanGenerationProfile EnsureGenerationProfile()
        {
            m_Settings.EnsureDefaults();
            if (m_Settings.GenerationProfiles.Count == 0)
            {
                m_Settings.GenerationProfiles.Add(new LubanGenerationProfile());
                m_Settings.SelectedGenerationProfileIndex = 0;
                m_Settings.SaveSettings();
            }

            if (m_Settings.SelectedGenerationProfileIndex < 0
                || m_Settings.SelectedGenerationProfileIndex >= m_Settings.GenerationProfiles.Count)
            {
                m_Settings.SelectedGenerationProfileIndex = 0;
                m_Settings.SaveSettings();
            }

            var profile = m_Settings.GenerationProfiles[m_Settings.SelectedGenerationProfileIndex];
            profile.EnsureDefaults();
            return profile;
        }

        /// <summary>
        /// 获取 Selected Generation Profile。
        /// </summary>
        /// <returns>执行结果。</returns>
        private LubanGenerationProfile GetSelectedGenerationProfile()
        {
            if (m_Settings.GenerationProfiles.Count == 0
                || m_Settings.SelectedGenerationProfileIndex < 0
                || m_Settings.SelectedGenerationProfileIndex >= m_Settings.GenerationProfiles.Count)
            {
                return null;
            }

            return m_Settings.GenerationProfiles[m_Settings.SelectedGenerationProfileIndex];
        }
    }
}
