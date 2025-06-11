using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class MaskMapGenerator : EditorWindow
{
    private Texture2D metallicMap;
    private Texture2D ambientOcclusionMap;
    private Texture2D detailMaskMap;
    private Texture2D smoothnessMap;
    private bool isRoughnessMap = false;

    [MenuItem("Tools/Mask Map Generator")]
    public static void ShowWindow()
    {
        GetWindow<MaskMapGenerator>("Mask Map Generator");
    }

    void OnGUI()
    {
        metallicMap = (Texture2D)EditorGUILayout.ObjectField("Metallic (R)", metallicMap, typeof(Texture2D), false);
        ambientOcclusionMap = (Texture2D)EditorGUILayout.ObjectField("Ambient Occlusion (G)", ambientOcclusionMap, typeof(Texture2D), false);
        detailMaskMap = (Texture2D)EditorGUILayout.ObjectField("Detail Mask (B)", detailMaskMap, typeof(Texture2D), false);
        smoothnessMap = (Texture2D)EditorGUILayout.ObjectField(isRoughnessMap ? "Roughness (A)" : "Smoothness (A)", smoothnessMap, typeof(Texture2D), false);
        isRoughnessMap = EditorGUILayout.Toggle("Use Roughness Map", isRoughnessMap);
        GUILayout.Space(20);

        if (GUILayout.Button("Generate Mask Map"))
        {
            GenerateMaskMap();
        }
    }
    void GenerateMaskMap()
    {
        if (metallicMap == null && ambientOcclusionMap == null && detailMaskMap == null && smoothnessMap == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign at least one texture.", "OK");
            return;
        }

        Texture2D referenceTexture = metallicMap ?? ambientOcclusionMap ?? detailMaskMap ?? smoothnessMap;
        int width = referenceTexture.width;
        int height = referenceTexture.height;
        List<TextureImporter> importersToRevert = new List<TextureImporter>();
        
        try
        {
            EditorUtility.DisplayProgressBar("Processing Textures", "Preparing...", 0f);
            Color[] metallicPixels = metallicMap ? PrepareAndGetPixels(metallicMap, width, height, importersToRevert) : null;
            EditorUtility.DisplayProgressBar("Processing Textures", "Reading Metallic Map...", 0.2f);

            Color[] aoPixels = ambientOcclusionMap ? PrepareAndGetPixels(ambientOcclusionMap, width, height, importersToRevert) : null;
            EditorUtility.DisplayProgressBar("Processing Textures", "Reading AO Map...", 0.4f);

            Color[] detailPixels = detailMaskMap ? PrepareAndGetPixels(detailMaskMap, width, height, importersToRevert) : null;
            EditorUtility.DisplayProgressBar("Processing Textures", "Reading Detail Map...", 0.6f);

            Color[] smoothnessPixels = smoothnessMap ? PrepareAndGetPixels(smoothnessMap, width, height, importersToRevert) : null;
            EditorUtility.DisplayProgressBar("Processing Textures", "Reading Smoothness/Roughness Map...", 0.8f);

            Texture2D maskMap = new Texture2D(width, height, TextureFormat.RGBA32, true);
            Color[] finalPixels = new Color[width * height];

            for (int i = 0; i < finalPixels.Length; i++)
            {
                float r = metallicPixels != null ? metallicPixels[i].r : 0f;
                float g = aoPixels != null ? aoPixels[i].r : 1f;
                float b = detailPixels != null ? detailPixels[i].r : 0f;
                float a = smoothnessPixels != null ? smoothnessPixels[i].r : 1f;

                if (isRoughnessMap && smoothnessMap != null)
                {
                    a = 1.0f - a;
                }
                finalPixels[i] = new Color(r, g, b, a);
            }

            maskMap.SetPixels(finalPixels);
            maskMap.Apply();

            string path = EditorUtility.SaveFilePanelInProject("Save Mask Map", "MaskMap", "png", "Choose where to save your Mask Map");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, maskMap.EncodeToPNG());
                AssetDatabase.Refresh();

                TextureImporter resultImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (resultImporter != null)
                {
                    resultImporter.sRGBTexture = false;
                    resultImporter.SaveAndReimport();
                }
            }
        }
        finally
        {
            foreach (var importer in importersToRevert)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            EditorUtility.ClearProgressBar();
        }
    }
    private Color[] PrepareAndGetPixels(Texture2D texture, int expectedWidth, int expectedHeight, List<TextureImporter> importersToRevert)
    {
        if (texture.width != expectedWidth || texture.height != expectedHeight)
        {
            Debug.LogWarning($"Texture '{texture.name}' dimensions ({texture.width}x{texture.height}) do not match the expected size ({expectedWidth}x{expectedHeight}). The result may be incorrect.");
        }

        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null) return null;

        bool needsReimport = false;

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            needsReimport = true;
        }

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            if (!importersToRevert.Contains(importer))
            {
                importersToRevert.Add(importer);
            }
            needsReimport = true;
        }

        if (needsReimport)
        {
            importer.SaveAndReimport();
        }

        return texture.GetPixels();
    }
}