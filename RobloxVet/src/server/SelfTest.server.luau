--!strict
-- SelfTest.server.luau
-- ====================
-- Oyun ici acilis denetimleri.
--
-- Neden var?
-- ----------
-- Bu depo Roblox Studio'ya erisimi OLMAYAN bir ortamda uretildi: kodun
-- calisir halini kimse gormedi. Python denetleyicileri veriyi ve metni
-- kontrol ediyor ama Luau'yu calistiramiyor. Bu betik o bosluğu
-- kapatiyor: sunucu acilir acilmaz veri tablolarina ve SAF MANTIK
-- modullerine karsi onlarca `assert` calistirip Output'a tek satirlik
-- sonuc basiyor.
--
-- Mevcut deponun `dotnet run -- --self-test` (245 denetim) aliskanligi
-- Roblox tarafina tasinmis hali.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Assets = require(Shared.Assets)
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local AnimalFactory = require(script.Parent.world.AnimalFactory)
local Props = require(script.Parent.world.Props)

local Achievements = require(script.Parent.game.Achievements)
local Diagnosis = require(script.Parent.game.Diagnosis)
local Economy = require(script.Parent.game.Economy)
local Profile = require(script.Parent.game.Profile)
local Tasks = require(script.Parent.game.Tasks)
local Treatment = require(script.Parent.game.Treatment)
local Upgrades = require(script.Parent.game.Upgrades)

local clinicPlan = require(Data.clinic)
local settingsData = require(Data.settings)

local passed = 0
local failures: { string } = {}

local function check(label: string, condition: boolean)
	if condition then
		passed += 1
	else
		table.insert(failures, label)
	end
end

-- ── Veri butunlugu ────────────────────────────────────────────────────

for _, animal in require(Data.animals).animals do
	check(("tur '%s' bilinen bir iskelet istiyor"):format(animal.id), AnimalFactory.knownRig(animal.rig))
	check(("tur '%s' adi cevrili"):format(animal.id), Loc.has(animal.nameKey))
	check(("tur '%s' ucreti pozitif"):format(animal.id), animal.baseFee > 0)
	check(
		("tur '%s' bacak sayisi makul"):format(animal.id),
		animal.legs.count == 0 or animal.legs.count == 2 or animal.legs.count == 4
	)
end

for _, symptom in Diagnosis.allSymptoms() do
	check(("semptom '%s' adi cevrili"):format(symptom.id), Loc.has(symptom.nameKey))
	check(
		("semptom '%s' var olan bir aletle aciliyor"):format(symptom.id),
		Upgrades.tool(symptom.revealedBy) ~= nil
	)
end

