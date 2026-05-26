import { encryptApiKeysInJsonBody } from './apiKeyEncryption';

export async function api(path, options = {}) {
  const headers = { ...(options.headers ?? {}) };
  const nextOptions = { ...options };
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
    nextOptions.body = await encryptApiKeysInJsonBody(options.body);
  }

  const response = await fetch(path, { ...nextOptions, headers });
  const text = await response.text();
  let result = {};
  try {
    result = text ? JSON.parse(text) : {};
  } catch {
    throw new Error(text || `请求失败：${response.status}`);
  }

  if (!response.ok || !result.success) {
    throw new Error(result.message || `请求失败：${response.status}`);
  }

  return result.data;
}

export function downloadUrl(url) {
  window.location.href = url;
}
