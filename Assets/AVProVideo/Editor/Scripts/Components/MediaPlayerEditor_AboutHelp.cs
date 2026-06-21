using UnityEngine;
using UnityEditor;

//-----------------------------------------------------------------------------
// Copyright 2015-2021 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo.Editor
{
	/// <summary>
	/// About/Help section of the editor for the MediaPlayer component
	/// </summary>
	public partial class MediaPlayerEditor : UnityEditor.Editor
	{
		public const string LinkPluginWebsite = "https://renderheads.com/products/avpro-video/";
		public const string LinkForumPage = "https://forum.unity.com/threads/released-avpro-video-complete-video-playback-solution.385611/";
		public const string LinkForumLastPage = "https://discussions.unity.com/t/released-avpro-video-complete-video-playback-solution/616470/5259";
		public const string LinkGithubIssues = "https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues";
		public const string LinkGithubIssuesNew = "https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/new/choose";
		//
		public const string LinkAssetStorePage_CoreDesktop = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpv_cd_u/[p_id:1100l104138]/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-video-v3-core-desktop-edition-278895";
		public const string LinkAssetStorePage_CoreMobile = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpv_m_u/[p_id:1100l104138]/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-video-v3-core-mobile-edition-278892";
		public const string LinkAssetStorePage_Core = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpv_c_u/[p_id:1100l104138]/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-video-v3-core-edition-278893";
		public const string LinkAssetStorePage_HarmonyOSNEXT = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_hos_ne_u/[p_id:1100l104138]/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-video-v3-harmonyos-next-edition-326663";
		public const string LinkAssetStorePage_Ultra = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpv_u_u/[p_id:1100l104138]/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-video-v3-ultra-edition-278896";
		//
		public const string LinkAssetStorePage_MC_Basic = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpmc_b_u/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-movie-capture-basic-edition-221916";
		public const string LinkAssetStorePage_MC_Mobile = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpmc_m_u/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-movie-capture-mobile-edition-221852";
		public const string LinkAssetStorePage_MC_Desktop = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpmc_d_u/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-movie-capture-desktop-edition-221914";
		public const string LinkAssetStorePage_MC_Ultra = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpmc_u_u/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-movie-capture-ultra-edition-221845";
		//
		public const string LinkAssetStorePage_DeckLink = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avpdl_u/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-decklink-68784";
		//
		public const string LinkAssetStorePage_LiveCamera = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_avplc_u/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Fvideo%2Favpro-live-camera-3683";
		//
		public const string LinkAssetStorePage_ExternalGameView = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_egv_u/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Futilities%2Fexternal-game-view-215946";
		//
		public const string LinkAssetStorePage_SessionRestore = "https://af.unity.com/sr/camref:1101lcNgx/pubref:rh_sr_u/destination:https%3A%2F%2Fassetstore.unity.com%2Fpackages%2Ftools%2Futilities%2Fsession-restore-94578";
		//
		public const string LinkUserManual = "https://www.renderheads.com/content/docs/AVProVideo-v3/articles/intro.html";
		public const string LinkScriptingClassReference = "https://www.renderheads.com/content/docs/AVProVideo-v3/api/RenderHeads.Media.AVProVideo.html";
		public const string LinkEditions = "https://www.renderheads.com/content/docs/AVProVideo-v3/articles/download.html";

		private struct Native
		{
#if UNITY_EDITOR_WIN
			[System.Runtime.InteropServices.DllImport("AVProVideo")]
			public static extern System.IntPtr GetPluginVersion();
#elif UNITY_EDITOR_OSX
			[System.Runtime.InteropServices.DllImport("AVProVideo")]
			public static extern System.IntPtr AVPPluginGetVersionStringPointer();
#endif
		}

		private static string GetPluginVersion()
		{
			string version = "Unknown";
			try
			{
#if UNITY_EDITOR_WIN
				version = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(Native.GetPluginVersion());
#elif UNITY_EDITOR_OSX
				version = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(Native.AVPPluginGetVersionStringPointer());
#endif
			}
			catch (System.DllNotFoundException e)
			{
#if UNITY_EDITOR_OSX
				Debug.LogError("[AVProVideo] Failed to load Bundle. " + e.Message);
#else
				Debug.LogError("[AVProVideo] Failed to load DLL. " + e.Message);
#endif
			}
			return version;
		}

		private static Texture2D GetIcon(Texture2D icon)
		{
			if (icon == null)
			{
				icon = Resources.Load<Texture2D>("AVProVideoIcon");
			}
			return icon;
		}

		private void OnInspectorGUI_About()
		{
			GUIStyle bigBold = new GUIStyle(EditorStyles.boldLabel);
			bigBold.fontSize = 14;
//			bigBold.alignment = TextAnchor.MiddleCenter;
//			EditorGUILayout.LabelField("Credits", bigBold);

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			_icon = GetIcon(_icon);
			if (_icon != null)
			{
				GUILayout.Label(new GUIContent(_icon));
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			GUI.color = Color.yellow;
			EditorHelper.IMGUI.CentreLabel("AVPro Video by RenderHeads Ltd", EditorStyles.boldLabel);
			EditorHelper.IMGUI.CentreLabel("version " + Helper.AVProVideoVersion + " (plugin v" + GetPluginVersion() + ")");
			GUI.color = Color.white;
			GUI.backgroundColor = Color.white;

			if (_icon != null)
			{
				GUILayout.Space(12.0f);
				ShowSupportWindowButton();
				GUILayout.Space(12.0f);
			}

//			EditorGUILayout.LabelField("Links", bigBold);
//			GUILayout.Space(8f);

			GUILayout.BeginHorizontal();
				GUILayout.Space(4.0f);
				EditorGUILayout.LabelField("Documentation", bigBold);
			GUILayout.EndHorizontal();
			GUILayout.Space(6.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(15.0f);
					GUILayout.BeginVertical();
						if( GUILayout.Button("User Manual, FAQ, Release Notes", GUILayout.ExpandWidth(false), GUILayout.Width(220.0f), GUILayout.Height(26.0f)) )
						{
							Application.OpenURL(LinkUserManual);
						}
						if( GUILayout.Button("Editions & Upgrades", GUILayout.ExpandWidth(false), GUILayout.Width(220.0f), GUILayout.Height(26.0f)) )
						{
							Application.OpenURL(LinkEditions);
						}
						if( GUILayout.Button("Scripting Class Reference", GUILayout.ExpandWidth(false), GUILayout.Width(220.0f), GUILayout.Height(26.0f)) )
						{
							Application.OpenURL(LinkScriptingClassReference);
						}
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();

			GUILayout.Space(20.0f);

			GUILayout.BeginHorizontal();
				GUILayout.Space(4.0f);
				GUILayout.Label("Bugs / Support / Community", bigBold);
			GUILayout.EndHorizontal();
			GUILayout.Space(6.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(15.0f);
					GUILayout.BeginVertical();
						GUILayout.BeginHorizontal();
							if(GUILayout.Button("Help & Support", GUILayout.ExpandWidth(false), GUILayout.Width(140.0f), GUILayout.Height(26.0f)) )
							{
								SupportWindow.Init();
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal();
							if ( GUILayout.Button("Github Issues", GUILayout.ExpandWidth(false), GUILayout.Width(140.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkGithubIssues);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal();
							if ( GUILayout.Button("Product Homepage", GUILayout.ExpandWidth(false), GUILayout.Width(140.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkPluginWebsite);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal();
							if ( GUILayout.Button("Discussions Thread", GUILayout.ExpandWidth(false), GUILayout.Width(140.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkForumPage);
							}
						GUILayout.EndHorizontal();
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();

			GUILayout.Space(20.0f);

			GUILayout.BeginHorizontal();
				GUILayout.Space(4.0f);
//				GUILayout.Label("Rate and Review (★★★★☆)", GUILayout.ExpandWidth(false, GUILayout.Width(230.0f)));
				GUILayout.Label("Asset Store - License Purchase", bigBold);
			GUILayout.EndHorizontal();
			GUILayout.Space(6.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(15.0f);
					GUILayout.BeginVertical();
						Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AVProVideo/Editor/Resources/AVProVideoIcon.png");

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Video v3 - Core Desktop Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_CoreDesktop);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Video v3 - Core Mobile Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_CoreMobile);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Video v3 - Core HarmonyOS NEXT Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_HarmonyOSNEXT);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Video v3 - Core Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_Core);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Video v3 - Ultra Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_Ultra);
							}
						GUILayout.EndHorizontal();
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();

			GUILayout.Space(20.0f);

			GUILayout.BeginHorizontal();
				GUILayout.Space(4.0f);
				EditorGUILayout.LabelField("Credits", bigBold);
			GUILayout.EndHorizontal();
			GUILayout.Space(6.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(15.0f);
					GUILayout.BeginVertical();
						GUILayout.Label("Development", EditorStyles.boldLabel);
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();
				GUILayout.Space(4.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(40.0f);
					GUILayout.BeginVertical();
						GUILayout.Label("Morris Butler", EditorStyles.label);
						GUILayout.Label("Richard Turnbull", EditorStyles.label);
						GUILayout.Label("Ste Butcher", EditorStyles.label);
						GUILayout.Label("Reuben Miller", EditorStyles.label);
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();

			GUILayout.Space(12.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(15.0f);
					GUILayout.BeginVertical();
						GUILayout.Label("QA/Support", EditorStyles.boldLabel);
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();
				GUILayout.Space(4.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(40.0f);
					GUILayout.BeginVertical();
						GUILayout.Label("Chris Clarkson", EditorStyles.label);
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();

			GUILayout.Space(12.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(15.0f);
					GUILayout.BeginVertical();
						GUILayout.Label("Special Thanks", EditorStyles.boldLabel);
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();
				GUILayout.Space(4.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(40.0f);
					GUILayout.BeginVertical();
						GUILayout.Label("Andrew Griffiths", EditorStyles.label);
						GUILayout.Label("Sunrise Wang", EditorStyles.label);
						GUILayout.Label("Luke Godward", EditorStyles.label);
						GUILayout.Label("Jeff Rusch", EditorStyles.label);
						GUILayout.Label("Shane Marks", EditorStyles.label);
						GUILayout.Label("Muano Mainganye", EditorStyles.label);
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();

			GUILayout.Space(20.0f);

			GUILayout.BeginHorizontal();
				GUILayout.Space(4.0f);
				GUILayout.Label("Also Available", bigBold);
			GUILayout.EndHorizontal();
			GUILayout.Space(6.0f);
				GUILayout.BeginHorizontal();
					GUILayout.Space(15.0f);
					GUILayout.BeginVertical();
						Texture2D mc_icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AVProVideo/Editor/Resources/AVProMovieCaptureIcon.png");
						Texture2D decklink_icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AVProVideo/Editor/Resources/AVProDeckLinkIcon.png");
						Texture2D livecamera_icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AVProVideo/Editor/Resources/AVProLiveCameraIcon.png");
						Texture2D externalgameview_icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AVProVideo/Editor/Resources/ExternalGameViewIcon.png");
						Texture2D sessionrestore_icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AVProVideo/Editor/Resources/SessionRestoreIcon.png");

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(mc_icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Movie Capture - Basic Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_MC_Basic);
							}
						GUILayout.EndHorizontal();
						//
						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(mc_icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Movie Capture - Mobile Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_MC_Mobile);
							}
						GUILayout.EndHorizontal();
						//
						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(mc_icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Movie Capture - Desktop Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_MC_Desktop);
							}
						GUILayout.EndHorizontal();
						//
						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(mc_icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Movie Capture - Ultra Edition", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_MC_Ultra);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(decklink_icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro DeckLink", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_DeckLink);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(livecamera_icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("AVPro Live Camera", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_LiveCamera);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(externalgameview_icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("External Game View", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_ExternalGameView);
							}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.Width(280.0f), GUILayout.Height(26.0f));
							GUILayout.Label(sessionrestore_icon, GUILayout.Width(26.0f), GUILayout.Height(26.0f));
							//
							if (GUILayout.Button("Session Restore", GUILayout.Width(280.0f), GUILayout.Height(26.0f)) )
							{
								Application.OpenURL(LinkAssetStorePage_SessionRestore);
							}
						GUILayout.EndHorizontal();
					GUILayout.EndVertical();
				GUILayout.EndHorizontal();

			GUILayout.Space(10.0f);
		}
	}
}