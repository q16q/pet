@echo off
setlocal enabledelayedexpansion

set "source=C:\Program Files (x86)\Steam\steamapps\common\KoboldKare\KoboldKare_Data\Managed"
set "target=lib"

rmdir /s /q "%target%" 2>nul
mkdir "%target%"

for %%f in (
    "UnityEngine.dll"
    "UnityEngine.CoreModule.dll"
    "UnityEngine.AssetBundleModule.dll"
    "UnityEngine.UI.dll"
    "UnityEngine.UIModule.dll"
    "UnityEngine.TextRenderingModule.dll"
    "Unity.ResourceManager.dll"
    "Unity.Addressables.dll"
    "naelstrof.Modding.dll"
    "Assembly-CSharp.dll"
    "com.rlabrecque.steamworks.net.dll"
) do (
    copy "!source!\%%f" "!target!" >nul
)

echo Done.
pause
