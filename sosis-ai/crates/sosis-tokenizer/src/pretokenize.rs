//! Kod odaklı ön-bölümleme (pre-tokenization).
//!
//! BPE birleştirmeleri yalnızca bir parçanın (chunk) *içinde* yapılır. Parçalar:
//! - kelimeler / tanımlayıcılar (`foo_bar1`), önlerinde tek bir boşlukla (` foo`)
//! - en fazla 3 haneli sayı grupları (`123`, `4`)
//! - en fazla 4 karakterlik noktalama dizileri (`::`, `->`, `{}`), önünde tek boşluk olabilir
//! - boşluk dizileri; satır sonu + girinti tek parça olur (`\n    `), böylece model
//!   girintiyi tek token olarak öğrenebilir.

const MAX_DIGITS: usize = 3;
const MAX_PUNCT: usize = 4;

fn is_word(c: char) -> bool {
    c.is_alphanumeric() || c == '_'
}

fn is_punct(c: char) -> bool {
    !c.is_whitespace() && !is_word(c)
}

/// `text` metnini parçalara böler. Parçalar birleştirildiğinde orijinal metni verir.
pub fn split(text: &str) -> Vec<&str> {
    let chars: Vec<(usize, char)> = text.char_indices().collect();
    let n = chars.len();
    let pos = |k: usize| if k < n { chars[k].0 } else { text.len() };

    // `j` konumundan başlayan boşluk olmayan bir token'ın bittiği indeks.
    let scan = |j: usize| -> usize {
        let c = chars[j].1;
        let mut k = j + 1;
        if c.is_ascii_digit() {
            while k < n && k - j < MAX_DIGITS && chars[k].1.is_ascii_digit() {
                k += 1;
            }
        } else if is_word(c) {
            while k < n
                && is_word(chars[k].1)
                && !(chars[k].1.is_ascii_digit() && !is_word_start(c))
            {
                k += 1;
            }
        } else {
            while k < n && k - j < MAX_PUNCT && is_punct(chars[k].1) {
                k += 1;
            }
        }
        k
    };

    let mut out = Vec::new();
    let mut i = 0;
    while i < n {
        if chars[i].1.is_whitespace() {
            let mut j = i;
            while j < n && chars[j].1.is_whitespace() {
                j += 1;
            }
            if j < n && chars[j - 1].1 == ' ' {
                // Son boşluk bir sonraki token'a yapışır (" foo").
                if j - 1 > i {
                    out.push(&text[pos(i)..pos(j - 1)]);
                }
                let k = scan(j);
                out.push(&text[pos(j - 1)..pos(k)]);
                i = k;
            } else {
                out.push(&text[pos(i)..pos(j)]);
                i = j;
            }
        } else {
            let k = scan(i);
            out.push(&text[pos(i)..pos(k)]);
            i = k;
        }
    }
    out
}

/// Harf veya `_` ile başlayan tanımlayıcılar içindeki rakamlar tanımlayıcıya dahildir
/// (`x1`, `u16`); sayı ile başlayan kelimeler ise sayı kuralına girer.
fn is_word_start(c: char) -> bool {
    c.is_alphabetic() || c == '_'
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn roundtrip_and_shapes() {
        let src = "fn main() {\n    let x1 = 12345;\n    println!(\"hi\");\n}\n";
        let parts = split(src);
        assert_eq!(parts.concat(), src);
        assert!(parts.contains(&"fn"));
        assert!(parts.contains(&" main"));
        assert!(parts.contains(&"\n   "));
        assert!(parts.contains(&" let"));
        assert!(parts.contains(&" x1"));
        assert!(parts.contains(&" 123"));
        assert!(parts.contains(&"45"));
    }

    #[test]
    fn unicode_safe() {
        let src = "// Türkçe yorum: ğüşıöç → çalışır\nlet ş = 1;";
        assert_eq!(split(src).concat(), src);
    }

    #[test]
    fn empty() {
        assert!(split("").is_empty());
    }
}
