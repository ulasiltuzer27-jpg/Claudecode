--!strict
-- Chart.luau
-- ==========
-- Hasta karti: tur, saglik/stres, BULUNMUS semptomlar ve taburcu dugmesi.
--
-- Kartta hastaligin adi ancak dogru teshisten SONRA cikiyor - cunku
-- sunucu oncesinde onu hic yollamiyor (PatientFlow.chartSnapshot).

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local Chart = {}

local widgets: any = nil

local SEVERITY_COLOR = {
	[1] = Theme.color.textDim,
	[2] = Theme.color.warn,
	[3] = Theme.color.bad,
}

function Chart.create(parent: ScreenGui)
	local panel = Theme.frame({
		name = "HastaKarti",
		size = UDim2.fromOffset(330, 268),
		position = UDim2.new(1, -16, 0, 82),
		anchor = Vector2.new(1, 0),
		parent = parent,
	})
	Theme.stroke(panel)

	local title = Theme.label({
		name = "Baslik",
		text = Loc.get("chart.empty"),
		size = UDim2.new(1, -24, 0, 24),
		position = UDim2.fromOffset(12, 8),
		textSize = 18,
		bold = true,
		parent = panel,
	})

	local condition = Theme.label({
		name = "Hastalik",
		text = "",
		size = UDim2.new(1, -24, 0, 18),
		position = UDim2.fromOffset(12, 32),
		textSize = 13,
		color = Theme.color.good,
		parent = panel,
	})

	local healthLabel = Theme.label({
		name = "SaglikYazi",
		text = "",
		size = UDim2.new(1, -24, 0, 14),
		position = UDim2.fromOffset(12, 54),
		textSize = 11,
		color = Theme.color.textDim,
		parent = panel,
	})
	local healthBar = Theme.bar(panel, UDim2.new(1, -24, 0, 9), UDim2.fromOffset(12, 70), Theme.color.health)

	local stressLabel = Theme.label({
		name = "StresYazi",
		text = "",
		size = UDim2.new(1, -24, 0, 14),
		position = UDim2.fromOffset(12, 84),
		textSize = 11,
		color = Theme.color.textDim,
		parent = panel,
	})
	local stressBar = Theme.bar(panel, UDim2.new(1, -24, 0, 9), UDim2.fromOffset(12, 100), Theme.color.stress)

	Theme.label({
		name = "SemptomBaslik",
		text = Loc.get("chart.symptoms"),
		size = UDim2.new(1, -24, 0, 16),
		position = UDim2.fromOffset(12, 116),
		textSize = 12,
		bold = true,
		color = Theme.color.textDim,
		parent = panel,
	})

	local list = Theme.scroller(panel, UDim2.new(1, -24, 0, 88), UDim2.fromOffset(12, 134))
	Theme.list(list, 3)

	local discharge = Theme.button({
		name = "Taburcu",
		text = Loc.get("chart.discharge"),
		size = UDim2.new(1, -24, 0, 28),
		position = UDim2.new(0, 12, 1, -36),
		color = Theme.color.accentDark,
		parent = panel,
		onClick = function()
			Net.toServer("Discharge")
		end,
	})

	widgets = {
		panel = panel,
		title = title,
		condition = condition,
		healthLabel = healthLabel,
		healthBar = healthBar,
		stressLabel = stressLabel,
		stressBar = stressBar,
		list = list,
		discharge = discharge,
	}
end

function Chart.update(chart: any)
	if widgets == nil then
		return
	end

	if chart == nil or chart.present ~= true then
		widgets.title.Text = Loc.get("chart.empty")
		widgets.condition.Text = ""
		widgets.healthLabel.Text = ""
		widgets.stressLabel.Text = ""
		Theme.setBar(widgets.healthBar, 0)
		Theme.setBar(widgets.stressBar, 0)
		widgets.list:ClearAllChildren()
		Theme.list(widgets.list, 3)
		widgets.discharge.Visible = false
		return
	end

	widgets.title.Text = Loc.get(chart.nameKey)
	widgets.condition.Text = if chart.diagnosed and chart.conditionNameKey ~= nil
		then Loc.get("chart.diagnosed", Loc.get(chart.conditionNameKey))
		else Loc.get("chart.undiagnosed")
	widgets.condition.TextColor3 = if chart.diagnosed then Theme.color.good else Theme.color.warn

	widgets.healthLabel.Text = Loc.get("chart.health", chart.health)
	Theme.setBar(widgets.healthBar, chart.health / 100)
	widgets.stressLabel.Text = Loc.get("chart.stress", chart.stress)
	Theme.setBar(widgets.stressBar, chart.stress / 100)

	widgets.list:ClearAllChildren()
	Theme.list(widgets.list, 3)

	if #chart.revealed == 0 then
		Theme.label({
			text = Loc.get("chart.noSymptoms"),
			size = UDim2.new(1, -8, 0, 18),
			textSize = 12,
			color = Theme.color.textDim,
			parent = widgets.list,
		})
	else
		for index, symptom in chart.revealed do
			Theme.label({
				text = "• " .. Loc.get(symptom.nameKey),
				size = UDim2.new(1, -8, 0, 18),
				textSize = 13,
				color = SEVERITY_COLOR[symptom.severity] or Theme.color.text,
				layoutOrder = index,
				parent = widgets.list,
			})
		end
	end

	widgets.discharge.Visible = chart.complete == true
end

return Chart
