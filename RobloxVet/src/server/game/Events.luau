--!strict
-- Events.luau
-- ===========
-- Vardiya olaylari: salgin, yogun gun, sakin gun, denetim.
--
-- Neden var?
-- ----------
-- Her vardiya birbirinin ayniydi. Gunduz/gece dongusu gunun SAATINI
-- degistiriyordu ama oyunun ritmini degistirmiyordu. Olaylar o
-- tekduzelige giriyor ve hepsi birbirinden FARKLI bir sey istiyor:
--   - salgin: ayni hastaliktan seri hasta, karantina baskisi,
--   - yogun gun: kuyruk dolup tasiyor, sira yonetimi,
--   - sakin gun: hasta seyrek ama ucret yuksek, aceleye gerek yok,
--   - denetim: XP zamli, dogru calismanin odullendigi vardiya.
--
-- Nerede duruyor?
-- ---------------
-- Olay SUNUCUDA: tek bir gercek var ve herkes ayni vardiyayi yasiyor.
-- Istemciye yalnizca adi, aciklamasi ve kalan suresi gidiyor. Salginin
-- HANGI hastalik oldugu istemciye GITMIYOR - gitseydi teshis listesini
-- bir anda daraltirdi; oyuncu salgini muayene ederek anliyor.
--
-- Zaman `Workspace:GetServerTimeNow()` ile olculuyor (senkron sunucu
-- saati), `os.clock()` ile degil: istemcideki geri sayimin sunucuyla
-- ayni sayiyi gostermesi ancak boyle mumkun.

local Players = game:GetService("Players")
local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Diagnosis = require(script.Parent.Diagnosis)
local Profile = require(script.Parent.Profile)

local plan = require(Data.events)

local Events = {}

export type Active = {
	id: string,
	kind: string,
	nameKey: string,
	descKey: string,
	startedAt: number,
	endsAt: number,
	-- Yalnizca salginda dolu: o vardiyanin hastaligi. SUNUCUDA kaliyor.
	conditionId: string?,
}

local active: Active? = nil
local nextEligibleAt = 0
local listeners: { (Active?) -> () } = {}
local running = false

local NEUTRAL = { arrivalScale = 1, feeMultiplier = 1, xpMultiplier = 1 }

--- Tablodaki olaylar (SelfTest ve verify_data icin).
function Events.all(): { any }
	return plan.events
end

function Events.active(): Active?
	local current = active
	if current == nil then
		return nil
	end
	if Workspace:GetServerTimeNow() >= current.endsAt then
		return nil
	end
	return current
end

--- O anki carpanlar. Olay yoksa hepsi 1.
function Events.modifiers(): { arrivalScale: number, feeMultiplier: number, xpMultiplier: number }
	local current = Events.active()
	if current == nil then
		return { arrivalScale = 1, feeMultiplier = 1, xpMultiplier = 1 }
	end
	for _, entry in plan.events do
		if entry.id == current.id then
			local effect = entry.effect
			return {
				arrivalScale = effect.arrivalScale or 1,
				feeMultiplier = effect.feeMultiplier or 1,
				xpMultiplier = effect.xpMultiplier or 1,
			}
		end
	end
	return { arrivalScale = 1, feeMultiplier = 1, xpMultiplier = 1 }
end

--- Salgin hastaligi (yoksa nil). YALNIZCA sunucu okuyor.
function Events.outbreakCondition(): string?
	local current = Events.active()
	if current == nil or current.kind ~= "outbreak" then
		return nil
	end
	return current.conditionId
end

--- Gelen hastanin salgin hastaligini tasima olasiligi.
function Events.outbreakChance(): number
	return plan.outbreakChance
end

