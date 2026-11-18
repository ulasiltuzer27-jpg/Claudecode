// `sosis serve` için küçük HTTP istemcisi (VS Code'dan bağımsız; Node testlerinde de kullanılır).

export interface GenOptions {
  maxTokens: number;
  temperature: number;
  stop?: string[];
}

export interface ServerInfo {
  name: string;
  context: number;
  rag: boolean;
}

export class SosisClient {
  constructor(public baseUrl: string) {}

  private url(path: string): string {
    return this.baseUrl.replace(/\/+$/, "") + path;
  }

  async info(signal?: AbortSignal): Promise<ServerInfo> {
    const r = await fetch(this.url("/api/info"), { signal });
    if (!r.ok) throw new Error(`sunucu yanıtı ${r.status}`);
    return (await r.json()) as ServerInfo;
  }

  /** Akışı sonuna kadar okur; `onPiece` her parçada çağrılır. */
  async generate(body: Record<string, unknown>, signal?: AbortSignal, onPiece?: (all: string) => void): Promise<string> {
    const r = await fetch(this.url("/api/generate"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      signal,
    });
    if (!r.ok) {
      let msg = r.statusText;
      try {
        msg = ((await r.json()) as { error?: string }).error ?? msg;
      } catch {
        /* gövde JSON değil */
      }
      throw new Error(msg);
    }
    const reader = r.body!.getReader();
    const dec = new TextDecoder();
    let all = "";
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      all += dec.decode(value, { stream: true });
      onPiece?.(all);
    }
    if (all.includes("\n[hata:")) throw new Error(all.slice(all.indexOf("[hata:")));
    return all;
  }

  /** İmlecin önü/arkası ile ortayı doldurma (satır içi öneri). */
  fim(prefix: string, suffix: string, file: string, o: GenOptions, signal?: AbortSignal): Promise<string> {
    return this.generate(
      { kind: "fim", prefix, suffix, file, max_tokens: o.maxTokens, temperature: o.temperature, stop: o.stop ?? ["\n\n"] },
      signal,
    );
  }

  async newSession(): Promise<string> {
    const r = await fetch(this.url("/api/sessions"), { method: "POST", body: "{}" });
    if (!r.ok) throw new Error(`oturum açılamadı: ${r.status}`);
    return ((await r.json()) as { id: string }).id;
  }

  /** Tek turluk sohbet (açıkla / düzelt komutları). */
  async ask(message: string, o: GenOptions, onPiece?: (all: string) => void): Promise<string> {
    const id = await this.newSession();
    return this.generate({ kind: "chat", id, message, max_tokens: o.maxTokens, temperature: o.temperature }, undefined, onPiece);
  }
}

/** Satır içi öneriyi temizler: sondaki boşlukları atar; boşsa null. */
export function cleanSuggestion(text: string): string | null {
  const t = text.replace(/\s+$/, "");
  return t.trim().length ? t : null;
}

/** Dosya adından Markdown kod bloğu etiketi. */
export function fenceFor(fileName: string): string {
  const ext = fileName.split(".").pop()?.toLowerCase() ?? "";
  const map: Record<string, string> = { rs: "rust", py: "python", cs: "csharp", ts: "typescript", js: "javascript", go: "go", cpp: "cpp", c: "c", java: "java" };
  return map[ext] ?? ext;
}
