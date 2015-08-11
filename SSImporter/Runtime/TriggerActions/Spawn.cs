using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Spawn : Triggerable<ObjectInstance.Trigger.Spawn> {
        public SystemShockObject FirstCorner;
        public SystemShockObject SecondCorner;

        private LevelInfo levelInfo;
        private ObjectFactory objectFactory;
        private ObjectPropertyLibrary objectPropertyLibrary;

        protected override void Awake() {
            base.Awake();

            objectFactory = ObjectFactory.GetController();
            levelInfo = objectFactory.LevelInfo;
            objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary(@"objprop.dat");
        }

        private void Start() {
            if (ActionData.Corner1ObjectId != 0 && !levelInfo.Objects.TryGetValue(ActionData.Corner1ObjectId, out FirstCorner))
                Debug.Log("Tried to find object! " + ActionData.Corner1ObjectId, this);

            if (ActionData.Corner2ObjectId != 0 && !levelInfo.Objects.TryGetValue(ActionData.Corner2ObjectId, out SecondCorner))
                Debug.Log("Tried to find object! " + ActionData.Corner2ObjectId, this);
        }

        public override void Trigger() {
            //Debug.Log("SPAWN! " + FirstCorner + " " + SecondCorner, this);

            if (FirstCorner == null || SecondCorner == null)
                return;

            Bounds bounds = new Bounds(FirstCorner.transform.position, Vector3.zero);
            bounds.Encapsulate(SecondCorner.transform.position);

            uint combinedId = ActionData.ObjectId;
            uint Class = (combinedId >> 16) & 0xFF;
            uint Subclass = (combinedId >> 8) & 0xFF;
            uint Type = combinedId & 0xFF;

            ObjectData objectData = objectPropertyLibrary.GetObject<ObjectData>(combinedId);
            BaseProperties baseProperties = objectData.Base;

            for (int i = 0; i < ActionData.Amount; ++i) {
                ObjectInstance objectInstance = new ObjectInstance();
                objectInstance.InUse = 1;
                objectInstance.Class = (ObjectClass)Class;
                objectInstance.SubClass = (byte)Subclass;
                objectInstance.Type = (byte)Type;
                objectInstance.State = (byte)ActionData.State;

                // TODO Spawn only on valid location
                objectInstance.X = (ushort)(Random.Range(bounds.min.x, bounds.max.x) * 256f);
                objectInstance.Y = (ushort)(Random.Range(bounds.min.z, bounds.max.z) * 256f);
                objectInstance.Z = (byte)(Random.Range(bounds.min.y, bounds.max.y) / levelInfo.HeightFactor); // TODO get floor height!
                objectInstance.Hitpoints = baseProperties.Hitpoints;

                IClassData classData = levelInfo.ClassDataTemplates[(int)Class].Clone();
                classData.ObjectId = objectFactory.NextFreeId();

                objectFactory.Instantiate(objectInstance, classData);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (FirstCorner == null || SecondCorner == null)
                return;

            Bounds bounds = new Bounds(FirstCorner.transform.position, Vector3.zero);
            bounds.Encapsulate(SecondCorner.transform.position);

            Color color = Color.red;
            color.a = 0.1f;
            Gizmos.color = color;

            Gizmos.DrawCube(bounds.center, bounds.size);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}