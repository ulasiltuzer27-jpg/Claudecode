--!strict
-- ToolGame.luau
-- =============
-- Alet mini-oyunlari: rontgende kirigi bulma, kan testinde ibreyi
-- durdurma, ultrasonda hedefi tarama.
--
-- `Surgery.luau` ile AYNI KALIP: oturum sunucuda acilir, hedef sunucuda
-- uretilir, sonuc sunucuda dogrulanir, istemci yalnizca cizer ve niyet
-- yollar. Sure dolarsa oturum sunucuda kapanir - istemcinin "bitti"
-- demesi beklenmiyor.
--
-- Neyi gizliyoruz, neyi gizlemiyoruz?
-- -----------------------------------
-- Hicbir mini-oyunda HASTALIGIN KIMLIGI istemciye gitmiyor; oyunun
-- sirri budur ve korunuyor.
--
-- Rontgende kirigin EKRANDAKI YERI istemciye gidiyor, cunku istemcinin
-- onu cizmesi gerekiyor: ekranda gorunmeyen bir seyi tiklatmak mumkun
-- degil. Yetki kaybi degil, cizim gerekliligi - ve tiklamanin menzili
-- ile suresi yine sunucuda olculuyor.
--
-- Kan testinde ibrenin yeri ZAMANDAN cikiyor (senkron sunucu saati),
-- tipki ameliyattaki gibi: istemci "su konumda durdurdum" demiyor,
-- sunucu tuşa BASILDIGI ANI kendi saatinde okuyor.
--
-- Ultrasonda hedef istemciye HIC gitmiyor: istemci prob konumunu
-- yolluyor, sunucu yalnizca "ne kadar yakinsin" sayisini donuyor.

local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Data = ReplicatedStorage:WaitForChild("VetData")

local toolData = require(Data.tools)

local ToolGame = {}

export type Session = {
	player: Player,
	patientId: number,
	toolId: string,
	kind: string,
	targetX: number,
	targetY: number,
	rangeLow: number,
	rangeHigh: number,
	startTime: number,
	deadline: number,
	done: boolean,
	onFinish: (ok: boolean, reasonKey: string) -> (),
}

local sessions: { [Player]: Session } = {}

local config = toolData.minigames

--- Bu oyuncunun acik bir mini-oyunu var mi?
function ToolGame.active(player: Player): Session?
	local session = sessions[player]
	if session ~= nil and not session.done then
		return session
	end
	return nil
end

--- Bir aletin mini-oyun turu ("" ise yok).
function ToolGame.kindOf(toolId: string): string
	for _, tool in toolData.tools do
		if tool.id == toolId then
			return tool.minigame or ""
		end
	end
	return ""
end

--- Bilinen mini-oyun turleri (SelfTest ve verify_data icin).
function ToolGame.kinds(): { string }
	local kinds = {}
	for kind in config do
		table.insert(kinds, kind)
	end
	table.sort(kinds)
	return kinds
end

local function finish(session: Session, ok: boolean, reasonKey: string)
	if session.done then
		return
	end
	session.done = true
	if sessions[session.player] == session then
		sessions[session.player] = nil
	end
	session.onFinish(ok, reasonKey)
end

--- Kan testi ibresinin o andaki konumu (0..1). Ucgen dalga: saga gidip
--- sola donuyor, yani ibre ekranda ileri geri suzuluyor.
local function needleAt(session: Session, now: number): number
	local sweep = config.range.sweepSeconds
	local cycle = ((now - session.startTime) % (sweep * 2)) / sweep
	return if cycle <= 1 then cycle else 2 - cycle
end

