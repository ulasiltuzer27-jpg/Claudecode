--!strict
-- ToolGamePanel.luau
-- ==================
-- Alet mini-oyunlarinin arayuzu. YALNIZCA CIZER: hedef de, sonuc da
-- sunucuda (src/server/game/ToolGame.luau).
--
-- Uc mini-oyun, tek panel
-- -----------------------
-- Uc ayri panel kurmak uc ayri acilis/kapanis/geri sayim koduna mal
-- olurdu. Cerceve, baslik, sure cubugu ve kapanis ortak; yalnizca
-- ortadaki "sahne" degisiyor.
--
-- Neyi biliyoruz?
-- ---------------
-- Rontgende kirigin YERI geliyor (cizmek icin gerekli), kan testinde
-- hedef ARALIK geliyor (cizmek icin gerekli) ama ibrenin yeri ZAMANDAN
-- cikiyor ve sunucu kendi saatinde olcuyor. Ultrasonda hedef HIC
-- gelmiyor: yalnizca "ne kadar yakinsin" sayisi.

local RunService = game:GetService("RunService")
local UserInputService = game:GetService("UserInputService")
local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local ToolGamePanel = {}

local widgets: any = nil
local session: any = nil
local connection: RBXScriptConnection? = nil
local lastProbe = 0

local STAGE = Vector2.new(420, 260)
local PROBE_INTERVAL = 1 / 10

--- Sahnedeki normalize (0..1) konumu piksel konumuna cevirir.
local function toPixels(x: number, y: number): UDim2
	return UDim2.fromOffset(x * STAGE.X, y * STAGE.Y)
end

local function clearStage()
	if widgets == nil then
		return
	end
	widgets.stage:ClearAllChildren()
	widgets.marks = {}
end

local function stop()
	if connection ~= nil then
		connection:Disconnect()
		connection = nil
	end
	session = nil
	if widgets ~= nil and widgets.panel.Visible then
		Theme.close(widgets.panel)
	end
end

--- Tiklanan noktanin sahne icindeki normalize konumu.
local function normalizedFromMouse(): (number, number)
	local position = UserInputService:GetMouseLocation()
	local origin = widgets.stage.AbsolutePosition
	local size = widgets.stage.AbsoluteSize
	if size.X <= 0 or size.Y <= 0 then
		return -1, -1
	end
	return (position.X - origin.X) / size.X, (position.Y - origin.Y) / size.Y
end

function ToolGamePanel.create(parent: ScreenGui)
	local panel = Theme.card({
		name = "AletOyunu",
		size = UDim2.fromOffset(STAGE.X + 48, STAGE.Y + 124),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		parent = parent,
		visible = false,
	}, Theme.color.accent)

	local icon = Theme.icon({
		name = "xray",
		size = 18,
		color = Theme.color.accentGlow,
		position = UDim2.fromOffset(Theme.space.lg, 14),
		parent = panel,
		zIndex = 2,
	})

	local title = Theme.label({
		name = "Baslik",
		text = "",
		size = UDim2.new(1, -Theme.space.lg * 2 - 26, 0, 24),
		position = UDim2.fromOffset(Theme.space.lg + 26, 12),
		textSize = Theme.size.title,
		bold = true,
		parent = panel,
		zIndex = 2,
	})

	local hint = Theme.label({
		name = "Ipucu",
		text = "",
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 18),
		position = UDim2.fromOffset(Theme.space.lg, 38),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		parent = panel,
		zIndex = 2,
	})

	local stage = Theme.frame({
		name = "Sahne",
		size = UDim2.fromOffset(STAGE.X, STAGE.Y),
		position = UDim2.new(0.5, 0, 0, 62),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.panelDeep,
		transparency = 0,
		radius = Theme.radius.sm,
		parent = panel,
		zIndex = 2,
	})
	stage.ClipsDescendants = true
	Theme.stroke(stage, Theme.color.strokeSoft, 1)

	local timer = Theme.bar(
		panel,
		UDim2.new(1, -Theme.space.lg * 2, 0, 8),
		UDim2.new(0, Theme.space.lg, 1, -30),
		Theme.color.accent
	)
	timer.track.ZIndex = 2
	timer.fill.ZIndex = 3

	-- Sahnenin tamami tiklanabilir; "tikla" ve "durdur" ayni dugmeyi
	-- kullaniyor, cunku ikisi de tek bir "simdi" niyeti.
	local hit = Instance.new("TextButton")
	hit.Name = "Tik"
	hit.Text = ""
	hit.Size = UDim2.fromScale(1, 1)
	hit.BackgroundTransparency = 1
	hit.BorderSizePixel = 0
	hit.ZIndex = 20
	hit.Parent = stage
	hit.Activated:Connect(function()
		ToolGamePanel.submit()
	end)

	widgets = {
		panel = panel,
		icon = icon,
		title = title,
		hint = hint,
		stage = stage,
		timer = timer,
		marks = {},
	}

	UserInputService.InputBegan:Connect(function(input: InputObject, processed: boolean)
		if processed or session == nil then
			return
		end
		if input.KeyCode == Enum.KeyCode.Space then
			ToolGamePanel.submit()
		end
	end)
