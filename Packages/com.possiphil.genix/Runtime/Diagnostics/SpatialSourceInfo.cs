namespace Genix.Diagnostics
{
    /// <summary>Identifies the external spatial-data source used for a generation run.</summary>
    public readonly struct SpatialSourceInfo
    {
        /// <summary>Gets source type.</summary>
        public string SourceType { get; }
        /// <summary>Gets source name.</summary>
        public string SourceName { get; }
        /// <summary>Gets source id.</summary>
        public string SourceId { get; }

        /// <summary>Initializes a new instance of spatial source info.</summary>
        public SpatialSourceInfo(string sourceType, string sourceName, string sourceId)
        {
            SourceType = sourceType;
            SourceName = sourceName;
            SourceId = sourceId;
        }
    }
}
