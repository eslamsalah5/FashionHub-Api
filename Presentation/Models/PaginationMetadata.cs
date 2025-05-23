using System;

namespace Presentation.Models
{
    public class PaginationMetadata
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        public PaginationMetadata(int pageIndex, int pageSize, int totalCount)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = totalCount;
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            HasPreviousPage = pageIndex > 0;
            HasNextPage = pageIndex < TotalPages - 1;
        }
    }
}
