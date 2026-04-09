/**
 * Integration tests — run with: node src/tests/api.test.js
 *
 * Requires the server to be running on PORT (default 4000) with a seeded DB.
 * Tests run sequentially and share state (login token, created holding id).
 *
 * No test framework needed — uses Node's built-in fetch (Node 18+).
 */

const BASE = `http://localhost:${process.env.PORT || 4000}/api`;
let token = null;
let createdHoldingId = null;
let passed = 0;
let failed = 0;

// ── Assertion helpers ─────────────────────────────────────────────────────────
function assert(condition, message) {
  if (condition) {
    console.log(`  ✓ ${message}`);
    passed++;
  } else {
    console.error(`  ✗ ${message}`);
    failed++;
  }
}

async function req(method, path, body, authToken) {
  const headers = { 'Content-Type': 'application/json' };
  if (authToken) headers.Authorization = `Bearer ${authToken}`;
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  const json = await res.json().catch(() => null);
  return { status: res.status, body: json };
}

// ── Tests ─────────────────────────────────────────────────────────────────────
async function testHealth() {
  console.log('\n── Health check');
  const { status, body } = await req('GET', '/../health');
  assert(status === 200, 'returns 200');
  assert(body.status === 'ok', 'status is ok');
  assert(body.db === 'connected', 'DB connected');
}

async function testAuth() {
  console.log('\n── Auth: register + login');

  // Register a new test user
  const email = `test_${Date.now()}@gwt.dev`;
  const { status: regStatus, body: regBody } = await req('POST', '/auth/register', {
    email, name: 'Test User', password: 'test1234',
  });
  assert(regStatus === 201, 'register returns 201');
  assert(typeof regBody.token === 'string', 'register returns token');

  // Login with same credentials
  const { status: loginStatus, body: loginBody } = await req('POST', '/auth/login', {
    email, password: 'test1234',
  });
  assert(loginStatus === 200, 'login returns 200');
  assert(typeof loginBody.token === 'string', 'login returns token');
  token = loginBody.token;

  // Verify /me endpoint
  const { status: meStatus, body: meBody } = await req('GET', '/auth/me', null, token);
  assert(meStatus === 200, '/me returns 200');
  assert(meBody.email === email, '/me returns correct email');

  // Wrong password
  const { status: badStatus } = await req('POST', '/auth/login', { email, password: 'wrong' });
  assert(badStatus === 401, 'wrong password returns 401');

  // No token
  const { status: noAuthStatus } = await req('GET', '/portfolio');
  assert(noAuthStatus === 401, 'no token returns 401');
}

async function testFundSearch() {
  console.log('\n── Fund search');

  // India search
  const { status, body } = await req('GET', '/funds/search?q=parag+parikh&region=INDIA');
  assert(status === 200, 'India search returns 200');
  assert(Array.isArray(body.results), 'results is array');
  assert(body.results.length > 0, 'found Parag Parikh funds');
  assert(body.results[0].region === 'INDIA', 'results have correct region');
  assert(typeof body.results[0].latestNav === 'number', 'results include NAV');

  // Global search
  const { status: gs, body: gb } = await req('GET', '/funds/search?q=vanguard&region=GLOBAL');
  assert(gs === 200, 'Global search returns 200');
  assert(gb.results.length > 0, 'found Vanguard funds');
  assert(gb.results[0].region === 'GLOBAL', 'global results have correct region');

  // Validation errors
  const { status: shortQ } = await req('GET', '/funds/search?q=a&region=INDIA');
  assert(shortQ === 400, 'query too short returns 400');

  const { status: badRegion } = await req('GET', '/funds/search?q=hdfc&region=INVALID');
  assert(badRegion === 400, 'invalid region returns 400');

  // AMFI ticker search
  const { status: ts, body: tb } = await req('GET', '/funds/search?q=hdfc&region=INDIA');
  assert(ts === 200, 'HDFC search returns 200');
  assert(tb.results.some(f => f.amc.includes('HDFC')), 'HDFC funds found by AMC name');
}

