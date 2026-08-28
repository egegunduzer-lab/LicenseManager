import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { CompanyListItem } from '../../core/models/company.model';
import { CompaniesApiService } from '../../core/services/companies-api.service';

@Component({
  selector: 'app-companies',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './companies.component.html',
  styleUrl: './companies.component.css'
})
export class CompaniesComponent implements OnInit {
  private readonly companiesApi = inject(CompaniesApiService);

  readonly companies = signal<CompanyListItem[]>([]);
  readonly isLoading = signal(true);
  readonly isSubmitting = signal(false);
  readonly loadError = signal('');
  readonly formError = signal('');
  readonly successMessage = signal('');

  readonly companyNameControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(200)]
  });

  ngOnInit(): void {
    this.loadCompanies();
  }

  loadCompanies(): void {
    this.isLoading.set(true);
    this.loadError.set('');

    this.companiesApi
      .getCompanies()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (companies) => this.companies.set(companies),
        error: (error: unknown) => {
          this.loadError.set(this.getErrorMessage(error, 'Şirketler yüklenemedi.'));
        }
      });
  }

  createCompany(event: Event): void {
    event.preventDefault();

    if (this.companyNameControl.invalid || this.isSubmitting()) {
      this.companyNameControl.markAsTouched();
      return;
    }

    const name = this.companyNameControl.value.trim();

    if (!name) {
      this.companyNameControl.setErrors({ required: true });
      this.companyNameControl.markAsTouched();
      return;
    }

    this.formError.set('');
    this.successMessage.set('');
    this.isSubmitting.set(true);

    this.companiesApi
      .createCompany({ name })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (company) => {
          this.companyNameControl.reset();
          this.successMessage.set(`${company.name} oluşturuldu.`);
          this.loadCompanies();
        },
        error: (error: unknown) => {
          this.formError.set(this.getErrorMessage(error, 'Şirket oluşturulamadı.'));
        }
      });
  }

  private getErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const apiMessage = error.error?.message;

      if (typeof apiMessage === 'string' && apiMessage.trim()) {
        return apiMessage;
      }

      if (error.status === 0) {
        return 'API’ye ulaşılamadı. API’nin çalıştığını kontrol et.';
      }
    }

    return fallback;
  }
}
