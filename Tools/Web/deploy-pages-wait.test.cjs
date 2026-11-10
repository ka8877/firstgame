'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { deployPages } = require('./deploy-pages-wait.cjs');

const ROUTES = {
  artifacts: 'GET /repos/{owner}/{repo}/actions/runs/{run_id}/artifacts',
  create: 'POST /repos/{owner}/{repo}/pages/deployments',
  status: 'GET /repos/{owner}/{repo}/pages/deployments/{deploymentId}',
  cancel: 'POST /repos/{owner}/{repo}/pages/deployments/{deploymentId}/cancel',
};
const CONTEXT = {
  repo: { owner: 'ka8877', repo: 'firstgame' },
  runId: 42764028487,
  sha: 'bf21d639fff9e109be9cf061a81ea71f3661cb58',
};
const PAGE_URL = 'https://ka8877.github.io/firstgame/';
const OIDC = 'synthetic-oidc-never-write-to-log';
const ARTIFACT_LIMIT = 1024 * 1024 * 1024;

function artifact(overrides = {}) {
  return {
    id: 8181,
    name: 'github-pages',
    expired: false,
    size_in_bytes: 982794532,
    workflow_run: { id: CONTEXT.runId, head_sha: CONTEXT.sha },
    ...overrides,
  };
}

function upstreamError(status) {
  const error = new Error('untrusted upstream payload: ' + OIDC);
  error.status = status;
  error.response = { status, data: { message: OIDC }, headers: {} };
  return error;
}

function fixture(options = {}) {
  let elapsed = 0;
  const calls = [];
  const outputs = new Map();
  const logs = [];
  const secrets = [];
  const sleeps = [];
  const counts = { artifacts: 0, create: 0, status: 0, cancel: 0, oidc: 0 };
  const created = options.created === undefined
    ? { id: CONTEXT.sha, page_url: PAGE_URL }
    : options.created;

  async function response(selected, index, parameters, fallback) {
    let value = selected === undefined ? fallback : selected;
    if (Array.isArray(value)) value = value[Math.min(index, value.length - 1)];
    if (typeof value === 'function') value = await value({ index, parameters, elapsed });
    if (value instanceof Error) throw value;
    return { data: value };
  }

  const github = {
    async request(route, parameters) {
      calls.push({ route, parameters });
      if (route === ROUTES.artifacts) {
        return response(options.artifacts, counts.artifacts++, parameters, {
          total_count: 1,
          artifacts: [artifact()],
        });
      }
      if (route === ROUTES.create) {
        return response(created, counts.create++, parameters, undefined);
      }
      if (route === ROUTES.status) {
        return response(options.poll, counts.status++, parameters, { status: 'succeed' });
      }
      if (route === ROUTES.cancel) {
        return response(options.cancel, counts.cancel++, parameters, {});
      }
      assert.fail('Unexpected GitHub route: ' + route);
    },
  };
  const core = {
    async getIDToken() {
      counts.oidc++;
      if (options.oidcError) throw options.oidcError;
      return OIDC;
    },
    setOutput(name, value) { outputs.set(name, value); },
    setSecret(value) { secrets.push(value); },
    info(message) { logs.push(['info', String(message)]); },
    warning(message) { logs.push(['warning', String(message)]); },
    error(message) { logs.push(['error', String(message)]); },
    debug(message) { logs.push(['debug', String(message)]); },
    setFailed(message) { logs.push(['failed', String(message)]); },
  };
  const clock = {
    now() { return elapsed; },
    async sleep(milliseconds) {
      assert.ok(Number.isFinite(milliseconds) && milliseconds > 0, 'wait must advance time');
      sleeps.push(milliseconds);
      elapsed += milliseconds;
    },
  };
  return {
    github, core, clock, calls, outputs, logs, secrets, sleeps, counts,
    options: { github, core, clock, context: options.context || CONTEXT },
    elapsed: () => elapsed,
  };
}

function noSuccess(run) {
  assert.notEqual(run.outputs.get('status'), 'succeed');
  assert.equal(run.outputs.has('page_url'), false);
  assert.equal(JSON.stringify(run.logs).includes(OIDC), false, 'never log OIDC or raw upstream errors');
}

function canceledOnce(run) {
  const canceled = run.calls.filter(call => call.route === ROUTES.cancel);
  assert.equal(canceled.length, 1);
  assert.equal(canceled[0].parameters.owner, CONTEXT.repo.owner);
  assert.equal(canceled[0].parameters.repo, CONTEXT.repo.repo);
  assert.equal(canceled[0].parameters.deploymentId, CONTEXT.sha);
}

