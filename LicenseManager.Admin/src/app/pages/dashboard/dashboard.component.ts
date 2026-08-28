import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { DashboardResponse, RecentLicense } from '../../core/models/dashboard.model';
import { DashboardApiService } from '../../core/services/dashboard-api.service';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  private readonly dashboardApi = inject(DashboardApiService);

  readonly dashboard = signal<DashboardResponse | null>(null);
  readonly isLoading = signal(true);
  readonly errorMessage = signal('');

  readonly summaryCards = computed(() => {
    const summary = this.dashboard()?.summary;

    if (!summary) {
      return [];
    }

    return [
      { label: 'Toplam şirket', value: summary.total_companies, tone: 'neutral' },
      { label: 'Toplam proje', value: summary.total_projects, tone: 'neutral' },
      { label: 'Toplam lisans', value: summary.total_licenses, tone: 'neutral' },
      { label: 'Aktif lisans', value: summary.active_licenses, tone: 'success' },
      { label: 'Bloklanan', value: summary.blocked_licenses, tone: 'danger' },
      { label: 'Süresi biten', value: summary.expired_licenses, tone: 'danger' },
      {
        label: 'Signed · 7 gün',
        value: summary.signed_expiring_within_7_days,
        tone: 'warning'
      },
      {
        label: 'Gerçek süre · 30 gün',
        value: summary.real_expiring_within_30_days,
        tone: 'warning'
      }
    ];
  });

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.dashboardApi
      .getDashboard()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (dashboard) => this.dashboard.set(dashboard),
        error: (error: unknown) => this.errorMessage.set(this.getErrorMessage(error))
      });
  }

  maskLicenseKey(licenseKey: string): string {
    if (licenseKey.length <= 10) {
      return '••••••••';
    }

    return `${licenseKey.slice(0, 3)}••••••${licenseKey.slice(-6)}`;
  }

  licenseStatus(license: RecentLicense): string {
    if (license.is_blocked) {
      return 'Bloklandı';
    }

    return new Date(license.valid_until).getTime() <= Date.now()
      ? 'Süresi bitti'
      : 'Aktif';
  }

  licenseStatusClass(license: RecentLicense): string {
    if (license.is_blocked || new Date(license.valid_until).getTime() <= Date.now()) {
      return 'status danger';
    }

    return 'status success';
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'API’ye ulaşılamadı. API’nin çalıştığını, CORS ayarını ve HTTPS sertifikasını kontrol et.';
    }

    return 'Dashboard verileri yüklenemedi. Lütfen tekrar dene.';
  }

}
