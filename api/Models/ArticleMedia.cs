namespace NewsAggregator.Models
{
    /// <summary>
    /// One image or video that belongs to the body of an article. Media is
    /// optional: it is returned as metadata and the reader decides whether to
    /// display it.
    /// </summary>
    public class ArticleMedia
    {
        /// <summary>"image", "video" or "videoEmbed".</summary>
        public string Type { get; set; } = "image";

        /// <summary>Absolute HTTP(S) URL of the image, video file or watch page.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Preview image for a video, when the source publishes one.</summary>
        public string? PosterUrl { get; set; }

        /// <summary>Caption published by the outlet, when available.</summary>
        public string? Caption { get; set; }

        /// <summary>Alternative text published by the outlet, when available.</summary>
        public string? Alt { get; set; }

        /// <summary>"native", "youtube" or "vimeo" for videos; null for images.</summary>
        public string? Provider { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        /// <summary>
        /// Number of extracted paragraphs that precede this medium. Zero means
        /// it appears before the first paragraph.
        /// </summary>
        public int AfterParagraph { get; set; }
    }
}