test('waits beyond ten minutes and succeeds only after the actual succeed status', async () => {
  const run = fixture({
    poll: ({ elapsed }) => ({ status: elapsed < 615000 ? 'deployment_in_progress' : 'succeed' }),
  });
  const result = await deployPages(run.options);
  assert.ok(run.elapsed() > 600000);
  assert.ok(run.elapsed() < 1800000);
  assert.deepEqual(result, { id: CONTEXT.sha, page_url: PAGE_URL, status: 'succeed' });
  assert.deepEqual(Object.fromEntries(run.outputs), { page_url: PAGE_URL, status: 'succeed' });
  assert.ok(run.sleeps.every(milliseconds => milliseconds <= 5000));
  assert.equal(run.counts.cancel, 0);
  assert.equal(run.counts.oidc, 1);

  const listed = run.calls.filter(call => call.route === ROUTES.artifacts);
  assert.equal(listed.length, 1);
  assert.equal(listed[0].parameters.owner, CONTEXT.repo.owner);
  assert.equal(listed[0].parameters.repo, CONTEXT.repo.repo);
  assert.equal(listed[0].parameters.run_id, CONTEXT.runId);
  assert.equal(listed[0].parameters.per_page, 100);
  const post = run.calls.find(call => call.route === ROUTES.create);
  assert.equal(post.parameters.owner, CONTEXT.repo.owner);
  assert.equal(post.parameters.repo, CONTEXT.repo.repo);
  assert.equal(post.parameters.artifact_id, 8181);
  assert.equal(post.parameters.pages_build_version, CONTEXT.sha);
  assert.equal(post.parameters.oidc_token, OIDC);
  assert.ok(run.calls.filter(call => call.route === ROUTES.status)
    .every(call => call.parameters.deploymentId === CONTEXT.sha));
  assert.equal(JSON.stringify(run.logs).includes(OIDC), false);
});

test('all known pending states keep waiting without publishing outputs', async () => {
  const pending = ['', undefined, null, 'unknown_status', 'not_found', 'deployment_attempt_error',
    'deployment_in_progress', 'queued', 'pending', 'syncing_files', 'updating_pages', 'waiting'];
  const run = fixture({ poll: pending.map(status => ({ status })).concat({ status: 'succeed' }) });
  const originalRequest = run.github.request;
  run.github.request = async (route, parameters) => {
    if (route === ROUTES.status) assert.equal(run.outputs.size, 0);
    return originalRequest(route, parameters);
  };
  await deployPages(run.options);
  assert.equal(run.counts.status, pending.length + 1);
  assert.equal(run.outputs.get('status'), 'succeed');
  assert.equal(run.counts.cancel, 0);
});

test('finds the current-run artifact on a later page instead of selecting unrelated artifacts', async () => {
  const firstPage = Array.from({ length: 100 }, (_, index) => artifact({ id: index + 1, name: 'other-' + index }));
  const run = fixture({ artifacts: ({ parameters }) => ({
    total_count: 101,
    artifacts: parameters.page === 2 ? [artifact()] : firstPage,
  }) });
  await deployPages(run.options);
  assert.deepEqual(run.calls.filter(call => call.route === ROUTES.artifacts)
    .map(call => call.parameters.page), [1, 2]);
  assert.equal(run.calls.find(call => call.route === ROUTES.create).parameters.artifact_id, 8181);
});

test('rejects duplicate matching artifacts across pagination', async () => {
  const firstPage = [artifact(), ...Array.from({ length: 99 }, (_, index) => artifact({ id: index + 1, name: 'other-' + index }))];
  const run = fixture({ artifacts: ({ parameters }) => ({
    total_count: 101,
    artifacts: parameters.page === 2 ? [artifact({ id: 8182 })] : firstPage,
  }) });
  await assert.rejects(deployPages(run.options));
  assert.equal(run.counts.create, 0);
  assert.equal(run.counts.oidc, 0);
  noSuccess(run);
});