--- Mini-oyunu baslatir; istemciye yollanacak tabloyu doner.
--- `onFinish(ok, reasonKey)` basari, basarisizlik ya da sure dolunca
--- TAM BIR KEZ cagriliyor.
function ToolGame.begin(
	player: Player,
	patientId: number,
	toolId: string,
	kind: string,
	onFinish: (ok: boolean, reasonKey: string) -> ()
): any?
	local settings = config[kind]
	if settings == nil then
		return nil
	end
	ToolGame.cancel(player)

	local now = Workspace:GetServerTimeNow()
	local session: Session = {
		player = player,
		patientId = patientId,
		toolId = toolId,
		kind = kind,
		-- Hedef kenarlardan uzak duruyor: kosede beliren bir hedef,
		-- beceriden cok sansa donuyordu.
		targetX = 0.18 + math.random() * 0.64,
		targetY = 0.18 + math.random() * 0.64,
		rangeLow = 0,
		rangeHigh = 0,
		startTime = now + 0.6, -- istemcinin paneli kurmasi icin pay
		deadline = now + 0.6 + settings.seconds,
		done = false,
		onFinish = onFinish,
	}

	if kind == "range" then
		local width = config.range.width
		session.rangeLow = 0.08 + math.random() * (0.84 - width)
		session.rangeHigh = session.rangeLow + width
	end

	sessions[player] = session

	-- Sure dolunca oturum SUNUCUDA kapaniyor: istemcinin "bitti"
	-- demesini beklemek, hic bitirmeyen bir istemciye sonsuz sure
	-- vermek olurdu.
	task.spawn(function()
		while Workspace:GetServerTimeNow() < session.deadline do
			if session.done or sessions[player] ~= session then
				return
			end
			task.wait(0.15)
		end
		finish(session, false, "toolgame.timeout")
	end)

	local payload: any = {
		toolId = toolId,
		kind = kind,
		seconds = settings.seconds,
		startTime = session.startTime,
	}
	if kind == "spot" then
		-- Kirigin yeri istemciye gidiyor: cizilmesi gerekiyor.
		-- Sahte isaretler de sunucuda uretiliyor ki her oyuncuda ayni
		-- goruntu cikmasin.
		payload.targetX = session.targetX
		payload.targetY = session.targetY
		payload.radius = settings.radius
		local decoys = {}
		for _ = 1, settings.decoys do
			table.insert(decoys, { math.random(), math.random() })
		end
		payload.decoys = decoys
	elseif kind == "range" then
		payload.low = session.rangeLow
		payload.high = session.rangeHigh
		payload.sweepSeconds = config.range.sweepSeconds
	elseif kind == "probe" then
		payload.radius = settings.radius
	end
	return payload
end

--- Ultrason probu: "ne kadar yakinsin" (0 = uzak, 1 = tam ustunde).
--- Hedefin kendisi DONMUYOR.
function ToolGame.probe(player: Player, x: number, y: number): number
	local session = ToolGame.active(player)
	if session == nil or session.kind ~= "probe" then
		return 0
	end
	if x ~= x or y ~= y then
		return 0 -- NaN
	end
	local dx = math.clamp(x, 0, 1) - session.targetX
	local dy = math.clamp(y, 0, 1) - session.targetY
	local distance = math.sqrt(dx * dx + dy * dy)
	-- 0,55 stud'luk normalize mesafe "tamamen soguk" sayiliyor: daha
	-- genis bir olcek her yerde ilik gosterip aramayi anlamsiz kiliyor.
	return math.clamp(1 - distance / 0.55, 0, 1)
end

--- Oyuncunun cevabi. Her mini-oyun kendi olcutuyle dogrulaniyor.
function ToolGame.submit(player: Player, x: number, y: number): boolean
	local session = ToolGame.active(player)
	if session == nil then
		return false
	end
	if x ~= x or y ~= y then
		finish(session, false, "toolgame.failed")
		return false
	end

	local now = Workspace:GetServerTimeNow()
	if now > session.deadline then
		finish(session, false, "toolgame.timeout")
		return false
	end
	if now < session.startTime then
		return false -- panel daha acilmadi; erken gelen giris yok sayiliyor
	end

	local settings = config[session.kind]
	if session.kind == "range" then
		-- Ibrenin yeri ZAMANDAN cikiyor; istemcinin bildirdigi konum
		-- kullanilmiyor.
		local position = needleAt(session, now)
		local ok = position >= session.rangeLow and position <= session.rangeHigh
		finish(session, ok, if ok then "toolgame.success" else "toolgame.failed")
		return ok
	end

	local dx = math.clamp(x, 0, 1) - session.targetX
	local dy = math.clamp(y, 0, 1) - session.targetY
	local ok = math.sqrt(dx * dx + dy * dy) <= settings.radius
	finish(session, ok, if ok then "toolgame.success" else "toolgame.failed")
	return ok
end

--- Oturumu iptal eder (oyuncu ayrildi, hasta kayboldu, yeni alet).
--- `onFinish` BASARISIZLIKLA cagriliyor: cagrilmadan birakmak,
--- PatientFlow'da aleti sonsuza kadar mesgul birakirdi.
function ToolGame.cancel(player: Player)
	local session = sessions[player]
	if session ~= nil and not session.done then
		finish(session, false, "toolgame.cancelled")
	end
	sessions[player] = nil
end

return ToolGame
