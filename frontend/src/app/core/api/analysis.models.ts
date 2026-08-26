/** Which language a reading was written in; the API sends the name, never an ordinal. */
export type AnalysisLanguage = 'Hungarian' | 'English';

/** One stored monthly analysis, mirroring the backend MonthlyAnalysisDto (#187). */
export interface MonthlyAnalysisResult {
  id: string;
  year: number;
  month: number;
  language: AnalysisLanguage;
  text: string;
  model: string;
  /** How many rides the month held when this was written; the running month may be written again once it changes. */
  rideCount: number;
  writtenAt: string;
}

/** Why the API turned a request down. Named, because the page has different things to say about each. */
export type AnalysisRefusal = 'AlreadyWritten' | 'Unchanged' | 'Unavailable';
