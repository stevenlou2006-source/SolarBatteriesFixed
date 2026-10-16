/*
 * Solar Batteries Fixed
 * 基于 Omniscye 的 Solar Charging (SolarBatteries) 修改
 * 原始项目: https://thunderstore.io/c/repo/p/Omniscye/Solar_Charging/
 * 
 * 本模组采用 GPLv3 许可证发布
 * 修改者: cn_xc (小菜)
 */

using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SolarBatteriesFixed
{
	[BepInPlugin("cn_xc.SolarBatteriesFixed", "Solar Batteries Fixed", "1.0.0")]
	[BepInProcess("REPO.exe")]
	public class SolarBatteriesFixedPlugin : BaseUnityPlugin
	{
		internal static ManualLogSource Log;

		public static ConfigEntry<bool> EnableMod;
		public static ConfigEntry<int> ChargeRate;
		public static ConfigEntry<float> MaxRange;

		private readonly Harmony harmony = new Harmony("cn_xc.SolarBatteriesFixed");

		private void Awake()
		{
			Log = Logger;

			EnableMod = Config.Bind("General", "EnableMod", true, "是否启用手电筒太阳能充电功能");
			ChargeRate = Config.Bind("General", "ChargeRate", 5, new ConfigDescription(
				"电池每秒充电百分比（整数，1-10）",
				new AcceptableValueRange<int>(1, 10)));
			MaxRange = Config.Bind("General", "MaxRange", 3f, new ConfigDescription(
				"手电筒照射电池的最大有效距离",
				new AcceptableValueRange<float>(0.5f, 10f)));

			try
			{
				var original = AccessTools.Method(typeof(ItemBattery), "Update");
				if (original != null)
				{
					harmony.Patch(original,
						prefix: new HarmonyMethod(typeof(SolarBatteriesFixedPlugin), nameof(UpdatePrefix)));
					Log.LogInfo("✅ Prefix 补丁成功: ItemBattery.Update");
				}
				else
				{
					Log.LogError("❌ 找不到 ItemBattery.Update 方法");
				}
			}
			catch (Exception ex)
			{
				Log.LogError($"补丁失败: {ex.Message}");
			}

			Log.LogInfo($"Solar Batteries Fixed v1.0.0 加载完成！");
		}

		[HarmonyPrefix]
		public static bool UpdatePrefix(ItemBattery __instance)
		{
			if (!EnableMod.Value)
				return true;

			if (!SemiFunc.IsMasterClientOrSingleplayer())
				return true;

			if (__instance.batteryLife >= 100f)
				return true;

			var players = SemiFunc.PlayerGetList();
			if (players == null || players.Count == 0)
				return true;

			foreach (PlayerAvatar player in players)
			{
				if (player == null)
					continue;

				var flashlight = player.flashlightController;
				if (flashlight == null || !flashlight.LightActive)
					continue;

				Light spotlight = flashlight.spotlight;
				if (spotlight == null)
					continue;

				if (IsBatteryLit(__instance, spotlight, MaxRange.Value))
				{
					__instance.ChargeBattery(player.gameObject, ChargeRate.Value);
					break;
				}
			}

			return true;
		}

		private static bool IsBatteryLit(ItemBattery battery, Light spot, float maxRange)
		{
			Vector3 batteryPos = (battery.batteryTransform != null)
				? battery.batteryTransform.position
				: battery.transform.position;

			Vector3 lightPos = spot.transform.position;
			Vector3 direction = batteryPos - lightPos;
			float distance = direction.magnitude;

			if (distance > maxRange)
				return false;

			float angle = Vector3.Angle(spot.transform.forward, direction);
			if (angle > spot.spotAngle * 0.5f)
				return false;

			RaycastHit hit;
			if (Physics.Raycast(lightPos, direction.normalized, out hit, distance,
					SemiFunc.LayerMaskGetVisionObstruct()))
			{
				if (hit.transform != battery.transform
					&& !hit.transform.IsChildOf(battery.transform))
					return false;
			}

			return true;
		}
	}
}