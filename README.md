## AutoDismount

> **Superseded by [YABOT](https://github.com/Le-Vagabond-gh/FFXIV_YABOT).**
> AutoDismount has been folded into YABOT (Yet Another Bundle of Tweaks) as the
> "Auto-Dismount on Blocked Action" feature. New users should install YABOT
> instead - this repository is archived and no longer receives updates.

Automatically dismounts your character when you try to use actions that are unavailable while mounted.

**Author:** Le Vagabond

## Installation
- Download the DLL and manifest JSON from [Releases](https://github.com/Le-Vagabond-gh/ffxiv_autodismount/releases) in the same location
- Open the Dalamud Plugin Installer
- Go to Settings
- Head to the "Experimental" tab
- Under "Dev Plugin Locations", click "Select dev plugin DLL"
- Add the DLL you downloaded
- Press "Save and Close"
- in the main plugin installer window, enable the plugin in Dev Tools

note: adding custom repositories to Dalamud is a security risk, this way protects you from malicious updates from untrusted sources

## Usage
This plugin works automatically once enabled. When you try to use any action that is blocked while mounted (status 579), the plugin will automatically dismount you instead.

No configuration required - it just works!

You can enable or disable the plugin at any time from the Dalamud Plugin Installer.
