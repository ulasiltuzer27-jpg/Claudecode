//! Talimat ince ayarı (SFT) verisi.
//!
//! İki kaynak:
//! 1. `.jsonl` dosyaları: `{"messages": [{"role": "user", "content": ...}, ...]}` ya da
//!    `{"instruction": ..., "input"?: ..., "response"|"output": ..., "system"?: ...}`.
//! 2. **API'siz otomatik veri**: kodunuzdaki belgelenmiş fonksiyonlar. Açıklama (`///`, C#
//!    `<summary>`, Python docstring) soruya, fonksiyonun kendisi cevaba dönüşür.

use std::collections::hash_map::DefaultHasher;
use std::fs;
use std::hash::{Hash, Hasher};
use std::io::{BufRead, BufReader};
use std::path::{Path, PathBuf};

use anyhow::{bail, Result};
use serde::{Deserialize, Serialize};
use sosis_tokenizer::chat::{encode_chat, Message, Role, DEFAULT_SYSTEM};
use sosis_tokenizer::Tokenizer;

use crate::collect::{self, CollectOptions, Document};
use crate::shard::{mask_path, MaskWriter, ShardWriter};
use crate::{DataMeta, META_FILE, TRAIN_FILE, VAL_FILE};

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct SftExample {
    pub messages: Vec<Message>,
}

impl SftExample {
    pub fn qa(question: impl Into<String>, answer: impl Into<String>) -> Self {
        Self {
            messages: vec![
                Message::new(Role::User, question),
                Message::new(Role::Assistant, answer),
            ],
        }
    }
}

/// `.jsonl` SFT dosyasını okur; tanınmayan satırları atlar.
pub fn read_jsonl(path: &Path) -> Result<Vec<SftExample>> {
    #[derive(Deserialize)]
    struct Flat {
        instruction: Option<String>,
        input: Option<String>,
        response: Option<String>,
        output: Option<String>,
        system: Option<String>,
        messages: Option<Vec<Message>>,
    }
    let mut out = Vec::new();
    for line in BufReader::new(fs::File::open(path)?).lines() {
        let Ok(line) = line else { continue };
        let Ok(f) = serde_json::from_str::<Flat>(&line) else {
            continue;
        };
        if let Some(m) = f.messages {
            if m.iter().any(|m| m.role == Role::Assistant) {
                out.push(SftExample { messages: m });
            }
            continue;
        }
        let (Some(q), Some(a)) = (f.instruction, f.response.or(f.output)) else {
            continue;
        };
        let q = match f.input.filter(|i| !i.trim().is_empty()) {
            Some(i) => format!("{q}\n\n{i}"),
            None => q,
        };
        let mut messages = vec![];
        if let Some(s) = f.system {
            messages.push(Message::new(Role::System, s));
        }
        messages.push(Message::new(Role::User, q));
        messages.push(Message::new(Role::Assistant, a));
        out.push(SftExample { messages });
    }
    Ok(out)
}

pub fn write_jsonl(path: &Path, examples: &[SftExample]) -> Result<()> {
    let mut s = String::new();
    for e in examples {
        s.push_str(&serde_json::to_string(e)?);
        s.push('\n');
    }
    fs::write(path, s)?;
    Ok(())
}

#[derive(Clone, Copy)]
struct LangInfo {
    name: &'static str,
    fence: &'static str,
}

fn lang_of(path: &str) -> Option<LangInfo> {
    let ext = Path::new(path).extension()?.to_str()?.to_lowercase();
    Some(match ext.as_str() {
        "rs" => LangInfo {
            name: "Rust",
            fence: "rust",
        },
        "cs" => LangInfo {
            name: "C#",
            fence: "csharp",
        },
        "py" => LangInfo {
            name: "Python",
            fence: "python",
        },
        _ => return None,
    })
}

/// Bir fonksiyon: açıklaması, imza satırı ve (açıklama dahil) tam kodu.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DocumentedFn {
    pub doc: String,
    pub signature: String,
    pub code: String,
}

const MIN_DOC_CHARS: usize = 12;
const MAX_FN_LINES: usize = 80;

fn dedent(lines: &[&str]) -> String {
    let indent = lines
        .iter()
        .filter(|l| !l.trim().is_empty())
        .map(|l| l.len() - l.trim_start().len())
        .min()
        .unwrap_or(0);
    lines
        .iter()
        .map(|l| {
            if l.len() >= indent {
                &l[indent..]
            } else {
                l.trim_start()
            }
        })
        .collect::<Vec<_>>()
        .join("\n")
}

