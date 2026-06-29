using UnityEngine;
using UnityEngine.UI;
using TankTrouble.Config;
using TankTrouble.Map;

namespace TankTrouble.UI
{
    [RequireComponent(typeof(RawImage))]
    public sealed class MapThumbnailRenderer : MonoBehaviour
    {
        [SerializeField] private MapKind mapKind = MapKind.Open;
        [SerializeField] private int width = 190;
        [SerializeField] private int height = 100;
        [SerializeField] private Color background = new Color32(250, 250, 250, 255);
        [SerializeField] private Color wallColor = new Color32(40, 40, 40, 255);
        [SerializeField] private bool randomUsesStablePreview = true;
        [SerializeField] private int randomPreviewSeed = 12345;

        private RawImage image;

        private void Awake()
        {
            image = GetComponent<RawImage>();
            Render();
        }

        private void OnValidate()
        {
            if (image == null)
                image = GetComponent<RawImage>();
            Render();
        }

        public void SetMapKind(MapKind kind)
        {
            mapKind = kind;
            Render();
        }

        public void Render()
        {
            if (image == null)
                return;

            width = Mathf.Max(32, width);
            height = Mathf.Max(32, height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            var pixels = new Color32[width * height];
            var bg = (Color32)background;
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = bg;
            texture.SetPixels32(pixels);

            int? seed = randomUsesStablePreview ? randomPreviewSeed : (int?)null;
            var grid = mapKind == MapKind.Random
                ? RandomMapGenerator.Generate(seed)
                : PresetMaps.Build((int)mapKind);

            var scaleX = width / (float)(GameConfig.GridCols * GameConfig.CellSize);
            var scaleY = height / (float)(GameConfig.GridRows * GameConfig.CellSize);
            var scale = Mathf.Min(scaleX, scaleY);

            var segments = grid.GetWallSegments(mergeContiguous: true);
            for (var i = 0; i < segments.Count; i++)
                DrawSegment(texture, segments[i], scale);

            texture.Apply(false);
            image.texture = texture;
        }

        private void DrawSegment(Texture2D texture, WallSegment segment, float scale)
        {
            var x1 = Mathf.RoundToInt(segment.StartPixel.x * scale);
            var y1 = Mathf.RoundToInt((segment.StartPixel.y - GameConfig.GridOffsetY) * scale);
            var x2 = Mathf.RoundToInt(segment.EndPixel.x * scale);
            var y2 = Mathf.RoundToInt((segment.EndPixel.y - GameConfig.GridOffsetY) * scale);
            var thickness = Mathf.Max(1, Mathf.RoundToInt(GameConfig.WallThickness * scale));

            if (y1 == y2)
                FillRect(texture, Mathf.Min(x1, x2), y1 - thickness / 2, Mathf.Abs(x2 - x1) + 1, thickness);
            else
                FillRect(texture, x1 - thickness / 2, Mathf.Min(y1, y2), thickness, Mathf.Abs(y2 - y1) + 1);
        }

        private void FillRect(Texture2D texture, int x, int y, int w, int h)
        {
            for (var yy = 0; yy < h; yy++)
            for (var xx = 0; xx < w; xx++)
            {
                var px = x + xx;
                var py = height - 1 - (y + yy);
                if (px >= 0 && px < width && py >= 0 && py < height)
                    texture.SetPixel(px, py, wallColor);
            }
        }
    }
}