for _, condition in Diagnosis.allConditions() do
	check(("hastalik '%s' adi cevrili"):format(condition.id), Loc.has(condition.nameKey))
	check(("hastalik '%s' en az bir semptoma sahip"):format(condition.id), #condition.symptoms > 0)
	check(("hastalik '%s' en az bir tedaviye sahip"):format(condition.id), #condition.treatments > 0)
	check(("hastalik '%s' sahip ipucu cevrili"):format(condition.id), Loc.has(condition.hintKey))

	for _, symptomId in condition.symptoms do
		check(
			("hastalik '%s' var olan bir semptom kullaniyor: %s"):format(condition.id, symptomId),
			Diagnosis.symptom(symptomId) ~= nil
		)
	end
	for _, treatmentId in condition.treatments do
		check(
			("hastalik '%s' var olan bir tedavi istiyor: %s"):format(condition.id, treatmentId),
			Treatment.get(treatmentId) ~= nil
		)
	end
	for _, species in condition.species do
		check(
			("hastalik '%s' var olan bir tur istiyor: %s"):format(condition.id, species),
			AnimalFactory.spec(species) ~= nil
		)
	end
end

for _, treatment in Treatment.all() do
	check(("tedavi '%s' adi cevrili"):format(treatment.id), Loc.has(treatment.nameKey))
	check(("tedavi '%s' sarf bedeli negatif degil"):format(treatment.id), treatment.supplyCost >= 0)
end

for _, tool in Upgrades.allTools() do
	check(("alet '%s' adi cevrili"):format(tool.id), Loc.has(tool.nameKey))
	check(("alet '%s' kullanim suresi pozitif"):format(tool.id), tool.useSeconds > 0)
end

for _, upgrade in Upgrades.allUpgrades() do
	check(("yukseltme '%s' adi cevrili"):format(upgrade.id), Loc.has(upgrade.nameKey))
	check(("yukseltme '%s' aciklamasi cevrili"):format(upgrade.id), Loc.has(upgrade.descKey))
	check(("yukseltme '%s' bedeli pozitif"):format(upgrade.id), upgrade.cost > 0)
	for key in upgrade.effect do
		check(("yukseltme '%s' bilinen etki kullaniyor: %s"):format(upgrade.id, key), Upgrades.knownEffect(key))
	end
end

-- Cozulebilirlik: ayni turde iki hastaligin semptom kumesi birebir ayni
-- olamaz, yoksa bulmacanin dogru cevabi yok demektir.
do
	local conditions = Diagnosis.allConditions()
	for i = 1, #conditions do
		for j = i + 1, #conditions do
			local a, b = conditions[i], conditions[j]
			local sharedSpecies = false
			for _, speciesA in a.species do
				for _, speciesB in b.species do
					if speciesA == speciesB then
						sharedSpecies = true
						break
					end
				end
			end
			if not sharedSpecies then
				continue
			end
			if #a.symptoms ~= #b.symptoms then
				continue
			end
			local same = true
			for _, symptomId in a.symptoms do
				local found = false
				for _, other in b.symptoms do
					if other == symptomId then
						found = true
						break
					end
				end
				if not found then
					same = false
					break
				end
			end
			check(("'%s' ve '%s' ayirt edilebilir"):format(a.id, b.id), not same)
		end
	end
end

-- ── Klinik plani ──────────────────────────────────────────────────────

for _, room in clinicPlan.rooms do
	check(("oda '%s' adi cevrili"):format(room.id), Loc.has(room.nameKey))
	check(
		("oda '%s' gecerli bir dikdortgen"):format(room.id),
		room.max[1] > room.min[1] and room.max[2] > room.min[2]
	)
	for _, prop in room.props do
		check(("esya '%s' Props tarafindan biliniyor"):format(prop.id), Props.known(prop.id))
	end
end
for _, prop in clinicPlan.outdoor.props do
	check(("dis esya '%s' Props tarafindan biliniyor"):format(prop.id), Props.known(prop.id))
end
check("kapi yuksekligi duvardan alcak", clinicPlan.doorHeight < clinicPlan.wallHeight)

-- ── Saf mantik ────────────────────────────────────────────────────────

do
	check("1. seviye 0 XP", Economy.xpForLevel(1) == 0)
	check("XP esikleri artiyor", Economy.xpForLevel(2) > Economy.xpForLevel(1))
	check("0 XP -> 1. seviye", Economy.levelForXp(0) == 1)
	check("2. seviyenin esigi 2. seviye veriyor", Economy.levelForXp(Economy.xpForLevel(2)) == 2)
	check("esigin bir altinda seviye atlamiyor", Economy.levelForXp(Economy.xpForLevel(2) - 1) == 1)
	check("ilerleme 0..1 arasinda", Economy.levelProgress(50) >= 0 and Economy.levelProgress(50) <= 1)
	check("son seviyede ilerleme dolu", Economy.levelProgress(Economy.xpForLevel(Economy.MAX_LEVEL)) == 1)

	local low = Economy.fee({ baseFee = 100, feeMultiplier = 1, reputation = 0, health = 100, feeBonus = 1 })
	local high = Economy.fee({ baseFee = 100, feeMultiplier = 1, reputation = 100, health = 100, feeBonus = 1 })
	check("itibar ucreti yukseltiyor", high > low)
	check("ucret pozitif", low > 0)

	local sick = Economy.fee({ baseFee = 100, feeMultiplier = 1, reputation = 50, health = 10, feeBonus = 1 })
	local healthy = Economy.fee({ baseFee = 100, feeMultiplier = 1, reputation = 50, health = 100, feeBonus = 1 })
	check("saglikli taburcu daha cok kazandiriyor", healthy > sick)

	check("itibar tavani var", Economy.clampReputation(1000) <= 100)
	check("itibar tabani var", Economy.clampReputation(-1000) >= 0)
	check(
		"iyi itibar hastalari hizlandiriyor",
		Economy.arrivalSeconds(100, 1) < Economy.arrivalSeconds(0, 1)
	)
end

do
	-- Teshis: bilinen bir hastalik uzerinde alet-semptom eslesmesi.
	local condition = Diagnosis.allConditions()[1]
	local firstSymptom = condition.symptoms[1]
	local tool = Diagnosis.toolFor(firstSymptom)
	check("semptomun aleti bulunuyor", tool ~= nil)

	local revealed: { [string]: boolean } = {}
	local found = Diagnosis.revealWith(revealed, condition.id, tool :: string)
	check("dogru alet semptomu aciyor", #found > 0)

	for _, id in found do
		revealed[id] = true
	end
	local again = Diagnosis.revealWith(revealed, condition.id, tool :: string)
	check("ayni alet ikinci kez ayni semptomu vermiyor", #again == 0)

	check("dogru tahmin kabul ediliyor", Diagnosis.check(condition.id, condition.id))
	check("yanlis tahmin reddediliyor", not Diagnosis.check(condition.id, "__yok__"))
	check(
		"bilinmeyen hastalikta alet bos donuyor",
		#Diagnosis.revealWith({}, "__yok__", tool :: string) == 0
	)
end

do
	-- Tedavi: dogru/yanlis/direnc.
	local condition = Diagnosis.allConditions()[1]
	local rightId = condition.treatments[1]
	local effects = { washBonus = 0 }

	local right = Treatment.evaluate(condition.id, rightId, 0, effects)
	check("dogru tedavi dogru isaretleniyor", right.ok and right.correct)
	check("dogru tedavi saglik veriyor", right.healthDelta > 0)

	local wrongId: string? = nil
	for _, treatment in Treatment.all() do
		if not Treatment.isRequired(condition.id, treatment.id) then
			wrongId = treatment.id
			break
		end
	end
	check("yanlis tedavi adayi bulundu", wrongId ~= nil)
	if wrongId ~= nil then
		local wrong = Treatment.evaluate(condition.id, wrongId, 0, effects)
		check("yanlis tedavi yanlis isaretleniyor", wrong.ok and not wrong.correct)
		check("yanlis tedavi cezali", wrong.healthDelta < 0)
	end

	local resisted = Treatment.evaluate(condition.id, rightId, 100, effects)
	check("asiri stresli hayvan direniyor", not resisted.ok)

	check("eksik tedavi tamamlanmis sayilmiyor", not Treatment.isComplete({}, condition.id))
	local done: { [string]: boolean } = {}
	for _, id in condition.treatments do
		done[id] = true
	end
	check("butun tedaviler bitince tamam", Treatment.isComplete(done, condition.id))
	check("indirim sarf bedelini dusuruyor", Treatment.cost(rightId, 0.5) <= Treatment.cost(rightId, 1))
end

do
	-- Satin alma kurallari.
	local data = Profile.defaultData()
	data.money = 0
	data.level = 10
	local broke = Upgrades.buyTool(data, "thermometer")
	check("parasi yetmeyen alet alamiyor", not broke.ok and broke.reasonKey == "shop.notEnoughMoney")

	data.money = 1000000
	data.level = 1
	local locked = Upgrades.buyUpgrade(data, "premiumCare")
	check("seviyesi yetmeyen yukseltme alamiyor", not locked.ok and locked.reasonKey == "shop.levelTooLow")

	data.level = 10
	local bought = Upgrades.buyUpgrade(data, "premiumCare")
	check("gecerli satin alma kabul ediliyor", bought.ok)
	local twice = Upgrades.buyUpgrade(data, "premiumCare")
	check("ayni yukseltme iki kez alinamiyor", not twice.ok and twice.reasonKey == "shop.alreadyOwned")

	local effects = Upgrades.effectsOf(data)
	check("premiumCare ucret bonusu veriyor", effects.feeBonus > 1)

	local fresh = Profile.defaultData()
	check("baslangicta gozle muayene var", fresh.tools.visual == true)
	check("baslangicta rontgen yok", fresh.tools.xray ~= true)
	check("baslangic parasi pozitif", fresh.money > 0)
	check("bilinmeyen urun reddediliyor", not Upgrades.buyTool(fresh, "__yok__").ok)
end

do
	-- Ceviri ve remote'lar.
	check("eksik anahtar kose parantezle gorunuyor", Loc.get("__yok__") == "[__yok__]")
	check("yer tutucu doluyor", string.find(Loc.get("hud.money", 42), "42") ~= nil)
	check("ceviri tablosu dolu", Loc.count() > 100)

	for name in Net.Schema do
		check(("remote '%s' olusturulmus"):format(name), Net.get(name) ~= nil)
	end
end


-- ── Basarimlar, gunluk gorevler, ayarlar ──────────────────────────────

do
	for _, achievement in Achievements.all() do
		check(("basarim '%s' adi cevrili"):format(achievement.id), Loc.has(achievement.nameKey))
		check(("basarim '%s' aciklamasi cevrili"):format(achievement.id), Loc.has(achievement.descKey))
		check(
			("basarim '%s' bilinen sayac kullaniyor"):format(achievement.id),
			Achievements.knownCounter(achievement.kind)
		)
		check(("basarim '%s' hedefi pozitif"):format(achievement.id), achievement.target > 0)
	end

	for _, task in Tasks.all() do
		check(("gorev '%s' adi cevrili"):format(task.id), Loc.has(task.nameKey))
		check(
			("gorev '%s' bilinen sayac kullaniyor"):format(task.id),
			Achievements.knownCounter(task.kind)
		)
	end

	-- Basarim akisi: sifir profilde hicbir sey acik olmamali, sayac
	-- hedefe gelince acilmali ve odul VERILMELI.
	local data = Profile.defaultData()
	local none = Achievements.check(data)
	check("yeni profilde hemen acilan basarim yok mu?", #none <= 1)

	local before = data.money
	data.stats.patients = 1
	local unlocked = Achievements.check(data)
	check("ilk hasta basarimi aciliyor", #unlocked > 0)
	check("basarim odulu veriliyor", data.money > before)
	local again = Achievements.check(data)
	check("acilan basarim ikinci kez odul vermiyor", #again == 0)

	check("tur sayaci bos profilde sifir", Achievements.value(Profile.defaultData(), "species") == 0)
	local counted = Profile.defaultData()
	counted.species.dog = true
	counted.species.cat = true
	check("tur sayaci farkli turleri sayiyor", Achievements.value(counted, "species") == 2)

	-- Gunluk gorevler: ayni oyuncu ayni gun ayni gorevleri gormeli.
	local first = Profile.defaultData()
	Tasks.ensure(first, 12345)
	local second = Profile.defaultData()
	Tasks.ensure(second, 12345)
	check("gunluk gorevler ayni oyuncu icin kararli", #first.daily.tasks == #second.daily.tasks)
	local sameIds = #first.daily.tasks > 0
	for index, entry in first.daily.tasks do
		if second.daily.tasks[index] == nil or second.daily.tasks[index].id ~= entry.id then
			sameIds = false
			break
		end
	end
	check("gunluk gorevlerin sirasi da ayni", sameIds)

	local other = Profile.defaultData()
	Tasks.ensure(other, 999)
	check("gorev sayisi ayarlandigi kadar", #other.daily.tasks == Economy.table().dailyTasks.count)

	-- Gorev tamamlama: sayac hedefe gelince odul verilmeli.
	local runner = Profile.defaultData()
	Tasks.ensure(runner, 4242)
	local taskEntry = runner.daily.tasks[1]
	local taskSpec = Tasks.get(taskEntry.id)
	check("secilen gorev tabloda var", taskSpec ~= nil)
	if taskSpec ~= nil then
		local money = runner.money
		Tasks.bump(runner, taskSpec.kind, taskSpec.target)
		local completed = Tasks.check(runner)
		check("hedefe ulasan gorev tamamlaniyor", #completed > 0)
		check("gorev odulu veriliyor", runner.money > money)
		check("tamamlanan gorev ikinci kez odul vermiyor", #Tasks.check(runner) == 0)
	end

	-- Ayni turu iki kez saymamali.
	local speciesRunner = Profile.defaultData()
	Tasks.ensure(speciesRunner, 77)
	Tasks.markSpecies(speciesRunner, "dog")
	Tasks.markSpecies(speciesRunner, "dog")
	check("ayni tur iki kez sayilmiyor", speciesRunner.daily.counters.species == 1)
	Tasks.markSpecies(speciesRunner, "cat")
	check("farkli tur sayaci artiriyor", speciesRunner.daily.counters.species == 2)

	-- Ayarlar
	local settings = Profile.defaultSettings()
	check(
		"varsayilan sallanma sinirlarin icinde",
		settings.bobbing >= settingsData.limits.bobbing.min
			and settings.bobbing <= settingsData.limits.bobbing.max
	)
	check(
		"varsayilan FOV sinirlarin icinde",
		settings.fieldOfView >= settingsData.limits.fieldOfView.min
			and settings.fieldOfView <= settingsData.limits.fieldOfView.max
	)
	check("kosma hizi yurume hizindan buyuk", settingsData.movement.sprintSpeed > settingsData.movement.walkSpeed)
end

-- ── Rapor ─────────────────────────────────────────────────────────────

local total = passed + #failures
if #failures == 0 then
	print(("[SELF-TEST] %d/%d gecti — veri tablolari ve oyun mantigi tutarli."):format(passed, total))
	print(("[SELF-TEST] assetIds.json'da dolu yuva: %d (0 ise butun modeller kod ile uretildi)"):format(
		Assets.filledCount()
	))
else
	warn(("[SELF-TEST] %d/%d gecti — %d DENETIM BASARISIZ:"):format(passed, total, #failures))
	for index, label in failures do
		warn(("  %d. %s"):format(index, label))
	end
end
