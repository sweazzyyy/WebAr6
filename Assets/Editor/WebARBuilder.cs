using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using WebXR;

namespace WebARDemo.Editor
{
  /// <summary>
  /// Автоматическая настройка проекта и сборка WebGL.
  /// Меню: WebAR Demo/... или batchmode: -executeMethod WebARDemo.Editor.WebARBuilder.SetupAndBuild
  /// </summary>
  public static class WebARBuilder
  {
    const string ScenePath = "Assets/Scenes/WebARScene.unity";
    const string CameraSetPrefab = "Packages/com.de-panther.webxr-interactions/Runtime/Prefabs/WebXRCameraSet.prefab";
    const string TemplateName = "WebXRFullView2020";

    [MenuItem("WebAR Demo/1. Setup Project (URP, XR, WebGL settings)")]
    public static void SetupProject()
    {
      CopyWebGLTemplates();
      SetupURP();
      SetupXRManagement();
      SetupPlayerSettings(WebGLCompressionFormat.Gzip);
      AssetDatabase.SaveAssets();
      Debug.Log("[WebAR] Project setup done");
    }

    [MenuItem("WebAR Demo/2. Create Scene")]
    public static void CreateScene()
    {
      Directory.CreateDirectory("Assets/Scenes");
      Directory.CreateDirectory("Assets/Prefabs");
      Directory.CreateDirectory("Assets/Materials");

      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

      // Свет
      var lightGo = new GameObject("Directional Light");
      var light = lightGo.AddComponent<Light>();
      light.type = LightType.Directional;
      light.intensity = 1.2f;
      lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

      // Камера WebXR + контроллеры (WebXRController handL/handR) из пакета webxr-interactions
      var camSetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CameraSetPrefab);
      if (camSetAsset == null)
      {
        throw new Exception("WebXRCameraSet prefab not found: " + CameraSetPrefab);
      }
      var camSet = (GameObject)PrefabUtility.InstantiatePrefab(camSetAsset, scene);
      camSet.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
      var cam = camSet.GetComponentInChildren<Camera>(true);
      var controllers = camSet.GetComponentsInChildren<WebXRController>(true);
      Debug.Log($"[WebAR] WebXRCameraSet: camera={cam?.name}, controllers={controllers.Length}");
      AddWebXRCameraRig(cam);

      // Материал и prefab куба
      var shader = Shader.Find("Universal Render Pipeline/Lit");
      var mat = new Material(shader) { name = "CubeMat" };
      mat.SetColor("_BaseColor", new Color(0.1f, 0.6f, 1f));
      AssetDatabase.CreateAsset(mat, "Assets/Materials/CubeMat.mat");

      var cubeTmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cubeTmp.name = "ARCube";
      cubeTmp.transform.localScale = Vector3.one * 0.15f;
      cubeTmp.GetComponent<MeshRenderer>().sharedMaterial = mat;
      var placerTmp = cubeTmp.AddComponent<ViewerSpacePlacer>();
      placerTmp.distance = 0.6f;
      var cubePrefab = PrefabUtility.SaveAsPrefabAsset(cubeTmp, "Assets/Prefabs/ARCube.prefab");
      UnityEngine.Object.DestroyImmediate(cubeTmp);

      // Экземпляр куба в сцене, привязанный к камере зрителя
      var cube = (GameObject)PrefabUtility.InstantiatePrefab(cubePrefab, scene);
      cube.transform.position = new Vector3(0f, 0f, 0.6f);
      var placer = cube.GetComponent<ViewerSpacePlacer>();
      placer.viewer = cam != null ? cam.transform : null;
      EditorUtility.SetDirty(placer);

      EditorSceneManager.SaveScene(scene, ScenePath);
      EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
      AssetDatabase.SaveAssets();
      Debug.Log("[WebAR] Scene created: " + ScenePath);
    }

