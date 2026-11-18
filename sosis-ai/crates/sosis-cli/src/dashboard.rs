//! `sosis dashboard`: eğitimi canlı izleme paneli (salt-okunur).
//!
//! `log.csv`, `config.toml`, `state.json` ve `meta.json`'dan kayıp, doğrulama kaybı, token/s,
//! VRAM/RAM ve kalan süre tahmini gösterir. Varsayılan olarak yalnızca bu bilgisayardan erişilir;
//! `--lan` ile yerel ağdaki telefon/tabletten, başlangıçta yazdırılan anahtarla açılabilir.

use std::path::{Path, PathBuf};

use anyhow::Result;
use serde_json::{json, Map, Value};
use tiny_http::{Header, Response, Server};

const PAGE: &str = include_str!("web/dashboard.html");

/// `log.csv` → başlık adlarıyla nesne listesi (boş hücreler atlanır).
pub fn parse_log(text: &str) -> Vec<Value> {
    let mut lines = text.lines();
    let Some(header) = lines.next() else {
        return vec![];
    };
    let cols: Vec<&str> = header.split(',').collect();
    lines
        .filter_map(|l| {
            let mut row = Map::new();
            for (c, v) in cols.iter().zip(l.split(',')) {
                if let Ok(x) = v.trim().parse::<f64>() {
                    if x.is_finite() {
                        row.insert(c.to_string(), json!(x));
                    }
                }
            }
            row.contains_key("step").then_some(Value::Object(row))
        })
        .collect()
}

/// Panelin tek JSON durumu.
pub fn status(run: &Path) -> Value {
    let read = |f: &str| std::fs::read_to_string(run.join(f)).ok();
    let rows = read("log.csv").map(|t| parse_log(&t)).unwrap_or_default();
    let cfg = sosis_train::RunConfig::load(run.join(sosis_train::CONFIG_FILE)).ok();
    let (name, params, max_steps, tps_step) = match &cfg {
        Some(c) => (
            c.model.name.clone(),
            c.model.param_count(),
            c.train.max_steps,
            c.tokens_per_step(),
        ),
        None => ("?".into(), 0, 0, 0),
    };
    // Kalan süre: son 10 log satırının ortalama token/s'si ile.
    let recent: Vec<f64> = rows
        .iter()
        .rev()
        .take(10)
        .filter_map(|r| r.get("tokens_per_sec").and_then(Value::as_f64))
        .filter(|v| *v > 0.0)
        .collect();
    let step = rows
        .last()
        .and_then(|r| r.get("step"))
        .and_then(Value::as_f64)
        .unwrap_or(0.0);
    let eta = (!recent.is_empty() && max_steps > 0).then(|| {
        let tps = recent.iter().sum::<f64>() / recent.len() as f64;
        (max_steps as f64 - step).max(0.0) * tps_step as f64 / tps
    });
    let modified = std::fs::metadata(run.join("log.csv"))
        .and_then(|m| m.modified())
        .ok()
        .and_then(|t| t.elapsed().ok())
        .map(|d| d.as_secs());
    json!({
        "name": name,
        "params": params,
        "max_steps": max_steps,
        "tokens_per_step": tps_step,
        "rows": rows,
        "eta_seconds": eta,
        "seconds_since_update": modified,
        "bench": read("bench.csv").map(|t| parse_log(&t)).unwrap_or_default(),
        "state": read("state.json").and_then(|s| serde_json::from_str::<Value>(&s).ok()),
        "data": cfg.as_ref().and_then(|c| std::fs::read_to_string(c.train.data_dir.join("meta.json")).ok())
            .and_then(|s| serde_json::from_str::<Value>(&s).ok()),
    })
}

/// URL'deki `key=` değeri.
fn key_of(url: &str) -> Option<&str> {
    url.split_once('?')?
        .1
        .split('&')
        .find_map(|kv| kv.strip_prefix("key="))
}

fn random_key() -> String {
    use std::collections::hash_map::RandomState;
    use std::hash::{BuildHasher, Hasher};
    // RandomState, işletim sisteminin rastgelelik kaynağıyla tohumlanır.
    (0..2)
        .map(|_| {
            let mut h = RandomState::new().build_hasher();
            h.write_u128(
                std::time::SystemTime::now()
                    .elapsed()
                    .map(|d| d.as_nanos())
                    .unwrap_or(0),
            );
            format!("{:016x}", h.finish())
        })
        .collect()
}

pub fn serve(run: PathBuf, port: u16, lan: bool) -> Result<()> {
    let addr = if lan {
        format!("0.0.0.0:{port}")
    } else {
        format!("127.0.0.1:{port}")
    };
    let server = Server::http(&addr).map_err(|e| anyhow::anyhow!("{addr} açılamadı: {e}"))?;
    let key = lan.then(random_key);
    let local_hosts = [format!("127.0.0.1:{port}"), format!("localhost:{port}")];
    match &key {
        Some(k) => {
            eprintln!(
                "📈 Eğitim paneli (yerel ağ): http://<bu-bilgisayarın-IP'si>:{port}/?key={k}"
            );
            eprintln!("   Yalnızca okuma; anahtarı bilmeyen erişemez. Durdurmak için Ctrl+C.");
        }
        None => eprintln!("📈 Eğitim paneli: http://127.0.0.1:{port}  (durdurmak için Ctrl+C)"),
    }
    let html = Header::from_bytes("Content-Type", "text/html; charset=utf-8").unwrap();
    let js = Header::from_bytes("Content-Type", "application/json; charset=utf-8").unwrap();
    for req in server.incoming_requests() {
        let url = req.url().to_string();
        let host = req
            .headers()
            .iter()
            .find(|h| h.field.equiv("Host"))
            .map(|h| h.value.as_str().to_string())
            .unwrap_or_default();
        let allowed = match &key {
            Some(k) => key_of(&url) == Some(k.as_str()),
            None => local_hosts.contains(&host),
        };
        if !allowed {
            let _ = req.respond(Response::from_string("erişim yok").with_status_code(403));
            continue;
        }
        let path = url.split('?').next().unwrap_or("");
        let resp = match path {
            "/" => Response::from_string(PAGE).with_header(html.clone()),
            "/api/status" => {
                Response::from_string(status(&run).to_string()).with_header(js.clone())
            }
            _ => Response::from_string("bulunamadı").with_status_code(404),
        };
        let _ = req.respond(resp);
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_train_and_dpo_logs() {
        let t = "step,loss,val_loss,lr,tokens_per_sec,tokens_seen,vram_gb,ram_gb\n1,7.6,,0.001,3000,2048,,0.1\n2,7.1,6.9,0.001,3100,4096,1.5,0.1\n";
        let rows = parse_log(t);
        assert_eq!(rows.len(), 2);
        assert!(rows[0].get("val_loss").is_none());
        assert_eq!(rows[1]["val_loss"], json!(6.9));
        let d = parse_log("step,loss,reward_acc,margin,lr\n1,0.69,0,0,0.0001\n");
        assert_eq!(d[0]["reward_acc"], json!(0.0));
    }

    #[test]
    fn key_parsing() {
        assert_eq!(key_of("/api/status?key=abc&x=1"), Some("abc"));
        assert_eq!(key_of("/"), None);
        assert_eq!(random_key().len(), 32);
        assert_ne!(random_key(), random_key());
    }
}
