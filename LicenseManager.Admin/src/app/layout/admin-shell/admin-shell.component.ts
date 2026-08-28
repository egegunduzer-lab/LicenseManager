import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminSessionService } from '../../core/services/admin-session.service';

@Component({
  selector: 'app-admin-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.css'
})
export class AdminShellComponent {
  private readonly adminSession = inject(AdminSessionService);
  private readonly router = inject(Router);

  logout(): void {
    this.adminSession.clear();
    void this.router.navigate(['/login']);
  }
}
