--!strict
-- Staff.luau
-- ==========
-- Ise alinan personel: satin alma, etkiler ve GUNLUK MAAS.
--
-- Neden var?
-- ----------
-- Ekonomi simulasyonu (tools/simulate_economy.py) sunu gosteriyordu:
-- oyuncu 15. seviyeyi ~176.000 TL ile bitiriyor ve harcayacak yer
-- bulamiyor. Yukseltmeler tek seferlik; alinanlar bitince para yigiliyor.
-- Personel hem o yigina bir yer aciyor hem de SUREKLI BIR GIDER
-- (gunluk maas) koyuyor - yani para bir kez degil, her gun anlam
-- kazaniyor.
--
-- Maasi odeyemezsen ne olur?
-- --------------------------
-- Personel ISTEN AYRILIYOR ve bildirim gidiyor. Sessizce bedava
-- calismaya devam etmek, maasi anlamsiz kilardi; oyuncuyu eksi bakiyeye
-- dusurmek ise oyunu kilitlerdi.
--
-- Etkiler nasil birlesiyor?
-- -------------------------
-- Upgrades.luau ile AYNI kalip: `additive` toplaniyor, `multiplicative`
-- carpiliyor; `clinic` kapsamlilar cok oyunculuda ONLINE oyuncularin en
-- iyisini aliyor. Kume `data/staff.json` -> effectKeys ile ayni olmak
-- zorunda ve verify_data.py ikisini karsilastiriyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Economy = require(script.Parent.Economy)
local Profile = require(script.Parent.Profile)

local staffData = require(Data.staff)

local Staff = {}

-- Etki kumeleri. data/staff.json -> effectKeys ile AYNI olmak zorunda.
local ADDITIVE = { queueSlots = true, stressRelief = true }
local MULTIPLICATIVE = { minigameScale = true, penaltyScale = true }
local CLINIC_WIDE = { queueSlots = true, stressRelief = true }

local byId: { [string]: any } = {}
for _, entry in staffData.staff do
	byId[entry.id] = entry
end

function Staff.get(id: string): any?
	return byId[id]
end

function Staff.all(): { any }
	return staffData.staff
end

function Staff.known(id: string): boolean
	return byId[id] ~= nil
end

--- Devriye rotasi (StaffFactory kullaniyor).
function Staff.patrol(): { { number } }
	return staffData.patrol
end

--- Bir etki anahtari taniniyor mu? (verify_data ve SelfTest icin.)
function Staff.knownEffect(key: string): boolean
	return ADDITIVE[key] == true or MULTIPLICATIVE[key] == true
end

local function blank(): { [string]: number }
	local effects = {}
	for key in ADDITIVE do
		effects[key] = 0
	end
	for key in MULTIPLICATIVE do
		effects[key] = 1
	end
	return effects
end

--- Bir oyuncunun personelinden gelen KISISEL etkiler.
function Staff.effectsOf(data: Profile.Data): { [string]: number }
	local effects = blank()
	for id, hired in data.staff do
		if hired ~= true then
			continue
		end
		local entry = byId[id]
		if entry == nil then
			continue
		end
		for key, value in entry.effect do
			if ADDITIVE[key] then
				effects[key] += value
			elseif MULTIPLICATIVE[key] then
				effects[key] *= value
			end
		end
	end
	return effects
end

--- Binaya isleyen etkiler: online oyunculardan EN IYISI.
function Staff.clinicEffects(players: { Player }): { [string]: number }
	local best = blank()
	for _, player in players do
		local entry = Profile.get(player)
		if entry == nil then
			continue
		end
		local effects = Staff.effectsOf(entry.data)
		for key in CLINIC_WIDE do
			best[key] = math.max(best[key], effects[key])
		end
	end
	return best
end

export type HireResult = { ok: boolean, reasonKey: string, args: { any } }

--- Personel ise alir. Fiyat, seviye ve kasa rezervi denetimi burada;
--- magazadaki "kilitli" gorunum yalnizca kolaylik.
function Staff.hire(data: Profile.Data, id: string): HireResult
	local entry = byId[id]
	if entry == nil then
		return { ok = false, reasonKey = "shop.unknown", args = {} }
	end
	if data.staff[id] == true then
		return { ok = false, reasonKey = "staff.alreadyHired", args = {} }
	end
	if data.level < entry.unlockLevel then
		return { ok = false, reasonKey = "shop.levelTooLow", args = { entry.unlockLevel } }
	end
	if data.money < entry.hireCost then
		return { ok = false, reasonKey = "shop.notEnoughMoney", args = {} }
	end

	-- Kasa rezervi: butun parasini ise alima yatiran oyuncu sarf
	-- malzemesi alamaz ve oyun kilitlenir (yukseltmelerdeki kuralin
	-- aynisi). Ustune ILK GUNUN MAASI da ayriliyor: ertesi gun hemen
	-- isten cikarilan bir personel, para harcatip hicbir sey vermezdi.
	local economy = Economy.table()
	local reserve = economy.minimumReserve + entry.dailyWage
	if data.money - entry.hireCost < reserve then
		return { ok = false, reasonKey = "staff.keepReserve", args = { reserve } }
	end

	data.money -= entry.hireCost
	data.staff[id] = true
	return { ok = true, reasonKey = "staff.hired", args = { entry.nameKey } }
end

--- Isten cikarir. Maas odenemedigi icin de cagriliyor.
function Staff.dismiss(data: Profile.Data, id: string): boolean
	if data.staff[id] ~= true then
		return false
	end
	data.staff[id] = nil
	return true
end

--- Gunluk toplam maas.
function Staff.dailyWage(data: Profile.Data): number
	local total = 0
	for id, hired in data.staff do
		local entry = byId[id]
		if hired == true and entry ~= nil then
			total += entry.dailyWage
		end
	end
	return total
end

export type Payroll = { paid: number, quit: { string } }

--- Gun sonu bordrosu. Parasi yetmeyenin personeli isten ayriliyor;
--- once EN PAHALI olan gidiyor, cunku ucuz olanlari tutmak oyuncunun
--- elinde daha cok sey birakiyor.
function Staff.payWages(data: Profile.Data): Payroll
	local quit: { string } = {}
	local order: { any } = {}
	for id, hired in data.staff do
		local entry = byId[id]
		if hired == true and entry ~= nil then
			table.insert(order, entry)
		end
	end
	table.sort(order, function(a, b)
		return a.dailyWage > b.dailyWage
	end)

	local paid = 0
	for _, entry in order do
		if data.money >= entry.dailyWage then
			data.money -= entry.dailyWage
			paid += entry.dailyWage
		else
			data.staff[entry.id] = nil
			table.insert(quit, entry.id)
		end
	end
	return { paid = paid, quit = quit }
end

--- Magaza listesi (istemciye yollanan bicim).
function Staff.shopSnapshot(data: Profile.Data): any
	local list = {}
	for _, entry in staffData.staff do
		table.insert(list, {
			id = entry.id,
			nameKey = entry.nameKey,
			descKey = entry.descKey,
			price = entry.hireCost,
			wage = entry.dailyWage,
			unlockLevel = entry.unlockLevel,
			owned = data.staff[entry.id] == true,
			locked = data.level < entry.unlockLevel,
		})
	end
	return list
end

return Staff
