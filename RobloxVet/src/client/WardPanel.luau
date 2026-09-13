--!strict
-- WardPanel.luau
-- ==============
-- Kogus karti: yatan hastalar, iyilesme cubugu ve ILAC SAATI.
--
-- Neden HUD'da surekli duruyor?
-- -----------------------------
-- Yatan hasta arka planda isliyor: oyuncu baska bir hastayla ugrasirken
-- dozun saati geliyor. Acilip kapanan bir panel olsaydi oyuncu dozu
-- kacirdigini ancak ceza yedikten sonra ogrenirdi. Kart ekranin sol
-- altinda duruyor ve doz penceresi acilinca NABIZ atiyor.
--
-- Kogus BOSKEN kart tamamen gizli: bos bir kutu ekranda yer kaplamasin.

local RunService = game:GetService("RunService")
local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local WardPanel = {}

local widgets: any = nil
local rows: { any } = {}
local lastSnapshot: any = nil

local ROW_HEIGHT = 42

--- Tek bir padok satiri. Yatak numarasi sabit, icerigi degisiyor.
local function buildRow(parent: Instance, bed: number): any
	local root = Theme.frame({
		name = "Padok" .. bed,
		size = UDim2.new(1, 0, 0, ROW_HEIGHT),
		color = Theme.color.panelLight,
		transparency = 0.35,
		radius = Theme.radius.sm,
		parent = parent,
		layoutOrder = bed,
		zIndex = 2,
	})

	local number = Theme.label({
		name = "Numara",
		text = tostring(bed),
		size = UDim2.fromOffset(18, ROW_HEIGHT),
		position = UDim2.fromOffset(Theme.space.sm, 0),
		textSize = Theme.size.small,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textFaint,
		parent = root,
		zIndex = 3,
	})

	local name = Theme.label({
		name = "Ad",
		text = "",
		size = UDim2.new(1, -136, 0, 16),
		position = UDim2.fromOffset(30, 4),
		textSize = Theme.size.micro,
		bold = true,
		parent = root,
		zIndex = 3,
	})

	local status = Theme.label({
		name = "Durum",
		text = "",
		size = UDim2.new(1, -136, 0, 14),
		position = UDim2.fromOffset(30, 22),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		parent = root,
		zIndex = 3,
	})

	local bar = Theme.bar(
		root,
		UDim2.new(1, -142, 0, 4),
		UDim2.fromOffset(30, ROW_HEIGHT - 8),
		Theme.color.good,
		{ radius = 2 }
	)
	bar.track.ZIndex = 3
	bar.ghost.ZIndex = 3
	bar.fill.ZIndex = 4

	local button = Theme.button({
		name = "Ilac",
		text = Loc.get("ward.dose"),
		size = UDim2.fromOffset(96, 28),
		position = UDim2.new(1, -Theme.space.sm, 0.5, 0),
		anchor = Vector2.new(1, 0.5),
		textSize = Theme.size.micro,
		color = Theme.color.accentDark,
		parent = root,
		zIndex = 3,
		onClick = function()
			Net.toServer("GiveMedicine", bed)
		end,
	})

	return {
		root = root,
		number = number,
		name = name,
		status = status,
		bar = bar,
		button = button,
		pulse = nil :: Theme.Pulse?,
		due = false,
	}
end

function WardPanel.create(parent: ScreenGui)
	local panel = Theme.card({
		name = "Kogus",
		size = UDim2.fromOffset(320, 60),
		position = UDim2.new(0, Theme.space.lg, 1, -112),
		anchor = Vector2.new(0, 1),
		parent = parent,
		visible = false,
	})

	Theme.icon({
		name = "paw",
		size = 14,
		color = Theme.color.accent,
		position = UDim2.fromOffset(Theme.space.md, 11),
		parent = panel,
		zIndex = 2,
	})
	Theme.label({
		name = "Baslik",
		text = Loc.get("ward.title"),
		size = UDim2.new(1, -Theme.space.md * 2 - 22, 0, 18),
		position = UDim2.fromOffset(Theme.space.md + 22, Theme.space.sm),
		textSize = Theme.size.micro,
		bold = true,
		color = Theme.color.textDim,
		parent = panel,
		zIndex = 2,
	})

	local list = Theme.frame({
		name = "Padoklar",
		size = UDim2.new(1, -Theme.space.md * 2, 0, 0),
		position = UDim2.fromOffset(Theme.space.md, 30),
		transparency = 1,
		radius = 0,
		parent = panel,
		zIndex = 2,
	})
	Theme.list(list, Theme.space.xs)

	widgets = { panel = panel, list = list }
