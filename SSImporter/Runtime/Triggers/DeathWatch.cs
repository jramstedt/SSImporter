using UnityEngine;

using System;
using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.Triggers {
    [ExecuteInEditMode]
    public class DeathWatch : MonoBehaviour, IActionPermission {
        private ObjectInstance.Trigger trigger;
        private TriggerAction triggerable;
        public SystemShockObject watchedObject;

        private bool triggered;

        private void Awake() {
            trigger = GetComponent<InstanceObjects.Trigger>().ClassData;
            triggerable = GetComponent<TriggerAction>();

            LevelInfo levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            // TODO Get objects to watch

            uint combinedId = (uint)(trigger.ConditionValue << 16) | (uint)trigger.ConditionVariable;
            bool IsId = ((combinedId >> 24) & 0xFF) != 0;
            uint Class = (combinedId >> 16) & 0xFF;
            uint Subclass = (combinedId >> 8) & 0xFF;
            uint Type = combinedId & 0xFF;

            ushort objectIndex = (ushort)(combinedId & 0x0FFF);

            if (IsId) {
                levelInfo.Objects.TryGetValue(objectIndex, out watchedObject);
                Debug.LogFormat(watchedObject, "DeathWatch {0} / {1}", objectIndex, watchedObject);

                this.name = @"DeathWatch " + watchedObject.name;
            } else {
                ObjectPropertyLibrary objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary(@"objprop.dat");

                Debug.LogFormat(gameObject, "DeathWatch {0} / {1} {2} {3}", combinedId, Class, Subclass, Type);

                ObjectData objectData = objectPropertyLibrary.GetObject<ObjectData>(combinedId);
                this.name = string.Format(@"DeathWatch {0}:{1}:{2} {3}", (ObjectClass)Class, Subclass, Type, objectData.FullName);
            }

            // TODO Add destroyed event to object factory?

            triggered = watchedObject == null;
        }

        private void Update() {
            if (!triggered && (watchedObject == null || !watchedObject.isActiveAndEnabled))
                OnObjectDestroyed();
        }

        private void OnObjectDestroyed() {
            triggered = true;

            if (triggerable != null)
                triggerable.Act();
        }

        public bool CanAct() { return true; }
    }
}