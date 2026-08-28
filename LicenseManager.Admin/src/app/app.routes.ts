import { Routes } from '@angular/router';
import { adminAuthGuard } from './core/guards/admin-auth.guard';
import { AdminShellComponent } from './layout/admin-shell/admin-shell.component';
import { CompaniesComponent } from './pages/companies/companies.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { LicensesComponent } from './pages/licenses/licenses.component';
import { LoginComponent } from './pages/login/login.component';
import { ProjectsComponent } from './pages/projects/projects.component';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent,
    title: 'Yönetici Girişi | License Manager'
  },
  {
    path: '',
    component: AdminShellComponent,
    canActivate: [adminAuthGuard],
    children: [
      {
        path: 'dashboard',
        component: DashboardComponent,
        title: 'Dashboard | License Manager'
      },
      {
        path: 'companies',
        component: CompaniesComponent,
        title: 'Şirketler | License Manager'
      },
      {
        path: 'projects',
        component: ProjectsComponent,
        title: 'Projeler | License Manager'
      },
      {
        path: 'licenses',
        component: LicensesComponent,
        title: 'Lisanslar | License Manager'
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard'
      }
    ]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
