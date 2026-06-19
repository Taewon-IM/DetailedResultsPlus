using HarmonyLib;
using UnityEngine;
using System;
using UnityModManagerNet;
using System.Reflection;
using System.Collections.Generic;

namespace DetailedResultsPlus
{
    public static class Localization
    {
        private static Dictionary<string, (string en, string ko, string zh_cn)> _texts = new Dictionary<string, (string, string, string)>
        {
            { "speed", ("Speed", "배속", "倍速") },
            { "avgOffset", ("Avg Offset", "평균 오프셋", "平均偏移") },
            { "completed", ("% Complete", "% 완료", "% 完成度") }
        };

        public static string Get(string key)
        {
            string lang = RDString.language.ToString().ToLower();
            bool isKorean = (lang == "korean");
            bool isChinese = (lang == "chinese");
            bool isChineseSimplified = (lang == "chinesesimplified");

            if (_texts.TryGetValue(key, out var text))
            {
                return isChinese || isChineseSimplified ? text.zh_cn : isKorean ? text.ko : text.en;
            }

            return "Key Error";
        }
    }
    public static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static Harmony harmony;
        public static bool isEnabled = false;
        public static int startPercent = 0;
        public static List<float> customOffsets = new List<float>();
        public static bool shouldSkipOffset = false;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            modEntry.OnToggle = OnToggle;
            return true;
        }

        public static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            isEnabled = value;
            if (isEnabled)
            {
                harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            else
            {
                harmony.UnpatchAll(modEntry.Info.Id);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(scrController), nameof(scrController.Start_Rewind))]
    public static class scrController_Start_Rewind_Patch
    {
        public static void Postfix(scrController __instance)
        {
            if (!Main.isEnabled) return;
            Main.startPercent = Mathf.FloorToInt(__instance.percentComplete * 100f);
            Main.customOffsets.Clear();
        }
    }

    [HarmonyPatch(typeof(DetailedResults), nameof(DetailedResults.Show))]
    public static class DetailedResults_Show_Patch
    {
        public static void Postfix()
        {
            if (!Main.isEnabled) return;
            var controller = scrController.instance;
            var ui = scrUIController.instance;

            if (controller != null && ui != null && ui.txtCongrats != null)
            {
                bool isFail = (controller.state == States.Fail || controller.state == States.Fail2);

                if (controller.startedFromCheckpoint || isFail)
                {
                    int currentPercent = Mathf.FloorToInt(controller.percentComplete * 100f);

                    if (controller.startedFromCheckpoint)
                    {
                        ui.txtCongrats.text = $"{Main.startPercent}~{currentPercent}{Localization.Get("completed")}!";
                    }
                    else
                    {
                        ui.txtCongrats.text = $"{currentPercent}{Localization.Get("completed")}!";
                    }

                    ui.txtCongrats.gameObject.SetActive(true);

                    if (ui.txtPercent != null) ui.txtPercent.gameObject.SetActive(false);
                    if (ui.txtAprilCongrats != null) ui.txtAprilCongrats.gameObject.SetActive(false);
                }
            }
        }
    }

    [HarmonyPatch(typeof(DetailedResults), "GenerateResults")]
    public static class DetailedResults_GenerateResults_Patch
    {
        public static void Postfix(ref string __result)
        {
            if (!Main.isEnabled) return;

            float speed = ADOBase.conductor.song.pitch;
            string speedInfo = $"{Localization.Get("speed")}: {speed:F2}x";

            float avgAngle = 0f;
            if (Main.customOffsets.Count > 0)
            {
                float sum = 0f;
                foreach (float f in Main.customOffsets) sum += f;
                avgAngle = sum / Main.customOffsets.Count;
            }

            string errorInfo = $"{Localization.Get("avgOffset")}: {avgAngle:F1}ms";
            string infoLine = $"<color=white>{speedInfo}     {errorInfo}</color>";

            __result += $"{infoLine}";
        }
    }

    [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
    public static class scrMarginTracker_AddHit_Patch
    {
        public static void Prefix(HitMargin hit)
        {
            if (!Main.isEnabled) return;
            Main.shouldSkipOffset = (hit == HitMargin.TooEarly ||
                                     hit == HitMargin.TooLate ||
                                     hit == HitMargin.FailMiss ||
                                     hit == HitMargin.FailOverload);
        }
    }

    [HarmonyPatch(typeof(scrHitErrorMeter), nameof(scrHitErrorMeter.AddHit))]
    public static class scrHitErrorMeter_AddHit_Patch
    {
        public static void Prefix(float angleDiff, float marginScale, scrPlanet planet, scrFloor hitFloor)
        {
            if (!Main.isEnabled) return;
            if (Main.shouldSkipOffset) return;
            if (ADOBase.controller.state != States.PlayerControl) return;
            if (Mathf.Abs(angleDiff) > 0.5f) return;

            float currentBpm = (float)(ADOBase.conductor.bpm * planet.player.planetarySystem.speed * ADOBase.conductor.song.pitch);
            float angularVelocity = (currentBpm * 2f * Mathf.PI) / 60f;

            if (angularVelocity < 0.01f) return;

            float offsetMs = (angleDiff / angularVelocity) * 1000f;
            Main.customOffsets.Add(offsetMs);
        }
    }

    [HarmonyPatch(typeof(scrController), nameof(scrController.FailAction))]
    public static class scrController_FailAction_Patch
    {
        public static void Postfix(scrController __instance)
        {
            if (!Main.isEnabled) return;
            if (__instance.txtTryCalibrating != null) __instance.txtTryCalibrating.text = "";

            if (scrUIController.instance != null && scrUIController.instance.txtCountdown != null)
            {
                var textComp = scrUIController.instance.txtCountdown.GetComponent<UnityEngine.UI.Text>();
                if (textComp != null) textComp.text = "";
            }
        }
    }

    [HarmonyPatch(typeof(scrController), nameof(scrController.Fail2Action))]
    public static class scrController_Fail2Action_Patch
    {
        public static void Postfix(scrController __instance)
        {
            if (!Main.isEnabled) return;

            if (__instance.txtTryCalibrating != null) __instance.txtTryCalibrating.text = "";
            if (__instance.txtTryCalibrating != null) __instance.txtTryCalibrating.gameObject.SetActive(false);

            if (__instance.detailedResults != null)
            {
                __instance.detailedResults.gameObject.SetActive(true);
                __instance.detailedResults.Show();
            }
        }
    }
}
