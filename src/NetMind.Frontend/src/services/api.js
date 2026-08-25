export async function api(path, options = {}) {
  const headers = { ...(options.headers ?? {}) };
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(path, { ...options, headers });
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

/** 获取节点的知识探索数据（含中心节点、各层相邻节点与关系边）。 */
export function getNodeExplore(id, depth) {
  return api(`/api/nodes/${id}/explore?depth=${depth}`);
}

export function downloadUrl(url) {
  window.location.href = url;
}
