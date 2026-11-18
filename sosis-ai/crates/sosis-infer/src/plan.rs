//! Planlama ("önce plan, sonra kod"): model önce numaralı bir plan yazar; kullanıcı planı onaylar,
//! düzenler ya da reddeder; kod onaylanan plana göre yazılır. İstem kalıpları eğitim verisiyle
//! (`sft-data` planlama örnekleri) aynıdır.

use sosis_tokenizer::chat::{plan_approved, plan_request, plan_steps};

use crate::chat::{reply, ChatSession};
use crate::rag::CodeIndex;
use crate::{Generator, SamplingParams};

/// Plan ister; plan metnini döndürür (konuşma oturumda kalır).
pub fn make_plan(
    generator: &Generator,
    session: &mut ChatSession,
    task: &str,
    index: Option<&CodeIndex>,
    params: &SamplingParams,
    on_text: &mut dyn FnMut(&str),
) -> String {
    reply(
        generator,
        session,
        &plan_request(task),
        index,
        params,
        on_text,
    )
}

/// Onaylanan (gerekirse kullanıcının düzenlediği) planla kodu yazdırır.
pub fn execute_plan(
    generator: &Generator,
    session: &mut ChatSession,
    plan: &str,
    index: Option<&CodeIndex>,
    params: &SamplingParams,
    on_text: &mut dyn FnMut(&str),
) -> String {
    reply(
        generator,
        session,
        &plan_approved(plan),
        index,
        params,
        on_text,
    )
}

/// Plandaki adım sayısı (arayüzde göstermek için).
pub fn step_count(plan: &str) -> usize {
    plan_steps(plan).len()
}
