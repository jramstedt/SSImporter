﻿using UnityEngine;
using System.Collections;

namespace SystemShock {
    public class Billboard : MonoBehaviour {
        private void OnWillRenderObject() {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}