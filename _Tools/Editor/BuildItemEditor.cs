using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(BuildItem))]
public class BuildItemEditor : Editor
{
    static string proj_path = "Assets/ItemPreviews/";
    static int size = 512;
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        BuildItem item = (BuildItem)target;
        if (GUILayout.Button("Generate Preview Image"))
        {
            RuntimePreviewGenerator.MarkTextureNonReadable = false;
            RuntimePreviewGenerator.RenderSupersampling = 1;
            RuntimePreviewGenerator.BackgroundColor = Color.clear;
            RuntimePreviewGenerator.OrthographicMode = true;
            RuntimePreviewGenerator.PreviewDirection = new Vector3(1, -1, -1);
            Texture2D texture = RuntimePreviewGenerator.GenerateModelPreview(item.transform, size, size, true, true);
            byte[] bytes;
            bytes = texture.EncodeToPNG();
            string path = proj_path + item.m_name + ".png";
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);

            var tImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (tImporter != null)
            {
                tImporter.textureType = TextureImporterType.Sprite;

                tImporter.isReadable = true;

                AssetDatabase.ImportAsset(path);
                AssetDatabase.Refresh();
            }

            item.image = (Sprite) AssetDatabase.LoadAssetAtPath(path, typeof(Sprite)) as Sprite;
            Debug.Log("Completed!");
        }
    }



}