end

--- Rontgen sahnesi: kaburga izlenimi veren cizgiler, sahte isaretler ve
--- hedefteki kirik. Hicbiri doku degil - hepsi Frame.
local function buildSpot(payload: any)
	for index = 1, 7 do
		Theme.frame({
			name = "Kaburga" .. index,
			size = UDim2.fromOffset(STAGE.X * 0.52, 5),
			position = UDim2.fromOffset(STAGE.X * 0.24, 34 + index * 26),
			color = Theme.color.text,
			transparency = 0.78,
			radius = 3,
			parent = widgets.stage,
			zIndex = 3,
		})
	end
	Theme.frame({
		name = "Omurga",
		size = UDim2.fromOffset(9, STAGE.Y * 0.78),
		position = UDim2.new(0.5, -4, 0, 26),
		color = Theme.color.text,
		transparency = 0.7,
		radius = 4,
		parent = widgets.stage,
		zIndex = 3,
	})

	-- Sahte isaretler: kirigi ilk bakista ele vermesin.
	for index, spot in payload.decoys do
		Theme.frame({
			name = "Sahte" .. index,
			size = UDim2.fromOffset(10, 10),
			position = toPixels(spot[1], spot[2]),
			anchor = Vector2.new(0.5, 0.5),
			color = Theme.color.text,
			transparency = 0.55,
			radius = 5,
			parent = widgets.stage,
			zIndex = 4,
		})
	end

	-- Kirik: kesisen iki kisa cizgi. Cevresinde halka YOK - halka
	-- koymak "iste burada" demek olurdu.
	local crack = Theme.frame({
		name = "Kirik",
		size = UDim2.fromOffset(26, 26),
		position = toPixels(payload.targetX, payload.targetY),
		anchor = Vector2.new(0.5, 0.5),
		transparency = 1,
		radius = 0,
		parent = widgets.stage,
		zIndex = 5,
	})
	for _, rotation in { 28, -34 } do
		local shard = Theme.frame({
			name = "Catlak",
			size = UDim2.fromOffset(22, 3),
			position = UDim2.fromScale(0.5, 0.5),
			anchor = Vector2.new(0.5, 0.5),
			color = Theme.color.panelDeep,
			transparency = 0,
			radius = 2,
			parent = crack,
			zIndex = 6,
		})
		shard.Rotation = rotation
	end
end

--- Kan testi sahnesi: yatay olcek, hedef aralik ve suzulen ibre.
local function buildRange(payload: any)
	Theme.frame({
		name = "Olcek",
		size = UDim2.fromOffset(STAGE.X - 40, 28),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		color = Theme.color.panel,
		transparency = 0,
		radius = Theme.radius.sm,
		parent = widgets.stage,
		zIndex = 3,
	})

	local width = STAGE.X - 40
	local left = (STAGE.X - width) * 0.5

	Theme.frame({
		name = "Hedef",
		size = UDim2.fromOffset(width * (payload.high - payload.low), 28),
		position = UDim2.fromOffset(left + width * payload.low, STAGE.Y * 0.5 - 14),
		color = Theme.color.good,
		transparency = 0.25,
		radius = Theme.radius.sm,
		parent = widgets.stage,
		zIndex = 4,
	})

	for index = 1, 9 do
		Theme.frame({
			name = "Tik" .. index,
			size = UDim2.fromOffset(1, 12),
			position = UDim2.fromOffset(left + width * index / 10, STAGE.Y * 0.5 + 20),
			color = Theme.color.strokeSoft,
			transparency = 0.3,
			radius = 0,
			parent = widgets.stage,
			zIndex = 5,
		})
	end

	widgets.marks.needle = Theme.frame({
		name = "Ibre",
		size = UDim2.fromOffset(5, 48),
		position = UDim2.fromOffset(left, STAGE.Y * 0.5 - 24),
		color = Theme.color.text,
		transparency = 0,
		radius = 3,
		parent = widgets.stage,
		zIndex = 6,
	})
	widgets.marks.rangeLeft = left
	widgets.marks.rangeWidth = width
end

--- Ultrason sahnesi: karanlik bir alan ve prob. Hedef CIZILMIYOR -
--- sunucu nerede oldugunu soylemiyor. Oyuncunun tek bilgisi probun
--- rengi ve halkasi (sicak/soguk).
local function buildProbe()
	for index = 1, 6 do
		Theme.frame({
			name = "Doku" .. index,
			size = UDim2.fromOffset(STAGE.X * 0.9, 2),
			position = UDim2.fromOffset(STAGE.X * 0.05, index * (STAGE.Y / 7)),
			color = Theme.color.text,
			transparency = 0.88,
			radius = 1,
			parent = widgets.stage,
			zIndex = 3,
		})
	end

	local probe = Theme.frame({
		name = "Prob",
		size = UDim2.fromOffset(34, 34),
		position = UDim2.fromScale(0.5, 0.5),
		anchor = Vector2.new(0.5, 0.5),
		color = Theme.color.accent,
		transparency = 0.4,
		radius = 17,
		parent = widgets.stage,
		zIndex = 6,
	})
	local stroke = Theme.stroke(probe, Theme.color.accentGlow, 2)

	widgets.marks.probe = probe
	widgets.marks.probeStroke = stroke
	widgets.marks.heat = 0
