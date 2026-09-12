--!strict
-- ToolBar.luau
-- ============
-- Ekranin altindaki alet cubugu. Tiklamak ya da 1-6 tuslari aleti
-- DOGRUDAN kullanir; ayrica bir "sec sonra uygula" adimi yok, cunku
-- muayenede her tiklama bir islem.

local UserInputService = game:GetService("UserInputService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local ToolBar = {}

local container: Frame? = nil
local buttons: { TextButton } = {}
local toolIds: { string } = {}
local selected = 1

local KEYS = {
	[Enum.KeyCode.One] = 1,
	[Enum.KeyCode.Two] = 2,
	[Enum.KeyCode.Three] = 3,
	[Enum.KeyCode.Four] = 4,
	[Enum.KeyCode.Five] = 5,
	[Enum.KeyCode.Six] = 6,
	[Enum.KeyCode.Seven] = 7,
	[Enum.KeyCode.Eight] = 8,
}

local function highlight()
	for index, button in buttons do
		local active = index == selected
		button.BackgroundColor3 = if active then Theme.color.accentDark else Theme.color.panelLight
		button.TextColor3 = if active then Theme.color.text else Theme.color.textDim
	end
end

local function use(index: number)
	local toolId = toolIds[index]
	if toolId == nil then
		return
	end
	selected = index
	highlight()
	Net.toServer("SelectTool", toolId)
	Net.toServer("UseTool", toolId)
end

function ToolBar.create(parent: ScreenGui)
	local frame = Theme.frame({
		name = "AletCubugu",
		size = UDim2.fromOffset(730, 66),
		position = UDim2.new(0.5, 0, 1, -16),
		anchor = Vector2.new(0.5, 1),
		parent = parent,
	})
	Theme.stroke(frame)
	Theme.padding(frame, 8)
	local layout = Theme.list(frame, 8, Enum.FillDirection.Horizontal)
	layout.HorizontalAlignment = Enum.HorizontalAlignment.Center
	container = frame

	UserInputService.InputBegan:Connect(function(input: InputObject, processed: boolean)
		if processed then
			return
		end
		local index = KEYS[input.KeyCode]
		if index ~= nil then
			use(index)
		end
	end)
end

--- Alet listesini sunucudan gelen duruma gore yeniden kurar.
function ToolBar.update(state: any)
	if container == nil or state == nil or state.ready ~= true then
		return
	end

	local same = #state.tools == #toolIds
	if same then
		for index, tool in state.tools do
			if toolIds[index] ~= tool.id then
				same = false
				break
			end
		end
	end
	if same then
		return -- degismedi; her karede arayuzu yeniden kurmanin anlami yok
	end

	for _, button in buttons do
		button:Destroy()
	end
	buttons = {}
	toolIds = {}

	for index, tool in state.tools do
		table.insert(toolIds, tool.id)
		local button = Theme.button({
			name = tool.id,
			text = ("%d  %s"):format(index, Loc.get(tool.nameKey)),
			size = UDim2.fromOffset(84, 50),
			layoutOrder = index,
			parent = container,
			onClick = function()
				use(index)
			end,
		})
		button.TextWrapped = true
		button.TextSize = 12
		table.insert(buttons, button)
	end

	selected = math.clamp(selected, 1, math.max(#buttons, 1))
	highlight()
end

return ToolBar
