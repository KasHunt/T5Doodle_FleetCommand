MixCast SDK for Unity - v2.5.1
(c) Blueprint Reality Inc., 2022. All rights reserved
https://mixcast.me

Basic Installation:
- MixCast will automatically activate when your project runs starting with v2.5.0!

To Test:
1) Ensure MixCast is running (MixCast icon in the system tray) or launch it from the Start Menu
2) Run your application and a MixCast output window should launch shortly after

Comprehensive instructions can be found here: https://mixcast.me/docs/develop/unity

Note: When upgrading from a previous version of MixCast, please delete the old MixCast folder(s) before importing the new package.
	  If MixCast or any other folder re-imports right after deleting, please close Unity and any code editors (Visual Studio, MonoDevelop) or other programs that may be accessing script files and try again.

Project Requirements:
- Unity 5.4.0 or above
- Windows Standalone x64
- Api Compatibility: .Net 2.0 or greater (.Net 2.0 Subset not supported)
- Scripting Backend: Mono (IL2CPP not yet supported)
- XR Platform: OpenVR or Oculus

Note: To produce MixCast output from your application at runtime, the separately offered MixCast Client must be installed, configured, and running.


Extras:
MixCast also comes with some extra prefabs and scripts to aid with mixed reality development and production.

SetRendererVisibilityForMixCast script - Controls whether the specified Renderer components are visible during regular rendering or MixCast rendering rather than both. By default grabs all Renderers under it in the hierarchy.

Slate UI prefab - Provides a film slate style display that can be called up via keypress to aid in the capture of multiple takes in a row. Inspect the attached script for more details. Drop into any scene.

Player Blob Shadow prefabs - Causes a simple blob shadow to appear in MixCast output at the point on the ground where the player's head is over. Attach to the Head or Eye transform.


Known Issues:
- MixCast and the VR experience must be running at the same permission level to communicate (either both Run as Admin or neither)


Additional Info:

MixCast Changelist - https://mixcast.me/route.php?dest=sdk_changelist_unity
MixCast User and Developer Documentation - https://mixcast.me/docs/
MixCast Support - https://support.blueprintreality.com/