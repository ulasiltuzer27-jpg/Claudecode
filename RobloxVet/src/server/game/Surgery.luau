--!strict
-- Surgery.luau
-- ============
-- Ameliyat mini-oyunu. ADIM PROGRAMI SUNUCUDA URETILIYOR, TIKLAMALAR
-- SUNUCUDA DOGRULANIYOR. Istemci yalnizca cizer.
--
-- Saat senkronu
-- -------------
-- Zamanlama `workspace:GetServerTimeNow()` uzerinden yuruyor: bu saat
-- istemci ve sunucuda SENKRON. Istemci kendi saatini kullansa gecikme
-- kadar kayardi. Yine de tiklamanin sunucuya ulasmasi zaman aliyor;
-- pencereler (perfectWindow 0.18 sn, goodWindow 0.34 sn) bunu
-- karsilayacak kadar genis tutuldu. Pencereyi gecikme kadar ONCEYE
-- kaydirmak istemcinin bildirdigi bir sayiya guvenmek olurdu - bu
-- yuzden yapilmiyor, comert davranmak tercih edildi.

local Workspace = game:GetService("Workspace")

local Treatment = require(script.Parent.Treatment)

local Surgery = {}

export type Session = {
	player: Player,
	patientId: number,
	startTime: number,
	stepSeconds: number,
	stepCount: number,
	resolved: { [number]: string },
	score: number,
	threshold: number,
	done: boolean,
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

--- Adim `index` icin hedef zaman (senkron sunucu saati).
local function targetTime(session: Session, index: number): number
	return session.startTime + index * session.stepSeconds
end

--- Ameliyati baslatir. Istemciye yollanacak program tablosunu doner.
--- `onFinish(passed, score)` butun adimlar bitince cagrilir.
function Surgery.begin(
	player: Player,
	patientId: number,
	surgeryTolerance: number,
	onFinish: (passed: boolean, score: number) -> ()
): any
	local config = Treatment.surgeryConfig

	local session: Session = {
		player = player,
		patientId = patientId,
		startTime = Workspace:GetServerTimeNow() + 1.5, -- istemcinin arayuzu kurmasi icin pay
		stepSeconds = config.stepSeconds,
		stepCount = config.steps,
		resolved = {},
		score = 0,
		threshold = math.max(0.2, config.minScoreToPass - surgeryTolerance),
		done = false,
	}
	sessions[player] = session

	task.spawn(function()
		for index = 1, session.stepCount do
			local deadline = targetTime(session, index) + config.goodWindow
			while Workspace:GetServerTimeNow() < deadline do
				if session.done or sessions[player] ~= session then
					return
				end
				task.wait(0.05)
			end
			if session.resolved[index] == nil then
				-- Hic tiklanmadi: kacirildi.
				session.resolved[index] = "miss"
				session.score -= config.missPenalty
			end
		end

		if session.done or sessions[player] ~= session then
			return
		end
		session.done = true
		local finalScore = math.clamp(session.score, 0, 1)
		sessions[player] = nil
		onFinish(finalScore >= session.threshold, finalScore)
	end)

	return {
		steps = session.stepCount,
		startTime = session.startTime,
		stepSeconds = session.stepSeconds,
		perfectWindow = config.perfectWindow,
		goodWindow = config.goodWindow,
	}
end

export type InputResult = { accepted: boolean, grade: string, step: number }

--- Istemciden gelen tiklama. Ayni adim iki kez puanlanmaz.
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

	local config = Treatment.surgeryConfig
	local error = math.abs(Workspace:GetServerTimeNow() - targetTime(session, index))

	local grade: string
	if error <= config.perfectWindow then
		grade = "perfect"
		session.score += (1 / session.stepCount) + config.perfectBonus / session.stepCount
	elseif error <= config.goodWindow then
		grade = "good"
		session.score += 0.7 / session.stepCount
	else
		grade = "miss"
		session.score -= config.missPenalty
	end

	session.resolved[index] = grade
	return { accepted = true, grade = grade, step = index }
end

--- Ameliyati iptal eder (oyuncu ayrildi, hasta kayboldu).
function Surgery.cancel(player: Player)
	local session = sessions[player]
	if session ~= nil then
		session.done = true
		sessions[player] = nil
	end
end

return Surgery
