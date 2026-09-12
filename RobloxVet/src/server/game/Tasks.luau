--!strict
-- Tasks.luau
-- ==========
-- Gunluk gorevler. SUNUCU OTORITER.
--
-- Gorevler nasil seciliyor?
-- -------------------------
-- Gunun numarasi ile oyuncunun UserId'sinden turetilen SABIT bir
-- tohumla. Yani: aynı gun aynı oyuncu hangi sunucuya girerse girsin
-- AYNI gorevleri goruyor. Rastgele secip profile yazmak da olurdu, ama
-- o zaman iki sunucu arasinda yaris olusur ve oyuncu gorev degistirmek
-- icin sunucu degistirebilirdi.
--
-- Sayaclar gun basinda sifirlaniyor ve profile yaziliyor; sunucu
-- degisse de ilerleme kaybolmuyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Profile = require(script.Parent.Profile)

local economy = require(Data.economy)
local taskData = require(Data.tasks)

local Tasks = {}

local byId: { [string]: any } = {}
for _, task in taskData.tasks do
	byId[task.id] = task
end

function Tasks.all(): { any }
	return taskData.tasks
end

function Tasks.get(id: string): any?
	return byId[id]
end

--- Simdi hangi "gun"deyiz? os.time() UTC saniye; resetHours'a bolunce
--- gun numarasi cikiyor.
local function currentDay(): number
	return math.floor(os.time() / (economy.dailyTasks.resetHours * 3600))
end

--- Deterministik karistirma: ayni tohum -> ayni sira. Roblox'un global
--- `math.random`'ini kirletmemek icin kendi dogrusal uretecini kullaniyor.
local function shuffledPool(seed: number): { any }
	local pool = table.clone(taskData.tasks)
	local state = (seed % 2147483647) + 1

	local function nextRandom(limit: number): number
		state = (state * 48271) % 2147483647
		return (state % limit) + 1
	end

	for index = #pool, 2, -1 do
		local swap = nextRandom(index)
		pool[index], pool[swap] = pool[swap], pool[index]
	end
	return pool
end

--- Gun degistiyse gorevleri ve sayaclari yeniler. Her etkilesimden once
--- cagrilmali; tek giris noktasi bu.
function Tasks.ensure(data: Profile.Data, userId: number): boolean
	local day = currentDay()
	if data.daily.day == day and #data.daily.tasks > 0 then
		return false
	end

	local pool = shuffledPool(day * 1000003 + userId)
	local chosen = {}
	for index = 1, math.min(economy.dailyTasks.count, #pool) do
		table.insert(chosen, { id = pool[index].id, progress = 0, done = false })
	end

	data.daily = { day = day, tasks = chosen, counters = {}, species = {} }
	return true
end

--- Gunluk sayaci artirir. `kind` gorev tablosundaki kind ile ayni.
function Tasks.bump(data: Profile.Data, kind: string, amount: number)
	data.daily.counters[kind] = (data.daily.counters[kind] or 0) + amount
end

--- Gunluk sayaci dogrudan ayarlar (streak ve species gibi "sayan degil,
--- olan" degerler icin).
function Tasks.set(data: Profile.Data, kind: string, value: number)
	data.daily.counters[kind] = math.max(data.daily.counters[kind] or 0, value)
end

--- O gun bakilan bir turu isaretler ve "species" sayacini gunceller.
--- Ayni turu iki kez saymamasi icin sayac degil KUME tutuluyor.
function Tasks.markSpecies(data: Profile.Data, animalId: string)
	if data.daily.species[animalId] == true then
		return
	end
	data.daily.species[animalId] = true

	local total = 0
	for _, treated in data.daily.species do
		if treated == true then
			total += 1
		end
	end
	data.daily.counters.species = total
end

export type Completed = { id: string, nameKey: string, money: number, xp: number }

--- Tamamlananlarin odulunu verir ve listesini doner.
function Tasks.check(data: Profile.Data): { Completed }
	local completed: { Completed } = {}

	for _, entry in data.daily.tasks do
		local task = byId[entry.id]
		if task == nil then
			-- Gorev veri dosyasindan silinmis: sessizce yutmuyoruz.
			warn(("[Tasks] profilde bilinmeyen gorev: '%s'"):format(entry.id))
			continue
		end
		entry.progress = math.min(data.daily.counters[task.kind] or 0, task.target)
		if entry.done or entry.progress < task.target then
			continue
		end

		entry.done = true
		data.money += task.reward.money
		data.xp += task.reward.xp
		data.stats.tasks += 1
		table.insert(completed, {
			id = task.id,
			nameKey = task.nameKey,
			money = task.reward.money,
			xp = task.reward.xp,
		})
	end

	return completed
end

--- Istemciye yollanan goruntu.
function Tasks.snapshot(data: Profile.Data): any
	local list = {}
	local doneCount = 0

	for _, entry in data.daily.tasks do
		local task = byId[entry.id]
		if task == nil then
			continue
		end
		if entry.done then
			doneCount += 1
		end
		table.insert(list, {
			id = task.id,
			nameKey = task.nameKey,
			target = task.target,
			value = math.min(data.daily.counters[task.kind] or 0, task.target),
			done = entry.done,
			money = task.reward.money,
			xp = task.reward.xp,
		})
	end

	-- Sonraki yenilenmeye kalan sure: arayuzde geri sayim gosteriliyor.
	local period = economy.dailyTasks.resetHours * 3600
	local remaining = period - (os.time() % period)

	return { list = list, done = doneCount, total = #list, resetsIn = remaining }
end

return Tasks
