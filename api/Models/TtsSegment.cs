namespace NewsAggregator.Models
{
    public enum TtsSegmentPart
    {
        Title,
        Attribution,
        Body
    }

    /// <summary>A piece of an article that can be synthesized independently.</summary>
    public sealed record TtsSegment(TtsSegmentPart Part, int? Index);
}
