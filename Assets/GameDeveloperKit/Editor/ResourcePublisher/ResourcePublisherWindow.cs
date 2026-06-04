using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourceEditor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourcePublisher
{
    public sealed class ResourcePublisherWindow : EditorWindow
    {
        private const string WindowTitle = "Resource Publisher";
        private const string UxmlPath = "Assets/GameDeveloperKit/Editor/ResourcePublisher/UI/ResourcePublisherWindow.uxml";

        private readonly List<StorageRegionInfo> m_Regions = new List<StorageRegionInfo>();
        private readonly List<StorageBucketInfo> m_Buckets = new List<StorageBucketInfo>();
        private readonly List<BuildVersionItem> m_BuildVersions = new List<BuildVersionItem>();
        private List<IObjectStorageProvider> m_Providers = new List<IObjectStorageProvider>();
        private ResourcePublisherSettings m_Settings;
        private ListView m_ChannelList;
        private ListView m_BuildVersionList;
        private Label m_StatusLabel;
        private Label m_BucketSummaryLabel;
        private Label m_BuildSummaryLabel;
        private VisualElement m_ChannelEmptyState;
        private VisualElement m_ChannelDetail;
        private TextField m_ChannelNameField;
        private DropdownField m_BuildTargetDropdown;
        private DropdownField m_PlatformDropdown;
        private TextField m_SecretIdField;
        private TextField m_SecretKeyField;
        private DropdownField m_RegionDropdown;
        private DropdownField m_BucketDropdown;
        private string m_CurrentRemoteVersion;

        [MenuItem("GameDeveloperKit/Resource Publisher")]
        public static void Open()
        {
            var window = GetWindow<ResourcePublisherWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(980, 600);
            window.Show();
        }

        public void CreateGUI()
        {
            m_Settings = ResourcePublisherSettings.LoadOrCreate();

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label($"Missing UXML: {UxmlPath}"));
                return;
            }

            rootVisualElement.Clear();
            visualTree.CloneTree(rootVisualElement);
            ApplyEditorTheme();
            QueryElements();
            BindToolbar();
            BindChannelList();
            BindBuildVersionList();
            RefreshAll();
        }

        private void ApplyEditorTheme()
        {
            var root = rootVisualElement.Q<VisualElement>(className: "resource-publisher");
            if (root == null)
            {
                return;
            }

            root.EnableInClassList("resource-publisher--dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("resource-publisher--light", EditorGUIUtility.isProSkin is false);
        }

        private void QueryElements()
        {
            m_ChannelList = rootVisualElement.Q<ListView>("channel-list");
            m_BuildVersionList = rootVisualElement.Q<ListView>("build-version-list");
            m_StatusLabel = rootVisualElement.Q<Label>("status-label");
            m_BucketSummaryLabel = rootVisualElement.Q<Label>("bucket-summary-label");
            m_BuildSummaryLabel = rootVisualElement.Q<Label>("build-summary-label");
            m_ChannelEmptyState = rootVisualElement.Q<VisualElement>("channel-empty-state");
            m_ChannelDetail = rootVisualElement.Q<VisualElement>("channel-detail");
            m_ChannelNameField = rootVisualElement.Q<TextField>("channel-name-field");
            m_BuildTargetDropdown = rootVisualElement.Q<DropdownField>("build-target-dropdown");
            m_PlatformDropdown = rootVisualElement.Q<DropdownField>("platform-dropdown");
            m_SecretIdField = rootVisualElement.Q<TextField>("secret-id-field");
            m_SecretKeyField = rootVisualElement.Q<TextField>("secret-key-field");
            m_RegionDropdown = rootVisualElement.Q<DropdownField>("region-dropdown");
            m_BucketDropdown = rootVisualElement.Q<DropdownField>("bucket-dropdown");

            m_ChannelNameField.isDelayed = true;
            m_SecretIdField.isDelayed = true;
            m_SecretKeyField.isDelayed = true;
            m_SecretKeyField.isPasswordField = true;
        }

        private void BindToolbar()
        {
            rootVisualElement.Q<Button>("refresh-button").clicked += RefreshStorageByCurrentState;
            rootVisualElement.Q<Button>("save-button").clicked += Save;
            rootVisualElement.Q<Button>("add-channel-button").clicked += AddChannel;
            rootVisualElement.Q<Button>("remove-channel-button").clicked += RemoveSelectedChannel;
            BindChannelFields();
        }

        private void BindChannelFields()
        {
            m_ChannelNameField.RegisterValueChangedCallback(evt =>
            {
                var channel = GetSelectedChannel();
                if (channel == null)
                {
                    return;
                }

                if (channel.IsPublished)
                {
                    m_ChannelNameField.SetValueWithoutNotify(channel.ChannelName ?? string.Empty);
                    RefreshStatus("已上传过版本的 channel 名称不可修改");
                    return;
                }

                channel.ChannelName = evt.newValue;
                SaveSettingsImmediately();
                RefreshChannelList();
                RefreshBuildVersions();
            });

            m_BuildTargetDropdown.RegisterValueChangedCallback(evt =>
            {
                var channel = GetSelectedChannel();
                if (channel == null)
                {
                    return;
                }

                channel.BuildTarget = evt.newValue;
                SaveSettingsImmediately();
                RefreshChannelList();
                RefreshBuildVersions();
            });

            m_PlatformDropdown.RegisterValueChangedCallback(evt =>
            {
                var channel = GetSelectedChannel();
                if (channel == null)
                {
                    return;
                }

                channel.PlatformId = FindProviderIdByDisplayName(evt.newValue);
                channel.RegionId = string.Empty;
                channel.BucketName = string.Empty;
                m_Regions.Clear();
                m_Buckets.Clear();
                SaveSettingsImmediately();
                RefreshChannelList();
                RefreshChannelDetail();
                AutoRefreshStorageChoices();
            });

            m_SecretIdField.RegisterValueChangedCallback(evt =>
            {
                var channel = GetSelectedChannel();
                if (channel == null)
                {
                    return;
                }

                channel.SecretId = evt.newValue;
                SaveSettingsImmediately();
                AutoRefreshStorageChoices();
            });

            m_SecretKeyField.RegisterValueChangedCallback(evt =>
            {
                var channel = GetSelectedChannel();
                if (channel == null)
                {
                    return;
                }

                channel.SecretKey = evt.newValue;
                SaveSettingsImmediately();
                AutoRefreshStorageChoices();
            });

            m_RegionDropdown.RegisterValueChangedCallback(evt =>
            {
                var channel = GetSelectedChannel();
                if (channel == null)
                {
                    return;
                }

                channel.RegionId = FindRegionIdByDisplayName(evt.newValue);
                channel.BucketName = string.Empty;
                m_Buckets.Clear();
                SaveSettingsImmediately();
                RefreshChannelDetail();
                AutoRefreshStorageChoices();
            });

            m_BucketDropdown.RegisterValueChangedCallback(evt =>
            {
                var channel = GetSelectedChannel();
                if (channel == null)
                {
                    return;
                }

                channel.BucketName = FindBucketNameByDisplayName(evt.newValue);
                SaveSettingsImmediately();
                RefreshBucketSummary();
                RefreshBuildVersions();
            });
        }

        private void BindChannelList()
        {
            m_ChannelList.itemsSource = m_Settings.Channels;
            m_ChannelList.selectionType = SelectionType.Single;
            m_ChannelList.fixedItemHeight = 56;
            m_ChannelList.makeItem = MakeChannelRow;
            m_ChannelList.bindItem = BindChannelRow;
            m_ChannelList.selectionChanged += _ =>
            {
                var selectedIndex = m_ChannelList.selectedIndex;
                if (m_Settings.SelectedChannelIndex != selectedIndex)
                {
                    m_Settings.SelectedChannelIndex = selectedIndex;
                    m_Regions.Clear();
                    m_Buckets.Clear();
                    m_CurrentRemoteVersion = null;
                    SaveSettingsImmediately();
                    m_ChannelList.RefreshItems();
                }

                RefreshChannelDetail();
                AutoRefreshStorageChoices();
                RefreshBuildVersions();
            };
        }

        private void BindBuildVersionList()
        {
            m_BuildVersionList.itemsSource = m_BuildVersions;
            m_BuildVersionList.selectionType = SelectionType.None;
            m_BuildVersionList.fixedItemHeight = 94;
            m_BuildVersionList.makeItem = MakeBuildVersionRow;
            m_BuildVersionList.bindItem = BindBuildVersionRow;
        }

        private static VisualElement MakeChannelRow()
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            var top = new VisualElement();
            top.AddToClassList("list-row__top");
            var name = new Label { name = "name" };
            name.AddToClassList("list-row__name");
            var badge = new Label { name = "badge" };
            badge.AddToClassList("badge");
            top.Add(name);
            top.Add(badge);

            var meta = new Label { name = "meta" };
            meta.AddToClassList("list-row__meta");
            row.Add(top);
            row.Add(meta);
            return row;
        }

        private static VisualElement MakeBuildVersionRow()
        {
            var row = new VisualElement();
            row.AddToClassList("build-row");
            return row;
        }

        private void BindChannelRow(VisualElement element, int index)
        {
            var channel = m_Settings.Channels[index];
            var name = element.Q<Label>("name");
            var badge = element.Q<Label>("badge");
            var meta = element.Q<Label>("meta");

            element.EnableInClassList("list-row--selected", index == m_Settings.SelectedChannelIndex);
            name.text = string.IsNullOrWhiteSpace(channel.ChannelName) ? "(未命名)" : channel.ChannelName;
            badge.text = string.IsNullOrWhiteSpace(channel.BuildTarget) ? "Target" : channel.BuildTarget;
            meta.text = $"{ProviderDisplayName(channel.PlatformId)} · {EmptyAsDash(channel.RegionId)} · {EmptyAsDash(channel.BucketName)}";
        }

        private void BindBuildVersionRow(VisualElement element, int index)
        {
            var item = m_BuildVersions[index];
            element.Clear();
            element.EnableInClassList("build-row--uploaded", item.IsUploaded);
            element.EnableInClassList("build-row--current", item.IsCurrent);
            element.EnableInClassList("build-row--uploading", item.IsUploading);

            var info = new VisualElement();
            info.AddToClassList("build-row__info");
            var top = new VisualElement();
            top.AddToClassList("build-row__top");
            var version = new Label(item.Version);
            version.AddToClassList("build-row__version");
            var badge = new Label(item.IsUploading ? "上传中" : item.IsCurrent ? "已应用" : item.IsUploaded ? "已上传" : "未上传");
            badge.AddToClassList("badge");
            top.Add(version);
            top.Add(badge);
            var meta = new Label(item.IsUploading
                ? $"{item.UploadedCount}/{item.UploadItems.Count} files · {item.UploadStatus}"
                : $"{item.UploadItems.Count} files · {item.Size} bytes · {item.LocalPath}");
            meta.AddToClassList("build-row__meta");
            info.Add(top);
            info.Add(meta);

            var progress = new ProgressBar
            {
                title = item.UploadStatus,
                lowValue = 0f,
                highValue = 1f,
                value = item.UploadProgress
            };
            progress.AddToClassList("build-row__progress");
            progress.style.display = item.IsUploading ? DisplayStyle.Flex : DisplayStyle.None;
            info.Add(progress);

            var actions = new VisualElement();
            actions.AddToClassList("build-row__actions");
            var uploadOrDelete = new Button(() =>
            {
                if (item.IsUploaded)
                {
                    DeleteRemoteVersion(item);
                }
                else
                {
                    UploadVersion(item);
                }
            })
            {
                text = item.IsUploaded ? "删除" : "上传"
            };
            uploadOrDelete.AddToClassList("small-button");
            uploadOrDelete.AddToClassList(item.IsUploaded ? "small-button--danger" : "small-button--primary");
            uploadOrDelete.SetEnabled(item.IsUploading is false);

            var current = new Button(() => SetCurrentVersion(item)) { text = item.IsCurrent ? "已应用" : "设置版本" };
            current.AddToClassList("small-button");
            current.EnableInClassList("small-button--applied", item.IsCurrent);
            current.SetEnabled(item.IsUploaded && item.IsCurrent is false && item.IsUploading is false);
            actions.Add(uploadOrDelete);
            actions.Add(current);
            element.Add(info);
            element.Add(actions);
        }

        private void RefreshAll()
        {
            RefreshProviders();
            m_Settings.EnsureDefaults();
            RefreshChannelList();
            RefreshChannelDetail();
            AutoRefreshStorageChoices();
            RefreshBuildVersions();
            RefreshStatus("已刷新 Publisher");
        }

        private void RefreshProviders()
        {
            m_Providers = ObjectStorageProviderRegistry.Providers.ToList();
            if (m_Providers.Count == 0)
            {
                m_Providers.Add(new UnavailableObjectStorageProvider("unavailable", "Unavailable"));
            }

            m_PlatformDropdown.choices = m_Providers.Select(x => x.DisplayName).ToList();
            m_BuildTargetDropdown.choices = BuildTargetChoices();
        }

        private void RefreshChannelList()
        {
            m_ChannelList.itemsSource = m_Settings.Channels;
            m_ChannelList.RefreshItems();
            if (m_Settings.SelectedChannelIndex >= 0 && m_Settings.SelectedChannelIndex < m_Settings.Channels.Count)
            {
                m_ChannelList.SetSelectionWithoutNotify(new[] { m_Settings.SelectedChannelIndex });
            }
            else
            {
                m_ChannelList.ClearSelection();
            }
        }

        private void RefreshChannelDetail()
        {
            var channel = GetSelectedChannel();
            var hasChannel = channel != null;
            m_ChannelEmptyState.style.display = hasChannel ? DisplayStyle.None : DisplayStyle.Flex;
            m_ChannelDetail.style.display = hasChannel ? DisplayStyle.Flex : DisplayStyle.None;
            if (channel == null)
            {
                m_ChannelNameField.SetEnabled(false);
                RefreshBucketSummary();
                return;
            }

            EnsureChannelBuildTarget(channel);
            m_ChannelNameField.SetValueWithoutNotify(channel.ChannelName ?? string.Empty);
            m_ChannelNameField.SetEnabled(channel.IsPublished is false);
            m_ChannelNameField.tooltip = channel.IsPublished ? "已上传过版本的 channel 名称不可修改" : string.Empty;
            m_BuildTargetDropdown.SetValueWithoutNotify(channel.BuildTarget);
            m_PlatformDropdown.SetValueWithoutNotify(ProviderDisplayName(channel.PlatformId));
            m_SecretIdField.SetValueWithoutNotify(channel.SecretId ?? string.Empty);
            m_SecretKeyField.SetValueWithoutNotify(channel.SecretKey ?? string.Empty);
            RefreshRegionChoices(channel);
            RefreshBucketChoices(channel);
            RefreshBucketSummary();
        }

        private void RefreshRegionChoices(PublisherChannel channel)
        {
            var choices = m_Regions.Select(RegionDisplayName).ToList();
            if (choices.Count == 0 && string.IsNullOrWhiteSpace(channel.RegionId) is false)
            {
                choices.Add(channel.RegionId);
            }

            if (choices.Count == 0)
            {
                choices.Add("未刷新");
            }

            m_RegionDropdown.choices = choices;
            var selected = string.IsNullOrWhiteSpace(channel.RegionId)
                ? choices[0]
                : RegionDisplayName(FindRegionById(channel.RegionId));
            if (choices.Contains(selected) is false)
            {
                selected = choices[0];
            }

            m_RegionDropdown.SetValueWithoutNotify(selected);
        }

        private void RefreshBucketChoices(PublisherChannel channel)
        {
            var choices = m_Buckets.Select(BucketDisplayName).ToList();
            if (choices.Count == 0 && string.IsNullOrWhiteSpace(channel.BucketName) is false)
            {
                choices.Add(channel.BucketName);
            }

            if (choices.Count == 0)
            {
                choices.Add("未刷新");
            }

            m_BucketDropdown.choices = choices;
            var selected = string.IsNullOrWhiteSpace(channel.BucketName)
                ? choices[0]
                : BucketDisplayName(FindBucketByName(channel.BucketName));
            if (choices.Contains(selected) is false)
            {
                selected = choices[0];
            }

            m_BucketDropdown.SetValueWithoutNotify(selected);
        }

        private void RefreshStorageByCurrentState()
        {
            var channel = GetSelectedChannel();
            if (channel == null)
            {
                RefreshStatus("请先选择 channel");
                return;
            }

            if (IsStorageReady(channel) is false)
            {
                RefreshBuildVersions();
                RefreshStatus("SecretId、SecretKey 和平台完整后才能刷新远端");
                return;
            }

            if (m_Regions.Count == 0 || string.IsNullOrWhiteSpace(channel.RegionId))
            {
                RefreshRegionsFromProvider();
            }
            else
            {
                RefreshBucketsFromProvider();
            }

            RefreshBuildVersions();
        }

        private void AutoRefreshStorageChoices()
        {
            var channel = GetSelectedChannel();
            if (channel == null || IsStorageReady(channel) is false)
            {
                RefreshBuildVersions();
                return;
            }

            if (m_Regions.Count == 0 || string.IsNullOrWhiteSpace(channel.RegionId))
            {
                RefreshRegionsFromProvider();
            }

            if (string.IsNullOrWhiteSpace(channel.RegionId) is false)
            {
                RefreshBucketsFromProvider();
            }

            RefreshBuildVersions();
        }

        private void RefreshRegionsFromProvider()
        {
            var channel = GetSelectedChannel();
            var provider = GetProvider(channel);
            if (channel == null || provider == null)
            {
                return;
            }

            try
            {
                m_Regions.Clear();
                m_Regions.AddRange(provider.ListRegions(channel.ToCredential()) ?? Array.Empty<StorageRegionInfo>());
                if (string.IsNullOrWhiteSpace(channel.RegionId) || FindRegionById(channel.RegionId) == null)
                {
                    channel.RegionId = m_Regions.FirstOrDefault()?.RegionId ?? string.Empty;
                    channel.BucketName = string.Empty;
                    m_Buckets.Clear();
                    SaveSettingsImmediately();
                }

                RefreshChannelDetail();
                RefreshStatus(m_Regions.Count == 0 ? $"{provider.DisplayName} 没有返回区域" : $"已刷新区域 · {m_Regions.Count}");
            }
            catch (Exception exception)
            {
                RefreshStatus(SanitizeMessage($"刷新区域失败：{exception.Message}"));
            }
        }

        private void RefreshBucketsFromProvider()
        {
            var channel = GetSelectedChannel();
            var provider = GetProvider(channel);
            if (channel == null || provider == null || string.IsNullOrWhiteSpace(channel.RegionId))
            {
                return;
            }

            try
            {
                m_Buckets.Clear();
                m_Buckets.AddRange(provider.ListBuckets(channel.ToCredential(), channel.RegionId) ?? Array.Empty<StorageBucketInfo>());
                if (string.IsNullOrWhiteSpace(channel.BucketName) || FindBucketByName(channel.BucketName) == null)
                {
                    channel.BucketName = m_Buckets.FirstOrDefault()?.BucketName ?? string.Empty;
                    SaveSettingsImmediately();
                }

                RefreshChannelDetail();
                RefreshStatus(m_Buckets.Count == 0 ? $"{provider.DisplayName} 没有返回 Bucket" : $"已刷新 Bucket · {m_Buckets.Count}");
            }
            catch (Exception exception)
            {
                RefreshStatus(SanitizeMessage($"刷新 Bucket 失败：{exception.Message}"));
            }
        }

        private void RefreshBuildVersions()
        {
            m_BuildVersions.Clear();
            var channel = GetSelectedChannel();
            if (channel == null)
            {
                RefreshBuildVersionList();
                RefreshBuildSummary();
                return;
            }

            EnsureChannelBuildTarget(channel);
            var remoteKeys = LoadRemoteKeys(channel);
            m_CurrentRemoteVersion = ReadCurrentVersion(channel);
            var platformRoot = ResolvePlatformBuildRoot(channel);
            if (Directory.Exists(platformRoot))
            {
                foreach (var versionRoot in Directory.EnumerateDirectories(platformRoot).OrderByDescending(x => x, StringComparer.Ordinal))
                {
                    var manifestPath = Path.Combine(versionRoot, ResourceSettings.MANIFEST_NAME);
                    if (System.IO.File.Exists(manifestPath) is false)
                    {
                        continue;
                    }

                    try
                    {
                        var plan = ResourceUploadPlanBuilder.Build(versionRoot, channel);
                        var missingCount = remoteKeys == null
                            ? plan.Items.Count
                            : plan.Items.Count(item => remoteKeys.Contains(item.RemoteKey) is false);
                        m_BuildVersions.Add(new BuildVersionItem
                        {
                            Version = plan.Version,
                            LocalPath = versionRoot.Replace('\\', '/'),
                            UploadItems = plan.Items.ToList(),
                            Size = plan.Items.Sum(x => x.Size),
                            MissingCount = missingCount,
                            IsUploaded = remoteKeys != null && plan.Items.Count > 0 && missingCount == 0,
                            IsCurrent = string.Equals(m_CurrentRemoteVersion, plan.Version, StringComparison.Ordinal)
                        });
                    }
                    catch (Exception exception)
                    {
                        RefreshStatus(SanitizeMessage($"读取构建版本失败：{exception.Message}"));
                    }
                }
            }

            RefreshBuildVersionList();
            RefreshBuildSummary();
        }

        private HashSet<string> LoadRemoteKeys(PublisherChannel channel)
        {
            if (CanQueryRemote(channel) is false)
            {
                return null;
            }

            try
            {
                var provider = GetProvider(channel);
                var prefix = ResourceUploadPlanBuilder.BuildRoot(channel);
                return new HashSet<string>(
                    provider.ListObjects(channel.ToCredential(), channel, prefix)
                        .Where(x => x != null && string.IsNullOrWhiteSpace(x.Key) is false)
                        .Select(x => x.Key),
                    StringComparer.Ordinal);
            }
            catch (Exception exception)
            {
                RefreshStatus(SanitizeMessage($"读取远端对象失败：{exception.Message}"));
                return null;
            }
        }

        private string ReadCurrentVersion(PublisherChannel channel)
        {
            if (CanQueryRemote(channel) is false)
            {
                return null;
            }

            try
            {
                var provider = GetProvider(channel);
                var text = provider.DownloadText(channel.ToCredential(), channel, ResourceUploadPlanBuilder.IndexKey(channel));
                var pointer = string.IsNullOrWhiteSpace(text) ? null : JsonConvert.DeserializeObject<ResourcePublishPointer>(text);
                return pointer?.version;
            }
            catch (Exception exception)
            {
                RefreshStatus(SanitizeMessage($"读取 publish.json 失败：{exception.Message}"));
                return null;
            }
        }

        private void RefreshBuildVersionList()
        {
            m_BuildVersionList.itemsSource = m_BuildVersions;
            m_BuildVersionList.RefreshItems();
        }

        private void RefreshBuildSummary()
        {
            var channel = GetSelectedChannel();
            if (channel == null)
            {
                m_BuildSummaryLabel.text = "选择 channel 后显示构建版本";
                return;
            }

            var uploaded = m_BuildVersions.Count(x => x.IsUploaded);
            m_BuildSummaryLabel.text = $"{ResolvePlatformBuildRoot(channel)} · 本地 {m_BuildVersions.Count} · 已上传 {uploaded} · Current {EmptyAsDash(m_CurrentRemoteVersion)}";
        }

        private void RefreshBucketSummary()
        {
            var channel = GetSelectedChannel();
            if (channel == null)
            {
                m_BucketSummaryLabel.text = "选择 channel 后配置对象存储";
                return;
            }

            m_BucketSummaryLabel.text = $"区域 {EmptyAsDash(channel.RegionId)} · Bucket {EmptyAsDash(channel.BucketName)} · 区域缓存 {m_Regions.Count} · Bucket 缓存 {m_Buckets.Count}";
        }

        private void UploadVersion(BuildVersionItem item)
        {
            var channel = GetSelectedChannel();
            var provider = GetProvider(channel);
            if (item == null || channel == null || provider == null || CanQueryRemote(channel) is false)
            {
                RefreshStatus("请先配置平台、密钥、区域和 Bucket");
                return;
            }

            var operationResult = ResourcePublishOperationResult.Success();
            item.IsUploading = true;
            item.UploadedCount = 0;
            item.UploadProgress = 0f;
            item.UploadStatus = "准备上传";
            RefreshBuildVersionList();

            try
            {
                for (var i = 0; i < item.UploadItems.Count; i++)
                {
                    var uploadItem = item.UploadItems[i];
                    UpdateUploadProgress(item, i, item.UploadItems.Count, $"上传 {i + 1}/{item.UploadItems.Count} · {Path.GetFileName(uploadItem.LocalPath)}");

                    var result = provider.UploadObject(channel.ToCredential(), channel, uploadItem);
                    operationResult.Items.Add(new ResourcePublishOperationItem
                    {
                        Key = uploadItem.RemoteKey,
                        Succeeded = result.Succeeded,
                        Message = SanitizeMessage(result.Message)
                    });

                    item.UploadedCount = i + 1;
                    UpdateUploadProgress(item, i + 1, item.UploadItems.Count, $"完成 {i + 1}/{item.UploadItems.Count} · {Path.GetFileName(uploadItem.LocalPath)}");

                    if (result.Succeeded)
                    {
                        continue;
                    }

                    operationResult.Succeeded = false;
                    operationResult.Message = SanitizeMessage(result.Message);
                    break;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                item.IsUploading = false;
                item.UploadProgress = 0f;
                item.UploadStatus = string.Empty;
                RefreshBuildVersionList();
            }

            if (operationResult.Succeeded is false)
            {
                RefreshStatus(operationResult.Message);
                ResourcePublisherResultWindow.Open(operationResult);
                return;
            }

            channel.IsPublished = true;
            SaveSettingsImmediately();
            operationResult.Succeeded = true;
            operationResult.Message = $"上传完成 · {item.Version} · {item.UploadItems.Count} files";
            RefreshStatus(operationResult.Message);
            ResourcePublisherResultWindow.Open(operationResult);
            RefreshChannelDetail();
            RefreshChannelList();
            RefreshBuildVersions();
        }

        private void UpdateUploadProgress(BuildVersionItem item, int completedCount, int totalCount, string status)
        {
            item.UploadedCount = Mathf.Clamp(completedCount, 0, totalCount);
            item.UploadProgress = totalCount <= 0 ? 1f : Mathf.Clamp01((float)completedCount / totalCount);
            item.UploadStatus = status;
            EditorUtility.DisplayProgressBar("上传资源版本", status, item.UploadProgress);
            RefreshStatus(status);
            RefreshBuildVersionList();
            Repaint();
        }

        private void DeleteRemoteVersion(BuildVersionItem item)
        {
            var channel = GetSelectedChannel();
            var provider = GetProvider(channel);
            if (item == null || channel == null || provider == null || CanQueryRemote(channel) is false)
            {
                RefreshStatus("请先配置平台、密钥、区域和 Bucket");
                return;
            }

            var keys = item.UploadItems.Select(x => x.RemoteKey).ToList();
            var result = provider.DeleteObjects(channel.ToCredential(), channel, keys);
            result.Message = SanitizeMessage(result.Succeeded ? $"已删除远端版本 · {item.Version}" : result.Message);
            RefreshStatus(result.Message);
            ResourcePublisherResultWindow.Open(result);
            RefreshBuildVersions();
        }

        private void SetCurrentVersion(BuildVersionItem item)
        {
            var channel = GetSelectedChannel();
            var provider = GetProvider(channel);
            if (item == null || channel == null || provider == null || CanQueryRemote(channel) is false)
            {
                RefreshStatus("请先配置平台、密钥、区域和 Bucket");
                return;
            }

            var key = ResourceUploadPlanBuilder.IndexKey(channel);
            var json = JsonConvert.SerializeObject(new ResourcePublishPointer { version = item.Version }, Formatting.Indented);
            var result = provider.UploadText(channel.ToCredential(), channel, key, json);
            result.Message = SanitizeMessage(result.Succeeded ? $"Current 已指向 {item.Version}" : result.Message);
            result.Items.Add(new ResourcePublishOperationItem
            {
                Key = key,
                Succeeded = result.Succeeded,
                Message = result.Message
            });
            RefreshStatus(result.Message);
            ResourcePublisherResultWindow.Open(result);
            RefreshBuildVersions();
        }

        private void AddChannel()
        {
            var channel = new PublisherChannel
            {
                ChannelName = UniqueName("dev", m_Settings.Channels.Select(x => x?.ChannelName)),
                PlatformId = m_Providers.FirstOrDefault()?.PlatformId,
                BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString()
            };
            channel.EnsureDefaults();
            m_Settings.Channels.Add(channel);
            m_Settings.SelectedChannelIndex = m_Settings.Channels.Count - 1;
            SaveSettingsImmediately();
            RefreshChannelList();
            RefreshChannelDetail();
            RefreshBuildVersions();
        }

        private void RemoveSelectedChannel()
        {
            var channel = GetSelectedChannel();
            if (channel == null)
            {
                return;
            }

            m_Settings.Channels.Remove(channel);
            m_Settings.SelectedChannelIndex = Mathf.Min(m_Settings.SelectedChannelIndex, m_Settings.Channels.Count - 1);
            m_Regions.Clear();
            m_Buckets.Clear();
            m_CurrentRemoteVersion = null;
            SaveSettingsImmediately();
            RefreshChannelList();
            RefreshChannelDetail();
            RefreshBuildVersions();
        }

        private PublisherChannel GetSelectedChannel()
        {
            if (m_Settings.SelectedChannelIndex < 0 || m_Settings.SelectedChannelIndex >= m_Settings.Channels.Count)
            {
                return null;
            }

            return m_Settings.Channels[m_Settings.SelectedChannelIndex];
        }

        private IObjectStorageProvider GetProvider(PublisherChannel channel)
        {
            if (channel == null)
            {
                return null;
            }

            return m_Providers.FirstOrDefault(x => x.PlatformId == channel.PlatformId)
                   ?? ObjectStorageProviderRegistry.GetProviderOrFallback(channel.PlatformId);
        }

        private bool IsStorageReady(PublisherChannel channel)
        {
            return channel != null
                   && string.IsNullOrWhiteSpace(channel.PlatformId) is false
                   && string.IsNullOrWhiteSpace(channel.SecretId) is false
                   && string.IsNullOrWhiteSpace(channel.SecretKey) is false;
        }

        private bool CanQueryRemote(PublisherChannel channel)
        {
            return IsStorageReady(channel)
                   && string.IsNullOrWhiteSpace(channel.RegionId) is false
                   && string.IsNullOrWhiteSpace(channel.BucketName) is false;
        }

        private void EnsureChannelBuildTarget(PublisherChannel channel)
        {
            if (channel == null || string.IsNullOrWhiteSpace(channel.BuildTarget) is false)
            {
                return;
            }

            channel.BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            SaveSettingsImmediately();
            RefreshChannelList();
        }

        private string ResolvePlatformBuildRoot(PublisherChannel channel)
        {
            var outputRoot = ResourceBuildUtilities.ProjectRelativeOrAbsolutePath(ResourceBuildSettings.OUTPUT_ROOT);
            return Path.Combine(
                    outputRoot,
                    ResourceBuildUtilities.SanitizeSegment(channel.ChannelName, "dev"),
                    ResourceBuildUtilities.SanitizeSegment(channel.BuildTarget, "platform"))
                .Replace('\\', '/');
        }

        private void Save()
        {
            SaveSettingsImmediately();
            RefreshStatus("Publisher 配置已保存");
        }

        private void SaveSettingsImmediately()
        {
            m_Settings.SaveSettings();
        }

        private void RefreshStatus(string message)
        {
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = message;
            }
        }

        private string FindProviderIdByDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            return m_Providers.FirstOrDefault(x => x.DisplayName == displayName)?.PlatformId;
        }

        private string ProviderDisplayName(string platformId)
        {
            return m_Providers.FirstOrDefault(x => x.PlatformId == platformId)?.DisplayName
                   ?? ObjectStorageProviderRegistry.GetProviderOrFallback(platformId).DisplayName;
        }

        private StorageRegionInfo FindRegionById(string regionId)
        {
            return m_Regions.FirstOrDefault(x => x != null && x.RegionId == regionId);
        }

        private string FindRegionIdByDisplayName(string displayName)
        {
            if (displayName == "未刷新")
            {
                return string.Empty;
            }

            var region = m_Regions.FirstOrDefault(x => RegionDisplayName(x) == displayName);
            return region?.RegionId ?? displayName;
        }

        private static string RegionDisplayName(StorageRegionInfo region)
        {
            if (region == null)
            {
                return "未刷新";
            }

            return string.IsNullOrWhiteSpace(region.DisplayName) ? region.RegionId : region.DisplayName;
        }

        private StorageBucketInfo FindBucketByName(string bucketName)
        {
            return m_Buckets.FirstOrDefault(x => x != null && x.BucketName == bucketName);
        }

        private string FindBucketNameByDisplayName(string displayName)
        {
            if (displayName == "未刷新")
            {
                return string.Empty;
            }

            var bucket = m_Buckets.FirstOrDefault(x => BucketDisplayName(x) == displayName);
            return bucket?.BucketName ?? displayName;
        }

        private static string BucketDisplayName(StorageBucketInfo bucket)
        {
            if (bucket == null)
            {
                return "未刷新";
            }

            return string.IsNullOrWhiteSpace(bucket.RegionId) ? bucket.BucketName : $"{bucket.BucketName} · {bucket.RegionId}";
        }

        private string SanitizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            foreach (var channel in m_Settings.Channels)
            {
                if (string.IsNullOrWhiteSpace(channel?.SecretId) is false)
                {
                    message = message.Replace(channel.SecretId, "***");
                }

                if (string.IsNullOrWhiteSpace(channel?.SecretKey) is false)
                {
                    message = message.Replace(channel.SecretKey, "***");
                }
            }

            return message;
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static List<string> BuildTargetChoices()
        {
            return Enum.GetNames(typeof(BuildTarget))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }

        private static string UniqueName(string baseName, IEnumerable<string> names)
        {
            var used = new HashSet<string>(names.Where(x => string.IsNullOrWhiteSpace(x) is false), StringComparer.Ordinal);
            if (used.Contains(baseName) is false)
            {
                return baseName;
            }

            var index = 2;
            while (used.Contains($"{baseName} {index}"))
            {
                index++;
            }

            return $"{baseName} {index}";
        }

        private sealed class BuildVersionItem
        {
            public string Version;
            public string LocalPath;
            public List<StorageUploadItem> UploadItems = new List<StorageUploadItem>();
            public long Size;
            public int MissingCount;
            public bool IsUploaded;
            public bool IsCurrent;
            public bool IsUploading;
            public int UploadedCount;
            public float UploadProgress;
            public string UploadStatus;
        }
    }
}
