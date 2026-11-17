//! Sohbet şablonu (talimat ince ayarı ve sohbet modu için ortak).
//!
//! ```text
//! <|system|>sistem mesajı<|user|>soru<|assistant|>cevap<|endoftext|><|user|>...<|assistant|>
//! ```
//!
//! Eğitimde yalnızca asistan cevapları ve onları bitiren `<|endoftext|>` öğrenilir
//! (`mask = true`); böylece model cevabın nerede bittiğini de öğrenir.

use serde::{Deserialize, Serialize};

use crate::{Tokenizer, ASSISTANT, EOS, SYSTEM, USER};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum Role {
    System,
    User,
    Assistant,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Message {
    pub role: Role,
    pub content: String,
}

impl Message {
    pub fn new(role: Role, content: impl Into<String>) -> Self {
        Self {
            role,
            content: content.into(),
        }
    }
}

/// Varsayılan sistem mesajı.
pub const DEFAULT_SYSTEM: &str =
    "Sen Sosy'sin: Sosis AI tarafından geliştirilen, kod yazan yardımcı bir yapay zekâsın.";

/// Mesajları token'lara çevirir; `mask[i]`, `ids[i]`'nin öğrenilecek bir hedef olup olmadığıdır.
/// `generation_prompt` ise sona `<|assistant|>` eklenir (modelin cevap yazması için).
pub fn encode_chat(
    tok: &Tokenizer,
    messages: &[Message],
    generation_prompt: bool,
) -> (Vec<u32>, Vec<bool>) {
    let mut ids = Vec::new();
    let mut mask = Vec::new();
    let push = |ids_: &[u32], learn: bool, ids: &mut Vec<u32>, mask: &mut Vec<bool>| {
        ids.extend_from_slice(ids_);
        mask.extend(std::iter::repeat_n(learn, ids_.len()));
    };
    for m in messages {
        match m.role {
            Role::System => {
                push(&[SYSTEM], false, &mut ids, &mut mask);
                push(&tok.encode(&m.content), false, &mut ids, &mut mask);
            }
            Role::User => {
                push(&[USER], false, &mut ids, &mut mask);
                push(&tok.encode(&m.content), false, &mut ids, &mut mask);
            }
            Role::Assistant => {
                push(&[ASSISTANT], false, &mut ids, &mut mask);
                push(&tok.encode(&m.content), true, &mut ids, &mut mask);
                push(&[EOS], true, &mut ids, &mut mask);
            }
        }
    }
    if generation_prompt {
        push(&[ASSISTANT], false, &mut ids, &mut mask);
    }
    (ids, mask)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::BpeTrainer;

    #[test]
    fn mask_covers_only_answers() {
        let mut t = BpeTrainer::new();
        t.feed("merhaba fn main() {}");
        let tok = t.train(300, |_, _| {});
        let msgs = vec![
            Message::new(Role::System, "sys"),
            Message::new(Role::User, "soru"),
            Message::new(Role::Assistant, "cevap"),
        ];
        let (ids, mask) = encode_chat(&tok, &msgs, false);
        assert_eq!(ids.len(), mask.len());
        assert_eq!(ids[0], SYSTEM);
        assert_eq!(*ids.last().unwrap(), EOS);
        assert!(*mask.last().unwrap());
        let a = ids.iter().position(|&i| i == ASSISTANT).unwrap();
        assert!(mask[..=a].iter().all(|m| !m));
        assert!(mask[a + 1..].iter().all(|m| *m));
        assert_eq!(tok.decode(&ids[a + 1..ids.len() - 1], false), "cevap");

        let (gen, _) = encode_chat(&tok, &msgs[..2], true);
        assert_eq!(*gen.last().unwrap(), ASSISTANT);
    }
}
