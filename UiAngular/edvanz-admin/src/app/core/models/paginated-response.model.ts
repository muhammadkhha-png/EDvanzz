/**
 * Mirrors the Edvanz backend PaginatedResponse<T> envelope.
 * The list payload lives in `data`, not `items`.
 */
export interface PaginatedResponse<T> {
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  data: T;
}

/** Common query params for paginated endpoints. */
export interface PagedQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}
