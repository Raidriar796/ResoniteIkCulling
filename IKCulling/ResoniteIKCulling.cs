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
            new ModConfigurationKey<bool>(
                "Enabled",
                "ResoniteIkCulling is Enabled.",
                () => true);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> DisableAfkUser =
            new ModConfigurationKey<bool>(
                "DisableAfkUser",
                "Disable IK of users not in the session.",
                () => true);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> DisableIkWithoutUser =
            new ModConfigurationKey<bool>(
                "DisableIkWithoutUser",
                "Disable IK without an active user.",
                () => true);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<int> MinUserCount =
            new ModConfigurationKey<int>(
                "MinUserCount",
                "Minimum amount of active users in the world to enable culling.",
                () => 3);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> UseUserScale =
            new ModConfigurationKey<bool>(
                "UseUserScale",
                "Use your scale for distance checks.",
                () => false);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> UseOtherUserScale =
            new ModConfigurationKey<bool>("UseOtherUserScale",
                "Use other user's scale for distance checks.",
                () => false);

        [Range(-1f, 1f)]
        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> Fov = 
            new ModConfigurationKey<float>(
                "FOV",
                "Field of view used for culling, ranging from never culled (-1) to fully culled (1).",
                () => 0.6f, false, v => v <= 1f && v >= -1f);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> MinCullingRange =
            new ModConfigurationKey<float>(
                "MinCullingRange",
                "Minimum range for IK to always be enabled, useful for mirrors.",
                () => 4);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> MaxViewRange =
            new ModConfigurationKey<float>(
                "MaxViewRange",
                "Maximum range before fully disabling IK.",
                () => 30);

        public override string Name => "ResoniteIkCulling";
        public override string Author => "Raidriar796 & KyuubiYoru";
        public override string Version => "2.1.1";
        public override string Link => "https://github.com/Raidriar796/ResoniteIkCulling";

        public override void OnEngineInit()
        {
            try
            {
                Harmony harmony = new Harmony("net.Raidriar796.ResoniteIkCulling");
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
                    //IkCulling is Disabled
                    if (!Config.GetValue(Enabled)) return true;

                    //User is Headless
                    if (__instance.LocalUser.HeadDevice == HeadOutputDevice.Headless) return false;

                    //Always Update local Ik
                    if (__instance.IsUnderLocalUser) return true;

                    //Ik is Disabled
                    if (!__instance.Enabled) return false;

                    //Users not present
                    if (__instance.Slot.ActiveUser != null && Config.GetValue(DisableAfkUser) &&
                        !__instance.Slot.ActiveUser.IsPresentInWorld) return false;

                    //No active user
                    if (Config.GetValue(DisableIkWithoutUser) && !__instance.IsEquipped) return false;

                    //Too few users
                    if (__instance.Slot.World.ActiveUserCount < Config.GetValue(MinUserCount)) return true;

                    
                    float3 playerPos = __instance.Slot.World.LocalUserViewPosition;
                    float3 ikPos = __instance.HeadProxy.GlobalPosition;

                    float dist = MathX.DistanceSqr(playerPos, ikPos);

                    //Include user scale in calculation
                    if (Config.GetValue(UseUserScale)) dist = dist / Sqr(__instance.LocalUserRoot.GlobalScale);

                    //Include other user's scale in calculation
                    if (Config.GetValue(UseOtherUserScale))
                        if (__instance.Slot.ActiveUser != null)
                            dist = dist / Sqr(__instance.Slot.ActiveUser.Root.GlobalScale);

                    //Check if IK is outside of max range
                    if (dist > Sqr(Config.GetValue(MaxViewRange))) return false;

                    //Check if IK is within min range and within dot product range
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

        //Used to search through the entire FBT calibrator to enable "AutoUpdate" on every IK
        public static void CalibratorForceIkAutoUpdate(FullBodyCalibrator __instance) {
            var allVRIK = __instance.Slot.GetComponentsInChildren<VRIK>();
            foreach (var vrik in allVRIK) {
                vrik.AutoUpdate.Value = true;
            }
        }

    
        //Forces IK to be active when FBT calibrator is created
        [HarmonyPatch(typeof(FullBodyCalibrator), "OnAwake")]
        class FullBodyCalibratorPatch {
            
            [HarmonyPostfix]
            static void Postfix(FullBodyCalibrator __instance) {
                __instance.RunInUpdates(3, ()=> {
                    CalibratorForceIkAutoUpdate(__instance);
                });
            }
        }

        //Forces IK to be active when pressing "Calibrate Avatar" on the FBT calibrator
        [HarmonyPatch(typeof(FullBodyCalibrator), "CalibrateAvatar")]
        class FullBodyCalibratorAvatarPatch {

            [HarmonyPostfix]
            static void Postfix(FullBodyCalibrator __instance) {
                CalibratorForceIkAutoUpdate(__instance);
            }
            
        }
    }
}