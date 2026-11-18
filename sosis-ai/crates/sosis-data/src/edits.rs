//! Düzenleme blokları: modelin dosya değişikliklerini ifade ettiği biçim. Hem git geçmişinden
//! üretilen eğitim örnekleri hem de proje ajanı bu biçimi kullanır.
//!
//! ```text
//! DOSYA: src/oyuncu.rs
//! <<<<<<< ARA
//! eski satırlar (bağlamla birlikte, dosyada tam olarak geçmeli)
//! =======
//! yeni satırlar
//! >>>>>>> DEĞİŞTİR
//! ```
//! `ARA` boşsa ve dosya yoksa dosya oluşturulur.

pub const FILE_TAG: &str = "DOSYA: ";
pub const SEARCH: &str = "<<<<<<< ARA";
pub const DIVIDER: &str = "=======";
pub const REPLACE: &str = ">>>>>>> DEĞİŞTİR";

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EditBlock {
    pub path: String,
    pub search: String,
    pub replace: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ApplyError {
    NotFound,
    Ambiguous(usize),
}

impl std::fmt::Display for ApplyError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ApplyError::NotFound => write!(f, "ARA bloğu dosyada bulunamadı"),
            ApplyError::Ambiguous(n) => write!(
                f,
                "ARA bloğu dosyada {n} kez geçiyor; daha fazla bağlam ekleyin"
            ),
        }
    }
}

/// Blokları metne çevirir.
pub fn format_blocks(blocks: &[EditBlock]) -> String {
    let mut out = String::new();
    let mut last_path: Option<&str> = None;
    for b in blocks {
        if last_path != Some(b.path.as_str()) {
            out.push_str(&format!("{FILE_TAG}{}\n", b.path));
            last_path = Some(&b.path);
        }
        out.push_str(SEARCH);
        out.push('\n');
        if !b.search.is_empty() {
            out.push_str(b.search.trim_end_matches('\n'));
            out.push('\n');
        }
        out.push_str(DIVIDER);
        out.push('\n');
        if !b.replace.is_empty() {
            out.push_str(b.replace.trim_end_matches('\n'));
            out.push('\n');
        }
        out.push_str(REPLACE);
        out.push('\n');
    }
    out
}

/// Metindeki blokları ayrıştırır (kod çitleri ve açıklamalar yok sayılır; yarım blok atlanır).
pub fn parse_blocks(text: &str) -> Vec<EditBlock> {
    let mut out = Vec::new();
    let mut path = String::new();
    let mut lines = text.lines().peekable();
    while let Some(line) = lines.next() {
        let t = line.trim();
        if let Some(p) = t.strip_prefix(FILE_TAG.trim_end()) {
            path = p.trim().trim_matches('`').to_string();
            continue;
        }
        if t != SEARCH || path.is_empty() {
            continue;
        }
        let mut search = Vec::new();
        let mut replace = Vec::new();
        let mut in_replace = false;
        let mut closed = false;
        for l in lines.by_ref() {
            match l.trim_end() {
                x if x == DIVIDER && !in_replace => in_replace = true,
                x if x == REPLACE => {
                    closed = true;
                    break;
                }
                _ if in_replace => replace.push(l),
                _ => search.push(l),
            }
        }
        if closed && in_replace {
            out.push(EditBlock {
                path: path.clone(),
                search: search.join("\n"),
                replace: replace.join("\n"),
            });
        }
    }
    out
}

