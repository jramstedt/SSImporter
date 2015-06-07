﻿using UnityEngine;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    [RequireComponent(typeof(BoxCollider))]
    public class Entry : MonoBehaviour {
        private BoxCollider boxCollider;
        private Triggerable triggerable;

        private void Awake() {
            LevelInfo levelInfo = ObjectFactory.GetController().LevelInfo;

            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1f, 256.0f * levelInfo.HeightFactor, 1f);

            triggerable = GetComponent<Triggerable>();
        }

        private void OnCollisionEnter(Collision collision) {
            // TODO Better check if player

            if(collision.gameObject.tag == "Player")
                triggerable.Trigger();
        }
    }
}