--!strict
-- Surgery.luau
-- ============
-- Ameliyat mini-oyunlari. ADIM PROGRAMI SUNUCUDA URETILIYOR, TUSLAR
-- SUNUCUDA DOGRULANIYOR. Istemci yalnizca cizer.
--
-- Dort tur, tek cekirdek
-- ----------------------
-- `rhythm` (genel), `suture` (dikis), `bleed` (kanama), `align`
-- (kirik hizalama). Hepsi ayni zamanlama cekirdegini kullaniyor ama
-- adim sayisi, ritmi ve isaretcinin HAREKETI farkli: 7 hizli dikis ile
-- 3 yavas hizalama oynanista birbirine benzemiyor. Dort ayri motor
-- yazmak, ayni dogrulama mantigini dort kez tekrarlamak olurdu - ve
-- hatasi da dort yerde duzeltilmek zorunda kalirdi.
--
-- Saat senkronu
-- -------------
-- Zamanlama `workspace:GetServerTimeNow()` uzerinden yuruyor: bu saat
-- istemci ve sunucuda SENKRON. Istemci kendi saatini kullansa gecikme
-- kadar kayardi. Yine de tusun sunucuya ulasmasi zaman aliyor;
-- pencereler bunu karsilayacak kadar genis tutuldu. Pencereyi gecikme
-- kadar ONCEYE kaydirmak istemcinin bildirdigi bir sayiya guvenmek
-- olurdu - bu yuzden yapilmiyor, comert davranmak tercih edildi.
--
-- ASISTANLIK
-- ----------
-- Ameliyathanedeki ikinci oyuncu Bosluk'a basili tutarak KARARLILIK
-- cubugunu yukarida tutuyor. Kararlilik yuksekken cerrahin pencereleri
-- geniliyor. Asistan bosuna beklemiyor: bitince XP aliyor; ucret
-- ameliyati yapanin kaliyor.

local Workspace = game:GetService("Workspace")

local Treatment = require(script.Parent.Treatment)

local Surgery = {}

export type Session = {
	player: Player,
	patientId: number,
	kind: string,
	startTime: number,
	stepSeconds: number,
	stepCount: number,
	resolved: { [number]: string },
	score: number,
	threshold: number,
	done: boolean,
	-- Asistan ve kararlilik (0..1).
	assistant: Player?,
	stability: number,
	lastPulse: number,
}

local sessions: { [Player]: Session } = {}

--- Bu oyuncunun acik bir ameliyati var mi?
function Surgery.active(player: Player): Session?
	local session = sessions[player]
	if session ~= nil and not session.done then
		return session
	end
	return nil
end

--- Bu oyuncunun asistanlik ettigi ameliyat.
function Surgery.assisting(player: Player): Session?
	for _, session in sessions do
		if session.assistant == player and not session.done then
			return session
		end
	end
	return nil
end

--- Tanimli ameliyat turleri (SelfTest ve verify_data icin).
function Surgery.kinds(): { string }
	local kinds = {}
	for kind in Treatment.surgeryTypes do
		table.insert(kinds, kind)
	end
	table.sort(kinds)
	return kinds
end

local function configFor(kind: string): any
	return Treatment.surgeryTypes[kind] or Treatment.surgeryTypes.rhythm
end

--- Adim `index` icin hedef zaman (senkron sunucu saati).
local function targetTime(session: Session, index: number): number
	return session.startTime + index * session.stepSeconds
end

--- Kararlilik zamanla dusuyor; asistan bastikca yukseliyor.
local function stabilityNow(session: Session, now: number): number
	if session.assistant == nil then
		return 0
	end
	local assist = Treatment.assistConfig
	local decayed = session.stability - (now - session.lastPulse) * assist.decayPerSecond
	return math.clamp(decayed, 0, 1)
end

--- Asistan varken pencereler geniliyor. Carpani burada tek yerde
--- hesapliyoruz ki istemci ile sunucu ayni sayiyi kullansin.
local function windowScale(session: Session, now: number): number
	return 1 + stabilityNow(session, now) * Treatment.assistConfig.windowBonus
end

