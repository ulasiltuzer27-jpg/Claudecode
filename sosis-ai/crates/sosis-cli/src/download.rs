//! `sosis download`: Hugging Face'teki açık veri setlerinden dosya indirme (bir kerelik).
//!
//! - Dosya listesi: `GET {endpoint}/api/datasets/{repo}/tree/{rev}/{yol}?recursive=true`
//! - İndirme: `GET {endpoint}/datasets/{repo}/resolve/{rev}/{dosya}`
//! - Erişimi kısıtlı setler için `HF_TOKEN` ortam değişkeni (diske yazılmaz).
//! - Yarım kalan dosyalar `.part` olarak saklanır ve `Range` ile kaldığı yerden sürer.
//!
//! İndirme bir kez yapılır; eğitim ve model kullanımı tamamen çevrimdışıdır.

use std::fs;
use std::io::{Read, Write};
use std::path::{Path, PathBuf};
use std::time::Duration;

use anyhow::{bail, Context, Result};
use serde::Deserialize;

pub const DEFAULT_ENDPOINT: &str = "https://huggingface.co";

#[derive(Debug, Clone, Deserialize)]
pub struct RemoteFile {
    #[serde(rename = "type")]
    pub kind: String,
    pub path: String,
    #[serde(default)]
    pub size: u64,
}

/// `*` (klasör ayırıcı hariç), `**` (her şey) ve `?` destekleyen basit glob.
pub fn glob_match(pattern: &str, path: &str) -> bool {
    fn go(p: &[u8], s: &[u8]) -> bool {
        match p.first() {
            None => s.is_empty(),
            Some(b'*') if p.get(1) == Some(&b'*') => {
                let rest = if p.get(2) == Some(&b'/') {
                    &p[3..]
                } else {
                    &p[2..]
                };
                (0..=s.len()).any(|i| go(rest, &s[i..]))
            }
            Some(b'*') => (0..=s.len())
                .take_while(|&i| i == 0 || s[i - 1] != b'/')
                .any(|i| go(&p[1..], &s[i..])),
            Some(b'?') => !s.is_empty() && s[0] != b'/' && go(&p[1..], &s[1..]),
            Some(c) => s.first() == Some(c) && go(&p[1..], &s[1..]),
        }
    }
    go(pattern.as_bytes(), path.as_bytes())
}

/// Dosyaları desene göre seçer ve toplam boyut sınırına kadar alır (sıralı, deterministik).
pub fn select(files: &[RemoteFile], include: &[String], max_bytes: u64) -> Vec<RemoteFile> {
    let mut chosen: Vec<RemoteFile> = files
        .iter()
        .filter(|f| f.kind == "file")
        .filter(|f| include.is_empty() || include.iter().any(|p| glob_match(p, &f.path)))
        .cloned()
        .collect();
    chosen.sort_by(|a, b| a.path.cmp(&b.path));
    let mut total = 0u64;
    chosen
        .into_iter()
        .take_while(|f| {
            total += f.size;
            total <= max_bytes || total == f.size
        })
        .collect()
}

/// Dosya listesi adresi (klasör boşsa sonda "/" olmaz).
pub fn tree_url(endpoint: &str, repo: &str, revision: &str, dir: &str) -> String {
    let dir = dir.trim_matches('/');
    let sub = if dir.is_empty() {
        String::new()
    } else {
        format!("/{dir}")
    };
    format!("{endpoint}/api/datasets/{repo}/tree/{revision}{sub}?recursive=true")
}

pub struct Client {
    agent: ureq::Agent,
    endpoint: String,
    token: Option<String>,
}

