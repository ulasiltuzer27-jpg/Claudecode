//! Kaynak toplama: klasörleri tarar, kod dosyalarını ve `.jsonl` / `.parquet` veri setlerini okur.

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

/// Bir kaynak: tek bir kod dosyası ya da çok belgeli bir `.jsonl` / `.parquet` dosyası.
#[derive(Debug, Clone)]
pub enum Source {
    File { path: PathBuf, rel: String },
    Jsonl(PathBuf),
    Parquet(PathBuf),
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
    if ext == "parquet" {
        out.push(Source::Parquet(path.to_path_buf()));
        return;
    }
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
/// `.jsonl` satırları ve `.parquet` satırları `content` (veya `text`) alanını, varsa `path`
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
        Source::Parquet(path) => read_parquet(path, f)?,
    }
    Ok(())
}

const CONTENT_COLUMNS: &[&str] = &["content", "text", "code"];
const PATH_COLUMNS: &[&str] = &["path", "max_stars_repo_path", "file_path", "filename"];

/// `.parquet` veri setleri (ör. The Stack'ten indirilmiş dosyalar).
fn read_parquet(path: &Path, mut f: impl FnMut(Document)) -> Result<()> {
    use parquet::file::reader::{FileReader, SerializedFileReader};
    use parquet::record::Field;

    let reader = SerializedFileReader::new(fs::File::open(path)?)
        .map_err(|e| anyhow::anyhow!("parquet açılamadı {}: {e}", path.display()))?;
    let as_str = |field: &Field| -> Option<String> {
        match field {
            Field::Str(s) => Some(s.clone()),
            Field::Bytes(b) => std::str::from_utf8(b.data()).ok().map(str::to_string),
            _ => None,
        }
    };
    for row in reader.get_row_iter(None)? {
        let Ok(row) = row else { continue };
        let (mut content, mut p) = (None, None);
        for (name, field) in row.get_column_iter() {
            let name = name.as_str();
            if content.is_none() && CONTENT_COLUMNS.contains(&name) {
                content = as_str(field);
            } else if p.is_none() && PATH_COLUMNS.contains(&name) {
                p = as_str(field);
            }
        }
        if let Some(content) = content {
            f(Document {
                path: p.unwrap_or_default(),
                content,
            });
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use parquet::data_type::{ByteArray, ByteArrayType};
    use parquet::file::properties::WriterProperties;
    use parquet::file::writer::SerializedFileWriter;
    use parquet::schema::parser::parse_message_type;
    use std::sync::Arc;

    #[test]
    fn reads_parquet_rows() {
        let dir = std::env::temp_dir().join(format!("sosis_pq_{}", std::process::id()));
        fs::create_dir_all(&dir).unwrap();
        let path = dir.join("stack.parquet");
        let schema = Arc::new(
            parse_message_type(
                "message s { REQUIRED BYTE_ARRAY path (UTF8); REQUIRED INT64 size; REQUIRED BYTE_ARRAY content (UTF8); }",
            )
            .unwrap(),
        );
        let mut w = SerializedFileWriter::new(
            fs::File::create(&path).unwrap(),
            schema,
            Arc::new(WriterProperties::builder().build()),
        )
        .unwrap();
        let mut rg = w.next_row_group().unwrap();
        let mut col = 0;
        while let Some(mut c) = rg.next_column().unwrap() {
            match col {
                0 => {
                    let v: Vec<ByteArray> = vec!["a.rs".into(), "b.py".into()];
                    c.typed::<ByteArrayType>()
                        .write_batch(&v, None, None)
                        .unwrap();
                }
                1 => {
                    c.typed::<parquet::data_type::Int64Type>()
                        .write_batch(&[1, 2], None, None)
                        .unwrap();
                }
                _ => {
                    let v: Vec<ByteArray> = vec!["fn a() {}".into(), "def b(): pass".into()];
                    c.typed::<ByteArrayType>()
                        .write_batch(&v, None, None)
                        .unwrap();
                }
            }
            c.close().unwrap();
            col += 1;
        }
        rg.close().unwrap();
        w.close().unwrap();

        let srcs = find_sources(std::slice::from_ref(&dir), &CollectOptions::default()).unwrap();
        assert!(matches!(srcs[0], Source::Parquet(_)));
        let mut docs = vec![];
        read_source(&srcs[0], |d| docs.push((d.path, d.content))).unwrap();
        assert_eq!(
            docs,
            vec![
                ("a.rs".to_string(), "fn a() {}".to_string()),
                ("b.py".to_string(), "def b(): pass".to_string())
            ]
        );
        fs::remove_dir_all(&dir).ok();
    }
}
