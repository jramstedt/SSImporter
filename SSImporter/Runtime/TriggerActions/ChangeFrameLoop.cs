using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeFrameLoop : Triggerable<ObjectInstance.Trigger.ChangeFrameLoop> {
        public SystemShockObject Target;

        private LevelInfo levelInfo;
        private TextureLibrary animationLibrary;

        // TODO Should this support all animation and screen types like in Decoration material override?

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
            animationLibrary = TextureLibrary.GetLibrary(@"texture.res.anim");
        }

        private void Start() {
            if (ActionData.ObjectId != 0 && !levelInfo.Objects.TryGetValue((ushort)ActionData.ObjectId, out Target))
                Debug.Log("Tried to find object! " + ActionData.ObjectId, this);
        }

        public override void Trigger() {
            if (Target != null) {
                AnimateMaterial animate = Target.GetComponent<AnimateMaterial>();
                AnimateMaterial.AnimationSet animation = animate.GetAnimationSet();

                animation.WrapMode = ActionData.AnimationType == 0 ? AnimateMaterial.WrapMode.Once : AnimateMaterial.WrapMode.ReverseOnce;
                animation.Frames = animationLibrary.GetMaterialAnimation(ActionData.StartFrameIndex, (ushort)animation.Frames.Length);
                animation.CurrentFrame = -1;

                animate.SetAnimation(animation);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}