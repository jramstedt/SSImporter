﻿using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace SS.Data {
  internal struct Vertex {
    public float3 pos;
    public float3 normal;
    public float3 tangent;
    public half2 uv;
    public float light;
  }
}
