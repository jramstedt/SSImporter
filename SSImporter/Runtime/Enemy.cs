using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class Enemy : MonoBehaviour {
        private readonly int AttackPrimaryHash= Animator.StringToHash(@"AttackPrimary");
        private readonly int AttackSecondaryHash = Animator.StringToHash(@"AttackSecondary");
        private readonly int EvadingHash = Animator.StringToHash(@"Evading");
        private readonly int DeadHash = Animator.StringToHash(@"Dead");
        private readonly int DirectionHash = Animator.StringToHash(@"Direction");
        private readonly int SpeedHash = Animator.StringToHash(@"Speed");
        private readonly int DamageHash = Animator.StringToHash(@"Damage");
        private readonly int CriticalDamageHash = Animator.StringToHash(@"CriticalDamage");

        private Renderer Renderer;
        private Animator animator;

        private void Awake() {
            Renderer = GetComponentInChildren<Renderer>();
            animator = GetComponentInChildren<Animator>();
        }

        private void Update() {
            if (!Renderer.isVisible)
                return;

            Vector3 lookDir = Camera.main.transform.position - transform.position;

            Vector2 lookDir2D = new Vector2(lookDir.x, lookDir.z);
            Vector2 forward2D = new Vector2(transform.forward.x, transform.forward.z);

            float sign = Vector3.Cross(forward2D, lookDir2D).z > 0 ? 1f : -1f;
            //sign *= Vector2.Dot(forward2D, lookDir2D) < 0f ? 1f : -1f;
            float angle = Vector2.Angle(forward2D, lookDir2D) * sign;

            while (angle < 0)
                angle += 360f;

            animator.SetFloat(DirectionHash, angle / 360f);

            animator.transform.rotation = Camera.main.transform.rotation;
        }
    }
}