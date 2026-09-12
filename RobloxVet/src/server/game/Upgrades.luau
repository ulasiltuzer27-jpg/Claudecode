--!strict
-- Upgrades.luau
-- =============
-- Alet ve klinik yukseltmesi satin alma + etkilerin toplanmasi.
-- SUNUCU OTORITER: fiyat, seviye ve "zaten var mi" denetimleri burada.
--
-- Kisisel ve klinik etkileri
-- --------------------------
-- Bazi etkiler oyuncunun kendisine isliyor (ucret bonusu, sarf
-- indirimi, tedavi hizi); bazilari BINAYA isliyor ve tek bir kuyruk
-- paylasildigi icin cok oyunculu oturumda ONLINE OYUNCULARIN EN
-- IYISINI aliyor (bekleme yeri, reklam). Ikisini karistirmak, bir
-- oyuncunun aldigi yukseltmeyi digerinin kaybetmesi demek olurdu.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Economy = require(script.Parent.Economy)
local Profile = require(script.Parent.Profile)

local toolData = require(Data.tools)
local upgradeData = require(Data.upgrades)

local Upgrades = {}

-- Etkiler nasil birlestirilecek? Veri dosyasindaki effectKeys ile
-- burasi ayni kumeyi tanimlamak zorunda; verify_data.py karsilastiriyor.
local ADDITIVE = { queueSlots = true, surgeryTolerance = true, washBonus = true }
local MULTIPLICATIVE = { arrivalScale = true, treatSpeed = true, supplyDiscount = true, feeBonus = true, repGain = true }

-- Binaya isleyen etkiler: cok oyunculuda en iyi deger kazanir.
local CLINIC_WIDE = { queueSlots = true, arrivalScale = true }

local toolById: { [string]: any } = {}
for _, tool in toolData.tools do
	toolById[tool.id] = tool
end

local upgradeById: { [string]: any } = {}
for _, upgrade in upgradeData.upgrades do
	upgradeById[upgrade.id] = upgrade
end

function Upgrades.tool(id: string): any?
	return toolById[id]
end

function Upgrades.upgrade(id: string): any?
	return upgradeById[id]
end

function Upgrades.allTools(): { any }
	return toolData.tools
end

function Upgrades.allUpgrades(): { any }
	return upgradeData.upgrades
end

--- Etki anahtari taniniyor mu? verify_data.py ve SelfTest icin.
function Upgrades.knownEffect(key: string): boolean
	return ADDITIVE[key] == true or MULTIPLICATIVE[key] == true
end

--- Tek oyuncunun KISISEL etkileri.
function Upgrades.effectsOf(data: Profile.Data): { [string]: number }
	local effects: { [string]: number } = {
		queueSlots = 0,
		surgeryTolerance = 0,
		washBonus = 0,
		arrivalScale = 1,
		treatSpeed = 1,
		supplyDiscount = 1,
		feeBonus = 1,
		repGain = 1,
	}

	for id, owned in data.upgrades do
		if owned ~= true then
			continue
		end
		local upgrade = upgradeById[id]
		if upgrade == nil then
			-- Veri dosyasindan silinmis bir yukseltme: sessizce yutmuyoruz.
			warn(("[Upgrades] profilde bilinmeyen yukseltme: '%s'"):format(id))
			continue
		end
		for key, value in upgrade.effect do
			if ADDITIVE[key] then
				effects[key] += value
			elseif MULTIPLICATIVE[key] then
				effects[key] *= value
			else
				warn(("[Upgrades] '%s' bilinmeyen etki anahtari kullaniyor: '%s'"):format(id, key))
			end
		end
	end

	return effects
end

--- Klinige isleyen etkiler: online oyunculardan EN IYISI.
--- queueSlots'ta en buyuk, arrivalScale'de en kucuk iyi olan.
function Upgrades.clinicEffects(players: { Player }): { [string]: number }
	local best = { queueSlots = 0, arrivalScale = 1 }
	for _, player in players do
		local entry = Profile.get(player)
		if entry == nil then
			continue
		end
		local effects = Upgrades.effectsOf(entry.data)
		for key in CLINIC_WIDE do
			if key == "arrivalScale" then
				best[key] = math.min(best[key], effects[key])
			else
				best[key] = math.max(best[key], effects[key])
			end
		end
	end
	return best
