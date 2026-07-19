namespace Genix.Diagnostics
{
    public readonly struct SpatialSourceInfo
    {
        public string SourceType { get; }
        public string SourceName { get; }
        public string SourceId { get; }

        public SpatialSourceInfo(string sourceType, string sourceName, string sourceId)
        {
            SourceType = sourceType;
            SourceName = sourceName;
            SourceId = sourceId;
        }
    }
}