--- Ameliyati baslatir. Istemciye yollanacak program tablosunu doner.
--- `onFinish(passed, score, assistant)` butun adimlar bitince cagrilir.
function Surgery.begin(
	player: Player,
	patientId: number,
	surgeryTolerance: number,
	kind: string,
	onFinish: (passed: boolean, score: number, assistant: Player?) -> ()
): any
	local config = configFor(kind)

	local session: Session = {
		player = player,
		patientId = patientId,
		kind = kind,
		startTime = Workspace:GetServerTimeNow() + 1.5, -- istemcinin arayuzu kurmasi icin pay
		stepSeconds = config.stepSeconds,
		stepCount = config.steps,
		resolved = {},
		score = 0,
		threshold = math.max(0.2, config.minScoreToPass - surgeryTolerance),
		done = false,
		assistant = nil,
		stability = 0,
		lastPulse = 0,
	}
	sessions[player] = session

	task.spawn(function()
		for index = 1, session.stepCount do
			local deadline = targetTime(session, index) + config.goodWindow * 2
			while Workspace:GetServerTimeNow() < deadline do
				if session.done or sessions[player] ~= session then
					return
				end
				task.wait(0.05)
			end
			if session.resolved[index] == nil then
				-- Hic basilmadi: kacirildi.
				session.resolved[index] = "miss"
				session.score -= config.missPenalty
			end
		end

		if session.done or sessions[player] ~= session then
			return
		end
		session.done = true
		local finalScore = math.clamp(session.score, 0, 1)
		local assistant = session.assistant
		sessions[player] = nil
		onFinish(finalScore >= session.threshold, finalScore, assistant)
	end)

	return {
		kind = kind,
		motion = config.motion,
		nameKey = config.nameKey,
		hintKey = config.hintKey,
		steps = session.stepCount,
		startTime = session.startTime,
		stepSeconds = session.stepSeconds,
		perfectWindow = config.perfectWindow,
		goodWindow = config.goodWindow,
	}
end

export type InputResult = { accepted: boolean, grade: string, step: number }

--- Istemciden gelen tus. Ayni adim iki kez puanlanmaz.
function Surgery.input(player: Player, index: number): InputResult
	local session = Surgery.active(player)
	if session == nil then
		return { accepted = false, grade = "none", step = 0 }
	end
	if index < 1 or index > session.stepCount or index % 1 ~= 0 then
		return { accepted = false, grade = "none", step = 0 }
	end
	if session.resolved[index] ~= nil then
		return { accepted = false, grade = "none", step = index }
	end

	local config = configFor(session.kind)
	local now = Workspace:GetServerTimeNow()
	-- Asistanin kararliligi pencereleri genisletiyor.
	local scale = windowScale(session, now)
	local error = math.abs(now - targetTime(session, index))

	local grade: string
	if error <= config.perfectWindow * scale then
		grade = "perfect"
		session.score += (1 / session.stepCount) + config.perfectBonus / session.stepCount
	elseif error <= config.goodWindow * scale then
		grade = "good"
		session.score += 0.7 / session.stepCount
	else
		grade = "miss"
		session.score -= config.missPenalty
	end

	session.resolved[index] = grade
	return { accepted = true, grade = grade, step = index }
end

export type AssistResult = { ok: boolean, session: Session?, stability: number }

--- Asistanin darbesi. Ilk darbe ayni zamanda asistanligi USTLENIYOR.
--- Yakinlik denetimi PatientFlow'da (istasyon konumu orada).
function Surgery.assistPulse(player: Player, surgeon: Player): AssistResult
	local session = sessions[surgeon]
	if session == nil or session.done then
		return { ok = false, session = nil, stability = 0 }
	end
	if session.player == player then
		return { ok = false, session = session, stability = session.stability }
	end
	if session.assistant ~= nil and session.assistant ~= player then
		return { ok = false, session = session, stability = session.stability }
	end

	local now = Workspace:GetServerTimeNow()
	if session.assistant == nil then
		session.assistant = player
		session.stability = 0
		session.lastPulse = now
	end
	session.stability = math.clamp(
		stabilityNow(session, now) + Treatment.assistConfig.pulseGain,
		0,
		1
	)
	session.lastPulse = now
	return { ok = true, session = session, stability = session.stability }
end

--- Asistanligi birakir.
function Surgery.dropAssist(player: Player)
	for _, session in sessions do
		if session.assistant == player then
			session.assistant = nil
			session.stability = 0
		end
	end
end

--- Istemciye yollanan asistan goruntusu.
function Surgery.assistSnapshot(session: Session): any
	local now = Workspace:GetServerTimeNow()
	return {
		active = true,
		surgeon = session.player.Name,
		stability = stabilityNow(session, now),
		windowScale = windowScale(session, now),
	}
end

--- Acik ameliyati olan oyuncular (PatientFlow asistan adaylarini
--- bulmak icin kullaniyor).
function Surgery.surgeons(): { Player }
	local list = {}
	for player, session in sessions do
		if not session.done and player.Parent ~= nil then
			table.insert(list, player)
		end
	end
	return list
end

--- Ameliyati iptal eder (oyuncu ayrildi, hasta kayboldu).
function Surgery.cancel(player: Player)
	local session = sessions[player]
	if session ~= nil then
		session.done = true
		sessions[player] = nil
	end
	Surgery.dropAssist(player)
end


return Surgery
