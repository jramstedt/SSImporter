using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.Advertisements;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;
using SSImporter.Resource;
using SystemShock.Resource;

public class SerializeHUD : ScriptableObject {
    private static GraphicsLibrary GraphicsLibrary;
    private static FontLibrary FontLibrary;

    [MenuItem("Tools/Serialize HUD to JSON")]
    static void Serialize() {
        GraphicsLibrary = GraphicsLibrary.GetLibrary();
        FontLibrary = FontLibrary.GetLibrary();

        JSONObject jsonGameObject = serializeGameObject(Selection.activeGameObject);

        File.WriteAllText(Application.dataPath + "/SSImporter/GUI.json", jsonGameObject.Print(true), Encoding.UTF8);
    }

    private static JSONObject serializeGameObject(GameObject gameObject) {
        JSONObject jsonGameObject = new JSONObject(EditorJsonUtility.ToJson(gameObject));
        if (jsonGameObject.HasField(@"GameObject")) {
            Component[] components = gameObject.GetComponents<Component>();
            foreach (Component component in components) {
                JSONObject jsonComponent = new JSONObject(EditorJsonUtility.ToJson(component));
                jsonGameObject.AddField(component.GetType().AssemblyQualifiedName, jsonComponent);

                if (component is Transform) {
                    Transform transform = component as Transform;

                    JSONObject[] children = new JSONObject[transform.childCount];
                    for (int i = 0; i < children.Length; i++)
                        children[i] = serializeGameObject(transform.GetChild(i).gameObject);

                    jsonComponent.AddField(@"Children", new JSONObject(children));
                } else if (component is Image) {
                    Image image = component as Image;

                    if(image.sprite != null) {
                        KeyValuePair<KnownChunkId, int> spriteIdentifier = GraphicsLibrary.GetIdentifiers(image.sprite);

                        JSONObject spriteData = new JSONObject();
                        spriteData.AddField(@"ChunkId", (int)spriteIdentifier.Key);
                        spriteData.AddField(@"Index", spriteIdentifier.Value);

                        jsonComponent.AddField(@"SpriteData", spriteData);
                    }
                } else if(component is Text) {
                    Text text = component as Text;

                    if(text.font != null) {
                        KnownChunkId fontIdentifier = (KnownChunkId)FontLibrary.GetIdentifier(text.font);
                        jsonComponent.AddField(@"FontData", (int)fontIdentifier);
                    }
                }
            }
        }

        return jsonGameObject;
    }

    [MenuItem("Assets/System Shock/14. Generate HUD", false, 1014)]
    static void Deserialize() {
        GraphicsLibrary = GraphicsLibrary.GetLibrary();

        JSONObject jsonGameObject = new JSONObject(File.ReadAllText(Application.dataPath + "/SSImporter/GUI.json", Encoding.UTF8));
        deserializeGameObject(jsonGameObject);
    }

    [MenuItem("Assets/System Shock/14. Generate HUD", true)]
    public static bool Validate() {
        return PlayerPrefs.HasKey(@"SSHOCKRES");
    }

    private static GameObject deserializeGameObject(JSONObject jsonGameObject) {
        GameObject gameObject = new GameObject();
        foreach (string key in jsonGameObject.keys) {
            JSONObject jsonGameObjectData = jsonGameObject.GetField(key);

            if (key == @"GameObject") {
                EditorJsonUtility.FromJsonOverwrite(jsonGameObjectData.Print(), gameObject);
                gameObject.name = jsonGameObjectData.GetField(@"m_Name").str;
                gameObject.SetActive(jsonGameObjectData.GetField(@"m_IsActive").b);
                gameObject.layer = (int)jsonGameObjectData.GetField(@"m_Layer").i;
                GameObjectUtility.SetStaticEditorFlags(gameObject, (StaticEditorFlags)jsonGameObjectData.GetField(@"m_StaticEditorFlags").i);
            } else {
                Component component = gameObject.AddComponent(Type.GetType(key));
                EditorJsonUtility.FromJsonOverwrite(jsonGameObjectData.Print(), component);

                if (component is Transform) {
                    Transform transform = component as Transform;
                    List<JSONObject> childrenData = jsonGameObjectData.GetField(@"Children").list;
                    foreach (JSONObject childData in childrenData) {
                        GameObject child = deserializeGameObject(childData);
                        child.transform.SetParent(transform, false);
                    }
                } else if (component is Image) {
                    Image image = component as Image;

                    if (jsonGameObjectData.HasField(@"SpriteData")) {
                        JSONObject spriteData = jsonGameObjectData.GetField(@"SpriteData");
                        ushort chunkId = (ushort)spriteData.GetField(@"ChunkId").i;
                        uint spriteIndex = (uint)spriteData.GetField(@"Index").i;
                        image.sprite = GraphicsLibrary.GetResource(chunkId)[spriteIndex];
                    }
                } else if (component is Text) {
                    Text text = component as Text;
                    if (jsonGameObjectData.HasField(@"FontData"))
                        text.font = FontLibrary.GetResource((KnownChunkId)jsonGameObjectData.GetField(@"FontData").i);
                }
            }
        }

        return gameObject;
    }
}