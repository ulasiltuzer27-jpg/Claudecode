//! Kaynak toplama: klasörleri tarar, kod dosyalarını ve `.jsonl` veri setlerini okur.

use std::fs;
use std::io::{BufRead, BufReader};
use std::path::{Path, PathBuf};

use anyhow::Result;

/// Eğitime alınan dosya uzantıları.
pub const DEFAULT_EXTENSIONS: &[&str] = &[
    "rs", "py", "js", "mjs", "ts", "tsx", "jsx", "c", "h", "cc", "cpp", "hpp", "cs", "java", "kt",
    "go", "swift", "rb", "php", "lua", "sh", "bash", "sql", "html", "css", "scss", "toml", "yaml",
    "yml", "md", "zig", "dart", "scala", "hs", "ml", "ex", "exs", "r", "jl",
];

/// Taranmayacak klasörler.
const SKIP_DIRS: &[&str] = &[
    ".git",
    ".hg",
    ".svn",
    "target",
    "node_modules",
    "vendor",
    "dist",
    "build",
    "out",
    "bin",
    "obj",
    "__pycache__",
    ".venv",
    "venv",
    ".idea",
    ".vscode",
    ".next",
    "third_party",
];

/// Tek bir belge (kaynak dosya).
pub struct Document {
    /// Göreli yol / dosya adı (model başlığında kullanılır), ör. `src/main.rs`.
    pub path: String,
    pub content: String,
}

/// Bir kaynak: tek bir kod dosyası ya da çok belgeli bir `.jsonl` dosyası.
#[derive(Debug, Clone)]
pub enum Source {
    File { path: PathBuf, rel: String },
    Jsonl(PathBuf),
}

pub struct CollectOptions {
    pub extensions: Vec<String>,
    pub max_file_bytes: u64,
}

impl Default for CollectOptions {
    fn default() -> Self {
        Self {
            extensions: DEFAULT_EXTENSIONS.iter().map(|s| s.to_string()).collect(),
            max_file_bytes: 1 << 20,
        }
    }
}

/// Verilen kök yollarından kaynak listesini çıkarır (sıralı, deterministik).
pub fn find_sources(roots: &[PathBuf], opts: &CollectOptions) -> Result<Vec<Source>> {
    let mut out = Vec::new();
    for root in roots {
        if root.is_file() {
            push_file(root, root.parent().unwrap_or(Path::new("")), opts, &mut out);
        } else {
            walk(root, root, opts, &mut out)?;
        }
    }
    Ok(out)
}

fn walk(root: &Path, dir: &Path, opts: &CollectOptions, out: &mut Vec<Source>) -> Result<()> {
    let mut entries: Vec<_> = fs::read_dir(dir)?.filter_map(|e| e.ok()).collect();
    entries.sort_by_key(|e| e.file_name());
    for e in entries {
        let path = e.path();
        let Ok(ft) = e.file_type() else { continue };
        if ft.is_dir() {
            let name = e.file_name();
            let name = name.to_string_lossy();
            if SKIP_DIRS.contains(&name.as_ref()) {
                continue;
            }
            walk(root, &path, opts, out)?;
        } else if ft.is_file() {
            push_file(&path, root, opts, out);
        }
    }
    Ok(())
}

fn push_file(path: &Path, root: &Path, opts: &CollectOptions, out: &mut Vec<Source>) {
    let ext = path
        .extension()
        .map(|e| e.to_string_lossy().to_lowercase())
        .unwrap_or_default();
    if ext == "jsonl" {
        out.push(Source::Jsonl(path.to_path_buf()));
        return;
    }
    if !opts.extensions.contains(&ext) {
        return;
    }
    if fs::metadata(path)
        .map(|m| m.len() > opts.max_file_bytes || m.len() == 0)
        .unwrap_or(true)
    {
        return;
    }
    let rel = path
        .strip_prefix(root)
        .unwrap_or(path)
        .to_string_lossy()
        .replace('\\', "/");
    out.push(Source::File {
        path: path.to_path_buf(),
        rel,
    });
}

/// Bir kaynaktaki belgeleri sırayla `f`'ye verir.
///
/// `.jsonl` satırları `content` (veya `text`) alanını, varsa `path`
/// (veya `max_stars_repo_path`) alanını kullanır.
pub fn read_source(src: &Source, mut f: impl FnMut(Document)) -> Result<()> {
    match src {
        Source::File { path, rel } => {
            let Ok(bytes) = fs::read(path) else {
                return Ok(());
            };
            let Ok(content) = String::from_utf8(bytes) else {
                return Ok(());
            };
            f(Document {
                path: rel.clone(),
                content,
            });
        }
        Source::Jsonl(path) => {
            let reader = BufReader::new(fs::File::open(path)?);
            for line in reader.lines() {
                let Ok(line) = line else { continue };
                let Ok(v) = serde_json::from_str::<serde_json::Value>(&line) else {
                    continue;
                };
                let content = v
                    .get("content")
                    .or_else(|| v.get("text"))
                    .and_then(|c| c.as_str());
                let Some(content) = content else { continue };
                let p = v
                    .get("path")
                    .or_else(|| v.get("max_stars_repo_path"))
                    .and_then(|p| p.as_str())
                    .unwrap_or("");
                f(Document {
                    path: p.to_string(),
                    content: content.to_string(),
                });
            }
        }
    }
    Ok(())
}
