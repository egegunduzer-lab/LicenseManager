import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AdminSessionService } from '../services/admin-session.service';

export const adminAuthGuard: CanActivateFn = () => {
  const adminSession = inject(AdminSessionService);
  const router = inject(Router);

  return adminSession.isAuthenticated()
    ? true
    : router.createUrlTree(['/login']);
};