fn strip_xml(s: &str) -> String {
    let mut out = String::new();
    let mut in_tag = false;
    for c in s.chars() {
        match c {
            '<' => in_tag = true,
            '>' if in_tag => {
                in_tag = false;
                out.push(' ');
            }
            _ if !in_tag => out.push(c),
            _ => {}
        }
    }
    out.split_whitespace().collect::<Vec<_>>().join(" ")
}

/// `///` ile belgelenmiş, süslü parantezli fonksiyonlar (Rust, C#).
fn extract_braced(text: &str, is_rust: bool) -> Vec<DocumentedFn> {
    let lines: Vec<&str> = text.lines().collect();
    let mut out = Vec::new();
    let mut i = 0;
    while i < lines.len() {
        if !lines[i].trim_start().starts_with("///") {
            i += 1;
            continue;
        }
        let doc_start = i;
        while i < lines.len() && lines[i].trim_start().starts_with("///") {
            i += 1;
        }
        let doc_raw: Vec<&str> = lines[doc_start..i]
            .iter()
            .map(|l| l.trim_start().trim_start_matches('/').trim())
            .collect();
        // Öznitelikleri atla (#[...] / [Attr]).
        while i < lines.len() && {
            let t = lines[i].trim_start();
            t.starts_with("#[") || (!is_rust && t.starts_with('['))
        } {
            i += 1;
        }
        let Some(sig_line) = lines.get(i) else { break };
        let t = sig_line.trim();
        let looks_like_fn = if is_rust {
            t.contains("fn ") && t.contains('(')
        } else {
            t.contains('(')
                && !t.ends_with(';')
                && ![
                    "class ",
                    "struct ",
                    "enum ",
                    "interface ",
                    "namespace ",
                    "record ",
                    "delegate ",
                    "event ",
                ]
                .iter()
                .any(|k| t.contains(k))
                && !t.starts_with("if")
                && !t.starts_with("return")
                && !t.contains(" = ")
        };
        if !looks_like_fn {
            continue;
        }
        // İmzadan gövde sonuna kadar parantez dengesi.
        let sig_start = i;
        let mut depth = 0i64;
        let mut opened = false;
        let mut end = None;
        for (j, line) in lines.iter().enumerate().skip(sig_start).take(MAX_FN_LINES) {
            for c in line.chars() {
                match c {
                    '{' => {
                        depth += 1;
                        opened = true;
                    }
                    '}' => depth -= 1,
                    _ => {}
                }
            }
            if !opened && (line.trim_end().ends_with(';') || line.contains("=>")) {
                break; // gövdesiz bildirim veya ifade gövdesi
            }
            if opened && depth <= 0 {
                end = Some(j);
                break;
            }
        }
        let Some(end) = end else {
            i = sig_start + 1;
            continue;
        };
        let doc = if is_rust {
            doc_raw
                .join(" ")
                .split_whitespace()
                .collect::<Vec<_>>()
                .join(" ")
        } else {
            strip_xml(&doc_raw.join(" "))
        };
        if doc.chars().count() >= MIN_DOC_CHARS && end > sig_start {
            out.push(DocumentedFn {
                doc,
                signature: t.trim_end_matches('{').trim().to_string(),
                code: dedent(&lines[doc_start..=end]),
            });
        }
        i = end + 1;
    }
    out
}

/// Docstring'li Python fonksiyonları.
fn extract_python(text: &str) -> Vec<DocumentedFn> {
    let lines: Vec<&str> = text.lines().collect();
    let indent = |l: &str| l.len() - l.trim_start().len();
    let mut out = Vec::new();
    let mut i = 0;
    while i < lines.len() {
        let t = lines[i].trim_start();
        if !(t.starts_with("def ") || t.starts_with("async def ")) {
            i += 1;
            continue;
        }
        let def_indent = indent(lines[i]);
        let start = i;
        // Çok satırlı imza.
        while i < lines.len() && !lines[i].trim_end().ends_with(':') {
            i += 1;
        }
        let Some(_) = lines.get(i) else { break };
        let signature = lines[start..=i]
            .iter()
            .map(|l| l.trim())
            .collect::<Vec<_>>()
            .join(" ");
        i += 1;
        while i < lines.len() && lines[i].trim().is_empty() {
            i += 1;
        }
        let Some(first) = lines.get(i) else { break };
        let ft = first.trim();
        let quote = if ft.starts_with("\"\"\"") {
            "\"\"\""
        } else if ft.starts_with("'''") {
            "'''"
        } else {
            continue;
        };
        // Docstring.
        let mut doc_lines = vec![];
        let rest = &ft[3..];
        if let Some(p) = rest.find(quote) {
            doc_lines.push(&rest[..p]);
        } else {
            doc_lines.push(rest);
            i += 1;
            while i < lines.len() {
                if let Some(p) = lines[i].find(quote) {
                    doc_lines.push(&lines[i][..p]);
                    break;
                }
                doc_lines.push(lines[i]);
                i += 1;
            }
        }
        // Gövde: daha fazla girintili veya boş satırlar.
        let mut end = i;
        let mut j = i + 1;
        while j < lines.len() && j - start < MAX_FN_LINES {
            let l = lines[j];
            if !l.trim().is_empty() {
                if indent(l) <= def_indent {
                    break;
                }
                end = j;
            }
            j += 1;
        }
        if j - start >= MAX_FN_LINES {
            i = j;
            continue;
        }
        let doc = doc_lines
            .join(" ")
            .split_whitespace()
            .collect::<Vec<_>>()
            .join(" ");
        if doc.chars().count() >= MIN_DOC_CHARS && end > i.min(end) {
            out.push(DocumentedFn {
                doc,
                signature: signature.trim_end_matches(':').to_string(),
                code: dedent(&lines[start..=end]),
            });
        }
        i = end + 1;
    }
    out
}

