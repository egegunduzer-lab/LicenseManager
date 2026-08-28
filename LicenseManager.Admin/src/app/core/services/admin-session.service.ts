import { Injectable, computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AdminSessionService {
  private readonly apiKey = signal<string | null>(null);

  readonly isAuthenticated = computed(() => this.apiKey() !== null);

  setApiKey(value: string): void {
    const normalizedValue = value.trim();

    if (!normalizedValue) {
      throw new Error('Admin API key boş olamaz.');
    }

    this.apiKey.set(normalizedValue);
  }

  getApiKey(): string | null {
    return this.apiKey();
  }

  clear(): void {
    this.apiKey.set(null);
  }
}
