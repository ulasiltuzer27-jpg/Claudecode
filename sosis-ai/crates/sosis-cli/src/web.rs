//! `sosis serve`: yerel web arayüzü (yalnızca 127.0.0.1, harici API/CDN yok).
//!
//! Model tek bir işçi iş parçacığında çalışır; HTTP istekleri ona iş gönderir ve üretilen
//! parçalar chunked HTTP yanıtı olarak anında tarayıcıya akar.

use std::io::{Read, Write};
use std::path::PathBuf;
use std::sync::mpsc;
use std::sync::Arc;

use anyhow::Result;
use serde::Deserialize;
use serde_json::json;
use sosis_infer::chat::{reply, valid_id, ChatSession};
use sosis_infer::rag::CodeIndex;
use sosis_infer::{Generator, SamplingParams};
use tiny_http::{Header, Method, Request, Response, Server};

const INDEX_HTML: &str = include_str!("web/index.html");
const MAX_BODY: usize = 1 << 20;

#[derive(Deserialize, Default)]
#[serde(default)]
struct GenOpts {
    temperature: Option<f32>,
    top_p: Option<f32>,
    max_tokens: Option<usize>,
    rag: bool,
    /// Görüldüğünde üretimi durduran diziler (ör. editör tamamlamasında "\n\n").
    stop: Vec<String>,
}

impl GenOpts {
    fn params(&self) -> SamplingParams {
        let d = SamplingParams::default();
        SamplingParams {
            temperature: self.temperature.unwrap_or(d.temperature).clamp(0.0, 2.0),
            top_p: self.top_p.unwrap_or(d.top_p).clamp(0.05, 1.0),
            max_new_tokens: self.max_tokens.unwrap_or(512).clamp(1, 4096),
            stop: self.stop.iter().take(8).cloned().collect(),
            ..d
        }
    }
}

#[derive(Deserialize)]
#[serde(tag = "kind", rename_all = "lowercase")]
enum Job {
    Chat {
        id: String,
        message: String,
        #[serde(flatten)]
        opts: GenOpts,
    },
    Complete {
        prompt: String,
        file: Option<String>,
        #[serde(flatten)]
        opts: GenOpts,
    },
    Fim {
        prefix: String,
        suffix: String,
        file: Option<String>,
        #[serde(flatten)]
        opts: GenOpts,
    },
    /// Önce plan: model numaralı plan yazar (onay arayüzde).
    Plan {
        id: String,
        message: String,
        #[serde(flatten)]
        opts: GenOpts,
    },
    /// Onaylanan / düzenlenen planla kod.
    Execute {
        id: String,
        plan: String,
        #[serde(flatten)]
        opts: GenOpts,
    },
    /// Çalıştırıp düzelt: kod derlenir/çalıştırılır, hata varsa model düzeltir.
    Solve {
        id: String,
        message: String,
        lang: String,
        #[serde(default = "d_rounds")]
        rounds: usize,
        #[serde(flatten)]
        opts: GenOpts,
    },
}

fn d_rounds() -> usize {
    3
}

enum Piece {
    Text(String),
    Error(String),
}

struct Work {
    job: Job,
    out: mpsc::Sender<Piece>,
}

struct Shared {
    work: mpsc::SyncSender<Work>,
    sessions: PathBuf,
    info: serde_json::Value,
    allowed_hosts: Vec<String>,
}

