--!strict
-- PatientFlow.luau
-- ================
-- Hastanin butun yasam dongusu: gelis, kuyruk, masaya alinma, teshis,
-- tedavi, taburcu. SUNUCU OTORITER - istemci yalnizca niyet bildirir.
--
-- Hastaligin kimligi sunucuda kalir
-- ---------------------------------
-- `patient.conditionId` istemciye ANCAK dogru teshisten sonra gidiyor.
-- Oncesinde istemci yalnizca BULUNMUS semptomlari goruyor.
--
-- Kuyruk paylasilir, odul taburcu edene
-- -------------------------------------
-- Klinik tek; kuyruk butun oyuncularin ortak kuyrugu. Bir hastayi
-- masaya alan oyuncu onun "sahibi" olur ve odulu o alir. Boylece cok
-- oyunculu oturumda iki veteriner ayni hastayi cekistirmez.

local Players = game:GetService("Players")
local TweenService = game:GetService("TweenService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Audio = require(Shared.Audio)
local Net = require(Shared.Net)

local AnimalAnimator = require(script.Parent.Parent.world.AnimalAnimator)
local AnimalFactory = require(script.Parent.Parent.world.AnimalFactory)
local Clinic = require(script.Parent.Parent.world.Clinic)
local DayCycle = require(script.Parent.Parent.world.DayCycle)
local Effects = require(script.Parent.Parent.world.Effects)
local OwnerFactory = require(script.Parent.Parent.world.OwnerFactory)
local SkyLighting = require(script.Parent.Parent.world.Lighting)
local Achievements = require(script.Parent.Achievements)
local ClinicLevel = require(script.Parent.ClinicLevel)
local Diagnosis = require(script.Parent.Diagnosis)
local Economy = require(script.Parent.Economy)
local Leaderboard = require(script.Parent.Leaderboard)
local Profile = require(script.Parent.Profile)
local Staff = require(script.Parent.Staff)
local Surgery = require(script.Parent.Surgery)
local Tasks = require(script.Parent.Tasks)
local ToolGame = require(script.Parent.ToolGame)
local Treatment = require(script.Parent.Treatment)
local Ward = require(script.Parent.Ward)
local Upgrades = require(script.Parent.Upgrades)

local economy = require(Data.economy)

local PatientFlow = {}

-- Oyuncunun bir istasyonda islem yapabilmesi icin gereken yakinlik.
-- Sunucuda denetleniyor: istemcinin "yanindayim" demesine guvenilmiyor.
local STATION_RADIUS = 16
local VITALS_TICK = 0.25

export type Patient = {
	id: number,
	animalId: string,
	conditionId: string,
	rig: AnimalFactory.Rig,
	health: number,
	stress: number,
	revealed: { [string]: boolean },
	toolsUsed: { [string]: boolean },
	diagnosed: boolean,
	treatmentsDone: { [string]: boolean },
	vet: Player?,
	owner: OwnerFactory.Owner?,
	emergency: boolean,
	satisfaction: number,
	deadline: number,
	stationId: string?,
	state: string,
	arrivedAt: number,
	busyUntil: number,
	queueSlot: number?,
	-- Laboratuvar: alinmis ama HENUZ INCELENMEMIS numuneler ve
	-- incelenmis olanlar. Numunenin kendisi bir nesne degil; hastanin
	-- dosyasinda duran bir kayit - oyuncu numuneyi elinde tasimiyor,
	-- laboratuvara gidip hastanin numunesini inceliyor.
	samples: { [string]: boolean },
	analyzed: { [string]: boolean },
	-- Numunesi laboratuvarda incelenen hasta taburcu edilirken kucuk
	-- bir ucret zammi aliyor: yuruyusun karsiligi.
	labBonus: boolean,
}

local clinic: Clinic.Built? = nil
local patientsFolder: Folder? = nil

local patients: { [number]: Patient } = {}
local activeOf: { [Player]: number } = {}
local occupiedStation: { [string]: number } = {}
local usedQueueSlots: { [number]: boolean } = {}
-- Teshisi konmus, bulasici ve HALA karantinaya alinmamis bir hasta var
-- mi? Yalnizca teshisten sonra true oluyor; teshis oncesi bir uyari,
-- hastalik listesini daraltan bir bilgi sizdirirdi.
local contagionAlert = false

local nextPatientId = 1
local started = false

-- ── Yardimcilar ───────────────────────────────────────────────────────

local function notify(player: Player, kind: string, messageKey: string, args: { any }?)
	Net.toPlayer("Notify", player, kind, messageKey, args or {})
end

local function notifyAll(kind: string, messageKey: string, args: { any }?)
	for _, player in Players:GetPlayers() do
		notify(player, kind, messageKey, args)
	end
end

local function bestReputation(): number
	local best = economy.reputation.min
	for _, player in Players:GetPlayers() do
		local entry = Profile.get(player)
		if entry ~= nil then
			best = math.max(best, entry.data.reputation)
		end
	end
	return best
end

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

local function queueSlotCount(): number
	local players = Players:GetPlayers()
	local clinicEffects = Upgrades.clinicEffects(players)
	-- Resepsiyonist de bekleme yeri aciyor: yukseltmeyle ayni kanal.
	local staffEffects = Staff.clinicEffects(players)
	-- Ortak klinik seviyesi de ayni kanaldan yer aciyor: uc kaynak da
	-- TOPLANIYOR, cunku ucu de "daha cok bekleme yeri" anlamina geliyor.
	local levelEffects = ClinicLevel.effects(players)
	local total = economy.arrival.queueSlots
		+ clinicEffects.queueSlots
		+ staffEffects.queueSlots
		+ levelEffects.queueSlots
	local available = if clinic ~= nil then #clinic.queuePoints else 0
	return math.min(total, available)
end

local function waitingCount(): number
	local total = 0
	for _, patient in patients do
		if patient.state == "waiting" or patient.state == "arriving" then
			total += 1
		end
	end
	return total
end

local function freeQueueSlot(): number?
	local limit = queueSlotCount()
	for slot = 1, limit do
		if usedQueueSlots[slot] ~= true then
			return slot
		end
	end
	return nil
end

--- Oyuncu bu istasyona yeterince yakin mi? Karakteri yoksa hayir.
local function nearStation(player: Player, stationId: string): boolean
	if clinic == nil then
		return false
	end
	local station = clinic.stations[stationId]
	if station == nil then
		return false
	end
	local character = player.Character
	if character == nil then
		return false
	end
	local root = character:FindFirstChild("HumanoidRootPart")
	if root == nil or not root:IsA("BasePart") then
		return false
	end
	local flat = Vector3.new(root.Position.X, 0, root.Position.Z)
	local target = Vector3.new(station.cframe.Position.X, 0, station.cframe.Position.Z)
	return (flat - target).Magnitude <= STATION_RADIUS
end

--- Oyuncu bu ADLI NOKTAYA yakin mi? Istasyondan farki: burada hasta
--- degil OYUNCU duruyor (laboratuvar tezgahi).
local function nearSpot(player: Player, name: string): boolean
	if clinic == nil then
		return false
	end
	local spot = clinic.spots[name]
	if spot == nil then
		return false
	end
	local character = player.Character
	if character == nil then
		return false
	end
	local root = character:FindFirstChild("HumanoidRootPart")
	if root == nil or not root:IsA("BasePart") then
		return false
	end
	local flat = Vector3.new(root.Position.X, 0, root.Position.Z)
	local target = Vector3.new(spot.Position.X, 0, spot.Position.Z)
	return (flat - target).Magnitude <= STATION_RADIUS
end

--- Bu istasyon bir karantina bolmesi mi? Bolmeler `station:quarantine1`
--- diye etiketli; ad ONEKTEN okunuyor ki yeni bir bolme eklemek kod
--- degisikligi gerektirmesin.
local function isQuarantineStation(stationId: string?): boolean
	return stationId ~= nil and string.sub(stationId, 1, 10) == "quarantine"
end

--- Oyuncuya en yakin istasyon. Hicbiri menzilde degilse nil.
local function nearestStation(player: Player): string?
	if clinic == nil then
		return nil
	end
	local best: string? = nil
	local bestDistance = math.huge
	local character = player.Character
	if character == nil then
		return nil
	end
	local root = character:FindFirstChild("HumanoidRootPart")
	if root == nil or not root:IsA("BasePart") then
		return nil
	end

	for id, station in clinic.stations do
		local flat = Vector3.new(root.Position.X, 0, root.Position.Z)
		local target = Vector3.new(station.cframe.Position.X, 0, station.cframe.Position.Z)
		local distance = (flat - target).Magnitude
		if distance <= STATION_RADIUS and distance < bestDistance then
			best = id
			bestDistance = distance
		end
	end
	return best
end

local function patientOf(player: Player): Patient?
	local id = activeOf[player]
	if id == nil then
		return nil
	end
	return patients[id]
end

-- ── Istemciye yollanan goruntuler ─────────────────────────────────────

local function chartSnapshot(patient: Patient?, viewer: Player): any
	if patient == nil then
		return { present = false }
	end

	local revealed = {}
	for symptomId in patient.revealed do
		local symptom = Diagnosis.symptom(symptomId)
		table.insert(revealed, {
			id = symptomId,
			nameKey = if symptom ~= nil then symptom.nameKey else symptomId,
			severity = if symptom ~= nil then symptom.severity else 1,
		})
	end

	local animal = AnimalFactory.spec(patient.animalId)
	local entry = Profile.get(viewer)
	local level = if entry ~= nil then entry.data.level else 1

	-- Teshis adaylari: bu turun, bu seviyede gorulebilecek hastaliklari.
	--
	-- Her adayin yaninda ESLESME ORANI da gidiyor: bulunan belirtilerin
	-- kaci bu hastalikta da var. Bunu istemciye HESAPLATMAK, hastalik
	-- tablosunun tamamini istemciye yollamak demekti; burada hesaplanan
	-- oran ise zaten oyuncunun elindeki bilgiden (bulunmus belirtiler)
	-- cikiyor, yani yeni bir sey sizdirmiyor. Hangi adayin dogru oldugu
	-- hala yalnizca sunucuda.
	local revealedCount = 0
	for _ in patient.revealed do
		revealedCount += 1
	end

	local candidates = {}
	for _, condition in Diagnosis.conditionsFor(patient.animalId, level) do
		local hits = 0
		for _, symptomId in condition.symptoms do
			if patient.revealed[symptomId] == true then
				hits += 1
			end
		end
		table.insert(candidates, {
			id = condition.id,
			nameKey = condition.nameKey,
			hits = hits,
			-- Bulunan belirtilerin kaci bu hastaligi destekliyor.
			match = if revealedCount == 0 then 0 else hits / revealedCount,
			-- Bu hastaligin belirtilerinin kaci bulundu (kapsam).
			coverage = if #condition.symptoms == 0 then 0 else hits / #condition.symptoms,
		})
	end
	-- Once eslesme, esitlikte kapsam, yine esitlikte ad: sira her
	-- guncellemede ayni kalsin diye kararli bir olcut zinciri.
	table.sort(candidates, function(a, b)
		if a.match ~= b.match then
			return a.match > b.match
		end
		if a.coverage ~= b.coverage then
			return a.coverage > b.coverage
		end
		return a.id < b.id
	end)

	local required = {}
	local done = {}
	if patient.diagnosed then
		for _, treatmentId in Treatment.requiredFor(patient.conditionId) do
			local treatment = Treatment.get(treatmentId)
			table.insert(required, {
				id = treatmentId,
				nameKey = if treatment ~= nil then treatment.nameKey else treatmentId,
				kind = if treatment ~= nil then treatment.kind else "",
				applied = patient.treatmentsDone[treatmentId] == true,
			})
		end
	end
	for treatmentId in patient.treatmentsDone do
		table.insert(done, treatmentId)
	end

	local samples = {}
	local analyzedCount = 0
	for toolId in patient.samples do
		if patient.analyzed[toolId] == true then
			analyzedCount += 1
		else
			local tool = Upgrades.tool(toolId)
			table.insert(samples, {
				id = toolId,
				nameKey = if tool ~= nil then tool.nameKey else toolId,
			})
		end
	end
	table.sort(samples, function(a, b)
		return a.id < b.id
	end)

	local condition = if patient.diagnosed then Diagnosis.condition(patient.conditionId) else nil
	-- Sahibin sikayeti HER ZAMAN gidiyor: NPC zaten yuksek sesle
	-- soyluyor ve bilerek teshisi ele vermeyen bir cumle. Hastaligin
	-- KIMLIGI yine yalnizca dogru teshisten sonra gidiyor
	-- (conditionNameKey), bu yuzden ayri okunuyor.
	local always = Diagnosis.condition(patient.conditionId)

	return {
		present = true,
		id = patient.id,
		animalId = patient.animalId,
		nameKey = if animal ~= nil then animal.nameKey else patient.animalId,
		health = math.floor(patient.health + 0.5),
		stress = math.floor(patient.stress + 0.5),
		revealed = revealed,
		candidates = candidates,
		diagnosed = patient.diagnosed,
		-- Hastaligin adi ANCAK dogru teshisten sonra gidiyor.
		conditionNameKey = if condition ~= nil then condition.nameKey else nil,
		required = required,
		done = done,
		stationId = patient.stationId,
		-- Bekleyen numuneler: alindi, HENUZ incelenmedi. Numunenin
		-- SONUCU burada yok - yalnizca "laboratuvara git" bilgisi.
		samples = samples,
		analyzed = analyzedCount,
		-- Hastanin karantinada olup olmadigi istemcinin zaten gordugu
		-- bir sey (hayvan gozunun onunde bolmede duruyor); kart bunu
		-- yazili da soyluyor ki "neden yayilma durmadi" sorusu kalmasin.
		quarantined = isQuarantineStation(patient.stationId),
		-- Bulasicilik ANCAK dogru teshisten sonra gidiyor: once
		-- gitseydi aday listesini 35'ten 6'ya indirirdi.
		contagious = patient.diagnosed and condition ~= nil and condition.contagious == true,
		complete = patient.diagnosed and Treatment.isComplete(patient.treatmentsDone, patient.conditionId),
		dischargeHealth = economy.patient.dischargeHealth,
		emergency = patient.emergency,
		-- Kalan sure istemcide geri sayim olarak gosteriliyor. Acil
		-- olmayan hastada sonsuz; istemci bunu gizliyor.
		secondsLeft = if patient.emergency then math.max(0, patient.deadline - os.clock()) else -1,
		satisfaction = math.floor(patient.satisfaction + 0.5),
		hintKey = if always ~= nil then always.hintKey else nil,
		-- Rutin islemde teshis paneli hic acilmiyor; istemci bunu
		-- buradan biliyor.
		routine = Diagnosis.isRoutine(patient.conditionId),
	}
end

local function pushChart(player: Player)
	Net.toPlayer("ChartUpdate", player, chartSnapshot(patientOf(player), player))
end

local function stateSnapshot(player: Player): any
	local entry = Profile.get(player)
	if entry == nil then
		return { ready = false }
	end
	local data = entry.data

	local tools = {}
	for _, tool in Upgrades.allTools() do
		if data.tools[tool.id] == true and data.level >= tool.unlockLevel then
			table.insert(tools, { id = tool.id, nameKey = tool.nameKey, order = tool.order, useSeconds = tool.useSeconds })
		end
	end
	table.sort(tools, function(a, b)
		return a.order < b.order
	end)

	-- Magaza goruntusu: aletler/yukseltmeler/dekor Upgrades'ten,
	-- personel Staff'tan. Upgrades'in personeli tanimasi gerekmiyor.
	local shopSnapshot = Upgrades.shopSnapshot(data)
	shopSnapshot.staff = Staff.shopSnapshot(data)

	local effects = Upgrades.effectsOf(data)
	local treatments = {}
	for _, treatment in Treatment.all() do
		if data.level >= treatment.unlockLevel then
			table.insert(treatments, {
				id = treatment.id,
				nameKey = treatment.nameKey,
				kind = treatment.kind,
				order = treatment.order,
				cost = Treatment.cost(treatment.id, effects.supplyDiscount),
			})
		end
	end
	table.sort(treatments, function(a, b)
		return a.order < b.order
	end)

	return {
		ready = true,
		money = data.money,
		xp = data.xp,
		level = data.level,
		maxLevel = Economy.MAX_LEVEL,
		titleKey = Economy.titleKeyForLevel(data.level),
		progress = Economy.levelProgress(data.xp),
		reputation = data.reputation,
		transient = entry.transient,
		stats = data.stats,
		tools = tools,
		treatments = treatments,
		queueWaiting = waitingCount(),
		queueSlots = queueSlotCount(),
		shop = shopSnapshot,
		-- Ayarlar profilde saklaniyor; istemci acilista bunlari alip
		-- kamerayi ona gore kuruyor.
		settings = data.settings,
		-- Gunduz/gece: saat, nobet rozeti ve ucret carpani. Sunucu
		-- saati tek kaynak; istemci yalnizca ciziyor.
		shift = DayCycle.snapshot(),
		-- Ortak klinik seviyesi: online oyuncularin EN YUKSEK klinik
		-- XP'sinden cikiyor, yani herkeste AYNI deger gorunuyor. Kendi
		-- seviyesi (`level`) kisisel, bu ise binanin.
		clinic = ClinicLevel.snapshot(Players:GetPlayers()),
		-- Bulasma uyarisi: teshisi konmus bulasici bir hasta hala
		-- karantina disinda. Hangi hasta oldugunu SOYLEMIYOR.
		contagion = contagionAlert,
	}
end

local function pushState(player: Player)
	local entry = Profile.get(player)
	if entry ~= nil then
		-- Tab listesi HUD ile ayni anda tazelensin.
		Leaderboard.update(player, entry.data)
	end
	Net.toPlayer("StateUpdate", player, stateSnapshot(player))
end

--- Odullerin XP'si seviye atlatabilir; seviye atlama tek yerde olsun
--- diye ayri fonksiyon. Bedava aletleri de burada aciyor.
local function applyLevel(player: Player, data: any, previousLevel: number)
	data.level = Economy.levelForXp(data.xp)
	if data.level <= previousLevel then
		return
	end
	local granted = Upgrades.grantFreeTools(data)
	notify(player, "good", "notify.levelUp", { data.level, Economy.titleKeyForLevel(data.level) })
	for _, toolId in granted do
		local tool = Upgrades.tool(toolId)
		notify(player, "good", "notify.toolUnlocked", { if tool ~= nil then tool.nameKey else toolId })
	end
end

--- Kisisel XP'nin bir payi ORTAK klinige de yaziliyor. Tek yerde
--- olsun diye ayri fonksiyon: pay orani veriden (`clinicLevels.xpShare`)
--- geliyor, kodda sabit degil.
---
--- Neden profilde? Klinik XP'si sunucunun belleginde tutulsaydi sunucu
--- kapaninca ilerleme silinirdi. Profilde tutulup aktif seviye online
--- oyuncularin EN YUKSEGINDEN okununca hem kalici oluyor hem de ortak.
local function awardClinicXp(data: any, xpGain: number)
	if xpGain <= 0 then
		return
	end
	data.clinicXp += math.floor(xpGain * ClinicLevel.xpShare() + 0.5)
end

local function pushProgress(player: Player)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	Tasks.ensure(entry.data, player.UserId)
	Net.toPlayer("ProgressUpdate", player, {
		stats = entry.data.stats,
		achievements = Achievements.snapshot(entry.data),
		tasks = Tasks.snapshot(entry.data),
	})
end

--- Gorev ve basarim denetimini yapar, odulleri verir, bildirir.
--- Sayaclari degistiren HER yol bunun uzerinden gecmeli; aksi halde
--- odul sessizce verilmemis olur.
local function settleProgress(player: Player, data: any)
	Tasks.ensure(data, player.UserId)

	local previousLevel = data.level
	for _, completed in Tasks.check(data) do
		notify(player, "good", "notify.taskDone", { completed.nameKey, completed.money, completed.xp })
	end
	for _, unlocked in Achievements.check(data) do
		notify(player, "good", "notify.achievement", { unlocked.nameKey, unlocked.money, unlocked.xp })
	end
	applyLevel(player, data, previousLevel)

	Profile.markDirty(player)
	pushProgress(player)
end

function PatientFlow.pushProgress(player: Player)
	pushProgress(player)
end

function PatientFlow.pushState(player: Player)
	pushState(player)
	pushChart(player)
	pushProgress(player)
end

local function pushStateAll()
	for _, player in Players:GetPlayers() do
		pushState(player)
	end
end

--- Ortak klinik seviyesi yukseldiyse HERKESE duyuruyor ve herkesin
--- HUD'ini tazeliyor. `before` odul verilmeden ONCE okunmus aktif
--- seviye - sonra okunursa atlama hic fark edilmez (`applyLevel` ile
--- ayni kalip).
---
--- Neden herkese? Klinik seviyesi binanin; bir oyuncunun taburcusuyla
--- yukselse bile acilan sey odadaki HERKESE aciliyor. Yalnizca taburcu
--- edene haber vermek, digerlerinin kuyrugunun neden uzadigini
--- anlamamasi demekti.
local function settleClinicLevel(before: number)
	local now = ClinicLevel.activeLevel(Players:GetPlayers())
	if now <= before then
		return
	end
	notifyAll("good", "notify.clinicLevelUp", { now, ClinicLevel.nameKey(now) })
	pushStateAll()
end

--- Ameliyathanedeki oyunculara "asistanlik edebilirsin" haberi.
---
--- Yakinlik SUNUCUDA olculuyor: istemcinin "oradayim" demesine
--- guvenilmiyor (istasyon kuralinin aynisi). Ameliyat olmayan ya da
--- uzakta olan oyuncuya bos bir panel gondermemek icin durum tek bir
--- tabloda gidiyor.
local function announceAssist()
	local surgeons = Surgery.surgeons()
	for _, player in Players:GetPlayers() do
		local payload: any = { active = false, canAssist = false, assisting = false }
		local mine = Surgery.assisting(player)
		if mine ~= nil then
			payload = Surgery.assistSnapshot(mine)
			payload.canAssist = true
			payload.assisting = true
		else
			for _, surgeon in surgeons do
				if surgeon == player then
					continue
				end
				-- Asistan ameliyat masasinin yaninda olmali.
				if nearStation(player, "surgery") then
					local session = Surgery.active(surgeon)
					if session ~= nil and session.assistant == nil then
						payload = Surgery.assistSnapshot(session)
						payload.canAssist = true
						payload.assisting = false
						break
					end
				end
			end
		end
		Net.toPlayer("SurgeryAssistState", player, payload)
	end
end

--- Kogus herkese ayni gorunuyor: padoklar ortak, dolduklarinda herkes
--- icin dolu. Bu yuzden tek bir goruntu butun oyunculara gidiyor.
local function pushWard()
	local snapshot = Ward.snapshot()
	for _, player in Players:GetPlayers() do
		Net.toPlayer("WardUpdate", player, snapshot)
	end
end

-- ── Hasta uretimi ─────────────────────────────────────────────────────

--- Seviyeye uygun (tur, hastalik) cifti secer. Hicbir cift yoksa nil.
--- `routine` true ise rutin islemler, degilse hastaliklar arasindan
--- seciyor. Rutin listesi bossa (veri henuz eklenmemisse) sessizce
--- hastaliklara dusuyor: kuyruk asla bos kalmamali.
local function pickCase(level: number, routine: boolean): (string?, string?)
	local candidates = {}
	for _, animalId in AnimalFactory.allIds() do
		local animal = AnimalFactory.spec(animalId)
		if animal == nil or animal.unlockLevel > level then
			continue
		end
		local list = if routine
			then Diagnosis.routinesFor(animalId, level)
			else Diagnosis.conditionsFor(animalId, level)
		for _, condition in list do
			table.insert(candidates, { animalId, condition.id })
		end
	end
	if #candidates == 0 then
		if routine then
			return pickCase(level, false)
		end
		return nil, nil
	end
	local pick = candidates[math.random(1, #candidates)]
	return pick[1], pick[2]
end

local function updateNameplate(patient: Patient)
	local frame = patient.rig.nameplate:FindFirstChild("Frame")
	if frame == nil then
		return
	end
	local health = frame:FindFirstChild("Saglik")
	local stress = frame:FindFirstChild("Stres")
	if health ~= nil then
		local fill = health:FindFirstChild("Dolgu")
		if fill ~= nil and fill:IsA("Frame") then
			fill.Size = UDim2.fromScale(math.clamp(patient.health / 100, 0, 1), 1)
		end
	end
	if stress ~= nil then
		local fill = stress:FindFirstChild("Dolgu")
		if fill ~= nil and fill:IsA("Frame") then
			fill.Size = UDim2.fromScale(math.clamp(patient.stress / 100, 0, 1), 1)
		end
	end
end

local function spawnPatient()
	if clinic == nil or patientsFolder == nil then
		return
	end

	local slot = freeQueueSlot()
	if slot == nil then
		return -- bekleme yeri dolu
	end

	-- Acil vaka once degerlendiriliyor: bir vaka hem acil hem rutin
	-- olamaz. Gece acil orani daha yuksek (data/daycycle.json).
	local night = DayCycle.modifiers()
	local level = highestLevel()
	local isEmergency = level >= economy.emergency.minLevel
		and math.random() < economy.emergency.chance + night.emergencyBonus
	local isRoutine = not isEmergency and math.random() < economy.routineChance

	local animalId, conditionId = pickCase(level, isRoutine)
	if animalId == nil or conditionId == nil then
		warn("[PatientFlow] bu seviyede uygun hasta bulunamadi - veri tablolarini kontrol edin")
		return
	end

	local spawnAt = CFrame.new(clinic.entranceOutside + Vector3.new(math.random(-6, 6), 0, -14))
	local rig = AnimalFactory.create(animalId, spawnAt, patientsFolder)
	if rig == nil then
		return
	end

	-- Acil vaka: ambulansla gelen agir hasta. minLevel'den once cikmiyor -
	-- yeni oyuncunun ilk dakikalarinda kaldiramayacagi bir sey olmasin.
	-- Rutin bir islem acil olamaz.
	local emergency = isEmergency and not Diagnosis.isRoutine(conditionId)

	local id = nextPatientId
	nextPatientId += 1

	local health = math.random(economy.patient.startHealthMin, economy.patient.startHealthMax)
	if emergency then
		health = math.max(8, health - economy.emergency.healthPenalty)
	end

	local patient: Patient = {
		id = id,
		animalId = animalId,
		conditionId = conditionId,
		rig = rig,
		health = health,
		stress = math.random(economy.patient.startStressMin, economy.patient.startStressMax),
		revealed = {},
		toolsUsed = {},
		-- Rutin islemde teshis yok: hasta zaten "asi olacak" diye
		-- geliyor. Bu yuzden teshisli doguyor ve tedavi paneli aninda
		-- aciliyor.
		diagnosed = Diagnosis.isRoutine(conditionId),
		treatmentsDone = {},
		vet = nil,
		owner = nil,
		emergency = emergency,
		satisfaction = economy.owner.startSatisfaction,
		deadline = if emergency then os.clock() + economy.emergency.timeLimitSeconds else math.huge,
		stationId = nil,
		state = "arriving",
		arrivedAt = os.clock(),
		busyUntil = 0,
		queueSlot = slot,
		samples = {},
		analyzed = {},
		labBonus = false,
	}
	patients[id] = patient
	usedQueueSlots[slot] = true

	AnimalAnimator.register(rig)
	AnimalAnimator.setStress(rig, patient.stress)

	-- Hayvanin sahibi: hastaligin `hintKey` ipucunu soyluyor ve
	-- memnuniyeti bahsisi belirliyor.
	local condition = Diagnosis.condition(conditionId)
	local hintKey = if condition ~= nil then condition.hintKey else "hint.generic"
	local owner = OwnerFactory.create(
		CFrame.new(spawnAt.Position + Vector3.new(3.5, 0, -2)),
		hintKey,
		patientsFolder
	)
	patient.owner = owner
	OwnerFactory.register(owner)
	OwnerFactory.updateSatisfaction(owner, patient.satisfaction)

	if emergency then
		-- Acil vaka gorsel olarak bagirsin: kirmizi donen isik.
		-- Acil feneri NABIZ gibi: sabit yanan bir isik hem dikkat
		-- cekmiyor hem sahneye surekli isik ekliyor.
		local lamp = SkyLighting.lamp("emergency")
		local beacon = Instance.new("PointLight")
		beacon.Name = "AcilIsigi"
		beacon.Color = lamp.color
		beacon.Brightness = 0
		beacon.Range = lamp.range
		beacon.Shadows = false
		beacon.Parent = rig.root
		task.spawn(function()
			local info = TweenInfo.new(0.6, Enum.EasingStyle.Sine, Enum.EasingDirection.InOut, -1, true)
			TweenService:Create(beacon, info, { Brightness = lamp.brightness }):Play()
		end)
		Audio.ambience("emergencySiren", rig.root)
		OwnerFactory.say(owner, "hint.emergency")
		OwnerFactory.gesture(owner, "worry")
	end

	-- Topallayan hastalik gozle belli olsun: oyuncu daha aleti eline
	-- almadan "bacakta bir sey var" diyebilmeli.
	if condition ~= nil then
		for _, symptomId in condition.symptoms do
			if symptomId == "limp" then
				AnimalAnimator.setLimp(rig, math.random(1, 4), 0.8)
				break
			end
		end
	end

	local queuePoint = clinic.queuePoints[slot]
	AnimalAnimator.walkPath(rig, {
		clinic.entranceOutside,
		clinic.entranceInside,
		queuePoint.Position,
	}, function()
		if patients[id] ~= nil and patients[id].state == "arriving" then
			patients[id].state = "waiting"
		end
	end)

	-- Sahip DATA'DAN gelen kendi noktasina yuruyor.
	--
	-- Eskiden bu nokta "kuyruk noktasinin 3 stud sagi" diye kodda
	-- hesaplaniyordu; oysa `data/clinic.json`'daki `ownerPoints` ayri
	-- yazili ve tools/verify_layout.py cakisma denetimini O NOKTALAR
	-- uzerinde yapiyor. Ikisi ayri olunca denetlenen yer ile durulan yer
	-- birbirinden kayiyordu - yani denetim bir sey garanti etmiyordu.
	-- Artik tek kaynak var; nokta yoksa eski davranisa duseceğine
	-- kuyruk noktasinin yaninda duruyor ve bu durum gorunur.
	local ownerPoint = clinic.ownerPoints[slot]
	if ownerPoint == nil then
		warn(("[PatientFlow] %d. kuyruk yuvasi icin sahip noktasi yok (data/clinic.json)"):format(slot))
	end
	local ownerSpot = if ownerPoint ~= nil
		then ownerPoint.Position
		else queuePoint.Position + Vector3.new(3, 0, 1.5)

	OwnerFactory.walkPath(owner, {
		clinic.entranceOutside + Vector3.new(3, 0, 0),
		clinic.entranceInside + Vector3.new(3, 0, 0),
		ownerSpot,
	}, function()
		local current = patients[id]
		if current ~= nil and current.owner ~= nil then
			OwnerFactory.faceTowards(current.owner, current.rig.root.Position)
			-- Yerine varinca hayvanini GOSTERIYOR: oyuncu kalabalik bir
			-- bekleme odasinda hangi sahibin hangi hayvana ait oldugunu
			-- bu jestle okuyor.
			OwnerFactory.gesture(current.owner, "point")
		end
	end)

	updateNameplate(patient)
	local animal = AnimalFactory.spec(animalId)
	local animalKey = if animal ~= nil then animal.nameKey else animalId
	notifyAll(
		if emergency then "bad" else "info",
		if emergency then "notify.emergencyArrived" else "notify.patientArrived",
		{ animalKey }
	)
	pushStateAll()
end

local function releasePatient(patient: Patient)
	if patient.queueSlot ~= nil then
		usedQueueSlots[patient.queueSlot] = nil
	end
	if patient.stationId ~= nil then
		occupiedStation[patient.stationId] = nil
	end
	local vet = patient.vet
	if vet ~= nil and activeOf[vet] == patient.id then
		activeOf[vet] = nil
		Surgery.cancel(vet)
	end
	patients[patient.id] = nil

	AnimalAnimator.unregister(patient.rig)
	patient.rig.model:Destroy()

	if patient.owner ~= nil then
		OwnerFactory.unregister(patient.owner)
		patient.owner.model:Destroy()
		patient.owner = nil
	end
end

--- Hasta klinigi terk eder (taburcu ya da bekleyip kacti).
local function sendAway(patient: Patient, walkOut: boolean)
	if clinic == nil then
		releasePatient(patient)
		return
	end

	patient.state = "leaving"
	if patient.queueSlot ~= nil then
		usedQueueSlots[patient.queueSlot] = nil
		patient.queueSlot = nil
	end
	if patient.stationId ~= nil then
		occupiedStation[patient.stationId] = nil
		patient.stationId = nil
	end
	local vet = patient.vet
	if vet ~= nil and activeOf[vet] == patient.id then
		activeOf[vet] = nil
		Surgery.cancel(vet)
	end

	if not walkOut then
		releasePatient(patient)
		return
	end

	AnimalAnimator.setLimp(patient.rig, 0, 0)
	AnimalAnimator.walkPath(patient.rig, {
		clinic.entranceInside,
		clinic.entranceOutside,
		clinic.entranceOutside + Vector3.new(0, 0, -30),
	}, function()
		local current = patients[patient.id]
		if current ~= nil then
			releasePatient(current)
		end
	end)

	if patient.owner ~= nil then
		OwnerFactory.walkPath(patient.owner, {
			clinic.entranceInside + Vector3.new(3, 0, 0),
			clinic.entranceOutside + Vector3.new(3, 0, 0),
			clinic.entranceOutside + Vector3.new(3, 0, -30),
		})
	end
end

--- Bu vaka agir mi? Ameliyat gerektiren ve acil vakalar kogusa yatiyor.
--- Kural VERIDEN cikiyor (tedavi zinciri), kodda hastalik listesi yok.
local function needsWard(patient: Patient): boolean
	if patient.emergency then
		return true
	end
	for _, treatmentId in Treatment.requiredFor(patient.conditionId) do
		if treatmentId == "surgery" then
			return true
		end
	end
	return false
end

--- Hastayi kogusa yatirir. Sahibi evine gidiyor, hayvan padoga
--- yuruyor. Yatak yoksa `false` doner ve hasta normal taburcu ediliyor -
--- kogus dolu diye oyuncuyu kilitlemek dogru olmaz.
local function admitToWard(patient: Patient, vet: Player): boolean
	if clinic == nil then
		return false
	end
	local animal = AnimalFactory.spec(patient.animalId)
	local entry = Ward.admit(
		patient.id,
		patient.animalId,
		if animal ~= nil then animal.nameKey else patient.animalId,
		vet
	)
	if entry == nil then
		return false
	end

	local bed = clinic.bedPoints[entry.bed]
	if bed == nil then
		Ward.release(entry.bed)
		return false
	end

	patient.state = "ward"
	if patient.queueSlot ~= nil then
		usedQueueSlots[patient.queueSlot] = nil
		patient.queueSlot = nil
	end
	if patient.stationId ~= nil then
		occupiedStation[patient.stationId] = nil
		patient.stationId = nil
	end
	-- Veteriner serbest kaliyor: yatan hasta masayi mesgul etmiyor,
	-- bir sonraki hastayi alabiliyor.
	if activeOf[vet] == patient.id then
		activeOf[vet] = nil
	end

	AnimalAnimator.setLimp(patient.rig, 0, 0)
	AnimalAnimator.walkPath(patient.rig, { bed.Position }, function()
		local current = patients[patient.id]
		if current ~= nil then
			AnimalAnimator.setState(current.rig, "rest")
		end
	end)

	-- Sahip evine gidiyor; hayvani iyilesince almaya donuyor.
	if patient.owner ~= nil then
		OwnerFactory.say(patient.owner, "hint.wardLeave")
		OwnerFactory.walkPath(patient.owner, {
			clinic.entranceInside + Vector3.new(3, 0, 0),
			clinic.entranceOutside + Vector3.new(3, 0, 0),
			clinic.entranceOutside + Vector3.new(3, 0, -30),
		}, function()
			local current = patients[patient.id]
			if current ~= nil and current.owner ~= nil then
				OwnerFactory.unregister(current.owner)
				current.owner.model:Destroy()
				current.owner = nil
			end
		end)
	end
	return true
end

-- ── Oyuncu eylemleri ──────────────────────────────────────────────────

--- Hayvan bu masaya sigiyor mu? Ati kucuk bir yikama kuvetine koymak
--- gercekci degil; kural oynanisin parcasi. Yilan kivrildigi icin
--- govde uzunlugu degil `tableFootprint` kullaniliyor.
local function fitsStation(animalId: string, stationId: string): boolean
	if clinic == nil then
		return false
	end
	local station = clinic.stations[stationId]
	local spec = AnimalFactory.spec(animalId)
	if station == nil or spec == nil then
		return false
	end
	local length, width = AnimalFactory.tableFootprint(spec)
	return length <= station.surface[1] + 1e-6 and width <= station.surface[2] + 1e-6
end

--- Hastayi bir istasyona yerlestirir: kivrilma ve ses dahil.
local function placeOnStation(patient: Patient, stationId: string)
	local station = (clinic :: Clinic.Built).stations[stationId]
	AnimalAnimator.placeAt(patient.rig, station.cframe * CFrame.new(0, 3.6, 0))
	AnimalAnimator.setCoiled(patient.rig, patient.rig.spec.rig == "serpentine")
	Audio.play("patientArrive", patient.rig.root)
end

--- Siradaki hastayi masaya alir; oyuncunun elinde hasta varsa onu
--- yakindaki baska istasyona tasir.
function PatientFlow.callNext(player: Player)
	if clinic == nil then
		return
	end

	local stationId = nearestStation(player)
	if stationId == nil then
		notify(player, "warn", "notify.notAtStation")
		return
	end

	local existing = patientOf(player)
	if existing ~= nil then
		-- Zaten bir hastasi var: onu buraya tasi.
		if existing.stationId == stationId then
			notify(player, "info", "notify.alreadyHere")
			return
		end
		if occupiedStation[stationId] ~= nil then
			notify(player, "warn", "notify.stationBusy")
			return
		end
		if not fitsStation(existing.animalId, stationId) then
			local animal = AnimalFactory.spec(existing.animalId)
			notify(player, "warn", "notify.tooBigForStation", {
				if animal ~= nil then animal.nameKey else existing.animalId,
			})
			return
		end
		if existing.stationId ~= nil then
			occupiedStation[existing.stationId] = nil
		end
		occupiedStation[stationId] = existing.id
		existing.stationId = stationId
		placeOnStation(existing, stationId)
		notify(player, "info", "notify.patientMoved")
		pushChart(player)
		return
	end

	if occupiedStation[stationId] ~= nil then
		notify(player, "warn", "notify.stationBusy")
		return
	end

	-- Kuyrukta en uzun bekleyen, sahipsiz hastayi al.
	local chosen: Patient? = nil
	for _, patient in patients do
		if patient.vet == nil and (patient.state == "waiting" or patient.state == "arriving") then
			if chosen == nil or patient.arrivedAt < chosen.arrivedAt then
				chosen = patient
			end
		end
	end

	if chosen == nil then
		notify(player, "info", if skipped then "notify.noneFitHere" else "notify.queueEmpty")
		return
	end

	chosen.vet = player
	chosen.state = "station"
	chosen.stationId = stationId
	if chosen.queueSlot ~= nil then
		usedQueueSlots[chosen.queueSlot] = nil
		chosen.queueSlot = nil
	end
	occupiedStation[stationId] = chosen.id
	activeOf[player] = chosen.id

	placeOnStation(chosen, stationId)

	notify(player, "good", "notify.patientTaken")
	pushState(player)
	pushChart(player)
	pushStateAll()
end

--- Aletin bulduklarini aciyor ve istemciye bildiriyor. Mini-oyunlu
--- aletlerde ancak mini-oyun KAZANILINCA cagriliyor; mini-oyunsuz
--- aletlerde dogrudan.
--- Bu alet numune mi aliyor? (data/tools.json -> sample)
local function takesSample(toolId: string): boolean
	local tool = Upgrades.tool(toolId)
	return tool ~= nil and tool.sample == true
end

--- Aletin bulgulari. `fromLab` numune incelemesinden geliyorsa true:
--- o zaman numune kuyruga alinmiyor, DOGRUDAN aciliyor.
---
--- Kan ve idrar tahlilinin masa basinda aninda sonuc vermesi zaten
--- gercekci degildi; artik alet numuneyi aliyor, sonucu laboratuvar
--- veriyor. Bulgunun KENDISI degismiyor - yalnizca nerede aciklandigi
--- degisiyor, yani hicbir hastalik cozulemez hale gelmiyor.
local function revealFindings(player: Player, patient: Patient, toolId: string, fromLab: boolean?)
	if fromLab ~= true and takesSample(toolId) then
		patient.toolsUsed[toolId] = true
		patient.samples[toolId] = true
		AnimalAnimator.react(patient.rig, "flinch")
		Audio.play("tool" .. toolId:sub(1, 1):upper() .. toolId:sub(2), patient.rig.root)
		local tool = Upgrades.tool(toolId)
		notify(player, "info", "notify.sampleTaken", { if tool ~= nil then tool.nameKey else toolId })
		pushChart(player)
		return
	end

	local found = Diagnosis.revealWith(patient.revealed, patient.conditionId, toolId)
	for _, symptomId in found do
		patient.revealed[symptomId] = true
	end
	patient.toolsUsed[toolId] = true

	-- Alet degdiginde hayvan basini kaldiriyor: muayenenin oyuncuya
	-- donen tarafi.
	AnimalAnimator.react(patient.rig, if #found > 0 then "perk" else "shake")

	local names = {}
	for _, symptomId in found do
		local symptom = Diagnosis.symptom(symptomId)
		table.insert(names, if symptom ~= nil then symptom.nameKey else symptomId)
	end

	Audio.play("tool" .. toolId:sub(1, 1):upper() .. toolId:sub(2), patient.rig.root)
	Net.toPlayer("ToolResult", player, toolId, names)
	pushChart(player)
end

function PatientFlow.useTool(player: Player, toolId: string)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if patient.stationId == nil or not nearStation(player, patient.stationId) then
		notify(player, "warn", "notify.tooFar")
		return
	end
	if not Upgrades.hasTool(entry.data, toolId) then
		notify(player, "warn", "notify.toolLocked")
		return
	end
	if os.clock() < patient.busyUntil then
		return -- islem suruyor
	end

	local tool = Upgrades.tool(toolId)
	if tool == nil then
		return
	end
	patient.busyUntil = os.clock() + tool.useSeconds

	-- Her alet kullanimi hayvani biraz gerer: butun aletleri sirayla
	-- denemek bedava degil. Mini-oyun kaybedilse bile bu bedel odendi -
	-- aleti hayvana dokundurmak zaten gerdi.
	patient.stress = math.clamp(patient.stress + economy.toolStress, 0, 100)
	AnimalAnimator.setStress(patient.rig, patient.stress)

	-- Mini-oyunlu alet: belirtiler ancak KAZANILINCA aciliyor.
	-- Kaybetmek aleti kilitlemiyor; oyuncu tekrar deneyebiliyor (ve her
	-- deneme hayvani yeniden geriyor).
	local kind = ToolGame.kindOf(toolId)
	if kind ~= "" then
		if ToolGame.active(player) ~= nil then
			return -- bir mini-oyun zaten acik
		end
		local patientId = patient.id
		-- Teknisyen mini-oyun pencerelerini genisletiyor: aleti kullanan
		-- OYUNCUYA isliyor, klinige degil.
		local scale = Staff.effectsOf(entry.data).minigameScale
		local payload = ToolGame.begin(player, patientId, toolId, kind, scale, function(ok: boolean, reasonKey: string)
			Net.toPlayer("ToolGameEnd", player, ok, reasonKey)
			local current = patients[patientId]
			if current == nil or current.vet ~= player then
				return
			end
			if ok then
				revealFindings(player, current, toolId)
			else
				notify(player, "warn", reasonKey)
				pushChart(player)
			end
		end)
		if payload ~= nil then
			Audio.play("tool" .. toolId:sub(1, 1):upper() .. toolId:sub(2), patient.rig.root)
			Net.toPlayer("ToolGameBegin", player, payload)
			return
		end
	end

	revealFindings(player, patient, toolId)
end

--- NUMUNE INCELEMESI
---
--- Oyuncu laboratuvar tezgahinin basinda "incele" diyor; bekleyen
--- butun numuneler ayni anda aciliyor. Hasta muayene masasinda
--- kalabiliyor - numuneyi tasiyan bir nesne yok, dosyada duran bir
--- kayit var. Hangi numunenin bekledigini ve ne cikacagini SUNUCU
--- biliyor; istemci yalnizca niyet yolluyor.
function PatientFlow.analyzeSample(player: Player)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if not nearSpot(player, "lab") then
		notify(player, "warn", "notify.notAtLab")
		return
	end
	if os.clock() < patient.busyUntil then
		return
	end

	local pending = {}
	for toolId in patient.samples do
		if patient.analyzed[toolId] ~= true then
			table.insert(pending, toolId)
		end
	end
	if #pending == 0 then
		notify(player, "info", "notify.noSample")
		return
	end
	-- Sira her incelemede ayni olsun: alet kimligine gore siralaniyor.
	table.sort(pending)

	patient.busyUntil = os.clock() + economy.lab.analysisSeconds
	local data = entry.data
	local previousLevel = data.level
	for _, toolId in pending do
		patient.analyzed[toolId] = true
		revealFindings(player, patient, toolId, true)
	end
	patient.labBonus = true
	data.xp += economy.lab.xpBonus
	awardClinicXp(data, economy.lab.xpBonus)
	applyLevel(player, data, previousLevel)
	Profile.markDirty(player)
	notify(player, "good", "notify.sampleAnalyzed", { #pending })
	pushState(player)
end

--- Ultrason probunun konumu: sunucu yalnizca "ne kadar yakinsin"
--- sayisini donuyor.
function PatientFlow.toolProbe(player: Player, x: number, y: number)
	Net.toPlayer("ToolGameHint", player, ToolGame.probe(player, x, y))
end

--- Mini-oyun cevabi. Sonucu ToolGame.begin'in geri cagrisi isliyor.
function PatientFlow.toolSubmit(player: Player, x: number, y: number)
	ToolGame.submit(player, x, y)
end

function PatientFlow.submitDiagnosis(player: Player, conditionId: string)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if patient.diagnosed then
		notify(player, "info", "notify.alreadyDiagnosed")
		return
	end
	if patient.stationId == nil or not nearStation(player, patient.stationId) then
		notify(player, "warn", "notify.tooFar")
		return
	end
	if Diagnosis.condition(conditionId) == nil then
		return
	end

	local data = entry.data
	local effects = Upgrades.effectsOf(data)

	if Diagnosis.check(patient.conditionId, conditionId) then
		patient.diagnosed = true
		data.reputation = Economy.clampReputation(
			data.reputation + economy.reputation.correctDiagnosis * effects.repGain
		)
		data.stats.correct += 1
		data.stats.streak += 1
		data.stats.bestStreak = math.max(data.stats.bestStreak, data.stats.streak)
		Tasks.bump(data, "correct", 1)
		Tasks.set(data, "streak", data.stats.streak)

		patient.satisfaction = math.clamp(
			patient.satisfaction + economy.owner.correctDiagnosisGain,
			0,
			100
		)
		local condition = Diagnosis.condition(conditionId)
		-- Dogru teshisin ILK isareti hayvanin kendisi: kuyruk sallamasi
		-- ve etrafindaki isilti, bildirimden once goruluyor.
		AnimalAnimator.react(patient.rig, "wag")
		Effects.sparkle(patient.rig.model, patient.rig.root.CFrame, Color3.fromRGB(96, 196, 120))
		if patient.owner ~= nil then
			OwnerFactory.updateSatisfaction(patient.owner, patient.satisfaction)
			OwnerFactory.say(patient.owner, "hint.relieved")
			OwnerFactory.gesture(patient.owner, "relief")
		end
		Audio.play("animal" .. patient.animalId:sub(1, 1):upper() .. patient.animalId:sub(2), patient.rig.root)
		notify(player, "good", "notify.diagnosisCorrect", { condition.nameKey })
	else
		-- Temizlikci itibar cezasini yumusatiyor: hata yine hata ama
		-- klinigin adi o kadar hizli dusmuyor.
		local penaltyScale = Staff.effectsOf(data).penaltyScale
		data.reputation = Economy.clampReputation(
			data.reputation + economy.reputation.wrongDiagnosis * penaltyScale
		)
		data.stats.wrong += 1
		-- Seri tek yanlista sifirlaniyor: "kusursuz" basarimlarinin
		-- anlami olsun.
		data.stats.streak = 0
		patient.stress = math.clamp(patient.stress + economy.wrongDiagnosisStress, 0, 100)
		AnimalAnimator.setStress(patient.rig, patient.stress)

		patient.satisfaction = math.clamp(
			patient.satisfaction + economy.owner.wrongDiagnosisHit,
			0,
			100
		)
		if patient.owner ~= nil then
			OwnerFactory.updateSatisfaction(patient.owner, patient.satisfaction)
			OwnerFactory.say(patient.owner, "hint.worried")
			OwnerFactory.gesture(patient.owner, "worry")
		end
		notify(player, "bad", "notify.diagnosisWrong")
	end

	settleProgress(player, data)
	pushState(player)
	pushChart(player)
end

-- Tedavi -> hayvanin tepkisi. Igne IRKILTIR, yikama GEVSETIR, besleme
-- ILGISINI CEKER. Bilinmeyen tedavi icin "perk": sessiz kalmaktansa
-- hayvanin basini kaldirmasi dogru.
local REACTION_FOR_TREATMENT = {
	injection = "flinch",
	pill = "shake",
	bandage = "flinch",
	drops = "shake",
	wash = "relax",
	feed = "perk",
	water = "relax",
	surgery = "breathe",
}

local function finishTreatment(player: Player, patient: Patient, treatmentId: string, outcome: Treatment.Outcome)
	patient.health = math.clamp(patient.health + outcome.healthDelta, 0, 100)
	patient.stress = math.clamp(patient.stress + outcome.stressDelta, 0, 100)
	AnimalAnimator.setStress(patient.rig, patient.stress)

	-- Tepki ve efekt DOGRU/YANLIS ayrimi yapmadan oynuyor: hayvan yanlis
	-- tedaviye de tepki verir ve oyuncunun "bir sey oldu" gormesi
	-- gerekiyor. Ayrim, tepkinin TURUNDE degil sonucunda.
	AnimalAnimator.react(patient.rig, REACTION_FOR_TREATMENT[treatmentId] or "perk")
	Effects.treatment(patient.rig.model, patient.rig.root.CFrame, treatmentId)

	if outcome.correct then
		Audio.play("treat" .. treatmentId:sub(1, 1):upper() .. treatmentId:sub(2), patient.rig.root)
		patient.treatmentsDone[treatmentId] = true
		-- Kirik iyilesince topallama biter: gorunur geri bildirim.
		if treatmentId == "surgery" or treatmentId == "bandage" then
			AnimalAnimator.setLimp(patient.rig, 0, 0)
		end
	end

	updateNameplate(patient)
	pushChart(player)
	pushState(player)
end

function PatientFlow.applyTreatment(player: Player, treatmentId: string)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if patient.stationId == nil or not nearStation(player, patient.stationId) then
		notify(player, "warn", "notify.tooFar")
		return
	end
	if not patient.diagnosed then
		notify(player, "warn", "notify.diagnoseFirst")
		return
	end
	if os.clock() < patient.busyUntil then
		return
	end

	local treatment = Treatment.get(treatmentId)
	if treatment == nil then
		return
	end

	local data = entry.data
	if data.level < treatment.unlockLevel then
		notify(player, "warn", "notify.treatmentLocked")
		return
	end
	if patient.treatmentsDone[treatmentId] == true then
		notify(player, "info", "notify.treatmentDone")
		return
	end

	local effects = Upgrades.effectsOf(data)
	local cost = Treatment.cost(treatmentId, effects.supplyDiscount)
	if data.money < cost then
		notify(player, "warn", "notify.noMoneyForSupply", { cost })
		return
	end

	-- Ameliyat, ameliyat masasinda yapilir. Mekani oynanisin parcasi
	-- yapan tek kural bu.
	if treatment.minigame == "surgery" and patient.stationId ~= "surgery" then
		notify(player, "warn", "notify.needSurgeryRoom")
		return
	end

	data.money -= cost
	Profile.markDirty(player)

	if treatment.minigame == "surgery" then
		if Surgery.active(player) ~= nil then
			return
		end
		local patientId = patient.id
		-- Hangi ameliyat? Hastalik soyluyor (data/conditions.json ->
		-- surgeryKind); kodda hastalik listesi yok.
		local caseCondition = Diagnosis.condition(patient.conditionId)
		local surgeryKind = if caseCondition ~= nil and caseCondition.surgeryKind ~= nil
			then caseCondition.surgeryKind
			else "rhythm"
		local schedule = Surgery.begin(player, patientId, effects.surgeryTolerance, surgeryKind, function(passed, score, assistant)
			local current = patients[patientId]
			Net.toPlayer("SurgeryEnd", player, passed, score)
			if current == nil then
				return
			end
			-- Asistan bosuna beklemedi: ameliyatin XP'sinden pay aliyor.
			-- Ucret ameliyati yapanin - iki kisi ayni parayi iki kez
			-- alamaz.
			if assistant ~= nil and assistant.Parent ~= nil then
				local helper = Profile.get(assistant)
				if helper ~= nil then
					local share = math.floor(
						Treatment.surgeryTypes[surgeryKind].steps
							* Treatment.assistConfig.xpShare
							* 10
							* score
						+ 0.5
					)
					local before = helper.data.level
					helper.data.xp += share
					applyLevel(assistant, helper.data, before)
					Profile.markDirty(assistant)
					notify(assistant, "good", "notify.assistReward", { share })
					pushState(assistant)
				end
			end
			announceAssist()

			if passed then
				finishTreatment(player, current, treatmentId, {
					ok = true,
					reasonKey = "treat.done",
					correct = true,
					healthDelta = treatment.healthDelta * (0.6 + 0.4 * score),
					stressDelta = treatment.stressDelta,
				})
				data.stats.surgeries += 1
				Tasks.bump(data, "surgeries", 1)
				-- "Kusursuz" ameliyat: 0.9 uzeri puan. Basarim ve gunluk
				-- gorev buna bakiyor.
				if score >= 0.9 then
					data.stats.perfectSurgeries += 1
					Tasks.bump(data, "perfectSurgeries", 1)
				end
				settleProgress(player, data)
				notify(player, "good", "notify.surgerySuccess", { math.floor(score * 100 + 0.5) })
			else
				finishTreatment(player, current, treatmentId, {
					ok = true,
					reasonKey = "treat.wrong",
					correct = false,
					healthDelta = economy.wrongTreatmentHealth,
					stressDelta = treatment.stressDelta,
				})
				notify(player, "bad", "notify.surgeryFailed", { math.floor(score * 100 + 0.5) })
			end
		end)
		Net.toPlayer("SurgeryBegin", player, schedule)
		-- Ameliyathanedeki diger oyunculara "asistanlik edebilirsin"
		-- haberi gidiyor.
		announceAssist()
		pushState(player)
		return
	end

	local seconds = Treatment.seconds(treatmentId, effects.treatSpeed)
	patient.busyUntil = os.clock() + seconds

	local outcome = Treatment.evaluate(patient.conditionId, treatmentId, patient.stress, effects)
	if not outcome.ok then
		patient.stress = math.clamp(patient.stress + outcome.stressDelta, 0, 100)
		AnimalAnimator.setStress(patient.rig, patient.stress)
		notify(player, "bad", outcome.reasonKey)
		pushChart(player)
		return
	end

	local patientId = patient.id
	task.delay(seconds, function()
		local current = patients[patientId]
		if current == nil or current.vet ~= player then
			return
		end
		finishTreatment(player, current, treatmentId, outcome)
		if outcome.correct then
			notify(player, "good", "notify.treatmentApplied", { treatment.nameKey })
		else
			notify(player, "bad", "notify.treatmentWrong")
		end
	end)
end

--- Kogus turu: kacirilan dozlar ve biten yatislar. Vitals dongusunun
--- icinden cagriliyor (ayri bir zamanlayici kurmak, ayni isi iki kez
--- planlamak olurdu).
local function tickWard()
	local result = Ward.tick()
	if #result.missed == 0 and #result.done == 0 then
		return
	end

	local ward = economy.ward
	for _, entry in result.missed do
		local patient = patients[entry.patientId]
		if patient ~= nil then
			patient.health = math.clamp(patient.health + ward.missedDoseHealth, 0, 100)
			updateNameplate(patient)
		end
		local vet = entry.vet
		if vet ~= nil and vet.Parent ~= nil then
			local profile = Profile.get(vet)
			if profile ~= nil then
				profile.data.reputation = Economy.clampReputation(
					profile.data.reputation
						+ ward.missedDoseReputation * Staff.effectsOf(profile.data).penaltyScale
				)
				Profile.markDirty(vet)
			end
			notify(vet, "bad", "notify.wardMissed", { entry.nameKey })
			pushState(vet)
		end
	end

	local clinicBefore = ClinicLevel.activeLevel(Players:GetPlayers())
	for _, entry in result.done do
		local patient = patients[entry.patientId]
		local score = Ward.careScore(entry)
		local vet = entry.vet
		if vet ~= nil and vet.Parent ~= nil then
			local profile = Profile.get(vet)
			if profile ~= nil then
				local data = profile.data
				local previousLevel = data.level
				-- Yatis ucreti BAKIM PUANIYLA carpiliyor: kogusu unutan
				-- oyuncu hastayi kaybetmiyor ama parayi da almiyor.
				local fee = math.floor(ward.boardingFee * score + 0.5)
				local xpGain = math.floor(ward.boardingXp * score + 0.5)
				data.money += fee
				data.xp += xpGain
				awardClinicXp(data, xpGain)
				data.stats.earned += fee
				Tasks.bump(data, "earned", fee)
				applyLevel(vet, data, previousLevel)
				settleProgress(vet, data)
				Profile.markDirty(vet)
				notify(vet, "good", "notify.wardDone", { entry.nameKey, fee, math.floor(score * 100 + 0.5) })
				pushState(vet)
			end
		end
		if patient ~= nil then
			sendAway(patient, true)
		end
	end

	settleClinicLevel(clinicBefore)
	pushWard()
end

--- Yatan hastaya ilac verir. Yakinlik ve zaman penceresi SUNUCUDA
--- denetleniyor (Ward.giveDose).
function PatientFlow.giveMedicine(player: Player, bed: number)
	if bed % 1 ~= 0 or bed < 1 then
		return
	end
	local result = Ward.giveDose(player, bed)
	if not result.ok then
		notify(player, "warn", result.reasonKey)
		return
	end

	local entry = result.entry
	if entry == nil then
		return
	end

	local ward = economy.ward
	local patient = patients[entry.patientId]
	if patient ~= nil then
		patient.health = math.clamp(patient.health + ward.healthPerDose, 0, 100)
		patient.stress = math.clamp(patient.stress - 6, 0, 100)
		AnimalAnimator.setStress(patient.rig, patient.stress)
		AnimalAnimator.react(patient.rig, "relax")
		Effects.treatment(patient.rig.model, patient.rig.root.CFrame, "pill")
		Audio.play("treatPill", patient.rig.root)
		updateNameplate(patient)
	end

	local profile = Profile.get(player)
	if profile ~= nil then
		profile.data.reputation = Economy.clampReputation(
			profile.data.reputation + ward.dosedReputation
		)
		Profile.markDirty(player)
		pushState(player)
	end

	notify(player, "good", "notify.wardDose", { entry.nameKey })
	pushWard()
end

--- Asistanin darbesi (Bosluk basili tutulurken saniyede ~7 kez).
--- Ilk darbe ayni zamanda asistanligi ustleniyor.
function PatientFlow.surgeryAssist(player: Player)
	if not nearStation(player, "surgery") then
		return
	end
	for _, surgeon in Surgery.surgeons() do
		if surgeon == player then
			continue
		end
		local result = Surgery.assistPulse(player, surgeon)
		if result.ok and result.session ~= nil then
			local payload = Surgery.assistSnapshot(result.session)
			payload.canAssist = true
			payload.assisting = true
			Net.toPlayer("SurgeryAssistState", player, payload)
			-- Cerrah da asistaninin ne kadar saglam tuttugunu goruyor.
			local surgeonPayload = Surgery.assistSnapshot(result.session)
			surgeonPayload.canAssist = false
			surgeonPayload.assisting = false
			Net.toPlayer("SurgeryAssistState", surgeon, surgeonPayload)
			return
		end
	end
end

function PatientFlow.surgeryInput(player: Player, step: number)
	local result = Surgery.input(player, step)
	if result.accepted then
		Net.toPlayer("ToolResult", player, "surgeryStep", { result.grade })
	end
end

function PatientFlow.discharge(player: Player)
	local entry = Profile.get(player)
	if entry == nil then
		return
	end
	local patient = patientOf(player)
	if patient == nil then
		notify(player, "warn", "notify.noPatient")
		return
	end
	if not patient.diagnosed then
		notify(player, "warn", "notify.diagnoseFirst")
		return
	end
	if not Treatment.isComplete(patient.treatmentsDone, patient.conditionId) then
		notify(player, "warn", "notify.treatmentsIncomplete")
		return
	end

	local data = entry.data
	local effects = Upgrades.effectsOf(data)
	local animal = AnimalFactory.spec(patient.animalId)
	local condition = Diagnosis.condition(patient.conditionId)
	if animal == nil or condition == nil then
		return
	end

	-- Ucret carpani IKI kaynaktan: oyuncunun kendi yukseltmeleri ve
	-- ortak klinik seviyesi. Ikisi de "ucret zammi" oldugu icin
	-- CARPILIYORLAR; toplansalardi iki kucuk zam bir buyuk zamdan daha
	-- cok eder, yani yanlis olurdu.
	local clinicBonus = ClinicLevel.effects(Players:GetPlayers())
	local fee = Economy.fee({
		baseFee = animal.baseFee,
		feeMultiplier = condition.feeMultiplier,
		reputation = data.reputation,
		health = patient.health,
		-- Laboratuvarda incelenen numune kucuk bir zam getiriyor:
		-- kuzey kanadina yurumenin karsiligi.
		feeBonus = effects.feeBonus
			* clinicBonus.feeBonus
			* (if patient.labBonus then economy.lab.feeBonus else 1),
	})

	-- Seviye, XP eklenmeden ONCE okunmali: sonra okunursa atlama hic
	-- fark edilmez.
	local previousLevel = data.level
	local clinicBefore = ClinicLevel.activeLevel(Players:GetPlayers())
	local xpGain = condition.xp

	-- Gece nobeti zammi: gece hasta seyrek ama ucret ve XP zamli.
	-- Carpan HASTA GELDIGI an degil, TABURCU edildigi an okunuyor -
	-- yani nobeti bitirmek de nobette calismak kadar onemli.
	local shift = DayCycle.modifiers()
	fee = math.floor(fee * shift.feeMultiplier + 0.5)
	xpGain = math.floor(xpGain * shift.xpMultiplier + 0.5)

	if patient.emergency then
		fee = math.floor(fee * economy.emergency.feeMultiplier + 0.5)
		xpGain = math.floor(xpGain * economy.emergency.xpMultiplier + 0.5)
		data.stats.emergencies += 1
		Tasks.bump(data, "emergencies", 1)
		data.reputation = Economy.clampReputation(
			data.reputation + economy.emergency.reputationGain * effects.repGain
		)
	end

	-- Bahsis: sahibi yeterince memnunsa ucretin uzerine birakiyor.
	-- Bekletmemenin ve dogru teshisin gorunur karsiligi bu.
	local tip = 0
	if patient.satisfaction >= economy.owner.minTipSatisfaction then
		local share = (patient.satisfaction - economy.owner.minTipSatisfaction)
			/ math.max(1, 100 - economy.owner.minTipSatisfaction)
		tip = math.floor(fee * economy.owner.tipShare * share + 0.5)
	end

	data.money += fee + tip
	data.xp += xpGain
	awardClinicXp(data, xpGain)
	data.stats.patients += 1
	data.stats.earned += fee + tip
	data.stats.tips += tip
	data.species[patient.animalId] = true

	Tasks.bump(data, "patients", 1)
	Tasks.bump(data, "earned", fee + tip)
	Tasks.markSpecies(data, patient.animalId)

	if patient.health >= economy.patient.healthyDischargeThreshold then
		data.reputation = Economy.clampReputation(
			data.reputation + economy.reputation.healthyDischarge * effects.repGain
		)
		patient.satisfaction = math.clamp(
			patient.satisfaction + economy.owner.healthyDischargeGain,
			0,
			100
		)
	elseif patient.stress >= 70 then
		data.reputation = Economy.clampReputation(data.reputation + economy.reputation.stressedDischarge)
	end

	if patient.owner ~= nil then
		OwnerFactory.updateSatisfaction(patient.owner, patient.satisfaction)
		OwnerFactory.say(patient.owner, if tip > 0 then "hint.thanks" else "hint.goodbye")
		OwnerFactory.gesture(patient.owner, "relief")
	end

	applyLevel(player, data, previousLevel)
	settleClinicLevel(clinicBefore)

	if tip > 0 then
		notify(player, "good", "notify.dischargedTip", { fee, tip, xpGain })
	else
		notify(player, "good", "notify.discharged", { fee, xpGain })
	end

	settleProgress(player, data)

	Audio.play("discharge", patient.rig.root)

	-- AGIR VAKA KOGUSA: ameliyat ve acil vakalar kapidan cikmiyor,
	-- iyilesmesi icin padoga yatiyor. Ucretin geri kalani (yatis
	-- ucreti) ancak bakim bitince ve BAKIM PUANIYLA carpilarak
	-- odeniyor - kogusu unutan oyuncu parayi da alamiyor.
	if needsWard(patient) and admitToWard(patient, player) then
		notify(player, "info", "notify.wardAdmitted", { animal.nameKey })
		pushWard()
	else
		sendAway(patient, true)
	end
	pushState(player)
	pushChart(player)
	pushStateAll()
end

--- Oyuncu ayrildi: hastasini kuyruga geri koymak yerine birakiyoruz ki
--- baska bir veteriner alabilsin.
function PatientFlow.playerLeft(player: Player)
	-- Kogustaki hastalari sahipsiz kaliyor: iyilesip gidiyorlar ama
	-- ucreti kimse alamiyor.
	Ward.playerLeft(player)
	Surgery.cancel(player)
	local patient = patientOf(player)
	activeOf[player] = nil
	if patient == nil then
		return
	end
	patient.vet = nil
	-- Masada kalsin; siradaki oyuncu "Sirayi cagir" ile devralabilir.
	patient.state = "waiting"
end

-- ── Dongu ─────────────────────────────────────────────────────────────

--- BULASMA
---
--- Bulasici bir hasta karantinaya alinmadan klinikte durdugu surece
--- BEKLEYEN butun hayvanlarin sagligi ve memnuniyeti geriliyor,
--- klinigin itibari dusuyor.
---
--- Neden hasta kendisi "bulasici" diye isaretlenmiyor da bekleyenler
--- eziliyor? Cunku bir hastanin KIMLIGI istemciye teshisten once
--- gitmiyor ve gitmemeli. "Su hasta bulasici" demek, hastalik
--- listesini 35'ten 6'ya indirmek olurdu. Bunun yerine bulasma
--- OLCULEBILIR bir etki olarak gorunuyor: bekleyenlerin sagligi
--- dusuyor, hayvanlar oksuruyor. Oyuncu sebebini muayene ederek
--- buluyor - yani oyunun kendisini oynayarak.
---
--- Karantina bolmesi bir ISTASYON: hasta oraya yatirilinca yayilma
--- duruyor ve tedavinin tamami orada yapilabiliyor. Yani karantina
--- "hastayi bir kenara koy" cezasi degil, akisin icinde bir yer.
local function tickContagion()
	local config = economy.contagion
	local sources = {}
	local waiting = {}
	for _, patient in patients do
		if patient.state == "leaving" or patient.state == "ward" then
			continue
		end
		if patient.state == "waiting" or patient.state == "arriving" then
			table.insert(waiting, patient)
		end
		local condition = Diagnosis.condition(patient.conditionId)
		if condition ~= nil and condition.contagious == true and not isQuarantineStation(patient.stationId) then
			table.insert(sources, patient)
		end
	end
	if #sources == 0 then
		return false
	end

	-- Kaynagin kendisi kurbanlardan sayilmiyor: hayvan kendine
	-- bulastirmaz.
	local hurt = 0
	for _, victim in waiting do
		local isSource = false
		for _, source in sources do
			if source.id == victim.id then
				isSource = true
				break
			end
		end
		if isSource then
			continue
		end
		victim.health = math.clamp(victim.health + config.healthDrain, 0, 100)
		victim.satisfaction = math.clamp(victim.satisfaction + config.satisfactionDrain, 0, 100)
		if victim.owner ~= nil then
			OwnerFactory.updateSatisfaction(victim.owner, victim.satisfaction)
		end
		updateNameplate(victim)
		hurt += 1
	end

	-- Oksuruk: bulasmanin GORUNUR tarafi. Ekranda bir yazi cikmiyor;
	-- hayvanlar oksuruyor ve oyuncu bunu fark ediyor.
	for _, source in sources do
		AnimalAnimator.react(source.rig, "shake")
	end
	if hurt > 0 and #waiting > 0 then
		AnimalAnimator.react(waiting[math.random(#waiting)].rig, "shake")
		for _, player in Players:GetPlayers() do
			local entry = Profile.get(player)
			if entry ~= nil then
				entry.data.reputation = Economy.clampReputation(
					entry.data.reputation + config.reputationDrain * Staff.effectsOf(entry.data).penaltyScale
				)
				Profile.markDirty(player)
			end
		end
	end

	-- Uyari ancak TESHIS KONMUSSA veriliyor: o noktada hastaligin
	-- bulasici oldugunu oyuncu zaten biliyor, yani sizan bir bilgi yok.
	for _, source in sources do
		if source.diagnosed then
			return true
		end
	end
	return false
end

local function tickVitals(dt: number)
	local config = economy.patient
	-- Hemsire butun klinigin stresini dusuruyor: tek bir hastaya degil,
	-- odadaki herkese isliyor.
	local staffEffects = Staff.clinicEffects(Players:GetPlayers())
	for _, patient in patients do
		if patient.state == "leaving" then
			continue
		end
		if patient.state == "ward" then
			-- Yatan hasta iyilesme surecinde: sagligi kendiliginden
			-- dusmuyor (tedavisi bitti) ve sahibi evde, sabri
			-- tukenmiyor. Doz zamanlayicisi asagidaki kogus turunde.
			AnimalAnimator.setStress(patient.rig, patient.stress)
			AnimalAnimator.lookAt(patient.rig, nil)
			updateNameplate(patient)
			continue
		end

		-- Tedavi edilmeyen hasta yavas yavas kotuluyor.
		if not Treatment.isComplete(patient.treatmentsDone, patient.conditionId) then
			patient.health = math.clamp(patient.health - config.healthDrainPerSecond * dt, 0, 100)
		end

		-- Stres kendiliginden yavas yavas duser.
		if patient.stress > 0 then
			local decay = config.stressDecayPerSecond + staffEffects.stressRelief
			patient.stress = math.clamp(patient.stress - decay * dt, 0, 100)
			AnimalAnimator.setStress(patient.rig, patient.stress)
		end

		AnimalAnimator.setState(patient.rig, if patient.stress >= 70 then "distress" else "idle")
		updateNameplate(patient)

		-- BAKIS: masadaki hayvan veterinere, bekleyen hayvan sahibine
		-- bakiyor. "Bu hayvan beni goruyor" hissini veren tek sey bu.
		-- Yururken bakis birakiliyor: yuruyen bir hayvanin basi yola
		-- donuk olmali.
		if patient.state == "walking" or patient.state == "arriving" or patient.state == "leaving" then
			AnimalAnimator.lookAt(patient.rig, nil)
		else
			local lookAt: Vector3? = nil
			local vet = patient.vet
			if vet ~= nil then
				local character = vet.Character
				local head = if character ~= nil then character:FindFirstChild("Head") else nil
				if head ~= nil and head:IsA("BasePart") then
					lookAt = head.Position
				end
			end
			if lookAt == nil and patient.owner ~= nil then
				lookAt = patient.owner.root.Position + Vector3.new(0, 4, 0)
			end
			-- Stresli hayvan bakisini kaciriyor: guclu stres agirligi
			-- dusuruyor.
			AnimalAnimator.lookAt(patient.rig, lookAt, 1 - patient.stress / 200)

			-- Sahip de bakiyor: veteriner varsa ona, yoksa hayvanina.
			-- Bekleme odasinda kimin kime ait oldugu boylece iyice
			-- okunur hale geliyor.
			if patient.owner ~= nil then
				OwnerFactory.lookAt(
					patient.owner,
					if lookAt ~= nil then lookAt else patient.rig.root.Position
				)
			end
		end

		-- Sahibin sabri: bekledikce tukeniyor. Hastasi masaya alinmis
		-- olsa bile isin uzamasi memnuniyeti dusuruyor.
		if patient.owner ~= nil and patient.state ~= "leaving" then
			patient.satisfaction = math.clamp(
				patient.satisfaction - economy.owner.decayPerSecond * dt,
				0,
				100
			)
			OwnerFactory.updateSatisfaction(patient.owner, patient.satisfaction)
		end

		-- Acil vakanin suresi: dolarsa hasta kaybediliyor. Bu, oyunun
		-- kaybedilebilir TEK durumu; bu yuzden ceza da agir.
		if patient.emergency and os.clock() > patient.deadline then
			local animal = AnimalFactory.spec(patient.animalId)
			local animalKey = if animal ~= nil then animal.nameKey else patient.animalId
			notifyAll("bad", "notify.emergencyLost", { animalKey })
			local vet = patient.vet
			if vet ~= nil then
				local entry = Profile.get(vet)
				if entry ~= nil then
					entry.data.reputation = Economy.clampReputation(
						entry.data.reputation + economy.emergency.reputationLoss
					)
					entry.data.stats.lost += 1
					entry.data.stats.streak = 0
					Profile.markDirty(vet)
				end
			end
			sendAway(patient, true)
			pushStateAll()
			continue
		end

		-- Bekleyip bekleyip kacan hasta: butun klinigin itibari duser.
		if patient.vet == nil and patient.state == "waiting" then
			if os.clock() - patient.arrivedAt > economy.arrival.patienceSeconds then
				local animal = AnimalFactory.spec(patient.animalId)
				notifyAll("bad", "notify.patientLeft", { if animal ~= nil then animal.nameKey else patient.animalId })
				for _, player in Players:GetPlayers() do
					local entry = Profile.get(player)
					if entry ~= nil then
						entry.data.reputation = Economy.clampReputation(
							entry.data.reputation + economy.reputation.timeout
						)
						Profile.markDirty(player)
					end
				end
				sendAway(patient, true)
				pushStateAll()
			end
		end
	end
end

--- Klinigi bagla ve dongulari baslat.
function PatientFlow.start(built: Clinic.Built)
	if started then
		return
	end
	started = true
	clinic = built

	-- Kogus yataklari klinikle birlikte kuruluyor.
	Ward.start(built.bedPoints)

	local folder = Instance.new("Folder")
	folder.Name = "Hastalar"
	folder.Parent = workspace
	patientsFolder = folder

	-- Klinigin ortam ugultusu: sessiz bir odadan cok daha yasayan bir yer.
	local ambience = Instance.new("Part")
	ambience.Name = "OrtamSesi"
	ambience.Size = Vector3.new(1, 1, 1)
	ambience.CFrame = CFrame.new(built.entranceInside)
	ambience.Anchored = true
	ambience.Transparency = 1
	ambience.CanCollide = false
	ambience.CanQuery = false
	ambience.Parent = workspace
	Audio.ambience("clinicAmbience", ambience)

	-- Hasta gelisi
	task.spawn(function()
		-- Ilk hasta hemen gelmesin: oyuncu once etrafa bakabilsin.
		task.wait(6)
		while true do
			if #Players:GetPlayers() > 0 then
				spawnPatient()
			end
			local clinicEffects = Upgrades.clinicEffects(Players:GetPlayers())
			-- Gece hasta SEYREK geliyor: arrivalScale 1'in ustunde,
			-- yani bekleme suresi uzuyor.
			local shift = DayCycle.modifiers()
			-- Klinik seviyesi de gelis arasini kisaltiyor (arrivalScale
			-- 1'in altinda). Uc carpan da ayni eksende oldugu icin
			-- carpiliyor.
			local levelEffects = ClinicLevel.effects(Players:GetPlayers())
			task.wait(Economy.arrivalSeconds(
				bestReputation(),
				clinicEffects.arrivalScale * shift.arrivalScale * levelEffects.arrivalScale
			))
		end
	end)

	-- Yasamsal belirtiler + kogus
	task.spawn(function()
		-- Asistanlik cagrisi saniyede bir tazeleniyor: ameliyat
		-- basladiktan SONRA odaya giren oyuncu da cagriyi gormeli.
		-- Vitals hiziyla (4/sn) yayin yapmak bosuna paket olurdu.
		local assistTick = 0
		local assistWasActive = false
		-- Bulasma kendi hizinda: vitals 4/sn donuyor, bulasmanin o
		-- sikligta islemesi sayilari anlamsiz kilardi.
		local contagionTick = 0
		local contagionEvery = math.max(1, math.floor(economy.contagion.tickSeconds / VITALS_TICK + 0.5))
		while true do
			task.wait(VITALS_TICK)
			tickVitals(VITALS_TICK)
			tickWard()

			contagionTick += 1
			if contagionTick >= contagionEvery then
				contagionTick = 0
				local alert = tickContagion()
				if alert ~= contagionAlert then
					contagionAlert = alert
					if alert then
						notifyAll("bad", "notify.contagionAlert")
					end
				end
				pushStateAll()
			end

			assistTick += 1
			if assistTick >= 4 then
				assistTick = 0
				local active = #Surgery.surgeons() > 0
				-- Ameliyat bitince bir kez daha yayin yapiliyor ki
				-- kartlar kapansin.
				if active or assistWasActive then
					announceAssist()
				end
				assistWasActive = active
			end
		end
	end)

	-- Arayuzun kuyruk sayaci canli kalsin.
	task.spawn(function()
		while true do
			task.wait(2)
			for _, player in Players:GetPlayers() do
				pushChart(player)
			end
		end
	end)
end

--- Kac hasta canli? SelfTest ve tanilama icin.
function PatientFlow.patientCount(): number
	local total = 0
	for _ in patients do
		total += 1
	end
	return total
end

return PatientFlow
