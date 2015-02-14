using UnityEngine;

using System;
using System.Collections.Generic;

namespace SSImporter.Resource {
    public abstract class BaseMesh {
        public readonly uint tileX;
        public readonly uint tileY;

        protected Vector2[] UVTemplate = new Vector2[] {
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f)
        };

        protected Vector2[] UVTemplateFlipped = new Vector2[] {
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f)
        };

        protected Vector3[] VerticeTemplate = new Vector3[] {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 1f),
            new Vector3(1f, 0f, 0f)
        };

        protected readonly int[] floorCornerHeight;
        protected readonly int[] ceilingCornerHeight;

        public readonly int[] movingFloorHeightRange;
        public readonly int[] movingCeilingHeightRange;

        public readonly Tile tile;
        public readonly LevelInfo levelInfo;

        public BaseMesh(LevelInfo levelInfo, Tile tile, uint tileX, uint tileY, int[] movingFloorHeightRange, int[] movingCeilingHeightRange) {
            this.levelInfo = levelInfo;
            this.tile = tile;
            this.tileX = tileX;
            this.tileY = tileY;

            this.movingFloorHeightRange = movingFloorHeightRange;
            this.movingCeilingHeightRange = movingCeilingHeightRange;

            this.floorCornerHeight = new int[4];
            this.ceilingCornerHeight = new int[4];
        }

        public Mesh CreateFloor(Tile.Orientation orientation) {
            return CreatePlane(floorCornerHeight, false, orientation);
        }

        public Mesh CreateCeiling(Tile.Orientation orientation) {
            return CreatePlane(ceilingCornerHeight, true, orientation);
        }

        private Mesh CreatePlane(int[] cornerHeight, bool reverseFaces = false, Tile.Orientation orientation = Tile.Orientation.North) {
            Vector3[] vertices = VerticeTemplate;
            int[] triangles = MapUtils.faceTriangles[(byte)tile.Type].Clone() as int[];

            if (reverseFaces)
                Array.Reverse(triangles);

            Vector2[] UVs = UVTemplate;

            if (orientation != Tile.Orientation.North) {
                uint rotation = 0;
                if (orientation == Tile.Orientation.East)
                    rotation = 1;
                else if (orientation == Tile.Orientation.South)
                    rotation = 2;
                else if (orientation == Tile.Orientation.West)
                    rotation = 3;

                UVs = UVTemplate.RotateRight(rotation);
            }

            for (int corner = 0; corner < 4; ++corner)
                vertices[corner].y = (float)cornerHeight[corner] / (float)Math.Pow(2, (float)levelInfo.HeightFactor);

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = UVs;
            mesh.triangles = triangles;

            return mesh;
        }

        public abstract Mesh CreateWall(uint leftCorner, uint rightCorner, TileMesh otherTile, uint otherLeftCorner, uint otherRightCorner, bool flip, params TileType[] ignoreTypes);

        public abstract bool HasFloor { get; }

        public abstract bool HasCeiling { get; }
    }
}