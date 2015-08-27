using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeFrameLoop : Triggerable<ObjectInstance.Trigger.ChangeFrameLoop> {
        public SystemShockObject Target1;
        public SystemShockObject Target2;

        private LevelInfo levelInfo;
        private TextureLibrary animationLibrary;

        // TODO Should this support all animation and screen types like in Decoration material override?

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
            animationLibrary = TextureLibrary.GetLibrary(@"texture.res.anim");
        }

        private void Start() {
            if (ActionData.ObjectId1 != 0 && !levelInfo.Objects.TryGetValue((ushort)ActionData.ObjectId1, out Target1))
                Debug.Log("Tried to find object! " + ActionData.ObjectId1, this);

            if (ActionData.ObjectId2 != 0 && !levelInfo.Objects.TryGetValue((ushort)ActionData.ObjectId2, out Target2))
                Debug.Log("Tried to find object! " + ActionData.ObjectId2, this);
        }

        public override void Trigger() {
            if (!CanActivate)
                return;

            if (Target1 != null)
                ChangeAnimation(Target1);

            if (Target2 != null)
                ChangeAnimation(Target2);
        }

        private void ChangeAnimation(SystemShockObject target) {
            AnimateMaterial animate = target.GetComponent<AnimateMaterial>();
            AnimateMaterial.AnimationSet animation = animate.GetAnimationSet();

            animation.WrapMode = ActionData.AnimationType == 0 ? AnimateMaterial.WrapMode.Once : AnimateMaterial.WrapMode.ReverseOnce;
            animation.Frames = animationLibrary.GetMaterialAnimation(ActionData.StartFrameIndex, (ushort)animation.Frames.Length);

            animate.SetAnimation(animation);
            animate.Reset();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}