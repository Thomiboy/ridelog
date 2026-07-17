/** Whether a Polar account is linked, since when, and when the last sync ran. */
export interface PolarStatus {
  linked: boolean;
  connectedAt?: string | null;
  lastSyncAt?: string | null;
}

/** Result of a Polar sync run. */
export interface SyncSummary {
  imported: number;
  skipped: number;
  failed: number;
}

export type ImportOutcome = 'Imported' | 'Skipped' | 'Failed';

export interface FileImportResult {
  fileName: string;
  outcome: ImportOutcome;
  error?: string;
}

/** Result of a GPX/TCX bulk import. */
export interface ImportSummary {
  files: FileImportResult[];
  imported: number;
  skipped: number;
  failed: number;
}
