using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.FinalIK;
using HarmonyLib;
using ResoniteModLoader;
using UnityEngine;

namespace IkCulling
{
    public class IkCulling : ResoniteMod
    {
        public static ModConfiguration Config;

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> Enabled =
            new ModConfigurationKey<bool>(
                "Enabled",
                "Enabled",
                () => true);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> DummySpacer1 =
            new ModConfigurationKey<dummy>(
                " ",
                "");
        
        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> DummySpacer2 =
            new ModConfigurationKey<dummy>(
                "DummySpacer2",
                "<b>Culling Behavior Options:</b>");

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<ScaleCompType> ScaleComp =
            new ModConfigurationKey<ScaleCompType>(
                "ScaleComp",
                "Type of scale compensation used for distance checks.",
                () => ScaleCompType.None);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float?> FOV = 
            new ModConfigurationKey<float?>(
                "FOV",
                "Enable to force specific culling FOV, automatic when disabled.",
                () => null, false, v => v <= 180 && v >= 0 || v == null);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<byte> MinUserCount =
            new ModConfigurationKey<byte>(
                "MinUserCount",
                "Minimum amount of users in the world to enable culling.",
                () => 3, false, v => v >= 0);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> MinCullingRange =
            new ModConfigurationKey<float>(
                "MinCullingRange",
                "Minimum range for IK to always be enabled, useful for mirrors.",
                () => 4f, false, v => v >= 0);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> MaxViewRange =
            new ModConfigurationKey<float>(
                "MaxViewRange",
                "Maximum range before fully disabling IK.",
                () => 30f, false, v => v >= 0);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> DummySpacer3 =
            new ModConfigurationKey<dummy>(
                "  ",
                "");

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> DummySpacer4 =
            new ModConfigurationKey<dummy>(
                "DummySpacer4",
                "<b>Extra Culling Options:</b>");

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

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> DisableOnDashboard =
            new ModConfigurationKey<bool>(
                "DisableOnInactiveUser",
                "Disable IK if the SteamVR/Oculus dash is open or the window is not focused on desktop mode.",
                () => true);

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> DummySpacer5 =
            new ModConfigurationKey<dummy>(
                "   ",
                "");

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> DummySpacer6 =
            new ModConfigurationKey<dummy>(
                "DummySpacer6",
                "<b>Throttling Options:</b>");

        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<IkUpdateRate> UpdateRate =
            new ModConfigurationKey<IkUpdateRate>(
                "UpdateRate",
                "Update rate for IK.",
                () => IkUpdateRate.Full);

        [FrooxEngine.Range(0, 100)]
        [AutoRegisterConfigKey] public static readonly ModConfigurationKey<byte> IkUpdateFalloff =
            new ModConfigurationKey<byte>(
                "IkUpdateFalloff",
                "Reduce IK updates based on distance, threshold is 0 - 100% relative to Max Range.",
                () => 50, false, v => v <= 100 && v >= 0);

        public override string Name => "ResoniteIkCulling";
        public override string Author => "Raidriar796 & KyuubiYoru";
        public override string Version => "2.6.2";
        public override string Link => "https://github.com/Raidriar796/ResoniteIkCulling";

        public override void OnEngineInit()
        {
            try
            {
                if (ModLoader.VERSION == "2.4.0") OutOfDateNotifier();
                else if (ModLoader.VERSION == "2.5.0") OutOfDateNotifier();
                else if (ModLoader.VERSION == "2.5.1") OutOfDateNotifier();
                else
                {
                    Harmony harmony = new Harmony("net.raidriar796.ResoniteIkCulling");
                    harmony.PatchAll();
                }

                Config = GetConfiguration();

                Config.Save(true);
            }
            catch (Exception e)
            {
                Msg("Error on startup");
                Debug(e.Message);
                Debug(e.ToString());
                throw;
            }

            Engine.Current.RunPostInit(() =>
            {
                if (Engine.Current.SystemInfo.HeadDevice.IsVR())
                {
                    DiscoverHeadset();
                }

                //Calling the methods on start instead of declaring the
                //variables with method calls because rml doesn't like that
                UpdateMinRange();
                UpdateMaxRange();
                UpdateFalloff();
            });

            //Sets up variables to avoid redundant calculations per ik per frame
            //by saving the result of calculations that infrequently change
            FOV.OnChanged += (value) => { UpdateFOV(); };
            MinCullingRange.OnChanged += (value) => { UpdateMinRange(); };
            MaxViewRange.OnChanged += (value) => { UpdateMaxRange(); };
            IkUpdateFalloff.OnChanged += (value) => { UpdateFalloff(); };
        }

