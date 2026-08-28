export interface DashboardResponse {
  generated_at: string;
  summary: DashboardSummary;
  recent_licenses: RecentLicense[];
  expiring_licenses: ExpiringLicense[];
}

export interface DashboardSummary {
  total_companies: number;
  total_projects: number;
  total_licenses: number;
  active_licenses: number;
  blocked_licenses: number;
  expired_licenses: number;
  signed_expiring_within_7_days: number;
  real_expiring_within_30_days: number;
}

export interface RecentLicense {
  id: string;
  company_name: string;
  project_name: string;
  project_version: string;
  license_key: string;
  expiry_date: string;
  valid_until: string;
  is_blocked: boolean;
  created_at: string;
}

export interface ExpiringLicense {
  id: string;
  company_name: string;
  project_name: string;
  project_version: string;
  valid_until: string;
  days_remaining: number;
}
