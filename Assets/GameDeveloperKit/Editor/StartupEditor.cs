// using GameDeveloperKit.Runtime;
// using UnityEditor;
// using UnityEngine.UIElements;

// namespace GameDeveloperKit.Editor
// {
//     [CustomEditor(typeof(Startup), true)]
//     public sealed class StartupEditor : UnityEditor.Editor
//     {
//         private HelpBox _summaryBox;

//         public override VisualElement CreateInspectorGUI()
//         {
//             var root = new VisualElement();
//             root.style.paddingLeft = 4f;
//             root.style.paddingRight = 4f;
//             root.style.paddingTop = 4f;
//             root.style.paddingBottom = 4f;

//             _summaryBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
//             _summaryBox.style.marginBottom = 6f;
//             root.Add(_summaryBox);

//             root.Add(new HelpBox("Startup only reads GameFrameworkConfiguration (ResourcePlayMode / DefaultResourcePackageName / GatewayServerUrl) and boots framework in Awake.", HelpBoxMessageType.None));

//             var buttonRow = new VisualElement();
//             buttonRow.style.flexDirection = FlexDirection.Row;
//             buttonRow.style.flexWrap = Wrap.Wrap;
//             buttonRow.style.marginTop = 6f;
//             buttonRow.style.marginBottom = 6f;
//             buttonRow.Add(CreateButton("Create Config", CreateConfigurationAsset));
//             buttonRow.Add(CreateButton("Open Resource Settings", OpenResourceSettingsWindow));
//             root.Add(buttonRow);

//             UpdateInspectorState();
//             return root;
//         }

//         private Button CreateButton(string text, System.Action action)
//         {
//             var button = new Button(action)
//             {
//                 text = text
//             };
//             button.style.marginRight = 6f;
//             button.style.marginBottom = 4f;
//             return button;
//         }

//         private void CreateConfigurationAsset()
//         {
//             var configuration = GameFrameworkConfigurationBridge.CreateConfigurationAsset();
//             if (configuration == null)
//             {
//                 return;
//             }

//             var result = GameFrameworkConfigurationBridge.SyncResourceSettings(configuration);
//             _summaryBox.text = result.Message;
//             _summaryBox.messageType = result.MessageType;
//         }

//         private void OpenResourceSettingsWindow()
//         {
//             ResourceCenterWindow.Open();
//         }

//         private void UpdateInspectorState()
//         {
//             var startup = target as Startup;
//             var hasConfiguration = startup != null && startup.Configuration != null;
//             _summaryBox.text = hasConfiguration
//                 ? "Startup is configured with GameFrameworkConfiguration."
//                 : "Startup has no GameFrameworkConfiguration assigned. A runtime default will be used.";
//             _summaryBox.messageType = HelpBoxMessageType.Info;
//         }
//     }
// }
