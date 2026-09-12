--!strict
-- DiagnosisPanel.luau
-- ===================
-- Teshis paneli: bu turde gorulebilecek hastaliklarin listesi. Oyuncu
-- birini secip gonderiyor, DOGRULUGUNU SUNUCU soyluyor.
--
-- Liste neden hep gorunuyor?
-- --------------------------
-- Oyuncuya "hangi hastaliklar olabilir" listesini vermek bulmacayi
-- kolaylastirmiyor; semptom->hastalik eslesmesini kurmak hala oyuncunun
-- isi. Listeyi gizlemek yalnizca oyuncuyu ezberlemeye zorlardi.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local DiagnosisPanel = {}

local widgets: any = nil
local lastSignature = ""

function DiagnosisPanel.create(parent: ScreenGui)
	local panel = Theme.frame({
		name = "TeshisPaneli",
		size = UDim2.fromOffset(330, 250),
		position = UDim2.new(1, -16, 0, 358),
		anchor = Vector2.new(1, 0),
		parent = parent,
		visible = false,
	})
	Theme.stroke(panel)

	Theme.label({
		text = Loc.get("diagnosis.title"),
		size = UDim2.new(1, -24, 0, 22),
		position = UDim2.fromOffset(12, 8),
		textSize = 16,
		bold = true,
		parent = panel,
	})
	Theme.label({
		text = Loc.get("diagnosis.hint"),
		size = UDim2.new(1, -24, 0, 26),
		position = UDim2.fromOffset(12, 28),
		textSize = 11,
		color = Theme.color.textDim,
		wrapped = true,
		parent = panel,
	})

	local list = Theme.scroller(panel, UDim2.new(1, -24, 1, -66), UDim2.fromOffset(12, 56))
	Theme.list(list, 4)

	widgets = { panel = panel, list = list }
end

function DiagnosisPanel.update(chart: any)
	if widgets == nil then
		return
	end

	local show = chart ~= nil and chart.present == true and chart.diagnosed ~= true
	widgets.panel.Visible = show
	if not show then
		lastSignature = ""
		return
	end

	-- Liste yalnizca degistiyse yeniden kuruluyor: her vitals tikinda
	-- dugmeleri silip yeniden yaratmak imleci dugmeden dusururdu.
	local signature = chart.animalId .. "|" .. tostring(#chart.candidates)
	if signature == lastSignature then
		return
	end
	lastSignature = signature

	widgets.list:ClearAllChildren()
	Theme.list(widgets.list, 4)

	for index, candidate in chart.candidates do
		local id = candidate.id
		Theme.button({
			name = id,
			text = Loc.get(candidate.nameKey),
			size = UDim2.new(1, -8, 0, 28),
			textSize = 13,
			layoutOrder = index,
			parent = widgets.list,
			onClick = function()
				Net.toServer("SubmitDiagnosis", id)
			end,
		})
	end
end

return DiagnosisPanel
