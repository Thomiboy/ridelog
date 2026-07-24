/** Result of a Polar sync run. */
export interface SyncSummary {
  imported: number;
  skipped: number;
  failed: number;
}

/** Whether a Polar account is linked, since when, when the last sync ran, and its outcome. */
export interface PolarStatus {
  linked: boolean;
  connectedAt?: string | null;
  lastSyncAt?: string | null;
  lastSyncResult?: SyncSummary | null;
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

/** Result of reprocessing stored rides from their raw files. */
export interface ReprocessSummary {
  processed: number;
  failed: number;
}

/** Editable user settings. Currently just the max heart rate anchoring the HR zones. */
export interface UserSettings {
  maxHeartRate: number | null;
}
