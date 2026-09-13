--!strict
-- SurgeryPanel.luau
-- =================
-- Ameliyat mini-oyununun arayuzu. YALNIZCA CIZER: puanlama sunucuda.
--
-- Zamanlama
-- ---------
-- Sunucu adimlarin hedef zamanini SENKRON sunucu saatine gore yolluyor
-- (`workspace:GetServerTimeNow()`). Istemci ayni saati okudugu icin
-- isaretci sunucunun bekledigi anda tam ortadan geciyor; kendi
-- saatimizi kullansak gecikme kadar kayardi.

local RunService = game:GetService("RunService")
local UserInputService = game:GetService("UserInputService")
local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local SurgeryPanel = {}

local widgets: any = nil
local schedule: any = nil
local connection: RBXScriptConnection? = nil
local sentSteps: { [number]: boolean } = {}
-- Sunucunun notu hangi adima ait oldugunu soylemiyor (tek bir dize
-- geliyor); en son gonderilen adimi burada tutup noktayi ona gore
-- boyuyoruz. Adimlar sirayla gittigi icin bu esleme guvenli.
local lastPressed = 0

local TRACK_WIDTH = 440

local function currentStep(now: number): number
	if schedule == nil then
		return 0
	end
	local raw = (now - schedule.startTime) / schedule.stepSeconds
	return math.clamp(math.floor(raw + 0.5), 1, schedule.steps)
end

local function stop()
	if connection ~= nil then
		connection:Disconnect()
		connection = nil
	end
	schedule = nil
	lastPressed = 0
	table.clear(sentSteps)
	if widgets ~= nil and widgets.panel.Visible then
		Theme.close(widgets.panel)
	end
end

local function press()
	if schedule == nil then
		return
	end
	local step = currentStep(Workspace:GetServerTimeNow())
	if step < 1 or sentSteps[step] then
		return
	end
	sentSteps[step] = true
	lastPressed = step
	Net.toServer("SurgeryInput", step)
end

function SurgeryPanel.create(parent: ScreenGui)
	local panel = Theme.card({
		name = "AmeliyatPaneli",
		size = UDim2.fromOffset(TRACK_WIDTH + 56, 196),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		parent = parent,
		visible = false,
	}, Theme.color.accent)

	Theme.icon({
		name = "surgery",
		size = 18,
		color = Theme.color.accentGlow,
		position = UDim2.new(0.5, -78, 0, 12),
		parent = panel,
		zIndex = 2,
	})
	Theme.label({
		text = Loc.get("surgery.title"),
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 24),
		position = UDim2.fromOffset(Theme.space.lg, 10),
		textSize = Theme.size.title,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
		zIndex = 2,
	})
	local hint = Theme.label({
		name = "Ipucu",
		text = Loc.get("surgery.hint"),
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 18),
		position = UDim2.fromOffset(Theme.space.lg, 36),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		align = Enum.TextXAlignment.Center,
		parent = panel,
		zIndex = 2,
	})

	local track = Theme.frame({
		name = "Ray",
		size = UDim2.fromOffset(TRACK_WIDTH, 38),
		position = UDim2.new(0.5, 0, 0, 68),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.panelDeep,
		transparency = 0,
		radius = Theme.radius.sm,
		parent = panel,
		zIndex = 2,
	})
	Theme.stroke(track, Theme.color.strokeSoft, 1)

	-- TIK ISARETLERI: ray boyunca esit arayla ince cizgiler. Isaretcinin
	-- ne kadar hizli gittigini ve merkeze ne kadar kaldigini bunlar
	-- okunur kiliyor; bos bir ray uzerinde hiz algilanmiyor.
	for index = 1, 9 do
		local major = index == 5
		Theme.frame({
			name = "Tik" .. index,
			size = UDim2.new(0, if major then 2 else 1, 0, if major then 38 else 12),
			position = UDim2.new(index / 10, 0, if major then 0 else 0.5, if major then 0 else -6),
			color = Theme.color.strokeSoft,
			transparency = if major then 0.1 else 0.45,
			radius = 0,
			parent = track,
			zIndex = 3,
		})
	end

	local goodZone = Theme.frame({
		name = "IyiBolge",
		size = UDim2.fromOffset(40, 38),
		position = UDim2.new(0.5, 0, 0, 0),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.accentDark,
		transparency = 0.3,
		radius = Theme.radius.sm,
		parent = track,
		zIndex = 4,
	})
	local perfectZone = Theme.frame({
		name = "TamBolge",
		size = UDim2.fromOffset(20, 38),
		position = UDim2.new(0.5, 0, 0, 0),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.good,
		transparency = 0.2,
		radius = Theme.radius.sm,
		parent = track,
		zIndex = 5,
	})
	-- Mukemmel bolge nabiz atiyor: ray uzerinde nereye basilacagi
	-- bir bakista belli olsun.
	Theme.pulse(perfectZone, "BackgroundTransparency", 0.5, 0.9)

	local marker = Theme.frame({
		name = "Isaretci",
		size = UDim2.fromOffset(6, 50),
		position = UDim2.new(0, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		color = Theme.color.text,
		transparency = 0,
		radius = 3,
		parent = track,
		zIndex = 6,
	})
	marker.ZIndex = 6

	-- Adim noktalari: kac adim var, kacinci adimdayiz. Sayidan once
	-- goz bunu okuyor.
	local pipRow = Theme.frame({
		name = "AdimNoktalari",
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 10),
		position = UDim2.fromOffset(Theme.space.lg, 116),
		transparency = 1,
		radius = 0,
		parent = panel,
		zIndex = 2,
	})
	local pipLayout = Theme.list(pipRow, Theme.space.xs, Enum.FillDirection.Horizontal)
	pipLayout.HorizontalAlignment = Enum.HorizontalAlignment.Center

	local steps = Theme.label({
		name = "Adim",
		text = "",
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 20),
		position = UDim2.fromOffset(Theme.space.lg, 132),
		textSize = Theme.size.small,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textDim,
		parent = panel,
		zIndex = 2,
	})
	local grade = Theme.label({
		name = "Not",
		text = "",
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 26),
		position = UDim2.fromOffset(Theme.space.lg, 156),
		textSize = Theme.size.title,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
		zIndex = 2,
	})
	local gradeScale = Instance.new("UIScale")
	gradeScale.Parent = grade

	local button = Theme.button({
		name = "Kes",
		text = "",
		size = UDim2.fromScale(1, 1),
		color = Theme.color.panel,
		parent = panel,
		onClick = press,
	})
	button.BackgroundTransparency = 1
	button.ZIndex = 0

	widgets = {
		panel = panel,
		track = track,
		marker = marker,
		goodZone = goodZone,
		perfectZone = perfectZone,
		pipRow = pipRow,
		pips = {},
		steps = steps,
		grade = grade,
		gradeScale = gradeScale,
		hint = hint,
	}

	UserInputService.InputBegan:Connect(function(input: InputObject, processed: boolean)
		if processed or schedule == nil then
			return
		end
		if input.KeyCode == Enum.KeyCode.Space or input.UserInputType == Enum.UserInputType.MouseButton1 then
			press()
		end
	end)
