using UnityEngine;

using System;
using System.Collections.Generic;

namespace SSImporter.Resource {
    public class TileMesh : BaseMesh {
        public TileMesh(LevelInfo levelInfo, Tile tile, uint tileX, uint tileY, int[] movingFloorHeightRange, int[] movingCeilingHeightRange)
            : base(levelInfo, tile, tileX, tileY, movingFloorHeightRange, movingCeilingHeightRange) {
            
            byte floorSlopeIndex = (byte)tile.Type;
            byte ceilingSlopeIndex = (Tile.SlopeControl)(tile.Flags & Tile.FlagMask.SlopeControl) == Tile.SlopeControl.CeilingReversed
                ? (byte)MapUtils.invertedTypes[(byte)tile.Type]
                : (byte)tile.Type;

            for (int cornerIndex = 0; cornerIndex < 4; ++cornerIndex) {
                if (FloorMoving) {
                    floorCornerHeight[cornerIndex] = movingFloorHeightRange[0];
                } else {
                    floorCornerHeight[cornerIndex] = tile.FloorHeight;

                    if ((Tile.SlopeControl)(tile.Flags & Tile.FlagMask.SlopeControl) != Tile.SlopeControl.CeilingOnly) { // Slope Floor
                        if (MapUtils.slopeAffectsCorner[floorSlopeIndex, cornerIndex])
                            floorCornerHeight[cornerIndex] += tile.SlopeSteepnessFactor;
                    }
                }

                if (CeilingMoving) {
                    ceilingCornerHeight[cornerIndex] = movingCeilingHeightRange[1];
                } else {
                    ceilingCornerHeight[cornerIndex] = tile.CeilingHeight;

                    if ((Tile.SlopeControl)(tile.Flags & Tile.FlagMask.SlopeControl) != Tile.SlopeControl.FloorOnly) { // Slope Ceiling
                        if (!MapUtils.slopeAffectsCorner[ceilingSlopeIndex, cornerIndex])
                            ceilingCornerHeight[cornerIndex] -= tile.SlopeSteepnessFactor;
                    }
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

            Vector2[] uvs = flip ? UVTemplateFlipped : UVTemplate;

            Mesh mesh = new Mesh();

            bool isSolidWall = otherTile.tile.Type == TileType.Solid;
            for (int i = 0; i < ignoreTypes.Length; ++i)
                isSolidWall |= otherTile.tile.Type == ignoreTypes[i];

            if (ignoreTypes.Length == 0) // Special case, diagonal walls have no ignore types
                isSolidWall = true;

            float mapScale = 1f / (float)Mathf.Pow(2, levelInfo.HeightFactor);
            float textureVerticalOffset = tile.TextureOffset * mapScale;

            if (isSolidWall) { // Add solid wall
                vertices[0].y = (float)floorCornerHeight[leftCorner] * mapScale;
                vertices[1].y = (float)ceilingCornerHeight[leftCorner] * mapScale;
                vertices[2].y = (float)ceilingCornerHeight[rightCorner] * mapScale;
                vertices[3].y = (float)floorCornerHeight[rightCorner] * mapScale;

                uvs[0].y = vertices[0].y - textureVerticalOffset;
                uvs[1].y = vertices[1].y - textureVerticalOffset;
                uvs[2].y = vertices[2].y - textureVerticalOffset;
                uvs[3].y = vertices[3].y - textureVerticalOffset;

                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.triangles = triangles;
            } else { // Possibly two part wall
                
                List<CombineInstance> partInstances = new List<CombineInstance>();

                int[] portalPoints = new int[] {
                    Mathf.Max(floorCornerHeight[leftCorner], otherTile.floorCornerHeight[otherLeftCorner]), 
                    Mathf.Min(ceilingCornerHeight[leftCorner], otherTile.ceilingCornerHeight[otherLeftCorner]),
                    Mathf.Min(ceilingCornerHeight[rightCorner], otherTile.ceilingCornerHeight[otherRightCorner]),
                    Mathf.Max(floorCornerHeight[rightCorner], otherTile.floorCornerHeight[otherRightCorner])
                };

                bool floorAboveCeiling = portalPoints[0] > portalPoints[1] ^ portalPoints[3] > portalPoints[2]; // Other corner of ceiling is above and other below floor

                // Upper portal border is below ceiling
                if (Mathf.Min(portalPoints[1], portalPoints[2]) < Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) {
                    vertices[0].y = (floorAboveCeiling ? portalPoints[1] : Mathf.Max(portalPoints[1], floorCornerHeight[leftCorner])) * mapScale;
                    vertices[1].y = ceilingCornerHeight[leftCorner] * mapScale;
                    vertices[2].y = ceilingCornerHeight[rightCorner] * mapScale;
                    vertices[3].y = (floorAboveCeiling ? portalPoints[2] : Mathf.Max(portalPoints[2], floorCornerHeight[leftCorner])) * mapScale;

                    uvs[0].y = vertices[0].y - textureVerticalOffset;
                    uvs[1].y = vertices[1].y - textureVerticalOffset;
                    uvs[2].y = vertices[2].y - textureVerticalOffset;
                    uvs[3].y = vertices[3].y - textureVerticalOffset;

                    Mesh topMesh = new Mesh();
                    topMesh.vertices = vertices;
                    topMesh.uv = uvs;
                    topMesh.triangles = triangles;

                    partInstances.Add(new CombineInstance() {
                        mesh = topMesh,
                        subMeshIndex = 0,
                        transform = Matrix4x4.identity
                    });
                }

                // Lower border is above floor
                if (Mathf.Max(portalPoints[0], portalPoints[3]) > Mathf.Min(floorCornerHeight[leftCorner], floorCornerHeight[rightCorner])) {
                    vertices[0].y = floorCornerHeight[leftCorner] * mapScale;
                    vertices[1].y = Mathf.Min(portalPoints[0], Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) * mapScale;
                    vertices[2].y = Mathf.Min(portalPoints[3], Mathf.Max(ceilingCornerHeight[leftCorner], ceilingCornerHeight[rightCorner])) * mapScale;
                    vertices[3].y = floorCornerHeight[rightCorner] * mapScale;

                    uvs[0].y = vertices[0].y - textureVerticalOffset;
                    uvs[1].y = vertices[1].y - textureVerticalOffset;
                    uvs[2].y = vertices[2].y - textureVerticalOffset;
                    uvs[3].y = vertices[3].y - textureVerticalOffset;

                    Mesh bottomMesh = new Mesh();
                    bottomMesh.vertices = vertices;
                    bottomMesh.uv = uvs;
                    bottomMesh.triangles = triangles;

                    partInstances.Add(new CombineInstance() {
                        mesh = bottomMesh,
                        subMeshIndex = 0,
                        transform = Matrix4x4.identity
                    });
                }

                if (partInstances.Count > 0)
                    mesh.CombineMeshes(partInstances.ToArray(), true, false);
            }
            
            return mesh.triangles.Length > 0 ? mesh : null;
        }

        public bool FloorMoving {
            get { return tile.FloorHeight != movingFloorHeightRange[0] || tile.FloorHeight != movingFloorHeightRange[1]; }
        }

        public bool CeilingMoving {
            get { return tile.CeilingHeight != movingCeilingHeightRange[0] || tile.CeilingHeight != movingCeilingHeightRange[1]; }
        }

        public override bool HasFloor {
            get { return !FloorMoving; }
        }

        public override bool HasCeiling {
            get { return !CeilingMoving; }
        }
    }
}