        public static void OutOfDateNotifier()
        {
            Msg("Mod loader version out of date, please update to version 2.6.0 or later");
        }

        //Variables
        private static Headset UserHeadset = Headset.Unknown;
        private static DesktopRenderSettings RenderSettingsInstance = null;
        private static float FOVDegToDot = 0f;
        private static float MinCullingRangeSqr = 0f;
        private static float MaxViewRangeSqr = 0f;
        private static float PercentAsFloat = 0f;
        private static float Threshold = 0f;
        private static float FalloffStep1 = 0f;
        private static float FalloffStep2 = 0f;
        private static float FalloffStep3 = 0f;
        private static float FalloffStep4 = 0f;
        private static float FalloffStep5 = 0f;
        public enum ScaleCompType
        {
            None,
            Relative,
            YourUserScale,
            OtherUserScale
        }
        //Value is exact or average FOV per headset(s)
        public enum Headset : byte
        {
            Beyond = 102,
            Index = 108,
            Pico = 102,
            Pimax = 150,
            Quest = 101,
            Reverb = 97,
            Rift = 89,
            Unknown = 100,
            Vive = 103
        }
        public enum IkUpdateRate
        {
            Full,
            Half,
            Quarter,
            Eighth
        }
        //Since this is retrieving hardware info instead of checking a known list,
        //this instead checks common words headset info may return to guess
        //what headset a user is using, until a better solution is found
        public static void DiscoverHeadset()
        {
            string XRDeviceModel = Engine.Current.SystemInfo.XRDeviceModel;

            if (XRDeviceModel.Contains("Beyond"))
            {
                UserHeadset = Headset.Beyond;
            }
            else if (XRDeviceModel.Contains("Index"))
            {
                UserHeadset = Headset.Index;
            }
            else if (XRDeviceModel.Contains("Pico"))
            {
                UserHeadset = Headset.Pico;
            }
            else if (XRDeviceModel.Contains("Pimax"))
            {
                UserHeadset = Headset.Pimax;
            }
            else if (XRDeviceModel.Contains("Quest"))
            {
                UserHeadset = Headset.Quest;
            }
            else if (XRDeviceModel.Contains("Reverb"))
            {
                UserHeadset = Headset.Reverb;
            }
            else if (XRDeviceModel.Contains("Rift"))
            {
                UserHeadset = Headset.Rift;
            }
            else if (XRDeviceModel.Contains("Vive"))
            {
                UserHeadset = Headset.Vive;
            }
        }

        //Populated to every IK in dictionary.
        //We probably didn't need to do this, but in the event
        //that more variables per IK are needed, this is here.
        public class Variables
        {
            public byte UpdateIndex = 1;
        }

        //Updates FOV when settings are fetched on startup
        [HarmonyPatch(typeof(DesktopRenderSettings), "OnAwake")]
        public class FOVSettingFetchPatch()
        {
            private static void Postfix(DesktopRenderSettings __instance)
            {
                RenderSettingsInstance = __instance;

                RenderSettingsInstance.Changed += (FieldOfView) => { UpdateFOV(); };
            }
        }

        public static void UpdateFOV()
        {
            if (!Config.GetValue(FOV).HasValue)
            {
                if (Engine.Current.SystemInfo.HeadDevice.IsVR())
                {
                    //Value assigned when in VR
                    FOVDegToDot = MathX.Cos(MathX.Deg2Rad * (float)UserHeadset);
                }
                else
                {
                    //Value assigned when in desktop
                    FOVDegToDot = MathX.Cos(MathX.Deg2Rad * ((RenderSettingsInstance != null) ? RenderSettingsInstance.FieldOfView.Value : 60f));
                }
            }
            else
            {
                //Value assigned when user manually specifies FOV
                FOVDegToDot = MathX.Cos(MathX.Deg2Rad * Config.GetValue(FOV).Value);
            }
        }

