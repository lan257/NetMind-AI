const ENCRYPTION_PREFIX = 'rsa-oaep-sha256';
let cachedPublicKey = null;
let cachedCryptoKey = null;

function base64ToArrayBuffer(value) {
  const binary = window.atob(value);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes.buffer;
}

function arrayBufferToBase64(buffer) {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return window.btoa(binary);
}

async function loadPublicKey() {
  if (cachedPublicKey && cachedCryptoKey) {
    return { publicKey: cachedPublicKey, cryptoKey: cachedCryptoKey };
  }

  const response = await fetch('/api/system/crypto/api-key-public-key');
  const text = await response.text();
  const result = text ? JSON.parse(text) : {};
  if (!response.ok || !result.success || !result.data?.publicKey) {
    throw new Error(result.message || '获取 API Key 加密公钥失败。');
  }

  cachedPublicKey = result.data;
  cachedCryptoKey = await window.crypto.subtle.importKey(
    'spki',
    base64ToArrayBuffer(cachedPublicKey.publicKey),
    { name: 'RSA-OAEP', hash: 'SHA-256' },
    false,
    ['encrypt']
  );
  return { publicKey: cachedPublicKey, cryptoKey: cachedCryptoKey };
}

async function encryptApiKey(value) {
  if (!value || typeof value !== 'string' || value.startsWith(`${ENCRYPTION_PREFIX}:`)) {
    return value;
  }

  if (!window.crypto?.subtle) {
    throw new Error('当前浏览器不支持 API Key 加密，请更换新版浏览器。');
  }

  const { publicKey, cryptoKey } = await loadPublicKey();
  const cipher = await window.crypto.subtle.encrypt(
    { name: 'RSA-OAEP' },
    cryptoKey,
    new TextEncoder().encode(value)
  );
  return `${ENCRYPTION_PREFIX}:${publicKey.keyId}:${arrayBufferToBase64(cipher)}`;
}

async function encryptApiKeys(value) {
  if (Array.isArray(value)) {
    return Promise.all(value.map((item) => encryptApiKeys(item)));
  }

  if (!value || typeof value !== 'object') {
    return value;
  }

  const entries = await Promise.all(Object.entries(value).map(async ([key, item]) => {
    if (key === 'apiKey') {
      return [key, await encryptApiKey(item)];
    }
    return [key, await encryptApiKeys(item)];
  }));
  return Object.fromEntries(entries);
}

export async function encryptApiKeysInJsonBody(body) {
  if (!body || typeof body !== 'string' || !body.includes('apiKey')) {
    return body;
  }

  const payload = JSON.parse(body);
  const encrypted = await encryptApiKeys(payload);
  return JSON.stringify(encrypted);
}
