export interface InsurerDto {
  id: string;
  code: string;
  name: string;
}

export interface CreateInsurerRequest {
  code: string;
  name: string;
}