        public static void UpdateMinRange()
        {   
            MinCullingRangeSqr = MathX.Pow(Config.GetValue(MinCullingRange), 2f);
        }

        public static void UpdateMaxRange()
        {   
            MaxViewRangeSqr = MathX.Pow(Config.GetValue(MaxViewRange), 2f);
        }

        public static void UpdateFalloff()
        {
            PercentAsFloat = Config.GetValue(IkUpdateFalloff) * 0.01f;

            Threshold = MathX.Lerp(0f, Config.GetValue(MaxViewRange), PercentAsFloat);

            FalloffStep1 = MathX.Pow(Threshold, 2f);

            FalloffStep2 = MathX.Pow(MathX.Lerp(Threshold, Config.GetValue(MaxViewRange), 0.2f), 2f);

            FalloffStep3 = MathX.Pow(MathX.Lerp(Threshold, Config.GetValue(MaxViewRange), 0.4f), 2f);
            
            FalloffStep4 = MathX.Pow(MathX.Lerp(Threshold, Config.GetValue(MaxViewRange), 0.6f), 2f);

            FalloffStep5 = MathX.Pow(MathX.Lerp(Threshold, Config.GetValue(MaxViewRange), 0.8f), 2f);
        }

        static Dictionary<VRIKAvatar, Variables> vrikList = new Dictionary<VRIKAvatar, Variables>();

