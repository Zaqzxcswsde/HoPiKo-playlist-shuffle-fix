# What is this?

Current Steam version of the game [HoPiKo](https://store.steampowered.com/app/437390/HoPiKo/) has a bug where "Shuffle" and "Repeat all" playlist modes silence the music entirely during gameplay. This is a shame considering this game's OST is actually really good!

So I decided to make a small patch that fixes this bug.


# Installation

1. Download the [patched `Assembly-CSharp.dll`](https://github.com/Zaqzxcswsde/HoPiKo-playlist-shuffle-fix/releases/) from the latest release

1. Open the game's folder: in Steam Library Right Click on HoPiKo > `Properties` > `Installed Files` > `Browse...`

1. Navigate to `HoPiKo_Data` > `Managed` folder

1. (*Optional*, since the game hasn't been updated in years)\
Verify original `Assembly-CSharp.dll` hash:
    
    1. For that `Shift + Right Click` on an empty space (not a file) while in `Managed` folder
    
    1. Select `Open in Terminal` in the context window

    1. Paste this command: `Get-FileHash Assembly-CSharp.dll` and hit Enter
    
    1. You should see this value in the output:

        ```
        1D7C6B9E7EA695ED747A4C0259518B487CAE0E7FA4E42F8EB776DEEF063D73F3
        ```

        - if the hash differs, this means that the game has been updated and you would **need to patch the game manually**, read about it [below](#patch-it-yourself)

1. **Replace** the original `Assembly-CSharp.dll` with the one you've just downloaded \
(no renaming, just replace the file)

1. Launch the game and enjoy :)


# Compatibility with other mods

This mod is incompatible with other mods that use the same `Assembly-CSharp.dll` patching method.

However, it *should* work fine with mods that use other injection methods (e.g. BepInEx, MelonLoader etc.)


# Uninstallation

To revert the game back to its original state: in Steam Library Right Click on HoPiKo > `Properties` > `Installed Files` > `Verify integrity of game files`\
(this works for both manual and prebuilt installation)


# Patch it yourself

If you would rather apply the patch manually (or if the original patched binary no longer works), here's how you can do that (purely through GUI, no programming):

1. Download the latest x64 version of dnSpyEx from their [releases page](https://github.com/dnSpyEx/dnSpy/releases)

1. Open `dnSpy`, click on `File` > `Open` (or press `Ctrl+O`)

1. Navigate to the `Managed` folder (read [above](#installation) how to find it), select all files inside this folder (press `Ctrl+A`), click `Open`

1. Open `Search Assemblies` menu (press `Ctrl+Shift+K`), search for `AudioManager Update`, double click on the only result (`void Update()`)

1. This will navigate you to the `Update()` function in the main code window of `dnSpy`, Right Click on the `private void Update()` line, select `Edit Method (C#)...`\
(or you can just press `Ctrl+Shift+E` right after the previous hotkey)

1. In the newly opened window, replace all (`Ctrl+A`) code with the contents of [`Update.cs`](https://github.com/Zaqzxcswsde/HoPiKo-playlist-shuffle-fix/blob/main/src/Update.cs) file from this repository, click `Compile`

1. Then open `File` > `Save Module...`, verify that `Filename` contains the actual path to your original `Assembly-CSharp.dll` inside the game's folder, hit `OK`

1. After the build finishes, you can close `dnSpy`, your game has been successfully patched


# AI disclosure

I'm not a Unity developer, nor have I ever made game patches like this before, so I did use AI for the *initial codebase research* and for *identifying the actual bug*.

However, **I did verify its output** by myself and then **reproduced the fix on my own**, including writing the code and coming up with the steps above, just to be sure.


# Note for experienced modders

If you have experience with BepInEx/Harmony and wish to write a better version of this fix, you are very much encouraged to do so!

- To make your life easier, I've included a condensed write-up of the research that AI performed in [this document](https://github.com/Zaqzxcswsde/HoPiKo-playlist-shuffle-fix/blob/main/docs/HoPiKo-Music-Bug-Analysis.md), so you wouldn't have to start from scratch.\
(I've tried to unslop it as much as I could)

I will happily redirect everyone to your version by linking it at the beginning of this document.
