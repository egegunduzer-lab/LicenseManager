export interface LicenseListItem {
  id: string;
  company_id: string;
  company_name: string;
  project_id: string;
  project_name: string;
  project_version: string;
  license_key: string;
  modules: string[];
  issued_at: string;
  expiry_date: string;
  valid_until: string;
  is_blocked: boolean;
  created_at: string;
}

export interface CreateLicenseRequest {
  company_id: string;
  project_id: string;
  modules: string[];
  valid_until: string;
}

export interface CreatedLicense {
  license_id: string;
  company_id: string;
  project_id: string;
  license_key: string;
  modules: string[];
  issued_at: string;
  expiry_date: string;
  valid_until: string;
  is_blocked: boolean;
}

export interface LicenseBlockStatus {
  license_id: string;
  is_blocked: boolean;
}
