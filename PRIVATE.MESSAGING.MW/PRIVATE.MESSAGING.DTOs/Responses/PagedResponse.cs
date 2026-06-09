namespace PRIVATE.MESSAGING.DTOs.Responses;

public class PagedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public string? NextCursor { get; set; }
}