test('rejects missing, wrong, ambiguous, expired, oversized and malformed artifacts', async t => {
  const cases = [
    ['empty', []],
    ['wrong name', [artifact({ name: 'unrelated' })]],
    ['duplicate', [artifact(), artifact({ id: 8182 })]],
    ['expired', [artifact({ expired: true })]],
    ['oversize', [artifact({ size_in_bytes: ARTIFACT_LIMIT + 1 })]],
    ['zero size', [artifact({ size_in_bytes: 0 })]],
    ['nonfinite size', [artifact({ size_in_bytes: Infinity })]],
    ['noninteger id', [artifact({ id: 1.5 })]],
    ['nonpositive id', [artifact({ id: 0 })]],
    ['missing workflow run', [artifact({ workflow_run: undefined })]],
    ['wrong workflow run', [artifact({ workflow_run: { id: CONTEXT.runId + 1, head_sha: CONTEXT.sha } })]],
    ['missing workflow head', [artifact({ workflow_run: { id: CONTEXT.runId } })]],
    ['wrong workflow head', [artifact({ workflow_run: { id: CONTEXT.runId, head_sha: 'a'.repeat(40) } })]],
  ];
  for (const [name, artifacts] of cases) {
    await t.test(name, async () => {
      const run = fixture({ artifacts: { total_count: artifacts.length, artifacts } });
      await assert.rejects(deployPages(run.options));
      assert.equal(run.counts.create, 0);
      assert.equal(run.counts.oidc, 0);
      assert.equal(run.counts.cancel, 0);
      noSuccess(run);
    });
  }
});

test('rejects invalid context before creating a deployment', async t => {
  const contexts = [
    { ...CONTEXT, repo: { ...CONTEXT.repo, owner: '../outside' } },
    { ...CONTEXT, repo: { ...CONTEXT.repo, repo: '' } },
    { ...CONTEXT, runId: 0 },
    { ...CONTEXT, runId: -1 },
    { ...CONTEXT, sha: 'not-a-commit' },
  ];
  for (const context of contexts) {
    await t.test(JSON.stringify(context), async () => {
      const run = fixture({ context });
      await assert.rejects(deployPages(run.options));
      assert.equal(run.counts.create, 0);
      assert.equal(run.counts.oidc, 0);
      noSuccess(run);
    });
  }
});

test('requires well-formed artifact-list totals and rows', async t => {
  const responses = [
    { artifacts: [artifact()] },
    { total_count: -1, artifacts: [artifact()] },
    { total_count: 1.5, artifacts: [artifact()] },
    { total_count: Infinity, artifacts: [artifact()] },
    { total_count: 1, artifacts: null },
    { total_count: 1, artifacts: [null] },
  ];
  for (let index = 0; index < responses.length; index++) {
    await t.test('malformed list ' + index, async () => {
      const run = fixture({ artifacts: responses[index] });
      await assert.rejects(deployPages(run.options));
      assert.equal(run.counts.create, 0);
      assert.equal(run.counts.oidc, 0);
      noSuccess(run);
    });
  }
});

test('accepts the artifact byte ceiling and a numeric deployment id', async () => {
  const run = fixture({
    artifacts: { total_count: 1, artifacts: [artifact({ size_in_bytes: ARTIFACT_LIMIT })] },
    created: { id: 99, page_url: PAGE_URL },
  });
  const result = await deployPages(run.options);
  assert.equal(result.id, 99);
  assert.equal(run.calls.find(call => call.route === ROUTES.status).parameters.deploymentId, 99);
});

test('terminal failure statuses cancel and never become success', async t => {
  for (const status of ['deployment_failed', 'deployment_perms_error', 'deployment_content_failed',
    'deployment_cancelled', 'deployment_lost', 'errored', 'error', 'canceled', 'failed', 'cancelled']) {
    await t.test(status, async () => {
      const run = fixture({ poll: { status } });
      await assert.rejects(deployPages(run.options));
      canceledOnce(run);
      noSuccess(run);
    });
  }
});

test('unrecognized service states remain pending until the fixed deadline', async t => {
  for (const status of ['unknown', 'success', 'future-service-state']) {
    await t.test(status, async () => {
      const run = fixture({ poll: { status } });
      await assert.rejects(deployPages({ ...run.options, timeoutMs: 15000 }));
      assert.ok(run.elapsed() >= 15000, 'unrecognized state must get a bounded chance to progress');
      canceledOnce(run);
      noSuccess(run);
    });
  }
});

test('blank and temporary service states for four minutes may progress to real success', async () => {
  const temporary = ['', undefined, 'unknown_status', 'not_found', 'deployment_attempt_error'];
  const run = fixture({ poll: ({ index, elapsed }) => ({
    status: elapsed < 240000 ? temporary[index % temporary.length]
      : elapsed < 250000 ? 'syncing_files' : 'succeed',
  }) });
  await deployPages(run.options);
  assert.ok(run.elapsed() >= 250000);
  assert.equal(run.outputs.get('status'), 'succeed');
  assert.equal(run.counts.cancel, 0);
});