    /// <summary>
    /// Риг из отдельных камер под WebXRCamera (как в сэмпле Desert-BiRP).
    /// Нужен, потому что XR Display Subsystem отключён: в Unity 6.6 / URP 17.6
    /// XRSystem.CreateDefaultLayout бросает "Unsupported XR layout: 1 render passes"
    /// для моно-AR на телефоне, и камера не рендерится (чёрный экран).
    /// </summary>
    static void AddWebXRCameraRig(Camera main)
    {
      Camera MakeChild(string name, bool ar)
      {
        var go = new GameObject(name);
        go.transform.SetParent(main.transform, false);
        var c = go.AddComponent<Camera>();
        c.enabled = false;
        c.nearClipPlane = main.nearClipPlane;
        c.farClipPlane = main.farClipPlane;
        c.cullingMask = main.cullingMask;
        c.stereoTargetEye = StereoTargetEyeMask.None;
        c.allowMSAA = false;
        c.allowHDR = false;
        if (ar)
        {
          c.clearFlags = CameraClearFlags.SolidColor;
          c.backgroundColor = new Color(0f, 0f, 0f, 0f); // прозрачный фон -> видно камеру телефона
        }
        else
        {
          c.clearFlags = CameraClearFlags.Skybox;
          c.backgroundColor = main.backgroundColor;
        }
        return c;
      }

      main.stereoTargetEye = StereoTargetEyeMask.None;
      var camL = MakeChild("CameraVRL", false);
      var camR = MakeChild("CameraVRR", false);
      var camARL = MakeChild("CameraARL", true);
      var camARR = MakeChild("CameraARR", true);

      var webxrCam = main.GetComponent<WebXRCamera>();
      if (webxrCam == null)
      {
        webxrCam = main.gameObject.AddComponent<WebXRCamera>();
      }
      var so = new SerializedObject(webxrCam);
      so.FindProperty("cameraMain").objectReferenceValue = main;
      so.FindProperty("cameraL").objectReferenceValue = camL;
      so.FindProperty("cameraR").objectReferenceValue = camR;
      so.FindProperty("cameraARL").objectReferenceValue = camARL;
      so.FindProperty("cameraARR").objectReferenceValue = camARR;
      so.ApplyModifiedPropertiesWithoutUndo();
      Debug.Log("[WebAR] WebXRCamera rig added (Main + VRL/VRR/ARL/ARR)");
    }

    [MenuItem("WebAR Demo/3. Build WebGL")]
    public static void BuildMenu()
    {
      Build(WebGLCompressionFormat.Gzip);
    }

    /// <summary>batchmode: -executeMethod WebARDemo.Editor.WebARBuilder.SetupAndBuild [-webarCompression Gzip|Brotli|Disabled]</summary>
    public static void SetupAndBuild()
    {
      var compression = ParseCompressionArg();
      SetupProject();
      CreateScene();
      var ok = Build(compression);
      if (Application.isBatchMode)
      {
        EditorApplication.Exit(ok ? 0 : 1);
      }
    }

    /// <summary>Только сборка (проект уже настроен) — для сравнения размеров с разным сжатием.</summary>
    public static void BuildOnly()
    {
      var ok = Build(ParseCompressionArg());
      if (Application.isBatchMode)
      {
        EditorApplication.Exit(ok ? 0 : 1);
      }
    }

    static WebGLCompressionFormat ParseCompressionArg()
    {
      var args = Environment.GetCommandLineArgs();
      int i = Array.IndexOf(args, "-webarCompression");
      if (i >= 0 && i + 1 < args.Length && Enum.TryParse(args[i + 1], true, out WebGLCompressionFormat f))
      {
        return f;
      }
      return WebGLCompressionFormat.Gzip;
    }

