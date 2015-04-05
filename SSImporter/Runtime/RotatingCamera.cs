using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class RotatingCamera : MonoBehaviour {
        private Quaternion startingRotation;

        private const float RotationSpeed = 360f / 30f;
        private float Angle;
        private float Direction;

        private void Start() {
            startingRotation = transform.localRotation;
            Angle = 0f;
            Direction = 1f;
        }

        private void Update() {
            Angle += Time.deltaTime * RotationSpeed * Direction;

            if (Angle >= 45f || Angle <= -45f) {
                Angle -= Angle - Direction * 45f;
                Direction *= -1f;
            }

            transform.localRotation = Quaternion.AngleAxis(Angle, Vector3.up) * startingRotation;
        }
    }
}