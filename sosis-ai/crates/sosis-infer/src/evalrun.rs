//! Bir üreticiyle derle-çalıştır değerlendirmesi (`sosis bench` ve eğitim sırasında otomatik bench).

use std::time::Duration;

use anyhow::Result;
use sosis_bench::{evaluate, function_end, Report, Suite, TaskResult};

use crate::{Generator, SamplingParams};

pub fn bench_completion(
    g: &Generator,
    suites: &[Suite],
    samples: usize,
    temperature: f32,
    max_tokens: usize,
    timeout: Duration,
    on_result: impl FnMut(&TaskResult),
) -> Result<Report> {
    evaluate(
        suites,
        samples,
        timeout,
        |suite, task, sample| {
            let params = SamplingParams {
                temperature,
                max_new_tokens: max_tokens,
                seed: 1000 + sample as u64,
                ..Default::default()
            };
            g.complete_until(&task.prompt, Some(&suite.file), &params, &mut |out| {
                function_end(suite.lang, &task.prompt, out)
            })
        },
        on_result,
    )
}