        [HarmonyPatch("OnAwake")]
        [HarmonyPostfix]
        private static void AddToList(VRIKAvatar __instance)
        {
            //Adds IK to list as new instances appear
            if (!vrikList.ContainsKey(__instance)) {
                vrikList.Add(__instance, new Variables());
            }

            foreach (var item in vrikList)
            {
                if (item.Key == null)
                {
                    vrikList.Remove(item.Key);
                }
            }
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
                    //IkCulling is disabled
                    if (!Config.GetValue(Enabled)) return true;
                        
                    //Ik is disabled
                    if (!__instance.Enabled) return false;

                    //User is Headless
                    if (ModLoader.IsHeadless) return false;

                    //Always skip local Ik
                    if (__instance.IsUnderLocalUser && __instance.IsEquipped) return true;

                    //Too few users
                    if (__instance.Slot.World.UserCount < Config.GetValue(MinUserCount)) return true;

                    //Platform dash is open or user is not focused on window
                    if (Config.GetValue(DisableOnDashboard))
                    {
                        if (__instance.LocalUser.VR_Active)
                        {
                            if (__instance.LocalUser.IsPlatformDashOpened) return false;
                        }
                        else
                        {
                            if (!Application.isFocused) return false;
                        }
                    }
                        
                    //No active user
                    if (Config.GetValue(DisableIkWithoutUser) && !__instance.IsEquipped) return false;

                    //Users not present
                    if (Config.GetValue(DisableAfkUser) && __instance.Slot.ActiveUser != null &&
                        !__instance.Slot.ActiveUser.IsPresentInWorld) return false;

                    float3 playerPos = __instance.Slot.World.LocalUserViewPosition;
                    float3 ikPos = __instance.HeadProxy.GlobalPosition;

                    float dist = MathX.DistanceSqr(playerPos, ikPos);

                    float LocalUserScale = __instance.LocalUserRoot.GlobalScale;
                    
                    switch (Config.GetValue(ScaleComp))
                    {
                        case ScaleCompType.None:
                        break;

                        case ScaleCompType.Relative:
                        dist /= LocalUserScale * LocalUserScale;
                        if (__instance.IsEquipped)
                        {
                            dist /= __instance.Slot.ActiveUser.Root.GlobalScale * __instance.Slot.ActiveUser.Root.GlobalScale;
                        }
                        break;
                                        
                        case ScaleCompType.YourUserScale:
                        dist /= LocalUserScale * LocalUserScale;
                        break;

                        case ScaleCompType.OtherUserScale:
                        if (__instance.IsEquipped)
                        {
                            dist /= __instance.Slot.ActiveUser.Root.GlobalScale * __instance.Slot.ActiveUser.Root.GlobalScale;
                        }
                        break;

                        default:
                        break;
                    }

                    //Checks if IK is within min range and in view
                    if (dist > MinCullingRangeSqr &&
                    MathX.Dot((ikPos - playerPos).Normalized, __instance.Slot.World.LocalUserViewRotation * float3.Forward) < FOVDegToDot) 
                    return false;

                    //Check if IK is outside of max range
                    if (dist > MaxViewRangeSqr) 
                    return false;
                        
                    //IK throttling
                    if ((Config.GetValue(IkUpdateFalloff) < 100) || 
                    (Config.GetValue(UpdateRate) != IkUpdateRate.Full))
                    {
                        //Adds an IK instance to the list if it's not already
                        if (!vrikList.ContainsKey(__instance))
                        {
                            vrikList.Add(__instance, new Variables());
                            return true;
                        }
                        byte skipCount = 1;

                        //Update skips for falloff
                        if (Config.GetValue(IkUpdateFalloff) < 100) 
                        {
                            if (dist > FalloffStep5)
                            {
                                skipCount = 6;
                            }
                            else if (dist > FalloffStep4)
                            {
                                skipCount = 5;
                            }
                            else if (dist > FalloffStep3)
                            {
                                skipCount = 4;
                            }
                            else if (dist > FalloffStep2)
                            {
                                skipCount = 3;
                            }
                            else if (dist > FalloffStep1)
                            {
                                skipCount = 2;
                            }
                        }

                        //Update skips lower update rate
                        else switch (Config.GetValue(UpdateRate))
                        {
                            case IkUpdateRate.Half:
                            skipCount = 2;
                            break;
                                            
                            case IkUpdateRate.Quarter:
                            skipCount = 4;
                            break;

                            case IkUpdateRate.Eighth:
                            skipCount = 8;
                            break;

                            default:
                            skipCount = 1;
                            break;
                        }

                        //Update skips for falloff + lower update rate
                        if (Config.GetValue(UpdateRate) != IkUpdateRate.Full && (Config.GetValue(IkUpdateFalloff) < 100))
                        {
                            switch (Config.GetValue(UpdateRate))
                            {
                                case IkUpdateRate.Half:
                                skipCount *= 2;
                                break;
                                                    
                                case IkUpdateRate.Quarter:
                                skipCount *= 4;
                                break;

                                case IkUpdateRate.Eighth:
                                skipCount *= 8;
                                break;

                                default:
                                skipCount = 1;
                                break;
                            }
                        }
                        //The part that actually skips updates
                        if (vrikList[__instance].UpdateIndex > skipCount) vrikList[__instance].UpdateIndex = 1;
                        if (vrikList[__instance].UpdateIndex == skipCount)
                        {
                            vrikList[__instance].UpdateIndex = 1;
                            return true;
                        }
                        else
                        {
                            vrikList[__instance].UpdateIndex += 1;
                            return false;
                        }
                    }
                }
                catch(Exception e)
                {
                    Msg("Error OnCommonUpdatePatch");
                    Debug(e.Message);
                    Debug(e.StackTrace);
                    return true;
                }
                return true;
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
            static void Postfix(FullBodyCalibrator __instance) {
                __instance.RunInUpdates(3, ()=> {
                    CalibratorForceIkAutoUpdate(__instance);
                });
            }
        }

        //Forces IK to be active when pressing "Calibrate Avatar" on the FBT calibrator
        [HarmonyPatch(typeof(FullBodyCalibrator), "CalibrateAvatar")]
        class FullBodyCalibratorAvatarPatch {
            static void Postfix(FullBodyCalibrator __instance) {
                CalibratorForceIkAutoUpdate(__instance);
            }
        }    
    }
}