    public static bool Build(WebGLCompressionFormat compression)
    {
      SetupPlayerSettings(compression);
      string outDir = Path.GetFullPath($"Builds/WebGL_{compression}");
      if (Directory.Exists(outDir))
      {
        Directory.Delete(outDir, true);
      }

      var options = new BuildPlayerOptions
      {
        scenes = new[] { ScenePath },
        locationPathName = outDir,
        target = BuildTarget.WebGL,
        targetGroup = BuildTargetGroup.WebGL,
        options = BuildOptions.None
      };
      BuildReport report = BuildPipeline.BuildPlayer(options);
      var s = report.summary;
      Debug.Log($"[WebAR] Build result: {s.result}, time: {s.totalTime}, errors: {s.totalErrors}, size (report): {s.totalSize} bytes");
      if (s.result != BuildResult.Succeeded)
      {
        return false;
      }

      // Отчёт о размерах файлов
      var sb = new StringBuilder();
      sb.AppendLine($"Compression: {compression}; decompressionFallback: {PlayerSettings.WebGL.decompressionFallback}; threads: {PlayerSettings.WebGL.threadsSupport}");
      long total = 0;
      foreach (var f in Directory.GetFiles(outDir, "*", SearchOption.AllDirectories).OrderByDescending(p => new FileInfo(p).Length))
      {
        long len = new FileInfo(f).Length;
        total += len;
        sb.AppendLine($"{len,12:N0}  {f.Substring(outDir.Length + 1)}");
      }
      sb.AppendLine($"{total,12:N0}  TOTAL");
      File.WriteAllText(Path.Combine(outDir, "..", $"size_{compression}.txt"), sb.ToString());
      Debug.Log("[WebAR] Sizes:\n" + sb);
      return true;
    }

    // ---------------------------------------------------------------------

    static void CopyWebGLTemplates()
    {
      var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.de-panther.webxr/package.json");
      if (info == null)
      {
        throw new Exception("com.de-panther.webxr package not found");
      }
      string src = Path.Combine(info.resolvedPath, "Hidden~", "WebGLTemplates");
      string dst = Path.Combine(Application.dataPath, "WebGLTemplates");
      CopyDir(src, dst);
      AssetDatabase.Refresh();
      Debug.Log("[WebAR] WebGL templates copied from " + src);
    }

    static void CopyDir(string src, string dst)
    {
      Directory.CreateDirectory(dst);
      foreach (var f in Directory.GetFiles(src))
      {
        File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
      }
      foreach (var d in Directory.GetDirectories(src))
      {
        CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
      }
    }

    static void SetupURP()
    {
      Directory.CreateDirectory("Assets/Settings");
      const string assetPath = "Assets/Settings/URP_Asset.asset";
      var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
      if (urp == null)
      {
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP_Renderer.asset");
        urp = UniversalRenderPipelineAsset.Create(rendererData);
        // Упрощаем для мобильного WebAR: без HDR, MSAA x1, без теней на дальних дистанциях
        urp.supportsHDR = false;
        urp.msaaSampleCount = 1;
        urp.shadowDistance = 10f;
        AssetDatabase.CreateAsset(urp, assetPath);
      }
      GraphicsSettings.defaultRenderPipeline = urp;
      int current = QualitySettings.GetQualityLevel();
      for (int i = 0; i < QualitySettings.names.Length; i++)
      {
        QualitySettings.SetQualityLevel(i, false);
        QualitySettings.renderPipeline = urp;
      }
      QualitySettings.SetQualityLevel(current, false);
      Debug.Log("[WebAR] URP configured");
    }

