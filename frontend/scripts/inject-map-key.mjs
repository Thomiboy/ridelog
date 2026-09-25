// Writes the CARTO basemap key into the production environment at build time (#193).
//
// A client-side tile key cannot be secret: it ships in the bundle and shows in devtools on the
// deployed site. The real protection is the domain restriction set at CARTO, so this script is not
// hiding anything — it keeps a credential out of a public repo and makes rotation a secret update
// rather than a commit, which is how every other credential here is handled.
//
// Without MAP_API_KEY it leaves the empty default alone and says so. That keeps `build-and-test`
// working where the secret is not available, and an empty key draws no basemap rather than a wrong
// one (see setTileLayer).
import { readFileSync, writeFileSync } from 'node:fs';

const FILE = new URL('../src/environments/environment.ts', import.meta.url);
const PLACEHOLDER = "mapApiKey: ''";

const key = process.env.MAP_API_KEY?.trim();
const source = readFileSync(FILE, 'utf8');

// Fail loudly rather than shipping a keyless build: if this line is ever renamed or reformatted, the
// substitution would silently do nothing and the basemap would quietly disappear in production. That
// class of silent miss is exactly what #191 and #193 cost, so it is an error, not a warning.
if (!source.includes(PLACEHOLDER)) {
  console.error(
    `inject-map-key: could not find ${PLACEHOLDER} in environment.ts. ` +
      'The placeholder moved — fix this script rather than shipping a build with no basemap.',
  );
  process.exit(1);
}

if (!key) {
  console.warn('inject-map-key: MAP_API_KEY is not set; building with no basemap key.');
  process.exit(0);
}

writeFileSync(FILE, source.replace(PLACEHOLDER, `mapApiKey: '${key}'`));
console.log('inject-map-key: basemap key written into environment.ts.');
