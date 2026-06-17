namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// Обёртка для постраничного результата
    /// </summary>

    public class PagedResult<T>
    {       
        public IReadOnlyList<T> Items { get; init; } = []; // Элементы текущей страницы
       
        public int TotalCount { get; init; } // Общее количество записей
       
        public int Page { get; init; } // Текущий номер страницы
       
        public int PageSize { get; init; } // Размер страницы
       
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0; // Общее количество страниц
               
        public bool HasNextPage => Page < TotalPages; // Есть ли следующая страница
       
        public bool HasPreviousPage => Page > 1; // Есть ли предыдущая страница
    }
}
