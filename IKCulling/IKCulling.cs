using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.FinalIK;
using HarmonyLib;
using ResoniteModLoader;

namespace IkCulling
{
    public class IkCulling : ResoniteMod
    {
        public static ModConfiguration Config;

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> Enabled =
            new ModConfigurationKey<bool>("Enabled", "IkCulling Enabled.", () => true);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> DisableAfkUser =
            new ModConfigurationKey<bool>("DisableAfkUser", "Disable IK's of users not in the session.", () => true);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> DisableIkWithoutUser =
            new ModConfigurationKey<bool>("DisableIkWithoutUser", "Disable IK's without active user.", () => true);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<int> MinUserCount =
            new ModConfigurationKey<int>("MinUserCount", "Min amount of active users in the world to enable ik culling.",
                () => 3);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> UseUserScale =
            new ModConfigurationKey<bool>("UseUserScale", "Should user scale be used for Distance check.", () => false);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> UseOtherUserScale =
            new ModConfigurationKey<bool>("UseOtherUserScale",
                "Should the other user's scale be used for Distance check.", () => false);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> Fov = new ModConfigurationKey<float>(
            "Fov",
            "Field of view used for IkCulling, can be between 1 (fully fov culled) and -1 (never fov culled).",
            () => 0.6f, false, v => v <= 1f && v >= -1f);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> MinCullingRange =
            new ModConfigurationKey<float>("MinCullingRange",
                "Minimal range for IkCulling, useful in front of a mirror.",
                () => 4);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> MaxViewRange =
            new ModConfigurationKey<float>("MaxViewRange", "Maximal view range where IkCulling is always enabled.",
                () => 30);

        public override string Name => "IkCulling";
        public override string Author => "Raidriar796 & KyuubiYoru";
        public override string Version => "2.1.0";
        public override string Link => "https://github.com/Raidriar796/IkCulling";

        public override void OnEngineInit()
        {
            try
            {
                Harmony harmony = new Harmony("net.Raidriar796.IkCulling");
                harmony.PatchAll();

                Config = GetConfiguration();

                Config.Save(true);
            }
            catch (Exception e)
            {
                Error(e.Message);
                Error(e.ToString());
                throw;
            }
        }

        public static float Sqr(float num) {
                return (num * num);
            }

        
        [HarmonyPatch(typeof(VRIKAvatar))]
        public class IkCullingPatch
        {

            [HarmonyPrefix]
            [HarmonyPatch("OnCommonUpdate")]
            private static bool OnCommonUpdatePrefix(VRIKAvatar __instance)
            {
                try
                {
                    if (!Config.GetValue(Enabled)) return true; //IkCulling is Disabled

                    if (__instance.LocalUser.HeadDevice == HeadOutputDevice.Headless) return false; //User is Headless

                    if (__instance.IsUnderLocalUser) return true; //Always Update local Ik

                    if (!__instance.Enabled) return false; //Ik is Disabled

                    if (__instance.Slot.ActiveUser != null && Config.GetValue(DisableAfkUser) &&
                        !__instance.Slot.ActiveUser.IsPresentInWorld) return false; //Users not present

                    if (Config.GetValue(DisableIkWithoutUser) && !__instance.IsEquipped) return false; //No active user

                    if (__instance.Slot.World.ActiveUserCount < Config.GetValue(MinUserCount)) return true; //Too few users

                    
                    float3 playerPos = __instance.Slot.World.LocalUserViewPosition;
                    float3 ikPos = __instance.HeadProxy.GlobalPosition;

                    float dist = MathX.DistanceSqr(playerPos, ikPos);

                    if (Config.GetValue(UseUserScale)) dist = dist / Sqr(__instance.LocalUserRoot.GlobalScale);

                    if (Config.GetValue(UseOtherUserScale))
                        if (__instance.Slot.ActiveUser != null)
                            dist = dist / Sqr(__instance.Slot.ActiveUser.Root.GlobalScale);

                    if (dist > Sqr(Config.GetValue(MaxViewRange))) return false;

                    if (dist > Sqr(Config.GetValue(MinCullingRange)) && MathX.Dot((ikPos - playerPos).Normalized, __instance.Slot.World.LocalUserViewRotation * float3.Forward) < Config.GetValue(Fov)) return false;

                    return true;
                }
                catch (Exception e)
                {
                    Debug("Error in OnCommonUpdatePrefix");
                    Debug(e.Message);
                    Debug(e.StackTrace);
                    return true;
                }
            }
        }

        public static void CalibratorForceIkAutoUpdate(FullBodyCalibrator __instance) {
            var allVRIK = __instance.Slot.GetComponentsInChildren<VRIK>();
            foreach (var vrik in allVRIK) {
                vrik.AutoUpdate.Value = true;
            }
        }

    
        [HarmonyPatch(typeof(FullBodyCalibrator), "OnAwake")]
        class FullBodyCalibratorPatch {
            
            [HarmonyPostfix]
            static void Postfix(FullBodyCalibrator __instance) {
                __instance.RunInUpdates(3, ()=> {
                    CalibratorForceIkAutoUpdate(__instance);
                });
            }
        }

        [HarmonyPatch(typeof(FullBodyCalibrator), "CalibrateAvatar")]
        class FullBodyCalibratorAvatarPatch {

            [HarmonyPostfix]
            static void Postfix(FullBodyCalibrator __instance) {
                CalibratorForceIkAutoUpdate(__instance);
            }
            
        }
    }
}