using System.Collections.Generic;
using UnityEngine;

namespace TankTrouble.Map
{
    public sealed class MapRenderer : MonoBehaviour
    {
        [SerializeField] private MapKind mapKind = MapKind.Random;
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool useRandomSeed;
        [SerializeField] private int randomSeed;
        [SerializeField] private Color wallColor = Color.black;
        [SerializeField] private Transform wallParent;
        [SerializeField] private Material wallMaterial;

        private readonly List<GameObject> wallObjects = new List<GameObject>();
        private static Sprite whiteSprite;

        public GridMap CurrentMap { get; private set; }

        private void Start()
        {
            if (buildOnStart)
                BuildSelectedMap();
        }

        public void BuildSelectedMap()
        {
            var seed = useRandomSeed ? randomSeed : (int?)null;
            Render(MapBuilder.Build(mapKind, seed));
        }

        public void Render(GridMap gridMap)
        {
            CurrentMap = gridMap;
            ClearWalls();

            if (wallParent == null)
                wallParent = transform;
            var segments = gridMap.GetWallSegments(mergeContiguous: true);
            for (var i = 0; i < segments.Count; i++)
                CreateWallObject(segments[i], i);
        }

        public void ClearWalls()
        {
            for (var i = wallObjects.Count - 1; i >= 0; i--)
            {
                if (wallObjects[i] == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(wallObjects[i]);
                else
                    DestroyImmediate(wallObjects[i]);
            }

            wallObjects.Clear();
        }

        private void CreateWallObject(WallSegment segment, int index)
        {
            var wall = new GameObject($"Wall_{index:000}");
            wall.transform.SetParent(wallParent, false);
            wall.transform.position = segment.WorldCenter;
            wall.transform.localScale = segment.WorldSize;
            wall.layer = LayerMask.NameToLayer("Wall");

            var sr = wall.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            sr.color = wallColor;
            if (wallMaterial != null)
                sr.sharedMaterial = wallMaterial;

            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.usedByComposite = false;

            wallObjects.Add(wall);
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return whiteSprite;
        }
    }
}