/// Uç noktanın proxy'siz mi gitmesi gerektiği: yerel adresler ve `NO_PROXY` listesi
/// (ureq 2, `NO_PROXY`'yi kendisi uygulamaz).
pub fn bypass_proxy(endpoint: &str, no_proxy: &str) -> bool {
    let host = endpoint
        .split("://")
        .nth(1)
        .unwrap_or(endpoint)
        .split(['/', '?'])
        .next()
        .unwrap_or("");
    let host = host
        .rsplit_once(':')
        .filter(|(h, p)| !h.is_empty() && p.chars().all(|c| c.is_ascii_digit()))
        .map(|(h, _)| h)
        .unwrap_or(host);
    let host = host.trim_matches(['[', ']']).to_lowercase();
    if host == "localhost" || host == "::1" || host.starts_with("127.") {
        return true;
    }
    no_proxy
        .split(',')
        .map(|e| e.trim().to_lowercase())
        .filter(|e| !e.is_empty())
        .any(|e| {
            if e == "*" {
                return true;
            }
            let suffix = e.trim_start_matches('*').trim_start_matches('.');
            host == suffix || host.ends_with(&format!(".{suffix}"))
        })
}

impl Client {
    pub fn new(endpoint: &str) -> Self {
        let no_proxy = std::env::var("NO_PROXY")
            .or_else(|_| std::env::var("no_proxy"))
            .unwrap_or_default();
        let use_proxy = !bypass_proxy(endpoint, &no_proxy);
        Self {
            agent: ureq::AgentBuilder::new()
                .try_proxy_from_env(use_proxy)
                .timeout_connect(Duration::from_secs(20))
                .timeout_read(Duration::from_secs(120))
                .build(),
            endpoint: endpoint.trim_end_matches('/').to_string(),
            token: std::env::var("HF_TOKEN").ok().filter(|t| !t.is_empty()),
        }
    }

    fn get(&self, url: &str) -> ureq::Request {
        let r = self.agent.get(url).set("User-Agent", "sosis-ai-downloader");
        match &self.token {
            Some(t) => r.set("Authorization", &format!("Bearer {t}")),
            None => r,
        }
    }

    pub fn list(&self, repo: &str, revision: &str, dir: &str) -> Result<Vec<RemoteFile>> {
        let url = tree_url(&self.endpoint, repo, revision, dir);
        match self.get(&url).call() {
            Ok(r) => Ok(r.into_json()?),
            Err(ureq::Error::Status(401 | 403, _)) => bail!(
                "{repo}: erişim reddedildi. Erişimi kısıtlı bir set olabilir: Hugging Face'te koşulları kabul edip \
                 HF_TOKEN ortam değişkenini ayarlayın."
            ),
            Err(e) => Err(e).with_context(|| format!("liste alınamadı: {url}")),
        }
    }

    /// Dosyayı indirir; `.part` varsa kaldığı yerden sürer. Zaten tamsa atlar. İndirilen bayt sayısı.
    pub fn download(
        &self,
        repo: &str,
        revision: &str,
        file: &RemoteFile,
        dest: &Path,
        mut progress: impl FnMut(u64),
    ) -> Result<u64> {
        if fs::metadata(dest)
            .map(|m| m.len() == file.size)
            .unwrap_or(false)
        {
            return Ok(0);
        }
        if let Some(d) = dest.parent() {
            fs::create_dir_all(d)?;
        }
        let part = PathBuf::from(format!("{}.part", dest.display()));
        let have = fs::metadata(&part).map(|m| m.len()).unwrap_or(0);
        let url = format!(
            "{}/datasets/{repo}/resolve/{revision}/{}",
            self.endpoint, file.path
        );
        let mut req = self.get(&url);
        if have > 0 && have < file.size {
            req = req.set("Range", &format!("bytes={have}-"));
        }
        let resp = req
            .call()
            .with_context(|| format!("indirilemedi: {}", file.path))?;
        // Sunucu Range'i desteklemediyse baştan yaz.
        let append = have > 0 && resp.status() == 206;
        let mut out = fs::OpenOptions::new()
            .create(true)
            .append(append)
            .write(true)
            .truncate(!append)
            .open(&part)?;
        let mut reader = resp.into_reader();
        let mut buf = vec![0u8; 1 << 20];
        let mut written = if append { have } else { 0 };
        let mut fresh = 0u64;
        loop {
            let n = reader.read(&mut buf)?;
            if n == 0 {
                break;
            }
            out.write_all(&buf[..n])?;
            written += n as u64;
            fresh += n as u64;
            progress(written);
        }
        out.flush()?;
        if file.size > 0 && written != file.size {
            bail!(
                "{}: boyut uyuşmuyor ({} / {} bayt); tekrar çalıştırınca kaldığı yerden sürer",
                file.path,
                written,
                file.size
            );
        }
        fs::rename(&part, dest)?;
        Ok(fresh)
    }
}

