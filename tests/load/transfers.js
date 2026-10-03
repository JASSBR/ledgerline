// Load test: transfers and reads through the gateway, then the bank's invariants checked after the storm.
// Runs inside the kind cluster (see run-in-kind.sh), so traffic goes through the Kubernetes Services to every replica.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const GATEWAY = __ENV.GATEWAY ?? 'http://gateway:8080';
const KEYCLOAK = __ENV.KEYCLOAK ?? 'http://keycloak:8080';
const RATE = Number(__ENV.RATE ?? 25);
const DURATION = __ENV.DURATION ?? '60s';

const replayMismatch = new Counter('idempotency_replay_mismatch');
let reported = 0;
function report(response, what) {
  // A few samples of anything unexpected, enough to diagnose without flooding the output.
  if (reported++ < 5) console.warn(`${what}: ${response.status} ${response.error ?? ''} ${String(response.body ?? '').slice(0, 200)}`);
}
const transferAccepted = new Trend('transfer_accepted_ms', true);

export const options = {
  teardownTimeout: '3m',
  scenarios: {
    transfers: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 50,
      exec: 'transfer',
    },
    reads: {
      executor: 'constant-vus',
      vus: 30,
      duration: DURATION,
      exec: 'read',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{kind:read}': ['p(95)<250'],
    transfer_accepted_ms: ['p(95)<400'],
    idempotency_replay_mismatch: ['count==0'],
  },
};

function token(username) {
  const response = http.post(
    `${KEYCLOAK}/realms/ledgerline/protocol/openid-connect/token`,
    { grant_type: 'password', client_id: 'ledgerline-smoke', username, password: 'ledgerline-demo' },
  );
  return response.json('access_token');
}

function auth(accessToken) {
  return { headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' } };
}

export function setup() {
  const users = {};
  for (const name of ['alice', 'bob', 'olivia']) {
    users[name] = { token: token(name) };
  }
  for (const name of ['alice', 'bob']) {
    const accounts = http.get(`${GATEWAY}/api/ledger/accounts`, auth(users[name].token)).json();
    const current = accounts.find((account) => account.name.includes('courant'));
    users[name].accountId = current.id;
    users[name].iban = current.iban;
  }
  return users;
}

// Alice and Bob pay each other 1 €: balances stay stable however long the test runs.
export function transfer(users) {
  const [from, to] = Math.random() < 0.5 ? ['alice', 'bob'] : ['bob', 'alice'];
  const key = uuidv4();
  const body = JSON.stringify({ fromAccountId: users[from].accountId, toIban: users[to].iban, amount: 1, label: 'k6' });
  const params = auth(users[from].token);
  params.headers['Idempotency-Key'] = key;
  params.tags = { kind: 'transfer' };

  const response = http.post(`${GATEWAY}/api/payments/transfers`, body, params);
  transferAccepted.add(response.timings.duration);
  if (!check(response, { 'transfer accepted (202)': (r) => r.status === 202 })) report(response, 'transfer');

  // One request in ten is a client retry with the same key: it must return the same transfer, flagged as a replay.
  if (Math.random() < 0.1) {
    const replay = http.post(`${GATEWAY}/api/payments/transfers`, body, params);
    const same = replay.status === 200 && replay.headers['Idempotent-Replayed'] === 'true' && replay.json('id') === response.json('id');
    if (!same) replayMismatch.add(1);
  }
}

export function read(users) {
  const user = Math.random() < 0.5 ? users.alice : users.bob;
  const params = { ...auth(user.token), tags: { kind: 'read' } };
  for (const url of [`${GATEWAY}/api/ledger/accounts`, `${GATEWAY}/api/payments/transfers?limit=20`]) {
    const response = http.get(url, params);
    if (response.status !== 200) report(response, url);
  }
  sleep(0.2);
}

// After the storm: no transfer stuck mid-saga, and the books still sum to exactly zero.
export function teardown(users) {
  const operator = auth(token('olivia'));
  let inFlight = -1;
  for (let attempt = 0; attempt < 60 && inFlight !== 0; attempt++) {
    const transfers = http.get(`${GATEWAY}/api/payments/transfers?limit=200`, operator).json();
    inFlight = transfers.filter((t) => ['Reserving', 'Screening', 'Capturing', 'Releasing'].includes(t.status)).length;
    if (inFlight !== 0) sleep(2);
  }
  const books = http.get(`${GATEWAY}/api/ledger/trial-balance`, operator).json();
  console.log(`after the run: ${inFlight} transfers mid-saga, trial balance total ${books.total}, ${books.journalEntries} entries`);
  check(books, {
    'no transfer stuck mid-saga': () => inFlight === 0,
    'books sum to zero': (b) => b.balanced && b.total === 0,
  });
}