/// Bloğu dosya içeriğine uygular: önce tam eşleşme, olmazsa satır başı/sonu boşluklarından
/// bağımsız satır eşleşmesi. Eşleşme tek olmalıdır.
pub fn apply_block(content: &str, b: &EditBlock) -> Result<String, ApplyError> {
    if b.search.trim().is_empty() {
        // Dosya oluşturma / sona ekleme.
        let mut s = content.to_string();
        if !s.is_empty() && !s.ends_with('\n') {
            s.push('\n');
        }
        s.push_str(&b.replace);
        if !s.ends_with('\n') {
            s.push('\n');
        }
        return Ok(s);
    }
    let n = content.matches(b.search.as_str()).count();
    if n == 1 {
        return Ok(content.replacen(b.search.as_str(), &b.replace, 1));
    }
    if n > 1 {
        return Err(ApplyError::Ambiguous(n));
    }
    // Boşluk-toleranslı: satırları kırpıp karşılaştır.
    let lines: Vec<&str> = content.split_inclusive('\n').collect();
    let want: Vec<&str> = b
        .search
        .lines()
        .map(str::trim)
        .filter(|l| !l.is_empty())
        .collect();
    if want.is_empty() {
        return Err(ApplyError::NotFound);
    }
    let mut hits = Vec::new();
    for start in 0..lines.len() {
        let mut i = start;
        let mut k = 0;
        while i < lines.len() && k < want.len() {
            let l = lines[i].trim();
            if l.is_empty() {
                i += 1;
                continue;
            }
            if l != want[k] {
                break;
            }
            i += 1;
            k += 1;
        }
        if k == want.len() && !lines[start].trim().is_empty() {
            hits.push((start, i));
        }
    }
    match hits.len() {
        0 => Err(ApplyError::NotFound),
        1 => {
            let (s, e) = hits[0];
            // Yeni bloğu, bulunan ilk satırın girintisine hizala.
            let indent: String = lines[s]
                .chars()
                .take_while(|c| c.is_whitespace() && *c != '\n')
                .collect();
            let first_indent: String = b
                .replace
                .lines()
                .next()
                .unwrap_or("")
                .chars()
                .take_while(|c| c.is_whitespace())
                .collect();
            let fixed: String = b
                .replace
                .lines()
                .map(|l| match l.strip_prefix(first_indent.as_str()) {
                    Some(rest) => format!("{indent}{rest}\n"),
                    None => format!("{l}\n"),
                })
                .collect();
            Ok(format!(
                "{}{}{}",
                lines[..s].concat(),
                fixed,
                lines[e..].concat()
            ))
        }
        n => Err(ApplyError::Ambiguous(n)),
    }
}

/// Ajan ve eğitim verisi için ortak istem: görev + ilgili kod → ARA/DEĞİŞTİR blokları.
pub fn edit_request(task: &str, snippets: &[(String, String)]) -> String {
    let mut s = format!("Görev: {}\n", task.trim());
    if !snippets.is_empty() {
        s.push_str("\nİlgili kod:\n");
        for (path, code) in snippets {
            s.push_str(&format!(
                "{FILE_TAG}{path}\n```\n{}\n```\n",
                code.trim_end()
            ));
        }
    }
    s.push_str("\nDeğişiklikleri DOSYA / ARA / DEĞİŞTİR bloklarıyla yaz.");
    s
}

#[cfg(test)]
mod tests {
    use super::*;

    const FILE: &str = "fn main() {\n    let x = 1;\n    println!(\"{}\", x);\n}\n";

    #[test]
    fn format_parse_roundtrip() {
        let blocks = vec![
            EditBlock {
                path: "src/a.rs".into(),
                search: "let x = 1;".into(),
                replace: "let x = 2;".into(),
            },
            EditBlock {
                path: "src/a.rs".into(),
                search: "".into(),
                replace: "fn yeni() {}".into(),
            },
            EditBlock {
                path: "b.py".into(),
                search: "a = 1".into(),
                replace: "".into(),
            },
        ];
        let text = format!("Açıklama...\n```\n{}```\n", format_blocks(&blocks));
        assert_eq!(parse_blocks(&text), blocks);
        // Yarım blok atlanır.
        assert!(parse_blocks("DOSYA: a\n<<<<<<< ARA\nx\n=======\ny\n").is_empty());
    }

    #[test]
    fn apply_exact_tolerant_and_errors() {
        let b = EditBlock {
            path: "m.rs".into(),
            search: "    let x = 1;".into(),
            replace: "    let x = 42;".into(),
        };
        assert!(apply_block(FILE, &b).unwrap().contains("let x = 42;"));
        // Girintisi farklı yazılmış ARA (model hatası) yine bulunur, girinti korunur.
        let loose = EditBlock {
            path: "m.rs".into(),
            search: "let x = 1;\nprintln!(\"{}\", x);".into(),
            replace: "let x = 7;\nprintln!(\"{x}\");".into(),
        };
        let r = apply_block(FILE, &loose).unwrap();
        assert!(
            r.contains("    let x = 7;\n    println!(\"{x}\");\n"),
            "{r}"
        );
        let missing = EditBlock {
            path: "m.rs".into(),
            search: "yok".into(),
            replace: "".into(),
        };
        assert_eq!(apply_block(FILE, &missing), Err(ApplyError::NotFound));
        let twice = EditBlock {
            path: "m.rs".into(),
            search: "x".into(),
            replace: "y".into(),
        };
        assert!(matches!(
            apply_block(FILE, &twice),
            Err(ApplyError::Ambiguous(_))
        ));
        let create = EditBlock {
            path: "n.rs".into(),
            search: "".into(),
            replace: "fn n() {}".into(),
        };
        assert_eq!(apply_block("", &create).unwrap(), "fn n() {}\n");
    }
}
