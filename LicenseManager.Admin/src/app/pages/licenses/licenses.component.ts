import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { finalize, forkJoin, startWith } from 'rxjs';
import { CompanyListItem } from '../../core/models/company.model';
import { CreatedLicense, LicenseListItem } from '../../core/models/license.model';
import { ProjectListItem } from '../../core/models/project.model';
import { CompaniesApiService } from '../../core/services/companies-api.service';
import { LicensesApiService } from '../../core/services/licenses-api.service';
import { ProjectsApiService } from '../../core/services/projects-api.service';

type LicenseStatusFilter = 'all' | 'active' | 'blocked' | 'expired';

@Component({
  selector: 'app-licenses',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './licenses.component.html',
  styleUrl: './licenses.component.css'
})
export class LicensesComponent implements OnInit {
  private readonly licensesApi = inject(LicensesApiService);
  private readonly companiesApi = inject(CompaniesApiService);
  private readonly projectsApi = inject(ProjectsApiService);

  readonly licenses = signal<LicenseListItem[]>([]);
  readonly companies = signal<CompanyListItem[]>([]);
  readonly projects = signal<ProjectListItem[]>([]);
  readonly createdLicense = signal<CreatedLicense | null>(null);

  readonly isLoading = signal(true);
  readonly isReferenceLoading = signal(true);
  readonly isSubmitting = signal(false);
  readonly changingLicenseId = signal<string | null>(null);
  readonly loadError = signal('');
  readonly referenceError = signal('');
  readonly formError = signal('');
  readonly actionMessage = signal('');
  readonly copyMessage = signal('');

  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly statusControl = new FormControl<LicenseStatusFilter>('all', { nonNullable: true });
  readonly companyIdControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required]
  });
  readonly projectIdControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required]
  });
  readonly modulesControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required]
  });
  readonly validUntilControl = new FormControl(this.defaultValidUntil(), {
    nonNullable: true,
    validators: [Validators.required]
  });

  private readonly searchQuery = toSignal(
    this.searchControl.valueChanges.pipe(startWith(this.searchControl.value)),
    { initialValue: '' }
  );
  private readonly statusFilter = toSignal(
    this.statusControl.valueChanges.pipe(startWith(this.statusControl.value)),
    { initialValue: 'all' as LicenseStatusFilter }
  );

  readonly filteredLicenses = computed(() => {
    const query = this.searchQuery().trim().toLocaleLowerCase('tr-TR');
    const status = this.statusFilter();

    return this.licenses().filter((license) => {
      const matchesQuery =
        !query ||
        license.company_name.toLocaleLowerCase('tr-TR').includes(query) ||
        license.project_name.toLocaleLowerCase('tr-TR').includes(query) ||
        license.license_key.toLocaleLowerCase('tr-TR').includes(query);

      return matchesQuery && (status === 'all' || this.licenseStatusCode(license) === status);
    });
  });

  readonly statusSummary = computed(() => ({
    total: this.licenses().length,
    active: this.licenses().filter((license) => this.licenseStatusCode(license) === 'active').length,
    blocked: this.licenses().filter((license) => license.is_blocked).length,
    expired: this.licenses().filter((license) => this.licenseStatusCode(license) === 'expired').length
  }));

  ngOnInit(): void {
    this.loadLicenses();
    this.loadReferenceData();
  }

  loadLicenses(): void {
    this.isLoading.set(true);
    this.loadError.set('');

    this.licensesApi
      .getLicenses()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (licenses) => this.licenses.set(licenses),
        error: (error: unknown) => {
          this.loadError.set(this.getErrorMessage(error, 'Lisanslar yüklenemedi.'));
        }
      });
  }

  createLicense(event: Event): void {
    event.preventDefault();

    const controls = [
      this.companyIdControl,
      this.projectIdControl,
      this.modulesControl,
      this.validUntilControl
    ];

    if (controls.some((control) => control.invalid) || this.isSubmitting()) {
      controls.forEach((control) => control.markAsTouched());
      return;
    }

    const modules = Array.from(
      new Set(
        this.modulesControl.value
          .split(',')
          .map((module) => module.trim())
          .filter(Boolean)
      )
    );
    const validUntil = new Date(this.validUntilControl.value);

    if (modules.length === 0) {
      this.modulesControl.setErrors({ required: true });
      this.modulesControl.markAsTouched();
      return;
    }

    if (Number.isNaN(validUntil.getTime()) || validUntil.getTime() <= Date.now()) {
      this.validUntilControl.setErrors({ futureDate: true });
      this.validUntilControl.markAsTouched();
      return;
    }

    this.formError.set('');
    this.actionMessage.set('');
    this.createdLicense.set(null);
    this.isSubmitting.set(true);

    this.licensesApi
      .createLicense({
        company_id: this.companyIdControl.value,
        project_id: this.projectIdControl.value,
        modules,
        valid_until: validUntil.toISOString()
      })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (license) => {
          this.createdLicense.set(license);
          this.modulesControl.reset();
          this.validUntilControl.setValue(this.defaultValidUntil());
          this.loadLicenses();
        },
        error: (error: unknown) => {
          this.formError.set(this.getErrorMessage(error, 'Lisans oluşturulamadı.'));
        }
      });
  }

  changeBlockedStatus(license: LicenseListItem): void {
    if (this.changingLicenseId() !== null) {
      return;
    }

    const nextBlockedStatus = !license.is_blocked;
    const operationText = nextBlockedStatus ? 'bloklansın' : 'yeniden aktif edilsin';
    const confirmed = window.confirm(
      `${license.company_name} / ${license.project_name} lisansı ${operationText} mı?`
    );

    if (!confirmed) {
      return;
    }

    this.actionMessage.set('');
    this.changingLicenseId.set(license.id);

    this.licensesApi
      .setBlockedStatus(license.id, nextBlockedStatus)
      .pipe(finalize(() => this.changingLicenseId.set(null)))
      .subscribe({
        next: () => {
          this.actionMessage.set(
            nextBlockedStatus ? 'Lisans bloklandı.' : 'Lisans engeli kaldırıldı.'
          );
          this.loadLicenses();
        },
        error: (error: unknown) => {
          this.actionMessage.set(this.getErrorMessage(error, 'Lisans durumu değiştirilemedi.'));
        }
      });
  }

  async copyCreatedLicenseKey(): Promise<void> {
    const license = this.createdLicense();

    if (!license) {
      return;
    }

    try {
      await navigator.clipboard.writeText(license.license_key);
      this.copyMessage.set('Lisans anahtarı kopyalandı.');
    } catch {
      this.copyMessage.set('Kopyalama başarısız; anahtarı elle kopyala.');
    }
  }

  closeCreatedLicense(): void {
    this.createdLicense.set(null);
    this.copyMessage.set('');
  }

  maskLicenseKey(licenseKey: string): string {
    if (licenseKey.length <= 10) {
      return '••••••••';
    }

    return `${licenseKey.slice(0, 3)}••••••${licenseKey.slice(-6)}`;
  }

  licenseStatusLabel(license: LicenseListItem): string {
    const status = this.licenseStatusCode(license);

    if (status === 'blocked') {
      return 'Bloklandı';
    }

    return status === 'expired' ? 'Süresi bitti' : 'Aktif';
  }

  licenseStatusClass(license: LicenseListItem): string {
    return `status ${this.licenseStatusCode(license)}`;
  }

  private licenseStatusCode(license: LicenseListItem): Exclude<LicenseStatusFilter, 'all'> {
    if (license.is_blocked) {
      return 'blocked';
    }

    return new Date(license.valid_until).getTime() <= Date.now() ? 'expired' : 'active';
  }

  private loadReferenceData(): void {
    this.isReferenceLoading.set(true);
    this.referenceError.set('');

    forkJoin({
      companies: this.companiesApi.getCompanies(),
      projects: this.projectsApi.getProjects()
    })
      .pipe(finalize(() => this.isReferenceLoading.set(false)))
      .subscribe({
        next: ({ companies, projects }) => {
          this.companies.set(companies);
          this.projects.set(projects);
        },
        error: (error: unknown) => {
          this.referenceError.set(
            this.getErrorMessage(error, 'Şirket ve proje seçenekleri yüklenemedi.')
          );
        }
      });
  }

  private defaultValidUntil(): string {
    const date = new Date();
    date.setFullYear(date.getFullYear() + 1);
    date.setSeconds(0, 0);

    const offset = date.getTimezoneOffset();
    return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 16);
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
