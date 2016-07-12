using UnityEngine;
using UnityEditor;
using SystemShock.Resource;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SSImporter {
    public sealed class ArtViewer : EditorWindow {
        private static GUIStyle centeredLabelStyle;
        private static GUIStyle headerLabelStyle;

        [MenuItem("Window/ArtViewer")]
        static void OpenWindow() {
            ArtViewer artViewer = GetWindow<ArtViewer>("Art Viewer");
        }

        const float SpriteMaxWidth = 128f;

        private Vector2 scrollPos;

        public void OnEnable() {
            Selection.selectionChanged += SelectionChanged;
        }

        public void OnDisable() {
            Selection.selectionChanged -= SelectionChanged;
        }

        private void SelectionChanged() {
            Repaint();
        }

        private void OnGUI() {
            if (centeredLabelStyle == null) {
                centeredLabelStyle = new GUIStyle(GUI.skin.label);
                centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (headerLabelStyle == null) {
                headerLabelStyle = new GUIStyle(GUI.skin.label);
                headerLabelStyle.alignment = TextAnchor.MiddleCenter;
                headerLabelStyle.fontSize = 24;
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos, false, false, GUILayout.Width(position.width), GUILayout.Height(position.height));

            UnityEngine.Object[] assets = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets | SelectionMode.DeepAssets);

            foreach (UnityEngine.Object asset in assets) {
                if (asset is SpriteLibrary) {
                    RenderSpriteLibrary(asset as SpriteLibrary);
                } else {
                    SpriteLibrary spriteLibrary = AssetDatabase.LoadAssetAtPath<SpriteLibrary>(AssetDatabase.GetAssetPath(asset));
                    if (spriteLibrary != null)
                        RenderSpriteLibrary(spriteLibrary);
                }
            }

            GUILayout.EndScrollView();
        }

        private void RenderSpriteLibrary(SpriteLibrary spriteLibrary) {
            GUILayout.Label(spriteLibrary.name, headerLabelStyle);

            SpriteAnimation[] animations = spriteLibrary.GetResources().ToArray();
            Texture texture = spriteLibrary.GetAtlas();

            int rowCount = (int)Mathf.Floor(position.width / SpriteMaxWidth);

            int index = 0;
            foreach (SpriteAnimation animation in animations) {
                List<Action> actions = new List<Action>();

                GUILayout.Label(string.Format("{0} ({1} sprites)", index++, animation.Sprites.Length.ToString()), centeredLabelStyle);

                foreach (SpriteDefinition spriteDefinition in animation.Sprites) {
                    float pixelWidth = spriteDefinition.UVRect.width * texture.width;
                    float pixelHeight = spriteDefinition.UVRect.height * texture.height;

                    SpriteDefinition localSpriteDefinition = spriteDefinition;

                    actions.Add(() => {
                        GUILayout.BeginVertical();

                        Rect boxRect = GUILayoutUtility.GetRect(SpriteMaxWidth, SpriteMaxWidth, GUILayout.ExpandWidth(false), GUILayout.ExpandWidth(false));

                        if(boxRect.yMax >= scrollPos.y && boxRect.yMin <= (scrollPos.y + position.height)) {
                            Rect textureRect = new Rect(boxRect.x + (SpriteMaxWidth - pixelWidth) / 2, boxRect.y + (SpriteMaxWidth - pixelHeight) / 2, pixelWidth, pixelHeight);
                            GUI.DrawTextureWithTexCoords(textureRect, texture, localSpriteDefinition.UVRect);
                        }

                        EditorGUILayout.SelectableLabel(localSpriteDefinition.Name, centeredLabelStyle, GUILayout.MinWidth(SpriteMaxWidth));

                        GUILayout.EndVertical();
                    });
                }

                int i = 0;
                GUILayout.BeginHorizontal();
                foreach (Action action in actions) {
                    if (++i >= rowCount) {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        i = 1;
                    }

                    action();
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