/// Bir belgedeki belgelenmiş fonksiyonlar (desteklenen diller: Rust, C#, Python).
pub fn documented_functions(doc: &Document) -> Vec<DocumentedFn> {
    match lang_of(&doc.path).map(|l| l.fence) {
        Some("rust") => extract_braced(&doc.content, true),
        Some("csharp") => extract_braced(&doc.content, false),
        Some("python") => extract_python(&doc.content),
        _ => vec![],
    }
}

const TEMPLATES: &[&str] = &[
    "{lang} dilinde şu işi yapan bir fonksiyon yaz: {doc}",
    "Bir {lang} fonksiyonu yazar mısın? Görevi: {doc}",
    "Write a {lang} function that does the following: {doc}",
    "{doc}\n\nBunu {lang} ile nasıl yazarım?",
    "Şunu yapan {lang} kodunu yaz: {doc}",
];

fn hash_of(s: &str) -> u64 {
    let mut h = DefaultHasher::new();
    s.hash(&mut h);
    h.finish()
}

/// Belgelenmiş bir fonksiyondan soru-cevap örneği üretir (deterministik şablon seçimi).
pub fn example_from_fn(f: &DocumentedFn, path: &str) -> Option<SftExample> {
    let lang = lang_of(path)?;
    let h = hash_of(&f.code);
    let mut q = TEMPLATES[(h % TEMPLATES.len() as u64) as usize]
        .replace("{lang}", lang.name)
        .replace("{doc}", &f.doc);
    if (h >> 8).is_multiple_of(2) {
        q.push_str(&format!("\nİmza: `{}`", f.signature));
    }
    let a = format!(
        "İşte {} fonksiyonu:\n\n```{}\n{}\n```",
        lang.name, lang.fence, f.code
    );
    Some(SftExample::qa(q, a))
}

/// Klasörlerdeki koddan SFT örnekleri çıkarır (tekrarlar elenir).
pub fn extract_from_code(
    inputs: &[PathBuf],
    collect_opts: &CollectOptions,
) -> Result<Vec<SftExample>> {
    let mut seen = std::collections::HashSet::new();
    let mut out = Vec::new();
    for src in collect::find_sources(inputs, collect_opts)? {
        collect::read_source(&src, |doc| {
            for f in documented_functions(&doc) {
                if seen.insert(hash_of(&f.code)) {
                    if let Some(e) = example_from_fn(&f, &doc.path) {
                        out.push(e);
                    }
                }
            }
        })?;
    }
    Ok(out)
}