end

--- Sunucu ameliyati baslatti.
function SurgeryPanel.begin(incoming: any)
	if widgets == nil then
		return
	end
	schedule = incoming
	table.clear(sentSteps)

	Theme.open(widgets.panel)
	widgets.grade.Text = ""

	-- Adim noktalari her ameliyatta yeniden kuruluyor: adim sayisi
	-- yukseltmelerle degisebiliyor.
	widgets.pipRow:ClearAllChildren()
	local pipLayout = Theme.list(widgets.pipRow, Theme.space.xs, Enum.FillDirection.Horizontal)
	pipLayout.HorizontalAlignment = Enum.HorizontalAlignment.Center
	widgets.pips = {}
	for index = 1, incoming.steps do
		table.insert(widgets.pips, Theme.frame({
			name = "Nokta" .. index,
			size = UDim2.fromOffset(10, 10),
			color = Theme.color.strokeSoft,
			transparency = 0.3,
			radius = 5,
			parent = widgets.pipRow,
			layoutOrder = index,
			zIndex = 3,
		}))
	end
	widgets.goodZone.Size = UDim2.fromOffset(
		math.max(8, TRACK_WIDTH * (incoming.goodWindow * 2 / incoming.stepSeconds)),
		34
	)
	widgets.perfectZone.Size = UDim2.fromOffset(
		math.max(6, TRACK_WIDTH * (incoming.perfectWindow * 2 / incoming.stepSeconds)),
		34
	)

	if connection ~= nil then
		connection:Disconnect()
	end
	connection = RunService.RenderStepped:Connect(function()
		if schedule == nil then
			return
		end
		local now = Workspace:GetServerTimeNow()
		local step = currentStep(now)
		local target = schedule.startTime + step * schedule.stepSeconds
		local ratio = math.clamp(0.5 + (now - target) / schedule.stepSeconds, 0, 1)

		widgets.marker.Position = UDim2.new(ratio, 0, 0.5, 0)
		-- Isaretci mukemmel bolgedeyken beyazdan yesile donuyor:
		-- basma anini yalnizca konumdan degil, renkten de okuyabilmek
		-- gecikmeli oynayan oyuncuya yardim ediyor.
		local distance = math.abs(ratio - 0.5) * schedule.stepSeconds
		widgets.marker.BackgroundColor3 = if distance <= schedule.perfectWindow
			then Theme.color.good
			elseif distance <= schedule.goodWindow then Theme.color.accentGlow
			else Theme.color.text
		widgets.steps.Text = Loc.get("surgery.step", step, schedule.steps)
	end)
end

--- Sunucu bir tiklamayi notlandirdi.
function SurgeryPanel.grade(grade: string)
	if widgets == nil then
		return
	end
	local color = Theme.color.bad
	if grade == "perfect" then
		widgets.grade.Text = Loc.get("surgery.perfect")
		color = Theme.color.good
		Audio.play("surgeryPerfect")
	elseif grade == "good" then
		widgets.grade.Text = Loc.get("surgery.good")
		color = Theme.color.accentGlow
		Audio.play("surgeryGood")
	else
		widgets.grade.Text = Loc.get("surgery.miss")
		Audio.play("surgeryMiss")
	end
	widgets.grade.TextColor3 = color

	-- Not yazisi ZIPLAYARAK giriyor: ayni yazi ust uste iki kez
	-- cikarsa bile ikincisinin geldigi anlasiliyor.
	widgets.gradeScale.Scale = 1.35
	Theme.tween(widgets.gradeScale, { Scale = 1 }, Theme.motion.normal, Enum.EasingStyle.Back)

	local pip = widgets.pips[lastPressed]
	if pip ~= nil then
		pip.BackgroundColor3 = color
		Theme.tween(pip, { BackgroundTransparency = 0 }, Theme.motion.quick)
	end
end

function SurgeryPanel.finish()
	stop()
end

return SurgeryPanel