end

--- Sunucu mini-oyunu baslatti.
function ToolGamePanel.begin(payload: any)
	if widgets == nil or payload == nil then
		return
	end
	stop()
	session = payload
	clearStage()

	widgets.title.Text = Loc.get("toolgame." .. payload.kind)
	widgets.hint.Text = Loc.get("toolgame." .. payload.kind .. ".hint")
	widgets.icon:Destroy()
	widgets.icon = Theme.icon({
		name = payload.toolId,
		size = 18,
		color = Theme.color.accentGlow,
		position = UDim2.fromOffset(Theme.space.lg, 14),
		parent = widgets.panel,
		zIndex = 2,
	})

	if payload.kind == "spot" then
		buildSpot(payload)
	elseif payload.kind == "range" then
		buildRange(payload)
	else
		buildProbe()
	end

	Theme.open(widgets.panel)
	Audio.play("uiOpen")

	connection = RunService.RenderStepped:Connect(function()
		if session == nil then
			return
		end
		local now = Workspace:GetServerTimeNow()
		local left = math.max(0, session.startTime + session.seconds - now)
		Theme.setBar(widgets.timer, left / session.seconds, true)
		Theme.setBarColor(widgets.timer, Theme.statusColor(left / session.seconds))

		if session.kind == "range" and widgets.marks.needle ~= nil then
			-- Ibrenin yeri sunucudakiyle AYNI formulden cikiyor: ucgen
			-- dalga, senkron sunucu saati. Kendi saatimizi kullansak
			-- ekranda gordugumuz yer ile sunucunun olctugu yer ayrilirdi.
			local sweep = session.sweepSeconds
			local cycle = ((now - session.startTime) % (sweep * 2)) / sweep
			local position = if cycle <= 1 then cycle else 2 - cycle
			widgets.marks.needle.Position = UDim2.fromOffset(
				widgets.marks.rangeLeft + widgets.marks.rangeWidth * position - 2,
				STAGE.Y * 0.5 - 24
			)
			widgets.marks.needle.BackgroundColor3 = if position >= session.low and position <= session.high
				then Theme.color.good
				else Theme.color.text
		elseif session.kind == "probe" and widgets.marks.probe ~= nil then
			local x, y = normalizedFromMouse()
			if x >= 0 and x <= 1 and y >= 0 and y <= 1 then
				widgets.marks.probe.Position = toPixels(x, y)
				if now - lastProbe >= PROBE_INTERVAL then
					lastProbe = now
					Net.toServer("ToolGameProbe", x, y)
				end
			end
		end
	end)
end

--- Sunucudan gelen sicak/soguk geri bildirimi.
function ToolGamePanel.hint(heat: number)
	if widgets == nil or session == nil or widgets.marks.probe == nil then
		return
	end
	widgets.marks.heat = heat
	-- Soguk = mavi ve kucuk, sicak = kirmizi ve buyuk. Iki kanal
	-- (renk + boyut) birden kullaniyoruz: renk korlugunde de okunsun.
	local color = Theme.color.xp:Lerp(Theme.color.bad, heat)
	Theme.tween(widgets.marks.probe, {
		BackgroundColor3 = color,
		Size = UDim2.fromOffset(34 + heat * 26, 34 + heat * 26),
		BackgroundTransparency = 0.55 - heat * 0.35,
	}, Theme.motion.quick)
	Theme.tween(widgets.marks.probeStroke, { Color = color }, Theme.motion.quick)
end

--- Oyuncunun cevabi. Konum sunucuda dogrulaniyor; kan testinde konum
--- yok sayilip sunucunun kendi saati kullaniliyor.
function ToolGamePanel.submit()
	if session == nil or widgets == nil then
		return
	end
	local x, y = normalizedFromMouse()
	if session.kind == "range" then
		x, y = 0, 0
	elseif x < 0 or x > 1 or y < 0 or y > 1 then
		return
	end
	Audio.play("uiClick")
	Net.toServer("ToolGameSubmit", x, y)
end

--- Sunucu mini-oyunu bitirdi.
function ToolGamePanel.finish(ok: boolean, _reasonKey: string)
	Audio.play(if ok then "diagnosisCorrect" else "uiDeny")
	stop()
end

function ToolGamePanel.isOpen(): boolean
	return session ~= nil
end

return ToolGamePanel
