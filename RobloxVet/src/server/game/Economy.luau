--!strict
-- Economy.luau
-- ============
-- Ucret, XP, seviye ve itibar hesaplari. SAF FONKSIYONLAR: girdi profil
-- ve veri tablolari, cikti sayi. Yan etkisi yok.
--
-- Neden saf?
-- ----------
-- Studio'ya erisimimiz yok, dengeyi oynayarak ayarlayamiyoruz. Butun
-- hesap saf fonksiyonlarda oldugu icin tools/simulate_economy.py ayni
-- formulleri Python'da yeniden kurup ilerleme egrisini BASSIZ
-- dogrulayabiliyor. Hesap modullere dagilmis olsaydi bu mumkun olmazdi.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local economy = require(Data.economy)

local Economy = {}

Economy.MAX_LEVEL = #economy.levels

--- Bir seviyeye ulasmak icin gereken toplam XP.
function Economy.xpForLevel(level: number): number
	local clamped = math.clamp(level, 1, Economy.MAX_LEVEL)
	return economy.levels[clamped].xp
end

--- Verilen XP hangi seviyeye denk gelir?
function Economy.levelForXp(xp: number): number
	local level = 1
	for _, row in economy.levels do
		if xp >= row.xp then
			level = row.level
		else
			break
		end
	end
	return level
end

function Economy.titleKeyForLevel(level: number): string
	local clamped = math.clamp(level, 1, Economy.MAX_LEVEL)
	return economy.levels[clamped].titleKey
end

--- Sonraki seviyeye ilerleme (0..1). Son seviyede 1.
function Economy.levelProgress(xp: number): number
	local level = Economy.levelForXp(xp)
	if level >= Economy.MAX_LEVEL then
		return 1
	end
	local current = Economy.xpForLevel(level)
	local upper = Economy.xpForLevel(level + 1)
	if upper <= current then
		return 1
	end
	return math.clamp((xp - current) / (upper - current), 0, 1)
end

--- Itibarin ucrete etkisi: reputationFeeFloor ile reputationFeeCeil
--- arasinda dogrusal.
function Economy.reputationMultiplier(reputation: number): number
	local span = economy.reputation.max - economy.reputation.min
	local ratio = if span <= 0 then 0 else (reputation - economy.reputation.min) / span
	ratio = math.clamp(ratio, 0, 1)
	local floor = economy.fee.reputationFeeFloor
	local ceil = economy.fee.reputationFeeCeil
	return floor + (ceil - floor) * ratio
end

export type FeeInput = {
	baseFee: number,
	feeMultiplier: number,
	reputation: number,
	health: number,
	feeBonus: number,
}

--- Taburcu ucreti. Hayvanin sagligi ucretin healthShare kadarlik
--- kismini belirliyor: yarim iyilestirilmis hayvan tam para etmiyor.
function Economy.fee(input: FeeInput): number
	local healthRatio = math.clamp(input.health / 100, 0, 1)
	local healthFactor = (1 - economy.fee.healthShare) + economy.fee.healthShare * healthRatio
	local raw = input.baseFee
		* input.feeMultiplier
		* Economy.reputationMultiplier(input.reputation)
		* input.feeBonus
		* healthFactor
	return math.max(economy.fee.minimumFee, math.floor(raw + 0.5))
end

function Economy.clampReputation(value: number): number
	return math.clamp(value, economy.reputation.min, economy.reputation.max)
end

--- Hasta gelme araligi. En iyi itibar ve klinik yukseltmeleri kisaltir.
function Economy.arrivalSeconds(bestReputation: number, arrivalScale: number): number
	local span = economy.reputation.max - economy.reputation.min
	local ratio = if span <= 0 then 0 else (bestReputation - economy.reputation.min) / span
	ratio = math.clamp(ratio, 0, 1)
	local seconds = economy.arrival.baseSeconds * (1 - economy.arrival.reputationSpeedup * ratio) * arrivalScale
	return math.max(economy.arrival.minSeconds, seconds)
end

--- Ham veri tablosuna erisim (SelfTest ve digerleri icin tek nokta).
function Economy.table(): any
	return economy
end

return Economy
