--!strict
-- LobbyPanel.luau
-- ===============
-- Lobi arayuzu: dort mod, sirada bekleyen sayisi ve geri sayim.
--
-- Secim SUNUCUDA tutuluyor; burasi yalnizca ciziyor ve niyet yolluyor.
-- Ayni moda ikinci kez basmak siradan cikmak demek.

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
	local panel = Theme.frame({
		name = "Lobi",
		size = UDim2.fromOffset(720, 320),
		position = UDim2.new(0.5, 0, 1, -28),
		anchor = Vector2.new(0.5, 1),
		transparency = 0.04,
		parent = parent,
	})
	Theme.stroke(panel, Theme.color.accent, 2)

	Theme.label({
		text = Loc.get("lobby.title"),
		size = UDim2.new(1, -32, 0, 30),
		position = UDim2.fromOffset(16, 12),
		textSize = 22,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
	})
	local subtitle = Theme.label({
		name = "AltBaslik",
		text = Loc.get("lobby.hint"),
		size = UDim2.new(1, -32, 0, 20),
		position = UDim2.fromOffset(16, 42),
		textSize = 13,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textDim,
		parent = panel,
	})

	local row = Theme.frame({
		name = "Modlar",
		size = UDim2.new(1, -32, 0, 200),
		position = UDim2.fromOffset(16, 72),
		transparency = 1,
		radius = 0,
		parent = panel,
	})
	local layout = Theme.list(row, 10, Enum.FillDirection.Horizontal)
	layout.HorizontalAlignment = Enum.HorizontalAlignment.Center

	local status = Theme.label({
		name = "Durum",
		text = "",
		size = UDim2.new(1, -32, 0, 20),
		position = UDim2.new(0, 16, 1, -26),
		textSize = 13,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.accent,
		parent = panel,
	})

	widgets = { panel = panel, row = row, status = status, subtitle = subtitle }
end

local function buildCard(mode: any, index: number)
	local card = Theme.frame({
		name = mode.id,
		size = UDim2.fromOffset(162, 200),
		color = Theme.color.panelLight,
		transparency = 0.1,
		radius = 10,
		layoutOrder = index,
		parent = widgets.row,
	})
	local stroke = Theme.stroke(card, Theme.color.stroke, 2)

	Theme.label({
		name = "Ad",
		text = Loc.get(mode.nameKey),
		size = UDim2.new(1, -16, 0, 26),
		position = UDim2.fromOffset(8, 10),
		textSize = 18,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = card,
	})
	local count = Theme.label({
		name = "Sayac",
		text = "",
		size = UDim2.new(1, -16, 0, 46),
		position = UDim2.fromOffset(8, 40),
		textSize = 34,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.accent,
		parent = card,
	})
	local waitText = Theme.label({
		name = "Bekleme",
		text = "",
		size = UDim2.new(1, -16, 0, 32),
		position = UDim2.fromOffset(8, 90),
		textSize = 11,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textDim,
		wrapped = true,
		parent = card,
	})
	local bar = Theme.bar(card, UDim2.new(1, -24, 0, 8), UDim2.fromOffset(12, 126), Theme.color.accent)
	-- Siradaysa ayrilma, degilse katilma. Iki ayri remote: "ayni dugmeye
	-- ikinci kez basinca ne oluyor" belirsizligini birakmamak icin.
	local button = Theme.button({
		name = "Katil",
		text = Loc.get("lobby.join"),
		size = UDim2.new(1, -24, 0, 32),
		position = UDim2.new(0, 12, 1, -44),
		color = Theme.color.accentDark,
		parent = card,
		onClick = function()
			if mine == mode.id then
				Net.toServer("LeaveQueue")
			else
				Net.toServer("JoinQueue", mode.id)
			end
		end,
	})

	cards[mode.id] = { card = card, stroke = stroke, count = count, wait = waitText, bar = bar, button = button }
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
		Theme.setBar(card.bar, mode.waiting / math.max(mode.capacity, 1))

		local isMine = state.mine == mode.id
		card.stroke.Color = if isMine then Theme.color.accent else Theme.color.stroke
		card.button.Text = Loc.get(if isMine then "lobby.leave" else "lobby.join")
		card.button.BackgroundColor3 = if isMine then Theme.color.bad else Theme.color.accentDark

		if mode.waiting == 0 then
			card.wait.Text = Loc.get("lobby.empty")
		elseif mode.waiting >= mode.capacity then
			card.wait.Text = Loc.get("lobby.starting")
		else
			card.wait.Text = Loc.get("lobby.waiting", math.ceil(mode.fallbackIn))
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
	end
end

return LobbyPanel
