--!strict
-- ToolBar.luau
-- ============
-- Ekranin altindaki alet cubugu. Tiklamak ya da 1-8 tuslari aleti
-- DOGRUDAN kullanir; ayrica bir "sec sonra uygula" adimi yok, cunku
-- muayenede her tiklama bir islem.
--
-- Tus kapagi gorunumu
-- -------------------
-- Her alet bir klavye tusu gibi duruyor: ustte tus numarasi, ortada
-- ikon, altta ad. Secili alet bir tik yukari kalkiyor ve kenari
-- parliyor - hangi aletin elde oldugu bakmadan biliniyor.
--
-- Bekleme suresi supurmesi
-- ------------------------
-- Alet kullanildiginda karenin ustunden soldan saga bir golge geciyor
-- (`useSeconds` kadar). Sunucu zaten bu surede ikinci kullanimi
-- reddediyor; supurme, reddi ONCEDEN gorunur kiliyor.

local UserInputService = game:GetService("UserInputService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local ToolBar = {}

local container: Frame? = nil
local cells: { any } = {}
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
	for index, cell in cells do
		local active = index == selected
		Theme.tween(cell.root, {
			Position = UDim2.fromOffset(0, if active then -6 else 0),
			BackgroundColor3 = if active then Theme.color.accentDark else Theme.color.panelLight,
		}, Theme.motion.quick)
		Theme.tween(cell.stroke, {
			Color = if active then Theme.color.accentGlow else Theme.color.strokeSoft,
			Thickness = if active then 2 else 1,
		}, Theme.motion.quick)
		cell.key.TextColor3 = if active then Theme.color.accentGlow else Theme.color.textFaint
		cell.name.TextColor3 = if active then Theme.color.text else Theme.color.textDim
	end
end

--- Bekleme supurmesi: sag kenardan basliyor, sifira iniyor.
local function sweep(cell: any)
	cell.cooldown.Size = UDim2.fromScale(1, 1)
	cell.cooldown.BackgroundTransparency = 0.45
	Theme.tween(cell.cooldown, { Size = UDim2.fromScale(0, 1) }, cell.seconds, Enum.EasingStyle.Linear)
end

local function use(index: number)
	local toolId = toolIds[index]
	if toolId == nil then
		return
	end
	selected = index
	highlight()
	sweep(cells[index])
	Audio.play("uiClick")
	Net.toServer("SelectTool", toolId)
	Net.toServer("UseTool", toolId)
end

function ToolBar.create(parent: ScreenGui)
	local frame = Theme.card({
		name = "AletCubugu",
		size = UDim2.fromOffset(760, 84),
		position = UDim2.new(0.5, 0, 1, -Theme.space.lg),
		anchor = Vector2.new(0.5, 1),
		parent = parent,
	})
	Theme.padding(frame, Theme.space.sm, Theme.space.md)
	local layout = Theme.list(frame, Theme.space.sm, Enum.FillDirection.Horizontal)
	layout.HorizontalAlignment = Enum.HorizontalAlignment.Center
	layout.VerticalAlignment = Enum.VerticalAlignment.Center
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

--- Tek bir alet kapagini kurar.
local function toolCell(parent: Instance, tool: any, index: number): any
	local root = Theme.frame({
		name = tool.id,
		size = UDim2.fromOffset(84, 62),
		color = Theme.color.panelLight,
		transparency = 0,
		radius = Theme.radius.sm,
		parent = parent,
		layoutOrder = index,
		zIndex = 2,
	})
	local stroke = Theme.stroke(root, Theme.color.strokeSoft, 1)

	local cooldown = Theme.frame({
		name = "Bekleme",
		size = UDim2.fromScale(0, 1),
		color = Theme.color.shadow,
		transparency = 1,
		radius = Theme.radius.sm,
		parent = root,
		zIndex = 4,
	})

	local key = Theme.label({
		name = "Tus",
		text = tostring(index),
		size = UDim2.fromOffset(14, 12),
		position = UDim2.fromOffset(6, 4),
		textSize = Theme.size.micro,
		bold = true,
		color = Theme.color.textFaint,
		parent = root,
		zIndex = 3,
	})

	Theme.icon({
		name = tool.id,
		size = 20,
		color = Theme.color.text,
		position = UDim2.new(0.5, 0, 0, 14),
		anchor = Vector2.new(0.5, 0),
		parent = root,
		zIndex = 3,
	})

	local name = Theme.label({
		name = "Ad",
		text = Loc.get(tool.nameKey),
		size = UDim2.new(1, -6, 0, 22),
		position = UDim2.new(0, 3, 1, -24),
		textSize = Theme.size.micro,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textDim,
		wrapped = true,
		parent = root,
		zIndex = 3,
	})

	-- Kare tiklanabilir olmali; goruntuyu bozmayan saydam bir dugme
	-- ustune biniyor (Theme.button'un kendi zemini bu tasarimi ezerdi).
	local hit = Instance.new("TextButton")
	hit.Name = "Tik"
	hit.Text = ""
	hit.Size = UDim2.fromScale(1, 1)
	hit.BackgroundTransparency = 1
	hit.BorderSizePixel = 0
	hit.ZIndex = 5
	hit.Parent = root
	hit.Activated:Connect(function()
		use(index)
	end)

	return {
		root = root,
		stroke = stroke,
		key = key,
		name = name,
		cooldown = cooldown,
		seconds = tool.useSeconds or 1.5,
	}
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

	for _, cell in cells do
		cell.root:Destroy()
	end
	cells = {}
	toolIds = {}

	for index, tool in state.tools do
		table.insert(toolIds, tool.id)
		table.insert(cells, toolCell(container, tool, index))
	end

	selected = math.clamp(selected, 1, math.max(#cells, 1))
	highlight()
end

return ToolBar
