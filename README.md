# IkCulling

A [NeosModLoader](https://github.com/zkxs/NeosModLoader) mod for [Neos VR](https://neos.com/) that disables the IK of Users who are behind you or far away.


## Installation
1. Install [NeosModLoader](https://github.com/zkxs/NeosModLoader).
2. Place [IKCulling.dll](https://github.com/Raidriar796/IkCulling/releases/latest/download/IKCulling.dll) into your `nml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\nml_mods` for a default install. You can create it if it's missing, or if you launch the game once with NeosModLoader installed it will create the folder for you.
3. Start the game. If you want to verify that the mod is working you can check your Neos logs.

## Info
This is a fork of [IKCulling](https://github.com/KyuubiYoru/IkCulling/) that aims to improve it's efficiency. No new features are planned as of now but I may make more changes and additions in the future.

## Notes for building from source
Add the following .dll files to the included `Libraries` folder
- 0Harmony.dll
- BaseX.dll
- Frooxengine.dll
- NeosModLoader.dll
- UnityEngine.dll
- UnityNeos.dll
