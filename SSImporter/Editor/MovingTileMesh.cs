using UnityEngine;

using System;
using System.Collections.Generic;

namespace SSImporter.Resource {
    public class MovingTileMesh : BaseMesh {
        public enum Type {
            Floor,
            Ceiling
        }

        private Type type;

        public MovingTileMesh(Type type, LevelInfo levelInfo, Tile tile, uint tileX, uint tileY, int[] movingFloorHeightRange, int[] movingCeilingHeightRange)
            : base(levelInfo, tile, tileX, tileY, movingFloorHeightRange, movingCeilingHeightRange) {
            
            this.type = type;

            byte floorSlopeIndex = (byte)tile.Type;
            byte ceilingSlopeIndex = (Tile.SlopeControl)(tile.Flags & Tile.FlagMask.SlopeControl) == Tile.SlopeControl.CeilingReversed
                ? (byte)MapUtils.invertedTypes[(byte)tile.Type]
                : (byte)tile.Type;

            for (int cornerIndex = 0; cornerIndex < 4; ++cornerIndex) {
                floorCornerHeight[cornerIndex] = movingFloorHeightRange[1];

                if ((Tile.SlopeControl)(tile.Flags & Tile.FlagMask.SlopeControl) != Tile.SlopeControl.CeilingOnly) { // Slope Floor
                    if (MapUtils.slopeAffectsCorner[floorSlopeIndex, cornerIndex])
                        floorCornerHeight[cornerIndex] += tile.SlopeSteepnessFactor;
                }

                ceilingCornerHeight[cornerIndex] = movingCeilingHeightRange[0];

                if ((Tile.SlopeControl)(tile.Flags & Tile.FlagMask.SlopeControl) != Tile.SlopeControl.FloorOnly) { // Slope Ceiling
                    if (!MapUtils.slopeAffectsCorner[ceilingSlopeIndex, cornerIndex])
                        ceilingCornerHeight[cornerIndex] -= tile.SlopeSteepnessFactor;
                }
            }
        }

        public override Mesh CreateWall(uint leftCorner, uint rightCorner, TileMesh otherTile, uint otherLeftCorner, uint otherRightCorner, bool flip, params TileType[] ignoreTypes) {
            int[] triangles = MapUtils.faceTriangles[1].Clone() as int[];

            Vector3[] vertices = new Vector3[] {
                VerticeTemplate[leftCorner],
                VerticeTemplate[leftCorner],
                VerticeTemplate[rightCorner],
                VerticeTemplate[rightCorner]
            };

            Vector2[] uvs = flip ? UVTemplate : UVTemplateFlipped;
            
            Mesh mesh = new Mesh();

            bool isSolidWall = otherTile.tile.Type == TileType.Solid;
            for (int i = 0; i < ignoreTypes.Length; ++i)
                isSolidWall |= otherTile.tile.Type == ignoreTypes[i];

            if (ignoreTypes.Length == 0) // Special case, diagonal walls have no ignore types
                isSolidWall = true;

            float mapScale = 1f / (float)(1 << (int)levelInfo.HeightShift);
            float textureVerticalOffset = tile.TextureOffset * mapScale;

            if (isSolidWall) {
                return null;
            } else if (type == Type.Floor) {
                vertices[0].y = floorCornerHeight[leftCorner] * mapScale;
                vertices[1].y = Mathf.Min(movingFloorHeightRange[0], Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) * mapScale;
                vertices[2].y = Mathf.Min(movingFloorHeightRange[0], Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) * mapScale;
                vertices[3].y = floorCornerHeight[rightCorner] * mapScale;

                uvs[0].y = vertices[0].y - textureVerticalOffset;
                uvs[1].y = vertices[1].y - textureVerticalOffset;
                uvs[2].y = vertices[2].y - textureVerticalOffset;
                uvs[3].y = vertices[3].y - textureVerticalOffset;

                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.triangles = triangles;
            } else if (type == Type.Ceiling) {
                vertices[0].y = Mathf.Max(movingCeilingHeightRange[1], floorCornerHeight[leftCorner]) * mapScale;
                vertices[1].y = ceilingCornerHeight[leftCorner] * mapScale;
                vertices[2].y = ceilingCornerHeight[rightCorner] * mapScale;
                vertices[3].y = Mathf.Max(movingCeilingHeightRange[1], floorCornerHeight[leftCorner]) * mapScale;

                /*
                vertices[0].y = movingCeilingHeightRange[1] * mapScale;
                vertices[1].y = Mathf.Min(movingCeilingHeightRange[0], Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) * mapScale;
                vertices[2].y = Mathf.Min(movingCeilingHeightRange[0], Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) * mapScale;
                vertices[3].y = movingCeilingHeightRange[1] * mapScale;
                */
                uvs[0].y = vertices[0].y - textureVerticalOffset;
                uvs[1].y = vertices[1].y - textureVerticalOffset;
                uvs[2].y = vertices[2].y - textureVerticalOffset;
                uvs[3].y = vertices[3].y - textureVerticalOffset;

                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.triangles = triangles;
            }

            return mesh;
        }

        public override bool HasFloor {
            get { return type == Type.Floor; }
        }

        public override bool HasCeiling {
            get { return type == Type.Ceiling; }
        }
    }
}