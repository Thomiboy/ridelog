/** What the public contact page renders from: whether the form is on, and the owner address when off. */
export interface ContactConfig {
  enabled: boolean;
  ownerEmail: string | null;
}

/** A contact submission. `website` is the honeypot — a real visitor leaves it empty. */
export interface ContactSubmission {
  name: string;
  email: string;
  message: string;
  website?: string;
}

/** One stored message in the owner's list. */
export interface ContactMessage {
  id: string;
  senderName: string;
  senderEmail: string;
  message: string;
  submittedAt: string;
}
