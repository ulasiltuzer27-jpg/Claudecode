--!strict
-- TreatmentPanel.luau
-- ===================
-- Tedavi paneli. Teshis konduktan SONRA aciliyor.
--
-- Gereken tedaviler ustte isaretli listede; alttaki ikon izgarasi
-- oyuncunun acabildigi butun tedaviler. Yanlis tedavi uygulamak serbest
-- (ve cezali): izgarayi yalnizca dogru tedavilerle doldurmak bulmacayi
-- ortadan kaldirirdi.
--
-- Neden ikon izgarasi?
-- --------------------
-- Sekiz tedavi alt alta yazi olarak durdugunda oyuncu her seferinde
-- okuyordu. Ikon + ad + fiyat rozeti olan kareler, ucuncu hastadan sonra
-- okunmadan taniniyor. Uygulanan tedavinin karesi HALKA DOLUMUYLA
-- isaretleniyor: onay tiki bir anda beliriyor, dolum ise "oldu" diyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local TreatmentPanel = {}

local widgets: any = nil
local builtSignature = ""
local cells: { [string]: any } = {}

local CELL = UDim2.fromOffset(96, 64)

function TreatmentPanel.create(parent: ScreenGui)
	local panel = Theme.card({
		name = "TedaviPaneli",
		size = UDim2.fromOffset(330, 276),
		position = UDim2.new(1, -Theme.space.lg, 0, 496),
		anchor = Vector2.new(1, 0),
		parent = parent,
		visible = false,
	})

	Theme.icon({
		name = "health",
		size = 14,
		color = Theme.color.good,
		position = UDim2.fromOffset(Theme.space.md, 12),
		parent = panel,
		zIndex = 2,
	})
	Theme.label({
		text = Loc.get("treatment.title"),
		size = UDim2.new(1, -Theme.space.md * 2 - 22, 0, 22),
		position = UDim2.fromOffset(Theme.space.md + 22, Theme.space.sm),
		textSize = Theme.size.body,
		bold = true,
		parent = panel,
		zIndex = 2,
	})

	local required = Theme.frame({
		name = "Gereken",
		size = UDim2.new(1, -Theme.space.md * 2, 0, 66),
		position = UDim2.fromOffset(Theme.space.md, 34),
		color = Theme.color.panelDeep,
		transparency = 0.25,
		radius = Theme.radius.sm,
		parent = panel,
		zIndex = 2,
	})
	Theme.padding(required, Theme.space.sm)
	Theme.list(required, Theme.space.xs)

	local buttons = Theme.scroller(
		panel,
		UDim2.new(1, -Theme.space.md * 2, 1, -114),
		UDim2.fromOffset(Theme.space.md, 108)
	)
	buttons.ZIndex = 2
	Theme.grid(buttons, CELL, UDim2.fromOffset(Theme.space.sm, Theme.space.sm))

	widgets = { panel = panel, required = required, buttons = buttons }
end

--- Bir tedavi karesi: ikon, ad, fiyat rozeti ve uygulanma halkasi.
local function treatmentCell(parent: Instance, treatment: any, index: number): any
	local button = Theme.button({
		name = treatment.id,
		text = "",
		size = CELL,
		layoutOrder = index,
		parent = parent,
		zIndex = 2,
		onClick = function()
			Net.toServer("ApplyTreatment", treatment.id)
		end,
	})

	local icon = Theme.icon({
		name = treatment.id,
		size = 20,
		color = Theme.color.accentGlow,
		position = UDim2.new(0.5, 0, 0, 8),
		anchor = Vector2.new(0.5, 0),
		parent = button,
		zIndex = 3,
	})

	Theme.label({
		name = "Ad",
		text = Loc.get(treatment.nameKey),
		size = UDim2.new(1, -Theme.space.sm, 0, 14),
		position = UDim2.fromOffset(Theme.space.xs, 32),
		textSize = Theme.size.micro,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = button,
		zIndex = 3,
	})

	local cost = Theme.label({
		name = "Fiyat",
		text = Loc.get("treatment.cost", treatment.cost),
		size = UDim2.new(1, -Theme.space.sm, 0, 14),
		position = UDim2.fromOffset(Theme.space.xs, 46),
		textSize = Theme.size.micro,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.money,
		parent = button,
		zIndex = 3,
	})

	-- Uygulandiginda karenin ustunu kaplayan dolum; basta bos.
	local done = Theme.frame({
		name = "Uygulandi",
		size = UDim2.fromScale(0, 1),
		color = Theme.color.good,
		transparency = 0.78,
		radius = Theme.radius.sm,
		parent = button,
		zIndex = 2,
	})

	return { button = button, icon = icon, cost = cost, done = done, applied = false }
end

function TreatmentPanel.update(chart: any, state: any)
	if widgets == nil then
		return
	end

	local show = chart ~= nil and chart.present == true and chart.diagnosed == true
	if show ~= widgets.panel.Visible then
		if show then
			Theme.open(widgets.panel)
		else
			Theme.close(widgets.panel)
		end
	end
	if not show then
		builtSignature = ""
		return
	end

	-- Gereken tedaviler: her guncellemede yenileniyor (isaretler degisiyor).
	widgets.required:ClearAllChildren()
	Theme.padding(widgets.required, Theme.space.sm)
	Theme.list(widgets.required, Theme.space.xs)
	for index, item in chart.required do
		local row = Theme.frame({
			name = "Gerek",
			size = UDim2.new(1, 0, 0, 18),
			transparency = 1,
			radius = 0,
			parent = widgets.required,
			layoutOrder = index,
			zIndex = 3,
		})
		Theme.icon({
			name = if item.applied then "check" else "info",
			size = 12,
			color = if item.applied then Theme.color.good else Theme.color.textFaint,
			position = UDim2.fromOffset(0, 3),
			parent = row,
			zIndex = 4,
		})
		Theme.label({
			text = Loc.get(item.nameKey),
			size = UDim2.new(1, -20, 1, 0),
			position = UDim2.fromOffset(18, 0),
			textSize = Theme.size.micro,
			bold = item.applied,
			color = if item.applied then Theme.color.good else Theme.color.textDim,
			parent = row,
			zIndex = 4,
		})
	end

	-- Izgara yalnizca acilan tedavi kumesi degistiginde kuruluyor.
	if state == nil or state.ready ~= true then
		return
	end
	local signature = ""
	for _, treatment in state.treatments do
		signature ..= treatment.id .. ":" .. tostring(treatment.cost) .. ";"
	end
	if signature ~= builtSignature then
		builtSignature = signature
		cells = {}
		widgets.buttons:ClearAllChildren()
		Theme.grid(widgets.buttons, CELL, UDim2.fromOffset(Theme.space.sm, Theme.space.sm))
		for index, treatment in state.treatments do
			cells[treatment.id] = treatmentCell(widgets.buttons, treatment, index)
		end
	end

	-- Uygulanmis tedavilerin karesi doluyor. Dolum bir kez oynasin diye
	-- durum kareye kaydediliyor; her guncellemede tekrar tweenlenmiyor.
	local applied: { [string]: boolean } = {}
	for _, treatmentId in chart.done do
		applied[treatmentId] = true
	end
	for id, cell in cells do
		local isDone = applied[id] == true
		if isDone ~= cell.applied then
			cell.applied = isDone
			Theme.tween(cell.done, { Size = UDim2.fromScale(if isDone then 1 else 0, 1) }, Theme.motion.slow)
			Theme.tween(cell.icon, { Rotation = if isDone then 360 else 0 }, Theme.motion.slow)
		end
	end
end

return TreatmentPanel
