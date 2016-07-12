using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeFrameLoop : TriggerAction<ObjectInstance.Trigger.ChangeFrameLoop> {
        private TextureLibrary textureLibrary;

        // TODO Should this support all animation and screen types like in Decoration material override?

        protected override void Awake() {
            base.Awake();

            textureLibrary = TextureLibrary.GetLibrary();
        }

        protected override void DoAct() {
            SystemShockObject Target1 = ObjectFactory.Get(ActionData.ObjectId1);
            if (Target1 != null)
                ChangeAnimation(Target1);

            SystemShockObject Target2 = ObjectFactory.Get(ActionData.ObjectId2);
            if (Target2 != null)
                ChangeAnimation(Target2);
        }

        private void ChangeAnimation(SystemShockObject target) {
            AnimateMaterial animate = target.GetComponent<AnimateMaterial>();
            AnimateMaterial.AnimationSet animation = animate.GetAnimationSet();

            animation.WrapMode = ActionData.AnimationType == 0 ? AnimateMaterial.WrapMode.Once : AnimateMaterial.WrapMode.ReverseOnce;
            animation.Frames = textureLibrary.GetAnimationTextures(ActionData.StartFrameIndex, (ushort)animation.Frames.Length);

            animate.SetAnimation(animation);
            animate.Reset();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            SystemShockObject Target1 = ObjectFactory.Get(ActionData.ObjectId1);
            SystemShockObject Target2 = ObjectFactory.Get(ActionData.ObjectId2);

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