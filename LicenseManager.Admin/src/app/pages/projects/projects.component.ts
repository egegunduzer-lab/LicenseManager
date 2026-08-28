import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ProjectKey, ProjectListItem } from '../../core/models/project.model';
import { ProjectsApiService } from '../../core/services/projects-api.service';

@Component({
  selector: 'app-projects',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.css'
})
export class ProjectsComponent implements OnInit {
  private readonly projectsApi = inject(ProjectsApiService);

  readonly projects = signal<ProjectListItem[]>([]);
  readonly projectKeys = signal<ProjectKey[]>([]);
  readonly selectedProjectId = signal<string | null>(null);
  readonly selectedProject = computed(() =>
    this.projects().find((project) => project.id === this.selectedProjectId()) ?? null
  );

  readonly isLoading = signal(true);
  readonly isSubmitting = signal(false);
  readonly isKeysLoading = signal(false);
  readonly rotationInProgress = signal(false);
  readonly activatingKeyVersion = signal<number | null>(null);

  readonly loadError = signal('');
  readonly formError = signal('');
  readonly formSuccess = signal('');
  readonly rotationError = signal('');
  readonly rotationSuccess = signal('');

  readonly projectNameControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(200)]
  });
  readonly projectVersionControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(50)]
  });

  ngOnInit(): void {
    this.loadProjects();
  }

  loadProjects(): void {
    this.isLoading.set(true);
    this.loadError.set('');

    this.projectsApi
      .getProjects()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (projects) => this.projects.set(projects),
        error: (error: unknown) => {
          this.loadError.set(this.getErrorMessage(error, 'Projeler yüklenemedi.'));
        }
      });
  }

  createProject(event: Event): void {
    event.preventDefault();

    if (
      this.projectNameControl.invalid ||
      this.projectVersionControl.invalid ||
      this.isSubmitting()
    ) {
      this.projectNameControl.markAsTouched();
      this.projectVersionControl.markAsTouched();
      return;
    }

    const name = this.projectNameControl.value.trim();
    const version = this.projectVersionControl.value.trim();

    if (!name || !version) {
      if (!name) {
        this.projectNameControl.setErrors({ required: true });
      }
      if (!version) {
        this.projectVersionControl.setErrors({ required: true });
      }
      return;
    }

    this.formError.set('');
    this.formSuccess.set('');
    this.isSubmitting.set(true);

    this.projectsApi
      .createProject({ name, version })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (project) => {
          this.projectNameControl.reset();
          this.projectVersionControl.reset();
          this.formSuccess.set(`${project.name} ${project.version} oluşturuldu.`);
          this.loadProjects();
        },
        error: (error: unknown) => {
          this.formError.set(this.getErrorMessage(error, 'Proje oluşturulamadı.'));
        }
      });
  }

  manageKeys(project: ProjectListItem): void {
    this.selectedProjectId.set(project.id);
    this.rotationError.set('');
    this.rotationSuccess.set('');
    this.loadProjectKeys(project.id);
  }

  prepareKeyRotation(): void {
    const project = this.selectedProject();

    if (!project || this.rotationInProgress()) {
      return;
    }

    const confirmed = window.confirm(
      `${project.name} ${project.version} için yeni ve pasif bir signing key hazırlansın mı?`
    );

    if (!confirmed) {
      return;
    }

    this.rotationError.set('');
    this.rotationSuccess.set('');
    this.rotationInProgress.set(true);

    this.projectsApi
      .prepareKeyRotation(project.id)
      .pipe(finalize(() => this.rotationInProgress.set(false)))
      .subscribe({
        next: (preparedKey) => {
          this.rotationSuccess.set(
            `Key v${preparedKey.key_version} hazırlandı; henüz aktif değil.`
          );
          this.loadProjectKeys(project.id);
          this.loadProjects();
        },
        error: (error: unknown) => {
          this.rotationError.set(
            this.getErrorMessage(error, 'Yeni signing key hazırlanamadı.')
          );
        }
      });
  }

  activateKey(key: ProjectKey): void {
    const project = this.selectedProject();

    if (!project || key.is_active || this.activatingKeyVersion() !== null) {
      return;
    }

    const confirmed = window.confirm(
      `Key v${key.key_version} aktif edilsin mi? Bundan sonra yeni lisanslar bu anahtarla imzalanacak.`
    );

    if (!confirmed) {
      return;
    }

    this.rotationError.set('');
    this.rotationSuccess.set('');
    this.activatingKeyVersion.set(key.key_version);

    this.projectsApi
      .activateKey(project.id, key.key_version)
      .pipe(finalize(() => this.activatingKeyVersion.set(null)))
      .subscribe({
        next: () => {
          this.rotationSuccess.set(`Key v${key.key_version} aktif edildi.`);
          this.loadProjectKeys(project.id);
          this.loadProjects();
        },
        error: (error: unknown) => {
          this.rotationError.set(this.getErrorMessage(error, 'Signing key aktif edilemedi.'));
        }
      });
  }

  maskPublicKey(publicKey: string): string {
    if (publicKey.length <= 28) {
      return publicKey;
    }

    return `${publicKey.slice(0, 16)}…${publicKey.slice(-10)}`;
  }

  private loadProjectKeys(projectId: string): void {
    this.isKeysLoading.set(true);
    this.projectKeys.set([]);

    this.projectsApi
      .getProjectKeys(projectId)
      .pipe(finalize(() => this.isKeysLoading.set(false)))
      .subscribe({
        next: (keys) => this.projectKeys.set(keys),
        error: (error: unknown) => {
          this.rotationError.set(this.getErrorMessage(error, 'Proje anahtarları yüklenemedi.'));
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
