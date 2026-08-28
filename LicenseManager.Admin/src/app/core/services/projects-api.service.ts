import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ActivatedProjectKey,
  CreatedProject,
  CreateProjectRequest,
  PreparedProjectKey,
  ProjectKey,
  ProjectListItem
} from '../models/project.model';

@Injectable({ providedIn: 'root' })
export class ProjectsApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}/api/projects`;

  getProjects(): Observable<ProjectListItem[]> {
    return this.http.get<ProjectListItem[]>(this.endpoint);
  }

  createProject(request: CreateProjectRequest): Observable<CreatedProject> {
    return this.http.post<CreatedProject>(this.endpoint, request);
  }

  getProjectKeys(projectId: string): Observable<ProjectKey[]> {
    return this.http.get<ProjectKey[]>(`${this.endpoint}/${projectId}/keys`);
  }

  prepareKeyRotation(projectId: string): Observable<PreparedProjectKey> {
    return this.http.post<PreparedProjectKey>(
      `${this.endpoint}/${projectId}/keys/prepare`,
      null
    );
  }

  activateKey(projectId: string, keyVersion: number): Observable<ActivatedProjectKey> {
    return this.http.post<ActivatedProjectKey>(
      `${this.endpoint}/${projectId}/keys/${keyVersion}/activate`,
      null
    );
  }
}
