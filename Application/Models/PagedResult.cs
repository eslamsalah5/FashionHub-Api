using System.Collections.Generic;

namespace Application.Models
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => PageIndex > 0;
        public bool HasNextPage => PageIndex < TotalPages - 1;

        public PagedResult(IReadOnlyList<T> items, int pageIndex, int pageSize, int totalCount)
        {
            Items = items;
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = totalCount;
            TotalPages = (int)System.Math.Ceiling(totalCount / (double)pageSize);
        }
    }
}
