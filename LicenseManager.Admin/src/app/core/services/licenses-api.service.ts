import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatedLicense,
  CreateLicenseRequest,
  LicenseBlockStatus,
  LicenseListItem
} from '../models/license.model';

@Injectable({ providedIn: 'root' })
export class LicensesApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}/api/licenses`;

  getLicenses(): Observable<LicenseListItem[]> {
    return this.http.get<LicenseListItem[]>(this.endpoint);
  }

  createLicense(request: CreateLicenseRequest): Observable<CreatedLicense> {
    return this.http.post<CreatedLicense>(`${this.endpoint}/create`, request);
  }

  setBlockedStatus(licenseId: string, blocked: boolean): Observable<LicenseBlockStatus> {
    const operation = blocked ? 'block' : 'unblock';
    return this.http.post<LicenseBlockStatus>(`${this.endpoint}/${licenseId}/${operation}`, null);
  }
}