/// SFT örneklerini maskeli shard'lara yazar (`train.bin` + `train.mask`, `val.*`, `meta.json`).
/// Sistem mesajı olmayan örneklere `system` (yoksa varsayılan) eklenir.
pub fn prepare_sft(
    tok: &Tokenizer,
    examples: &[SftExample],
    out_dir: &Path,
    val_percent: f64,
    system: Option<&str>,
) -> Result<DataMeta> {
    if examples.is_empty() {
        bail!("hiç SFT örneği yok");
    }
    fs::create_dir_all(out_dir)?;
    let train_path = out_dir.join(TRAIN_FILE);
    let val_path = out_dir.join(VAL_FILE);
    let (mut tw, mut vw) = (
        ShardWriter::create(&train_path)?,
        ShardWriter::create(&val_path)?,
    );
    let (mut tm, mut vm) = (
        MaskWriter::create(&mask_path(&train_path))?,
        MaskWriter::create(&mask_path(&val_path))?,
    );
    let sys = Message::new(Role::System, system.unwrap_or(DEFAULT_SYSTEM));
    let mut docs = 0;
    for e in examples {
        let mut msgs = e.messages.clone();
        if !msgs.iter().any(|m| m.role == Role::System) {
            msgs.insert(0, sys.clone());
        }
        let (ids, mask) = encode_chat(tok, &msgs, false);
        let ids: Vec<u16> = ids.into_iter().map(|i| i as u16).collect();
        let h = hash_of(&serde_json::to_string(e)?);
        let is_val = ((h % 10_000) as f64) < val_percent * 100.0;
        if is_val {
            vw.write(&ids)?;
            vm.write(&mask)?;
        } else {
            tw.write(&ids)?;
            tm.write(&mask)?;
        }
        docs += 1;
    }
    tm.finish()?;
    vm.finish()?;
    let meta = DataMeta {
        vocab_size: tok.vocab_size(),
        train_tokens: tw.finish()?,
        val_tokens: vw.finish()?,
        docs,
        ..Default::default()
    };
    fs::write(
        out_dir.join(META_FILE),
        serde_json::to_string_pretty(&meta)?,
    )?;
    Ok(meta)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn doc(path: &str, content: &str) -> Document {
        Document {
            path: path.into(),
            content: content.into(),
        }
    }

    #[test]
    fn extracts_rust() {
        let src = "use x;\n\n/// İki sayıyı toplayıp sonucu döndürür.\n#[inline]\npub fn add(a: i32, b: i32) -> i32 {\n    if a > 0 { a + b } else { b + a }\n}\n\n/// kısa\nfn x() {}\n";
        let fns = documented_functions(&doc("lib.rs", src));
        assert_eq!(fns.len(), 1);
        assert_eq!(fns[0].doc, "İki sayıyı toplayıp sonucu döndürür.");
        assert_eq!(fns[0].signature, "pub fn add(a: i32, b: i32) -> i32");
        assert!(fns[0].code.starts_with("/// İki"));
        assert!(fns[0].code.ends_with('}'));
    }

    #[test]
    fn extracts_csharp_summary() {
        let src = "class P {\n    /// <summary>Oyuncunun canını verilen miktar kadar azaltır.</summary>\n    /// <param name=\"amount\">hasar</param>\n    public void TakeDamage(int amount)\n    {\n        Health -= amount;\n    }\n\n    /// <summary>Can değeri.</summary>\n    public int Health { get; set; }\n}\n";
        let fns = documented_functions(&doc("Player.cs", src));
        assert_eq!(fns.len(), 1, "{fns:?}");
        assert_eq!(
            fns[0].doc,
            "Oyuncunun canını verilen miktar kadar azaltır. hasar"
        );
        assert!(fns[0].code.contains("Health -= amount;"));
        assert!(fns[0].code.starts_with("/// <summary>"));
    }

    #[test]
    fn extracts_python() {
        let src = "import os\n\ndef slugify(text):\n    \"\"\"Metni URL dostu küçük harfli bir kısaltmaya çevirir.\"\"\"\n    t = text.lower()\n\n    return t.replace(' ', '-')\n\nprint(1)\n";
        let fns = documented_functions(&doc("u.py", src));
        assert_eq!(fns.len(), 1);
        assert_eq!(fns[0].signature, "def slugify(text)");
        assert!(fns[0].code.ends_with("return t.replace(' ', '-')"));
        let ex = example_from_fn(&fns[0], "u.py").unwrap();
        assert_eq!(ex.messages[0].role, Role::User);
        assert!(ex.messages[1].content.contains("```python\ndef slugify"));
    }

    #[test]
    fn reads_jsonl_formats() {
        let dir = std::env::temp_dir().join(format!("sosis_sft_{}", std::process::id()));
        fs::create_dir_all(&dir).unwrap();
        let p = dir.join("a.jsonl");
        fs::write(
            &p,
            concat!(
                r#"{"instruction": "Topla", "input": "1 ve 2", "output": "3"}"#,
                "\n",
                r#"{"messages": [{"role": "user", "content": "selam"}, {"role": "assistant", "content": "merhaba"}]}"#,
                "\nbozuk satır\n"
            ),
        )
        .unwrap();
        let ex = read_jsonl(&p).unwrap();
        assert_eq!(ex.len(), 2);
        assert_eq!(ex[0].messages[0].content, "Topla\n\n1 ve 2");
        assert_eq!(ex[1].messages[1].content, "merhaba");
        fs::remove_dir_all(&dir).ok();
    }
}
