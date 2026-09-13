--!strict
-- ClinicLevel.luau
-- ================
-- Ortak klinik seviyesi.
--
-- Neden var?
-- ----------
-- Cok oyunculuda herkes kendi XP'sini topluyordu; birlikte oynamanin
-- ORTAK BIR HEDEFI yoktu. Klinik seviyesi o boslugu dolduruyor:
-- taburcu edilen her hasta klinige de puan kazandiriyor ve acilan her
-- sey odadaki HERKESE aciliyor.
--
-- Nerede saklaniyor?
-- ------------------
-- Klinik XP'si oyuncunun PROFILINDE (`clinicXp`). Sunucunun belleginde
-- tutulsaydi sunucu kapaninca ilerleme silinirdi. Aktif seviye ise
-- online oyuncularin EN YUKSEGI - yukseltmelerdeki CLINIC_WIDE
-- kuralinin aynisi. Boylece klinik tek bir kisinin sirtinda kalmiyor
-- ama bir kisi girip cikinca da sifirlanmiyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Profile = require(script.Parent.Profile)

local plan = require(Data.clinicLevels)

local ClinicLevel = {}

ClinicLevel.MAX = #plan.levels

--- Taburcu edilen hastanin XP'sinin ne kadari klinige yaziliyor.
function ClinicLevel.xpShare(): number
	return plan.xpShare
end

--- Bu XP hangi seviyeye denk geliyor?
function ClinicLevel.levelForXp(xp: number): number
	local level = 1
	for _, row in plan.levels do
		if xp >= row.xp then
			level = row.level
		end
	end
	return level
end

function ClinicLevel.xpForLevel(level: number): number
	for _, row in plan.levels do
		if row.level == level then
			return row.xp
		end
	end
	return 0
end

function ClinicLevel.nameKey(level: number): string
	for _, row in plan.levels do
		if row.level == level then
			return row.nameKey
		end
	end
	return plan.levels[1].nameKey
end

--- Seviyeye kadar BIRIKEN etkiler. Kademeli: 4. seviyedeki klinik 2.,
--- 3. ve 4. seviyenin etkilerini birlikte tasiyor.
function ClinicLevel.effectsAt(level: number): { queueSlots: number, feeBonus: number, arrivalScale: number }
	local effects = { queueSlots = 0, feeBonus = 1, arrivalScale = 1 }
	for _, row in plan.levels do
		if row.level > level then
			continue
		end
		local effect = row.effect
		if effect.queueSlots ~= nil then
			effects.queueSlots += effect.queueSlots
		end
		if effect.feeBonus ~= nil then
			effects.feeBonus *= effect.feeBonus
		end
		if effect.arrivalScale ~= nil then
			effects.arrivalScale *= effect.arrivalScale
		end
	end
	return effects
end

--- Online oyuncularin en yuksek klinik XP'si.
function ClinicLevel.bestXp(players: { Player }): number
	local best = 0
	for _, player in players do
		local entry = Profile.get(player)
		if entry ~= nil then
			best = math.max(best, entry.data.clinicXp)
		end
	end
	return best
end

function ClinicLevel.activeLevel(players: { Player }): number
	return ClinicLevel.levelForXp(ClinicLevel.bestXp(players))
end

--- O anki etkiler.
function ClinicLevel.effects(players: { Player }): { queueSlots: number, feeBonus: number, arrivalScale: number }
	return ClinicLevel.effectsAt(ClinicLevel.activeLevel(players))
end

--- Istemciye yollanan goruntu: HUD'daki klinik cubugu.
function ClinicLevel.snapshot(players: { Player }): any
	local xp = ClinicLevel.bestXp(players)
	local level = ClinicLevel.levelForXp(xp)
	local current = ClinicLevel.xpForLevel(level)
	local nextXp = if level < ClinicLevel.MAX then ClinicLevel.xpForLevel(level + 1) else current
	local span = math.max(1, nextXp - current)
	return {
		level = level,
		maxLevel = ClinicLevel.MAX,
		nameKey = ClinicLevel.nameKey(level),
		xp = xp,
		progress = if level >= ClinicLevel.MAX then 1 else math.clamp((xp - current) / span, 0, 1),
	}
end

return ClinicLevel
