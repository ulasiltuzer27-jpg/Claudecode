//! Süreç RAM kullanımı (Windows / Linux / macOS) — eğitim ve veri hazırlamada sınır koymak için.

use sysinfo::{get_current_pid, ProcessRefreshKind, ProcessesToUpdate, System};

/// Varsayılan RAM sınırı (GB): 32 GB'lık sistemde işletim sistemine pay bırakır.
pub const DEFAULT_MAX_RAM_GB: f64 = 28.0;

/// Bu sürecin kullandığı fiziksel bellek (GB). Ölçülemezse 0.
pub fn process_ram_gb() -> f64 {
    let Ok(pid) = get_current_pid() else {
        return 0.0;
    };
    let Ok(mut sys) = System::new() else {
        return 0.0;
    };
    sys.refresh_processes_specifics(
        ProcessesToUpdate::Some(&[pid]),
        true,
        ProcessRefreshKind::nothing().with_memory(),
    );
    sys.process(pid)
        .map(|p| p.memory() as f64 / 1024f64.powi(3))
        .unwrap_or(0.0)
}

/// Belirli aralıklarla RAM'i ölçen hafif bekçi.
pub struct RamGuard {
    pub limit_gb: f64,
    calls: u64,
    every: u64,
    last_gb: f64,
}

impl RamGuard {
    pub fn new(limit_gb: f64, every: u64) -> Self {
        Self {
            limit_gb,
            calls: 0,
            every: every.max(1),
            last_gb: 0.0,
        }
    }

    /// Her `every` çağrıda bir ölçer; sınır (yüzde `frac`) aşıldıysa `true`.
    pub fn over(&mut self, frac: f64) -> bool {
        if self.calls.is_multiple_of(self.every) {
            self.last_gb = process_ram_gb();
        }
        self.calls += 1;
        self.last_gb > self.limit_gb * frac
    }

    pub fn last_gb(&self) -> f64 {
        self.last_gb
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn measures_something() {
        let gb = process_ram_gb();
        assert!(gb > 0.0 && gb < 64.0, "{gb}");
        let mut g = RamGuard::new(0.000001, 1);
        assert!(g.over(1.0));
    }
}
