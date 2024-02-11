# ResoniteIkCulling

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that disables the IK of Users who are behind you or far away. Includes IK throttling.

## Requirements
- [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) 2.6.0 or later
- [ResoniteModSettings](https://github.com/badhaloninja/ResoniteModSettings) for live config editing

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [ResoniteIKCulling.dll](https://github.com/Raidriar796/ResoniteIkCulling/releases/latest/download/ResoniteIKCulling.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create the folder for you.
3. Start the game. If you want to verify that the mod is working you can check your Resonite logs.

## How does it work?

This mod tries to reduce the amount of work that the CPU does when calculating IK. This is done by interrupting the normal IK solve process by first checking a handful of conditions to determine if the IK should or should not solve, then it either skips the solving process or lets it proceed. This does add slightly more work before each IK solve so the default options are configured to reduce as much work as possible while trying to be unintrusive.

## How much does this improve performance?

In most situations, not much if at all. This is because IK itself is rarely the bottleneck of any given session. Despite this, it is overall reducing the work the CPU has to do as IK is one of the heaviest components you'll commonly run into, especially since the vast majority of avatars utilize Resonite's IK system. There is often a lot going on in any given session, so at the very least you may see an improvement in thermals or power consumption, but you may not see benefits in framerate or frametimes unless you use throttling options or a session is actually being bottlenecked by IK.

## Info
This is a fork of [IKCulling](https://github.com/KyuubiYoru/IkCulling/) for that aims to improve and maintain the mod.
