import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CompanyListItem,
  CreateCompanyRequest,
  CreatedCompany
} from '../models/company.model';

@Injectable({ providedIn: 'root' })
export class CompaniesApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}/api/companies`;

  getCompanies(): Observable<CompanyListItem[]> {
    return this.http.get<CompanyListItem[]>(this.endpoint);
  }

  createCompany(request: CreateCompanyRequest): Observable<CreatedCompany> {
    return this.http.post<CreatedCompany>(this.endpoint, request);
  }
}
