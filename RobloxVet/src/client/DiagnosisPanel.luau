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
--
-- Eslesme yuzdesi neden var?
-- --------------------------
-- 31 hastalik arasindan ada bakarak secmek, bir sure sonra ezber
-- oluyordu. Her adayin yaninda "bulunan belirtilerin yuzde kaci bu
-- hastaligi destekliyor" yaziyor ve liste ona gore siralaniyor.
-- Bu oran SUNUCUDA hesaplaniyor (PatientFlow.chartSnapshot) ve zaten
-- oyuncunun elindeki bilgiden cikiyor; dogru cevabi soylemiyor -
-- iki hastalik ayni yuzdeyi rahatlikla paylasabiliyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local DiagnosisPanel = {}

local widgets: any = nil
local lastSignature = ""

function DiagnosisPanel.create(parent: ScreenGui)
	local panel = Theme.card({
		name = "TeshisPaneli",
		size = UDim2.fromOffset(330, 258),
		position = UDim2.new(1, -Theme.space.lg, 0, 496),
		anchor = Vector2.new(1, 0),
		parent = parent,
		visible = false,
	})

	Theme.icon({
		name = "info",
		size = 14,
		color = Theme.color.accent,
		position = UDim2.fromOffset(Theme.space.md, 12),
		parent = panel,
		zIndex = 2,
	})
	Theme.label({
		text = Loc.get("diagnosis.title"),
		size = UDim2.new(1, -Theme.space.md * 2 - 22, 0, 22),
		position = UDim2.fromOffset(Theme.space.md + 22, Theme.space.sm),
		textSize = Theme.size.body,
		bold = true,
		parent = panel,
		zIndex = 2,
	})
	Theme.label({
		text = Loc.get("diagnosis.hint"),
		size = UDim2.new(1, -Theme.space.md * 2, 0, 28),
		position = UDim2.fromOffset(Theme.space.md, 32),
		textSize = Theme.size.micro,
		color = Theme.color.textFaint,
		wrapped = true,
		parent = panel,
		zIndex = 2,
	})

	local list = Theme.scroller(
		panel,
		UDim2.new(1, -Theme.space.md * 2, 1, -72),
		UDim2.fromOffset(Theme.space.md, 64)
	)
	list.ZIndex = 2
	Theme.list(list, Theme.space.xs)

	widgets = { panel = panel, list = list }
end

--- Bir aday satiri: ad + eslesme cubugu + yuzde rozeti.
local function candidateRow(parent: Instance, candidate: any, index: number): TextButton
	local ratio = candidate.match or 0
	local color = if ratio >= 0.999
		then Theme.color.good
		elseif ratio >= 0.5 then Theme.color.warn
		else Theme.color.textFaint

	local button = Theme.button({
		name = candidate.id,
		text = "",
		size = UDim2.new(1, -Theme.space.sm, 0, 34),
		textSize = Theme.size.small,
		layoutOrder = index,
		parent = parent,
		zIndex = 2,
		onClick = function()
			Net.toServer("SubmitDiagnosis", candidate.id)
		end,
	})

	Theme.label({
		name = "Ad",
		text = Loc.get(candidate.nameKey),
		size = UDim2.new(1, -76, 0, 18),
		position = UDim2.fromOffset(Theme.space.sm, 3),
		textSize = Theme.size.small,
		bold = true,
		color = Theme.color.text,
		parent = button,
		zIndex = 3,
	})

	Theme.label({
		name = "Yuzde",
		text = string.format("%d%%", math.floor(ratio * 100 + 0.5)),
		size = UDim2.fromOffset(52, 18),
		position = UDim2.new(1, -Theme.space.sm - 52, 0, 3),
		textSize = Theme.size.micro,
		bold = true,
		align = Enum.TextXAlignment.Right,
		color = color,
		parent = button,
		zIndex = 3,
	})

	local bar = Theme.bar(
		button,
		UDim2.new(1, -Theme.space.sm * 2, 0, 5),
		UDim2.fromOffset(Theme.space.sm, 23),
		color,
		{ radius = 3 }
	)
	bar.track.ZIndex = 3
	bar.ghost.ZIndex = 3
	bar.fill.ZIndex = 4
	Theme.setBar(bar, ratio)

	return button
end

function DiagnosisPanel.update(chart: any)
	if widgets == nil then
		return
	end

	local show = chart ~= nil and chart.present == true and chart.diagnosed ~= true
	if show ~= widgets.panel.Visible then
		if show then
			Theme.open(widgets.panel)
		else
			Theme.close(widgets.panel)
		end
	end
	if not show then
		lastSignature = ""
		return
	end

	-- Liste yalnizca degistiyse yeniden kuruluyor: her vitals tikinda
	-- dugmeleri silip yeniden yaratmak imleci dugmeden dusururdu.
	-- Imza artik eslesme oranlarini da iceriyor, cunku yeni bir belirti
	-- bulundugunda siralamanin degismesi GEREKIYOR.
	local parts = { chart.animalId }
	for _, candidate in chart.candidates do
		table.insert(parts, candidate.id .. ":" .. tostring(candidate.hits or 0))
	end
	local signature = table.concat(parts, "|")
	if signature == lastSignature then
		return
	end
	lastSignature = signature

	widgets.list:ClearAllChildren()
	Theme.list(widgets.list, Theme.space.xs)

	for index, candidate in chart.candidates do
		candidateRow(widgets.list, candidate, index)
	end
end

return DiagnosisPanel
