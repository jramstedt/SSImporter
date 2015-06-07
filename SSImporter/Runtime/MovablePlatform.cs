using UnityEngine;
using System;
using System.Collections;


namespace SystemShock {
    [RequireComponent(typeof(Rigidbody))]
    public abstract class MovablePlatform : MonoBehaviour {
        private LevelInfo levelInfo;
        new private Rigidbody rigidbody;

        public int OriginHeight;

        private void Awake() {
            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
            rigidbody = GetComponent<Rigidbody>();
        }

        private void Start() {
            if (levelInfo == null)
                return;

            Vector3 position = rigidbody.position;
            position.y = (height - OriginHeight) * levelInfo.MapScale;
            rigidbody.MovePosition(position);
        }

        private void Update() {
            float targetY = (height - OriginHeight) * levelInfo.MapScale;
            
            Vector3 position = rigidbody.position;

            if (Mathf.Approximately(position.y, targetY))
                return;

            int delta = Math.Sign(targetY - position.y);

            position.y += delta * 0.35f * Time.deltaTime;

            if (delta < 0 && position.y <= targetY)
                position.y = targetY;
            else if(delta > 0 && position.y >= targetY)
                position.y = targetY;

            rigidbody.MovePosition(position);
        }

        [SerializeField, HideInInspector]
        private int height;
        public int Height {
            get { return height; }
            set { height = value; }
        }
    }
}