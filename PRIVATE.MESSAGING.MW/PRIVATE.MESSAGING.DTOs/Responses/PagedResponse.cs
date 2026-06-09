namespace PRIVATE.MESSAGING.DTOs.Responses;

public class PagedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Limit);
}
