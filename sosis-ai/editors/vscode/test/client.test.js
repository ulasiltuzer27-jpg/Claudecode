// Gerçek `sosis serve`'e karşı istemci testi: SOSIS_URL=http://127.0.0.1:8080 node test/client.test.js
const assert = require("assert");
const { SosisClient, cleanSuggestion, fenceFor } = require("../out/client");

(async () => {
  assert.strictEqual(cleanSuggestion("  \n "), null);
  assert.strictEqual(cleanSuggestion("x = 1  \n"), "x = 1");
  assert.strictEqual(fenceFor("Player.cs"), "csharp");
  const url = process.env.SOSIS_URL;
  if (!url) { console.log("birim testleri geçti (SOSIS_URL yok, sunucu testi atlandı)"); return; }
  const c = new SosisClient(url);
  const info = await c.info();
  assert.ok(info.name && info.context > 0);
  const fim = await c.fim("def add(a, b):\n", "\n\nprint(add(1, 2))\n", "main.py", { maxTokens: 24, temperature: 0 });
  assert.ok(typeof fim === "string");
  assert.ok(!fim.includes("\n\n"), "stop dizisi uygulanmalı");
  const ans = await c.ask("Merhaba", { maxTokens: 16, temperature: 0 });
  assert.ok(ans.length > 0);
  // İptal
  const ac = new AbortController();
  const p = c.fim("fn main() {", "}", "main.rs", { maxTokens: 64, temperature: 0 }, ac.signal);
  ac.abort();
  await assert.rejects(p);
  console.log(`sunucu testleri geçti — model: ${info.name}; FIM önerisi: ${JSON.stringify(fim.slice(0, 40))}`);
})().catch((e) => { console.error(e); process.exit(1); });
