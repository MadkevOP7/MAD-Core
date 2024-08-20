using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.VersionControl;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;

[ExecuteAlways]
public class PrefabLightmapData : MonoBehaviour
{
#if UNITY_EDITOR
    static string mTextureDirectory = "Assets/_Core/_Mechanics/LIMENBox/Baked Prefab Lightmap/";
    static int bakeaCounter = 0;
    //During lighting bake, we need to offset each BR instance by 60 units to prevent overlapping error during light bake
    static int mLightingOffset = 60;
    static AssetList mAssetList;
#endif
    [Tooltip("Reassigns shaders when applying the baked lightmaps. Might conflict with some shaders like transparent HDRP.")]
    public bool releaseShaders = true;
    [System.Serializable]
    struct RendererInfo
    {
        public Renderer renderer;
        public int lightmapIndex;
        public Vector4 lightmapOffsetScale;
    }
    [System.Serializable]
    struct LightInfo
    {
        public Light light;
        public int lightmapBaketype;
        public int mixedLightingMode;
    }

    [SerializeField]
    RendererInfo[] m_RendererInfo;
    [SerializeField]
    Texture2D[] m_Lightmaps;
    [SerializeField]
    Texture2D[] m_LightmapsDir;
    [SerializeField]
    Texture2D[] m_ShadowMasks;
    [SerializeField]
    LightInfo[] m_LightInfo;

    #region Internal Validation
    public bool GetRendererInfoSetupValidation()
    {
        if (m_RendererInfo.Length == 0) return false;
        for (int i = 0; i < m_RendererInfo.Length; i++)
        {
            if (m_RendererInfo[i].renderer == null) return false;
        }
        return true;
    }
    public bool GetLightmapSetupValidation()
    {
        if (m_Lightmaps.Length == 0 || m_LightmapsDir.Length == 0) return false;
        for (int i = 0; i < m_Lightmaps.Length; i++)
        {
            if (m_Lightmaps[i] == null) return false;
        }

        for (int i = 0; i < m_LightmapsDir.Length; i++)
        {
            if (m_LightmapsDir[i] == null) return false;
        }
        return true;
    }

    public bool GetShadowMapSetupValidation()
    {
        //Currently we don't have shadow mask it seems from prefab bake
        //if (m_ShadowMasks.Length == 0) return false;
        //for (int i = 0; i < m_ShadowMasks.Length; i++)
        //{
        //    if (m_ShadowMasks[i] == null) return false;
        //}
        return true;
    }
    public bool GetLightInfoSetupValidation()
    {
        if (m_LightInfo.Length == 0) return false;
        for (int i = 0; i < m_LightInfo.Length; i++)
        {
            if (m_LightInfo[i].light == null) return false;
        }
        return true;
    }

    public bool GetSetupValidation()
    {
        return GetRendererInfoSetupValidation()
            && GetLightmapSetupValidation() && GetShadowMapSetupValidation()
            && GetLightInfoSetupValidation();
    }
    #endregion
    void Awake()
    {
        Init();
    }

    void Init()
    {
        if (m_RendererInfo == null || m_RendererInfo.Length == 0)
            return;

        var lightmaps = LightmapSettings.lightmaps;
        int[] offsetsindexes = new int[m_Lightmaps.Length];
        int counttotal = lightmaps.Length;
        List<LightmapData> combinedLightmaps = new List<LightmapData>();

        for (int i = 0; i < m_Lightmaps.Length; i++)
        {
            bool exists = false;
            for (int j = 0; j < lightmaps.Length; j++)
            {

                if (m_Lightmaps[i] == lightmaps[j].lightmapColor)
                {
                    exists = true;
                    offsetsindexes[i] = j;

                }

            }
            if (!exists)
            {
                offsetsindexes[i] = counttotal;
                var newlightmapdata = new LightmapData
                {
                    lightmapColor = m_Lightmaps[i],
                    lightmapDir = m_LightmapsDir.Length == m_Lightmaps.Length ? m_LightmapsDir[i] : default(Texture2D),
                    shadowMask = m_ShadowMasks.Length == m_Lightmaps.Length ? m_ShadowMasks[i] : default(Texture2D),
                };

                combinedLightmaps.Add(newlightmapdata);

                counttotal += 1;


            }

        }

        var combinedLightmaps2 = new LightmapData[counttotal];

        lightmaps.CopyTo(combinedLightmaps2, 0);
        combinedLightmaps.ToArray().CopyTo(combinedLightmaps2, lightmaps.Length);

        bool directional = true;

        foreach (Texture2D t in m_LightmapsDir)
        {
            if (t == null)
            {
                directional = false;
                break;
            }
        }

        LightmapSettings.lightmapsMode = (m_LightmapsDir.Length == m_Lightmaps.Length && directional) ? LightmapsMode.CombinedDirectional : LightmapsMode.NonDirectional;
        ApplyRendererInfo(m_RendererInfo, offsetsindexes, m_LightInfo);
        LightmapSettings.lightmaps = combinedLightmaps2;
    }

