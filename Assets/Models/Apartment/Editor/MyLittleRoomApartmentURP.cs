// Creates only new URP assets and a new prefab. Does not modify scenes or the FBX importer.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MyLittleRoomApartmentURP
{
    const string Root = "Assets/Models/Apartment";
    const string Tex = "Assets/Textures/Apartment";
    const string PrefabPath = Root + "/MyLittleRoom_Apartment_URP.prefab";
    [Serializable] class MatInfo {
        public string name; public float[] @base; public float metallic, roughness, alpha;
        public float[] emission; public float emissionStrength; public string baseTexture, normalTexture;
    }
    [Serializable] class Manifest { public MatInfo[] materials; }
    static Color ColorOf(float[] v, float alpha = 1f) {
        return v != null && v.Length >= 3 ? new Color(v[0],v[1],v[2],alpha) : Color.white;
    }
    [MenuItem("My Little Room/Create URP Apartment Prefab")]
    public static void Build() {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) { Debug.LogError("URP/Lit unavailable. Enable the Universal Render Pipeline first."); return; }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/MyLittleRoom_Apartment.fbx");
        if (!model) { Debug.LogError("Wait until the FBX import completes."); return; }
        if (File.Exists(PrefabPath)) { Debug.LogError("Prefab already exists; refusing to overwrite."); return; }
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/Apartment_Import_Manifest.json"));
        string matFolder = Root + "/URP_Materials";
        foreach(var info in manifest.materials)
            if (File.Exists(matFolder + "/" + info.name + ".mat")) {
                Debug.LogError("Material already exists; refusing to overwrite: " + info.name); return;
            }
        if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(Root,"URP_Materials");
        var materials = new Dictionary<string,Material>();
        foreach(var info in manifest.materials) {
            if (materials.ContainsKey(info.name)) continue;
            var m = new Material(shader) { name = info.name };
            m.SetColor("_BaseColor", ColorOf(info.@base, info.alpha));
            m.SetFloat("_Metallic",info.metallic);
            m.SetFloat("_Smoothness",1f-info.roughness);
            if (!string.IsNullOrEmpty(info.baseTexture)) {
                var t=AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/" + info.baseTexture);
                m.SetTexture("_BaseMap",t); m.SetColor("_BaseColor",Color.white);
            }
            if (!string.IsNullOrEmpty(info.normalTexture)) {
                string path=Tex + "/" + info.normalTexture;
                var ti=AssetImporter.GetAtPath(path) as TextureImporter;
                if(ti != null) { ti.textureType=TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                m.EnableKeyword("_NORMALMAP");
            }
            if(info.alpha < .999f) {
                m.SetFloat("_Surface",1); m.SetFloat("_Blend",0);
                m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite",0); m.SetFloat("_Cull",0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue=3000;
                m.SetOverrideTag("RenderType","Transparent");
            }
            if(info.emissionStrength > 0f) {
                m.SetColor("_EmissionColor",ColorOf(info.emission)*info.emissionStrength);
                m.EnableKeyword("_EMISSION");
            }
            AssetDatabase.CreateAsset(m,matFolder + "/" + info.name + ".mat");
            materials.Add(info.name,m);
        }
        var instance = UnityEngine.Object.Instantiate(model);
        instance.name = "MyLittleRoom_Apartment";
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)) {
            var list=renderer.sharedMaterials;
            for(int i=0;i<list.Length;i++) {
                if(list[i] == null) continue;
                string n=list[i].name.Replace(" (Instance)","");
                if(materials.TryGetValue(n,out var replacement)) list[i]=replacement;
                else Debug.LogWarning("Unmapped material: " + n,renderer);
            }
            renderer.sharedMaterials=list;
        }
        PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log("Created URP materials and prefab. No existing scene was changed. Drag the new prefab into your scene.");
    }
}
