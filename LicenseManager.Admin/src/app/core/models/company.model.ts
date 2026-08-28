export interface CompanyListItem {
  id: string;
  name: string;
  created_at: string;
  license_count: number;
}

export interface CreateCompanyRequest {
  name: string;
}

export interface CreatedCompany {
  id: string;
  name: string;
  created_at: string;
}