    void OnEnable()
    {

        SceneManager.sceneLoaded += OnSceneLoaded;

    }

    // called second
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Init();
    }

    // called when the game is terminated
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }



    void ApplyRendererInfo(RendererInfo[] infos, int[] lightmapOffsetIndex, LightInfo[] lightsInfo)
    {
        for (int i = 0; i < infos.Length; i++)
        {
            var info = infos[i];

            info.renderer.lightmapIndex = lightmapOffsetIndex[info.lightmapIndex];
            info.renderer.lightmapScaleOffset = info.lightmapOffsetScale;

            if (releaseShaders)
            {
                // You have to release shaders.
                Material[] mat = info.renderer.sharedMaterials;
                for (int j = 0; j < mat.Length; j++)
                {
                    if (mat[j] != null && Shader.Find(mat[j].shader.name) != null)
                    {
                        mat[j].shader = Shader.Find(mat[j].shader.name);
                    }

                }
            }

        }

        for (int i = 0; i < lightsInfo.Length; i++)
        {
            LightBakingOutput bakingOutput = new LightBakingOutput();
            bakingOutput.isBaked = true;
            bakingOutput.lightmapBakeType = (LightmapBakeType)lightsInfo[i].lightmapBaketype;
            bakingOutput.mixedLightingMode = (MixedLightingMode)lightsInfo[i].mixedLightingMode;

            lightsInfo[i].light.bakingOutput = bakingOutput;

        }


    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("MADKEV/Bake Prefab Lightmaps")]
    static void GenerateLightmapInfo()
    {
        if (UnityEditor.Lightmapping.giWorkflowMode != UnityEditor.Lightmapping.GIWorkflowMode.OnDemand)
        {
            Debug.LogError("ExtractLightmapData requires that you have baked you lightmaps and Auto mode is disabled.");
            return;
        }
        if (!SceneManager.GetActiveScene().name.ToLower().Contains("test"))
        {
            Debug.LogError("Please switch to a testing scene to bake. Aborted due to possible open production scene!");
            return;
        }

        //Pre-validate number of prefabs in testing scene
        if (FindObjectsOfType<PrefabLightmapData>(true).Length == 0)
        {
            Debug.LogError("Aborted due to 0 prefabs to bake in scene!");
            return;
        }

        PrefabLightmapData[] prefabs = FindObjectsOfType<PrefabLightmapData>(true);
        foreach (var instance in prefabs)
        {
            if (!instance.gameObject.activeInHierarchy && !instance.gameObject.name.ToLower().Contains("backup"))
            {
                Debug.LogError("Bake Aborted: Please make sure all prefabs are enabled and bake. Due to editor loop, they must be enabled for data to apply correctly.");
                EditorUtility.DisplayDialog("Bake Aborted", "Please make sure all prefabs are enabled and bake. Due to editor loop, they must be enabled for data to apply correctly.", "Ok");

                return;
            }
        }

        bakeaCounter = 0;
        UnityEditor.Lightmapping.Bake();
        mAssetList = new AssetList();

        int currLightingOffset = 0;
        foreach (var instance in prefabs)
        {
            instance.transform.position = Vector3.zero;
            instance.transform.position = new Vector3(0, 0, currLightingOffset);
            //Skip backups
            if (instance.gameObject.name.ToLower().Contains("backup")) continue;
            var gameObject = instance.gameObject;
            var rendererInfos = new List<RendererInfo>();
            var lightmaps = new List<Texture2D>();
            var lightmapsDir = new List<Texture2D>();
            var shadowMasks = new List<Texture2D>();
            var lightsInfos = new List<LightInfo>();

            GenerateLightmapInfo(gameObject, rendererInfos, lightmaps, lightmapsDir, shadowMasks, lightsInfos);

            instance.m_RendererInfo = rendererInfos.ToArray();
            instance.m_Lightmaps = lightmaps.ToArray();
            instance.m_LightmapsDir = lightmapsDir.ToArray();
            instance.m_LightInfo = lightsInfos.ToArray();
            instance.m_ShadowMasks = shadowMasks.ToArray();
#if UNITY_2018_3_OR_NEWER
            var targetPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance.gameObject) as GameObject;
            if (targetPrefab != null)
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(instance.gameObject);// 根结点
                //如果当前预制体是是某个嵌套预制体的一部分（IsPartOfPrefabInstance）
                if (root != null)
                {
                    GameObject rootPrefab = PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                    string rootPath = AssetDatabase.GetAssetPath(rootPrefab);
                    //打开根部预制体
                    PrefabUtility.UnpackPrefabInstanceAndReturnNewOutermostRoots(root, PrefabUnpackMode.OutermostRoot);
                    try
                    {
                        //Apply各个子预制体的改变
                        PrefabUtility.ApplyPrefabInstance(instance.gameObject, InteractionMode.AutomatedAction);
                    }
                    catch { }
                    finally
                    {
                        //重新更新根预制体
                        PrefabUtility.SaveAsPrefabAssetAndConnect(root, rootPath, InteractionMode.AutomatedAction);
                    }
                }
                else
                {
                    PrefabUtility.ApplyPrefabInstance(instance.gameObject, InteractionMode.AutomatedAction);
                }
            }
