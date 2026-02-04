using BepInEx;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.UI;
using Steamworks;
using System.IO;

namespace Pet
{
    [BepInPlugin("dev.q16.plugins.pet", "pet", "1.0.2")]
    public class Pet : BaseUnityPlugin
    {
        private void Awake()
        {
            var harmony = new Harmony("dev.q16.plugins.pet");
            harmony.PatchAll();
            Logger.LogInfo("Patched!");
        }
    }
}

namespace Pet.Patches
{
    public static class Pet
    {
        public static readonly FieldInfo MMstatus = AccessTools.Field(typeof(ModManager), "status");
        public static readonly FieldInfo MMfullModList = AccessTools.Field(typeof(ModManager), "fullModList");
        public static readonly FieldInfo MMfailedToLoadMods = AccessTools.Field(typeof(ModManager), "failedToLoadMods");
        public static readonly FieldInfo MMlastException = AccessTools.Field(typeof(ModManager), "lastException");
        public static readonly FieldInfo MMready = AccessTools.Field(typeof(ModManager), "ready");
        public static readonly FieldInfo MMinstance = AccessTools.Field(typeof(ModManager), "instance");
        public static readonly FieldInfo MMearlyModPostProcessors = AccessTools.Field(typeof(ModManager), "earlyModPostProcessors");
        public static readonly FieldInfo MMmodPostProcessors = AccessTools.Field(typeof(ModManager), "modPostProcessors");
        public static readonly FieldInfo MMchanged = AccessTools.Field(typeof(ModManager), "changed");
        public static readonly FieldInfo MMcancelTokenSources = AccessTools.Field(typeof(ModManager), "cancelTokenSources");

        public static readonly FieldInfo MAmodMutex = AccessTools.Field(typeof(ModManager.ModAddressable), "modMutex");
        public static readonly FieldInfo MAloaded = AccessTools.Field(typeof(ModManager.ModAddressable), "loaded");
        public static readonly FieldInfo MAenabled = AccessTools.Field(typeof(ModManager.ModAddressable), "enabled");
        public static readonly FieldInfo MAloadedAssets = AccessTools.Field(typeof(ModManager.ModAddressable), "loadedAssets");
        public static readonly FieldInfo MAcausedException = AccessTools.Field(typeof(ModManager.ModAddressable), "causedException");
        public static readonly FieldInfo MAlocator = AccessTools.Field(typeof(ModManager.ModAddressable), "locator");
        public static readonly MethodInfo MATryUnload = AccessTools.Method(typeof(ModManager.ModAddressable), "TryUnload");
        public static readonly MethodInfo MATryLoad = AccessTools.Method(typeof(ModManager.ModAddressable), "TryLoad");
        public static readonly MethodInfo MATryGetCatalogPath = AccessTools.Method(typeof(ModManager.ModAddressable), "TryGetCatalogPath");

        public static readonly FieldInfo MABmodMutex = AccessTools.Field(typeof(ModManager.ModAssetBundle), "modMutex");
        public static readonly FieldInfo MABassetsLoaded = AccessTools.Field(typeof(ModManager.ModAssetBundle), "assetsLoaded");
        public static readonly FieldInfo MABcausedException = AccessTools.Field(typeof(ModManager.ModAssetBundle), "causedException");
        public static readonly FieldInfo MABloaded = AccessTools.Field(typeof(ModManager.ModAssetBundle), "loaded");
        public static readonly FieldInfo MABbundle = AccessTools.Field(typeof(ModManager.ModAssetBundle), "bundle");
        public static readonly FieldInfo MABshaderBundle = AccessTools.Field(typeof(ModManager.ModAssetBundle), "shaderBundle");
        public static readonly MethodInfo MABTryLoad = AccessTools.Method(typeof(ModManager.ModAssetBundle), "TryLoad");
        public static readonly MethodInfo MABTryUnload = AccessTools.Method(typeof(ModManager.ModAssetBundle), "TryUnload");
        public static readonly MethodInfo MABTryGetBundlePath = AccessTools.Method(typeof(ModManager.ModAssetBundle), "TryGetBundlePath");
        public static readonly MethodInfo MABTryGetShaderBundlePath = AccessTools.Method(typeof(ModManager.ModAssetBundle), "TryGetShaderBundlePath");