test('malformed status responses fail and cancel rather than treating arbitrary data as pending', async t => {
  for (const poll of [null, [], 'succeed', { status: 1 }, { status: true }, { status: {} }]) {
    await t.test(JSON.stringify(poll), async () => {
      const run = fixture({ poll });
      await assert.rejects(deployPages(run.options));
      canceledOnce(run);
      noSuccess(run);
    });
  }
});

test('nine retryable poll errors may recover to actual success', async () => {
  const run = fixture({
    poll: Array.from({ length: 9 }, (_, index) => upstreamError(index % 2 ? 429 : 503))
      .concat({ status: 'succeed' }),
  });
  await deployPages(run.options);
  assert.equal(run.counts.status, 10);
  assert.equal(run.outputs.get('status'), 'succeed');
  assert.equal(run.counts.cancel, 0);
  assert.equal(JSON.stringify(run.logs).includes(OIDC), false);
});

test('the tenth cumulative retryable failure cancels, even across successful pending polls', async () => {
  const run = fixture({
    artifacts: [upstreamError(503), { total_count: 1, artifacts: [artifact()] }],
    poll: Array.from({ length: 18 }, (_, index) => index % 2 ? { status: 'pending' } : upstreamError(429)),
  });
  await assert.rejects(deployPages({ ...run.options, maxErrors: 100 }));
  assert.equal(run.counts.artifacts, 2);
  assert.equal(run.counts.status, 17, 'one list failure plus nine poll failures must reach the fixed total of ten');
  canceledOnce(run);
  noSuccess(run);
});

test('retryable artifact-list errors also stop on the tenth failure before any deployment', async () => {
  const run = fixture({ artifacts: upstreamError(503) });
  await assert.rejects(deployPages(run.options));
  assert.equal(run.counts.artifacts, 10);
  assert.equal(run.counts.create, 0);
  assert.equal(run.counts.cancel, 0);
  noSuccess(run);
});

test('authentication failures are not retried and do not expose upstream payloads', async t => {
  for (const route of ['artifacts', 'poll']) {
    await t.test(route, async () => {
      const run = fixture({ [route]: upstreamError(401) });
      await assert.rejects(deployPages(run.options), error => !String(error.message).includes(OIDC));
      assert.equal(run.counts[route === 'poll' ? 'status' : 'artifacts'], 1);
      if (route === 'poll') canceledOnce(run);
      else assert.equal(run.counts.cancel, 0);
      noSuccess(run);
    });
  }
});

test('honors numeric and HTTP-date Retry-After without exceeding the remaining deadline', async t => {
  for (const retryAfter of ['12', 'Thu, 01 Jan 1970 00:00:12 GMT']) {
    await t.test(retryAfter, async () => {
      const retryable = upstreamError(429);
      retryable.response.headers['retry-after'] = retryAfter;
      const run = fixture({ poll: [retryable, { status: 'succeed' }] });
      await deployPages({ ...run.options, timeoutMs: 20000 });
      if (retryAfter === '12') assert.ok(run.sleeps.some(milliseconds => milliseconds >= 12000));
      assert.ok(run.elapsed() >= 12000 && run.elapsed() < 20000);
      assert.equal(run.outputs.get('status'), 'succeed');
    });
  }
  await t.test('Retry-After longer than the deadline', async () => {
    const retryable = upstreamError(429);
    retryable.response.headers['retry-after'] = '600';
    const run = fixture({ poll: retryable });
    await assert.rejects(deployPages({ ...run.options, timeoutMs: 12000 }));
    assert.ok(run.elapsed() >= 12000 && run.elapsed() <= 12001);
    canceledOnce(run);
    noSuccess(run);
  });
});

test('bounds every GitHub request and still permits bounded cancellation after timeout', async () => {
  const run = fixture({ poll: { status: 'pending' } });
  await assert.rejects(deployPages({ ...run.options, timeoutMs: 12000 }));
  for (const { parameters } of run.calls) {
    assert.ok(parameters.request && Number.isFinite(parameters.request.timeout));
    assert.ok(parameters.request.timeout > 0 && parameters.request.timeout <= 30000);
  }
  canceledOnce(run);
  noSuccess(run);
});

test('default thirty-minute timeout cancels a deployment that stays pending', async () => {
  const run = fixture({ poll: { status: 'deployment_in_progress' } });
  await assert.rejects(deployPages(run.options));
  assert.ok(run.elapsed() >= 1800000);
  assert.ok(run.elapsed() <= 1805000);
  canceledOnce(run);
  noSuccess(run);
});

