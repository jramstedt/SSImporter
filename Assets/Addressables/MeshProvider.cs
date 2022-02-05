using UnityEngine;
using System.Collections;
using UnityEngine.ResourceManagement;
using System;
using System.IO;
using Unity.Mathematics;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SS.Resources {
  public class MeshProvider : ResourceProviderBase {
    private Vertex[] vertexBuffer = new Vertex[1000];
    private Polygon polygon = new Polygon { vertices = new Vertex[100] };

    private byte[] parameterData = new byte[4*100];
    public UIntPtr parameterStackPtr;
    //public g3s_point[] _vpoint_tab = new g3s_point[32];
    
    public byte[] _vcolor_tab = new byte[32];

    public ShadeTable shadeTable; // TODO this should be global

    public override Type GetDefaultType(IResourceLocation location) => typeof(MeshInfo);

    public override void Provide(ProvideHandle provideHandle) {
      var location = provideHandle.Location;

      var resFile = provideHandle.GetDependency<ResourceFile>(0);
      if (resFile == null) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource file failed to load for location {location.PrimaryKey}."));
        return;
      }

      var key = provideHandle.ResourceManager.TransformInternalId(location);
      ushort resId, block;
      if (!Utils.ExtractResourceIdAndBlock(key, out resId, out block)) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource {location.InternalId} with key {key} is not valid."));
        return;
      }

      if (resFile.GetResourceInfo(resId).info.ContentType != ResourceFile.ContentType.Obj3D) {
        provideHandle.Complete<AudioClip>(null, false, new Exception($"Resource {location.InternalId} is not {nameof(ResourceFile.ContentType.Obj3D)}."));
        return;
      }

      byte[] rawResource = resFile.GetResourceData(resId, block);

      Array.Clear(vertexBuffer, 0, vertexBuffer.Length);
      Array.Clear(polygon.vertices, 0, polygon.vertices.Length);

      using (MemoryStream ms = new MemoryStream(rawResource)) {
        BinaryReader msbr = new BinaryReader(ms);
        intepreterLoop(ms, msbr);
      }

      provideHandle.Complete(default(MeshInfo), true, null);
    }

    /**
    TODO this should be ran at runtime to update the mesh.
    */
    private void intepreterLoop(MemoryStream ms, BinaryReader msbr) {
      while (ms.Position < ms.Length) {
        long dataPos = ms.Position;
        OpCode command = (OpCode)msbr.ReadUInt16();

        if (command == OpCode.eof || command == OpCode.debug) {
          break;
        } else if (command == OpCode.jnorm) {
          ushort skipBytes = msbr.ReadUInt16();

          Vector3 normal = new Vector3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          Vector3 point = new Vector3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());

          // if normal faces camera continue
          // else 
          // ms.Position = dataPos + skipBytes
        } else if (command == OpCode.lnres) {
          ushort vertexA = msbr.ReadUInt16();
          ushort vertexB = msbr.ReadUInt16();

          // draw line a -> b
        } else if (command == OpCode.multires) {
          ushort count = msbr.ReadUInt16();
          ushort vertexStart = msbr.ReadUInt16();

          for (ushort i = 0; i < count; ++i)
            vertexBuffer[vertexStart + i].position = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());

        } else if (command == OpCode.polyres) {
          ushort count = msbr.ReadUInt16();

          while (count-- > 0)
            polygon.vertices[count] = vertexBuffer[msbr.ReadUInt16()];

          // TODO draw polygon.
          // gour_flag = _itrp_gour_flg
          //if (polygon.check == false)
          //    draw_poly_common(gr_get_fcolor(), count2, poly_buf);
          //else
          //    check_and_draw_common(gr_get_fcolor(), count2, poly_buf);
        } else if (command == OpCode.setcolor) {
          polygon.color = (byte)msbr.ReadUInt16();
          polygon.gouraud = Gouraud.normal;
        } else if (command == OpCode.sortnorm) {
          float3 normal = new float3(msbr.ReadFixed1616(), msbr.ReadFixed1616(), msbr.ReadFixed1616());
          float3 point = new float3(msbr.ReadFixed1616(), msbr.ReadFixed1616(), msbr.ReadFixed1616());

          long firstOpcodePosition = dataPos + msbr.ReadUInt16();
          long secondOpcodePosition = dataPos + msbr.ReadUInt16();

          long continuePosition = ms.Position;

          // if normal faces camera
          ms.Position = firstOpcodePosition;
          intepreterLoop(ms, msbr);
          ms.Position = secondOpcodePosition;
          intepreterLoop(ms, msbr);
          // else
          ms.Position = secondOpcodePosition;
          intepreterLoop(ms, msbr);
          ms.Position = firstOpcodePosition;
          intepreterLoop(ms, msbr);

          ms.Position = continuePosition;
        } else if (command == OpCode.setshade) {
          ushort count = msbr.ReadUInt16();
          while (count-- > 0) {
            ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
            vertex.i = msbr.ReadUInt16();
            vertex.flags |= VertexFlag.I;
          }
        } else if (command == OpCode.goursurf) {
          polygon.gouraudColorBase = (ushort)(msbr.ReadUInt16() << 8);
          polygon.gouraud = Gouraud.spoly;
        } else if (command == OpCode.x_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.x += msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.y_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.y += msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.z_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.y += -msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.xy_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.x += msbr.ReadFixed1616();
          vertex.position.y += -msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.xz_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.x += msbr.ReadFixed1616();
          vertex.position.z += msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.yz_rel) {
          ushort vertexIndex = msbr.ReadUInt16();
          ushort referenceVertex = msbr.ReadUInt16();

          var vertex = vertexBuffer[referenceVertex];
          vertex.position.y += -msbr.ReadFixed1616();
          vertex.position.z += msbr.ReadFixed1616();
          vertex.flags = 0;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.icall_p || command == OpCode.icall_b || command == OpCode.icall_h) {
          // Moves view position
          // rotates along axis
          // runs intepreterLoop with absolute address
          ms.Position += 18;
        } else if (command == OpCode.sfcal) {
          long nextOpcode = dataPos + msbr.ReadUInt16();
          intepreterLoop(ms, msbr);
        } else if (command == OpCode.defres) {
          ushort vertexIndex = msbr.ReadUInt16();
          vertexBuffer[vertexIndex].position = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
        } else if (command == OpCode.defres_i) {
          ushort vertexIndex = msbr.ReadUInt16();
          Vertex vertex = default;
          vertex.position = new float3(msbr.ReadFixed1616(), -msbr.ReadFixed1616(), msbr.ReadFixed1616());
          vertex.i = msbr.ReadUInt16();
          vertex.flags |= VertexFlag.I;
          vertexBuffer[vertexIndex] = vertex;
        } else if (command == OpCode.getparms) {
          ushort dest = msbr.ReadUInt16();
          ushort src = msbr.ReadUInt16();
          ushort count = msbr.ReadUInt16();

          // In SS object rendering is called with variable amount of params. This copies them to array
                    
          // 32bit copy:
          // while (count-->0)
	        //  *(dest++) = *(src)++;

          //while(--count > 0)
          //  Write<uint>(parameterData, dest+=4) = Read<uint>(UIntPtr.Add(parameterStackPtr, src++)); // read byte from this address
        } else if (command == OpCode.getparms_i) {
          ushort dest = msbr.ReadUInt16();
          ushort src = msbr.ReadUInt16();
          ushort count = msbr.ReadUInt16();

          // In SS object rendering is called with variable amount of params. This copies them to array
          // param in dest is used as pointer to pointer
        } else if (command == OpCode.gour_p) {
          polygon.gouraudColorBase = (ushort)(parameterData[msbr.ReadUInt16()] << 8);
          polygon.gouraud = Gouraud.spoly;
        } else if (command == OpCode.gour_vc) {
          polygon.gouraudColorBase = (ushort)(_vcolor_tab[msbr.ReadUInt16()] << 8);
          polygon.gouraud = Gouraud.spoly;
        } else if (command == OpCode.getvcolor) {
          ushort colorIndex = msbr.ReadUInt16();
          polygon.color = _vcolor_tab[colorIndex];
          polygon.gouraud = 0;
        } else if (command == OpCode.getvscolor) {
          ushort colorIndex = msbr.ReadUInt16();
          ushort shade = msbr.ReadUInt16();
          polygon.color = shadeTable.paletteIndex[(shade << 8) | _vcolor_tab[colorIndex]];
        } else if (command == OpCode.rgbshades) {
          ushort count = msbr.ReadUInt16();
          while (count-- > 0) {
            ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
            vertex.rgb = msbr.ReadUInt32();
            vertex.flags |= VertexFlag.RGB;
            ms.Position += 4;
          }
        } else if (command == OpCode.draw_mode) {
          ushort flags = msbr.ReadUInt16();
          polygon.wire = ((flags >> 8) & 1) == 1;
          flags &= 0x00FF;
          flags <<= 1;
          polygon.check = ((flags >> 8) & 1) == 1;
          flags &= 0x00FF;
          flags <<= 2;
          polygon.gouraud = (Gouraud)(flags - 1);
        } else if (command == OpCode.getpcolor) {
          ushort paramIndex = msbr.ReadUInt16();
          polygon.color = parameterData[paramIndex];
          polygon.gouraud = 0;
        } else if (command == OpCode.getpscolor) {
          ushort colorIndex = msbr.ReadUInt16();
          ushort shade = msbr.ReadUInt16();
          polygon.color = shadeTable.paletteIndex[(shade << 8) | _vcolor_tab[colorIndex]];
        } else if (command == OpCode.scaleres) {
          break;
        } else if (command == OpCode.vpnt_p) {
          ushort paramByteOffset = msbr.ReadUInt16();
          ushort vertexIndex = msbr.ReadUInt16();
          //vertexBuffer[vertexIndex] = parameterData.Read<g3s_point>(paramByteOffset);
        } else if (command == OpCode.vpnt_v) {
          ushort vpointIndex = msbr.ReadUInt16();
          ushort vertexIndex = msbr.ReadUInt16();
          //vertexBuffer[vertexIndex] = _vpoint_tab[vpointIndex>>2];
        } else if (command == OpCode.setuv) {
          ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
          vertex.uv = new float2(msbr.ReadFixed1616(), 1f - msbr.ReadFixed1616());
          vertex.flags |= VertexFlag.U | VertexFlag.V;
        } else if (command == OpCode.uvlist) {
          ushort count = msbr.ReadUInt16();

          while (count-- > 0) {
            ref var vertex = ref vertexBuffer[msbr.ReadUInt16()];
            vertex.uv = new float2(msbr.ReadFixed1616(), 1f - msbr.ReadFixed1616());
            vertex.flags |= VertexFlag.U | VertexFlag.V;
          }
        } else if (command == OpCode.tmap) {
          polygon.textureId = msbr.ReadUInt16();
          ushort count = msbr.ReadUInt16();

          while (count-- > 0)
            polygon.vertices[count] = vertexBuffer[msbr.ReadUInt16()];

          // Draw textured polygon
        } else if (command == OpCode.dbg) {
          ushort skip = msbr.ReadUInt16();
          ushort code = msbr.ReadUInt16();
          ushort polygonId = msbr.ReadUInt16();
        }
      }
    }

    internal enum OpCode : ushort {
      eof,
      jnorm,
      lnres,
      multires,
      polyres,
      setcolor,
      sortnorm,
      debug,
      setshade,
      goursurf,
      x_rel,
      y_rel,
      z_rel,
      xy_rel,
      xz_rel,
      yz_rel,
      icall_p,
      icall_b,
      icall_h,
      _reserved,
      sfcal,
      defres,
      defres_i,
      getparms,
      getparms_i,
      gour_p,
      gour_vc,
      getvcolor,
      getvscolor,
      rgbshades,
      draw_mode,
      getpcolor,
      getpscolor,
      scaleres,
      vpnt_p,
      vpnt_v,
      setuv,
      uvlist,
      tmap,
      dbg
    }

    internal enum Gouraud : byte {
      normal,
      tluc_poly,
      spoly,
      tluc_spoly,
      cpoly
    }

    [Flags]
    internal enum VertexFlag : byte {
      U = 1,
      V = 2,
      I = 4,
      PROJECTED = 8,
      RGB = 16,
      CLIPPNT = 32,
      LIT = 64
    }

    internal struct Vertex {
      public float3 position;
      public float2 uv;
      public ushort i;
      public uint rgb;
      public VertexFlag flags;
    }

    internal struct Polygon {
      public Vertex[] vertices;
      public byte color; // SSCC, SS = shade, CC = color index
      public ushort textureId;
      public bool wire;
      public bool check;
      public Gouraud gouraud;
      public ushort gouraudColorBase;
    }

    /*
    struct g3s_point {
      uint x, y, z;
      uint sx, sy;
      byte codes;
      byte p3_flags;
      uint u, v;
      uint i;
    }
    */
  }

  public struct MeshInfo : IDisposable {
    public Mesh Mesh;
    public uint[] TextureIds;

    public void Dispose() {
      if (Mesh != null) UnityEngine.Object.Destroy(Mesh);
    }
  }
}