'use strict';

// Uses the same documented Pages REST endpoints and OIDC payload as
// actions/deploy-pages@d6db90164ac5ed86f2b6aed7e0febac5b3c0c03e.
// That action clamps its timeout to ten minutes. This helper waits at most
// thirty minutes, while retaining actual service failures and cancellation.
// No API payload, OIDC token or unsanitized request error is logged.
const LIMIT_MS = 30 * 60 * 1000;
const INTERVAL_MS = 5000;
const REQUEST_MS = 30000;
const MAX_ERRORS = 10;
const MAX_ARTIFACT_BYTES = 1073741824;
const FINAL_ERRORS = new Set([
  'deployment_failed', 'deployment_perms_error', 'deployment_content_failed',
  'deployment_cancelled', 'deployment_lost', 'failed', 'errored', 'error',
  'canceled', 'cancelled'
]);

class PagesError extends Error {}

function need(value, message) {
  if (!value) throw new PagesError(message);
}

function positiveInteger(value) {
  return Number.isSafeInteger(value) && value > 0;
}

function safeDeploymentId(value) {
  return positiveInteger(value) || (typeof value === 'string' &&
    /^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$/.test(value));
}

function httpStatus(error) {
  const status = error?.status ?? error?.response?.status;
  return Number.isInteger(status) && status >= 100 && status <= 599 ? status : 0;
}

function httpsPage(value) {
  if (typeof value !== 'string' || value.length > 2048) return false;
  try {
    const parsed = new URL(value);
    return parsed.protocol === 'https:' && Boolean(parsed.hostname) &&
      !parsed.username && !parsed.password;
  } catch {
    return false;
  }
}