#else
            var targetPrefab = UnityEditor.PrefabUtility.GetPrefabParent(gameObject) as GameObject;
            if (targetPrefab != null)
            {
                //UnityEditor.Prefab
                UnityEditor.PrefabUtility.ReplacePrefab(gameObject, targetPrefab);
            }
#endif
            currLightingOffset += mLightingOffset;
        }
        foreach (var t in mAssetList)
        {
            Debug.Log(t.name + " Path: " + t.path);
        }
        Task task = Provider.Add(mAssetList, true);
        task.Wait();
        task = Provider.Checkout(mAssetList, CheckoutMode.Both);
        task.Wait();
        task.SetCompletionAction(CompletionAction.UpdatePendingWindow);

        //Move all instances back to origin
        foreach(var instance in prefabs)
        {
            instance.transform.position = Vector3.zero;
        }
        Debug.Log("Prefab Lightmap Complete! Baked count: " + bakeaCounter);
        EditorUtility.DisplayDialog("Prefab Lightmap Complete", "Successfully baked lightmap for " + bakeaCounter + " prefabs.\n [IMPORTANT] Please make sure to select them all and in inspector, select override original to make the change affect the stored prefabs!", "I understand");
    }
    static void MoveToDirectory(Object asset)
    {
        string extension = Path.GetExtension(AssetDatabase.GetAssetPath(asset));
        FileUtil.MoveFileOrDirectory(AssetDatabase.GetAssetPath(asset), mTextureDirectory + asset.name + extension);
        FileUtil.MoveFileOrDirectory(AssetDatabase.GetAssetPath(asset) + ".meta", mTextureDirectory + asset.name + extension + ".meta");
        AssetDatabase.Refresh();
        mAssetList.Add(Provider.GetAssetByPath(mTextureDirectory + asset.name + extension));

    }
    static void GenerateLightmapInfo(GameObject root, List<RendererInfo> rendererInfos, List<Texture2D> lightmaps, List<Texture2D> lightmapsDir, List<Texture2D> shadowMasks, List<LightInfo> lightsInfo)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer.lightmapIndex != -1)
            {
                RendererInfo info = new RendererInfo();
                info.renderer = renderer;

                if (renderer.lightmapScaleOffset != Vector4.zero)
                {
                    //1ibrium's pointed out this issue : https://docs.unity3d.com/ScriptReference/Renderer-lightmapIndex.html
                    if (renderer.lightmapIndex < 0 || renderer.lightmapIndex == 0xFFFE) continue;
                    info.lightmapOffsetScale = renderer.lightmapScaleOffset;

                    Texture2D lightmap = LightmapSettings.lightmaps[renderer.lightmapIndex].lightmapColor;
                    Texture2D lightmapDir = LightmapSettings.lightmaps[renderer.lightmapIndex].lightmapDir;
                    Texture2D shadowMask = LightmapSettings.lightmaps[renderer.lightmapIndex].shadowMask;

                    info.lightmapIndex = lightmaps.IndexOf(lightmap);
                    if (info.lightmapIndex == -1)
                    {
                        info.lightmapIndex = lightmaps.Count;
                        lightmaps.Add(lightmap);
                        lightmapsDir.Add(lightmapDir);
                        shadowMasks.Add(shadowMask);

#if UNITY_EDITOR
                        //Move directory
                        MoveToDirectory(lightmap);
                        MoveToDirectory(lightmapDir);
                        if (shadowMask != null)
                        {
                            MoveToDirectory(shadowMask);
                        }
                        bakeaCounter++;
#endif
                    }

                    rendererInfos.Add(info);
                }

            }
        }

        var lights = root.GetComponentsInChildren<Light>(true);

        foreach (Light l in lights)
        {
            LightInfo lightInfo = new LightInfo();
            lightInfo.light = l;
            lightInfo.lightmapBaketype = (int)l.lightmapBakeType;
#if UNITY_2020_1_OR_NEWER
            lightInfo.mixedLightingMode = (int)UnityEditor.Lightmapping.lightingSettings.mixedBakeMode;
#elif UNITY_2018_1_OR_NEWER
            lightInfo.mixedLightingMode = (int)UnityEditor.LightmapEditorSettings.mixedBakeMode;
#else
            lightInfo.mixedLightingMode = (int)l.bakingOutput.lightmapBakeType;            
#endif
            lightsInfo.Add(lightInfo);

        }
    }
#endif

}
