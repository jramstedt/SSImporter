using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.Triggers {
    [ExecuteInEditMode]
    public class DeathWatch : MonoBehaviour {
        private InstanceObjects.Trigger trigger;
        private Triggerable triggerable;
        private SystemShockObject watchedObject;

        private bool triggered;

        private void Awake() {
            trigger = GetComponent<InstanceObjects.Trigger>();
            triggerable = GetComponent<Triggerable>();

            LevelInfo levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            // TODO Get objects to watch

            uint combinedId = (uint)(trigger.ClassData.ConditionValue << 16) | (uint)trigger.ClassData.ConditionVariable;
            bool IsId = ((combinedId >> 24) & 0xFF) != 0;
            uint Class = (combinedId >> 16) & 0xFF;
            uint Subclass = (combinedId >> 8) & 0xFF;
            uint Type = combinedId & 0xFF;

            ushort objectIndex = (ushort)(combinedId & 0x0FFF);

            if (IsId) {
                levelInfo.Objects.TryGetValue(objectIndex, out watchedObject);
                Debug.LogFormat(watchedObject, "DeathWatch {0} / {1}", objectIndex, watchedObject);
            } else {
                Debug.LogFormat(gameObject, "DeathWatch {0} / {1} {2} {3}", combinedId, Class, Subclass, Type);
            }

            // TODO Add destroyed event to object factory?

            triggered = watchedObject != null;
        }

        private void Update() {
            if (!triggered && (watchedObject == null || !watchedObject.isActiveAndEnabled))
                OnObjectDestroyed();
        }

        private void OnObjectDestroyed() {
            triggered = true;

            if (triggerable != null)
                triggerable.Trigger();
        }
    }
}