async function deployPages({github, context, core, clock,
  timeoutMs = LIMIT_MS, intervalMs = INTERVAL_MS} = {}) {
  need(github && typeof github.request === 'function', 'A GitHub API client is required.');
  need(context && context.repo &&
    typeof context.repo.owner === 'string' &&
    /^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$/.test(context.repo.owner) &&
    typeof context.repo.repo === 'string' && /^[A-Za-z0-9_.-]{1,100}$/.test(context.repo.repo) &&
    !['.', '..'].includes(context.repo.repo) && positiveInteger(context.runId) &&
    typeof context.sha === 'string' && /^[0-9a-f]{40}$/.test(context.sha),
    'A valid exact repository, workflow run ID and source SHA are required.');
  need(core && ['getIDToken', 'setSecret', 'setOutput', 'info', 'warning']
    .every(name => typeof core[name] === 'function'), 'The Actions core interface is required.');
  need(Number.isFinite(timeoutMs) && timeoutMs > 0 && timeoutMs <= LIMIT_MS &&
    Number.isFinite(intervalMs) && intervalMs > 0 && intervalMs <= INTERVAL_MS,
    'Wait bounds may only shorten the fixed thirty-minute/five-second production limits.');
  const timer = clock ?? {now: () => Date.now(), sleep: ms => new Promise(resolve => setTimeout(resolve, ms))};
  need(typeof timer.now === 'function' && typeof timer.sleep === 'function', 'A valid clock is required.');
  const started = timer.now();
  need(Number.isFinite(started), 'The clock must provide a finite time.');
  let previousTime = started;
  let errors = 0;
  let deploymentId;
  const repository = {owner: context.repo.owner, repo: context.repo.repo};

  function remaining() {
    const current = timer.now();
    need(Number.isFinite(current) && current >= previousTime, 'The deployment clock moved backwards.');
    previousTime = current;
    const left = timeoutMs - (current - started);
    need(left > 0, 'Pages deployment timed out before the service reported success.');
    return left;
  }

  async function pause(delay) {
    await timer.sleep(Math.min(delay, remaining()));
    remaining();
  }

  function retryDelay(error) {
    const headers = error?.response?.headers ?? {};
    const value = headers['retry-after'] ?? headers['Retry-After'];
    let delay = intervalMs;
    if (typeof value === 'string' || typeof value === 'number') {
      const text = String(value).trim();
      if (/^\d+(?:\.\d+)?$/.test(text)) {
        const millis = Number(text) * 1000;
        if (Number.isFinite(millis)) delay = Math.max(delay, millis);
      } else {
        const date = Date.parse(text);
        if (Number.isFinite(date)) delay = Math.max(delay, date - timer.now());
      }
    }
    return Math.max(intervalMs, delay);
  }

  async function bounded(operation, duration, abort) {
    let timerId;
    try {
      return await Promise.race([
        Promise.resolve().then(operation),
        new Promise((resolve, reject) => {
          timerId = setTimeout(() => {
            if (abort) abort();
            reject(new PagesError('Pages API operation exceeded its bounded request time.'));
          }, duration);
        })
      ]);
    } finally {
      clearTimeout(timerId);
    }
  }

  async function apiRequest(route, parameters, duration) {
    const controller = new AbortController();
    return bounded(() => github.request(route, {...repository, ...parameters,
      request: {timeout: duration, signal: controller.signal}}), duration, () => controller.abort());
  }

  async function request(route, parameters, retryable = true) {
    for (;;) {
      const left = remaining();
      try {
        const result = await apiRequest(route, parameters, Math.max(1, Math.min(REQUEST_MS, left)));
        if (Number.isInteger(result?.status) && result.status >= 400) {
          // Octokit normally throws. Also reject resolved error responses from
          // other injected clients without accepting their data as success.
          throw {status: result.status, response: {headers: result.headers}};
        }
        // Creation must return its actual ID to the outer scope before a late
        // response is rejected, so that known deployment is still canceled.
        if (retryable) remaining();
        return result;
      } catch (error) {
        if (error instanceof PagesError) throw error;
        const status = httpStatus(error);
        if (retryable && (status === 429 || status >= 500)) {
          errors += 1;
          need(errors < MAX_ERRORS, 'Pages API reached its ten-error limit.');
          core.warning(`Pages API temporarily unavailable (HTTP ${status}); bounded retry ${errors}/${MAX_ERRORS}.`);
          await pause(status === 429 ? retryDelay(error) : intervalMs);
          continue;
        }
        throw new PagesError(`Pages API request failed${status ? ` (HTTP ${status})` : ''}.`);
      }
    }
  }

  try {
    // Read every page for this exact run. Never silently select one of several
    // same-name artifacts left behind by a failed-job rerun.
    const artifacts = [];
    const seen = new Set();
    let expectedCount;
    for (let page = 1; ; page += 1) {
      const response = await request('GET /repos/{owner}/{repo}/actions/runs/{run_id}/artifacts',
        {run_id: context.runId, per_page: 100, page});
      const data = response?.data;
      need(data && Array.isArray(data.artifacts) && Number.isSafeInteger(data.total_count) && data.total_count >= 0,
        'Current-run artifact metadata is malformed.');
      if (expectedCount === undefined) expectedCount = data.total_count;
      need(data.total_count === expectedCount && data.artifacts.length <= 100,
        'Current-run artifact metadata changed during enumeration.');
      for (const artifact of data.artifacts) {
        need(artifact && positiveInteger(artifact.id) && !seen.has(artifact.id),
          'Artifact IDs must be positive, unique and finite.');
        seen.add(artifact.id);
        artifacts.push(artifact);
      }
      need(artifacts.length <= expectedCount, 'Artifact pagination exceeded its reported total.');
      if (artifacts.length === expectedCount) break;
      need(data.artifacts.length === 100, 'Artifact pagination omitted current-run metadata.');
    }
    const matches = artifacts.filter(artifact => artifact.name === 'github-pages');
    need(matches.length === 1, 'Exactly one github-pages artifact for this workflow run is required.');
    const artifact = matches[0];
    need(artifact.expired === false && positiveInteger(artifact.size_in_bytes) &&
      artifact.size_in_bytes <= MAX_ARTIFACT_BYTES &&
      artifact.workflow_run?.id === context.runId && artifact.workflow_run?.head_sha === context.sha,
      'The Pages artifact must be unexpired, at most 1 GiB, and bound to this exact run/source SHA.');

    let token;
    try {
      token = await bounded(() => core.getIDToken(), Math.max(1, Math.min(REQUEST_MS, remaining())));
      need(typeof token === 'string' && token.length > 0, 'An actual OIDC token is required.');
      core.setSecret(token);
    } catch {
      throw new PagesError('Obtaining and masking the deployment OIDC token failed.');
    }
    // Never retry a creation POST: an ambiguous response could otherwise create
    // another deployment. Only safe GET observations are retried above.
    const response = await request('POST /repos/{owner}/{repo}/pages/deployments', {
      artifact_id: artifact.id, pages_build_version: context.sha, oidc_token: token
    }, false);
    const created = response?.data;
    if (safeDeploymentId(created?.id)) deploymentId = created.id;
    need(deploymentId !== undefined, 'The Pages create response omitted a safe deployment ID.');
    remaining();
    need(created && typeof created === 'object' && !Array.isArray(created), 'Pages create response is malformed.');
    need(!FINAL_ERRORS.has(created.status), 'Pages creation reported a terminal deployment failure.');
    need(httpsPage(created.page_url), 'The Pages create response omitted an actual HTTPS page URL.');
    const pageUrl = created.page_url;
    core.info('Pages deployment created; waiting for the actual service result (up to thirty minutes).');

    for (;;) {
      const observed = await request('GET /repos/{owner}/{repo}/pages/deployments/{deploymentId}', {deploymentId});
      const data = observed?.data;
      need(data && typeof data === 'object' && !Array.isArray(data), 'Pages status response is malformed.');
      need(data.id === undefined || data.id === deploymentId, 'Pages status response belongs to a different deployment.');
      need(data.status == null || typeof data.status === 'string', 'Pages status value is malformed.');
      if (data.status === 'succeed') {
        need(data.page_url === undefined || data.page_url === pageUrl, 'Pages status URL differs from its creation result.');
        core.setOutput('page_url', pageUrl);
        core.setOutput('status', 'succeed');
        core.info('Pages service reported successful deployment.');
        return {id: deploymentId, page_url: pageUrl, status: 'succeed'};
      }
      need(!FINAL_ERRORS.has(data.status), 'Pages service reported a terminal deployment failure.');
      // Empty initial status and temporary/unknown service states are observed
      // while GitHub synchronizes files. They never count as success and never
      // reset/consume the separate HTTP error budget; the deadline still holds.
      core.info('Pages service has not reported completion; waiting.');
      await pause(intervalMs);
    }
  } catch (error) {
    if (deploymentId !== undefined) {
      try {
        const canceled = await apiRequest('POST /repos/{owner}/{repo}/pages/deployments/{deploymentId}/cancel',
          {deploymentId}, REQUEST_MS);
        need(!Number.isInteger(canceled?.status) || canceled.status < 400, 'Pages cancellation was rejected.');
        core.warning('Requested cancellation of the incomplete Pages deployment.');
      } catch {
        core.warning('The Pages cancellation request failed; the deployment result remains a failure.');
      }
    }
    if (error instanceof PagesError) throw error;
    throw new PagesError('Pages deployment helper failed; no successful completion was recorded.');
  }
}

module.exports = {deployPages};
