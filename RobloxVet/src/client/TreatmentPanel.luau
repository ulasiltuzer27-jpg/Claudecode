--!strict
-- TreatmentPanel.luau
-- ===================
-- Tedavi paneli. Teshis konduktan SONRA aciliyor.
--
-- Gereken tedaviler ustte isaretli listede; alttaki dugmeler oyuncunun
-- acabildigi butun tedaviler. Yanlis tedavi uygulamak serbest (ve
-- cezali): listeyi yalnizca dogru tedavilerle doldurmak bulmacayi
-- ortadan kaldirirdi.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local TreatmentPanel = {}

local widgets: any = nil
local builtSignature = ""

function TreatmentPanel.create(parent: ScreenGui)
	local panel = Theme.frame({
		name = "TedaviPaneli",
		size = UDim2.fromOffset(330, 250),
		position = UDim2.new(1, -16, 0, 406),
		anchor = Vector2.new(1, 0),
		parent = parent,
		visible = false,
	})
	Theme.stroke(panel)

	Theme.label({
		text = Loc.get("treatment.title"),
		size = UDim2.new(1, -24, 0, 22),
		position = UDim2.fromOffset(12, 8),
		textSize = 16,
		bold = true,
		parent = panel,
	})

	local required = Theme.frame({
		name = "Gereken",
		size = UDim2.new(1, -24, 0, 62),
		position = UDim2.fromOffset(12, 32),
		color = Theme.color.panelLight,
		transparency = 0.2,
		radius = 8,
		parent = panel,
	})
	Theme.padding(required, 6)
	Theme.list(required, 2)

	local buttons = Theme.scroller(panel, UDim2.new(1, -24, 1, -106), UDim2.fromOffset(12, 100))
	local grid = Instance.new("UIGridLayout")
	grid.CellSize = UDim2.fromOffset(96, 32)
	grid.CellPadding = UDim2.fromOffset(6, 6)
	grid.SortOrder = Enum.SortOrder.LayoutOrder
	grid.Parent = buttons

	widgets = { panel = panel, required = required, buttons = buttons }
end

function TreatmentPanel.update(chart: any, state: any)
	if widgets == nil then
		return
	end

	local show = chart ~= nil and chart.present == true and chart.diagnosed == true
	widgets.panel.Visible = show
	if not show then
		builtSignature = ""
		return
	end

	-- Gereken tedaviler: her guncellemede yenileniyor (isaretler degisiyor).
	widgets.required:ClearAllChildren()
	Theme.padding(widgets.required, 6)
	Theme.list(widgets.required, 2)
	for index, item in chart.required do
		Theme.label({
			text = (if item.applied then "✓ " else "○ ") .. Loc.get(item.nameKey),
			size = UDim2.new(1, 0, 0, 16),
			textSize = 12,
			bold = item.applied,
			color = if item.applied then Theme.color.good else Theme.color.textDim,
			layoutOrder = index,
			parent = widgets.required,
		})
	end

	-- Dugmeler yalnizca acilan tedavi kumesi degistiginde kuruluyor.
	if state == nil or state.ready ~= true then
		return
	end
	local signature = ""
	for _, treatment in state.treatments do
		signature ..= treatment.id .. ":" .. tostring(treatment.cost) .. ";"
	end
	if signature == builtSignature then
		return
	end
	builtSignature = signature

	widgets.buttons:ClearAllChildren()
	local grid = Instance.new("UIGridLayout")
	grid.CellSize = UDim2.fromOffset(96, 32)
	grid.CellPadding = UDim2.fromOffset(6, 6)
	grid.SortOrder = Enum.SortOrder.LayoutOrder
	grid.Parent = widgets.buttons

	for index, treatment in state.treatments do
		local id = treatment.id
		local button = Theme.button({
			name = id,
			text = ("%s\n%d TL"):format(Loc.get(treatment.nameKey), treatment.cost),
			size = UDim2.fromOffset(96, 32),
			textSize = 11,
			layoutOrder = index,
			parent = widgets.buttons,
			onClick = function()
				Net.toServer("ApplyTreatment", id)
			end,
		})
		button.TextWrapped = true
	end
end

return TreatmentPanel