async function testPortfolio() {
  console.log('\n── Portfolio CRUD');

  // Empty portfolio for new user
  const { status: emptyStatus, body: emptyBody } = await req('GET', '/portfolio', null, token);
  assert(emptyStatus === 200, 'GET /portfolio returns 200');
  assert(Array.isArray(emptyBody.holdings), 'holdings is array');
  assert(emptyBody.holdings.length === 0, 'new user has empty portfolio');

  // Ensure fund exists first
  const fund = {
    id: 'GL-VOO',
    region: 'GLOBAL',
    name: 'Vanguard S&P 500 ETF',
    amc: 'Vanguard',
    ticker: 'VOO',
    category: 'ETF - US Large Cap Blend',
  };
  const { status: ensureStatus } = await req('POST', '/funds/ensure', fund, token);
  assert(ensureStatus === 200 || ensureStatus === 201, 'fund ensure succeeds');

  // Add holding
  const { status: addStatus, body: addBody } = await req('POST', '/portfolio/holdings', {
    fundId: 'GL-VOO',
    units: 5.5,
    avgCost: 450.00,
    purchaseAt: '2024-01-15',
  }, token);
  assert(addStatus === 201, 'add holding returns 201');
  assert(addBody.id, 'holding has id');
  createdHoldingId = addBody.id;

  // Portfolio now has one holding
  const { body: withHolding } = await req('GET', '/portfolio', null, token);
  assert(withHolding.holdings.length === 1, 'portfolio has 1 holding');
  const h = withHolding.holdings[0];
  assert(h.ticker === 'VOO', 'correct ticker');
  assert(Number(h.units) === 5.5, 'correct units');
  assert(typeof h.liveNav === 'number' && h.liveNav > 0, 'live NAV is populated');
  assert(typeof h.currentValue === 'number', 'current value calculated');
  assert(typeof h.gainPct === 'number', 'gain% calculated');

  // Auto-consolidation: add more units of the same fund
  const { status: consolidateStatus } = await req('POST', '/portfolio/holdings', {
    fundId: 'GL-VOO',
    units: 2.5,
    avgCost: 470.00,
    purchaseAt: '2024-03-01',
  }, token);
  assert(consolidateStatus === 201, 'consolidation succeeds');

  const { body: consolidated } = await req('GET', '/portfolio', null, token);
  assert(consolidated.holdings.length === 1, 'still 1 holding after consolidation');
  assert(Number(consolidated.holdings[0].units) === 8, 'units consolidated to 8');
  // Weighted avg cost: (5.5×450 + 2.5×470) / 8 = (2475 + 1175) / 8 = 456.25
  assert(Math.abs(Number(consolidated.holdings[0].avgCost) - 456.25) < 0.01, 'weighted avg cost correct');

  // Update holding
  const { status: updateStatus } = await req('PATCH', `/portfolio/holdings/${createdHoldingId}`, {
    units: 10,
    avgCost: 460.00,
  }, token);
  assert(updateStatus === 200, 'update holding returns 200');

  // Delete holding
  const { status: deleteStatus } = await req('DELETE', `/portfolio/holdings/${createdHoldingId}`, null, token);
  assert(deleteStatus === 204, 'delete holding returns 204');

  const { body: afterDelete } = await req('GET', '/portfolio', null, token);
  assert(afterDelete.holdings.length === 0, 'portfolio empty after delete');

  // Can't delete another user's holding
  const { status: wrongUser } = await req('DELETE', `/portfolio/holdings/nonexistent-id`, null, token);
  assert(wrongUser === 500 || wrongUser === 404, 'delete wrong holding returns error');
}

async function testInputValidation() {
  console.log('\n── Input validation');

  const { status: negUnits } = await req('POST', '/portfolio/holdings', {
    fundId: 'GL-VOO', units: -5, purchaseAt: '2024-01-01',
  }, token);
  assert(negUnits === 400, 'negative units rejected');

  const { status: noDate } = await req('POST', '/portfolio/holdings', {
    fundId: 'GL-VOO', units: 5,
  }, token);
  assert(noDate === 400, 'missing date rejected');

  const { status: emptyPatch } = await req('PATCH', `/portfolio/holdings/some-id`, {}, token);
  assert(emptyPatch === 400, 'empty patch body rejected');
}

// ── Runner ────────────────────────────────────────────────────────────────────
async function run() {
  console.log(`\n${'═'.repeat(50)}`);
  console.log('GWT API Integration Tests');
  console.log(`${'═'.repeat(50)}`);

  try {
    await testHealth();
    await testAuth();
    await testFundSearch();
    await testPortfolio();
    await testInputValidation();
  } catch (err) {
    console.error('\n💥 Unexpected test error:', err);
    failed++;
  }

  console.log(`\n${'═'.repeat(50)}`);
  console.log(`Results: ${passed} passed, ${failed} failed`);
  console.log('═'.repeat(50));
  process.exit(failed > 0 ? 1 : 0);
}

run();