        public static readonly SemaphoreSlim modMutex = new SemaphoreSlim(16, 16);
        public static int _pendingPostProcessors;
        public static TaskCompletionSource<bool> _allPostProcessorsLoaded;
    }

    public static class CenterScreenText
    {
        public static Text centerText;
        public static Text Create(string message)
        {
            // Canvas
            var canvasGO = new GameObject("HarmonyCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            UnityEngine.Object.DontDestroyOnLoad(canvasGO);

            // Text
            var textGO = new GameObject("CenterText");
            textGO.transform.SetParent(canvasGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.text = message;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 32;
            text.color = Color.black;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(800, 200);
            rect.anchoredPosition = Vector2.zero;

            var textComponent = textGO.GetComponent<Text>();
            textComponent.raycastTarget = false;

            var canvasGroup = textGO.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            return text;
        }
    }

    [HarmonyPatch(
        typeof(ModManager),
        "SyncEnabledStatusWithLoaded"
    )]
    public static class SyncEnabledStatusWithLoadedPatch
    {
        [HarmonyPrefix]
        static bool Prefix(ModManager __instance, ref Task __result)
        {
            if (CenterScreenText.centerText == null)
            {
                Debug.Log("Created text");
                CenterScreenText.centerText = CenterScreenText.Create("");
            }
            Debug.Log("Bypassing original SyncEnabledStatusWithLoaded");
            __result = SyncEnabledStatusWithLoadedPatched(__instance);
            return false;
        }

        static async Task SyncEnabledStatusWithLoadedPatched(ModManager __instance)
        {
            Debug.Log("SESWLPatched: initialized fields");

            var fullModList = (List<ModManager.Mod>)Pet.MMfullModList.GetValue(__instance);
            var modCount = fullModList.Count;
            int loadedModCount = 0;

            try
            {
                foreach (ModManager.Mod fullMod in fullModList)
                {
                    Debug.Log("SESWLPatched: !fullMod.enabled cycle");
                    await fullMod.SetAssetsAvailable(fullMod.enabled);
                    loadedModCount++;
                    CenterScreenText.centerText.text = $"Loaded {loadedModCount} out of {modCount} mods";
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Pet.MMfailedToLoadMods.SetValue(__instance, true);
                Pet.MMlastException.SetValue(__instance, ex);
                throw;
            }
            finally
            {
                // Debug.Log("Waiting for all post-processors to finish");
                while (Pet._pendingPostProcessors > 0)
                {
                    CenterScreenText.centerText.text = $"Waiting for post-processors: {Pet._pendingPostProcessors}";
                    await Task.Delay(100);
                }
                Debug.Log($"Waiting for post-processors\n{Pet._pendingPostProcessors}");
                Debug.Log($"Loaded {loadedModCount} out of {modCount} mods");
                Debug.Log("! ! ! ! ! ! --- finished loading");
                CenterScreenText.centerText.text = "";
                FieldInfo finishedLoadingField = AccessTools.Field(typeof(ModManager), "finishedLoading");
                var finishedLoading = (ModManager.ModReadyAction)finishedLoadingField.GetValue(__instance);
                if (finishedLoading != null)
                    finishedLoading();
            }
        }
    }

    [HarmonyPatch(
        typeof(ModManager.ModAddressable),
        "SetAssetsAvailable"
    )]
    public static class MASetAssetsAvailablePatch
    {
        [HarmonyPrefix]
        static bool Prefix(ModManager.ModAddressable __instance, bool active, ref Task __result)
        {
            Debug.Log("Bypassing ModAddressable.SetAssetsAvailable");
            __result = SetAssetsAvailablePatched(__instance, active);
            return false;
        }

        static async Task RunPostProcessor(ModPostProcessor modPostProcessor, CancellationTokenSource cancelTokenSource, ModManager.ModAddressable __instance)
        {
            try
            {
                cancelTokenSource.Token.ThrowIfCancellationRequested();

                var locator = (IResourceLocator)Pet.MAlocator.GetValue(__instance);
                Debug.Log("Post processor: " + modPostProcessor.GetType().Name);

                await modPostProcessor.HandleAddressableMod(__instance.info, locator);
            }
            finally
            {
                await Pet.modMutex.WaitAsync();
                var result = Interlocked.Decrement(ref Pet._pendingPostProcessors);
                if (result == 0)
                {
                    Pet._allPostProcessorsLoaded.TrySetResult(true);
                }
                Pet.modMutex.Release();
            }
        }

        static async Task SetAssetsAvailablePatched(ModManager.ModAddressable __instance, bool active)
        {
            Debug.Log("MASAA: initalizing fields");
            var modMutex = (SemaphoreSlim)Pet.MAmodMutex.GetValue(__instance);
            var loadedAssets = (bool)Pet.MAloadedAssets.GetValue(__instance);

            foreach (var f in AccessTools.GetDeclaredFields(typeof(ModManager.ModAddressable)))
            {
                Debug.Log($"Field: {f.Name}, Type: {f.FieldType}");
            }

            Debug.Log("MASAA: initalizing modManager field");
            var modManager = (ModManager)Pet.MMinstance.GetValue(__instance);

            Debug.Log("MASAA: initalizing ModManager fields");
            var earlyModPostProcessors = (List<ModPostProcessor>)Pet.MMearlyModPostProcessors.GetValue(modManager);
            var modPostProcessors = (List<ModPostProcessor>)Pet.MMmodPostProcessors.GetValue(modManager);
            var cancelTokenSources = (List<CancellationTokenSource>)Pet.MMcancelTokenSources.GetValue(modManager);

            // Debug.Log($"MASAAPatched: Loading {__instance.info.title}");
            await modMutex.WaitAsync();
            try
            {
                if (!loadedAssets && active)
                {
                    CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
                    cancelTokenSources.Add(cancelTokenSource);
                    try
                    {
                        await __instance.SetLoaded(true);
                        int total =
                                earlyModPostProcessors.Count +
                                modPostProcessors.Count;

                        Pet._pendingPostProcessors = total;
                        Pet._allPostProcessorsLoaded = new TaskCompletionSource<bool>();

                        var allProcessors = earlyModPostProcessors.Concat(modPostProcessors);
                        var tasks = allProcessors.Select(modPostProcessor =>
                            RunPostProcessor(modPostProcessor, cancelTokenSource, __instance));

                        Task.WhenAll(tasks);  // Parallel execution + proper await
                    }
                    finally
                    {
                        cancelTokenSources.Remove(cancelTokenSource);
                    }
                    cancelTokenSource = (CancellationTokenSource)null;
                }
                else if (loadedAssets && !active)
                {
                    foreach (ModPostProcessor modPostProcessor in earlyModPostProcessors)
                        await modPostProcessor.UnloadAssets(__instance.info);
                    foreach (ModPostProcessor modPostProcessor in modPostProcessors)
                        await modPostProcessor.UnloadAssets(__instance.info);
                    await __instance.SetLoaded(false);
                }
                Pet.MAloadedAssets.SetValue(__instance, active);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // Debug.LogError((object)$"Failed to set active state for mod {__instance.info.title} [{__instance.info.publishedFileId}]");
                Pet.MMlastException.SetValue(modManager, ex);
                Pet.MAcausedException.SetValue(__instance, true);
                Pet.MMchanged.SetValue(modManager, true);
                await (Task)Pet.MATryUnload.Invoke(__instance, null);
                throw;
            }
            finally
            {
                modMutex.Release();
            }
        }
    }

    [HarmonyPatch(
        typeof(ModManager.ModAssetBundle),
        "SetAssetsAvailable"
    )]
    public static class MABSetAssetsAvailablePatch
    {
        [HarmonyPrefix]
        static bool Prefix(ModManager.ModAssetBundle __instance, bool active, ref Task __result)
        {
            Debug.Log("Bypassing ModAssetBundle.SetAssetsAvailable");
            __result = SetAssetsAvailablePatched(__instance, active);
            return false;
        }

        static async Task SetAssetsAvailablePatched(ModManager.ModAssetBundle __instance, bool active)
        {
            var modMutex = (SemaphoreSlim)Pet.MABmodMutex.GetValue(__instance);
            var assetsLoaded = (bool)Pet.MABassetsLoaded.GetValue(__instance);
            var modManager = (ModManager)Pet.MMinstance.GetValue(__instance);
            var earlyModPostProcessors = (List<ModPostProcessor>)Pet.MMearlyModPostProcessors.GetValue(modManager);
            var modPostProcessors = (List<ModPostProcessor>)Pet.MMmodPostProcessors.GetValue(modManager);

            // Debug.Log($"MABSAAPatched: Loading {__instance.info.title}");
            await modMutex.WaitAsync();
            try
            {
                if (!assetsLoaded && active)
                {
                    await __instance.SetLoaded(true);
                    Pet.MMstatus.SetValue(modManager, ModManager.ModStatus.LoadingAssets);
                    foreach (ModPostProcessor modPostProcessor in earlyModPostProcessors)
                        await modPostProcessor.HandleAssetBundleMod(__instance.info, __instance.bundle);
                    foreach (ModPostProcessor modPostProcessor in modPostProcessors)
                        await modPostProcessor.HandleAssetBundleMod(__instance.info, __instance.bundle);
                }
                else if (assetsLoaded && !active)
                {
                    foreach (ModPostProcessor modPostProcessor in earlyModPostProcessors)
                        await modPostProcessor.UnloadAssets(__instance.info);
                    foreach (ModPostProcessor modPostProcessor in modPostProcessors)
                        await modPostProcessor.UnloadAssets(__instance.info);
                }
                Pet.MABassetsLoaded.SetValue(__instance, active);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // Debug.LogError((object)$"Failed to make assets available for mod {__instance.info.title} [{__instance.info.publishedFileId}]");
                Pet.MMlastException.SetValue(modManager, ex);
                Pet.MABcausedException.SetValue(__instance, true);
                Pet.MMchanged.SetValue(modManager, true);
            }
            finally
            {
                Pet.MMstatus.SetValue(modManager, ModManager.ModStatus.Ready);
                Pet.MMready.SetValue(modManager, true);
                modMutex.Release();
            }
        }
    }

    [HarmonyPatch(
        typeof(ModManager.ModAddressable),
        "SetLoaded"
    )]
    public static class MASetLoadedPatch
    {
        [HarmonyPrefix]
        static bool Prefix(ModManager.ModAddressable __instance, bool active, ref Task __result)
        {
            Debug.Log("Overriding original ModAddressable.SetLoaded");
            __result = SetLoadedPatched(__instance, active);
            return false;
        }

        static async Task SetLoadedPatched(ModManager.ModAddressable __instance, bool active)
        {
            var loaded = (bool)Pet.MAloaded.GetValue(__instance);
            var modManager = (ModManager)Pet.MMinstance.GetValue(__instance);

            Debug.Log("MASLP: try block");
            try
            {
                if (!loaded && active)
                {
                    await (Task)Pet.MATryLoad.Invoke(__instance, null);
                }
                else
                {
                    if (!loaded || active)
                        return;
                    await (Task)Pet.MATryUnload.Invoke(__instance, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // Debug.LogError((object)$"SETLOADED/MA: Failed to set active state for mod {__instance.info.title} [{__instance.info.publishedFileId}].");
                Pet.MMlastException.SetValue(modManager, ex);
                Pet.MAcausedException.SetValue(__instance, true);
                Pet.MMchanged.SetValue(modManager, true);
                throw;
            }
        }
    }

    [HarmonyPatch(
        typeof(ModManager.ModAssetBundle),
        "SetLoaded"
    )]
    public static class MABSetLoadedPatch
    {
        [HarmonyPrefix]
        static bool Prefix(ModManager.ModAssetBundle __instance, bool active, ref Task __result)
        {
            Debug.Log("Overriding original ModAssetBundle.SetLoaded");
            __result = SetLoadedPatched(__instance, active);
            return false;
        }

        static async Task SetLoadedPatched(ModManager.ModAssetBundle __instance, bool active)
        {
            var loaded = (bool)Pet.MABloaded.GetValue(__instance);

            var managerField = AccessTools.Field(typeof(ModManager), "instance");
            var modManager = (ModManager)managerField.GetValue(__instance);

            Debug.Log("MABSLP: try block");
            try
            {
                if (!loaded && active)
                {
                    await (Task)Pet.MABTryLoad.Invoke(__instance, null);
                }
                else
                {
                    if (!loaded || active)
                        return;
                    await (Task)Pet.MABTryUnload.Invoke(__instance, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // Debug.LogError((object)$"SETLOADED/MAB: Failed to set loaded state for mod {__instance.info.title} [{__instance.info.publishedFileId}].");
                Pet.MMlastException.SetValue(modManager, ex);
                Pet.MABcausedException.SetValue(__instance, true);
                Pet.MMchanged.SetValue(modManager, true);
                throw;
            }
        }
    }

    [HarmonyPatch(
        typeof(ModManager.ModAddressable),
        "TryLoad"
    )]
    public static class MATryLoadPatch
    {
        [HarmonyPrefix]
        static bool Prefix(ModManager.ModAddressable __instance, ref Task __result)
        {
            Debug.Log("Overriding MA.TryLoad");
            __result = TryLoadPatched(__instance);
            return false;
        }

        static async Task TryLoadPatched(ModManager.ModAddressable __instance)
        {
            var modManager = (ModManager)Pet.MMinstance.GetValue(__instance);

            AsyncOperationHandle<IResourceLocator> loader;
            var locator = (IResourceLocator)Pet.MAlocator.GetValue(__instance);
            if (!__instance.IsValid())
                loader = new AsyncOperationHandle<IResourceLocator>();
            else if (locator != null)
            {
                loader = new AsyncOperationHandle<IResourceLocator>();
            }
            else
            {
                AddressablesRuntimeProperties.ClearCachedPropertyValues();
                ModManager.currentLoadingMod = $"{__instance.info.directoryInfo.FullName}{Path.DirectorySeparatorChar}";

                object[] parameters = new object[] { null };
                bool result = (bool)Pet.MATryGetCatalogPath.Invoke(__instance, parameters);
                string path = (string)parameters[0];

                if (!result)
                {
                    Pet.MAenabled.SetValue(__instance, false);
                    Pet.MAcausedException.SetValue(__instance, true);
                    Pet.MMchanged.SetValue(modManager, true);
                }
                Debug.Log("TryLoadMA: Loading content catalog...");
                loader = Addressables.LoadContentCatalogAsync(path);
                IResourceLocator task = await loader.Task;
                Debug.Log("TryLoadMA: Content catalog loaded.");
                if (!loader.IsDone || !loader.IsValid() || loader.Status == AsyncOperationStatus.Failed || loader.OperationException != null)
                {
                    Pet.MAenabled.SetValue(__instance, false);
                    Pet.MAcausedException.SetValue(__instance, true);
                    Pet.MMchanged.SetValue(modManager, true);
                }
                else
                {
                    Debug.Log("TryLoadMA: loader.Result call");
                    Pet.MAlocator.SetValue(__instance, loader.Result);
                }
                Debug.Log("TryLoadMA: Release");
                Addressables.Release<IResourceLocator>(loader);
                Debug.Log("TryLoadMA: Release ended");
                Pet.MAloaded.SetValue(__instance, true);
                loader = new AsyncOperationHandle<IResourceLocator>();
            }
        }
    }

    [HarmonyPatch(
        typeof(ModManager.ModAssetBundle),
        "TryLoad"
    )]
    public static class MABTryLoadPatch
    {
        [HarmonyPrefix]
        static bool Prefix(ModManager.ModAssetBundle __instance, ref Task __result)
        {
            Debug.Log("Overriding MAB.TryLoad");
            __result = TryLoadPatched(__instance);
            return false;
        }

        static async Task TryLoadPatched(ModManager.ModAssetBundle __instance)
        {
            var modManager = (ModManager)Pet.MMinstance.GetValue(null);
            try
            {
                object[] bundleArgs = { null };
                object[] shaderBundleArgs = { null };

                bool hasBundlePath = (bool)Pet.MABTryGetBundlePath.Invoke(__instance, bundleArgs);
                string bundlePath = (string)bundleArgs[0];

                if (!hasBundlePath)
                    throw new Exception(
                        $"Failed to load bundle. {__instance.info.title} [{__instance.info.publishedFileId}], couldn't find bundle path. {bundlePath}"
                    );

                bool hasShaderBundlePath = false;
                string shaderBundlePath = null;

                if (Pet.MABTryGetShaderBundlePath != null)
                {
                    hasShaderBundlePath = (bool)Pet.MABTryGetShaderBundlePath.Invoke(__instance, shaderBundleArgs);
                    shaderBundlePath = (string)shaderBundleArgs[0];
                }

                if (hasShaderBundlePath && !string.IsNullOrEmpty(shaderBundlePath))
                {
                    var shaderBundleHandle = AssetBundle.LoadFromFileAsync(shaderBundlePath);
                    AssetBundle shaderBundle = await shaderBundleHandle.AsTask();
                    Pet.MABshaderBundle.SetValue(__instance, shaderBundle);

                    if (!(bool)(UnityEngine.Object)shaderBundle)
                        throw new Exception(
                            $"Failed to load shader bundle. {__instance.info.title} [{__instance.info.publishedFileId}], {shaderBundlePath}"
                        );

                    foreach (var svc in shaderBundle.LoadAllAssets<ShaderVariantCollection>())
                        svc.WarmUp();
                }

                var bundleHandle = AssetBundle.LoadFromFileAsync(bundlePath);
                AssetBundle bundle = await bundleHandle.AsTask();
                Pet.MABbundle.SetValue(__instance, bundle);


                if (!(bool)(UnityEngine.Object)bundle)
                    throw new Exception(
                        $"Failed to load bundle. {__instance.info.title} [{__instance.info.publishedFileId}], {bundlePath}"
                    );

                Pet.MABloaded.SetValue(__instance, true);

                bundlePath = null;
                shaderBundlePath = null;
            }
            catch (Exception ex)
            {
                // Debug.LogError($"Failed to load bundle for {__instance.info.title} [{__instance.info.publishedFileId}].");
                Debug.LogException(ex);

                Pet.MABcausedException.SetValue(__instance, true);
                if (modManager != null)
                {
                    Pet.MMchanged.SetValue(modManager, true);
                    Pet.MMlastException.SetValue(modManager, ex);
                }

                throw;
            }
        }
    }

    [HarmonyPatch(
        typeof(ModManager.ModAddressable),
        "TryUnload"
    )]
    public static class MATryUnloadPatch
    {
        [HarmonyPrefix]
        static bool Prefix(ModManager.ModAddressable __instance, ref Task __result)
        {
            Debug.Log("Overriding MA.TryUnload");
            __result = TryUnloadPatched(__instance);
            return false;
        }

        static async Task TryUnloadPatched(ModManager.ModAddressable __instance)
        {
            var locator = (IResourceLocator)Pet.MAlocator.GetValue(__instance);

            if (locator != null) return;
            Addressables.RemoveResourceLocator(locator);
            Pet.MAloaded.SetValue(__instance, false);
            Pet.MAlocator.SetValue(__instance, (IResourceLocator)null);
        }
    }

    [HarmonyPatch(
        typeof(UnityEngine.Object),
        "GetName"
    )]
    public static class ObjectGetNamePatch
    {
        [HarmonyPrefix]
        static bool Prefix(UnityEngine.Object __instance, ref string __result, UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return false;
            }
            return true;
        }
    }
}
