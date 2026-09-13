--!strict
-- LobbyPanel.luau
-- ===============
-- Lobi arayuzu: dort mod, sirada bekleyen sayisi ve geri sayim.
--
-- Secim SUNUCUDA tutuluyor; burasi yalnizca ciziyor ve niyet yolluyor.
-- Ayni moda ikinci kez basmak siradan cikmak demek.
--
-- Kapasite NOKTALARI
-- ------------------
-- "2 / 4" yazisi okunuyor ama hissedilmiyordu. Dort nokta, ikisi dolu:
-- kadroya kac kisi kaldigi bir bakista belli. Geri sayim da sayidan
-- once HALKA olarak gorunuyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")

local Audio = require(Shared.Audio)
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local LobbyPanel = {}

local widgets: any = nil
local cards: { [string]: any } = {}
local mine: string? = nil

function LobbyPanel.create(parent: ScreenGui)
	-- Lobinin arkasi tamamen koyu bir zemin: klinik arayuzunun hicbir
	-- parcasi lobide gorunmesin diye ayri bir ScreenGui zaten var;
	-- buradaki zemin yalnizca kartlari one cikariyor.
	local backdrop = Theme.frame({
		name = "Zemin",
		size = UDim2.fromScale(1, 1),
		color = Theme.color.panelDeep,
		transparency = 0.35,
		radius = 0,
		parent = parent,
		zIndex = 0,
	})
	backdrop.Visible = true

	local panel = Theme.card({
		name = "Lobi",
		size = UDim2.fromOffset(760, 360),
		position = UDim2.new(0.5, 0, 1, -32),
		anchor = Vector2.new(0.5, 1),
		parent = parent,
	}, Theme.color.accent)

	Theme.label({
		text = Loc.get("lobby.title"),
		size = UDim2.new(1, -Theme.space.xl * 2, 0, 32),
		position = UDim2.fromOffset(Theme.space.xl, 14),
		textSize = Theme.size.hero - 8,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
		zIndex = 2,
	})
	local subtitle = Theme.label({
		name = "AltBaslik",
		text = Loc.get("lobby.hint"),
		size = UDim2.new(1, -Theme.space.xl * 2, 0, 20),
		position = UDim2.fromOffset(Theme.space.xl, 48),
		textSize = Theme.size.small,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textDim,
		parent = panel,
		zIndex = 2,
	})

	local row = Theme.frame({
		name = "Modlar",
		size = UDim2.new(1, -Theme.space.xl * 2, 0, 232),
		position = UDim2.fromOffset(Theme.space.xl, 78),
		transparency = 1,
		radius = 0,
		parent = panel,
		zIndex = 2,
	})
	local layout = Theme.list(row, Theme.space.md, Enum.FillDirection.Horizontal)
	layout.HorizontalAlignment = Enum.HorizontalAlignment.Center

	local status = Theme.label({
		name = "Durum",
		text = "",
		size = UDim2.new(1, -Theme.space.xl * 2, 0, 22),
		position = UDim2.new(0, Theme.space.xl, 1, -30),
		textSize = Theme.size.small,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.accentGlow,
		parent = panel,
		zIndex = 2,
	})

	widgets = { panel = panel, row = row, status = status, subtitle = subtitle, backdrop = backdrop }
end

