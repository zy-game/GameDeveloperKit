const BRIDGE_JOBS = 'http://127.0.0.1:18766/gdk-lanhu/jobs/next';
let runningJob = '';

window.addEventListener('message', event => {
  if (event.source !== window || !event.data) return;
  if (event.data.type === 'GDK_LANHU_SYNC_RESULT') {
    chrome.runtime.sendMessage({
      type: 'sync-result',
      jobId: event.data.jobId,
      manifest: event.data.manifest,
      summary: event.data.summary
    });
    runningJob = '';
  } else if (event.data.type === 'GDK_LANHU_SYNC_ERROR') {
    chrome.runtime.sendMessage({
      type: 'sync-error',
      jobId: event.data.jobId,
      error: event.data.error
    });
    runningJob = '';
  }
});

async function poll() {
  try {
    const response = await fetch(BRIDGE_JOBS, { cache: 'no-store' });
    if (response.status === 204) return;
    if (!response.ok) return;
    const job = await response.json();
    if (!job.jobId || runningJob === job.jobId) return;

    const hashQuery = new URLSearchParams(location.hash.split('?')[1] || '');
    const projectId = hashQuery.get('pid') || hashQuery.get('project_id');
    const teamId = hashQuery.get('teamId') || hashQuery.get('tid');
    if (projectId !== job.projectId || teamId !== job.teamId) {
      location.href = job.url;
      return;
    }

    runningJob = job.jobId;
    const result = await chrome.runtime.sendMessage({ type: 'run-sync', jobId: job.jobId });
    if (!result || !result.ok) runningJob = '';
  } catch {
    runningJob = '';
  }
}

setInterval(poll, 750);
poll();
