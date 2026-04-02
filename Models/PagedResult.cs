using System.Collections.Generic;

namespace RdpManager.Models
{
    public class PagedResult<T>
    {
        public IList<T> Items { get; set; }

        public int TotalCount { get; set; }

        public int CurrentPage { get; set; }

        public int TotalPages { get; set; }
    }
}
