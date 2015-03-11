using UnityEngine;
using System.Collections;
using System.Reflection;

namespace SystemShock {
    public abstract class AbstractGameController : MonoBehaviour {
        private const string ResourceName = @"GameController";

        protected static class Initializer {
            static Initializer() {
                GameObject prefab = Resources.Load<GameObject>(ResourceName);

                if (!prefab) {
                    Debug.LogError(@"GameController prefab not found in: Resources/" + ResourceName);
                    return;
                }

#if UNITY_EDITOR
                GameObject gameControllers = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
#else
                GameObject gameControllers = Instantiate<GameObject>(prefab);
#endif
                gameControllers.name = ResourceName;
                gameControllers.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                DontDestroyOnLoad(gameControllers);

                foreach (AbstractGameController gameController in gameControllers.GetComponents<AbstractGameController>())
                    typeof(AbstractGameController<>).MakeGenericType(gameController.GetType()).GetField(@"instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(gameController, gameController);
            }

            public static void Initialize() { }
        }

        public static T GetController<T>() where T : AbstractGameController<T> {
            return AbstractGameController<T>.Instance;
        }

        public static void Initialize() { Initializer.Initialize(); }
    }

    public abstract class AbstractGameController<T> : AbstractGameController where T : AbstractGameController<T> {
        private static T instance;
        public static T Instance { get { Initializer.Initialize(); return instance; } private set { instance = value; } }

        public static T GetController() {
            return Instance;
        }
    }
}