/// İşçi: modeli ve indeksi tek başına sahiplenir, işleri sırayla yürütür.
fn worker(
    generator: Generator,
    index: Option<CodeIndex>,
    sessions: PathBuf,
    rx: mpsc::Receiver<Work>,
) {
    for Work { job, out } in rx {
        let send = |s: &str| {
            let _ = out.send(Piece::Text(s.to_string()));
        };
        let result: Result<()> = (|| {
            match job {
                Job::Chat { id, message, opts } => {
                    let mut s = ChatSession::load(&sessions, &id)?;
                    let idx = if opts.rag { index.as_ref() } else { None };
                    reply(
                        &generator,
                        &mut s,
                        &message,
                        idx,
                        &opts.params(),
                        &mut |p| send(p),
                    );
                    s.save(&sessions)?;
                }
                Job::Complete { prompt, file, opts } => {
                    let params = opts.params();
                    match (opts.rag, &index) {
                        (true, Some(idx)) => {
                            generator.complete_with_index(
                                &prompt,
                                file.as_deref(),
                                idx,
                                &params,
                                &mut |p| send(p),
                            );
                        }
                        _ => {
                            generator.complete(&prompt, file.as_deref(), &params, send);
                        }
                    }
                }
                Job::Fim {
                    prefix,
                    suffix,
                    file,
                    opts,
                } => {
                    generator.fill_in_middle(
                        &prefix,
                        &suffix,
                        file.as_deref(),
                        &opts.params(),
                        send,
                    );
                }
                Job::Plan { id, message, opts } => {
                    let mut s = ChatSession::load(&sessions, &id)?;
                    let idx = if opts.rag { index.as_ref() } else { None };
                    sosis_infer::plan::make_plan(
                        &generator,
                        &mut s,
                        &message,
                        idx,
                        &opts.params(),
                        &mut |p| send(p),
                    );
                    s.save(&sessions)?;
                }
                Job::Execute { id, plan, opts } => {
                    let mut s = ChatSession::load(&sessions, &id)?;
                    let idx = if opts.rag { index.as_ref() } else { None };
                    sosis_infer::plan::execute_plan(
                        &generator,
                        &mut s,
                        &plan,
                        idx,
                        &opts.params(),
                        &mut |p| send(p),
                    );
                    s.save(&sessions)?;
                }
                Job::Solve {
                    id,
                    message,
                    lang,
                    rounds,
                    opts,
                } => {
                    let lang = match lang.as_str() {
                        "python" | "py" => sosis_bench::Lang::Python,
                        _ => sosis_bench::Lang::Rust,
                    };
                    let mut s = ChatSession::load(&sessions, &id)?;
                    let idx = if opts.rag { index.as_ref() } else { None };
                    sosis_infer::repair::solve(
                        &generator,
                        &mut s,
                        &message,
                        lang,
                        None,
                        rounds.clamp(1, 5),
                        idx,
                        &opts.params(),
                        &mut |p| send(p),
                        |a| {
                            let status = match &a.outcome {
                                sosis_bench::Outcome::Pass => "✓ derlendi ve çalıştı".to_string(),
                                sosis_bench::Outcome::CompileError(e) => {
                                    format!("✗ derleme hatası: {}", e.lines().next().unwrap_or(""))
                                }
                                sosis_bench::Outcome::Fail(e) => {
                                    format!("✗ çalışma hatası: {}", e.lines().next().unwrap_or(""))
                                }
                                sosis_bench::Outcome::Timeout => "✗ zaman aşımı".to_string(),
                            };
                            send(&format!("\n\n[[tur {}: {status}]]\n\n", a.round));
                        },
                    )?;
                    s.save(&sessions)?;
                }
            }
            Ok(())
        })();
        if let Err(e) = result {
            let _ = out.send(Piece::Error(e.to_string()));
        }
    }
}

fn header(k: &str, v: &str) -> Header {
    Header::from_bytes(k.as_bytes(), v.as_bytes()).expect("geçerli başlık")
}

fn respond_json(req: Request, code: u16, v: &serde_json::Value) {
    let body = v.to_string();
    let _ = req.respond(
        Response::from_string(body)
            .with_status_code(code)
            .with_header(header("Content-Type", "application/json; charset=utf-8")),
    );
}

fn read_body(req: &mut Request) -> Result<String> {
    let mut buf = Vec::new();
    req.as_reader()
        .take(MAX_BODY as u64 + 1)
        .read_to_end(&mut buf)?;
    if buf.len() > MAX_BODY {
        anyhow::bail!("istek gövdesi çok büyük");
    }
    Ok(String::from_utf8(buf)?)
}

/// Üretimi chunked HTTP ile parça parça akıtır (her parçadan sonra flush).
fn stream(req: Request, rx: mpsc::Receiver<Piece>) {
    let mut w = req.into_writer();
    let head = "HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nTransfer-Encoding: chunked\r\n\
                Cache-Control: no-cache\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n";
    if w.write_all(head.as_bytes()).is_err() {
        return;
    }
    let mut chunk = |s: &str| -> bool {
        if s.is_empty() {
            return true;
        }
        write!(w, "{:x}\r\n{s}\r\n", s.len()).is_ok() && w.flush().is_ok()
    };
    for p in rx {
        let ok = match p {
            Piece::Text(s) => chunk(&s),
            Piece::Error(e) => chunk(&format!("\n[hata: {e}]")),
        };
        if !ok {
            return; // tarayıcı bağlantıyı kapattı
        }
    }
    let _ = w.write_all(b"0\r\n\r\n");
    let _ = w.flush();
}