end

-- `args` bildirimdeki {1}, {2} ... yer tutucularini doldurur; ceviriyi
-- istemci yapiyor, sunucu yalnizca anahtar ve sayilari yolluyor.
export type BuyResult = { ok: boolean, reasonKey: string, args: { any } }

local function purchase(data: Profile.Data, bag: { [string]: boolean }, item: any?, id: string): BuyResult
	if item == nil then
		return { ok = false, reasonKey = "shop.unknown", args = {} }
	end
	if bag[id] == true then
		return { ok = false, reasonKey = "shop.alreadyOwned", args = {} }
	end
	if data.level < item.unlockLevel then
		return { ok = false, reasonKey = "shop.levelTooLow", args = { item.unlockLevel } }
	end
	if data.money < item.cost then
		return { ok = false, reasonKey = "shop.notEnoughMoney", args = {} }
	end

	-- Kasa rezervi: butun parasini alete yatiran oyuncu sarf malzemesi
	-- alamaz, tedavi edemez, para kazanamaz - oyun kilitlenir. Bu
	-- kilitlenmeyi tools/simulate_economy.py yakaladi; kural o yuzden var.
	local economy = Economy.table()
	if data.money - item.cost < economy.minimumReserve then
		return { ok = false, reasonKey = "shop.keepReserve", args = { economy.minimumReserve } }
	end

	data.money -= item.cost
	bag[id] = true
	return { ok = true, reasonKey = "shop.bought", args = {} }
end

--- Alet satin alir. Aletin `price` alani var, `cost` degil; ortak
--- satin alma yoluna sokmak icin normalize ediliyor.
function Upgrades.buyTool(data: Profile.Data, id: string): BuyResult
	local tool = toolById[id]
	if tool == nil then
		return { ok = false, reasonKey = "shop.unknown", args = {} }
	end
	return purchase(data, data.tools, { cost = tool.price, unlockLevel = tool.unlockLevel }, id)
end

function Upgrades.buyUpgrade(data: Profile.Data, id: string): BuyResult
	return purchase(data, data.upgrades, upgradeById[id], id)
end

--- Seviye atlayinca fiyati 0 olan aletler kendiliginden aciliyor:
--- oyuncuyu bedava bir seyi "satin al" diye magazaya yollamak anlamsiz.
function Upgrades.grantFreeTools(data: Profile.Data): { string }
	local granted = {}
	for _, tool in toolData.tools do
		if tool.price == 0 and data.level >= tool.unlockLevel and data.tools[tool.id] ~= true then
			data.tools[tool.id] = true
			table.insert(granted, tool.id)
		end
	end
	return granted
end

--- Oyuncu bu aleti kullanabilir mi?
function Upgrades.hasTool(data: Profile.Data, id: string): boolean
	return data.tools[id] == true and data.level >= (toolById[id] and toolById[id].unlockLevel or math.huge)
end

--- Magaza listesi (istemciye yollanan bicim).
function Upgrades.shopSnapshot(data: Profile.Data): any
	local tools = {}
	for _, tool in toolData.tools do
		table.insert(tools, {
			id = tool.id,
			nameKey = tool.nameKey,
			price = tool.price,
			unlockLevel = tool.unlockLevel,
			owned = data.tools[tool.id] == true,
			locked = data.level < tool.unlockLevel,
		})
	end

	local upgrades = {}
	for _, upgrade in upgradeData.upgrades do
		table.insert(upgrades, {
			id = upgrade.id,
			nameKey = upgrade.nameKey,
			descKey = upgrade.descKey,
			price = upgrade.cost,
			unlockLevel = upgrade.unlockLevel,
			owned = data.upgrades[upgrade.id] == true,
			locked = data.level < upgrade.unlockLevel,
		})
	end

	return { tools = tools, upgrades = upgrades, maxLevel = Economy.MAX_LEVEL }
end

return Upgrades
