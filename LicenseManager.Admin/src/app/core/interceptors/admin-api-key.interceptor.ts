import { HttpErrorResponse, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminSessionService } from '../services/admin-session.service';

export function adminApiKeyInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  const adminSession = inject(AdminSessionService);
  const router = inject(Router);
  const apiKey = adminSession.getApiKey();
  const isAdminApiRequest = req.url.startsWith(`${environment.apiBaseUrl}/api/`);

  const authenticatedRequest = isAdminApiRequest && apiKey
    ? req.clone({
        setHeaders: {
          'X-Admin-Api-Key': apiKey
        }
      })
    : req;

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (isAdminApiRequest && error instanceof HttpErrorResponse && error.status === 401) {
        adminSession.clear();
        void router.navigate(['/login'], {
          queryParams: { reason: 'unauthorized' }
        });
      }

      return throwError(() => error);
    })
  );
}
