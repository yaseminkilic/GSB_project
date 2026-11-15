import http from 'k6/http';
import { check, sleep } from 'k6';

// Read-only scenario. Explicit target and existing event prevent accidental writes.
if (!__ENV.BASE_URL || !__ENV.EVENT_ID || !__ENV.API_TOKEN || !/^\d+$/.test(__ENV.EVENT_ID)) {
  throw new Error('Provide BASE_URL, API_TOKEN and an existing numeric EVENT_ID for a dedicated test environment.');
}
export const options = {
  scenarios: { reads: { executor: 'constant-vus', vus: 5, duration: '30s' } },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
    checks: ['rate>0.99'],
  },
};
export default function () {
  const response = http.get(`${__ENV.BASE_URL.replace(/\/$/, '')}/api/events/${__ENV.EVENT_ID}`, { headers: { Authorization: `Bearer ${__ENV.API_TOKEN}` } });
  check(response, {
    'GET succeeds': r => r.status === 200,
    'correct event': r => r.status === 200 && String(r.json('id')) === __ENV.EVENT_ID,
    'no-store': r => (r.headers['Cache-Control'] || '').includes('no-store'),
  });
  sleep(0.1);
}