end

--- Kogus goruntusunu cizer. Sunucu MUTLAK zaman yolluyor; geri sayim
--- istemcide her karede hesaplaniyor, yani sunucu yalnizca bir sey
--- DEGISINCE gonderiyor.
local function draw()
	local snapshot = lastSnapshot
	if widgets == nil or snapshot == nil then
		return
	end
	local now = Workspace:GetServerTimeNow()

	-- Satirlar yatak sayisi kadar; yatak sayisi degismedikce yeniden
	-- kurulmuyor.
	if #rows ~= snapshot.total then
		for _, row in rows do
			row.root:Destroy()
		end
		rows = {}
		for bed = 1, snapshot.total do
			table.insert(rows, buildRow(widgets.list, bed))
		end
	end

	local busy = 0
	for _, info in snapshot.beds do
		local row = rows[info.bed]
		if row == nil then
			continue
		end
		row.root.Visible = info.busy == true
		if info.busy ~= true then
			if row.pulse ~= nil then
				row.pulse.stop()
				row.pulse = nil
			end
			row.due = false
			continue
		end
		busy += 1

		row.name.Text = Loc.get(info.nameKey)

		-- Doz penceresi: saatinden `doseWindow` once acilip ayni kadar
		-- sonra kapaniyor. Pencere aciksa dugme canli, degilse geri
		-- sayim yaziyor.
		local doseIn = info.doseAt - now
		local recoverIn = info.recoverAt - now
		local due = doseIn <= info.doseWindow
		row.button.Visible = due
		row.button.BackgroundColor3 = Theme.color.accentDark
		if due then
			row.status.Text = Loc.get("ward.doseNow")
			row.status.TextColor3 = Theme.color.warn
		else
			row.status.Text = Loc.get("ward.doseIn", math.max(0, math.ceil(doseIn)))
			row.status.TextColor3 = Theme.color.textDim
		end

		-- Iyilesme cubugu: kalan sure degil, TAMAMLANAN oran.
		local progress = 1 - math.clamp(recoverIn / math.max(1, info.recoverTotal), 0, 1)
		Theme.setBar(row.bar, progress)
		Theme.setBarColor(row.bar, if info.dosesMissed > 0 then Theme.color.warn else Theme.color.good)

		-- Doz saati gelince satir NABIZ atiyor: oyuncu baska bir
		-- hastayla ugrasirken bile kenarda hareketi fark ediyor.
		if due ~= row.due then
			row.due = due
			if row.pulse ~= nil then
				row.pulse.stop()
				row.pulse = nil
			end
			if due then
				row.pulse = Theme.pulse(row.root, "BackgroundTransparency", 0.05, 0.5)
			end
		end
	end

	-- Panel yuksekligi dolu padok sayisina gore: bos satir yer
	-- kaplamasin.
	local height = 30 + busy * (ROW_HEIGHT + Theme.space.xs) + Theme.space.sm
	widgets.list.Size = UDim2.new(1, -Theme.space.md * 2, 0, busy * (ROW_HEIGHT + Theme.space.xs))
	widgets.panel.Size = UDim2.fromOffset(320, height)

	local show = busy > 0
	if show ~= widgets.panel.Visible then
		if show then
			Theme.open(widgets.panel)
		else
			Theme.close(widgets.panel)
		end
	end
end

--- Sunucudan gelen kogus goruntusu.
function WardPanel.update(snapshot: any)
	lastSnapshot = snapshot
	draw()
end

function WardPanel.start()
	-- Geri sayim her karede taze: sunucunun yeni bir paket yollamasini
	-- beklemiyor.
	RunService.RenderStepped:Connect(draw)
end

function WardPanel.snapshot(): any
	return lastSnapshot
end

return WardPanel
