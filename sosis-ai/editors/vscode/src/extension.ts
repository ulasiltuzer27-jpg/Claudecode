// Sosis AI VS Code eklentisi: yerel `sosis serve` sunucusuyla satır içi öneri, açıklama ve düzeltme.
import * as path from "path";
import * as vscode from "vscode";
import { cleanSuggestion, fenceFor, SosisClient } from "./client";

const cfg = () => vscode.workspace.getConfiguration("sosis");
const client = () => new SosisClient(cfg().get<string>("serverUrl", "http://127.0.0.1:8080"));
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

let status: vscode.StatusBarItem;

async function refreshStatus(): Promise<void> {
  const on = cfg().get<boolean>("enabled", true);
  try {
    const info = await client().info(AbortSignal.timeout(2000));
    status.text = on ? "$(sparkle) Sosy" : "$(circle-slash) Sosy";
    status.tooltip = `${info.name} · ${info.context} token\nTıkla: önerileri ${on ? "kapat" : "aç"}`;
    status.backgroundColor = undefined;
  } catch {
    status.text = "$(warning) Sosy";
    status.tooltip = "`sosis serve` çalışmıyor. Örn: sosis serve -r model.sosy";
    status.backgroundColor = new vscode.ThemeColor("statusBarItem.warningBackground");
  }
}

class InlineProvider implements vscode.InlineCompletionItemProvider {
  async provideInlineCompletionItems(
    doc: vscode.TextDocument,
    pos: vscode.Position,
    _ctx: vscode.InlineCompletionContext,
    token: vscode.CancellationToken,
  ): Promise<vscode.InlineCompletionItem[] | undefined> {
    const c = cfg();
    if (!c.get<boolean>("enabled", true)) return;
    await sleep(c.get<number>("debounceMs", 250));
    if (token.isCancellationRequested) return;
    const lines = c.get<number>("contextLines", 80);
    const start = new vscode.Position(Math.max(0, pos.line - lines), 0);
    const endLine = Math.min(doc.lineCount - 1, pos.line + 30);
    const prefix = doc.getText(new vscode.Range(start, pos));
    const suffix = doc.getText(new vscode.Range(pos, doc.lineAt(endLine).range.end));
    const abort = new AbortController();
    token.onCancellationRequested(() => abort.abort());
    try {
      const text = await client().fim(prefix, suffix, path.basename(doc.fileName), {
        maxTokens: c.get<number>("maxTokens", 64),
        temperature: c.get<number>("temperature", 0.2),
      }, abort.signal);
      const s = cleanSuggestion(text);
      if (!s || token.isCancellationRequested) return;
      return [new vscode.InlineCompletionItem(s, new vscode.Range(pos, pos))];
    } catch {
      return; // sunucu kapalı veya istek iptal edildi
    }
  }
}

async function askAboutSelection(kind: "explain" | "fix"): Promise<void> {
  const ed = vscode.window.activeTextEditor;
  if (!ed || ed.selection.isEmpty) return;
  const code = ed.document.getText(ed.selection);
  const fence = fenceFor(ed.document.fileName);
  const message =
    kind === "explain"
      ? `Şu kodun ne yaptığını kısaca açıkla:\n\`\`\`${fence}\n${code}\n\`\`\``
      : `Şu koddaki hataları düzelt ve düzeltilmiş kodun tamamını tek kod bloğunda yaz:\n\`\`\`${fence}\n${code}\n\`\`\``;
  const doc = await vscode.workspace.openTextDocument({ language: "markdown", content: "_Sosy düşünüyor…_" });
  const view = await vscode.window.showTextDocument(doc, vscode.ViewColumn.Beside, true);
  let last = 0;
  const show = async (text: string) => {
    const full = new vscode.Range(0, 0, doc.lineCount, 0);
    await view.edit((e) => e.replace(full, text));
  };
  try {
    const answer = await client().ask(message, { maxTokens: 768, temperature: 0.2 }, (all) => {
      if (Date.now() - last > 150) {
        last = Date.now();
        void show(all);
      }
    });
    await show(answer);
  } catch (e) {
    await show(`**Hata:** ${(e as Error).message}\n\nSunucu çalışıyor mu? \`sosis serve -r <model>\``);
  }
}

export function activate(context: vscode.ExtensionContext): void {
  status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
  status.command = "sosis.toggle";
  status.show();
  context.subscriptions.push(
    status,
    vscode.languages.registerInlineCompletionItemProvider({ pattern: "**" }, new InlineProvider()),
    vscode.commands.registerCommand("sosis.toggle", async () => {
      await cfg().update("enabled", !cfg().get<boolean>("enabled", true), vscode.ConfigurationTarget.Global);
      await refreshStatus();
    }),
    vscode.commands.registerCommand("sosis.explain", () => askAboutSelection("explain")),
    vscode.commands.registerCommand("sosis.fix", () => askAboutSelection("fix")),
    vscode.workspace.onDidChangeConfiguration((e) => e.affectsConfiguration("sosis") && refreshStatus()),
  );
  void refreshStatus();
  const timer = setInterval(() => void refreshStatus(), 30_000);
  context.subscriptions.push({ dispose: () => clearInterval(timer) });
}

export function deactivate(): void {}