/// Seçilen dosyaları `out/<repo>/<yol>` altına indirir.
pub fn run(
    endpoint: &str,
    repo: &str,
    revision: &str,
    dir: &str,
    include: &[String],
    out: &Path,
    max_gb: f64,
) -> Result<Vec<PathBuf>> {
    let client = Client::new(endpoint);
    let files = client.list(repo, revision, dir)?;
    let chosen = select(&files, include, (max_gb * 1024f64.powi(3)) as u64);
    if chosen.is_empty() {
        bail!(
            "eşleşen dosya yok ({} dosya listelendi; --include desenini kontrol edin)",
            files.len()
        );
    }
    let total: u64 = chosen.iter().map(|f| f.size).sum();
    eprintln!(
        "{} dosya, toplam {:.2} GB",
        chosen.len(),
        total as f64 / 1024f64.powi(3)
    );
    let base = out.join(repo.replace('/', "__"));
    let mut paths = Vec::new();
    for (i, f) in chosen.iter().enumerate() {
        // Sunucudan gelen yol klasör dışına çıkamaz.
        if f.path.split('/').any(|c| c == ".." || c.is_empty()) {
            bail!("güvenli olmayan dosya yolu: {}", f.path);
        }
        let dest = base.join(&f.path);
        let got = client.download(repo, revision, f, &dest, |w| {
            eprint!(
                "\r  [{}/{}] {} {:.0}%",
                i + 1,
                chosen.len(),
                f.path,
                100.0 * w as f64 / f.size.max(1) as f64
            )
        })?;
        eprintln!(
            "\r  [{}/{}] {} {}",
            i + 1,
            chosen.len(),
            f.path,
            if got == 0 { "(zaten var)" } else { "✓" }
        );
        paths.push(dest);
    }
    Ok(paths)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicUsize, Ordering};
    use std::sync::Arc;

    #[test]
    fn glob() {
        assert!(glob_match(
            "data/python/*.parquet",
            "data/python/train-0001.parquet"
        ));
        assert!(!glob_match(
            "data/python/*.parquet",
            "data/python/x/train.parquet"
        ));
        assert!(glob_match(
            "data/**/*.parquet",
            "data/python/x/train.parquet"
        ));
        assert!(glob_match("**", "a/b/c"));
        assert!(glob_match("d?ta/*", "data/x"));
        assert!(!glob_match("*.json", "a.jsonl"));
    }

    #[test]
    fn urls() {
        assert_eq!(
            tree_url("https://h", "a/b", "main", ""),
            "https://h/api/datasets/a/b/tree/main?recursive=true"
        );
        assert_eq!(
            tree_url("https://h", "a/b", "main", "/data/"),
            "https://h/api/datasets/a/b/tree/main/data?recursive=true"
        );
    }

    #[test]
    fn proxy_bypass_rules() {
        assert!(bypass_proxy("http://127.0.0.1:8080", ""));
        assert!(bypass_proxy("http://localhost:1/x", ""));
        assert!(!bypass_proxy(
            "https://huggingface.co",
            "localhost,.svc.local"
        ));
        assert!(bypass_proxy(
            "https://cdn.huggingface.co",
            "*.huggingface.co"
        ));
        assert!(bypass_proxy("https://huggingface.co", "huggingface.co"));
        assert!(bypass_proxy("https://x.y", "*"));
    }

    #[test]
    fn select_respects_budget() {
        let f = |p: &str, s: u64| RemoteFile {
            kind: "file".into(),
            path: p.into(),
            size: s,
        };
        let files = vec![
            f("b.parquet", 50),
            f("a.parquet", 60),
            f("c.txt", 1),
            RemoteFile {
                kind: "directory".into(),
                path: "d".into(),
                size: 0,
            },
        ];
        let s = select(&files, &["*.parquet".into()], 100);
        assert_eq!(
            s.iter().map(|x| x.path.as_str()).collect::<Vec<_>>(),
            vec!["a.parquet"]
        );
        // Tek dosya sınırdan büyükse yine de en az bir dosya alınır.
        assert_eq!(select(&files, &["*.parquet".into()], 10).len(), 1);
    }

    /// Yerel taklit sunucu: liste + Range destekli indirme; ilk indirme yarıda kesilir.
    #[test]
    fn downloads_and_resumes_from_mock_server() {
        let body: Vec<u8> = (0..300_000u32).map(|i| (i % 251) as u8).collect();
        let server = tiny_http::Server::http("127.0.0.1:0").unwrap();
        let port = server.server_addr().to_ip().unwrap().port();
        let body_s = Arc::new(body.clone());
        let hits = Arc::new(AtomicUsize::new(0));
        let hits_s = hits.clone();
        std::thread::spawn(move || {
            for req in server.incoming_requests() {
                let url = req.url().to_string();
                if url.starts_with("/api/datasets/org/set/tree/main") {
                    let list = format!(
                        r#"[{{"type":"directory","path":"data","size":0}},{{"type":"file","path":"data/a.parquet","size":{}}},{{"type":"file","path":"README.md","size":5}}]"#,
                        body_s.len()
                    );
                    let _ = req.respond(tiny_http::Response::from_string(list));
                    continue;
                }
                let n = hits_s.fetch_add(1, Ordering::SeqCst);
                let range = req
                    .headers()
                    .iter()
                    .find(|h| h.field.equiv("Range"))
                    .and_then(|h| {
                        h.value
                            .as_str()
                            .strip_prefix("bytes=")
                            .and_then(|v| v.trim_end_matches('-').parse::<usize>().ok())
                    });
                match (n, range) {
                    // İlk istek: verinin yalnızca bir kısmı gönderilir (kopma).
                    (0, _) => {
                        let _ =
                            req.respond(tiny_http::Response::from_data(body_s[..100_000].to_vec()));
                    }
                    (_, Some(start)) => {
                        let r = tiny_http::Response::from_data(body_s[start..].to_vec())
                            .with_status_code(206);
                        let _ = req.respond(r);
                    }
                    _ => {
                        let _ = req.respond(tiny_http::Response::from_data(body_s.to_vec()));
                    }
                }
            }
        });
        let out = std::env::temp_dir().join(format!("sosis_dl_{}", std::process::id()));
        let _ = fs::remove_dir_all(&out);
        let endpoint = format!("http://127.0.0.1:{port}");
        let inc = vec!["data/*.parquet".to_string()];
        // 1. deneme: boyut uyuşmaz → hata, .part kalır.
        let e = run(&endpoint, "org/set", "main", "", &inc, &out, 1.0);
        assert!(e.is_err());
        let part = out.join("org__set/data/a.parquet.part");
        assert_eq!(fs::metadata(&part).unwrap().len(), 100_000);
        // 2. deneme: Range ile kaldığı yerden sürer, dosya tam ve doğru.
        let paths = run(&endpoint, "org/set", "main", "", &inc, &out, 1.0).unwrap();
        assert_eq!(fs::read(&paths[0]).unwrap(), body);
        assert!(!part.exists());
        // 3. deneme: zaten var, indirme yapılmaz.
        let before = hits.load(Ordering::SeqCst);
        run(&endpoint, "org/set", "main", "", &inc, &out, 1.0).unwrap();
        assert_eq!(hits.load(Ordering::SeqCst), before);
        fs::remove_dir_all(&out).ok();
    }
}
