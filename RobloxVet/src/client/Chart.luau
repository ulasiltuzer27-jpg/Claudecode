--!strict
-- Chart.luau
-- ==========
-- Hasta karti: tur, sahibinin sikayeti, saglik/stres/memnuniyet ve
-- BULUNMUS belirtiler. Ayrica acil vakalarin geri sayimi.
--
-- Kartta hastaligin adi ancak dogru teshisten SONRA cikiyor - cunku
-- sunucu oncesinde onu hic yollamiyor (PatientFlow.chartSnapshot).
--
-- Sahibin sikayeti neden burada?
-- ------------------------------
-- NPC'nin basindaki balon uzaktan okunmuyor ve oyuncu masaya egildiginde
-- kayboluyor. Ayni cumle kartta da duruyor: ipucu, isin yapildigi yerde
-- goz onunde olmali.

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
	-- Acil vaka bandi ekranin ORTASINDA ve ustte: geri sayim, oyuncunun
	-- gozunun kacinilmaz olarak dustugu yerde olmali.
	local banner = Theme.frame({
		name = "AcilBandi",
		size = UDim2.fromOffset(380, 44),
		position = UDim2.new(0.5, 0, 0, 14),
		anchor = Vector2.new(0.5, 0),
		color = Color3.fromRGB(92, 26, 26),
		transparency = 0.05,
		parent = parent,
		visible = false,
	})
	Theme.stroke(banner, Theme.color.bad, 2)
	local bannerText = Theme.label({
		name = "Metin",
		text = "",
		size = UDim2.new(1, -20, 1, 0),
		position = UDim2.fromOffset(10, 0),
		textSize = 17,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = banner,
	})

	local panel = Theme.frame({
		name = "HastaKarti",
		size = UDim2.fromOffset(330, 316),
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

	local hint = Theme.label({
		name = "Sikayet",
		text = "",
		size = UDim2.new(1, -24, 0, 30),
		position = UDim2.fromOffset(12, 52),
		textSize = 11,
		color = Theme.color.textDim,
		wrapped = true,
		parent = panel,
	})

	local function meter(labelName: string, y: number, color: Color3)
		local label = Theme.label({
			name = labelName,
			text = "",
			size = UDim2.new(1, -24, 0, 14),
			position = UDim2.fromOffset(12, y),
			textSize = 11,
			color = Theme.color.textDim,
			parent = panel,
		})
		local bar = Theme.bar(panel, UDim2.new(1, -24, 0, 9), UDim2.fromOffset(12, y + 16), color)
		return label, bar
	end

	local healthLabel, healthBar = meter("SaglikYazi", 84, Theme.color.health)
	local stressLabel, stressBar = meter("StresYazi", 114, Theme.color.stress)
	local satisfactionLabel, satisfactionBar = meter("MemnuniyetYazi", 144, Theme.color.accent)

	Theme.label({
		name = "SemptomBaslik",
		text = Loc.get("chart.symptoms"),
		size = UDim2.new(1, -24, 0, 16),
		position = UDim2.fromOffset(12, 176),
		textSize = 12,
		bold = true,
		color = Theme.color.textDim,
		parent = panel,
	})

	local list = Theme.scroller(panel, UDim2.new(1, -24, 0, 80), UDim2.fromOffset(12, 194))
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
		banner = banner,
		bannerText = bannerText,
		title = title,
		condition = condition,
		hint = hint,
		healthLabel = healthLabel,
		healthBar = healthBar,
		stressLabel = stressLabel,
		stressBar = stressBar,
		satisfactionLabel = satisfactionLabel,
		satisfactionBar = satisfactionBar,
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
		widgets.hint.Text = ""
		widgets.healthLabel.Text = ""
		widgets.stressLabel.Text = ""
		widgets.satisfactionLabel.Text = ""
		Theme.setBar(widgets.healthBar, 0)
		Theme.setBar(widgets.stressBar, 0)
		Theme.setBar(widgets.satisfactionBar, 0)
		widgets.list:ClearAllChildren()
		Theme.list(widgets.list, 3)
		widgets.discharge.Visible = false
		widgets.banner.Visible = false
		return
	end

	widgets.title.Text = Loc.get(chart.nameKey)
	widgets.condition.Text = if chart.diagnosed and chart.conditionNameKey ~= nil
		then Loc.get("chart.diagnosed", Loc.get(chart.conditionNameKey))
		else Loc.get("chart.undiagnosed")
	widgets.condition.TextColor3 = if chart.diagnosed then Theme.color.good else Theme.color.warn

	widgets.hint.Text = if chart.hintKey ~= nil then "\u{201C}" .. Loc.get(chart.hintKey) .. "\u{201D}" else ""

	widgets.healthLabel.Text = Loc.get("chart.health", chart.health)
	Theme.setBar(widgets.healthBar, chart.health / 100)
	widgets.stressLabel.Text = Loc.get("chart.stress", chart.stress)
	Theme.setBar(widgets.stressBar, chart.stress / 100)
	widgets.satisfactionLabel.Text = Loc.get("chart.satisfaction", chart.satisfaction)
	Theme.setBar(widgets.satisfactionBar, chart.satisfaction / 100)

	-- Acil vaka geri sayimi. secondsLeft negatifse acil degil.
	if chart.emergency == true and chart.secondsLeft >= 0 then
		local seconds = math.floor(chart.secondsLeft)
		widgets.banner.Visible = true
		widgets.bannerText.Text = Loc.get("chart.emergency", math.floor(seconds / 60), seconds % 60)
	else
		widgets.banner.Visible = false
	end

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
