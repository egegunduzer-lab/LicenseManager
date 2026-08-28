import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AdminSessionService } from '../../core/services/admin-session.service';
import { DashboardApiService } from '../../core/services/dashboard-api.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  private readonly adminSession = inject(AdminSessionService);
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly apiKeyControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required]
  });
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal(
    this.route.snapshot.queryParamMap.get('reason') === 'unauthorized'
      ? 'Admin API key geçersiz veya süresi dolmuş.'
      : ''
  );

  submit(event: Event): void {
    event.preventDefault();

    if (this.apiKeyControl.invalid || this.isSubmitting()) {
      this.apiKeyControl.markAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.isSubmitting.set(true);
    this.adminSession.setApiKey(this.apiKeyControl.value);

    this.dashboardApi
      .getDashboard()
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => void this.router.navigate(['/dashboard']),
        error: (error: unknown) => {
          this.adminSession.clear();
          this.errorMessage.set(this.getErrorMessage(error));
        }
      });
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'API’ye ulaşılamadı. API’nin çalıştığını ve HTTPS sertifikasını kontrol et.';
    }

    if (error instanceof HttpErrorResponse && error.status === 401) {
      return 'Admin API key geçersiz.';
    }

    return 'Giriş kontrolü tamamlanamadı. Lütfen tekrar dene.';
  }
}