--- Istemciye yollanan goruntu. Hastaligin KIMLIGI burada YOK.
function Events.snapshot(): any
	local current = Events.active()
	if current == nil then
		return nil
	end
	return {
		id = current.id,
		nameKey = current.nameKey,
		descKey = current.descKey,
		-- Mutlak zaman: istemci geri sayimi kendi ciziyor, saniyede bir
		-- paket yollamaya gerek kalmiyor (kogus kartiyla ayni kalip).
		endsAt = current.endsAt,
		seconds = math.max(0, current.endsAt - Workspace:GetServerTimeNow()),
	}
end

function Events.onChanged(listener: (Active?) -> ())
	table.insert(listeners, listener)
end

local function announce(current: Active?)
	for _, listener in listeners do
		listener(current)
	end
end

--- Klinikteki en yuksek seviye: olaylar ancak minLevel'den sonra
--- cikiyor. (Upgrades'teki CLINIC_WIDE kuralinin aynisi.)
local function highestLevel(): number
	local best = 1
	for _, player in Players:GetPlayers() do
		local entry = Profile.get(player)
		if entry ~= nil then
			best = math.max(best, entry.data.level)
		end
	end
	return best
end

--- Agirliga gore olay secimi.
local function pickEvent(): any?
	local total = 0
	for _, entry in plan.events do
		total += entry.weight
	end
	if total <= 0 then
		return nil
	end
	local roll = math.random() * total
	for _, entry in plan.events do
		roll -= entry.weight
		if roll <= 0 then
			return entry
		end
	end
	return plan.events[#plan.events]
end

--- Salgin icin bulasici bir hastalik secer. Bulasici hastalik yoksa
--- salgin BASLAMIYOR: "salgin" deyip bulasmayan bir hastalik secmek,
--- olayin adiyla isini birbirinden ayirirdi.
local function pickOutbreakCondition(level: number): string?
	local pool = {}
	for _, condition in Diagnosis.allConditions() do
		if condition.contagious == true and condition.unlockLevel <= level then
			table.insert(pool, condition.id)
		end
	end
	if #pool == 0 then
		return nil
	end
	return pool[math.random(1, #pool)]
end

local function begin(): boolean
	local level = highestLevel()
	if level < plan.minLevel then
		return false
	end
	local entry = pickEvent()
	if entry == nil then
		return false
	end

	local conditionId: string? = nil
	if entry.kind == "outbreak" then
		conditionId = pickOutbreakCondition(level)
		if conditionId == nil then
			return false
		end
	end

	local now = Workspace:GetServerTimeNow()
	active = {
		id = entry.id,
		kind = entry.kind,
		nameKey = entry.nameKey,
		descKey = entry.descKey,
		startedAt = now,
		endsAt = now + entry.seconds,
		conditionId = conditionId,
	}
	nextEligibleAt = now + entry.seconds + plan.minGapSeconds
	announce(active)
	return true
end

--- Olay dongusu. Dis dunyaya tek dokunusu `onChanged` dinleyicileri.
function Events.start()
	if running then
		return
	end
	running = true
	nextEligibleAt = Workspace:GetServerTimeNow() + plan.minGapSeconds

	task.spawn(function()
		local wasActive = false
		while true do
			task.wait(plan.checkSeconds)
			local current = Events.active()
			if current == nil then
				-- Olay yeni bitmisse bir kez daha haber veriliyor ki
				-- HUD serit kapansin.
				if wasActive then
					wasActive = false
					active = nil
					announce(nil)
				end
				if #Players:GetPlayers() > 0
					and Workspace:GetServerTimeNow() >= nextEligibleAt
					and math.random() < plan.chance
				then
					wasActive = begin()
				end
			else
				wasActive = true
			end
		end
	end)
end

--- SelfTest icin: olay tablosunun kendisi tutarli mi?
function Events.kinds(): { string }
	local seen: { [string]: boolean } = {}
	local kinds = {}
	for _, entry in plan.events do
		if not seen[entry.kind] then
			seen[entry.kind] = true
			table.insert(kinds, entry.kind)
		end
	end
	table.sort(kinds)
	return kinds
end

function Events.minLevel(): number
	return plan.minLevel
end

return Events
