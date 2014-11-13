﻿/// Copyright (C) 2012-2014 Soomla Inc.
///
/// Licensed under the Apache License, Version 2.0 (the "License");
/// you may not use this file except in compliance with the License.
/// You may obtain a copy of the License at
///
///      http://www.apache.org/licenses/LICENSE-2.0
///
/// Unless required by applicable law or agreed to in writing, software
/// distributed under the License is distributed on an "AS IS" BASIS,
/// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
/// See the License for the specific language governing permissions and
/// limitations under the License.

using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Soomla.Profile
{
	
	#if UNITY_EDITOR
	[InitializeOnLoad]
	#endif
	/// <summary>
	/// This class holds the store's configurations. 
	/// </summary>
	public class ProfileSettings : ISoomlaSettings
	{
		
		#if UNITY_EDITOR
		
		static ProfileSettings instance = new ProfileSettings();
		static ProfileSettings()
		{
			SoomlaEditorScript.addSettings(instance);
		}

		BuildTargetGroup[] supportedPlatforms = { BuildTargetGroup.Android, BuildTargetGroup.iPhone, 
			BuildTargetGroup.WebPlayer, BuildTargetGroup.Standalone};
		
//		bool showAndroidSettings = (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android);
//		bool showIOSSettings = (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iPhone);

		Dictionary<string, bool?> socialIntegrationState = new Dictionary<string, bool?>();
		Dictionary<string, Dictionary<string, string>> socialLibPaths = new Dictionary<string, Dictionary<string, string>>();
		
//		GUIContent fbAppId = new GUIContent("FB app Id:");
//		GUIContent fbAppNS = new GUIContent("FB app namespace:");

		GUIContent gpClientId = new GUIContent ("Client ID [?]", "Client id of your google+ app (iOS only)");

		GUIContent twCustKey = new GUIContent ("Consumer Key [?]", "Consumer key of your twitter app");
		GUIContent twCustSecret = new GUIContent ("Consumer Secret [?]", "Consumer secret of your twitter app");

		GUIContent profileVersion = new GUIContent("Profile Version [?]", "The SOOMLA Profile version. ");
		GUIContent profileBuildVersion = new GUIContent("Profile Build [?]", "The SOOMLA Profile build.");

		private ProfileSettings()
		{
			ApplyCurrentSupportedProviders(socialIntegrationState);

			Dictionary<string, string> twitterPaths = new Dictionary<string, string>();
			twitterPaths.Add("/ios/ios-profile-twitter/libSTTwitter.a", "/iOS/libSTTwitter.a");
			twitterPaths.Add("/ios/ios-profile-twitter/libSoomlaiOSProfileTwitter.a", "/iOS/libSoomlaiOSProfileTwitter.a");
			twitterPaths.Add("/android/android-profile-twitter/AndroidProfileTwitter.jar", "/Android/AndroidProfileTwitter.jar");
			twitterPaths.Add("/android/android-profile-twitter/twitter4j-asyc-4.0.2.jar", "/Android/twitter4j-asyc-4.0.2.jar");
			twitterPaths.Add("/android/android-profile-twitter/twitter4j-core-4.0.2.jar", "/Android/twitter4j-core-4.0.2.jar");
			socialLibPaths.Add(Provider.TWITTER.ToString(), twitterPaths);

			Dictionary<string, string> googlePaths = new Dictionary<string, string>();
			googlePaths.Add("/ios/ios-profile-google/libSoomlaiOSProfileGoogle.a", "/iOS/libSoomlaiOSProfileGoogle.a");
			googlePaths.Add("/ios/ios-profile-google/GoogleOpenSource.framework", "/iOS/GoogleOpenSource.framework");
			googlePaths.Add("/ios/ios-profile-google/GooglePlus.bundle", "/iOS/GooglePlus.bundle");
			googlePaths.Add("/ios/ios-profile-google/GooglePlus.framework", "/iOS/GooglePlus.framework");
			googlePaths.Add("/android/android-profile-google/AndroidProfileGoogle.jar", "/Android/AndroidProfileGoogle.jar");
			socialLibPaths.Add(Provider.GOOGLE.ToString(), googlePaths);

			ReadSocialIntegrationState(socialIntegrationState);
        }

		private void WriteSocialIntegrationState()
		{
			List<string> savedStates = new List<string>();
			foreach (var entry in socialIntegrationState) {
				if (entry.Value != null) {
					savedStates.Add(entry.Key + "," + (entry.Value.Value ? 1 : 0));
				}
			}

			string result = string.Empty;
			if (savedStates.Count > 0) {
				result = string.Join(";", savedStates.ToArray());
			}

			SoomlaEditorScript.Instance.setSettingsValue("SocialIntegration", result);
			SoomlaEditorScript.DirtyEditor();
		}

		public void OnEnable() {
			// Generating AndroidManifest.xml
			//			ManifestTools.GenerateManifest();
		}
		
		public void OnModuleGUI() {
			IntegrationGUI();
		}
		
		public void OnInfoGUI() {
			SoomlaEditorScript.SelectableLabelField(profileVersion, "1.0");
			SoomlaEditorScript.SelectableLabelField(profileBuildVersion, "1");
			EditorGUILayout.Space();
		}
		
		public void OnSoomlaGUI() {
		}

		void IntegrationGUI()
		{
			EditorGUILayout.LabelField("Social Platforms:", EditorStyles.boldLabel);

			ReadSocialIntegrationState(socialIntegrationState);

			EditorGUI.BeginChangeCheck();

			Dictionary<string, bool?>.KeyCollection keys = socialIntegrationState.Keys;
			for (int i = 0; i < keys.Count; i++) {
				string socialPlatform = keys.ElementAt(i);
				bool? socialPlatformState = socialIntegrationState[socialPlatform];

				EditorGUILayout.BeginHorizontal();

				bool update = false;
				bool doIntegrate = false;
				if (socialPlatformState != null) {
					bool result = EditorGUILayout.Toggle(socialPlatform, socialPlatformState.Value);
					if (result != socialPlatformState.Value) {
						socialIntegrationState[socialPlatform] = result;
						doIntegrate = result;
						update = true;
					}

					EditorGUILayout.EndHorizontal();
					DrawPlatformParams(socialPlatform);
					EditorGUILayout.BeginHorizontal();
				}
				else {
					doIntegrate = IsSocialPlatformDetected(socialPlatform);
					bool result = EditorGUILayout.Toggle(socialPlatform, doIntegrate);
					
					// User changed automatic value
					if (doIntegrate != result) {
						doIntegrate = result;
						socialIntegrationState[socialPlatform] = doIntegrate;
						update = true;
					}
				}

				if (update) {
					ApplyIntegrationState(socialPlatform, doIntegrate);
				}

				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.Space();

			if (EditorGUI.EndChangeCheck()) {
				WriteSocialIntegrationState();
			}
		}

		void ApplyIntegrationState (string socialPlatform, bool doIntegrate)
		{
			foreach (var buildTarget in supportedPlatforms) {
				TryAddRemoveSocialPlatformFlag(buildTarget, socialPlatform, !doIntegrate);
			}

			ApplyIntegretionLibraries(socialPlatform, !doIntegrate);
		}

		private static string compilationsRootPath = Application.dataPath + "/Soomla/compilations";
		private static string pluginsRootPath = Application.dataPath + "/Plugins";

		void ApplyIntegretionLibraries (string socialPlatform, bool remove)
		{
			try {
				Dictionary<string, string> paths = null;
				socialLibPaths.TryGetValue(socialPlatform, out paths);
				if (paths != null) {
					if (remove) {
						foreach (var pathEntry in paths) {
							FileUtil.DeleteFileOrDirectory(pluginsRootPath + pathEntry.Value);
							FileUtil.DeleteFileOrDirectory(pluginsRootPath + pathEntry.Value + ".meta");
						}
					} else {
						foreach (var pathEntry in paths) {
							FileUtil.CopyFileOrDirectory(compilationsRootPath + pathEntry.Key,
							                             pluginsRootPath + pathEntry.Value);
						}
					}
				}
			}catch {}
		}

		/** Profile Providers util functions **/

		private void TryAddRemoveSocialPlatformFlag(BuildTargetGroup buildTarget, string socialPlatform, bool remove) {
			string targetFlag = GetSocialPlatformFlag(socialPlatform);
			string scriptDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTarget);
			List<string> flags = new List<string>(scriptDefines.Split(';'));

			if (flags.Contains(targetFlag)) {
				if (remove) {
					flags.Remove(targetFlag);
				}
			}
			else {
				if (!remove) {
					flags.Add(targetFlag);
				}
			}

			string result = string.Join(";", flags.ToArray());
			if (scriptDefines != result) {
				PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTarget, result);
			}
		}

		private string GetSocialPlatformFlag(string socialPlatform) {
			return "SOOMLA_" + socialPlatform.ToUpper();
		}
		
		void DrawPlatformParams(string socialPlatform){
			switch(socialPlatform)
			{
			case "google":
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.Space();
				EditorGUILayout.LabelField(gpClientId,  GUILayout.Width(150), SoomlaEditorScript.FieldHeight);
				GPClientId = EditorGUILayout.TextField(GPClientId, SoomlaEditorScript.FieldHeight);
				EditorGUILayout.EndHorizontal();
				break;
			case "twitter":
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.Space();
				EditorGUILayout.LabelField(twCustKey, GUILayout.Width(150), SoomlaEditorScript.FieldHeight);
				TwitterConsumerKey = EditorGUILayout.TextField(TwitterConsumerKey, SoomlaEditorScript.FieldHeight);
				EditorGUILayout.EndHorizontal();
				
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.Space();
				EditorGUILayout.LabelField(twCustSecret,  GUILayout.Width(150), SoomlaEditorScript.FieldHeight);
				TwitterConsumerSecret = EditorGUILayout.TextField(TwitterConsumerSecret, SoomlaEditorScript.FieldHeight);
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.Space();
				break;
			default:
				break;
			}
		}
		
		#endif
		
		/** Profile Specific Variables **/

		public static Dictionary<string, bool?> IntegrationState
		{
			get {
				Dictionary<string, bool?> result = new Dictionary<string, bool?>();
				ApplyCurrentSupportedProviders(result);

				Dictionary<string, bool?>.KeyCollection keys = result.Keys;
				for (int i = 0; i < keys.Count; i++) {
					string key = keys.ElementAt(i);
					result[key] = IsSocialPlatformDetected(key);
				}

				ReadSocialIntegrationState(result);
				return result;
			}
		}

		private static void ApplyCurrentSupportedProviders(Dictionary<string, bool?> target) {
			target.Add(Provider.FACEBOOK.ToString(), null);
			target.Add(Provider.TWITTER.ToString(), null);
			target.Add(Provider.GOOGLE.ToString(), null);
		}

		private static bool IsSocialPlatformDetected(string platform)
		{
			if (Provider.fromString(platform) == Provider.FACEBOOK) {
				Type fbType = Type.GetType("FB");
				return (fbType != null);
			}
			
			return false;
		}

		private static void ReadSocialIntegrationState(Dictionary<string, bool?> toTarget)
		{
			string value = string.Empty;
			SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("SocialIntegration", out value);
			
			if (value != null) {
				string[] savedIntegrations = value.Split(';');
				foreach (var savedIntegration in savedIntegrations) {
					string[] platformValue = savedIntegration.Split(',');
					string platform = platformValue[0];
					int state = int.Parse(platformValue[1]);
					
					bool? platformState = null;
					if (toTarget.TryGetValue(platform, out platformState)) {
						toTarget[platform] = (state > 0);
					}
				}
			}
		}
		
		/** FACEBOOK **/

		public static string FB_APP_ID_DEFAULT = "YOUR FB APP ID";
		
		public static string FBAppId
		{
			get {
				string value;
				return SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("FBAppId", out value) ? value : FB_APP_ID_DEFAULT;
			}
			set 
			{
				string v;
				SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("FBAppId", out v);
				if (v != value)
				{
					SoomlaEditorScript.Instance.setSettingsValue("FBAppId",value);
					SoomlaEditorScript.DirtyEditor ();
				}
			}
		}

		public static string FB_APP_NS_DEFAULT = "YOUR FB APP ID";
		
		public static string FBAppNamespace
		{
			get {
				string value;
				return SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("FBAppNS", out value) ? value : FB_APP_NS_DEFAULT;
			}
			set 
			{
				string v;
				SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("FBAppNS", out v);
				if (v != value)
				{
					SoomlaEditorScript.Instance.setSettingsValue("FBAppNS",value);
					SoomlaEditorScript.DirtyEditor ();
				}
			}
		}

		/** GOOGLE+ **/

		public static string GP_CLIENT_ID_DEFAULT = "YOUR GOOGLE+ CLIENT ID";

		public static string GPClientId
		{
			get {
				string value;
				return SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("GPClientId", out value) ? value : GP_CLIENT_ID_DEFAULT;
			}
			set 
			{
				string v;
				SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("GPClientId", out v);
				if (v != value)
				{
					SoomlaEditorScript.Instance.setSettingsValue("GPClientId",value);
					SoomlaEditorScript.DirtyEditor ();
				}
			}
		}

		/** TWITTER **/

		public static string TWITTER_CONSUMER_KEY_DEFAULT = "YOUR TWITTER CONSUMER KEY";
		public static string TWITTER_CONSUMER_SECRET_DEFFAULT = "YOUR TWITTER CONSUMER SECRET";
		
		public static string TwitterConsumerKey
		{
			get {
				string value;
				return SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("TwitterConsumerKey", out value) ? value : TWITTER_CONSUMER_KEY_DEFAULT;
			}
			set 
			{
				string v;
				SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("TwitterConsumerKey", out v);
				if (v != value)
				{
					SoomlaEditorScript.Instance.setSettingsValue("TwitterConsumerKey",value);
					SoomlaEditorScript.DirtyEditor ();
				}
			}
		}

		public static string TwitterConsumerSecret
		{
			get {
				string value;
				return SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("TwitterConsumerSecret", out value) ? value : TWITTER_CONSUMER_SECRET_DEFFAULT;
			}
			set 
			{
				string v;
				SoomlaEditorScript.Instance.SoomlaSettings.TryGetValue("TwitterConsumerSecret", out v);
				if (v != value)
				{
					SoomlaEditorScript.Instance.setSettingsValue("TwitterConsumerSecret",value);
					SoomlaEditorScript.DirtyEditor ();
				}
			}
		}

	}
}