local function buildCard(mode: any, index: number)
	local card = Theme.card({
		name = mode.id,
		size = UDim2.fromOffset(172, 232),
		color = Theme.color.panelLight,
		radius = Theme.radius.lg,
		layoutOrder = index,
		parent = widgets.row,
		zIndex = 3,
	})
	local stroke = card:FindFirstChildOfClass("UIStroke") :: UIStroke
	stroke.Thickness = 2

	Theme.label({
		name = "Ad",
		text = Loc.get(mode.nameKey),
		size = UDim2.new(1, -Theme.space.md * 2, 0, 24),
		position = UDim2.fromOffset(Theme.space.md, 12),
		textSize = Theme.size.title,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = card,
		zIndex = 4,
	})

	-- Kapasite noktalari: kac kisi girdi, kac kisi kaldi.
	local dots = Theme.frame({
		name = "Kapasite",
		size = UDim2.new(1, -Theme.space.md * 2, 0, 14),
		position = UDim2.fromOffset(Theme.space.md, 42),
		transparency = 1,
		radius = 0,
		parent = card,
		zIndex = 4,
	})
	local dotLayout = Theme.list(dots, Theme.space.xs, Enum.FillDirection.Horizontal)
	dotLayout.HorizontalAlignment = Enum.HorizontalAlignment.Center
	local dotList: { Frame } = {}
	for slot = 1, mode.capacity do
		table.insert(dotList, Theme.frame({
			name = "Yuva" .. slot,
			size = UDim2.fromOffset(12, 12),
			color = Theme.color.strokeSoft,
			transparency = 0.3,
			radius = 6,
			parent = dots,
			layoutOrder = slot,
			zIndex = 5,
		}))
	end

	local count = Theme.label({
		name = "Sayac",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2, 0, 42),
		position = UDim2.fromOffset(Theme.space.md, 62),
		textSize = Theme.size.hero,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.accentGlow,
		parent = card,
		zIndex = 4,
	})

	-- Geri sayim halkasi sayacin arkasinda degil, altinda: ustune
	-- binmesi rakami okunmaz kilardi.
	local ring = Theme.ring(card, 54, UDim2.new(0.5, 0, 0, 136), Theme.color.accent, 18)
	ring.root.ZIndex = 4
	for _, dot in ring.dots do
		dot.ZIndex = 5
	end

	local waitText = Theme.label({
		name = "Bekleme",
		text = "",
		size = UDim2.new(1, -Theme.space.sm * 2, 0, 26),
		position = UDim2.fromOffset(Theme.space.sm, 162),
		textSize = Theme.size.micro,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textDim,
		wrapped = true,
		parent = card,
		zIndex = 4,
	})

	-- Siradaysa ayrilma, degilse katilma. Iki ayri remote: "ayni dugmeye
	-- ikinci kez basinca ne oluyor" belirsizligini birakmamak icin.
	local button = Theme.button({
		name = "Katil",
		text = Loc.get("lobby.join"),
		size = UDim2.new(1, -Theme.space.md * 2, 0, 34),
		position = UDim2.new(0, Theme.space.md, 1, -44),
		color = Theme.color.accentDark,
		textSize = Theme.size.small,
		parent = card,
		zIndex = 4,
		onClick = function()
			if mine == mode.id then
				Net.toServer("LeaveQueue")
			else
				Net.toServer("JoinQueue", mode.id)
			end
		end,
	})

	cards[mode.id] = {
		card = card,
		stroke = stroke,
		count = count,
		wait = waitText,
		ring = ring,
		dots = dotList,
		button = button,
	}
end

function LobbyPanel.update(state: any)
	if widgets == nil or state == nil then
		return
	end

	for index, mode in state.modes do
		if cards[mode.id] == nil then
			buildCard(mode, index)
		end
		local card = cards[mode.id]
		card.count.Text = ("%d / %d"):format(mode.waiting, mode.capacity)

		for slot, dot in card.dots do
			local filled = slot <= mode.waiting
			Theme.tween(dot, {
				BackgroundColor3 = if filled then Theme.color.accentGlow else Theme.color.strokeSoft,
				BackgroundTransparency = if filled then 0 else 0.3,
			}, Theme.motion.quick)
		end

		local isMine = state.mine == mode.id
		Theme.tween(card.stroke, {
			Color = if isMine then Theme.color.accentGlow else Theme.color.strokeSoft,
		}, Theme.motion.quick)
		Theme.tween(card.card, {
			BackgroundColor3 = if isMine then Theme.color.accentDark else Theme.color.panelLight,
		}, Theme.motion.quick)
		card.button.Text = Loc.get(if isMine then "lobby.leave" else "lobby.join")
		card.button.BackgroundColor3 = if isMine then Theme.color.bad else Theme.color.accentDark

		if mode.waiting == 0 then
			card.wait.Text = Loc.get("lobby.empty")
			Theme.setRing(card.ring, 0)
		elseif mode.waiting >= mode.capacity then
			card.wait.Text = Loc.get("lobby.starting")
			Theme.setRing(card.ring, 1)
			Theme.setRingColor(card.ring, Theme.color.good)
		else
			local seconds = math.ceil(mode.fallbackIn)
			card.wait.Text = Loc.get("lobby.waiting", seconds)
			-- Halka DOLARAK degil, BOSALARAK gidiyor: kalan sure azaldikca
			-- yanan nokta sayisi da azaliyor.
			Theme.setRing(card.ring, math.clamp(mode.fallbackIn / math.max(mode.fallbackTotal or 30, 1), 0, 1))
			Theme.setRingColor(card.ring, if seconds <= 5 then Theme.color.warn else Theme.color.accent)
		end
	end

	if state.mine ~= mine then
		Audio.play(if state.mine ~= nil then "queueJoin" else "queueLeave")
		mine = state.mine
	end

	widgets.status.Text = if state.mine ~= nil
		then Loc.get("lobby.inQueue", Loc.get(state.mineNameKey or "lobby.solo"))
		else Loc.get("lobby.pick")
	widgets.subtitle.Text = Loc.get("lobby.online", state.players)
end

function LobbyPanel.setVisible(visible: boolean)
	if widgets ~= nil then
		widgets.panel.Visible = visible
		widgets.backdrop.Visible = visible
	end
end

return LobbyPanel
