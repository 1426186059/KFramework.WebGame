/**
 * 轻量混淆：XOR 逐字节变换 + Base64 输出。
 *
 * 仅用于防止内容被肉眼直接看到（配置表、存档等）。密钥随包发布，
 * 拿到即可逆向，**不是**安全加密，别拿来存敏感数据。
 */
const Key = new Uint8Array([
  0x3c, 0x7a, 0x91, 0x55, 0xe2, 0x14, 0x6b, 0x09, 0xa7, 0x42, 0xc3, 0x5f, 0x18,
  0xd9, 0x70, 0x2e,
]);

export class ContentEncryption {
  public static encode(text: string): string {
    const raw = new TextEncoder().encode(text);
    return bytesToBase64(xor(raw));
  }

  public static decode(text: string): string {
    const raw = xor(base64ToBytes(text));
    return new TextDecoder().decode(raw);
  }
}

function xor(data: Uint8Array): Uint8Array {
  const out = new Uint8Array(data.length);
  for (let i = 0; i < data.length; i++) {
    out[i] = data[i] ^ Key[i % Key.length];
  }
  return out;
}

function bytesToBase64(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary);
}

function base64ToBytes(base64: string): Uint8Array {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}
