--!strict
-- Ward.luau
-- =========
-- Kogus: ameliyat ve acil vaka sonrasi YATAN hasta.
--
-- Neden var?
-- ----------
-- Kogus odasi ve kafesleri bastan beri duruyordu ama hicbir oynanisi
-- yoktu: bes kutu ve sifir kural. Ustelik kafeslerin ici (4,8 stud) en
-- buyuk hastanin ayak izinden (6,4) kucuktu, yani icine bir sey koymak
-- da mumkun degildi. Kafesler uc gercek padoga donustu ve agir vakalar
-- artik taburcu edilmeden once burada yatiyor.
--
-- Dongu
-- -----
-- Agir vaka (ameliyat ya da acil) tedavisi bitince kapidan cikmiyor,
-- kogusa yatiyor. Belirli araliklarla ILAC saati geliyor; oyuncu
-- padogun yanina gidip dozu veriyor. Zamaninda verilen doz sagligi ve
-- itibari yukseltiyor, kacirilan doz ikisini de dusuruyor. Iyilesme
-- bitince sahibi gelip aliyor ve YATIS UCRETI bakim puaniyla carpilarak
-- odeniyor.
--
-- Neden ayri modul?
-- -----------------
-- PatientFlow zaten 1400 satir. Kogus kendi zamanlayicisi ve kendi
-- yuvalari olan bagimsiz bir defter; rigi hic ellemiyor (yurutme ve
-- silme hala PatientFlow'un isi), yalnizca "hangi yatakta kim var, bir
-- sonraki doz ne zaman" tutuyor. Boylece rig yasam dongusu tek yerde
-- kaliyor.

local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local economy = require(Data.economy)

local Ward = {}

export type Entry = {
	bed: number,
	patientId: number,
	animalId: string,
	nameKey: string,
	vet: Player?,
	recoverUntil: number,
	nextDoseAt: number,
	dosesGiven: number,
	dosesMissed: number,
	dosesTotal: number,
}

local beds: { CFrame } = {}
local occupants: { [number]: Entry } = {}

local config = economy.ward

--- Yatak yerlerini kaydeder. Klinik kurulunca bir kez cagriliyor.
function Ward.start(points: { CFrame })
	beds = points
	table.clear(occupants)
end

function Ward.bedCount(): number
	return #beds
end

function Ward.bedCFrame(index: number): CFrame?
	return beds[index]
end

--- Bos yatak. Yoksa nil - kogus doluyken agir vaka normal taburcu
--- ediliyor (hasta kapida bekletilmiyor).
function Ward.freeBed(): number?
	for index = 1, #beds do
		if occupants[index] == nil then
			return index
		end
	end
	return nil
end

function Ward.entry(index: number): Entry?
	return occupants[index]
end

--- Hastayi kogusa yatirir. Yatak yoksa nil doner.
function Ward.admit(patientId: number, animalId: string, nameKey: string, vet: Player?): Entry?
	local bed = Ward.freeBed()
	if bed == nil then
		return nil
	end
	local now = Workspace:GetServerTimeNow()
	local total = math.max(1, math.floor(config.recoverySeconds / config.doseIntervalSeconds))
	local entry: Entry = {
		bed = bed,
		patientId = patientId,
		animalId = animalId,
		nameKey = nameKey,
		vet = vet,
		recoverUntil = now + config.recoverySeconds,
		nextDoseAt = now + config.doseIntervalSeconds,
		dosesGiven = 0,
		dosesMissed = 0,
		dosesTotal = total,
	}
	occupants[bed] = entry
	return entry
end

function Ward.release(bed: number)
	occupants[bed] = nil
end

--- Oyuncu bu yatagin yanina gelmis mi? Sunucuda denetleniyor: istemcinin
--- "yanindayim" demesine guvenilmiyor (istasyon kuralinin aynisi).
local function nearBed(player: Player, bed: number): boolean
	local cframe = beds[bed]
	if cframe == nil then
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
	local target = Vector3.new(cframe.Position.X, 0, cframe.Position.Z)
	return (flat - target).Magnitude <= config.radius
end

export type DoseResult = { ok: boolean, reasonKey: string, entry: Entry? }

--- Dozu verir. Sunucu hem YAKINLIGI hem ZAMAN PENCERESINI denetliyor.
function Ward.giveDose(player: Player, bed: number): DoseResult
	local entry = occupants[bed]
	if entry == nil then
		return { ok = false, reasonKey = "ward.emptyBed", entry = nil }
	end
	if not nearBed(player, bed) then
		return { ok = false, reasonKey = "notify.tooFar", entry = entry }
	end

	local now = Workspace:GetServerTimeNow()
	-- Doz penceresi: saatinden ONCE de verilebiliyor ama pencerenin
	-- disinda erken verilen doz sayilmiyor. Aksi halde oyuncu butun
	-- dozlari pes pese basip kogusu anlamsizlastirirdi.
	if now < entry.nextDoseAt - config.doseWindowSeconds then
		return { ok = false, reasonKey = "ward.tooEarly", entry = entry }
	end

	entry.dosesGiven += 1
	entry.nextDoseAt = now + config.doseIntervalSeconds
	return { ok = true, reasonKey = "ward.doseGiven", entry = entry }
end

export type Tick = {
	missed: { Entry },
	done: { Entry },
}

--- Zamanlayici. Kacirilan dozlari ve biten yatislari doner; ne
--- yapilacagina PatientFlow karar veriyor (para ve itibar orada).
function Ward.tick(): Tick
	local now = Workspace:GetServerTimeNow()
	local missed: { Entry } = {}
	local done: { Entry } = {}

	for bed, entry in occupants do
		if now > entry.nextDoseAt + config.doseWindowSeconds then
			entry.dosesMissed += 1
			entry.nextDoseAt = now + config.doseIntervalSeconds
			table.insert(missed, entry)
		end
		if now >= entry.recoverUntil then
			table.insert(done, entry)
			occupants[bed] = nil
		end
	end

	return { missed = missed, done = done }
end

--- Bakim puani 0..1: verilen dozun beklenen doza orani, kacirilan doz
--- cezasiyla. Yatis ucreti bununla carpiliyor.
function Ward.careScore(entry: Entry): number
	local expected = math.max(1, entry.dosesGiven + entry.dosesMissed)
	return math.clamp(entry.dosesGiven / expected, 0, 1)
end

--- Istemciye yollanan goruntu: kogus karti bunu ciziyor.
---
--- Zamanlar MUTLAK ve SENKRON sunucu saatinde (`GetServerTimeNow`).
--- Kalan sure yollansaydi istemci onu ancak bir sonraki guncellemede
--- tazeleyebilirdi; mutlak zamanla istemci geri sayimi kendi ciziyor ve
--- sunucu yalnizca DEGISIKLIKTE gonderiyor.
function Ward.snapshot(): any
	local list = {}
	for bed = 1, #beds do
		local entry = occupants[bed]
		if entry == nil then
			table.insert(list, { bed = bed, busy = false })
			continue
		end
		table.insert(list, {
			bed = bed,
			busy = true,
			nameKey = entry.nameKey,
			doseAt = entry.nextDoseAt,
			doseWindow = config.doseWindowSeconds,
			recoverAt = entry.recoverUntil,
			recoverTotal = config.recoverySeconds,
			dosesGiven = entry.dosesGiven,
			dosesMissed = entry.dosesMissed,
			dosesTotal = entry.dosesTotal,
			mine = entry.vet ~= nil and entry.vet.Parent ~= nil,
		})
	end
	return { beds = list, total = #beds }
end

--- Oyuncu ayrildi: yatan hastasi koguste kaliyor ama sahibi yok sayiliyor
--- (ucreti kimse alamaz, hasta yine de iyilesip gidiyor).
function Ward.playerLeft(player: Player)
	for _, entry in occupants do
		if entry.vet == player then
			entry.vet = nil
		end
	end
end

--- Dolu yatak sayisi (SelfTest ve HUD icin).
function Ward.occupied(): number
	local total = 0
	for _ in occupants do
		total += 1
	end
	return total
end

return Ward
