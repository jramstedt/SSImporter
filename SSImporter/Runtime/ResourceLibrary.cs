using UnityEngine;
using System.Collections.Generic;
using System;

namespace SystemShock.Resource {
    public class ResourceLibrary : AbstractGameController<ResourceLibrary> {
        [SerializeField]
        private FontLibrary fontLibrary;
        public FontLibrary FontLibrary { get { return fontLibrary; } set { fontLibrary = value; Save(); } }

        [SerializeField]
        private ModelLibrary modelLibrary;
        public ModelLibrary ModelLibrary { get { return modelLibrary; } set { modelLibrary = value; Save(); } }

        [SerializeField]
        private PaletteLibrary paletteLibrary;
        public PaletteLibrary PaletteLibrary { get { return paletteLibrary; } set { paletteLibrary = value; Save(); } }

        [SerializeField]
        private SoundLibrary soundLibrary;
        public SoundLibrary SoundLibrary { get { return soundLibrary; } set { soundLibrary = value; Save(); } }

        [SerializeField]
        private SpriteLibrary spriteLibrary;
        public SpriteLibrary SpriteLibrary { get { return spriteLibrary; } set { spriteLibrary = value; Save(); } }

        [SerializeField]
        private StringLibrary stringLibrary;
        public StringLibrary StringLibrary { get { return stringLibrary; } set { stringLibrary = value; Save(); } }

        [SerializeField]
        private TextureLibrary textureLibrary;
        public TextureLibrary TextureLibrary { get { return textureLibrary; } set { textureLibrary = value; Save(); } }

        [SerializeField]
        private TexturePropertiesLibrary texturePropertiesLibrary;
        public TexturePropertiesLibrary TexturePropertiesLibrary { get { return texturePropertiesLibrary; } set { texturePropertiesLibrary = value; Save(); } }

        [SerializeField]
        private ObjectPropertyLibrary objectPropertyLibrary;
        public ObjectPropertyLibrary ObjectPropertyLibrary { get { return objectPropertyLibrary; } set { objectPropertyLibrary = value; Save(); } }

        [SerializeField]
        private PrefabLibrary prefabLibrary;
        public PrefabLibrary PrefabLibrary { get { return prefabLibrary; } set { prefabLibrary = value; Save(); } }

        public void Save() {
#if UNITY_EDITOR
            UnityEditor.PrefabUtility.ReplacePrefab(gameObject, UnityEditor.PrefabUtility.GetPrefabParent(gameObject), UnityEditor.ReplacePrefabOptions.ConnectToPrefab);
#endif
        }
    }
}