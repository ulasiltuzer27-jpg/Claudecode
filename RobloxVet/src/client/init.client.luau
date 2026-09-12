--!strict
-- init.client.luau
-- ================
-- Istemcinin TEK giris noktasi. Panelleri kurar, remote'lari dinler,
-- kisayollari baglar.
--
-- Istemci OYUN KURALI CALISTIRMAZ
-- -------------------------------
-- Burada hicbir para, seviye ya da teshis hesabi yok. Istemci
-- sunucudan gelen goruntuyu cizer ve niyet yollar. Bir panelin bir
-- dugmeyi "kilitli" gostermesi kolaylik; gercek denetim sunucuda.

local Players = game:GetService("Players")
local UserInputService = game:GetService("UserInputService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Chart = require(script.Chart)
local DiagnosisPanel = require(script.DiagnosisPanel)
local Hud = require(script.Hud)
local Notify = require(script.Notify)
local ShopPanel = require(script.ShopPanel)
local SurgeryPanel = require(script.SurgeryPanel)
local Theme = require(script.Theme)
local ToolBar = require(script.ToolBar)
local TreatmentPanel = require(script.TreatmentPanel)

local player = Players.LocalPlayer
local playerGui = player:WaitForChild("PlayerGui")

local screen = Instance.new("ScreenGui")
screen.Name = "VeterinerArayuz"
screen.ResetOnSpawn = false
screen.IgnoreGuiInset = true
screen.ZIndexBehavior = Enum.ZIndexBehavior.Sibling
screen.Parent = playerGui

Hud.create(screen)
Chart.create(screen)
DiagnosisPanel.create(screen)
TreatmentPanel.create(screen)
ToolBar.create(screen)
ShopPanel.create(screen)
SurgeryPanel.create(screen)
Notify.create(screen)

-- Kisayol serisi ekranin altinda: oyuncunun tuslari tahmin etmesi
-- gerekmesin.
local hints = Theme.label({
	name = "Kisayollar",
	text = Loc.get("hud.keys"),
	size = UDim2.new(0, 520, 0, 18),
	position = UDim2.new(0.5, 0, 1, -88),
	anchor = Vector2.new(0.5, 1),
	textSize = 12,
	align = Enum.TextXAlignment.Center,
	color = Theme.color.textDim,
	parent = screen,
})
hints.TextStrokeTransparency = 0.6

local lastState: any = nil
local lastChart: any = nil

local function redraw()
	Hud.update(lastState)
	ToolBar.update(lastState)
	ShopPanel.update(lastState)
	Chart.update(lastChart)
	DiagnosisPanel.update(lastChart)
	TreatmentPanel.update(lastChart, lastState)
end

Net.get("StateUpdate").OnClientEvent:Connect(function(state: any)
	lastState = state
	redraw()
end)

Net.get("ChartUpdate").OnClientEvent:Connect(function(chart: any)
	lastChart = chart
	redraw()
end)

Net.get("Notify").OnClientEvent:Connect(function(kind: string, messageKey: string, args: { any })
	Notify.push(kind, messageKey, args)
end)

Net.get("ToolResult").OnClientEvent:Connect(function(toolId: string, payload: { any })
	-- Ameliyat adimlarinin notu ayni kanaldan geliyor: tek bir remote
	-- tanimi, iki kullanim. Ayirt edici, toolId'nin ozel degeri.
	if toolId == "surgeryStep" then
		SurgeryPanel.grade(tostring(payload[1]))
		return
	end

	if #payload == 0 then
		Notify.push("info", "notify.nothingFound", { Loc.get("tool." .. toolId) })
		return
	end
	for _, nameKey in payload do
		Notify.push("good", "notify.symptomFound", { nameKey })
	end
end)

Net.get("SurgeryBegin").OnClientEvent:Connect(function(schedule: any)
	SurgeryPanel.begin(schedule)
end)

Net.get("SurgeryEnd").OnClientEvent:Connect(function(_passed: boolean, _score: number)
	SurgeryPanel.finish()
end)

UserInputService.InputBegan:Connect(function(input: InputObject, processed: boolean)
	if processed then
		return
	end
	if input.KeyCode == Enum.KeyCode.B then
		ShopPanel.toggle()
	elseif input.KeyCode == Enum.KeyCode.E then
		Net.toServer("CallNext")
	elseif input.KeyCode == Enum.KeyCode.T then
		Net.toServer("Discharge")
	end
end)

print("[Veteriner] istemci hazir.")
