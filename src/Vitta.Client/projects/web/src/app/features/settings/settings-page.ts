import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  AI_EFFORT_LABELS, AiConnectionTest, AiEffort, AiProviderId, AiProviderInfo, AiSettingsDto, AiSettingsInput,
  problemMessage, SettingsApi, toAiProviderEnum,
} from '@vitta/core';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

const CUSTOM = '__custom__';

@Component({
  selector: 'vt-settings-page',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
})
export class SettingsPage {
  private readonly api = inject(SettingsApi);
  private readonly toast = inject(ToastService);

  protected readonly effortLabels = AI_EFFORT_LABELS;
  protected readonly efforts: AiEffort[] = ['low', 'medium', 'high'];
  protected readonly custom = CUSTOM;

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly testing = signal(false);
  protected readonly data = signal<AiSettingsDto | null>(null);
  protected readonly test = signal<AiConnectionTest | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly provider = signal<AiProviderId>('anthropic');
  /** Modelo escolhido na lista, ou CUSTOM para digitar outro id. */
  protected readonly modelOption = signal('');
  protected readonly customModel = signal('');
  protected readonly effort = signal<AiEffort>('medium');

  protected readonly providers = computed(() => this.data()?.providers ?? []);
  protected readonly current = computed<AiProviderInfo | undefined>(() => this.providers().find(p => p.id === this.provider()));
  protected readonly model = computed(() => (this.modelOption() === CUSTOM ? this.customModel() : this.modelOption()).trim());
  protected readonly selectedNote = computed(() => this.current()?.models.find(m => m.id === this.modelOption())?.note ?? null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.getAi().subscribe({
      next: dto => {
        this.data.set(dto);
        this.provider.set(dto.provider ?? dto.defaultProvider ?? 'anthropic');
        this.effort.set(dto.effort ?? dto.defaultEffort ?? 'medium');
        this.applyModel(dto.model ?? this.current()?.defaultModel ?? '');
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar as configurações.');
      },
    });
  }

  /** Seleciona o modelo na lista quando ele existe nas sugestões; senão, vai para "Outro modelo". */
  private applyModel(model: string): void {
    const known = this.current()?.models.some(m => m.id === model);
    if (known || !model) {
      this.modelOption.set(model || (this.current()?.models[0]?.id ?? ''));
      this.customModel.set('');
    } else {
      this.modelOption.set(CUSTOM);
      this.customModel.set(model);
    }
  }

  protected selectProvider(id: AiProviderId): void {
    if (this.provider() === id) return;
    this.provider.set(id);
    this.test.set(null);
    const p = this.current();
    this.applyModel(p?.models.find(m => m.id === p.defaultModel)?.id ?? p?.models[0]?.id ?? p?.defaultModel ?? '');
  }

  protected setModelOption(value: string): void {
    this.modelOption.set(value);
    this.test.set(null);
  }

  private input(): AiSettingsInput | null {
    const model = this.model();
    if (!model) {
      this.error.set('Escolha ou digite o modelo.');
      return null;
    }
    this.error.set(null);
    return { provider: toAiProviderEnum(this.provider()), model, effort: this.effort() };
  }

  protected runTest(): void {
    const input = this.input();
    if (!input) return;
    this.testing.set(true);
    this.test.set(null);
    this.api.testAi(input).subscribe({
      next: result => {
        this.test.set(result);
        this.testing.set(false);
      },
      error: err => {
        this.testing.set(false);
        this.error.set(problemMessage(err, 'Não foi possível testar a conexão.'));
      },
    });
  }

  protected save(): void {
    const input = this.input();
    if (!input) return;
    this.saving.set(true);
    this.api.saveAi(input).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Configuração de IA salva.');
        this.data.update(d => (d ? { ...d, provider: this.provider(), model: input.model, effort: input.effort, isCustom: true } : d));
      },
      error: err => {
        this.saving.set(false);
        this.error.set(problemMessage(err, 'Não foi possível salvar.'));
      },
    });
  }
}
