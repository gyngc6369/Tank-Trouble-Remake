namespace TankTrouble.Map
{
    public static class MapBuilder
    {
        public static GridMap Build(MapKind kind, int? randomSeed = null)
        {
            return kind == MapKind.Random
                ? RandomMapGenerator.Generate(randomSeed)
                : PresetMaps.Build((int)kind);
        }
    }
}
