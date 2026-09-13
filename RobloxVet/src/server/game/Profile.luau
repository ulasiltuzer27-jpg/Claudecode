--!strict
-- Profile.luau
-- ============
-- Oyuncu ilerlemesinin kalici kaydi. SUNUCU OTORITER.
--
-- Tek yazici oturum kilidi
-- ------------------------
-- Ayni profil iki sunucuda ayni anda acilirsa biri digerinin yazdigini
-- ezer ve oyuncu ilerlemesini kaybeder. Bunu onlemek icin her kayitta
-- bir kilit tutuluyor: kilidi tutan is (JobId) ve son dokunma zamani.
-- Baska bir sunucu kilidi ancak BAYATLAMISSA (sunucu cokmus, kilit
-- birakilmamis) devralir.
--
-- Studio'da DataStore kapaliysa
-- -----------------------------
-- "Studio Access to API Services" kapaliyken DataStore cagrilari hata
-- verir. Bu durumda profil BELLEKTE tutuluyor ve `transient` isaretiyle
-- donuyor; istemci HUD'da Turkce bir uyari gosteriyor. Sessizce veri
-- kaybetmek yerine oyuncuya durumu soylemek gerekiyor.

local DataStoreService = game:GetService("DataStoreService")
local Players = game:GetService("Players")
local RunService = game:GetService("RunService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local economy = require(Data.economy)
local settingsData = require(Data.settings)
local toolData = require(Data.tools)

local Profile = {}

local SCHEMA_VERSION = 2
local STORE_NAME = "VetProfile_v1"
-- STORE_NAME degismiyor: sema surumu 2'ye ciksa da ESKI KAYITLAR OKUNMAYA
-- devam etsin. migrate() eksik alanlari varsayilanla dolduruyor; store'u
-- degistirmek herkesin ilerlemesini silmek demek olurdu.
local KEY_PREFIX = "player_"
local LOCK_STALE_SECONDS = 1800
local LOCK_REFRESH_SECONDS = 60
local AUTOSAVE_SECONDS = 90
local MAX_ATTEMPTS = 4

export type Stats = {
	patients: number,
	correct: number,
	wrong: number,
	earned: number,
	tips: number,
	surgeries: number,
	perfectSurgeries: number,
	emergencies: number,
	streak: number,
	bestStreak: number,
	tasks: number,
	lost: number,
}

export type Settings = {
	bobbing: number,
	fieldOfView: number,
	showFps: boolean,
	sprintEnabled: boolean,
	masterVolume: number,
	musicVolume: number,
	sfxVolume: number,
}

export type DailyTask = { id: string, progress: number, done: boolean }

export type Daily = {
	day: number,
	tasks: { DailyTask },
	counters: { [string]: number },
	-- O gun bakilan turler. Sayac olarak tutulamaz: "4 farkli tur"
	-- gorevi icin AYNI turu iki kez saymamak gerekiyor.
	species: { [string]: boolean },
}

export type Data = {
	version: number,
	money: number,
	xp: number,
	level: number,
	reputation: number,
	tools: { [string]: boolean },
	upgrades: { [string]: boolean },
	stats: Stats,
	species: { [string]: boolean },
	achievements: { [string]: boolean },
	daily: Daily,
	settings: Settings,
}

export type Entry = {
	data: Data,
	transient: boolean,
	lastSave: number,
	dirty: boolean,
}

local store: DataStore? = nil
local loaded: { [Player]: Entry } = {}

--- Baslangicta sahip olunan aletler VERIDEN cikariliyor: fiyati 0 ve
--- 1. seviyede acilan her alet bastan var. Kodda liste tutmak, veri
--- dosyasi degisince sessizce tutarsiz kalirdi.
local function starterTools(): { [string]: boolean }
	local tools = {}
	for _, tool in toolData.tools do
		if tool.price == 0 and tool.unlockLevel <= 1 then
			tools[tool.id] = true
		end
	end
	return tools
end

local function defaultStats(): Stats
	return {
		patients = 0,
		correct = 0,
		wrong = 0,
		earned = 0,
		tips = 0,
		surgeries = 0,
		perfectSurgeries = 0,
		emergencies = 0,
		streak = 0,
		bestStreak = 0,
		tasks = 0,
		lost = 0,
	}
end

--- Ayar varsayilanlari VERIDEN geliyor (data/settings.json); kodda
--- ikinci bir liste tutmak, veri dosyasi degisince sessizce tutarsiz
--- kalirdi.
local function defaultSettings(): Settings
	local defaults = settingsData.defaults
	return {
		bobbing = defaults.bobbing,
		fieldOfView = defaults.fieldOfView,
		showFps = defaults.showFps,
		sprintEnabled = defaults.sprintEnabled,
		masterVolume = defaults.masterVolume,
		musicVolume = defaults.musicVolume,
		sfxVolume = defaults.sfxVolume,
	}
end

local function defaultData(): Data
	return {
		version = SCHEMA_VERSION,
		money = economy.startingMoney,
		xp = 0,
		level = 1,
		reputation = economy.startingReputation,
		tools = starterTools(),
		upgrades = {},
		stats = defaultStats(),
		species = {},
		achievements = {},
		daily = { day = 0, tasks = {}, counters = {}, species = {} },
		settings = defaultSettings(),
	}
end

--- Diskten gelen tabloyu guncel semaya tasir. Eksik alanlar
--- varsayilanla dolduruluyor; bilinmeyen surum reddedilmiyor, cunku
--- oyuncunun ilerlemesini silmektense eksigini tamamlamak dogru.
local function migrate(raw: any): Data
	local fresh = defaultData()
	if type(raw) ~= "table" then
		return fresh
	end

	local result = fresh
	result.money = if type(raw.money) == "number" then raw.money else fresh.money
	result.xp = if type(raw.xp) == "number" then raw.xp else fresh.xp
	result.level = if type(raw.level) == "number" then raw.level else fresh.level
	result.reputation = if type(raw.reputation) == "number" then raw.reputation else fresh.reputation

	if type(raw.tools) == "table" then
		for id, owned in raw.tools do
			if owned == true then
				result.tools[id] = true
			end
		end
	end
	if type(raw.upgrades) == "table" then
		for id, owned in raw.upgrades do
			if owned == true then
				result.upgrades[id] = true
			end
		end
	end
	if type(raw.stats) == "table" then
		for key, value in raw.stats do
			if type(value) == "number" and result.stats[key] ~= nil then
				result.stats[key] = value
			end
		end
	end
	if type(raw.species) == "table" then
		for id, treated in raw.species do
			if treated == true then
				result.species[id] = true
			end
		end
	end
	if type(raw.achievements) == "table" then
		for id, unlocked in raw.achievements do
			if unlocked == true then
				result.achievements[id] = true
			end
		end
	end
	if type(raw.daily) == "table" then
		result.daily = {
			day = if type(raw.daily.day) == "number" then raw.daily.day else 0,
			tasks = {},
			counters = {},
			species = {},
		}
		if type(raw.daily.species) == "table" then
			for id, treated in raw.daily.species do
				if treated == true then
					result.daily.species[id] = true
				end
			end
		end
		if type(raw.daily.tasks) == "table" then
			for _, task in raw.daily.tasks do
				if type(task) == "table" and type(task.id) == "string" then
					table.insert(result.daily.tasks, {
						id = task.id,
						progress = if type(task.progress) == "number" then task.progress else 0,
						done = task.done == true,
					})
				end
			end
		end
		if type(raw.daily.counters) == "table" then
			for key, value in raw.daily.counters do
				if type(key) == "string" and type(value) == "number" then
					result.daily.counters[key] = value
				end
			end
		end
	end
	-- Ayarlarda TIP denetimi onemli: bozuk bir kayit istemcide kamerayi
	-- kitleyebilir. Yanlis tipli alan sessizce varsayilana donuyor.
	if type(raw.settings) == "table" then
		-- Eski kayitlardaki `firstPerson` alani artik yok (oyun yalnizca
		-- birinci sahis); sessizce yok sayiliyor, profil bozulmuyor.
		if type(raw.settings.showFps) == "boolean" then
			result.settings.showFps = raw.settings.showFps
		end
		if type(raw.settings.sprintEnabled) == "boolean" then
			result.settings.sprintEnabled = raw.settings.sprintEnabled
		end
		if type(raw.settings.bobbing) == "number" then
			result.settings.bobbing = math.clamp(
				raw.settings.bobbing,
				settingsData.limits.bobbing.min,
				settingsData.limits.bobbing.max
			)
		end
		if type(raw.settings.fieldOfView) == "number" then
			result.settings.fieldOfView = math.clamp(
				raw.settings.fieldOfView,
				settingsData.limits.fieldOfView.min,
				settingsData.limits.fieldOfView.max
			)
		end
		for _, key in { "masterVolume", "musicVolume", "sfxVolume" } do
			if type(raw.settings[key]) == "number" then
				result.settings[key] = math.clamp(
					raw.settings[key],
					settingsData.limits[key].min,
					settingsData.limits[key].max
				)
			end
		end
	end

	result.version = SCHEMA_VERSION
	return result
end

local function keyOf(player: Player): string
	return KEY_PREFIX .. tostring(player.UserId)
end

local function getStore(): DataStore?
	if store ~= nil then
		return store
	end
	local ok, result = pcall(function()
		return DataStoreService:GetDataStore(STORE_NAME)
	end)
	if ok then
		store = result
		return store
	end
	return nil
end

--- UpdateAsync'i birkac kez dener. Roblox DataStore'lari gecici olarak
--- hata verebiliyor; ilk hatada pes etmek oyuncunun kaydini dusurur.
local function tryUpdate(key: string, transform: (any) -> any?): (boolean, any)
	local dataStore = getStore()
	if dataStore == nil then
		return false, nil
	end

	local lastError: any = nil
	for attempt = 1, MAX_ATTEMPTS do
		local ok, result = pcall(function()
			return dataStore:UpdateAsync(key, transform)
		end)
		if ok then
			return true, result
		end
		lastError = result
		task.wait(2 ^ (attempt - 1))
	end
	warn(("[Profile] '%s' yazilamadi: %s"):format(key, tostring(lastError)))
	return false, nil
end

--- Profili yukler ve oturum kilidini alir.
function Profile.load(player: Player): Entry
	local existing = loaded[player]
	if existing ~= nil then
		return existing
	end

	local jobId = if RunService:IsStudio() then "studio" else game.JobId
	local now = os.time()

	local ok, stored = tryUpdate(keyOf(player), function(raw: any): any?
		if type(raw) == "table" and type(raw.lock) == "table" then
			local lock = raw.lock
			local fresh = (now - (tonumber(lock.at) or 0)) < LOCK_STALE_SECONDS
			if fresh and lock.jobId ~= jobId then
				-- Baska bir sunucu bu profili acik tutuyor; yazmayi iptal et.
				return nil
			end
		end
		local payload = if type(raw) == "table" then raw else {}
		payload.lock = { jobId = jobId, at = now }
		return payload
	end)

	local entry: Entry
	if ok and type(stored) == "table" then
		entry = {
			data = migrate(stored.data),
			transient = false,
			lastSave = os.clock(),
			dirty = false,
		}
	else
		-- Kilit alinamadi ya da DataStore kapali: bellekte devam.
		entry = {
			data = defaultData(),
			transient = true,
			lastSave = os.clock(),
			dirty = false,
		}
		warn(("[Profile] %s icin kalici kayit acilamadi - bellekte devam ediliyor"):format(player.Name))
	end

	loaded[player] = entry
	return entry
end

function Profile.get(player: Player): Entry?
	return loaded[player]
end

--- Bir sonraki otomatik kayitta yazilmak uzere isaretler.
function Profile.markDirty(player: Player)
	local entry = loaded[player]
	if entry ~= nil then
		entry.dirty = true
	end
end

--- Diske yazar. `release` true ise oturum kilidini de birakir.
function Profile.save(player: Player, release: boolean): boolean
	local entry = loaded[player]
	if entry == nil or entry.transient then
		return false
	end

	local jobId = if RunService:IsStudio() then "studio" else game.JobId
	local snapshot = entry.data

	local ok = tryUpdate(keyOf(player), function(raw: any): any?
		local payload = if type(raw) == "table" then raw else {}
		local lock = payload.lock
		if type(lock) == "table" and lock.jobId ~= jobId then
			local fresh = (os.time() - (tonumber(lock.at) or 0)) < LOCK_STALE_SECONDS
			if fresh then
				-- Kilidi baskasi devralmis: uzerine yazma.
				return nil
			end
		end
		payload.data = snapshot
		payload.lock = if release then nil else { jobId = jobId, at = os.time() }
		return payload
	end)

	if ok then
		entry.lastSave = os.clock()
		entry.dirty = false
	end
	return ok
end

function Profile.release(player: Player)
	if loaded[player] ~= nil then
		Profile.save(player, true)
		loaded[player] = nil
	end
end

--- Otomatik kayit ve kilit tazeleme dongusu. init.server.luau baslatiyor.
function Profile.startAutosave()
	task.spawn(function()
		while true do
			task.wait(LOCK_REFRESH_SECONDS)
			for player, entry in loaded do
				if player.Parent == nil then
					continue
				end
				local age = os.clock() - entry.lastSave
				if entry.dirty or age >= AUTOSAVE_SECONDS then
					Profile.save(player, false)
				end
			end
		end
	end)

	-- Sunucu kapanirken herkesin kaydini yaz. BindToClose'un butcesi
	-- kisitli (30 sn), bu yuzden paralel calistiriliyor.
	game:BindToClose(function()
		local threads = 0
		for player in loaded do
			threads += 1
			task.spawn(function()
				Profile.release(player)
				threads -= 1
			end)
		end
		local deadline = os.clock() + 20
		while threads > 0 and os.clock() < deadline do
			task.wait(0.2)
		end
	end)

	Players.PlayerRemoving:Connect(function(player: Player)
		Profile.release(player)
	end)
end

--- Varsayilan profil (SelfTest ve simulasyon icin disa aciliyor).
function Profile.defaultData(): Data
	return defaultData()
end

--- Varsayilan ayarlar.
function Profile.defaultSettings(): Settings
	return defaultSettings()
end

return Profile
