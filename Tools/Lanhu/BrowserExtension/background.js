const BRIDGE = 'http://127.0.0.1:18766/gdk-lanhu';

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || !message.type) return false;

  if (message.type === 'run-sync' && sender.tab && sender.tab.id) {
    (async () => {
      await chrome.scripting.executeScript({
        target: { tabId: sender.tab.id },
        world: 'MAIN',
        func: jobId => { globalThis.__GDK_LANHU_SYNC_JOB__ = jobId; },
        args: [message.jobId]
      });
      await chrome.scripting.executeScript({
        target: { tabId: sender.tab.id },
        world: 'MAIN',
        files: ['lanhu-sync.js']
      });
      sendResponse({ ok: true });
    })().catch(error => {
      post('/error', { jobId: message.jobId, error: error.message || String(error) });
      sendResponse({ ok: false, error: error.message || String(error) });
    });
    return true;
  }

  if (message.type === 'sync-result') {
    post('/complete', {
      jobId: message.jobId,
      manifest: message.manifest,
      summary: message.summary
    }).then(() => sendResponse({ ok: true })).catch(error =>
      sendResponse({ ok: false, error: error.message || String(error) }));
    return true;
  }

  if (message.type === 'sync-error') {
    post('/error', { jobId: message.jobId, error: message.error || '蓝湖同步失败。' })
      .then(() => sendResponse({ ok: true }))
      .catch(error => sendResponse({ ok: false, error: error.message || String(error) }));
    return true;
  }

  return false;
});

async function post(path, payload) {
  const response = await fetch(BRIDGE + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!response.ok) throw new Error(`Unity bridge HTTP ${response.status}`);
}
