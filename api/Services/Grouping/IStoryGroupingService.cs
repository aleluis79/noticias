using NewsAggregator.Models;

namespace NewsAggregator.Services.Grouping
{
    public interface IStoryGroupingService
    {
        IReadOnlyList<NewsStory> Group(IReadOnlyList<NewsArticle> articles);
    }
}
