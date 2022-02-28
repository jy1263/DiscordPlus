using Assets.Scripts.PeroTools.Commons;
using Assets.Scripts.PeroTools.Managers;
using Assets.Scripts.PeroTools.Nice.Datas;
using Assets.Scripts.PeroTools.Nice.Interface;
using Discord;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Il2CppSystem;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using MulticastDelegate = Il2CppSystem.MulticastDelegate;

namespace DiscordPlus
{
    public static class DiscordPlus
    {
		public static List<long> ClientIds = new List<long>()
		{
			820344593357996092L, // 0 - 40
			896044493839679498L, // 41 - 
		};
		public static int ClientSelected = 0;
		public static bool ChartDataLoaded = false;
		public static JArray ChartData = null;
        public static void DoPatching()
        {
            var harmony = new HarmonyLib.Harmony("com.github.mo10.discordplus");

            var discord = AccessTools.Constructor(typeof(Discord.Discord),new System.Type[] { typeof(long), typeof(ulong) });
            var discordPrefix = AccessTools.Method(typeof(DiscordPlus), "DiscordPrefix");
            harmony.Patch(discord, new HarmonyMethod(discordPrefix));

            var setUpdateActivity = AccessTools.Method(typeof(DiscordManager), "SetUpdateActivity", new System.Type[] { typeof(bool), typeof(string) });
            var setUpdateActivityPrefix = AccessTools.Method(typeof(DiscordPlus), "SetUpdateActivityPrefix");
            harmony.Patch(setUpdateActivity, new HarmonyMethod(setUpdateActivityPrefix));
        }

		public static void DiscordPrefix(ref long clientId, ref ulong flags)
		{
			clientId = ClientIds[ClientSelected];
			if (!ChartDataLoaded)
			{
				ChartDataLoaded = true;
				WebUtils.SendToUrl(
					url: "https://mdmc.moe/api/data/charts",
					method: "GET",
					failTime: 10,
					callback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action<UnityEngine.Networking.DownloadHandler>>(
						new System.Action<UnityEngine.Networking.DownloadHandler>
						(delegate(UnityEngine.Networking.DownloadHandler handler)
						{
							ChartData = JArray.Parse(handler.text);
						})
					),
					faillCallback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action<string>>(
						new System.Action<string>
						(delegate(string handler)
						{
							// ModLogger.Debug($"Send request failed: {reason}");
							ChartDataLoaded = false;
						})
					)
				);
			}
		}
		
		public static bool SetUpdateActivityPrefix(ref bool isPlaying,ref string levelInfo, ref DiscordManager __instance)
		{
			if (__instance.m_ActivityManager == null || __instance.m_ApplicationManager == null)
			{
				return false;
			}

			string musicUid = VariableUtils.GetResult<string>(Singleton<DataManager>.instance["Account"]["SelectedMusicUid"]);
			string musicPackage = VariableUtils.GetResult<string>(Singleton<DataManager>.instance["Account"]["SelectedAlbumUid"]);
			string musicLevel = VariableUtils.GetResult<string>(Singleton<DataManager>.instance["Account"]["SelectedMusicLevel"]);
			int diffculty = VariableUtils.GetResult<int>(Singleton<DataManager>.instance["Account"]["SelectedDifficulty"]);

			string diffcultyStr = string.Empty;
            switch (diffculty)
            {
				case 1:
					diffcultyStr = Singleton<ConfigManager>.instance.GetConfigStringValue("tip", 0, "diffcultyEasy");
					break;
				case 2:
					diffcultyStr = Singleton<ConfigManager>.instance.GetConfigStringValue("tip", 0, "diffcultyHard");
					break;
				case 3:
					diffcultyStr = Singleton<ConfigManager>.instance.GetConfigStringValue("tip", 0, "diffcultyMaster");
					break;
				default:
					diffcultyStr = "???";
					break;
			}

			string coverName = "random_song_cover";
			Activity activity;
			if (isPlaying)
			{
				// Switch to another discord client id, when music_package id > 40.
				int clientIdx = 0;
				if (int.Parse(musicUid.Split('-')[0]) >= 41)
					clientIdx = 1;
				if (clientIdx != ClientSelected)
				{
					// Need re-init discord sdk
					// ModLogger.Debug($"Switch to {clientIdx}");
					ClientSelected = clientIdx;
					ReInitDiscord(__instance);
				}

				// 33-12 39-8 cover is not exist
				if (musicPackage != "music_package_999"
					&& musicUid != "33-12"
					&& musicUid != "39-8")
				{
					coverName = musicUid;
				}
				else if (musicPackage == "music_package_999")
                {
					string songName = VariableUtils.GetResult<string>(Singleton<DataManager>.instance["Account"]["SelectedMusicName"]);
					JToken chart = ChartData.SelectToken($"$.[?(@.name=='{songName}')]");

					if (chart != null) coverName = $"mdmc_{chart["id"]}";
				}

				activity = new Activity
				{
					State = levelInfo,
					Details = $"{diffcultyStr} - Lvl.{musicLevel}",
					Assets = new ActivityAssets()
					{
						LargeImage = coverName,
						LargeText = levelInfo,
						SmallImage = "image_logo",
						SmallText = "Muse Dash",
					}
				};
			}
			else
			{
				activity = new Activity
				{
					State = "In Menu",
					Assets = new ActivityAssets()
					{
						LargeImage = "image_logo",
					}
				};
			}
			
			var thing = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Result>>
				(new System.Action<Result>
				(delegate(Result result)
				{
					if (result != Result.Ok)
					{
						// ModLogger.Debug("Discord Update Activity Failed!");
					}
				})
			);
			
			// Update activity
			__instance.m_ActivityManager.UpdateActivity(activity, new ActivityManager.UpdateActivityHandler(thing.Pointer));
			return false;
		}

		public static void ReInitDiscord(DiscordManager instance)
		{
			// Dispose old instant
			instance.m_ApplicationManager = null;
			instance.m_ActivityManager = null;
			instance.m_Discord.Dispose();
			// Init Discord SDK
			instance.m_Discord = new Discord.Discord(ClientIds[ClientSelected], 1UL);
			if (instance.m_Discord.isInit == Result.Ok)
			{
				instance.m_ActivityManager = instance.m_Discord.GetActivityManager();
				instance.m_ApplicationManager = instance.m_Discord.GetApplicationManager();
			}
		}
	}
}