test('short injected test timeout still cancels rather than reporting pending as success', async () => {
  const run = fixture({ poll: { status: 'waiting' } });
  await assert.rejects(deployPages({ ...run.options, timeoutMs: 12000, intervalMs: 1000 }));
  assert.ok(run.elapsed() >= 12000);
  assert.ok(run.elapsed() <= 13000);
  canceledOnce(run);
  noSuccess(run);
});

test('a failed non-idempotent create POST is never retried', async () => {
  const run = fixture({ created: upstreamError(503) });
  await assert.rejects(deployPages(run.options), error => !String(error.message).includes(OIDC));
  assert.equal(run.counts.create, 1);
  assert.equal(run.counts.status, 0);
  assert.equal(run.counts.cancel, 0, 'no known deployment id exists to cancel');
  noSuccess(run);
});

test('a successful create response arriving after the deadline cancels its known deployment', async () => {
  const run = fixture();
  const originalRequest = run.github.request;
  run.github.request = async (route, parameters) => {
    const result = await originalRequest(route, parameters);
    if (route === ROUTES.create) await run.clock.sleep(15000);
    return result;
  };
  await assert.rejects(deployPages({ ...run.options, timeoutMs: 12000 }));
  assert.equal(run.counts.create, 1);
  assert.equal(run.counts.status, 0);
  canceledOnce(run);
  noSuccess(run);
});

test('a safe create id with a terminal create status is canceled before polling', async () => {
  const run = fixture({
    created: { id: CONTEXT.sha, page_url: PAGE_URL, status: 'deployment_failed' },
  });
  await assert.rejects(deployPages(run.options));
  assert.equal(run.counts.create, 1);
  assert.equal(run.counts.status, 0);
  canceledOnce(run);
  noSuccess(run);
});

test('masks the actual OIDC token exactly once before submitting the create request', async () => {
  const run = fixture();
  const originalRequest = run.github.request;
  run.github.request = async (route, parameters) => {
    if (route === ROUTES.create) {
      assert.equal(parameters.oidc_token, OIDC);
      assert.deepEqual(run.secrets, [OIDC], 'the submitted token must already be masked');
    }
    return originalRequest(route, parameters);
  };
  await deployPages(run.options);
  assert.deepEqual(run.secrets, [OIDC]);
  assert.equal(run.counts.create, 1);
  assert.equal(JSON.stringify(run.logs).includes(OIDC), false);
});

test('failed token acquisition never posts or logs the token-provider error', async () => {
  const run = fixture({ oidcError: upstreamError(503) });
  await assert.rejects(deployPages(run.options), error => !String(error.message).includes(OIDC));
  assert.equal(run.counts.create, 0);
  assert.equal(run.counts.cancel, 0);
  noSuccess(run);
});

test('rejects malformed deployment ids and non-HTTPS or credential-bearing page URLs', async t => {
  const responses = [
    { id: '../unsafe', page_url: PAGE_URL },
    { id: '', page_url: PAGE_URL },
    { id: 0, page_url: PAGE_URL },
    { id: CONTEXT.sha, page_url: 'http://ka8877.github.io/firstgame/' },
    { id: CONTEXT.sha, page_url: 'https://user:secret@ka8877.github.io/firstgame/' },
  ];
  for (const created of responses) {
    await t.test(JSON.stringify(created), async () => {
      const run = fixture({ created });
      await assert.rejects(deployPages(run.options));
      assert.equal(run.counts.create, 1);
      assert.equal(run.counts.status, 0);
      if (created.id === CONTEXT.sha) canceledOnce(run);
      else assert.equal(run.counts.cancel, 0);
      noSuccess(run);
    });
  }
});

test('failed cancellation cannot turn terminal failure or timeout into success', async t => {
  for (const status of ['errored', 'pending']) {
    await t.test(status, async () => {
      const run = fixture({ poll: { status }, cancel: upstreamError(503) });
      await assert.rejects(deployPages({ ...run.options, timeoutMs: 10000 }),
        error => !String(error.message).includes(OIDC));
      canceledOnce(run);
      noSuccess(run);
    });
  }
});

test('rejects invalid test timing overrides rather than weakening the production bounds', async t => {
  for (const overrides of [
    { timeoutMs: 1800001 }, { timeoutMs: 0 }, { timeoutMs: Infinity },
    { intervalMs: 5001 }, { intervalMs: 0 }, { intervalMs: NaN },
  ]) {
    await t.test(String(Object.keys(overrides)[0]) + ':' + String(Object.values(overrides)[0]), async () => {
      const run = fixture();
      await assert.rejects(deployPages({ ...run.options, ...overrides }));
      assert.equal(run.counts.create, 0);
      noSuccess(run);
    });
  }
});
