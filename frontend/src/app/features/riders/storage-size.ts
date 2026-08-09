/**
 * A stored size as a person reads it. The riders page exists to answer where a shared 32 GB is
 * going, and a raw byte count answers that in a unit nobody compares in their head.
 */
export function storageSize(bytes: number): string {
  if (bytes < 1_000) {
    return `${bytes} B`;
  }
  if (bytes < 1_000_000) {
    return `${round(bytes / 1_000)} kB`;
  }
  if (bytes < 1_000_000_000) {
    return `${round(bytes / 1_000_000)} MB`;
  }
  return `${round(bytes / 1_000_000_000)} GB`;
}

/** One decimal, and none when it would be a trailing zero — "3.5 MB", but "12 MB". */
function round(value: number): string {
  return (Math.round(value * 10) / 10).toString();
}
