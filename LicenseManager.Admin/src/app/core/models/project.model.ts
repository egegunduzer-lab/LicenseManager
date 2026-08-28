export interface ProjectListItem {
  id: string;
  name: string;
  version: string;
  key_version: number;
  created_at: string;
  license_count: number;
  key_count: number;
}

export interface CreateProjectRequest {
  name: string;
  version: string;
}

export interface CreatedProject {
  id: string;
  name: string;
  version: string;
  keyVersion: number;
  publicKey: string;
  createdAt: string;
}

export interface ProjectKey {
  id: string;
  key_version: number;
  public_key: string;
  is_active: boolean;
  created_at: string;
}

export interface PreparedProjectKey {
  project_id: string;
  key_version: number;
  public_key: string;
}

export interface ActivatedProjectKey {
  project_id: string;
  key_version: number;
  activated: boolean;
}