fn handle(mut req: Request, sh: &Shared) {
    // DNS-rebinding koruması: yalnızca yerel Host başlıkları.
    let host = req
        .headers()
        .iter()
        .find(|h| h.field.equiv("Host"))
        .map(|h| h.value.as_str().to_string())
        .unwrap_or_default();
    if !sh.allowed_hosts.contains(&host) {
        respond_json(req, 403, &json!({"error": "yalnızca yerel erişim"}));
        return;
    }
    let url = req.url().split('?').next().unwrap_or("").to_string();
    let method = req.method().clone();
    match (method, url.as_str()) {
        (Method::Get, "/") => {
            let _ = req.respond(
                Response::from_string(INDEX_HTML)
                    .with_header(header("Content-Type", "text/html; charset=utf-8"))
                    .with_header(header(
                        "Content-Security-Policy",
                        "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'",
                    )),
            );
        }
        (Method::Get, "/api/info") => respond_json(req, 200, &sh.info),
        (Method::Get, "/api/sessions") => {
            let list: Vec<_> = ChatSession::list(&sh.sessions)
                .into_iter()
                .map(|s| json!({"id": s.id, "title": s.title, "updated": s.updated, "messages": s.messages.len()}))
                .collect();
            respond_json(req, 200, &json!(list));
        }
        (Method::Post, "/api/sessions") => {
            let body = read_body(&mut req).unwrap_or_default();
            let system = serde_json::from_str::<serde_json::Value>(&body)
                .ok()
                .and_then(|v| v.get("system")?.as_str().map(str::to_string))
                .filter(|s| !s.trim().is_empty());
            let s = ChatSession::new(system.as_deref());
            match s.save(&sh.sessions) {
                Ok(()) => respond_json(req, 200, &json!(s)),
                Err(e) => respond_json(req, 500, &json!({"error": e.to_string()})),
            }
        }
        (m, p) if p.starts_with("/api/sessions/") => {
            let id = &p["/api/sessions/".len()..];
            if !valid_id(id) {
                return respond_json(req, 400, &json!({"error": "geçersiz kimlik"}));
            }
            match m {
                Method::Get => match ChatSession::load(&sh.sessions, id) {
                    Ok(s) => respond_json(req, 200, &json!(s)),
                    Err(e) => respond_json(req, 404, &json!({"error": e.to_string()})),
                },
                Method::Delete => match ChatSession::delete(&sh.sessions, id) {
                    Ok(()) => respond_json(req, 200, &json!({"ok": true})),
                    Err(e) => respond_json(req, 404, &json!({"error": e.to_string()})),
                },
                _ => respond_json(req, 405, &json!({"error": "yöntem desteklenmiyor"})),
            }
        }
        (Method::Post, "/api/generate") => {
            let job = match read_body(&mut req).and_then(|b| Ok(serde_json::from_str::<Job>(&b)?)) {
                Ok(j) => j,
                Err(e) => return respond_json(req, 400, &json!({"error": e.to_string()})),
            };
            if let Job::Chat { id, .. }
            | Job::Plan { id, .. }
            | Job::Execute { id, .. }
            | Job::Solve { id, .. } = &job
            {
                if !valid_id(id) || ChatSession::load(&sh.sessions, id).is_err() {
                    return respond_json(req, 404, &json!({"error": "oturum bulunamadı"}));
                }
            }
            let (tx, rx) = mpsc::channel();
            if sh.work.send(Work { job, out: tx }).is_err() {
                return respond_json(req, 503, &json!({"error": "model iş parçacığı durdu"}));
            }
            stream(req, rx);
        }
        _ => respond_json(req, 404, &json!({"error": "bulunamadı"})),
    }
}

pub fn serve(
    generator: Generator,
    index: Option<CodeIndex>,
    sessions: PathBuf,
    port: u16,
) -> Result<()> {
    let addr = format!("127.0.0.1:{port}");
    let server = Server::http(&addr).map_err(|e| anyhow::anyhow!("{addr} açılamadı: {e}"))?;
    let info = json!({
        "name": generator.config.name,
        "context": generator.context_len(),
        "rag": index.is_some(),
        "chunks": index.as_ref().map(|i| i.chunks.len()).unwrap_or(0),
    });
    let (wtx, wrx) = mpsc::sync_channel::<Work>(16);
    let sessions_w = sessions.clone();
    std::thread::spawn(move || worker(generator, index, sessions_w, wrx));
    let shared = Arc::new(Shared {
        work: wtx,
        sessions,
        info,
        allowed_hosts: vec![format!("127.0.0.1:{port}"), format!("localhost:{port}")],
    });
    eprintln!("🌭 Sosis AI arayüzü: http://127.0.0.1:{port}  (durdurmak için Ctrl+C)");
    for req in server.incoming_requests() {
        let sh = shared.clone();
        std::thread::spawn(move || handle(req, &sh));
    }
    Ok(())
}