    static void SetupXRManagement()
    {
      Directory.CreateDirectory("Assets/XR/Settings");

      // WebXR settings (AR: reference space local, hit-test optional)
      const string webxrSettingsPath = "Assets/XR/Settings/WebXRSettings.asset";
      var webxr = AssetDatabase.LoadAssetAtPath<WebXRSettings>(webxrSettingsPath);
      if (webxr == null)
      {
        webxr = ScriptableObject.CreateInstance<WebXRSettings>();
        AssetDatabase.CreateAsset(webxr, webxrSettingsPath);
      }
      webxr.ARRequiredReferenceSpace = WebXRSettings.ReferenceSpaceTypes.local;
      webxr.AROptionalFeatures = WebXRSettings.ExtraFeatureTypes.hit_test;
      webxr.VRRequiredReferenceSpace = WebXRSettings.ReferenceSpaceTypes.local_floor;
      webxr.VROptionalFeatures = 0;
      webxr.AutoLoadWebXRManager = true;
      webxr.AutoLoadWebXRInputSystem = false;
      // true: обходим баг URP 17.6 (Unity 6.6) с моно-XR layout; рендер через WebXRCamera
      webxr.DisableXRDisplaySubsystem = true;
      EditorUtility.SetDirty(webxr);
      EditorBuildSettings.AddConfigObject("WebXR.Settings", webxr, true);

      // XR Plug-in Management: WebGL -> WebXR Export loader
      const string perBuildPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
      EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perBuild);
      if (perBuild == null)
      {
        perBuild = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(perBuildPath);
      }
      if (perBuild == null)
      {
        perBuild = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
        AssetDatabase.CreateAsset(perBuild, perBuildPath);
      }
      EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuild, true);

      var general = perBuild.SettingsForBuildTarget(BuildTargetGroup.WebGL);
      if (general == null)
      {
        general = ScriptableObject.CreateInstance<XRGeneralSettings>();
        general.name = "WebGL Settings";
        perBuild.SetSettingsForBuildTarget(BuildTargetGroup.WebGL, general);
        AssetDatabase.AddObjectToAsset(general, perBuild);
      }
      if (general.Manager == null)
      {
        var mgr = ScriptableObject.CreateInstance<XRManagerSettings>();
        mgr.name = "WebGL Providers";
        general.Manager = mgr;
        AssetDatabase.AddObjectToAsset(mgr, perBuild);
      }
      general.InitManagerOnStart = true;
      EditorUtility.SetDirty(general);
      EditorUtility.SetDirty(perBuild);

      bool hasLoader = general.Manager.activeLoaders.Any(l => l is WebXRLoader);
      if (!hasLoader)
      {
        hasLoader = XRPackageMetadataStore.AssignLoader(general.Manager, typeof(WebXRLoader).FullName, BuildTargetGroup.WebGL);
      }
      if (!hasLoader)
      {
        // Fallback: создаём loader вручную
        var loader = ScriptableObject.CreateInstance<WebXRLoader>();
        AssetDatabase.CreateAsset(loader, "Assets/XR/WebXRLoader.asset");
        hasLoader = general.Manager.TryAddLoader(loader);
      }
      EditorUtility.SetDirty(general.Manager);
      Debug.Log($"[WebAR] XR Management: WebXR loader assigned = {hasLoader}, loaders = {string.Join(",", general.Manager.activeLoaders.Select(l => l.name))}");
    }

    static void SetupPlayerSettings(WebGLCompressionFormat compression)
    {
      PlayerSettings.companyName = "Student";
      PlayerSettings.productName = "WebAR Viewer-Space Cube";
      PlayerSettings.colorSpace = ColorSpace.Linear; // требование WebXR Export для Unity 6

      // WebGL 2 only (WebXR работает через WebGL-контекст; WebGPU отключаем)
      PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
      PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });

      PlayerSettings.WebGL.template = "PROJECT:" + TemplateName;
      PlayerSettings.WebGL.compressionFormat = compression;
      PlayerSettings.WebGL.decompressionFallback = false; // compression fallback отключён
      PlayerSettings.WebGL.threadsSupport = false;        // multithread отключён
      PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
      PlayerSettings.WebGL.dataCaching = true;
      PlayerSettings.WebGL.nameFilesAsHashes = false;
      PlayerSettings.runInBackground = true;
      PlayerSettings.stripEngineCode = true;
      PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
    }
  }
}
