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
	table.clear(sentSteps)
	if widgets ~= nil then
		widgets.panel.Visible = false
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
	Net.toServer("SurgeryInput", step)
end

function SurgeryPanel.create(parent: ScreenGui)
	local panel = Theme.frame({
		name = "AmeliyatPaneli",
		size = UDim2.fromOffset(TRACK_WIDTH + 48, 168),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		transparency = 0.04,
		parent = parent,
		visible = false,
	})
	Theme.stroke(panel, Theme.color.accent, 2)

	Theme.label({
		text = Loc.get("surgery.title"),
		size = UDim2.new(1, -24, 0, 24),
		position = UDim2.fromOffset(12, 10),
		textSize = 18,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
	})
	local hint = Theme.label({
		name = "Ipucu",
		text = Loc.get("surgery.hint"),
		size = UDim2.new(1, -24, 0, 18),
		position = UDim2.fromOffset(12, 34),
		textSize = 12,
		color = Theme.color.textDim,
		align = Enum.TextXAlignment.Center,
		parent = panel,
	})

	local track = Theme.frame({
		name = "Ray",
		size = UDim2.fromOffset(TRACK_WIDTH, 34),
		position = UDim2.new(0.5, 0, 0, 66),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.panelLight,
		transparency = 0,
		radius = 8,
		parent = panel,
	})

	local goodZone = Theme.frame({
		name = "IyiBolge",
		size = UDim2.fromOffset(40, 34),
		position = UDim2.new(0.5, 0, 0, 0),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.accentDark,
		transparency = 0.25,
		radius = 6,
		parent = track,
	})
	local perfectZone = Theme.frame({
		name = "TamBolge",
		size = UDim2.fromOffset(20, 34),
		position = UDim2.new(0.5, 0, 0, 0),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.good,
		transparency = 0.25,
		radius = 6,
		parent = track,
	})

	local marker = Theme.frame({
		name = "Isaretci",
		size = UDim2.fromOffset(6, 44),
		position = UDim2.new(0, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		color = Theme.color.text,
		transparency = 0,
		radius = 3,
		parent = track,
	})
	marker.ZIndex = 3

	local steps = Theme.label({
		name = "Adim",
		text = "",
		size = UDim2.new(1, -24, 0, 20),
		position = UDim2.fromOffset(12, 112),
		textSize = 14,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
	})
	local grade = Theme.label({
		name = "Not",
		text = "",
		size = UDim2.new(1, -24, 0, 20),
		position = UDim2.fromOffset(12, 134),
		textSize = 14,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
	})

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
		steps = steps,
		grade = grade,
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

	widgets.panel.Visible = true
	widgets.grade.Text = ""
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
		widgets.steps.Text = Loc.get("surgery.step", step, schedule.steps)
	end)
end

--- Sunucu bir tiklamayi notlandirdi.
function SurgeryPanel.grade(grade: string)
	if widgets == nil then
		return
	end
	if grade == "perfect" then
		widgets.grade.Text = Loc.get("surgery.perfect")
		widgets.grade.TextColor3 = Theme.color.good
		Audio.play("surgeryPerfect")
	elseif grade == "good" then
		widgets.grade.Text = Loc.get("surgery.good")
		widgets.grade.TextColor3 = Theme.color.accent
		Audio.play("surgeryGood")
	else
		widgets.grade.Text = Loc.get("surgery.miss")
		widgets.grade.TextColor3 = Theme.color.bad
		Audio.play("surgeryMiss")
	end
end

function SurgeryPanel.finish()
	stop()
end

return SurgeryPanel
