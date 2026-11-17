//! Sohbet hafızası: konuşma geçmişi, bağlam penceresine sığdırma ve oturum kaydı.
//!
//! Geçmiş bağlama sığmazsa **en eski** soru-cevap turları düşürülür; sistem mesajı ve son soru
//! her zaman korunur. Oturumlar `sessions/<id>.json` dosyalarına kaydedilir.

use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use anyhow::{bail, Context, Result};
use serde::{Deserialize, Serialize};
use sosis_tokenizer::chat::{encode_chat, Message, Role, DEFAULT_SYSTEM};

use crate::rag::{format_for_chat, CodeIndex};
use crate::{Generator, SamplingParams};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ChatSession {
    pub id: String,
    pub title: String,
    pub system: String,
    pub messages: Vec<Message>,
    pub created: u64,
    pub updated: u64,
}

fn now() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0)
}

/// Oturum kimliği: yalnızca harf, rakam, `-` ve `_` (dosya yolu güvenliği).
pub fn valid_id(id: &str) -> bool {
    !id.is_empty()
        && id.len() <= 64
        && id
            .chars()
            .all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
}

impl ChatSession {
    pub fn new(system: Option<&str>) -> Self {
        let t = now();
        static N: std::sync::atomic::AtomicU32 = std::sync::atomic::AtomicU32::new(0);
        let n = N.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
        Self {
            id: format!("{t}-{n}"),
            title: "Yeni sohbet".into(),
            system: system.unwrap_or(DEFAULT_SYSTEM).into(),
            messages: vec![],
            created: t,
            updated: t,
        }
    }

    fn path(dir: &Path, id: &str) -> Result<PathBuf> {
        if !valid_id(id) {
            bail!("geçersiz oturum kimliği: {id}");
        }
        Ok(dir.join(format!("{id}.json")))
    }

    pub fn save(&self, dir: &Path) -> Result<()> {
        std::fs::create_dir_all(dir)?;
        std::fs::write(
            Self::path(dir, &self.id)?,
            serde_json::to_string_pretty(self)?,
        )?;
        Ok(())
    }

    pub fn load(dir: &Path, id: &str) -> Result<Self> {
        let p = Self::path(dir, id)?;
        let text =
            std::fs::read_to_string(&p).with_context(|| format!("oturum bulunamadı: {id}"))?;
        Ok(serde_json::from_str(&text)?)
    }

    pub fn delete(dir: &Path, id: &str) -> Result<()> {
        std::fs::remove_file(Self::path(dir, id)?)?;
        Ok(())
    }

    /// Kayıtlı oturumlar, en yenisi önce.
    pub fn list(dir: &Path) -> Vec<ChatSession> {
        let mut out: Vec<ChatSession> = std::fs::read_dir(dir)
            .into_iter()
            .flatten()
            .filter_map(|e| e.ok())
            .filter(|e| e.path().extension().is_some_and(|x| x == "json"))
            .filter_map(|e| serde_json::from_str(&std::fs::read_to_string(e.path()).ok()?).ok())
            .collect();
        out.sort_by_key(|s| std::cmp::Reverse(s.updated));
        out
    }

    /// Bağlama sığacak mesaj listesi: sistem (+ proje bağlamı) + sığan en yeni turlar.
    pub fn fit(
        &self,
        generator: &Generator,
        extra_context: Option<&str>,
        budget: usize,
    ) -> Vec<Message> {
        let system = match extra_context {
            Some(c) => format!("{}\n\n{c}", self.system),
            None => self.system.clone(),
        };
        let sys = Message::new(Role::System, system);
        let len = |msgs: &[Message]| encode_chat(&generator.tokenizer, msgs, true).0.len();
        let mut start = 0;
        loop {
            let mut msgs = vec![sys.clone()];
            msgs.extend_from_slice(&self.messages[start..]);
            if len(&msgs) <= budget || start + 1 >= self.messages.len() {
                return msgs;
            }
            // En eski turu düşür (kullanıcı + asistan).
            start += 1;
            while start < self.messages.len() && self.messages[start].role != Role::User {
                start += 1;
            }
        }
    }
}

/// Sohbette bir tur: soruyu ekler, cevabı üretip (akışlı) geçmişe yazar.
pub fn reply(
    generator: &Generator,
    session: &mut ChatSession,
    user: &str,
    index: Option<&CodeIndex>,
    params: &SamplingParams,
    on_text: &mut dyn FnMut(&str),
) -> String {
    session.messages.push(Message::new(Role::User, user));
    if session.title == "Yeni sohbet" {
        session.title = user.lines().next().unwrap_or("").chars().take(48).collect();
    }
    let ctx = generator.context_len();
    let budget = ctx.saturating_sub(params.max_new_tokens.min(ctx / 2));
    // Proje bağlamı en fazla bütçenin yarısı.
    let extra = index.map(|idx| {
        let chunks = idx.retrieve(user, &generator.tokenizer, budget / 2);
        format_for_chat(&chunks)
    });
    let msgs = session.fit(generator, extra.as_deref(), budget);
    let (mut ids, _) = encode_chat(&generator.tokenizer, &msgs, true);
    if ids.len() > budget {
        // Tek soru bile sığmıyorsa sondan kırp.
        ids.drain(..ids.len() - budget);
    }
    let answer = generator.stream_until(&ids, params, on_text, &mut |_| None);
    let answer = answer.trim().to_string();
    session
        .messages
        .push(Message::new(Role::Assistant, answer.clone()));
    session.updated = now();
    answer
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ids_are_path_safe() {
        assert!(valid_id("1700000000-3"));
        assert!(!valid_id("../etc/passwd"));
        assert!(!valid_id(""));
        assert!(!valid_id("a/b"));
    }

    #[test]
    fn save_load_list_delete() {
        let dir = std::env::temp_dir().join(format!("sosis_chat_{}", std::process::id()));
        let mut s = ChatSession::new(None);
        s.messages.push(Message::new(Role::User, "selam"));
        s.save(&dir).unwrap();
        let l = ChatSession::load(&dir, &s.id).unwrap();
        assert_eq!(l.messages, s.messages);
        assert_eq!(ChatSession::list(&dir).len(), 1);
        ChatSession::delete(&dir, &s.id).unwrap();
        assert!(ChatSession::list(&dir).is_empty());
        assert!(ChatSession::load(&dir, "../x").is_err());
        std::fs::remove_dir_all(&dir).ok();
    }
}
