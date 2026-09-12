--!strict
-- Achievements.luau
-- =================
-- Basarimlar. SUNUCU OTORITER: sayaclar profilden okunuyor, oduller
-- sunucuda veriliyor.
--
-- Sayaclar tek yerden okunuyor
-- ----------------------------
-- Her basarimin bir `kind` alani var ve o ad, asagidaki COUNTERS
-- tablosundaki bir okuyucuya karsilik geliyor. Basarim veri dosyasina
-- yeni bir `kind` yazip buraya okuyucusunu eklemeyi unutmak, basarimin
-- sessizce hic acilmamasi demekti — verify_data.py iki tarafi
-- karsilastiriyor, bu yuzden unutulamiyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Profile = require(script.Parent.Profile)

local achievementData = require(Data.achievements)

local Achievements = {}

-- kind -> profilden o sayaci okuyan fonksiyon.
local COUNTERS: { [string]: (Profile.Data) -> number } = {
	patients = function(data)
		return data.stats.patients
	end,
	correct = function(data)
		return data.stats.correct
	end,
	earned = function(data)
		return data.stats.earned
	end,
	level = function(data)
		return data.level
	end,
	surgeries = function(data)
		return data.stats.surgeries
	end,
	perfectSurgeries = function(data)
		return data.stats.perfectSurgeries
	end,
	emergencies = function(data)
		return data.stats.emergencies
	end,
	reputation = function(data)
		return data.reputation
	end,
	tasks = function(data)
		return data.stats.tasks
	end,
	streak = function(data)
		return data.stats.bestStreak
	end,
	species = function(data)
		local total = 0
		for _, treated in data.species do
			if treated == true then
				total += 1
			end
		end
		return total
	end,
}

local byId: { [string]: any } = {}
for _, achievement in achievementData.achievements do
	byId[achievement.id] = achievement
end

--- Bu sayac adi taniniyor mu? verify_data.py ve SelfTest icin.
function Achievements.knownCounter(kind: string): boolean
	return COUNTERS[kind] ~= nil
end

function Achievements.all(): { any }
	return achievementData.achievements
end

function Achievements.get(id: string): any?
	return byId[id]
end

--- Bir sayacin su anki degeri.
function Achievements.value(data: Profile.Data, kind: string): number
	local reader = COUNTERS[kind]
	if reader == nil then
		return 0
	end
	return reader(data)
end

export type Unlocked = { id: string, nameKey: string, money: number, xp: number }

--- Acilmamis basarimlari denetler, acilanlarin odulunu VERIR ve listesini
--- doner. Odulu burada vermek onemli: cagiran taraf unutursa basarim
--- acilip odulsuz kalirdi.
function Achievements.check(data: Profile.Data): { Unlocked }
	local unlocked: { Unlocked } = {}

	for _, achievement in achievementData.achievements do
		if data.achievements[achievement.id] == true then
			continue
		end
		if Achievements.value(data, achievement.kind) < achievement.target then
			continue
		end

		data.achievements[achievement.id] = true
		data.money += achievement.reward.money
		data.xp += achievement.reward.xp
		table.insert(unlocked, {
			id = achievement.id,
			nameKey = achievement.nameKey,
			money = achievement.reward.money,
			xp = achievement.reward.xp,
		})
	end

	return unlocked
end

--- Istemciye yollanan goruntu: her basarimin ilerlemesi ve durumu.
function Achievements.snapshot(data: Profile.Data): any
	local list = {}
	local unlockedCount = 0

	for _, achievement in achievementData.achievements do
		local owned = data.achievements[achievement.id] == true
		if owned then
			unlockedCount += 1
		end
		local value = math.min(Achievements.value(data, achievement.kind), achievement.target)
		table.insert(list, {
			id = achievement.id,
			nameKey = achievement.nameKey,
			descKey = achievement.descKey,
			order = achievement.order,
			target = achievement.target,
			value = value,
			unlocked = owned,
			money = achievement.reward.money,
			xp = achievement.reward.xp,
		})
	end

	table.sort(list, function(a, b)
		return a.order < b.order
	end)
	return { list = list, unlocked = unlockedCount, total = #achievementData.achievements }
end

return Achievements
