import { Pipe, PipeTransform } from '@angular/core';
import { changeTypeLabel, formatCurrency, formatDate, formatMonth, policyStatusLabel, prospectStageLabel } from '@vitta/core';

@Pipe({ name: 'brl' })
export class BrlPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return formatCurrency(value);
  }
}

@Pipe({ name: 'isoDate' })
export class IsoDatePipe implements PipeTransform {
  transform(value: string | null | undefined, fallback = '—'): string {
    return formatDate(value) || fallback;
  }
}

@Pipe({ name: 'month' })
export class MonthPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return formatMonth(value);
  }
}

@Pipe({ name: 'policyStatus' })
export class PolicyStatusPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return policyStatusLabel(value);
  }
}

@Pipe({ name: 'stage' })
export class StagePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return prospectStageLabel(value);
  }
}

@Pipe({ name: 'changeType' })
export class ChangeTypePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return changeTypeLabel(value);
  }
}
